using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static SaveManager;

// Persistence-only access stays inside the owning classes. Loading never calls
// gameplay actions (minting, payroll, procurement, or construction completion).
public sealed partial class MoneyLedger
{
    internal IEnumerable<MoneyAccount> SaveAccounts => _registeredAccounts;
    internal void RestoreStatistics(LedgerData data)
    {
        GameSaveState.Require(data.supply == MoneySupply && Audit(out _), "Money supply mismatch.");
        GameSaveState.Require(data.weeklyTax >= 0, "Invalid weekly tax.");
        SalesTaxBasisPoints = data.taxRate;
        WeeklyTaxRevenue = data.weeklyTax;
        _transactions.Clear();
        foreach (var item in data.transactions)
        {
            GameSaveState.Require(item.amount >= 0 && Enum.IsDefined(typeof(MoneyTransactionKind), item.kind), "Invalid transaction.");
            _transactions.Add(new MoneyTransactionRecord(item.kind, item.sources, item.destinations, item.amount, item.reason));
        }
        SealInitialization();
    }
}

public partial class GovernmentBudget
{
    internal BudgetData CaptureBudget() => new()
    {
        research = Policy.ResearchFund, military = Policy.MilitarySalary, realEstate = Policy.RealEstateFund,
        subsidies = GameSaveState.Amounts(Policy.IndustrySubsidy),
        inflation = InflationRate, priceIndex = CurrentPriceIndex, priceHistory = _priceIndexHistory.ToList()
    };

    internal void RestoreBudget(BudgetData data)
    {
        GameSaveState.Require(data != null && data.research >= 0 && data.military >= 0 && data.realEstate >= 0,
            "Invalid policy budget.");
        Policy = new PolicyAllocation
        {
            ResearchFund = data.research, MilitarySalary = data.military, RealEstateFund = data.realEstate,
            IndustrySubsidy = GameSaveState.AmountMap(data.subsidies)
        };
        _ = Policy.Total;
        GameSaveState.Require(Policy.IndustrySubsidy.Values.All(v => v >= 0) && data.priceHistory.Count <= 4 &&
            GameSaveState.Finite(data.inflation) && GameSaveState.Finite(data.priceIndex) &&
            data.priceHistory.All(v => GameSaveState.Finite(v)), "Invalid budget history.");
        InflationRate = data.inflation;
        CurrentPriceIndex = data.priceIndex;
        _priceIndexHistory.Clear();
        foreach (float value in data.priceHistory) _priceIndexHistory.Enqueue(value);
    }
}

public sealed partial class ProvinceEmployment
{
    internal List<EmploymentData> CaptureEmployment() => records.SelectMany(row => row.Value.Select(pair =>
        new EmploymentData { building = row.Key.buildingType.name, pop = pair.Key.Account.Id, workers = pair.Value })).ToList();

    internal static void RestoreEmployment(Province province, ProvinceData data, long week)
    {
        var result = new ProvinceEmployment(province);
        result.Validate();
        GameSaveState.Require(data.lastPaidWeek >= -1 && data.lastPaidWeek <= week, "Invalid payroll week.");
        var pops = province.provinceEthnicPops.ToDictionary(p => p.Account.Id);
        foreach (var item in data.employment)
        {
            var building = province.buildings[GlobalVariables.BUILDING_TYPE[item.building]];
            var pop = pops[item.pop];
            GameSaveState.Require(item.workers >= 0, "Negative employment.");
            if (!result.records.TryGetValue(building, out var row))
                result.records.Add(building, row = new Dictionary<ProvinceEthnicPop, long>());
            row.Add(pop, item.workers);
        }
        foreach (var building in province.buildings.Values)
            GameSaveState.Require(result.WorkersAt(building) <= Capacity(building), "Employment exceeds building capacity.");
        foreach (var pop in pops.Values)
            GameSaveState.Require(result.Employed(pop) <= pop.EmployablePopulation, "Employment exceeds population.");
        result.LastPaidWeek = data.lastPaidWeek;
        result.LastError = data.employmentError;
        province.Employment = result;
    }
}

internal sealed partial class ConstructionMaterials
{
    internal int SaveStartBasisPoints => _startBasisPoints;
    internal ConstructionMaterials(MandateData data)
    {
        _startBasisPoints = data.startBasisPoints;
        GameSaveState.Require(_startBasisPoints >= 0 && _startBasisPoints <= 10000 && data.materialSpending >= 0,
            "Invalid construction materials.");
        foreach (var pair in GameSaveState.AmountMap(data.required)) _required.Add(pair.Key, pair.Value);
        _acquired = GameSaveState.AmountMap(data.acquired);
        _consumed = GameSaveState.AmountMap(data.consumed);
        GameSaveState.Require(_required.Count == _acquired.Count && _required.Count == _consumed.Count,
            "Construction material keys do not match.");
        foreach (var pair in _required)
            GameSaveState.Require(pair.Value > 0 && _acquired[pair.Key] >= 0 && _acquired[pair.Key] <= pair.Value &&
                _consumed[pair.Key] >= 0 && _consumed[pair.Key] <= _acquired[pair.Key], "Invalid construction quantities.");
        Required = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(_required);
        Spending = data.materialSpending;
    }
}

