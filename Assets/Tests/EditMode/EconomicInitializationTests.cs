using System;
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
