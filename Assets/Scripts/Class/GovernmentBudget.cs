using System;
using System.Collections.Generic;
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

    public void PrintMoney()
    {
        if (!TryValidatePolicy(out long total, out long nextResearchFund)) return;
        MoneyLedger ledger = nation.Ledger;
        if (ledger == null ||
            !ledger.TryMint(nation, nation.Account, total, "policy issuance")) return;

        ApplyResearchPolicy(nextResearchFund);
        ApplyMilitarySalary();
        ApplyIndustrySubsidy();
        ApplyRealEstatePolicy();

        Policy = new PolicyAllocation();
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

    private bool TryValidatePolicy(out long total, out long nextResearchFund)
    {
        total = 0;
        nextResearchFund = nation.researchFund;
        if (Policy == null || Policy.ResearchFund < 0 || Policy.MilitarySalary < 0 ||
            Policy.RealEstateFund < 0 || Policy.IndustrySubsidy == null ||
            Policy.IndustrySubsidy.Values.Any(amount => amount < 0))
        {
            return false;
        }

        try
        {
            total = Policy.Total;
            nextResearchFund = checked(nation.researchFund + Policy.ResearchFund);
            return total > 0;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private void ApplyResearchPolicy(long nextResearchFund)
    {
        if (Policy.ResearchFund > 0)
            nation.researchFund = nextResearchFund;
    }

    private void ApplyMilitarySalary()
    {
        if (Policy.MilitarySalary <= 0) return;

        Dictionary<ProvinceEthnicPop, long> weights = new();
        try
        {
            foreach (Regiment regiment in nation.regiments)
            {
                if (regiment.location?.provinceEthnicPops == null) continue;
                int soldiers = regiment.GetUnitCount();
                if (soldiers <= 0) continue;

                foreach (ProvinceEthnicPop population in regiment.location.provinceEthnicPops)
                {
                    weights.TryGetValue(population, out long current);
                    weights[population] = checked(current + soldiers);
                }
            }

            if (weights.Count == 0 && nation.capital?.provinceEthnicPops != null)
            {
                foreach (ProvinceEthnicPop population in nation.capital.provinceEthnicPops)
                    if (population.population > 0)
                        weights[population] = population.population;
            }
        }
        catch (OverflowException)
        {
            return;
        }

        TryDistribute(Policy.MilitarySalary, weights, population => population.Account,
            population => population.Account.Id, "policy military salary", out _);
    }

    private void ApplyIndustrySubsidy()
    {
        if (Policy.IndustrySubsidy.Count == 0) return;

        foreach (KeyValuePair<string, long> policy in Policy.IndustrySubsidy
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            string typeName = policy.Key;
            long subsidy = policy.Value;
            if (subsidy <= 0) continue;

            Dictionary<Building, long> weights = new();
            try
            {
                foreach (Province province in nation.provinces)
                {
                    foreach (KeyValuePair<BuildingType, Building> entry in province.buildings)
                    {
                        if (entry.Key.name != typeName) continue;

                        Building building = entry.Value;
                        long capacity = Math.Max(1L,
                            checked(building.level * building.buildingType.workerNeeded));
                        double saturation = (double)building.currentWorkers / capacity;
                        long efficiencyWeight = saturation >= 0.9 ? 1L
                            : saturation >= 0.6 ? 2L
                            : 3L;
                        weights[building] = checked(
                            Math.Max(1, building.level) * efficiencyWeight);
                    }
                }
            }
            catch (OverflowException)
            {
                continue;
            }

            if (TryDistribute(subsidy, weights, building => building.Account,
                building => building.Account.Id, $"policy industry subsidy:{typeName}",
                out List<Building> fundedBuildings))
            {
                foreach (Building building in fundedBuildings)
                    building.HireWorkers();
            }
        }
    }

    private void ApplyRealEstatePolicy()
    {
        if (Policy.RealEstateFund <= 0) return;

        Dictionary<ProvinceEthnicPop, long> weights = new();
        foreach (Province province in nation.provinces)
            foreach (ProvinceEthnicPop population in province.provinceEthnicPops)
                if (population.population > 0)
                    weights[population] = population.population;

        if (!TryDistribute(Policy.RealEstateFund, weights,
            population => population.Account, population => population.Account.Id,
            "policy real estate", out List<ProvinceEthnicPop> fundedPopulations))
        {
            return;
        }

        double livingStandardBoost =
            (double)Policy.RealEstateFund / Math.Max(1L, nation.GDPAverage) * 0.1;
        foreach (ProvinceEthnicPop population in fundedPopulations)
            population.livingStandard = Math.Min(
                population.livingStandard + livingStandardBoost, 10.0);
    }

    private bool TryDistribute<T>(
        long amount,
        IReadOnlyDictionary<T, long> candidateWeights,
        Func<T, MoneyAccount> accountSelector,
        Func<T, string> stableKey,
        string reason,
        out List<T> fundedTargets)
    {
        fundedTargets = new List<T>();
        if (amount <= 0 || candidateWeights == null || nation.Ledger == null)
            return false;

        Dictionary<T, long> validWeights = candidateWeights
            .Where(pair => !ReferenceEquals(pair.Key, null) && pair.Value > 0 &&
                accountSelector(pair.Key)?.Ledger == nation.Ledger)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (validWeights.Count == 0)
            return false;

        Dictionary<T, long> allocations;
        try
        {
            allocations = ProportionalAllocator.Allocate(amount, validWeights, stableKey);
        }
        catch (Exception exception) when (
            exception is OverflowException || exception is ArgumentException)
        {
            return false;
        }

        List<MoneyTransferEntry> entries = new()
        {
            new MoneyTransferEntry(nation.Account, -amount)
        };
        foreach (KeyValuePair<T, long> allocation in allocations
            .OrderBy(pair => stableKey(pair.Key), StringComparer.Ordinal))
        {
            if (allocation.Value > 0)
                entries.Add(new MoneyTransferEntry(
                    accountSelector(allocation.Key), allocation.Value));
        }

        if (entries.Count <= 1 || !nation.Ledger.TryTransferBatch(entries, reason))
            return false;

        fundedTargets = allocations
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => stableKey(pair.Key), StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .ToList();
        return true;
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
}
