using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public class PolicyAllocation
{
    public long ResearchFund = 0;
    public long MilitarySalary = 0;
    public Dictionary<string, long> IndustrySubsidy = new();
    public long RealEstateFund = 0;

    public long Total
    {
        get
        {
            checked
            {
                return ResearchFund + MilitarySalary + IndustryTotal() + RealEstateFund;
            }
        }
    }

    public long IndustryTotal()
    {
        long total = 0;
        checked
        {
            foreach (long amount in IndustrySubsidy.Values)
                total += amount;
        }
        return total;
    }
}

public class GovernmentBudget
{
    public NationMarket market;
    public Nation nation;

    public long MoneySupply => nation.Ledger.MoneySupply;
    public long WeeklyTaxRevenue => nation.Ledger.WeeklyTaxRevenue;
    public int SalesTaxBasisPoints
    {
        get => nation.Ledger.SalesTaxBasisPoints;
        set => nation.Ledger.SalesTaxBasisPoints = value;
    }

    public float InflationRate { get; private set; }
    public float CurrentPriceIndex { get; private set; }
    public PolicyAllocation Policy { get; set; } = new();
    public long PendingMoneyPrint => Policy.Total;

    private readonly Queue<float> _priceIndexHistory = new();
    private const int PRICE_HISTORY_WEEKS = 4;

    public GovernmentBudget(NationMarket market, Nation nation)
    {
        this.market = market;
        this.nation = nation;
    }

    public bool PrintMoney()
    {
        if (!TryBuildPolicyApplicationPlan(out PolicyApplicationPlan plan) ||
            !ReferenceEquals(Policy, plan.PolicyRecord))
        {
            return false;
        }

        if (!nation.Ledger.TryIssueAndTransferBatches(
            nation,
            nation.Account,
            plan.IssueAmount,
            "policy issuance",
            plan.TransferBatches))
        {
            return false;
        }

        nation.researchFund = plan.NextResearchFund;
        foreach (KeyValuePair<Building, long> effect in plan.NextWorkerCounts)
            effect.Key.currentWorkers = effect.Value;
        foreach (KeyValuePair<ProvinceEthnicPop, double> effect in plan.NextLivingStandards)
            effect.Key.livingStandard = effect.Value;

        Policy = new PolicyAllocation();
        return true;
    }

    public void UpdateInflation()
    {
        float priceIndex = CalculatePriceIndex();
        CurrentPriceIndex = priceIndex;

        _priceIndexHistory.Enqueue(priceIndex);
        if (_priceIndexHistory.Count > PRICE_HISTORY_WEEKS)
            _priceIndexHistory.Dequeue();

        if (_priceIndexHistory.Count >= 2 && _priceIndexHistory.Peek() > 0f)
        {
            float oldest = _priceIndexHistory.Peek();
            InflationRate = (priceIndex - oldest) / oldest * 100f;
        }
        else
        {
            InflationRate = 0f;
        }
    }

    public string GetInflationStatus()
    {
        if (InflationRate > 10f) return "Hyperinflation";
        if (InflationRate > 5f) return "High Inflation";
        if (InflationRate > 2f) return "Inflation";
        if (InflationRate > 0f) return "Mild Rise";
        if (InflationRate < -5f) return "Deflation";
        if (InflationRate < -2f) return "Mild Fall";
        return "Stable";
    }

    public void SetIndustrySubsidyTotal(long totalAmount)
    {
        Policy.IndustrySubsidy.Clear();
        if (totalAmount <= 0) return;

        SortedSet<string> typeNames = new(StringComparer.Ordinal);
        foreach (Province province in nation.provinces)
            foreach (BuildingType buildingType in province.buildings.Keys)
                typeNames.Add(buildingType.name);

        if (typeNames.Count == 0) return;

        long perType = totalAmount / typeNames.Count;
        long remainder = totalAmount - perType * typeNames.Count;
        bool first = true;
        foreach (string typeName in typeNames)
        {
            Policy.IndustrySubsidy[typeName] = first ? perType + remainder : perType;
            first = false;
        }
    }

