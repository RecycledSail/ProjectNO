using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WeeklyMarketSimulationTests
{
    private const string SharedProduct = "task9-shared-iron";
    private const string FactoryInput = "task9-factory-input";
    private const string FactoryOutput = "task9-factory-output";
    private const string FactoryType = "Task9Factory";
    private const string ProducerType = "Task9Producer";
    private const string CompanyType = "Task9Company";
    private const string ProjectType = "Task9Project";
    private const string MaterialFreeType = "Task9MaterialFree";

    private readonly Dictionary<string, object> previousRecipes = new();
    private object previousFoods;
    private bool hadFoods;

    [SetUp]
    public void SetUp()
    {
        IDictionary categories = StaticDictionary("CATEGORIES");
        hadFoods = categories.Contains("basic_food");
        previousFoods = hadFoods ? categories["basic_food"] : null;
        categories["basic_food"] = new List<string> { SharedProduct };

        SaveRecipe(FactoryType);
        SaveRecipe(ProducerType);
        SaveRecipe(CompanyType);
        SaveRecipe(ProjectType);
        SaveRecipe(MaterialFreeType);
    }

    [TearDown]
    public void TearDown()
    {
        IDictionary categories = StaticDictionary("CATEGORIES");
        if (hadFoods)
            categories["basic_food"] = previousFoods;
        else
            categories.Remove("basic_food");

        IDictionary recipes = StaticDictionary("BUILDING_RECIPE");
        foreach (KeyValuePair<string, object> entry in previousRecipes)
        {
            if (entry.Value == null)
                recipes.Remove(entry.Key);
            else
                recipes[entry.Key] = entry.Value;
        }
    }

    [Test]
    public void Process_ConfiguredTenPercentCapReachesEveryCollector()
    {
        using TestWorld world = new("Task9CapNation", true);
        world.AddPopulation(0, "configured-cap", 1_000, 1000L, 2d);
        world.AddFactory(0, FactoryType, SharedProduct, FactoryOutput, 1, 1000L);
        world.Initialize();
        world.PlaceProject(0, ProjectType, 10, (SharedProduct, 1));
        object product = world.AddNationalProduct(SharedProduct, 100, 1);

        object report = world.Process(false, ReflectionTestHelpers.New("MarketPriceSettings", 3000, 1000));

        Assert.That(ReportIds(report, "FailedMarketIds"), Is.Empty);
        Assert.That(Get(report, "OrderCount"), Is.EqualTo(3));
        // One unit for three rich buyers makes any adapter bidding 125 set the clearing price.
        Assert.That(Get(product, "LastClearingPrice"), Is.EqualTo(110));
        Assert.That(Get(product, "LastDemand"), Is.EqualTo(1));
        Assert.That(Get(product, "Price"), Is.EqualTo(110));
        world.AssertAudit();
    }

    [Test]
    public void Process_FailedPlannerDoesNotReportUnperformedBidSorts()
    {
        using TestWorld world = new("Task9SortNation", true);
        world.AddPopulation(0, "first", 1_000, 1000L, 2d);
        world.AddPopulation(0, "second", 1_000, 1000L, 2d);
        world.Initialize();
        world.AddNationalProduct(SharedProduct, 100, 1,
            ReflectionTestHelpers.New("MoneyAccount", "unregistered-sort-supplier", 0L));

        object report = world.Process(false);

        Assert.That(ReportIds(report, "FailedMarketIds"), Does.Contain("nation:Task9SortNation"));
        Assert.That(Get(report, "OrderCount"), Is.EqualTo(2));
        Assert.That(Get(report, "SortedOrderCount"), Is.Zero);
    }

    [Test]
    public void Process_ResidentsFactoriesAndConstructionClearOneSharedProductWithoutOrderBias()
    {
        SharedOutcome forward = RunSharedWorld(false);
        SharedOutcome reverse = RunSharedWorld(true);

        Assert.That(reverse.State, Is.EqualTo(forward.State));
        Assert.That(forward.OrderCount, Is.EqualTo(4));
        Assert.That(forward.SortedOrderCount, Is.EqualTo(4));
        Assert.That(forward.SuccessfulMarkets,
            Is.EqualTo(new[] { "nation:Task9Nation", "province:Task9P0", "province:Task9P1" }));
        Assert.That(forward.FailedMarkets, Is.Empty);
        Assert.That(forward.ElapsedTicks, Is.GreaterThanOrEqualTo(0L));
    }

    [Test]
    public void Process_IsolatedMarketFailureDoesNotRollbackSuccessfulNationalMarket()
    {
        using TestWorld world = new("Task9FailureNation", true, false);
        object connected = world.AddPopulation(0, "connected", 1_000, 20L, 2d);
        object isolated = world.AddPopulation(1, "isolated", 1_000, 20L, 2d);
        world.Initialize();
        object national = world.AddNationalProduct(SharedProduct, 10, 1);
        object corruptSupplier = ReflectionTestHelpers.New("MoneyAccount", "task9-unregistered", 0L);
        object local = world.AddLocalProduct(1, SharedProduct, 10, 1, corruptSupplier);
        string isolatedBefore = SnapshotProductAndPopulation(local, isolated);

        object report = world.Process(false);

        Assert.That(ReportIds(report, "SuccessfulMarketIds"), Does.Contain("nation:Task9FailureNation"));
        Assert.That(ReportIds(report, "FailedMarketIds"),
            Is.EqualTo(new[] { "province:Task9FailureP1" }));
        Assert.That(Get(national, "Stock"), Is.Zero);
        Assert.That(Get(national, "LastDemand"), Is.EqualTo(1));
        Assert.That(Get(connected, "livingStandard"), Is.EqualTo(2.01d).Within(0.000001d));
        Assert.That(SnapshotProductAndPopulation(local, isolated), Is.EqualTo(isolatedBefore));
        world.AssertAudit();
    }

    [Test]
    public void Process_ConstructionCanBuyMaterialProducedEarlierInTheSameWeek()
    {
        using TestWorld world = new("Task9ProductionNation", false);
        world.AddProducer(0, ProducerType, SharedProduct, 1, 100L);
        world.AddConstructionCompany(0, CompanyType, 100L);
        world.Initialize();
        object project = world.PlaceProject(0, ProjectType, 10, (SharedProduct, 1));

        world.Produce(0);
        Assert.That(Get(world.LocalProduct(0, SharedProduct), "LastSupply"), Is.EqualTo(1));
        object report = world.Process(false);

        Assert.That(ReportIds(report, "FailedMarketIds"), Is.Empty);
        Assert.That(Material(project, SharedProduct), Is.EqualTo(1L));
        Assert.That(Get(world.LocalProduct(0, SharedProduct), "Stock"), Is.Zero);
        world.AssertAudit();
    }

    [Test]
    public void Process_FactoryPurchasesBecomeInputsForNextWeeksProduction()
    {
        using TestWorld world = new("Task9FactoryNation", false);
        object factory = world.AddFactory(0, FactoryType, FactoryInput, FactoryOutput, 1, 100L);
        world.Initialize();
        object input = world.AddLocalProduct(0, FactoryInput, 10, 1);

        object report = world.Process(false);

        Assert.That(ReportIds(report, "FailedMarketIds"), Is.Empty);
        Assert.That(Inventory(factory, FactoryInput), Is.EqualTo(1L));
        Assert.That(Get(input, "Stock"), Is.Zero);
        Assert.That(world.HasLocalProduct(0, FactoryOutput), Is.False);

        world.Produce(0);

        Assert.That(Inventory(factory, FactoryInput), Is.Zero);
        Assert.That(Get(world.LocalProduct(0, FactoryOutput), "Stock"), Is.EqualTo(1));
        world.AssertAudit();
    }

    [Test]
    public void Process_ZeroStockDemandStillFinalizesUpwardPricePressure()
    {
        using TestWorld world = new("Task9PressureNation", true);
        object population = world.AddPopulation(0, "pressure", 1_000, 20L, 2d);
        world.Initialize();
        object product = world.AddNationalProduct(SharedProduct, 10, 0);

        object report = world.Process(false);

        Assert.That(ReportIds(report, "FailedMarketIds"), Is.Empty);
        Assert.That(Get(product, "RequestedDemand"), Is.EqualTo(1));
        Assert.That(Get(product, "UnmetDemand"), Is.EqualTo(1));
        Assert.That(Get(product, "LastDemand"), Is.Zero);
        Assert.That(Get(product, "LastClearingPrice"), Is.EqualTo(10));
        Assert.That(Get(product, "Price"), Is.GreaterThan(10));
        Assert.That(Get(population, "livingStandard"), Is.EqualTo(1.95d).Within(0.000001d));
        world.AssertAudit();
    }

    [Test]
    public void Process_OneAccountCannotReserveItsStartingBalanceInTwoMarkets()
    {
        using TestWorld world = new("Task9GlobalBudgetNation", true, false);
        world.Initialize();
        object nationalProject = world.PlaceProject(0, ProjectType, 10, (SharedProduct, 500));
        object isolatedProject = world.PlaceProject(1, ProjectType, 10, (SharedProduct, 500));
        world.AddNationalProduct(SharedProduct, 10, 500);
        object isolatedProduct = world.AddLocalProduct(1, SharedProduct, 10, 500);

        object report = world.Process(true);

        Assert.That(Get(report, "OrderCount"), Is.EqualTo(1));
        Assert.That(Material(nationalProject, SharedProduct), Is.EqualTo(500L));
        Assert.That(Material(isolatedProject, SharedProduct), Is.Zero);
        Assert.That(Get(isolatedProduct, "Stock"), Is.EqualTo(500));
        Assert.That(Get(isolatedProduct, "RequestedDemand"), Is.Zero);
        Assert.That(Get(isolatedProduct, "LastClearingPrice"), Is.EqualTo(10));
        Assert.That(Get(isolatedProduct, "Price"), Is.EqualTo(7));
        world.AssertAudit();
    }

    [Test]
    public void Process_MaterialFreeConstructionStillProgressesAfterEmptyMarketClearing()
    {
        using TestWorld world = new("Task9MaterialFreeNation", true);
        world.AddConstructionCompany(0, CompanyType, 100L);
        world.Initialize();
        object project = world.PlaceProject(0, MaterialFreeType, 10);

        object report = world.Process(false);
        world.Progress(10d);

        Assert.That(Get(report, "OrderCount"), Is.Zero);
        Assert.That(ReportIds(report, "FailedMarketIds"), Is.Empty);
        Assert.That(Get(project, "Status").ToString(), Is.EqualTo("Completed"));
        world.AssertAudit();
    }

    private SharedOutcome RunSharedWorld(bool reverse)
    {
        using TestWorld world = new("Task9Nation", true, true);
        object firstPopulation = world.AddPopulation(0, "first", 1_000, 100L, 2d);
        object secondPopulation = world.AddPopulation(1, "second", 1_000, 100L, 2d);
        object factory = world.AddFactory(0, FactoryType, SharedProduct, FactoryOutput, 4, 200L);
        world.AddConstructionCompany(0, CompanyType, 100L);
        world.Initialize();
        object project = world.PlaceProject(1, ProjectType, 10, (SharedProduct, 4));
        object product = world.AddNationalProduct(SharedProduct, 10, 5);

        object report = world.Process(reverse);
        world.AssertAudit();
        string state = string.Join("|", new[]
        {
            Get(firstPopulation, "livingStandard").ToString(),
            Get(secondPopulation, "livingStandard").ToString(),
            Inventory(factory, SharedProduct).ToString(),
            Material(project, SharedProduct).ToString(),
            Get(product, "Stock").ToString(),
            Get(product, "LastSupply").ToString(),
            Get(product, "LastDemand").ToString(),
            Get(product, "LastClearingPrice").ToString(),
            Get(product, "Price").ToString(),
            Get(world.Seller, "Balance").ToString(),
            Get(world.Ledger, "WeeklyTaxRevenue").ToString()
        });
        return new SharedOutcome(
            state,
            (int)Get(report, "OrderCount"),
            (int)Get(report, "SortedOrderCount"),
            (long)Get(report, "ElapsedTicks"),
            ReportIds(report, "SuccessfulMarketIds"),
            ReportIds(report, "FailedMarketIds"));
    }

    private void SaveRecipe(string name)
    {
        IDictionary recipes = StaticDictionary("BUILDING_RECIPE");
        previousRecipes[name] = recipes.Contains(name) ? recipes[name] : null;
    }

    private static string SnapshotProductAndPopulation(object product, object population) =>
        string.Join("|", new[]
        {
            Get(product, "Stock").ToString(),
            Get(product, "Price").ToString(),
            Get(product, "LastDemand").ToString(),
            Get(product, "RequestedDemand").ToString(),
            Get(product, "UnmetDemand").ToString(),
            Get(product, "LastClearingPrice").ToString(),
            Get(Get(population, "Account"), "Balance").ToString(),
            Get(population, "livingStandard").ToString()
        });

    private static IReadOnlyList<string> ReportIds(object report, string member) =>
        ((IEnumerable)Get(report, member)).Cast<object>().Select(value => (string)value).ToList();

    private static long Inventory(object building, string product)
    {
        var inventory = (IReadOnlyDictionary<string, long>)Get(building, "InputInventory");
        return inventory.TryGetValue(product, out long quantity) ? quantity : 0L;
    }

    private static long Material(object project, string product) =>
        ((IReadOnlyDictionary<string, long>)Get(project, "AcquiredMaterials"))[product];

    private static object Get(object instance, string member) =>
        ReflectionTestHelpers.Get(instance, member);

    private static void Set(object instance, string member, object value) =>
        ReflectionTestHelpers.Set(instance, member, value);

    private static IDictionary StaticDictionary(string name) =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            name, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private sealed class TestWorld : IDisposable
    {
        private readonly object nation;
        private readonly object[] provinces;
        private readonly bool[] connected;
        private GameObject engineObject;
        private object engine;
        private object paidProvinces;

        public object Ledger { get; private set; }
        public object Seller { get; private set; }

        public TestWorld(string nationName, params bool[] connectedProvinces)
        {
            nation = TestEconomyFactory.NewNation(nationName, 5_000L);
            connected = connectedProvinces;
            provinces = new object[connected.Length];
            for (int index = 0; index < connected.Length; index++)
            {
                object province = TestEconomyFactory.NewProvince(
                    90_000 + index, nationName.Replace("Nation", string.Empty) + "P" + index);
                Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);
                Set(province, "market", ReflectionTestHelpers.New("ProvinceMarket", Get(province, "name")));
                Set(province, "isConnectedToCapital", connected[index]);
                provinces[index] = province;
            }
            Set(nation, "capital", provinces[0]);
        }

        public object AddPopulation(int provinceIndex, string cultureName, int count,
            long balance, double livingStandard)
        {
            object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
            Set(species, "name", "Human");
            object group = ReflectionTestHelpers.New("EthnicGroup", species,
                ReflectionTestHelpers.New("Culture", cultureName));
            object population = ReflectionTestHelpers.New("ProvinceEthnicPop", provinces[provinceIndex],
                group, new List<int> { 0, count, 0, 0 }, balance, livingStandard);
            ((IList)Get(provinces[provinceIndex], "provinceEthnicPops")).Add(population);
            return population;
        }

        public object AddFactory(int provinceIndex, string typeName, string input,
            string output, int quantity, long capital)
        {
            object type = NewBuildingType(typeName, 1L);
            ((IDictionary)Get(type, "requireItems"))[input] = quantity;
            ((IDictionary)Get(type, "produceItems"))[output] = quantity;
            InstallRecipe(typeName, capital, 1);
            object building = ReflectionTestHelpers.New("Building", type, provinces[provinceIndex]);
            Set(building, "level", 1);
            Set(building, "currentWorkers", 1L);
            ((IDictionary)Get(provinces[provinceIndex], "buildings"))[type] = building;
            return building;
        }

        public object AddProducer(int provinceIndex, string typeName, string output,
            int quantity, long capital)
        {
            object type = NewBuildingType(typeName, 1L);
            ((IDictionary)Get(type, "produceItems"))[output] = quantity;
            InstallRecipe(typeName, capital, 1);
            object building = ReflectionTestHelpers.New("Building", type, provinces[provinceIndex]);
            Set(building, "level", 1);
            Set(building, "currentWorkers", 1L);
            ((IDictionary)Get(provinces[provinceIndex], "buildings"))[type] = building;
            return building;
        }

        public object AddConstructionCompany(int provinceIndex, string typeName, long capital)
        {
            object type = NewBuildingType(typeName, 1L);
            InstallRecipe(typeName, capital, 1);
            object company = ReflectionTestHelpers.New(
                "ConstructionCompanyBuilding", type, provinces[provinceIndex], 1);
            Set(company, "currentWorkers", 1L);
            ((IDictionary)Get(provinces[provinceIndex], "buildings"))[type] = company;
            return company;
        }

        public void Initialize()
        {
            foreach (object province in provinces)
                ReflectionTestHelpers.Call<object>(province, "InitializePopulation");
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
                null,
                new[]
                {
                    TestEconomyFactory.ListOf("Nation", nation),
                    TestEconomyFactory.ListOf("Province", provinces)
                });
            Ledger = Get(nation, "Ledger");
            Seller = ReflectionTestHelpers.New("MoneyAccount", "task9-seller", 0L);
            Assert.That(ReflectionTestHelpers.Call<bool>(Ledger, "RegisterEmptyAccount", Seller), Is.True);
            Set(Ledger, "SalesTaxBasisPoints", 1000);
            engineObject = new GameObject("WeeklyMarketSimulationTests");
            engine = engineObject.AddComponent(ReflectionTestHelpers.Find("EconomicEngine"));
            paidProvinces = CreateProvinceSet(provinces);
        }

        public object PlaceProject(int provinceIndex, string typeName, int time,
            params (string Name, int Quantity)[] materials)
        {
            object type = ReflectionTestHelpers.New("BuildingType", typeName);
            object recipe = InstallRecipe(typeName, 0L, time);
            foreach ((string name, int quantity) in materials)
                ((IDictionary)Get(recipe, "requireItems"))[name] = quantity;
            object project = ReflectionTestHelpers.Call<object>(
                nation, "PlaceConstructionMandate", type, provinces[provinceIndex]);
            Assert.That(project, Is.Not.Null);
            return project;
        }

        public object AddNationalProduct(string name, int price, int stock,
            object supplier = null) => AddProduct(Get(nation, "market"), name, price, stock, supplier);

        public object AddLocalProduct(int provinceIndex, string name, int price, int stock,
            object supplier = null) => AddProduct(Get(provinces[provinceIndex], "market"),
            name, price, stock, supplier);

        public object LocalProduct(int provinceIndex, string name) =>
            ((IDictionary)Get(Get(provinces[provinceIndex], "market"), "Products"))[name];

        public bool HasLocalProduct(int provinceIndex, string name) =>
            ((IDictionary)Get(Get(provinces[provinceIndex], "market"), "Products")).Contains(name);

        public void Produce(int provinceIndex) =>
            ReflectionTestHelpers.Call<object>(provinces[provinceIndex], "ProduceGoodsWeekly");

        public object Process(bool reverse, object settings = null)
        {
            object[] ordered = reverse ? provinces.Reverse().ToArray() : provinces;
            MethodInfo process = ReflectionTestHelpers.Find("WeeklyMarketSimulation").GetMethod(
                "Process", BindingFlags.Public | BindingFlags.Static);
            Assert.That(process, Is.Not.Null, "Missing WeeklyMarketSimulation.Process.");
            return process.Invoke(null, new[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", ordered),
                paidProvinces,
                engine,
                settings ?? ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500)
            });
        }

        public void Progress(double hours) =>
            ReflectionTestHelpers.Find("ConstructionWeeklySimulation")
                .GetMethod("ProgressPaidCompanies")
                .Invoke(null, new[]
                {
                    TestEconomyFactory.ListOf("Province", provinces),
                    paidProvinces,
                    (object)hours
                });

        public void AssertAudit()
        {
            object[] arguments = { 0L };
            Assert.That(Ledger.GetType().GetMethod("Audit").Invoke(Ledger, arguments), Is.True);
            Assert.That(arguments[0], Is.EqualTo(Get(Ledger, "MoneySupply")));
        }

        public void Dispose()
        {
            if (engineObject != null)
                UnityEngine.Object.DestroyImmediate(engineObject);
        }

        private object AddProduct(object market, string name, int price, int stock, object supplier)
        {
            object product = ReflectionTestHelpers.New("ProductState", name, price);
            ReflectionTestHelpers.Call<object>(product, "BeginWeek");
            ((IDictionary)Get(market, "Products"))[name] = product;
            if (stock > 0)
                ReflectionTestHelpers.Call<object>(product, "AddSupply", supplier ?? Seller, stock);
            return product;
        }

        private static object NewBuildingType(string name, long workerNeeded)
        {
            object type = ReflectionTestHelpers.New("BuildingType", name);
            Set(type, "workerNeeded", workerNeeded);
            Set(type, "weeklyWage", 1L);
            Set(type, "requireItems", new Dictionary<string, int>());
            Set(type, "produceItems", new Dictionary<string, int>());
            return type;
        }

        private static object InstallRecipe(string name, long initialCapital, int time)
        {
            object recipe = ReflectionTestHelpers.New("BuildingRecipe", name);
            Set(recipe, "InitialCapital", initialCapital);
            Set(recipe, "TimeToBuild", time);
            Set(recipe, "ConstructionFee", 0L);
            StaticDictionary("BUILDING_RECIPE")[name] = recipe;
            return recipe;
        }

        private static object CreateProvinceSet(object[] values)
        {
            Type setType = typeof(HashSet<>).MakeGenericType(ReflectionTestHelpers.Find("Province"));
            return Activator.CreateInstance(setType,
                new[] { TestEconomyFactory.ListOf("Province", values) });
        }
    }

    private sealed class SharedOutcome
    {
        public string State { get; }
        public int OrderCount { get; }
        public int SortedOrderCount { get; }
        public long ElapsedTicks { get; }
        public IReadOnlyList<string> SuccessfulMarkets { get; }
        public IReadOnlyList<string> FailedMarkets { get; }

        public SharedOutcome(string state, int orderCount, int sortedOrderCount, long elapsedTicks,
            IReadOnlyList<string> successfulMarkets, IReadOnlyList<string> failedMarkets)
        {
            State = state;
            OrderCount = orderCount;
            SortedOrderCount = sortedOrderCount;
            ElapsedTicks = elapsedTicks;
            SuccessfulMarkets = successfulMarkets;
            FailedMarkets = failedMarkets;
        }
    }
}
