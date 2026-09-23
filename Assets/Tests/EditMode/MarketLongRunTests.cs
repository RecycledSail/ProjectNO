using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MarketLongRunTests
{
    private const string Food = "task11-food";
    private const string Ore = "task11-ore";
    private const string Fuel = "task11-fuel";
    private const string FactoryOutput = "task11-alloy";
    private const string Stone = "task11-stone";
    private const string Timber = "task11-timber";
    private const string FactoryType = "Task11MultiInputFactory";
    private const string ProducerType = "Task11InputProducer";
    private const string CompanyType = "Task11ConstructionCompany";
    private const string ProjectType = "Task11ConstructionProject";

    private readonly Dictionary<string, object> previousRecipes = new();
    private object previousFoods;
    private bool hadFoods;

    [SetUp]
    public void SetUp()
    {
        IDictionary categories = StaticDictionary("CATEGORIES");
        hadFoods = categories.Contains("basic_food");
        previousFoods = hadFoods ? categories["basic_food"] : null;
        categories["basic_food"] = new List<string> { Food };

        SaveRecipe(FactoryType);
        SaveRecipe(ProducerType);
        SaveRecipe(CompanyType);
        SaveRecipe(ProjectType);
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
    public void FiftyTwoWeeks_PreserveInvariantsAndObservableStateWhenEnumerationIsReversed()
    {
        RunOutcome forward = RunWorld(false);
        RunOutcome reverse = RunWorld(true);

        Assert.That(forward.WeeklyStates, Has.Count.EqualTo(52));
        Assert.That(reverse.WeeklyStates, Is.EqualTo(forward.WeeklyStates),
            "Equivalent worlds must have identical weekly balances, inventories, prices and fills.");
        Assert.That(forward.FirstWeekFactoryOutputSupply, Is.Zero,
            "Inputs bought in week one must not produce output in that same week.");
        Assert.That(forward.SecondWeekFactoryOutputSupply, Is.GreaterThan(0),
            "Week-one input receipts must be available to week-two production.");
        Assert.That(forward.SawNationalMarket, Is.True);
        Assert.That(forward.SawIsolatedMarket, Is.True);
    }

    private static RunOutcome RunWorld(bool reverse)
    {
        using LongRunWorld world = new();
        List<string> observations = new();
        int firstWeekOutput = 0;
        int secondWeekOutput = 0;
        bool sawNational = false;
        bool sawIsolated = false;

        for (int week = 1; week <= 52; week++)
        {
            object report = world.RunWeek(week, reverse);
            world.AssertInvariants(week);
            observations.Add(world.Observe(week, report));
            IReadOnlyList<string> successful = ReportIds(report, "SuccessfulMarketIds");
            sawNational |= successful.Contains("nation:Task11Nation");
            sawIsolated |= successful.Contains("province:Task11Isolated");
            if (week == 1) firstWeekOutput = world.FactoryOutputSupply;
            if (week == 2) secondWeekOutput = world.FactoryOutputSupply;
        }

        return new RunOutcome(
            observations,
            firstWeekOutput,
            secondWeekOutput,
            sawNational,
            sawIsolated);
    }

    private void SaveRecipe(string name)
    {
        IDictionary recipes = StaticDictionary("BUILDING_RECIPE");
        previousRecipes[name] = recipes.Contains(name) ? recipes[name] : null;
    }

    private static IReadOnlyList<string> ReportIds(object report, string member) =>
        ((IEnumerable)Get(report, member)).Cast<object>().Select(value => (string)value).ToList();

    private static object Get(object instance, string member) =>
        ReflectionTestHelpers.Get(instance, member);

    private static void Set(object instance, string member, object value) =>
        ReflectionTestHelpers.Set(instance, member, value);

    private static IDictionary StaticDictionary(string name) =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            name, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private sealed class LongRunWorld : IDisposable
    {
        private readonly object nation;
        private readonly object[] provinces;
        private readonly object[] populations;
        private readonly object factory;
        private readonly object project;
        private readonly object seller;
        private readonly object paidProvinces;
        private readonly object settings;
        private readonly GameObject engineObject;
        private readonly object engine;
        private readonly object ledger;
        private readonly long initialMoneySupply;

        public int FactoryOutputSupply => ProductInt(NationalProduct(FactoryOutput), "LastSupply");

        public LongRunWorld()
        {
            nation = TestEconomyFactory.NewNation("Task11Nation", 2_000_000L);
            provinces = new[]
            {
                AddProvince(110_001, "Task11ConnectedA", true),
                AddProvince(110_002, "Task11ConnectedB", true),
                AddProvince(110_003, "Task11Isolated", false)
            };
            Set(nation, "capital", provinces[0]);

            populations = new[]
            {
                AddPopulation(0, "alpha", 2_000, 20_000L),
                AddPopulation(0, "beta", 1_000, 20_000L),
                AddPopulation(1, "gamma", 1_000, 20_000L),
                AddPopulation(2, "delta", 2_000, 20_000L),
                AddPopulation(2, "epsilon", 1_000, 20_000L)
            };

            factory = AddFactory();
            AddProducer();
            AddConstructionCompany();

            foreach (object province in provinces)
                ReflectionTestHelpers.Call<object>(province, "InitializePopulation");
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
                null,
                new[]
                {
                    TestEconomyFactory.ListOf("Nation", nation),
                    TestEconomyFactory.ListOf("Province", provinces)
                });

            ledger = Get(nation, "Ledger");
            seller = ReflectionTestHelpers.New("MoneyAccount", "task11-stock-supplier", 0L);
            Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterEmptyAccount", seller), Is.True);
            Set(ledger, "SalesTaxBasisPoints", 1000);

            AddNationalProduct(Food, 10, 20);
            AddNationalProduct(Ore, 12, 80);
            AddNationalProduct(Fuel, 8, 50);
            AddNationalProduct(FactoryOutput, 20, 0);
            AddLocalProduct(0, Food, 10, 0);
            AddLocalProduct(0, Ore, 12, 0);
            AddLocalProduct(0, Fuel, 8, 0);
            AddLocalProduct(0, FactoryOutput, 20, 0);
            AddLocalProduct(1, Food, 10, 0);
            AddLocalProduct(1, Ore, 12, 0);
            AddLocalProduct(1, Fuel, 8, 0);
            AddLocalProduct(2, Food, 10, 30);
            AddLocalProduct(2, Stone, 7, 80);
            AddLocalProduct(2, Timber, 9, 60);

            project = PlaceProject();
            paidProvinces = CreateProvinceSet(provinces);
            settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
            engineObject = new GameObject("MarketLongRunTests");
            engine = engineObject.AddComponent(ReflectionTestHelpers.Find("EconomicEngine"));
            initialMoneySupply = (long)Get(ledger, "MoneySupply");
        }

        public object RunWeek(int week, bool reverse)
        {
            ReflectionTestHelpers.Call<object>(ledger, "BeginWeek");
            List<object> products = Products().Distinct().ToList();
            if (reverse) products.Reverse();
            foreach (object product in products)
                ReflectionTestHelpers.Call<object>(product, "BeginWeek");

            ReflectionTestHelpers.Call<object>(nation, "SimulateWeeklyTurn");
            object[] orderedProvinces = reverse ? provinces.Reverse().ToArray() : provinces.ToArray();
            foreach (object province in orderedProvinces)
                ReflectionTestHelpers.Call<object>(province, "ProduceGoodsWeekly");
            foreach (object province in orderedProvinces)
                TransferConnectedProduction(province, reverse);

            MethodInfo process = ReflectionTestHelpers.Find("WeeklyMarketSimulation").GetMethod(
                "Process", BindingFlags.Public | BindingFlags.Static);
            object report = process.Invoke(null, new[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", orderedProvinces),
                paidProvinces,
                engine,
                settings
            });

            ReflectionTestHelpers.Find("ConstructionWeeklySimulation")
                .GetMethod("ProgressPaidCompanies", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[]
                {
                    TestEconomyFactory.ListOf("Province", orderedProvinces),
                    paidProvinces,
                    (object)1d
                });

            Assert.That(ReportIds(report, "FailedMarketIds"), Is.Empty, $"week {week}");
            return report;
        }

        public void AssertInvariants(int week)
        {
            object[] audit = { 0L };
            Assert.That(ledger.GetType().GetMethod("Audit").Invoke(ledger, audit), Is.True,
                $"ledger audit failed in week {week}");
            Assert.That(audit[0], Is.EqualTo(initialMoneySupply),
                $"money supply changed in week {week}");

            List<object> products = Products().ToList();
            Assert.That(products.All(product =>
                    ProductInt(product, "Price") >= 1 && ProductInt(product, "Stock") >= 0),
                Is.True,
                $"negative stock or sub-minimum price in week {week}");

            IEnumerable<long> inputQuantities = Buildings()
                .SelectMany(building =>
                    ((IEnumerable)Get(building, "InputInventory")).Cast<object>())
                .Select(entry => (long)Get(entry, "Value"));
            Assert.That(inputQuantities.All(quantity => quantity >= 0L), Is.True,
                $"negative factory input in week {week}");
        }

        public string Observe(int week, object report)
        {
            string balances = string.Join(",", Accounts()
                .OrderBy(account => (string)Get(account, "Id"), StringComparer.Ordinal)
                .Select(account => $"{Get(account, "Id")}={Get(account, "Balance")}"));
            string products = string.Join(",", MarketProducts()
                .OrderBy(item => item.MarketId, StringComparer.Ordinal)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => ProductObservation(item.MarketId, item.Name, item.Product)));
            string inputs = string.Join(",", Buildings()
                .OrderBy(building => (string)Get(Get(building, "Account"), "Id"), StringComparer.Ordinal)
                .Select(building =>
                    $"{Get(Get(building, "Account"), "Id")}[{DictionaryObservation(Get(building, "InputInventory"))}]"));
            string populationState = string.Join(",", populations
                .OrderBy(population => (string)Get(Get(population, "Account"), "Id"), StringComparer.Ordinal)
                .Select(population =>
                    $"{Get(Get(population, "Account"), "Id")}={Convert.ToDouble(Get(population, "livingStandard"), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)}"));
            string construction =
                $"status={Get(project, "Status")};remaining={Convert.ToDouble(Get(project, "RemainingManhours"), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)};" +
                $"acquired=[{DictionaryObservation(Get(project, "AcquiredMaterials"))}];" +
                $"consumed=[{DictionaryObservation(Get(project, "ConsumedMaterials"))}]";
            string markets =
                $"ok=[{string.Join(",", ReportIds(report, "SuccessfulMarketIds"))}];" +
                $"failed=[{string.Join(",", ReportIds(report, "FailedMarketIds"))}];" +
                $"orders={Get(report, "OrderCount")};sorted={Get(report, "SortedOrderCount")}";
            return $"week={week}|balances:{balances}|products:{products}|inputs:{inputs}|" +
                   $"populations:{populationState}|construction:{construction}|markets:{markets}";
        }

        public void Dispose()
        {
            if (project != null && !string.Equals(Get(project, "Status").ToString(), "Completed", StringComparison.Ordinal) &&
                !string.Equals(Get(project, "Status").ToString(), "Cancelled", StringComparison.Ordinal))
            {
                ReflectionTestHelpers.Call<bool>(project, "Cancel");
            }
            if (engineObject != null)
                UnityEngine.Object.DestroyImmediate(engineObject);
        }

        private object AddProvince(int id, string name, bool connected)
        {
            object province = TestEconomyFactory.NewProvince(id, name);
            Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);
            Set(province, "market", ReflectionTestHelpers.New("ProvinceMarket", name));
            Set(province, "isConnectedToCapital", connected);
            return province;
        }

        private object AddPopulation(int provinceIndex, string cultureName, int count, long balance)
        {
            object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
            Set(species, "name", "Human");
            object group = ReflectionTestHelpers.New(
                "EthnicGroup", species, ReflectionTestHelpers.New("Culture", cultureName));
            object population = ReflectionTestHelpers.New(
                "ProvinceEthnicPop",
                provinces[provinceIndex],
                group,
                new List<int> { 0, count, 0, 0 },
                balance,
                2d);
            ((IList)Get(provinces[provinceIndex], "provinceEthnicPops")).Add(population);
            return population;
        }

        private object AddFactory()
        {
            object type = NewBuildingType(FactoryType, 1L);
            ((IDictionary)Get(type, "requireItems"))[Ore] = 2;
            ((IDictionary)Get(type, "requireItems"))[Fuel] = 1;
            ((IDictionary)Get(type, "produceItems"))[FactoryOutput] = 1;
            InstallRecipe(FactoryType, 200_000L, 1);
            object building = ReflectionTestHelpers.New("Building", type, provinces[0]);
            Set(building, "level", 1);
            Set(building, "currentWorkers", 1L);
            ((IDictionary)Get(provinces[0], "buildings"))[type] = building;
            return building;
        }

        private void AddProducer()
        {
            object type = NewBuildingType(ProducerType, 1L);
            ((IDictionary)Get(type, "produceItems"))[Ore] = 4;
            ((IDictionary)Get(type, "produceItems"))[Fuel] = 2;
            InstallRecipe(ProducerType, 100_000L, 1);
            object building = ReflectionTestHelpers.New("Building", type, provinces[1]);
            Set(building, "level", 1);
            Set(building, "currentWorkers", 1L);
            ((IDictionary)Get(provinces[1], "buildings"))[type] = building;
        }

        private void AddConstructionCompany()
        {
            object type = NewBuildingType(CompanyType, 1L);
            InstallRecipe(CompanyType, 100_000L, 1);
            object company = ReflectionTestHelpers.New(
                "ConstructionCompanyBuilding", type, provinces[2], 1);
            Set(company, "currentWorkers", 1L);
            ((IDictionary)Get(provinces[2], "buildings"))[type] = company;
        }

        private object PlaceProject()
        {
            object type = ReflectionTestHelpers.New("BuildingType", ProjectType);
            object recipe = InstallRecipe(ProjectType, 0L, 1_000);
            ((IDictionary)Get(recipe, "requireItems"))[Stone] = 40;
            ((IDictionary)Get(recipe, "requireItems"))[Timber] = 20;
            object mandate = ReflectionTestHelpers.Call<object>(
                nation, "PlaceConstructionMandate", type, provinces[2]);
            Assert.That(mandate, Is.Not.Null);
            return mandate;
        }

        private object AddNationalProduct(string name, int price, int stock) =>
            AddProduct(Get(nation, "market"), name, price, stock);

        private object AddLocalProduct(int provinceIndex, string name, int price, int stock) =>
            AddProduct(Get(provinces[provinceIndex], "market"), name, price, stock);

        private object AddProduct(object market, string name, int price, int stock)
        {
            object product = ReflectionTestHelpers.New("ProductState", name, price);
            ((IDictionary)Get(market, "Products"))[name] = product;
            if (stock > 0)
                ReflectionTestHelpers.Call<object>(product, "AddSupply", seller, stock);
            return product;
        }

        private void TransferConnectedProduction(object province, bool reverse)
        {
            if (!(bool)Get(province, "isConnectedToCapital"))
                return;

            IDictionary local = (IDictionary)Get(Get(province, "market"), "Products");
            List<object> localProducts = local.Values.Cast<object>()
                .OrderBy(product => (string)Get(product, "ProductName"), StringComparer.Ordinal)
                .ToList();
            if (reverse) localProducts.Reverse();
            IDictionary national = (IDictionary)Get(Get(nation, "market"), "Products");
            foreach (object source in localProducts)
            {
                string name = (string)Get(source, "ProductName");
                if (!national.Contains(name))
                    national[name] = ReflectionTestHelpers.New("ProductState", name, ProductInt(source, "Price"));
                ReflectionTestHelpers.Call<object>(source, "TransferAllStockTo", national[name]);
            }
        }

        private object NationalProduct(string name) =>
            ((IDictionary)Get(Get(nation, "market"), "Products"))[name];

        private IEnumerable<object> Products() =>
            MarketProducts().Select(item => item.Product).Distinct();

        private IEnumerable<MarketProduct> MarketProducts()
        {
            foreach (DictionaryEntry entry in (IDictionary)Get(Get(nation, "market"), "Products"))
                yield return new MarketProduct("nation:Task11Nation", (string)entry.Key, entry.Value);
            foreach (object province in provinces)
            {
                string marketId = "province:" + Get(province, "name");
                foreach (DictionaryEntry entry in (IDictionary)Get(Get(province, "market"), "Products"))
                    yield return new MarketProduct(marketId, (string)entry.Key, entry.Value);
            }
        }

        private IEnumerable<object> Buildings() => provinces
            .SelectMany(province => ((IDictionary)Get(province, "buildings")).Values.Cast<object>())
            .Distinct();

        private IEnumerable<object> Accounts()
        {
            yield return Get(nation, "Account");
            foreach (object population in populations)
                yield return Get(population, "Account");
            foreach (object building in Buildings())
                yield return Get(building, "Account");
            yield return seller;
            yield return Get(project, "EscrowAccount");
        }

        private static string ProductObservation(string marketId, string name, object product)
        {
            string lots = string.Join(",", ((IEnumerable)Get(Get(product, "Inventory"), "Lots"))
                .Cast<object>()
                .OrderBy(entry => (string)Get(Get(entry, "Key"), "Id"), StringComparer.Ordinal)
                .Select(entry => $"{Get(Get(entry, "Key"), "Id")}={Get(entry, "Value")}"));
            return $"{marketId}/{name}[price={Get(product, "Price")};last={Get(product, "LastPrice")};" +
                   $"clear={Get(product, "LastClearingPrice")};supply={Get(product, "LastSupply")};" +
                   $"fill={Get(product, "LastDemand")};requested={Get(product, "RequestedDemand")};" +
                   $"unmet={Get(product, "UnmetDemand")};stock={Get(product, "Stock")};lots={lots}]";
        }

        private static string DictionaryObservation(object dictionary) =>
            string.Join(",", ((IEnumerable)dictionary).Cast<object>()
                .OrderBy(entry => Get(entry, "Key").ToString(), StringComparer.Ordinal)
                .Select(entry => $"{Get(entry, "Key")}={Get(entry, "Value")}"));

        private static int ProductInt(object product, string member) => (int)Get(product, member);

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
            return Activator.CreateInstance(
                setType,
                new[] { TestEconomyFactory.ListOf("Province", values) });
        }
    }

    private sealed class MarketProduct
    {
        public string MarketId { get; }
        public string Name { get; }
        public object Product { get; }

        public MarketProduct(string marketId, string name, object product)
        {
            MarketId = marketId;
            Name = name;
            Product = product;
        }
    }

    private sealed class RunOutcome
    {
        public IReadOnlyList<string> WeeklyStates { get; }
        public int FirstWeekFactoryOutputSupply { get; }
        public int SecondWeekFactoryOutputSupply { get; }
        public bool SawNationalMarket { get; }
        public bool SawIsolatedMarket { get; }

        public RunOutcome(
            IReadOnlyList<string> weeklyStates,
            int firstWeekFactoryOutputSupply,
            int secondWeekFactoryOutputSupply,
            bool sawNationalMarket,
            bool sawIsolatedMarket)
        {
            WeeklyStates = weeklyStates;
            FirstWeekFactoryOutputSupply = firstWeekFactoryOutputSupply;
            SecondWeekFactoryOutputSupply = secondWeekFactoryOutputSupply;
            SawNationalMarket = sawNationalMarket;
            SawIsolatedMarket = sawIsolatedMarket;
        }
    }
}
