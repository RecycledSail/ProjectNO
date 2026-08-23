using System;
using System.Collections;
using System.Collections.Generic;
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
    public void ResourceEconomicValues_MatchTheAuthoredUnequalBalancesExactly()
    {
        ProvinceWrapper provinces = ReadResource<ProvinceWrapper>("Provinces");
        NationWrapper nations = ReadResource<NationWrapper>("Nations");
        BuildingRecipeWrapper recipes = ReadResource<BuildingRecipeWrapper>("BuildingRecipes");

        Dictionary<string, (long Property, double LivingStandard)> expectedPopulations = new()
        {
            ["Bebino/Human/Paimon"] = (180000L, 1.20),
            ["Stein/Human/Paimon"] = (40000L, 0.90),
            ["Eiglepsk/Human/Paimon"] = (15000L, 0.80),
            ["Sando/Human/Paimon"] = (140000L, 1.00),
            ["Sando/Elf/Lisa"] = (30000L, 0.70),
            ["Talem/Human/Paimon"] = (60000L, 0.75),
            ["Uzyda/Human/Paimon"] = (65000L, 0.80),
            ["Buske/Human/Paimon"] = (55000L, 0.75),
            ["Svovoda/Human/Paimon"] = (90000L, 0.90),
            ["Tarantsusi/Human/Paimon"] = (80000L, 0.85),
            ["Jojisha/Human/Paimon"] = (110000L, 0.95),
            ["Matz/Human/Paimon"] = (90000L, 0.85),
            ["Zilia/Human/Paimon"] = (70000L, 0.80),
            ["Prano/Human/Paimon"] = (75000L, 0.80),
            ["Meril/Human/Paimon"] = (100000L, 0.95),
            ["Ozisk/Human/Paimon"] = (60000L, 0.75),
        };
        Dictionary<string, (long Property, double LivingStandard)> actualPopulations =
            provinces.provinces.SelectMany(province => province.pops.Select(population => new
                {
                    Key = $"{province.name}/{population.name}/{population.culture}",
                    Value = (population.property, population.livingStandard),
                }))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        Assert.That(actualPopulations, Is.EqualTo(expectedPopulations));

        Assert.That(nations.nations.ToDictionary(nation => nation.name, nation => nation.initialBalance),
            Is.EqualTo(new Dictionary<string, long>
            {
                ["Nation1"] = 300000L,
                ["Nation2"] = 220000L,
                ["Nation3"] = 160000L,
            }));

        Assert.That(recipes.buildingrecipes.ToDictionary(recipe => recipe.name, recipe => recipe.initialCapital),
            Is.EqualTo(new Dictionary<string, long>
            {
                ["WheatField"] = 5000L,
                ["LogField"] = 5000L,
                ["SlimeFarm"] = 7000L,
                ["IronMine"] = 8000L,
                ["SwordSmith"] = 15000L,
                ["FurnitureShop"] = 10000L,
                ["Stable"] = 9000L,
                ["GoldMine"] = 15000L,
                ["CoalMine"] = 8000L,
                ["GlassShop"] = 10000L,
                ["PaperShop"] = 9000L,
                ["BookShop"] = 9000L,
                ["PotionShop"] = 14000L,
                ["WoolField"] = 6000L,
                ["ClothShop"] = 10000L,
                ["LeatherShop"] = 11000L,
                ["SilkField"] = 9000L,
                ["CatalystShop"] = 14000L,
                ["construcntionCompany"] = 20000L,
            }));
    }

    [Test]
    public void ResourceProvinceTreasuries_AreZeroForOwnedAndExactForNeutralProvinces()
    {
        ProvinceWrapper provinces = ReadResource<ProvinceWrapper>("Provinces");
        InitialProvinceWrapper ownership = ReadResource<InitialProvinceWrapper>("InitialProvinces");
        HashSet<string> ownedNames = ownership.initialProvinces
            .SelectMany(entry => entry.provinces)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(provinces.provinces.Where(province => ownedNames.Contains(province.name))
            .All(province => province.initialLocalTreasury == 0L), Is.True);
        Assert.That(provinces.provinces.Where(province => !ownedNames.Contains(province.name))
                .ToDictionary(province => province.name, province => province.initialLocalTreasury),
            Is.EqualTo(new Dictionary<string, long>
            {
                ["Tarantsusi"] = 50000L,
                ["Prano"] = 50000L,
                ["Meril"] = 50000L,
                ["Ozisk"] = 50000L,
            }));
    }

    [Test]
    public void LoadNations_NegativeInitialBalanceNamesRecordAndFieldBeforeCreatingAnyAccount()
    {
        const string firstName = "InvalidLoaderNationFirst";
        InvalidOperationException exception = AssertLoaderRejects(
            "LoadNations",
            "GlobalVariables+GameDataFormat+NationsWrapper",
            "{\"nations\":[" +
            "{\"id\":901,\"name\":\"InvalidLoaderNationFirst\",\"initialBalance\":10," +
            "\"researchNodeNames\":[],\"regiments\":[]}," +
            "{\"id\":902,\"name\":\"InvalidLoaderNationNegative\",\"initialBalance\":-1," +
            "\"researchNodeNames\":[],\"regiments\":[]}]}");

        Assert.That(exception.Message, Does.Contain("InvalidLoaderNationNegative"));
        Assert.That(exception.Message, Does.Contain("initialBalance"));
        Assert.That(RuntimeDictionary("NATIONS").Contains(firstName), Is.False);
    }

    [Test]
    public void LoadProvinces_NegativePopulationPropertyNamesRecordAndFieldBeforeCreatingAnyAccount()
    {
        const string firstName = "InvalidLoaderProvinceFirst";
        InvalidOperationException exception = AssertLoaderRejects(
            "LoadProvinces",
            "GlobalVariables+GameDataFormat+ProvincesWrapper",
            "{\"provinces\":[" +
            "{\"id\":901,\"name\":\"InvalidLoaderProvinceFirst\",\"initialLocalTreasury\":0," +
            "\"pops\":[],\"topography\":\"Plane\",\"buildings\":[],\"specialBuildings\":[]}," +
            "{\"id\":902,\"name\":\"InvalidLoaderProvinceNegative\",\"initialLocalTreasury\":0," +
            "\"pops\":[{\"name\":\"Human\",\"population\":[0,1,0,0],\"culture\":\"Paimon\"," +
            "\"property\":-1,\"livingStandard\":1.0}],\"topography\":\"Plane\"," +
            "\"buildings\":[],\"specialBuildings\":[]}]}");

        Assert.That(exception.Message, Does.Contain("InvalidLoaderProvinceNegative"));
        Assert.That(exception.Message, Does.Contain("property"));
        Assert.That(RuntimeDictionary("PROVINCES").Contains(firstName), Is.False);
    }

    [Test]
    public void LoadProvinces_MissingLivingStandardNamesRecordAndFieldBeforeCreatingAnyAccount()
    {
        InvalidOperationException exception = AssertLoaderRejects(
            "LoadProvinces",
            "GlobalVariables+GameDataFormat+ProvincesWrapper",
            "{\"provinces\":[{\"id\":903,\"name\":\"InvalidLoaderLivingStandard\"," +
            "\"initialLocalTreasury\":0,\"pops\":[{\"name\":\"Human\"," +
            "\"population\":[0,1,0,0],\"culture\":\"Paimon\",\"property\":1}]," +
            "\"topography\":\"Plane\",\"buildings\":[],\"specialBuildings\":[]}]}");

        Assert.That(exception.Message, Does.Contain("InvalidLoaderLivingStandard"));
        Assert.That(exception.Message, Does.Contain("livingStandard"));
        Assert.That(RuntimeDictionary("PROVINCES").Contains("InvalidLoaderLivingStandard"), Is.False);
    }

    [Test]
    public void LoadBuildingRecipes_MissingInitialCapitalNamesRecordAndFieldBeforeRegistration()
    {
        InvalidOperationException exception = AssertLoaderRejects(
            "LoadBuildingRecipes",
            "GlobalVariables+GameDataFormat+BuildingrecipesWrapper",
            "{\"buildingrecipes\":[{\"name\":\"InvalidLoaderRecipe\"," +
            "\"buildRequirements\":[],\"TimeToBuild\":1}]}");

        Assert.That(exception.Message, Does.Contain("InvalidLoaderRecipe"));
        Assert.That(exception.Message, Does.Contain("initialCapital"));
        Assert.That(RuntimeDictionary("BUILDING_RECIPE").Contains("InvalidLoaderRecipe"), Is.False);
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

    [Test]
    public void Initialize_NonfiniteLivingStandardNamesActorAndLeavesEconomyRetryable()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, double.NaN);

        InvalidOperationException exception = AssertInitializationFails(nation, province);

        Assert.That(exception.Message, Does.Contain("P1"));
        Assert.That(exception.Message, Does.Contain("livingStandard"));
        AssertUninitialized(nation, province, pop, 1000L, 300L);
    }

    [Test]
    public void Initialize_OwnedProvinceNonzeroLocalTreasuryNamesFieldAndLeavesEconomyRetryable()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "P1");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 25L);
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);

        InvalidOperationException exception = AssertInitializationFails(nation, province);

        Assert.That(exception.Message, Does.Contain("P1"));
        Assert.That(exception.Message, Does.Contain("initialLocalTreasury"));
        AssertUninitialized(nation, province, pop, 1000L, 300L);
    }

    [Test]
    public void Initialize_NeutralProvinceInsufficientCapitalLeavesEconomyRetryable()
    {
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 50L);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 1, 100L);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
                null,
                new object[]
                {
                    TestEconomyFactory.ListOf("Nation"),
                    TestEconomyFactory.ListOf("Province", province),
                }));

        Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(invocation.InnerException.Message, Does.Contain("Neutral province Prano"));
        Assert.That(invocation.InnerException.Message, Does.Contain("need 100"));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalLedger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "LocalTreasuryAccount"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "ActiveLedger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(pop, "Account"), "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(pop, "property"), Is.EqualTo(300L));
        Assert.That(ReflectionTestHelpers.Get(TestEconomyFactory.GetOnlyBuilding(province), "balance"), Is.Zero);
    }

    [Test]
    public void Initialize_ProvinceOwnedByExcludedNationNamesMissingAuthorityAndLeavesActorsRetryable()
    {
        object nation = TestEconomyFactory.NewNation("Excluded", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "Orphaned");
        ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
        object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
                null,
                new object[]
                {
                    TestEconomyFactory.ListOf("Nation"),
                    TestEconomyFactory.ListOf("Province", province),
                }));

        Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(invocation.InnerException.Message, Does.Contain("Orphaned"));
        Assert.That(invocation.InnerException.Message, Does.Contain("authority ledger"));
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

    private static InvalidOperationException AssertLoaderRejects(
        string methodName,
        string wrapperTypeName,
        string json)
    {
        Type wrapperType = ReflectionTestHelpers.Find(wrapperTypeName);
        object data = JsonUtility.FromJson(json, wrapperType);
        MethodInfo loader = ReflectionTestHelpers.Find("GlobalVariables").GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { wrapperType },
            null);
        Assert.That(loader, Is.Not.Null, $"Missing validated {methodName} data path");
        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => loader.Invoke(null, new[] { data }));
        Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
        return (InvalidOperationException)invocation.InnerException;
    }

    private static IDictionary RuntimeDictionary(string fieldName) =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static).GetValue(null);

    [Serializable]
    private sealed class ProvinceWrapper
    {
        public ProvinceData[] provinces;
    }

    [Serializable]
    private sealed class ProvinceData
    {
        public string name;
        public long initialLocalTreasury;
        public PopulationData[] pops;
    }

    [Serializable]
    private sealed class PopulationData
    {
        public string name;
        public string culture;
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
        public string name;
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
        public string name;
        public long initialCapital;
    }

    [Serializable]
    private sealed class InitialProvinceWrapper
    {
        public InitialProvinceData[] initialProvinces;
    }

    [Serializable]
    private sealed class InitialProvinceData
    {
        public string[] provinces;
    }
}
