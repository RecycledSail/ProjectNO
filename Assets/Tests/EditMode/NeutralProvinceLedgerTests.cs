using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

public class NeutralProvinceLedgerTests
{
    [Test]
    public void AbsorbNeutralProvince_MigratesCombinedSupplyWithoutChangingActorBalances()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 700L);
        object population = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);
        Initialize(nation, province);

        object nationalLedger = ReflectionTestHelpers.Get(nation, "Ledger");
        object localLedger = ReflectionTestHelpers.Get(province, "LocalLedger");
        object nationalTreasury = ReflectionTestHelpers.Get(nation, "Account");
        object localTreasury = ReflectionTestHelpers.Get(province, "LocalTreasuryAccount");
        object building = TestEconomyFactory.GetOnlyBuilding(province);
        object populationAccount = ReflectionTestHelpers.Get(population, "Account");
        object buildingAccount = ReflectionTestHelpers.Get(building, "Account");

        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Balance"), Is.EqualTo(500L));
        Assert.That(TryAbsorb(province, nation, out string error), Is.True, error);

        Assert.That(ReflectionTestHelpers.Get(localLedger, "MoneySupply"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(nationalLedger, "MoneySupply"), Is.EqualTo(2000L));
        Assert.That(ReflectionTestHelpers.Get(populationAccount, "Ledger"), Is.SameAs(nationalLedger));
        Assert.That(ReflectionTestHelpers.Get(buildingAccount, "Ledger"), Is.SameAs(nationalLedger));
        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Balance"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(nationalTreasury, "Balance"), Is.EqualTo(1500L));
        Assert.That(ReflectionTestHelpers.Get(population, "property"), Is.EqualTo(300L));
        Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(200L));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalLedger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "LocalTreasuryAccount"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "ActiveLedger"), Is.SameAs(nationalLedger));
        Assert.That(ReflectionTestHelpers.Get(province, "nation"), Is.SameAs(nation));
        Assert.That((bool)ReflectionTestHelpers.Call<bool>(nation, "HasProvinces", province), Is.True);
    }

    [Test]
    public void AbsorbNeutralProvince_ForeignActorAccountLeavesAllStateUnchanged()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 700L);
        object population = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);
        Initialize(nation, province);

        object foreignPopulation = TestEconomyFactory.AddPop(province, 0L, 1.0);
        object foreignAccount = ReflectionTestHelpers.Get(foreignPopulation, "Account");
        object foreignTreasury = ReflectionTestHelpers.New("MoneyAccount", "foreign:treasury", 0L);
        object foreignLedger = ReflectionTestHelpers.New("MoneyLedger", "foreign", null, foreignTreasury);
        ReflectionTestHelpers.Call<bool>(foreignLedger, "RegisterInitialAccount", foreignTreasury);
        ReflectionTestHelpers.Call<bool>(foreignLedger, "RegisterInitialAccount", foreignAccount);
        ReflectionTestHelpers.Call<object>(foreignLedger, "SealInitialization");

        object nationalLedger = ReflectionTestHelpers.Get(nation, "Ledger");
        object localLedger = ReflectionTestHelpers.Get(province, "LocalLedger");
        object localTreasury = ReflectionTestHelpers.Get(province, "LocalTreasuryAccount");
        object building = TestEconomyFactory.GetOnlyBuilding(province);
        object populationAccount = ReflectionTestHelpers.Get(population, "Account");
        object buildingAccount = ReflectionTestHelpers.Get(building, "Account");

        Assert.That(TryAbsorb(province, nation, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
        Assert.That(ReflectionTestHelpers.Get(nationalLedger, "MoneySupply"), Is.EqualTo(1000L));
        Assert.That(ReflectionTestHelpers.Get(localLedger, "MoneySupply"), Is.EqualTo(1000L));
        Assert.That(ReflectionTestHelpers.Get(foreignLedger, "MoneySupply"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Balance"), Is.EqualTo(500L));
        Assert.That(ReflectionTestHelpers.Get(population, "property"), Is.EqualTo(300L));
        Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(200L));
        Assert.That(ReflectionTestHelpers.Get(populationAccount, "Ledger"), Is.SameAs(localLedger));
        Assert.That(ReflectionTestHelpers.Get(buildingAccount, "Ledger"), Is.SameAs(localLedger));
        Assert.That(ReflectionTestHelpers.Get(foreignAccount, "Ledger"), Is.SameAs(foreignLedger));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalLedger"), Is.SameAs(localLedger));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalTreasuryAccount"), Is.SameAs(localTreasury));
        Assert.That(ReflectionTestHelpers.Get(province, "nation"), Is.Null);
        Assert.That((bool)ReflectionTestHelpers.Call<bool>(nation, "HasProvinces", province), Is.False);
    }

    private static void Initialize(object nation, object province)
    {
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
            .Invoke(null, new object[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });
    }

    private static bool TryAbsorb(object province, object nation, out string error)
    {
        MethodInfo method = ReflectionTestHelpers.Find("ProvinceCurrencyMigration").GetMethod(
            "TryAbsorbNeutralProvince", BindingFlags.Public | BindingFlags.Static);
        object[] arguments = { province, nation, null };
        bool result = (bool)method.Invoke(null, arguments);
        error = (string)arguments[2];
        return result;
    }
}
