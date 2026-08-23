using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EconomicInitializationTests
{
    [Test]
    public void ResourcePopulationValues_AreNonnegativeWithPositiveLivingStandards()
    {
        ProvinceWrapper data = ReadResource<ProvinceWrapper>("Provinces");

        Assert.That(data.provinces.SelectMany(province => province.pops)
            .All(pop => pop.property >= 0 && pop.livingStandard > 0), Is.True);
    }

    [Test]
    public void ResourceNationBalances_AreNonnegative()
    {
        NationWrapper data = ReadResource<NationWrapper>("Nations");

        Assert.That(data.nations.All(nation => nation.initialBalance >= 0), Is.True);
    }

    [Test]
    public void ResourceRecipeCapital_IsPositive()
    {
        BuildingRecipeWrapper data = ReadResource<BuildingRecipeWrapper>("BuildingRecipes");

        Assert.That(data.buildingrecipes.All(recipe => recipe.initialCapital > 0), Is.True);
    }

    [Test]
    public void Initialize_CapitalizesStartingBuildingWithoutChangingSupply()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);
        object recipe = TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);

        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
            .Invoke(null, new object[] {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });

        object building = TestEconomyFactory.GetOnlyBuilding(province);
        Assert.That(ReflectionTestHelpers.Get(nation, "balance"), Is.EqualTo(800L));
        Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(200L));
        Assert.That(ReflectionTestHelpers.Get(pop, "property"), Is.EqualTo(300L));
        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(1300L));
        object budget = ReflectionTestHelpers.Get(nation, "governmentBudget");
        Assert.That(ReflectionTestHelpers.Get(budget, "MoneySupply"), Is.EqualTo(1300L));
    }

    [Test]
    public void Initialize_ThrowsWhenNationTreasuryCannotFundStartingBuildings()
    {
        object nation = TestEconomyFactory.NewNation("N1", 10L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
                .Invoke(null, new object[] {
                    TestEconomyFactory.ListOf("Nation", nation),
                    TestEconomyFactory.ListOf("Province", province)
                }));

        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(exception.InnerException.Message, Does.Contain("Nation N1"));
    }

    [Test]
    public void Initialize_CreatesAuditableNeutralProvinceLedger()
    {
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 200L);
        TestEconomyFactory.AddPop(province, 50L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 1, 100L);

        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
            .Invoke(null, new object[] {
                TestEconomyFactory.ListOf("Nation"),
                TestEconomyFactory.ListOf("Province", province)
            });

        object ledger = ReflectionTestHelpers.Get(province, "LocalLedger");
        Assert.That(ReflectionTestHelpers.Get(province, "ActiveLedger"), Is.SameAs(ledger));
        MethodInfo audit = ledger.GetType().GetMethod("Audit");
        object[] arguments = { null };
        Assert.That((bool)audit.Invoke(ledger, arguments), Is.True);
        Assert.That((long)arguments[0], Is.EqualTo(250L));
    }

    [Test]
    public void Initialize_RegistersLevelZeroBuildingWithoutCapitalizingIt()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        TestEconomyFactory.AddBuilding(province, "WheatField", 0, 100L);

        Initialize(nation, province);

        object building = TestEconomyFactory.GetOnlyBuilding(province);
        Assert.That(ReflectionTestHelpers.Get(nation, "balance"), Is.EqualTo(1000L));
        Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(0L));
        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(1000L));
    }

    [Test]
    public void Initialize_MissingLateRecipeLeavesNationalActorsRetryable()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 1, 100L);
        TestEconomyFactory.AddBuilding(province, "Missing", 1, 100L);
        Recipes().Remove("Missing");

        InvalidOperationException exception = AssertInitializationFails(nation, province);

        Assert.That(exception.Message, Does.Contain("Nation N1"));
        Assert.That(exception.Message, Does.Contain("Missing"));
        AssertUninitialized(nation, province, pop, 1000L, 300L);
    }

    [Test]
    public void Initialize_InsufficientAggregateFundsLeavesNationalActorsRetryable()
    {
        object nation = TestEconomyFactory.NewNation("N1", 150L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 1, 100L);
        TestEconomyFactory.AddBuilding(province, "LogField", 1, 100L);

        InvalidOperationException exception = AssertInitializationFails(nation, province);

        Assert.That(exception.Message, Does.Contain("Nation N1"));
        Assert.That(exception.Message, Does.Contain("need 200"));
        AssertUninitialized(nation, province, pop, 150L, 300L);
    }

    [Test]
    public void Initialize_OverflowingCapitalLeavesNationalActorsRetryable()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 1, 100L);
        TestEconomyFactory.AddBuilding(province, "Overflow", 2, long.MaxValue);

        InvalidOperationException exception = AssertInitializationFails(nation, province);

        Assert.That(exception.Message, Does.Contain("Nation N1"));
        Assert.That(exception.Message, Does.Contain("Overflow"));
        AssertUninitialized(nation, province, pop, 1000L, 300L);
    }

    private static void Initialize(object nation, object province)
    {
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
            .Invoke(null, new object[] {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });
    }

    private static InvalidOperationException AssertInitializationFails(object nation, object province)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Initialize(nation, province));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        return (InvalidOperationException)exception.InnerException;
    }

    private static IDictionary Recipes() =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static void AssertUninitialized(
        object nation,
        object province,
        object pop,
        long nationBalance,
        long populationProperty)
    {
        Assert.That(ReflectionTestHelpers.Get(nation, "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(nation, "Account"), "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "ActiveLedger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(pop, "Account"), "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(nation, "balance"), Is.EqualTo(nationBalance));
        Assert.That(ReflectionTestHelpers.Get(pop, "property"), Is.EqualTo(populationProperty));

        foreach (object building in ((IDictionary)ReflectionTestHelpers.Get(
            province, "buildings")).Values)
        {
            Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(0L));
            Assert.That(ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(building, "Account"), "Ledger"), Is.Null);
        }
    }

    private static T ReadResource<T>(string name)
    {
        string path = Path.Combine(Application.dataPath, "Resources", $"{name}.json");
        return JsonUtility.FromJson<T>(File.ReadAllText(path));
    }

    [Serializable]
    private sealed class ProvinceWrapper
    {
        public ProvinceData[] provinces;
    }

    [Serializable]
    private sealed class ProvinceData
    {
        public PopulationData[] pops;
    }

    [Serializable]
    private sealed class PopulationData
    {
        public long property;
        public double livingStandard;
    }

    [Serializable]
    private sealed class NationWrapper
    {
        public NationData[] nations;
    }

    [Serializable]
    private sealed class NationData
    {
        public long initialBalance;
    }

    [Serializable]
    private sealed class BuildingRecipeWrapper
    {
        public BuildingRecipeData[] buildingrecipes;
    }

    [Serializable]
    private sealed class BuildingRecipeData
    {
        public long initialCapital;
    }
}
