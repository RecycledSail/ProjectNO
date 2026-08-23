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

        foreach (Nation nation in nationList)
            InitializeNation(nation, provinceList.Where(province => province.nation == nation));

        foreach (Province province in provinceList.Where(province => province.nation == null))
            InitializeNeutralProvince(province);
    }

    private static void InitializeNation(Nation nation, IEnumerable<Province> provinces)
    {
        MoneyLedger ledger = new($"nation:{nation.name}", nation, nation.Account);
        nation.Ledger = ledger;

        RegisterInitial(ledger, nation.Account);
        foreach (Province province in provinces)
        {
            province.ActiveLedger = ledger;
            RegisterProvinceAccounts(ledger, province);
        }

        foreach (Province province in provinces)
            CapitalizeBuildings(ledger, nation.Account, province, $"Nation {nation.name}");

        ledger.SealInitialization();
        VerifyAudit(ledger, $"Nation {nation.name}");
        nation.governmentBudget.SetOpeningMoneySupply(ledger.MoneySupply);
    }

    private static void InitializeNeutralProvince(Province province)
    {
        MoneyAccount treasury = new($"province:{province.name}:treasury", province.initialLocalTreasury);
        MoneyLedger ledger = new($"province:{province.name}", null, treasury);
        province.LocalTreasuryAccount = treasury;
        province.LocalLedger = ledger;
        province.ActiveLedger = ledger;

        RegisterInitial(ledger, treasury);
        RegisterProvinceAccounts(ledger, province);
        CapitalizeBuildings(ledger, treasury, province, $"Neutral province {province.name}");

        ledger.SealInitialization();
        VerifyAudit(ledger, $"Neutral province {province.name}");
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

    private static void CapitalizeBuildings(
        MoneyLedger ledger,
        MoneyAccount treasury,
        Province province,
        string ownerDescription)
    {
        foreach (Building building in province.buildings.Values)
        {
            if (!GlobalVariables.BUILDING_RECIPE.TryGetValue(
                building.buildingType.name, out BuildingRecipe recipe))
                throw new InvalidOperationException(
                    $"{ownerDescription} has no recipe for {building.buildingType.name}.");

            long required = checked(recipe.InitialCapital * building.level);
            if (!ledger.TryTransfer(treasury, building.Account, required, "Starting building capital"))
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} cannot capitalize {building.buildingType.name} " +
                    $"level {building.level}" +
                    (province.nation == null ? string.Empty : $" in {province.name}") +
                    $": need {required}, have {treasury.Balance}.");
            }
        }
    }

    private static void VerifyAudit(MoneyLedger ledger, string ownerDescription)
    {
        if (!ledger.Audit(out _))
            throw new InvalidOperationException($"{ownerDescription} ledger failed its opening audit.");
    }
}
