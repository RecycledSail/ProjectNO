using System;
using System.Collections.Generic;
using System.Linq;

public static class EconomicInitializer
{
    public static void Initialize(IEnumerable<Nation> nations, IEnumerable<Province> provinces)
    {
        List<Nation> nationList = nations?.Where(nation => nation != null).Distinct().ToList()
            ?? new List<Nation>();
        List<Province> provinceList = provinces?.Where(province => province != null).Distinct().ToList()
            ?? new List<Province>();

        List<NationPlan> nationPlans = nationList.Select(nation =>
            PreflightNation(nation, provinceList.Where(province => province.nation == nation).ToList()))
            .ToList();
        List<NeutralProvincePlan> neutralPlans = provinceList
            .Where(province => province.nation == null)
            .Select(PreflightNeutralProvince)
            .ToList();

        foreach (NationPlan plan in nationPlans)
            ApplyNation(plan);
        foreach (NeutralProvincePlan plan in neutralPlans)
            ApplyNeutralProvince(plan);
    }

    private static NationPlan PreflightNation(Nation nation, List<Province> provinces)
    {
        string ownerDescription = $"Nation {nation.name}";
        return new NationPlan(
            nation,
            provinces,
            PreflightAccountsAndCapitalization(
                nation.Account, provinces, ownerDescription));
    }

    private static NeutralProvincePlan PreflightNeutralProvince(Province province)
    {
        string ownerDescription = $"Neutral province {province.name}";
        MoneyAccount treasury = new($"province:{province.name}:treasury", province.initialLocalTreasury);
        return new NeutralProvincePlan(
            province,
            treasury,
            PreflightAccountsAndCapitalization(
                treasury, new List<Province> { province }, ownerDescription));
    }

    private static List<BuildingCapitalization> PreflightAccountsAndCapitalization(
        MoneyAccount treasury,
        List<Province> provinces,
        string ownerDescription)
    {
        ValidateInitialAccounts(treasury, provinces, ownerDescription);

        List<BuildingCapitalization> capitalizations = new();
        long aggregateRequired = 0;
        foreach (Province province in provinces)
        {
            foreach (Building building in province.buildings.Values)
            {
                BuildingCapitalization capitalization = PreflightBuilding(
                    building, province, ownerDescription);
                capitalizations.Add(capitalization);

                try
                {
                    aggregateRequired = checked(aggregateRequired + capitalization.Amount);
                }
                catch (OverflowException)
                {
                    throw CapitalizationOverflow(capitalization);
                }

                if (aggregateRequired > treasury.Balance)
                    throw CapitalizationFailure(capitalization, aggregateRequired, treasury.Balance);
            }
        }

        return capitalizations;
    }

    private static void ValidateInitialAccounts(
        MoneyAccount treasury,
        List<Province> provinces,
        string ownerDescription)
    {
        HashSet<MoneyAccount> accounts = new();
        long openingSupply = 0;
        AddInitialAccount(treasury, accounts, ref openingSupply, ownerDescription);

        foreach (Province province in provinces)
        {
            foreach (ProvinceEthnicPop population in province.provinceEthnicPops)
                AddInitialAccount(population.Account, accounts, ref openingSupply, ownerDescription);

            foreach (Building building in province.buildings.Values)
            {
                if (building == null || building.Account.Balance != 0)
                    throw new InvalidOperationException(
                        $"{ownerDescription} has a non-empty starting building account.");
                AddInitialAccount(building.Account, accounts, ref openingSupply, ownerDescription);
            }
        }
    }

    private static void AddInitialAccount(
        MoneyAccount account,
        HashSet<MoneyAccount> accounts,
        ref long openingSupply,
        string ownerDescription)
    {
        if (account == null || account.Ledger != null || !accounts.Add(account))
            throw new InvalidOperationException(
                $"{ownerDescription} has an account that cannot be initialized.");

        try
        {
            openingSupply = checked(openingSupply + account.Balance);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(
                $"{ownerDescription} opening money supply exceeds Int64 capacity.");
        }
    }

    private static BuildingCapitalization PreflightBuilding(
        Building building,
        Province province,
        string ownerDescription)
    {
        if (building == null || building.buildingType == null ||
            !GlobalVariables.BUILDING_RECIPE.TryGetValue(
                building.buildingType.name, out BuildingRecipe recipe))
            throw new InvalidOperationException(
                $"{ownerDescription} has no recipe for {building?.buildingType?.name ?? "unknown building"}.");

        if (building.level < 0 || recipe.InitialCapital < 0)
            throw new InvalidOperationException(
                $"{ownerDescription} cannot capitalize {building.buildingType.name} " +
                $"level {building.level}: level and initial capital must be nonnegative.");

        try
        {
            return new BuildingCapitalization(
                building,
                province,
                ownerDescription,
                checked(recipe.InitialCapital * building.level));
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(
                $"{ownerDescription} cannot capitalize {building.buildingType.name} " +
                $"level {building.level}" + LocationSuffix(province, ownerDescription) +
                ": required capital exceeds Int64 capacity.");
        }
    }