    private bool TryBuildPolicyApplicationPlan(out PolicyApplicationPlan plan)
    {
        plan = null;
        PolicyAllocation policy = Policy;
        MoneyLedger ledger = nation.Ledger;
        if (policy == null || ledger == null || !ledger.OwnsAccount(nation.Account) ||
            policy.ResearchFund < 0 || policy.MilitarySalary < 0 ||
            policy.RealEstateFund < 0 || policy.IndustrySubsidy == null ||
            policy.IndustrySubsidy.Values.Any(amount => amount < 0))
        {
            return false;
        }

        try
        {
            long issueAmount = policy.Total;
            if (issueAmount <= 0) return false;

            _ = checked(ledger.MoneySupply + issueAmount);
            _ = checked(nation.Account.Balance + issueAmount);
            long nextResearchFund = checked(nation.researchFund + policy.ResearchFund);

            List<MoneyTransferBatch> batches = new();
            PlanDistribution(
                policy.MilitarySalary,
                BuildMilitaryWeights(),
                population => population.Account,
                population => population.Account.Id,
                "policy military salary",
                batches);

            Dictionary<Building, long> fundedBuildings = new();
            foreach (KeyValuePair<string, long> subsidy in policy.IndustrySubsidy
                .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (subsidy.Value <= 0) continue;
                Dictionary<Building, long> weights = BuildIndustryWeights(subsidy.Key);
                Dictionary<Building, long> allocations = PlanDistribution(
                    subsidy.Value,
                    weights,
                    building => building.Account,
                    building => building.Account.Id,
                    $"policy industry subsidy:{subsidy.Key}",
                    batches);
                foreach (KeyValuePair<Building, long> allocation in allocations)
                    if (allocation.Value > 0)
                        fundedBuildings.Add(allocation.Key, allocation.Value);
            }

            Dictionary<ProvinceEthnicPop, long> realEstateAllocations = PlanDistribution(
                policy.RealEstateFund,
                BuildRealEstateWeights(),
                population => population.Account,
                population => population.Account.Id,
                "policy real estate",
                batches);

            Dictionary<MoneyAccount, long> aggregateDeltas = ValidateMonetaryPlan(
                ledger, issueAmount, batches);
            Dictionary<Building, long> nextWorkerCounts = PlanWorkerEffects(
                fundedBuildings.Keys, aggregateDeltas);
            Dictionary<ProvinceEthnicPop, double> nextLivingStandards =
                PlanLivingStandardEffects(policy.RealEstateFund, realEstateAllocations);

            plan = new PolicyApplicationPlan(
                policy,
                issueAmount,
                nextResearchFund,
                batches,
                nextWorkerCounts,
                nextLivingStandards);
            return true;
        }
        catch (Exception exception) when (
            exception is OverflowException ||
            exception is ArgumentException ||
            exception is InvalidOperationException)
        {
            return false;
        }
    }

    private Dictionary<ProvinceEthnicPop, long> BuildMilitaryWeights()
    {
        Dictionary<ProvinceEthnicPop, long> weights = new();
        foreach (Regiment regiment in nation.regiments)
        {
            if (regiment?.location?.provinceEthnicPops == null) continue;
            int soldiers = regiment.GetUnitCount();
            if (soldiers <= 0) continue;

            foreach (ProvinceEthnicPop population in regiment.location.provinceEthnicPops)
            {
                if (population == null) continue;
                weights.TryGetValue(population, out long current);
                weights[population] = checked(current + soldiers);
            }
        }

        if (weights.Count == 0 && nation.capital?.provinceEthnicPops != null)
            foreach (ProvinceEthnicPop population in nation.capital.provinceEthnicPops)
                if (population != null && population.population > 0)
                    weights[population] = population.population;

        return weights;
    }