public sealed partial class ConstructionMandate
{
    internal MandateData CaptureMandate() => new()
    {
        id = Id, investor = Investor?.InvestmentAccount.Id, type = BuildingType.name,
        province = TargetProvince.name, escrow = EscrowAccount?.Id, company = AssignedCompany?.Account.Id,
        capital = InvestedCapital, fee = ConstructionFee, paidFee = PaidConstructionFee,
        materialSpending = MaterialSpending, startBasisPoints = _materials.SaveStartBasisPoints,
        requiredManhours = RequiredManhours, completedManhours = _completedManhours.ToString(CultureInfo.InvariantCulture),
        status = Status, procurementFailed = ProcurementFailed,
        required = GameSaveState.Amounts(RequiredMaterials), acquired = GameSaveState.Amounts(AcquiredMaterials),
        consumed = GameSaveState.Amounts(ConsumedMaterials)
    };

    internal ConstructionMandate(MandateData data, IBuildingInvestor investor, BuildingType type,
        Province province, MoneyAccount escrow, ConstructionCompanyBuilding company)
    {
        GameSaveState.Require(!string.IsNullOrWhiteSpace(data.id) && investor != null &&
            GameSaveState.Finite(data.requiredManhours) && data.requiredManhours > 0 &&
            data.capital >= 0 && data.fee >= 0 && data.paidFee >= 0 && data.paidFee <= data.fee &&
            Enum.IsDefined(typeof(ConstructionMandateStatus), data.status), "Invalid construction contract.");
        Id = data.id;
        Investor = investor;
        BuildingType = type;
        TargetProvince = province;
        EscrowAccount = escrow;
        AssignedCompany = company;
        InvestedCapital = data.capital;
        ConstructionFee = data.fee;
        PaidConstructionFee = data.paidFee;
        RequiredManhours = data.requiredManhours;
        _requiredManhours = (decimal)data.requiredManhours;
        _completedManhours = decimal.Parse(data.completedManhours, CultureInfo.InvariantCulture);
        GameSaveState.Require(_completedManhours >= 0 && _completedManhours <= _requiredManhours, "Invalid construction progress.");
        RemainingManhours = (double)(_requiredManhours - _completedManhours);
        Status = data.status;
        ProcurementFailed = data.procurementFailed;
        _materials = new ConstructionMaterials(data);
        GameSaveState.Require(!IsActive || Status == ConstructionMandateStatus.Requested || company != null,
            "Assigned construction has no company.");
        if (IsActive && escrow != null)
            GameSaveState.Require(escrow.Id == Id && escrow.Ledger == province.ActiveLedger &&
                investor.InvestmentAccount.Ledger == escrow.Ledger &&
                // Materials are paid from the investor account, not the escrow.
                escrow.Balance == checked(checked(InvestedCapital + ConstructionFee) - PaidConstructionFee),
                "Construction escrow mismatch.");
        // Static tracking is installed only after the entire save has validated.
    }

    internal static void ResetTracking(IEnumerable<ConstructionMandate> mandates)
    {
        ActiveByProvince.Clear();
        foreach (var mandate in mandates.Where(m => m.IsActive)) TrackActiveMandate(mandate);
    }
}

public partial class ConstructionCompanyBuilding
{
    internal List<CompanySlotData> CaptureSlots() => _active.OrderBy(p => p.Key)
        .Select(p => new CompanySlotData { slot = p.Key, mandate = p.Value.Id }).ToList();

    internal void RestoreSlots(List<CompanySlotData> slots, Dictionary<string, ConstructionMandate> mandates)
    {
        foreach (var item in slots)
        {
            var mandate = mandates[item.mandate];
            GameSaveState.Require(item.slot >= 0 && item.slot < level && mandate.AssignedCompany == this &&
                !_active.ContainsValue(mandate), "Invalid construction company slot.");
            _active.Add(item.slot, mandate);
        }
    }
}

public partial class Nation
{
    internal void RestoreMandate(ConstructionMandate mandate) => _constructionMandates.Add(mandate);
}

public partial class Regiment
{
    // Does not advance the global counter during validation of an uncommitted save.
    internal Regiment(Nation nation, RegimentData data, Province location)
    {
        this.nation = nation;
        id = data.id;
        name = data.name;
        this.location = location;
        state = data.state;
        units = new();
        foreach (var squad in data.squads)
        {
            GameSaveState.Require(squad.capacity >= 0 && squad.population >= 0 && squad.population <= squad.capacity,
                "Invalid squad.");
            var type = GlobalVariables.UNIT_TYPE[squad.type];
            units.Add(type, new Squad(type, squad.capacity, squad.population));
        }
    }
}

public partial class BattleManager
{
    internal IEnumerable<Regiment> SaveRegiments => regiments;
    internal IEnumerable<Battle> SaveBattles => battleInProvinces.Values;
    internal void RestoreBattles(List<Regiment> restoredRegiments, Dictionary<Province, Battle> battles)
    {
        regiments = restoredRegiments;
        battleInProvinces = battles;
    }
}

public partial class GameManager
{
    internal long EmploymentWeek => employmentWeek;
    internal int SavedSpeed => timeSpeed;
    internal void RestoreClock(SaveDataFormat data)
    {
        year = data.year; month = data.month; day = data.day;
        dayoftheWeek = data.dayOfWeek; employmentWeek = data.employmentWeek;
        timeSpeed = data.speed; dayInterval = 1f / timeSpeed; paused = data.paused;
        roadConnectedProvinceCache.Clear();
        roadCacheValid.Clear();
    }
}