    private static void ApplyNation(NationPlan plan)
    {
        MoneyLedger ledger = new($"nation:{plan.Nation.name}", plan.Nation, plan.Nation.Account);
        plan.Nation.Ledger = ledger;

        RegisterInitial(ledger, plan.Nation.Account);
        foreach (Province province in plan.Provinces)
        {
            province.ActiveLedger = ledger;
            RegisterProvinceAccounts(ledger, province);
        }

        ApplyCapitalizations(ledger, plan.Nation.Account, plan.Capitalizations);
        ledger.SealInitialization();
        VerifyAudit(ledger, $"Nation {plan.Nation.name}");
    }

    private static void ApplyNeutralProvince(NeutralProvincePlan plan)
    {
        MoneyLedger ledger = new($"province:{plan.Province.name}", null, plan.Treasury);
        plan.Province.LocalTreasuryAccount = plan.Treasury;
        plan.Province.LocalLedger = ledger;
        plan.Province.ActiveLedger = ledger;

        RegisterInitial(ledger, plan.Treasury);
        RegisterProvinceAccounts(ledger, plan.Province);
        ApplyCapitalizations(ledger, plan.Treasury, plan.Capitalizations);
        ledger.SealInitialization();
        VerifyAudit(ledger, $"Neutral province {plan.Province.name}");
    }

    private static void RegisterProvinceAccounts(MoneyLedger ledger, Province province)
    {
        foreach (ProvinceEthnicPop population in province.provinceEthnicPops)
            RegisterInitial(ledger, population.Account);
        foreach (Building building in province.buildings.Values)
            RegisterInitial(ledger, building.Account);
    }

    private static void RegisterInitial(MoneyLedger ledger, MoneyAccount account)
    {
        if (!ledger.RegisterInitialAccount(account))
            throw new InvalidOperationException($"Could not register initial account {account.Id}.");
    }

    private static void ApplyCapitalizations(
        MoneyLedger ledger,
        MoneyAccount treasury,
        List<BuildingCapitalization> capitalizations)
    {
        foreach (BuildingCapitalization capitalization in capitalizations)
        {
            if (capitalization.Amount == 0)
                continue;

            if (!ledger.TryTransfer(
                treasury,
                capitalization.Building.Account,
                capitalization.Amount,
                "Starting building capital"))
                throw new InvalidOperationException(
                    $"{capitalization.OwnerDescription} capitalization preflight changed unexpectedly.");
        }
    }

    private static InvalidOperationException CapitalizationFailure(
        BuildingCapitalization capitalization,
        long required,
        long available) =>
        new($"{capitalization.OwnerDescription} cannot capitalize " +
            $"{capitalization.Building.buildingType.name} level {capitalization.Building.level}" +
            LocationSuffix(capitalization.Province, capitalization.OwnerDescription) +
            $": need {required}, have {available}.");

    private static InvalidOperationException CapitalizationOverflow(
        BuildingCapitalization capitalization) =>
        new($"{capitalization.OwnerDescription} cannot capitalize " +
            $"{capitalization.Building.buildingType.name} level {capitalization.Building.level}" +
            LocationSuffix(capitalization.Province, capitalization.OwnerDescription) +
            ": required capital exceeds Int64 capacity.");

    private static string LocationSuffix(Province province, string ownerDescription) =>
        ownerDescription.StartsWith("Neutral province ") ? string.Empty : $" in {province.name}";

    private static void VerifyAudit(MoneyLedger ledger, string ownerDescription)
    {
        if (!ledger.Audit(out _))
            throw new InvalidOperationException($"{ownerDescription} ledger failed its opening audit.");
    }

    private sealed class NationPlan
    {
        public Nation Nation { get; }
        public List<Province> Provinces { get; }
        public List<BuildingCapitalization> Capitalizations { get; }

        public NationPlan(
            Nation nation,
            List<Province> provinces,
            List<BuildingCapitalization> capitalizations)
        {
            Nation = nation;
            Provinces = provinces;
            Capitalizations = capitalizations;
        }
    }

    private sealed class NeutralProvincePlan
    {
        public Province Province { get; }
        public MoneyAccount Treasury { get; }
        public List<BuildingCapitalization> Capitalizations { get; }

        public NeutralProvincePlan(
            Province province,
            MoneyAccount treasury,
            List<BuildingCapitalization> capitalizations)
        {
            Province = province;
            Treasury = treasury;
            Capitalizations = capitalizations;
        }
    }

    private sealed class BuildingCapitalization
    {
        public Building Building { get; }
        public Province Province { get; }
        public string OwnerDescription { get; }
        public long Amount { get; }

        public BuildingCapitalization(
            Building building,
            Province province,
            string ownerDescription,
            long amount)
        {
            Building = building;
            Province = province;
            OwnerDescription = ownerDescription;
            Amount = amount;
        }
    }
}