    private Dictionary<Building, long> BuildIndustryWeights(string typeName)
    {
        Dictionary<Building, long> weights = new();
        foreach (Province province in nation.provinces)
        {
            if (province?.buildings == null) continue;
            foreach (KeyValuePair<BuildingType, Building> entry in province.buildings)
            {
                Building building = entry.Value;
                if (entry.Key?.name != typeName || building?.Account?.Ledger != nation.Ledger)
                    continue;
                if (building.level < 0 || building.buildingType == null ||
                    building.buildingType.workerNeeded < 0 || building.currentWorkers < 0)
                {
                    throw new InvalidOperationException("Industry worker state is invalid.");
                }

                long capacity = Math.Max(1L,
                    checked(building.level * building.buildingType.workerNeeded));
                double saturation = (double)building.currentWorkers / capacity;
                long efficiencyWeight = saturation >= 0.9 ? 1L
                    : saturation >= 0.6 ? 2L
                    : 3L;
                weights.Add(building, checked(
                    Math.Max(1, building.level) * efficiencyWeight));
            }
        }
        return weights;
    }

    private Dictionary<ProvinceEthnicPop, long> BuildRealEstateWeights()
    {
        Dictionary<ProvinceEthnicPop, long> weights = new();
        foreach (Province province in nation.provinces)
        {
            if (province?.provinceEthnicPops == null) continue;
            foreach (ProvinceEthnicPop population in province.provinceEthnicPops)
                if (population != null && population.population > 0)
                    weights[population] = population.population;
        }
        return weights;
    }

    private Dictionary<T, long> PlanDistribution<T>(
        long amount,
        IReadOnlyDictionary<T, long> candidateWeights,
        Func<T, MoneyAccount> accountSelector,
        Func<T, string> stableKey,
        string reason,
        List<MoneyTransferBatch> batches)
    {
        if (amount <= 0) return new Dictionary<T, long>();

        Dictionary<T, long> validWeights = candidateWeights
            .Where(pair => !ReferenceEquals(pair.Key, null) && pair.Value > 0 &&
                accountSelector(pair.Key)?.Ledger == nation.Ledger)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (validWeights.Count == 0)
            return new Dictionary<T, long>();

        Dictionary<T, long> allocations = ProportionalAllocator.Allocate(
            amount, validWeights, stableKey);
        List<MoneyTransferEntry> entries = new()
        {
            new MoneyTransferEntry(nation.Account, -amount)
        };
        foreach (KeyValuePair<T, long> allocation in allocations
            .OrderBy(pair => stableKey(pair.Key), StringComparer.Ordinal))
            if (allocation.Value > 0)
                entries.Add(new MoneyTransferEntry(
                    accountSelector(allocation.Key), allocation.Value));

        if (entries.Count <= 1)
            throw new InvalidOperationException("A funded policy channel has no recipients.");
        batches.Add(new MoneyTransferBatch(entries, reason));
        return allocations;
    }

    private static Dictionary<MoneyAccount, long> ValidateMonetaryPlan(
        MoneyLedger ledger,
        long issueAmount,
        IReadOnlyList<MoneyTransferBatch> batches)
    {
        Dictionary<MoneyAccount, long> aggregateDeltas = new()
        {
            [ledger.TreasuryAccount] = issueAmount
        };
        foreach (MoneyTransferBatch batch in batches)
        {
            if (batch?.Entries == null || batch.Entries.Count == 0)
                throw new InvalidOperationException("A policy transfer batch is empty.");

            long batchTotal = 0;
            long recordedAmount = 0;
            foreach (MoneyTransferEntry entry in batch.Entries)
            {
                if (entry?.Account == null || !ledger.OwnsAccount(entry.Account))
                    throw new InvalidOperationException("A policy account is not registered.");

                batchTotal = checked(batchTotal + entry.Delta);
                if (entry.Delta < 0)
                    recordedAmount = checked(recordedAmount - entry.Delta);
                aggregateDeltas.TryGetValue(entry.Account, out long current);
                aggregateDeltas[entry.Account] = checked(current + entry.Delta);
            }

            if (batchTotal != 0 || recordedAmount <= 0)
                throw new InvalidOperationException("A policy transfer is not representable.");
        }

        _ = checked(ledger.MoneySupply + issueAmount);
        foreach (KeyValuePair<MoneyAccount, long> delta in aggregateDeltas)
            if (checked(delta.Key.Balance + delta.Value) < 0)
                throw new InvalidOperationException("The policy treasury cannot fund its plan.");
        return aggregateDeltas;
    }

