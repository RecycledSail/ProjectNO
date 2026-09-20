using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class MarketClearingPerformanceTests
{
    private const int ProvinceCount = 200;
    private const int FactoryCountPerProvince = 25;
    private const int ProductCount = 18;
    private const int OrderCount = ProvinceCount * FactoryCountPerProvince;

    [Test]
    public void TwoHundredProvinces_ReportCollectionPlanningAndSettlementForFiveThousandOrders()
    {
        using RepresentativeWorld world = new();
        Assert.That(world.Provinces, Has.Count.EqualTo(ProvinceCount));
        Assert.That(world.Factories, Has.Count.EqualTo(OrderCount));
        Assert.That(world.Products, Has.Length.EqualTo(ProductCount));
        AssertAudit(world.Ledger, world.InitialMoneySupply);

        MethodInfo accessMethod = ReflectionTestHelpers.Find("MarketAccess")
            .GetMethod("TryResolve", BindingFlags.Public | BindingFlags.Static);
        MethodInfo collectMethod = ReflectionTestHelpers.Find("FactoryMarketOrders")
            .GetMethod("Collect", BindingFlags.Public | BindingFlags.Static);
        Assert.That(accessMethod, Is.Not.Null);
        Assert.That(collectMethod, Is.Not.Null);

        Stopwatch collection = Stopwatch.StartNew();
        IDictionary remainingBudgets = world.CreateStartingBudgets();
        IList orders = (IList)TestEconomyFactory.ListOf("MarketOrder");
        foreach (object province in world.Provinces)
        {
            object[] accessArguments = { province, null };
            if (!(bool)accessMethod.Invoke(null, accessArguments))
                throw new InvalidOperationException("The representative province has no market access.");

            object collected = collectMethod.Invoke(
                null,
                new[] { province, accessArguments[1], (object)true, remainingBudgets });
            foreach (object order in (IEnumerable)collected)
                orders.Add(order);
        }
        collection.Stop();

        Assert.That(orders, Has.Count.EqualTo(OrderCount));
        Assert.That(orders.Cast<object>().Select(order =>
                (string)ReflectionTestHelpers.Get(order, "Id")),
            Is.Unique);
        int sortedOrderCount = CountOrdersInShortageBooks(orders, world.Products);
        Assert.That(sortedOrderCount, Is.EqualTo(OrderCount));
        Assert.That(remainingBudgets.Values.Cast<long>().All(balance => balance >= 0L), Is.True);

        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
        object[] planArguments =
        {
            orders,
            TestEconomyFactory.ListOf("ProductState", world.Products),
            world.Ledger,
            settings,
            null,
            null
        };
        MethodInfo planMethod = ReflectionTestHelpers.Find("MarketClearingEngine")
            .GetMethod("TryPlan", BindingFlags.Public | BindingFlags.Static);
        Assert.That(planMethod, Is.Not.Null);
        Stopwatch planner = Stopwatch.StartNew();
        bool planned = (bool)planMethod.Invoke(null, planArguments);
        planner.Stop();

        Assert.That(planned, Is.True, planArguments[5] as string);
        object plan = planArguments[4];
        IList fills = (IList)ReflectionTestHelpers.Get(plan, "Fills");
        IList purchases = (IList)ReflectionTestHelpers.Get(plan, "Purchases");
        IList results = (IList)ReflectionTestHelpers.Get(plan, "ProductResults");
        Assert.That(results, Has.Count.EqualTo(ProductCount));
        Assert.That(fills, Has.Count.EqualTo(world.ExpectedFillCount));
        Assert.That(purchases, Has.Count.EqualTo(world.ExpectedFillCount));
        Assert.That(fills.Cast<object>().Sum(fill =>
            (int)ReflectionTestHelpers.Get(fill, "Quantity")), Is.EqualTo(world.ExpectedFillCount));
        string[] fillIds = fills.Cast<object>()
            .Select(fill => (string)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(fill, "Order"), "Id"))
            .ToArray();
        Assert.That(fillIds,
            Is.EqualTo(fillIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        AssertUniformPrices(fills);
        AssertPlannedProductTotals(results, world.Products);

        object[] settlementArguments =
        {
            world.Ledger,
            TestEconomyFactory.ListOf("IMarketOrderRecipient"),
            null
        };
        MethodInfo settlementMethod = plan.GetType().GetMethod("TrySettle");
        Assert.That(settlementMethod, Is.Not.Null);
        Stopwatch settlement = Stopwatch.StartNew();
        bool settled = (bool)settlementMethod.Invoke(plan, settlementArguments);
        settlement.Stop();

        Assert.That(settled, Is.True, settlementArguments[2] as string);
        Assert.That(world.Products.All(product =>
            (int)ReflectionTestHelpers.Get(product, "Stock") == 0), Is.True);
        Assert.That(world.Products.Sum(product =>
            (int)ReflectionTestHelpers.Get(product, "LastDemand")),
            Is.EqualTo(world.ExpectedFillCount));
        Assert.That(world.TotalFactoryInputInventory(), Is.EqualTo(world.ExpectedFillCount));
        Assert.That(world.Factories.All(factory =>
            (long)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(factory, "Account"), "Balance") >= 0L), Is.True);
        AssertAudit(world.Ledger, world.InitialMoneySupply);

        double totalMillisecondsPerOrder =
            (collection.Elapsed.TotalMilliseconds + planner.Elapsed.TotalMilliseconds +
             settlement.Elapsed.TotalMilliseconds) / OrderCount;
        TestContext.WriteLine(
            $"Market clearing performance: provinces={ProvinceCount}, factories={world.Factories.Count}, " +
            $"orders={orders.Count}, products={ProductCount}, sortedOrders={sortedOrderCount}, " +
            $"fills={world.ExpectedFillCount}, collectionMs={collection.Elapsed.TotalMilliseconds:F3}, " +
            $"planMs={planner.Elapsed.TotalMilliseconds:F3}, " +
            $"settlementMs={settlement.Elapsed.TotalMilliseconds:F3}, " +
            $"totalMsPerOrder={totalMillisecondsPerOrder:F6}");
    }

    private static int CountOrdersInShortageBooks(IList orders, object[] products)
    {
        int sorted = 0;
        foreach (object product in products)
        {
            List<object> eligible = orders.Cast<object>()
                .Where(order => ReferenceEquals(
                    ReflectionTestHelpers.Get(order, "Product"), product))
                .Where(order => (int)ReflectionTestHelpers.Get(order, "MaximumUnitPrice") >=
                                (int)ReflectionTestHelpers.Get(product, "Price"))
                .ToList();
            int demand = eligible.Sum(order => (int)ReflectionTestHelpers.Get(order, "Quantity"));
            int stock = (int)ReflectionTestHelpers.Get(product, "Stock");
            if (stock > 0 && stock < demand)
                sorted += eligible.Count;
        }
        return sorted;
    }

    private static void AssertUniformPrices(IList fills)
    {
        foreach (IGrouping<object, object> productFills in fills.Cast<object>()
                     .GroupBy(fill => ReflectionTestHelpers.Get(
                         ReflectionTestHelpers.Get(fill, "Order"), "Product")))
        {
            Assert.That(productFills.Select(fill =>
                    (int)ReflectionTestHelpers.Get(fill, "UnitPrice")).Distinct().Count(),
                Is.EqualTo(1));
        }
    }

    private static void AssertPlannedProductTotals(IList results, object[] products)
    {
        for (int index = 0; index < ProductCount; index++)
        {
            object result = results.Cast<object>().Single(candidate => ReferenceEquals(
                ReflectionTestHelpers.Get(candidate, "Product"), products[index]));
            int factoriesPerProduct = FactoryCountPerProvince / ProductCount +
                                      (index < FactoryCountPerProvince % ProductCount ? 1 : 0);
            int expectedDemand = ProvinceCount * factoriesPerProduct;
            int expectedStock = 100 + index;
            Assert.That(ReflectionTestHelpers.Get(result, "RequestedDemand"),
                Is.EqualTo(expectedDemand));
            Assert.That(ReflectionTestHelpers.Get(result, "AvailableStock"),
                Is.EqualTo(expectedStock));
            Assert.That(ReflectionTestHelpers.Get(result, "SoldQuantity"),
                Is.EqualTo(expectedStock));
        }
    }

    private static void AssertAudit(object ledger, long expectedMoneySupply)
    {
        object[] audit = { 0L };
        Assert.That(ledger.GetType().GetMethod("Audit").Invoke(ledger, audit), Is.True);
        Assert.That(audit[0], Is.EqualTo(expectedMoneySupply));
    }

    private sealed class RepresentativeWorld : IDisposable
    {
        private const string FactoryTypePrefix = "Task11PerformanceFactory";
        private const string InputProductPrefix = "performance-product-";
        private const string OutputProduct = "performance-output";

        private readonly Dictionary<string, object> previousRecipes = new();
        private readonly HashSet<string> existingRecipes = new(StringComparer.Ordinal);
        private bool disposed;

        public List<object> Provinces { get; } = new();
        public List<object> Factories { get; } = new();
        public object[] Products { get; private set; }
        public object Ledger { get; private set; }
        public long InitialMoneySupply { get; private set; }
        public int ExpectedFillCount { get; private set; }

        public RepresentativeWorld()
        {
            try
            {
                object[] factoryTypes = CreateFactoryTypes();
                object nation = TestEconomyFactory.NewNation(
                    "Task11PerformanceNation", 2_000_000L);

                for (int provinceIndex = 0; provinceIndex < ProvinceCount; provinceIndex++)
                {
                    string provinceName = $"Task11PerformanceProvince{provinceIndex:D3}";
                    object province = TestEconomyFactory.NewProvince(120_000 + provinceIndex,
                        provinceName);
                    Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province),
                        Is.True);
                    ReflectionTestHelpers.Set(province, "market",
                        ReflectionTestHelpers.New("ProvinceMarket", provinceName));
                    ReflectionTestHelpers.Set(province, "isConnectedToCapital", true);
                    Provinces.Add(province);

                    IDictionary buildings = (IDictionary)ReflectionTestHelpers.Get(
                        province, "buildings");
                    foreach (object factoryType in factoryTypes)
                    {
                        object factory = ReflectionTestHelpers.New(
                            "Building", factoryType, province);
                        ReflectionTestHelpers.Set(factory, "level", 1);
                        ReflectionTestHelpers.Set(factory, "currentWorkers", 1L);
                        buildings.Add(factoryType, factory);
                        Factories.Add(factory);
                    }
                }

                ReflectionTestHelpers.Set(nation, "capital", Provinces[0]);
                ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
                    null,
                    new[]
                    {
                        TestEconomyFactory.ListOf("Nation", nation),
                        TestEconomyFactory.ListOf("Province", Provinces.ToArray())
                    });

                Ledger = ReflectionTestHelpers.Get(nation, "Ledger");
                ReflectionTestHelpers.Set(Ledger, "SalesTaxBasisPoints", 1000);
                Products = CreateProducts(nation);
                InitialMoneySupply = (long)ReflectionTestHelpers.Get(Ledger, "MoneySupply");
            }
            catch
            {
                RestoreRecipes();
                throw;
            }
        }

        public IDictionary CreateStartingBudgets()
        {
            Type accountType = ReflectionTestHelpers.Find("MoneyAccount");
            IDictionary budgets = (IDictionary)Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(accountType, typeof(long)));
            foreach (object factory in Factories)
            {
                object account = ReflectionTestHelpers.Get(factory, "Account");
                budgets.Add(account, (long)ReflectionTestHelpers.Get(account, "Balance"));
            }
            return budgets;
        }

        public int TotalFactoryInputInventory() => Factories.Sum(factory =>
            ((IEnumerable)ReflectionTestHelpers.Get(factory, "InputInventory"))
            .Cast<object>()
            .Sum(pair => checked((int)(long)ReflectionTestHelpers.Get(pair, "Value"))));

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            RestoreRecipes();
        }

        private object[] CreateFactoryTypes()
        {
            IDictionary recipes = StaticDictionary("BUILDING_RECIPE");
            object[] types = new object[FactoryCountPerProvince];
            for (int index = 0; index < types.Length; index++)
            {
                string name = $"{FactoryTypePrefix}{index:D2}";
                if (recipes.Contains(name))
                {
                    existingRecipes.Add(name);
                    previousRecipes[name] = recipes[name];
                }

                string inputName = $"{InputProductPrefix}{index % ProductCount:D2}";
                var inputs = new Dictionary<string, int> { [inputName] = 1 };
                object type = ReflectionTestHelpers.New("BuildingType", name);
                ReflectionTestHelpers.Set(type, "workerNeeded", 1L);
                ReflectionTestHelpers.Set(type, "weeklyWage", 1L);
                ReflectionTestHelpers.Set(type, "requireItems", inputs);
                ReflectionTestHelpers.Set(type, "produceItems",
                    new Dictionary<string, int> { [OutputProduct] = 1 });
                types[index] = type;

                object recipe = ReflectionTestHelpers.New("BuildingRecipe", name);
                ReflectionTestHelpers.Set(recipe, "requireItems",
                    new Dictionary<string, int>(inputs, StringComparer.Ordinal));
                ReflectionTestHelpers.Set(recipe, "TimeToBuild", 1);
                ReflectionTestHelpers.Set(recipe, "InitialCapital", 100L);
                ReflectionTestHelpers.Set(recipe, "ConstructionFee", 0L);
                recipes[name] = recipe;
            }
            return types;
        }

        private object[] CreateProducts(object nation)
        {
            IDictionary products = (IDictionary)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(nation, "market"), "Products");
            object[] result = new object[ProductCount];
            for (int index = 0; index < result.Length; index++)
            {
                string productName = $"{InputProductPrefix}{index:D2}";
                object product = ReflectionTestHelpers.New(
                    "ProductState", productName, 10 + index % 3);
                int stock = 100 + index;
                object supplier = ReflectionTestHelpers.Get(Factories[index], "Account");
                ReflectionTestHelpers.Call<object>(product, "AddSupply", supplier, stock);
                products.Add(productName, product);
                result[index] = product;
                ExpectedFillCount += stock;
            }
            return result;
        }

        private void RestoreRecipes()
        {
            IDictionary recipes = StaticDictionary("BUILDING_RECIPE");
            for (int index = 0; index < FactoryCountPerProvince; index++)
            {
                string name = $"{FactoryTypePrefix}{index:D2}";
                if (existingRecipes.Contains(name))
                    recipes[name] = previousRecipes[name];
                else
                    recipes.Remove(name);
            }
        }

        private static IDictionary StaticDictionary(string name) =>
            (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
                name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }
}
