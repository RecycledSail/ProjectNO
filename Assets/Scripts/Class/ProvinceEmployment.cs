using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>One authoritative employment register for a province's ordinary buildings.</summary>
public sealed partial class ProvinceEmployment
{
    private readonly Province province;
    private Dictionary<Building, Dictionary<ProvinceEthnicPop, long>> records = new();
    public long LastPaidWeek { get; private set; } = -1;
    public string LastError { get; private set; }
    public long TotalEmployed => records.Values.Sum(row => row.Values.Sum());

    private ProvinceEmployment(Province province) { this.province = province; }

    public static void Initialize(Province province)
    {
        if (province == null) throw new ArgumentNullException(nameof(province));
        if (province.Employment != null) return;
        var employment = new ProvinceEmployment(province);
        employment.Validate();
        var targets = province.buildings.Values.ToDictionary(b => b,
            b => Math.Min(Capacity(b), Math.Max(0, b.currentWorkers)));
        employment.records = employment.Plan(targets);
        province.Employment = employment;
    }

    public long WorkersAt(Building building) => records.TryGetValue(building, out var row) ? row.Values.Sum() : 0L;
    public long Employed(ProvinceEthnicPop pop) => records.Values.Sum(row => row.TryGetValue(pop, out long n) ? n : 0L);
    public IReadOnlyDictionary<ProvinceEthnicPop, long> GetWorkers(Building building) =>
        new ReadOnlyDictionary<ProvinceEthnicPop, long>(records.TryGetValue(building, out var row)
            ? new Dictionary<ProvinceEthnicPop, long>(row) : new Dictionary<ProvinceEthnicPop, long>());

    public void Reconcile()
    {
        Validate();
        var targets = province.buildings.Values.ToDictionary(b => b, b =>
            Math.Min(Capacity(b), WorkersAt(b)));
        records = Plan(targets, false);
    }

    public bool TryProcessWeek(long week)
    {
        if (week < 0 || week < LastPaidWeek) return false;
        if (week == LastPaidWeek) return true;
        try
        {
            Validate();
            var ledger = province.ActiveLedger;
            if (ledger == null || province.provinceEthnicPops.Any(p => !ledger.OwnsAccount(p.Account)) ||
                province.buildings.Values.Any(b => !ledger.OwnsAccount(b.Account)))
                throw new InvalidOperationException("Payroll accounts must belong to the province ledger.");

            var targets = new Dictionary<Building, long>();
            foreach (var building in province.buildings.Values)
            {
                long wage = building.buildingType.weeklyWage;
                if (wage <= 0) throw new InvalidOperationException("Weekly wage must be positive.");
                bool needsInputs = building.buildingType.requireItems?.Any(item => item.Value > 0) == true;
                long budget = needsInputs ? building.balance / 2 : building.balance;
                targets[building] = Math.Min(budget / wage,
                    Math.Min(Capacity(building), checked(WorkersAt(building) + 50L)));
            }
            var plan = Plan(targets);
            var transfers = new List<MoneyTransferEntry>();
            foreach (var row in plan)
            {
                long total = 0;
                foreach (var contract in row.Value)
                {
                    long amount = checked(contract.Value * row.Key.buildingType.weeklyWage);
                    if (amount == 0) continue;
                    total = checked(total + amount);
                    transfers.Add(new MoneyTransferEntry(contract.Key.Account, amount));
                }
                if (total > 0) transfers.Add(new MoneyTransferEntry(row.Key.Account, -total));
            }
            if (transfers.Count > 0 && !ledger.TryTransferBatch(transfers, $"Weekly wages: {province.name}, week {week}"))
                throw new InvalidOperationException("Payroll transfer batch was rejected.");
            records = plan;
            LastPaidWeek = week;
            LastError = null;
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is OverflowException)
        {
            LastError = error.Message;
            return false;
        }
    }

    private static long Capacity(Building building) => checked(building.level * building.buildingType.workerNeeded);

    private void Validate()
    {
        if (province.buildings == null || province.provinceEthnicPops == null)
            throw new InvalidOperationException("Employment requires buildings and population records.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pop in province.provinceEthnicPops)
        {
            if (pop == null || !ReferenceEquals(pop.province, province) || pop.Account == null ||
                !ids.Add(pop.Account.Id) || pop.ageGroups == null ||
                pop.ageGroups.Any(age => age == null || age.agepopulation < 0) || pop.EmployablePopulation < 0)
                throw new InvalidOperationException("Invalid or duplicate local employment population.");
        }
        ids.Clear();
        foreach (var entry in province.buildings)
        {
            var building = entry.Value;
            if (building == null || !ReferenceEquals(building.province, province) ||
                !ReferenceEquals(entry.Key, building.buildingType) || building.Account == null ||
                !ids.Add(building.Account.Id) || building.level < 0 || building.buildingType.workerNeeded < 0)
                throw new InvalidOperationException("Invalid or duplicate local employer.");
            _ = Capacity(building);
        }
    }

    private Dictionary<Building, Dictionary<ProvinceEthnicPop, long>> Plan(Dictionary<Building, long> targets, bool fillVacancies = true)
    {
        var pops = province.provinceEthnicPops.OrderBy(p => p.Account.Id, StringComparer.Ordinal).ToList();
        var buildings = targets.Keys.OrderBy(b => b.Account.Id, StringComparer.Ordinal).ToList();
        var plan = new Dictionary<Building, Dictionary<ProvinceEthnicPop, long>>();
        // Keep surviving contracts, trimming closed or downsized employers first.
        foreach (var building in buildings)
        {
            var row = pops.ToDictionary(p => p, p => records.TryGetValue(building, out var old) &&
                old.TryGetValue(p, out long count) ? count : 0L);
            long total = row.Values.Sum();
            plan[building] = ProportionalAllocator.Allocate(Math.Min(total, targets[building]), row, p => p.Account.Id);
        }
        // A demographic decline trims a population's contracts across all employers.
        foreach (var pop in pops)
        {
            var weights = buildings.ToDictionary(b => b, b => plan[b][pop]);
            long total = weights.Values.Sum();
            var kept = ProportionalAllocator.Allocate(Math.Min(total, pop.EmployablePopulation), weights, b => b.Account.Id);
            foreach (var building in buildings) plan[building][pop] = kept[building];
        }
        if (!fillVacancies) return plan;
        var available = pops.ToDictionary(p => p, p => p.EmployablePopulation - buildings.Sum(b => plan[b][p]));
        var vacancies = buildings.ToDictionary(b => b, b => targets[b] - plan[b].Values.Sum());
        long hires = Math.Min(available.Values.Sum(), vacancies.Values.Sum());
        var quotas = ProportionalAllocator.Allocate(hires, vacancies, b => b.Account.Id);
        foreach (var building in buildings)
        {
            var hired = ProportionalAllocator.Allocate(quotas[building], available, p => p.Account.Id);
            foreach (var pop in pops)
            {
                plan[building][pop] = checked(plan[building][pop] + hired[pop]);
                available[pop] -= hired[pop];
            }
        }
        return plan;
    }
}