    private static Dictionary<Building, long> PlanWorkerEffects(
        IEnumerable<Building> fundedBuildings,
        IReadOnlyDictionary<MoneyAccount, long> aggregateDeltas)
    {
        Dictionary<Building, long> nextWorkerCounts = new();
        foreach (Building building in fundedBuildings)
        {
            if (building == null || building.Account == null || building.province == null ||
                building.buildingType == null || building.level < 0 ||
                building.buildingType.workerNeeded < 0 || building.currentWorkers < 0)
            {
                throw new InvalidOperationException("Industry worker state is invalid.");
            }

            aggregateDeltas.TryGetValue(building.Account, out long monetaryDelta);
            long plannedBalance = checked(building.Account.Balance + monetaryDelta);
            long capacity = checked(building.level * building.buildingType.workerNeeded);
            long nextWorkers = building.currentWorkers;
            if (plannedBalance > 0 && building.previousGain > 0 &&
                building.currentWorkers < capacity &&
                building.province.population > building.province.hiredPopulation)
            {
                long available = checked(capacity - building.currentWorkers);
                long hired = Math.Min(available, 50L);
                nextWorkers = checked(building.currentWorkers + hired);
            }
            nextWorkerCounts.Add(building, nextWorkers);
        }
        return nextWorkerCounts;
    }

    private Dictionary<ProvinceEthnicPop, double> PlanLivingStandardEffects(
        long realEstateAmount,
        IReadOnlyDictionary<ProvinceEthnicPop, long> allocations)
    {
        Dictionary<ProvinceEthnicPop, double> nextLivingStandards = new();
        if (realEstateAmount <= 0) return nextLivingStandards;

        double boost = (double)realEstateAmount / Math.Max(1L, nation.GDPAverage) * 0.1;
        foreach (KeyValuePair<ProvinceEthnicPop, long> allocation in allocations)
            if (allocation.Value > 0)
                nextLivingStandards.Add(allocation.Key,
                    Math.Min(allocation.Key.livingStandard + boost, 10.0));
        return nextLivingStandards;
    }

    private float CalculatePriceIndex()
    {
        if (market.Products.Count == 0) return 1f;

        float weightedSum = 0f;
        long totalWeight = 0;
        foreach (ProductState product in market.Products.Values)
        {
            int weight = product.LastSupply + product.LastDemand;
            if (weight <= 0) continue;

            weightedSum += product.Price * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 1f;
    }

    private sealed class PolicyApplicationPlan
    {
        public PolicyAllocation PolicyRecord { get; }
        public long IssueAmount { get; }
        public long NextResearchFund { get; }
        public IReadOnlyList<MoneyTransferBatch> TransferBatches { get; }
        public IReadOnlyDictionary<Building, long> NextWorkerCounts { get; }
        public IReadOnlyDictionary<ProvinceEthnicPop, double> NextLivingStandards { get; }

        public PolicyApplicationPlan(
            PolicyAllocation policyRecord,
            long issueAmount,
            long nextResearchFund,
            IReadOnlyList<MoneyTransferBatch> transferBatches,
            IReadOnlyDictionary<Building, long> nextWorkerCounts,
            IReadOnlyDictionary<ProvinceEthnicPop, double> nextLivingStandards)
        {
            PolicyRecord = policyRecord;
            IssueAmount = issueAmount;
            NextResearchFund = nextResearchFund;
            TransferBatches = new ReadOnlyCollection<MoneyTransferBatch>(
                new List<MoneyTransferBatch>(transferBatches));
            NextWorkerCounts = new ReadOnlyDictionary<Building, long>(
                new Dictionary<Building, long>(nextWorkerCounts));
            NextLivingStandards = new ReadOnlyDictionary<ProvinceEthnicPop, double>(
                new Dictionary<ProvinceEthnicPop, double>(nextLivingStandards));
        }
    }
}
