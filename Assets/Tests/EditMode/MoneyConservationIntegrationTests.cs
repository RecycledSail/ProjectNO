using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MoneyConservationIntegrationTests
{
    private const string Food = "money-conservation-food";
    private const string Input = "money-conservation-input";
    private const string Output = "money-conservation-output";
    private const string ProducerType = "MoneyConservationProducer";
    private const string CompanyType = "construcntionCompany";
    private const string ConstructionType = "MoneyConservationConstruction";

    private readonly Dictionary<string, object> _previousRecipes = new();
    private readonly Dictionary<string, object> _previousProducts = new();
    private object _previousBasicFoods;
    private bool _hadBasicFoods;

    [SetUp]
    public void ConfigureRepresentativeEconomyData()
    {
        IDictionary categories = StaticDictionary("CATEGORIES");
        _hadBasicFoods = categories.Contains("basic_food");
        _previousBasicFoods = _hadBasicFoods ? categories["basic_food"] : null;
        categories["basic_food"] = new List<string> { Food };

        SaveAndReplace(StaticDictionary("PRODUCTS"), _previousProducts,
            Food, ReflectionTestHelpers.New("Products", 10));
        SaveAndReplace(StaticDictionary("PRODUCTS"), _previousProducts,
            Input, ReflectionTestHelpers.New("Products", 10));
        SaveAndReplace(StaticDictionary("PRODUCTS"), _previousProducts,
            Output, ReflectionTestHelpers.New("Products", 25));

        SaveAndReplace(Recipes, _previousRecipes,
            ProducerType, NewRecipe(ProducerType, 500L));
        SaveAndReplace(Recipes, _previousRecipes,
            CompanyType, NewRecipe(CompanyType, 100L));
        object constructionRecipe = NewRecipe(ConstructionType, 300L, 10);
        ((IDictionary)ReflectionTestHelpers.Get(
            constructionRecipe, "requireItems"))[Output] = 1;
        SaveAndReplace(Recipes, _previousRecipes, ConstructionType, constructionRecipe);
    }

    [TearDown]
    public void RestoreRepresentativeEconomyData()
    {
        RestoreEntries(Recipes, _previousRecipes,
            ProducerType, CompanyType, ConstructionType);
        RestoreEntries(StaticDictionary("PRODUCTS"), _previousProducts,
            Food, Input, Output);

        IDictionary categories = StaticDictionary("CATEGORIES");
        if (_hadBasicFoods)
            categories["basic_food"] = _previousBasicFoods;
        else
            categories.Remove("basic_food");
    }

    [Test]
    public void RepresentativeDomesticWeek_ConservesRegisteredSupplyAcrossEveryFlow()
    {
        RepresentativeContext context = CreateRepresentativeContext();
        long capturedSupply = GetLong(context.Ledger, "MoneySupply");
        long startingTreasury = Balance(context.Treasury);

        Call(context.Province, "ProduceGoodsWeekly");
        Assert.That(Balance(context.Producer), Is.EqualTo(480L));
        Assert.That(Balance(context.InputSupplier), Is.EqualTo(18L));

        TransferProvinceProductionToNationMarket(context.Nation);
        InvokeEconomicEngine("ConsumeFoodsWeekly", context.Province, true);

        const long actualSettlementTax = 22L;
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"),
            Is.EqualTo(actualSettlementTax));
        Assert.That(GetLong(context.Budget, "WeeklyTaxRevenue"),
            Is.EqualTo(actualSettlementTax));
        Assert.That(Balance(context.Treasury) - startingTreasury,
            Is.EqualTo(actualSettlementTax));
        Assert.That(Balance(context.FirstPopulation), Is.EqualTo(190L));
        Assert.That(Balance(context.SecondPopulation), Is.EqualTo(190L));
        Assert.That(Balance(context.FirstPopulation) + Balance(context.SecondPopulation),
            Is.EqualTo(380L));

        object mandate = Call(context.Nation,
            "PlaceConstructionMandate", context.ConstructionBuildingType, context.Province);
        Assert.That(mandate, Is.Not.Null);
        object escrow = ReflectionTestHelpers.Get(mandate, "EscrowAccount");
        Assert.That(Balance(escrow), Is.EqualTo(300L));

        Call(context.ConstructionCompany, "ProgressWeekly", 10d);

        object completedBuilding = ((IDictionary)ReflectionTestHelpers.Get(
            context.Province, "buildings"))[context.ConstructionBuildingType];
        Assert.That(ReflectionTestHelpers.Get(mandate, "Status").ToString(),
            Is.EqualTo("Completed"));
        Assert.That(Balance(completedBuilding), Is.EqualTo(300L));
        Assert.That(Balance(escrow), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(escrow, "Ledger"), Is.Null);
        Assert.That(Balance(context.Treasury),
            Is.EqualTo(startingTreasury + actualSettlementTax - 300L));

        InvokeEconomicEngine("UpdateGDPWeekly",
            TestEconomyFactory.ListOf("Nation", context.Nation));

        Assert.That(GetLong(context.Nation, "GDP"), Is.EqualTo(1_180L));
        Assert.That(GetLong(context.Nation, "GDPAverage"), Is.EqualTo(1_180L));
        Assert.That(GetInt(Product(context.NationMarket, Food), "Stock"), Is.EqualTo(80));
        Assert.That(GetInt(Product(context.NationMarket, Input), "Stock"), Is.EqualTo(8));
        Assert.That(GetInt(Product(context.NationMarket, Output), "Stock"), Is.EqualTo(4));
        Assert.That(GetInt(Product(context.ProvinceMarket, Food), "Stock"), Is.Zero);
        Assert.That(GetInt(Product(context.ProvinceMarket, Input), "Stock"), Is.Zero);
        Assert.That(GetInt(Product(context.ProvinceMarket, Output), "Stock"), Is.Zero);
        Assert.That(OwnedQuantity(Product(context.NationMarket, Food),
            AccountId(context.FirstPopulation)), Is.EqualTo(40));
        Assert.That(OwnedQuantity(Product(context.NationMarket, Food),
            AccountId(context.SecondPopulation)), Is.EqualTo(40));
        Assert.That(OwnedQuantity(Product(context.NationMarket, Input),
            AccountId(context.InputSupplier)), Is.EqualTo(8));
        Assert.That(OwnedQuantity(Product(context.NationMarket, Output),
            AccountId(context.Producer)), Is.EqualTo(4));
        AssertEveryStockEqualsRegisteredOwnedStock(context);

        Assert.That(Balance(context.InputSupplier), Is.EqualTo(18L));
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(capturedSupply));
        Assert.That(GetLong(context.Budget, "MoneySupply"), Is.EqualTo(capturedSupply));
        Assert.That(Transactions(context.Ledger).Count(record =>
            ReflectionTestHelpers.Get(record, "Kind").ToString() == "Mint"), Is.Zero);
        AssertAudit(context.Ledger, capturedSupply);
    }

    [Test]
    public void ExplicitMint_IncreasesSupplyByExactlyTheIssuedAmount()
    {
        object nation = TestEconomyFactory.NewNation("MoneyConservationMintNation", 400L);
        Initialize(nation);
        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        object treasury = ReflectionTestHelpers.Get(nation, "Account");
        long capturedSupply = GetLong(ledger, "MoneySupply");

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryMint",
            nation, treasury, 75L, "explicit policy issuance"), Is.True);

        Assert.That(GetLong(ledger, "MoneySupply"), Is.EqualTo(capturedSupply + 75L));
        Assert.That(Balance(treasury), Is.EqualTo(475L));
        List<object> mints = Transactions(ledger).Where(record =>
            ReflectionTestHelpers.Get(record, "Kind").ToString() == "Mint").ToList();
        Assert.That(mints, Has.Count.EqualTo(1));
        Assert.That(GetLong(mints[0], "Amount"), Is.EqualTo(75L));
        AssertAudit(ledger, capturedSupply + 75L);
    }

    private static RepresentativeContext CreateRepresentativeContext()
    {
        object nation = TestEconomyFactory.NewNation("MoneyConservationNation", 5_000L);
        object province = TestEconomyFactory.NewProvince(10_001, "MoneyConservationProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);
        ReflectionTestHelpers.Set(nation, "capital", province);
        ReflectionTestHelpers.Set(province, "isConnectedToCapital", true);

        object provinceMarket = ReflectionTestHelpers.New(
            "ProvinceMarket", "MoneyConservationProvince");
        ReflectionTestHelpers.Set(province, "market", provinceMarket);
        AddProduct(provinceMarket, Food, 10);
        AddProduct(provinceMarket, Input, 10);
        AddProduct(provinceMarket, Output, 25);
        object nationMarket = ReflectionTestHelpers.Get(nation, "market");
        AddProduct(nationMarket, Food, 10);
        AddProduct(nationMarket, Input, 10);
        AddProduct(nationMarket, Output, 25);

        object firstPopulation = AddPopulation(
            province, "MoneyConservationCultureA", 10_000, 200L);
        object secondPopulation = AddPopulation(
            province, "MoneyConservationCultureB", 10_000, 200L);
        Call(province, "InitializePopulation");

        object producerType = NewBuildingType(
            ProducerType,
            new Dictionary<string, int> { [Input] = 2 },
            new Dictionary<string, int> { [Output] = 4 },
            1L);
        object producer = AddBuilding(province, producerType, 1, 1L);
        object companyType = NewBuildingType(
            CompanyType,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            1L);
        object constructionCompany = AddBuilding(province, companyType, 1, 0L);
        object constructionBuildingType = NewBuildingType(
            ConstructionType,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            1L);

        Initialize(nation, province);
        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        object budget = ReflectionTestHelpers.Get(nation, "governmentBudget");
        ReflectionTestHelpers.Set(budget, "SalesTaxBasisPoints", 1_000);
        Call(ledger, "BeginWeek");

        object inputSupplier = ReflectionTestHelpers.New(
            "MoneyAccount", "supplier:money-conservation-input", 0L);
        Assert.That(ReflectionTestHelpers.Call<bool>(
            ledger, "RegisterEmptyAccount", inputSupplier), Is.True);
        Call(Product(provinceMarket, Input), "AddSupply", inputSupplier, 10);

        return new RepresentativeContext(
            nation,
            province,
            provinceMarket,
            nationMarket,
            firstPopulation,
            secondPopulation,
            producer,
            constructionCompany,
            constructionBuildingType,
            inputSupplier,
            ledger,
            budget);
    }

    private static object AddPopulation(
        object province, string cultureName, int count, long openingBalance)
    {
        object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
        ReflectionTestHelpers.Set(species, "name", "Human");
        object culture = ReflectionTestHelpers.New("Culture", cultureName);
        object group = ReflectionTestHelpers.New("EthnicGroup", species, culture);
        object population = ReflectionTestHelpers.New(
            "ProvinceEthnicPop",
            province,
            group,
            new List<int> { 0, count, 0, 0 },
            openingBalance,
            1.0);
        ((IList)ReflectionTestHelpers.Get(
            province, "provinceEthnicPops")).Add(population);
        return population;
    }

    private static object NewBuildingType(
        string name,
        Dictionary<string, int> requiredItems,
        Dictionary<string, int> producedItems,
        long workerNeeded)
    {
        object type = ReflectionTestHelpers.New("BuildingType", name);
        ReflectionTestHelpers.Set(type, "requireItems", requiredItems);
        ReflectionTestHelpers.Set(type, "produceItems", producedItems);
        ReflectionTestHelpers.Set(type, "workerNeeded", workerNeeded);
        return type;
    }

    private static object AddBuilding(
        object province, object buildingType, int level, long workers)
    {
        object building = ReflectionTestHelpers.Find("BuildingFactory").GetMethod("Create")
            .Invoke(null, new[] { buildingType, province, (object)level, workers });
        ((IDictionary)ReflectionTestHelpers.Get(
            province, "buildings"))[buildingType] = building;
        return building;
    }

    private static object NewRecipe(string name, long capital, int timeToBuild = 1)
    {
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", name);
        ReflectionTestHelpers.Set(recipe, "InitialCapital", capital);
        ReflectionTestHelpers.Set(recipe, "TimeToBuild", timeToBuild);
        return recipe;
    }

    private static void AddProduct(object market, string name, int price) =>
        Call(market, "AddProduct", name, price);

    private static object Product(object market, string name) =>
        ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))[name];

    private static void Initialize(object nation, params object[] provinces) =>
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
            null,
            new[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", provinces)
            });

    private static void TransferProvinceProductionToNationMarket(object nation)
    {
        GameObject gameObject = new("MoneyConservationGameManager");
        try
        {
            object manager = gameObject.AddComponent(ReflectionTestHelpers.Find("GameManager"));
            MethodInfo transfer = manager.GetType().GetMethod(
                "TransferProvinceProductionToNationMarket",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(transfer, Is.Not.Null);
            transfer.Invoke(manager, new[] { nation });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void InvokeEconomicEngine(string method, params object[] arguments)
    {
        GameObject gameObject = new("MoneyConservationEconomicEngine");
        try
        {
            object engine = gameObject.AddComponent(ReflectionTestHelpers.Find("EconomicEngine"));
            engine.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
                .Invoke(engine, arguments);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void AssertEveryStockEqualsRegisteredOwnedStock(
        RepresentativeContext context)
    {
        IEnumerable<object> products = MarketProducts(context.ProvinceMarket)
            .Concat(MarketProducts(context.NationMarket));
        foreach (object product in products)
        {
            object inventory = ReflectionTestHelpers.Get(product, "Inventory");
            IEnumerable lots = (IEnumerable)ReflectionTestHelpers.Get(inventory, "Lots");
            int ownedQuantity = 0;
            foreach (object lot in lots)
            {
                object owner = ReflectionTestHelpers.Get(lot, "Key");
                Assert.That(ReflectionTestHelpers.Get(owner, "Ledger"),
                    Is.SameAs(context.Ledger));
                ownedQuantity = checked(ownedQuantity + GetInt(lot, "Value"));
            }

            Assert.That(GetInt(product, "Stock"), Is.EqualTo(ownedQuantity),
                $"Aggregate stock diverged from owned lots for " +
                ReflectionTestHelpers.Get(product, "ProductName"));
        }
    }

    private static IEnumerable<object> MarketProducts(object market) =>
        ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))
            .Values.Cast<object>();

    private static int OwnedQuantity(object product, string accountId)
    {
        object inventory = ReflectionTestHelpers.Get(product, "Inventory");
        IEnumerable lots = (IEnumerable)ReflectionTestHelpers.Get(inventory, "Lots");
        return lots.Cast<object>()
            .Where(lot => AccountId(ReflectionTestHelpers.Get(lot, "Key")) == accountId)
            .Sum(lot => GetInt(lot, "Value"));
    }

    private static void AssertAudit(object ledger, long expectedRegisteredBalance)
    {
        object[] arguments = { null };
        bool audited = (bool)ledger.GetType().GetMethod("Audit").Invoke(ledger, arguments);
        Assert.That(audited, Is.True);
        Assert.That((long)arguments[0], Is.EqualTo(expectedRegisteredBalance));
    }

    private static List<object> Transactions(object ledger) =>
        ((IEnumerable)ReflectionTestHelpers.Get(ledger, "Transactions"))
            .Cast<object>().ToList();

    private static string AccountId(object owner)
    {
        object account = owner.GetType().Name == "MoneyAccount"
            ? owner
            : ReflectionTestHelpers.Get(owner, "Account");
        return (string)ReflectionTestHelpers.Get(account, "Id");
    }

    private static long Balance(object owner)
    {
        object account = owner.GetType().Name == "MoneyAccount"
            ? owner
            : ReflectionTestHelpers.Get(owner, "Account");
        return GetLong(account, "Balance");
    }

    private static int GetInt(object instance, string member) =>
        (int)ReflectionTestHelpers.Get(instance, member);

    private static long GetLong(object instance, string member) =>
        (long)ReflectionTestHelpers.Get(instance, member);

    private static object Call(object instance, string method, params object[] arguments) =>
        ReflectionTestHelpers.Call<object>(instance, method, arguments);

    private static IDictionary Recipes => StaticDictionary("BUILDING_RECIPE");

    private static IDictionary StaticDictionary(string fieldName) =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static void SaveAndReplace(
        IDictionary dictionary,
        IDictionary<string, object> previous,
        string key,
        object replacement)
    {
        previous[key] = dictionary.Contains(key) ? dictionary[key] : null;
        dictionary[key] = replacement;
    }

    private static void RestoreEntries(
        IDictionary dictionary,
        IReadOnlyDictionary<string, object> previous,
        params string[] keys)
    {
        foreach (string key in keys)
        {
            if (previous.TryGetValue(key, out object value) && value != null)
                dictionary[key] = value;
            else
                dictionary.Remove(key);
        }
    }

    private sealed class RepresentativeContext
    {
        public object Nation { get; }
        public object Province { get; }
        public object ProvinceMarket { get; }
        public object NationMarket { get; }
        public object FirstPopulation { get; }
        public object SecondPopulation { get; }
        public object Producer { get; }
        public object ConstructionCompany { get; }
        public object ConstructionBuildingType { get; }
        public object InputSupplier { get; }
        public object Ledger { get; }
        public object Budget { get; }
        public object Treasury => ReflectionTestHelpers.Get(Nation, "Account");

        public RepresentativeContext(
            object nation,
            object province,
            object provinceMarket,
            object nationMarket,
            object firstPopulation,
            object secondPopulation,
            object producer,
            object constructionCompany,
            object constructionBuildingType,
            object inputSupplier,
            object ledger,
            object budget)
        {
            Nation = nation;
            Province = province;
            ProvinceMarket = provinceMarket;
            NationMarket = nationMarket;
            FirstPopulation = firstPopulation;
            SecondPopulation = secondPopulation;
            Producer = producer;
            ConstructionCompany = constructionCompany;
            ConstructionBuildingType = constructionBuildingType;
            InputSupplier = inputSupplier;
            Ledger = ledger;
            Budget = budget;
        }
    }
}
