using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PopulationMarketOrderTests
{
    private const string FoodA = "population-order-food-a";
    private const string FoodB = "population-order-food-b";
    private const string FoodC = "population-order-food-c";

    [Test]
    public void CollectFoodOrders_ChoosesLowestPriceThenHighestStockThenNameWithoutMutation()
    {
        using Context context = new();
        object population = context.AddPopulation("chooser", 10_000, 100L, 2.0);
        context.Initialize();
        object lowStock = context.AddProduct(FoodA, 10, 5);
        object highStock = context.AddProduct(FoodB, 10, 8);
        context.AddProduct(FoodC, 12, 20);
        context.SetFoods(FoodC, FoodA, FoodB);
        string before = context.Snapshot(population);

        object batch = Collect(context);
        IReadOnlyList<object> orders = Items(batch, "Orders");

        Assert.That(orders, Has.Count.EqualTo(1));
        Assert.That(Get(orders[0], "Product"), Is.SameAs(highStock));
        Assert.That(Get(orders[0], "Quantity"), Is.EqualTo(10));
        Assert.That(Get(orders[0], "MaximumUnitPrice"), Is.EqualTo(10));
        Assert.That(Get(orders[0], "ReservedBudget"), Is.EqualTo(100L));
        Assert.That(Get(lowStock, "Stock"), Is.EqualTo(5));
        Assert.That(context.Snapshot(population), Is.EqualTo(before));
        Assert.That(context.Budgets[Get(population, "Account")], Is.EqualTo(0L));
    }

    [Test]
    public void FoodRecipients_CompeteByBidAndEachUpdatesLivingStandardOnce()
    {
        using Context context = new();
        object rich = context.AddPopulation("rich", 1_000, 13L, 2.0);
        object poor = context.AddPopulation("poor", 1_000, 12L, 2.0);
        context.Initialize();
        object product = context.AddProduct(FoodA, 10, 1);
        context.SetFoods(FoodA);
        object batch = Collect(context);

        object plan = Plan(context, Items(batch, "Orders"));
        Assert.That(Settle(plan, context.Ledger, Items(batch, "Recipients")), Is.True);

        Assert.That(Balance(rich), Is.EqualTo(0L));
        Assert.That(Balance(poor), Is.EqualTo(12L));
        Assert.That(Balance(context.Seller), Is.EqualTo(12L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(1L));
        Assert.That(Get(product, "Stock"), Is.EqualTo(0));
        Assert.That(Get(rich, "livingStandard"), Is.EqualTo(2.01).Within(0.000001));
        Assert.That(Get(poor, "livingStandard"), Is.EqualTo(1.95).Within(0.000001));
    }

    [Test]
    public void CollectFoodOrders_ReturnsEveryPopulationRecipientForZeroOutcomes()
    {
        using Context context = new();
        object zeroNeed = context.AddPopulation("zero", 500, 20L, 2.0);
        object unaffordable = context.AddPopulation("unaffordable", 1_000, 9L, 2.0);
        context.Initialize();
        context.AddProduct(FoodA, 10, 10);
        context.SetFoods(FoodA);
        object batch = Collect(context);

        Assert.That(Items(batch, "Orders"), Is.Empty);
        Assert.That(Items(batch, "Recipients"), Has.Count.EqualTo(2));
        object plan = Plan(context, Items(batch, "Orders"));
        Assert.That(Settle(plan, context.Ledger, Items(batch, "Recipients")), Is.True);
        Assert.That(Get(zeroNeed, "livingStandard"), Is.EqualTo(2.01).Within(0.000001));
        Assert.That(Get(unaffordable, "livingStandard"), Is.EqualTo(1.95).Within(0.000001));
    }

    [Test]
    public void CollectFoodOrders_ProducesAZeroFillRecipientWhenNoFoodDefinitionExists()
    {
        using Context context = new();
        object population = context.AddPopulation("no-definition", 1_000, 20L, 2.0);
        context.Initialize();
        context.SetFoods(FoodA);

        object batch = Collect(context);

        Assert.That(Items(batch, "Orders"), Is.Empty);
        Assert.That(Items(batch, "Recipients"), Has.Count.EqualTo(1));
        Assert.That(Settle(Plan(context, Items(batch, "Orders")), context.Ledger,
            Items(batch, "Recipients")), Is.True);
        Assert.That(Get(population, "livingStandard"), Is.EqualTo(1.95).Within(0.000001));
    }

    [Test]
    public void CollectFoodOrders_UsesExactMarketAccessProductIdentityForConnectedAndIsolatedMarkets()
    {
        using Context context = new();
        object population = context.AddPopulation("identity", 1_000, 20L, 2.0);
        context.Initialize();
        object local = context.AddProduct(FoodA, 10, 10);
        object national = context.AddNationalProduct(FoodA, 10, 10);
        context.SetFoods(FoodA);

        Set(context.Province, "isConnectedToCapital", false);
        object localBatch = Collect(context, Resolve(context.Province));
        context.Budgets[Get(population, "Account")] = 20L;
        Set(context.Province, "isConnectedToCapital", true);
        object nationalBatch = Collect(context, Resolve(context.Province));

        Assert.That(Get(Items(localBatch, "Orders").Single(), "Product"), Is.SameAs(local));
        Assert.That(Get(Items(nationalBatch, "Orders").Single(), "Product"), Is.SameAs(national));
        Assert.That(Get(Items(localBatch, "Orders").Single(), "Id"),
            Is.Not.EqualTo(Get(Items(nationalBatch, "Orders").Single(), "Id")));
    }

    [Test]
    public void FoodRecipient_RejectsForeignOrdersAndZeroReceiptSupersedesEarlierReceipt()
    {
        using Context context = new();
        object population = context.AddPopulation("receipt", 1_000, 20L, 2.0);
        context.Initialize();
        object product = context.AddProduct(FoodA, 10, 10);
        context.SetFoods(FoodA);
        object order = Items(Collect(context), "Orders").Single();
        object recipient = Get(order, "Recipient");
        object fill = ReflectionTestHelpers.New("MarketOrderFill", order, 1, 10);

        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill", fill), out object stale), Is.True);
        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill"), out object current), Is.True);
        Commit(stale);
        Commit(current);
        Assert.That(Get(population, "livingStandard"), Is.EqualTo(1.95).Within(0.000001));

        object foreignBuyer = ReflectionTestHelpers.New("MoneyAccount", "population-order-foreign", 10L);
        object foreignOrder = ReflectionTestHelpers.New("MarketOrder", "foreign", foreignBuyer,
            product, 1, 10, 10L, 1, recipient);
        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill",
            ReflectionTestHelpers.New("MarketOrderFill", foreignOrder, 1, 10)), out object receipt), Is.False);
        Assert.That(receipt, Is.Null);
    }

    [Test]
    public void FoodRecipient_CommitsPositiveAndZeroReceiptsExactlyOnceAndIgnoresStaleReceipt()
    {
        using Context positiveContext = new();
        object positivePopulation = positiveContext.AddPopulation("positive-once", 1_000, 20L, 2.0);
        positiveContext.Initialize();
        object positiveProduct = positiveContext.AddProduct(FoodA, 10, 10);
        positiveContext.SetFoods(FoodA);
        object positiveOrder = Items(Collect(positiveContext), "Orders").Single();
        object positiveRecipient = Get(positiveOrder, "Recipient");
        object positiveFill = ReflectionTestHelpers.New("MarketOrderFill", positiveOrder, 1, 10);

        Assert.That(TryPrepare(positiveRecipient,
            TestEconomyFactory.ListOf("MarketOrderFill", positiveFill), out object positiveReceipt), Is.True);
        Commit(positiveReceipt);
        Commit(positiveReceipt);
        Assert.That(Get(positivePopulation, "livingStandard"), Is.EqualTo(2.01).Within(0.000001));

        using Context zeroContext = new();
        object zeroPopulation = zeroContext.AddPopulation("zero-once", 1_000, 20L, 2.0);
        zeroContext.Initialize();
        zeroContext.AddProduct(FoodA, 10, 10);
        zeroContext.SetFoods(FoodA);
        object zeroOrder = Items(Collect(zeroContext), "Orders").Single();
        object zeroRecipient = Get(zeroOrder, "Recipient");
        object staleFill = ReflectionTestHelpers.New("MarketOrderFill", zeroOrder, 1, 10);

        Assert.That(TryPrepare(zeroRecipient,
            TestEconomyFactory.ListOf("MarketOrderFill", staleFill), out object staleReceipt), Is.True);
        Assert.That(TryPrepare(zeroRecipient, TestEconomyFactory.ListOf("MarketOrderFill"),
            out object zeroReceipt), Is.True);
        Commit(staleReceipt);
        Commit(zeroReceipt);
        Commit(zeroReceipt);
        Assert.That(Get(zeroPopulation, "livingStandard"), Is.EqualTo(1.95).Within(0.000001));
        Assert.That(Get(positiveProduct, "Stock"), Is.EqualTo(10));
    }

    private static object Collect(Context context, object access = null)
    {
        GameObject gameObject = new("PopulationMarketOrderTests");
        try
        {
            object engine = gameObject.AddComponent(ReflectionTestHelpers.Find("EconomicEngine"));
            MethodInfo method = engine.GetType().GetMethod("CollectFoodOrders", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing EconomicEngine.CollectFoodOrders.");
            return method.Invoke(engine, new[] { context.Province, access ?? context.LocalAccess, context.Budgets });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static object Resolve(object province)
    {
        object[] arguments = { province, null };
        Assert.That((bool)ReflectionTestHelpers.Find("MarketAccess").GetMethod("TryResolve").Invoke(null, arguments), Is.True);
        return arguments[1];
    }

    private static object Plan(Context context, IReadOnlyList<object> orders)
    {
        object[] arguments =
        {
            TestEconomyFactory.ListOf("MarketOrder", orders.ToArray()),
            TestEconomyFactory.ListOf("ProductState", context.Products.Values.Cast<object>().ToArray()),
            context.Ledger, ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500), null, null
        };
        Assert.That((bool)ReflectionTestHelpers.Find("MarketClearingEngine").GetMethod("TryPlan").Invoke(null, arguments), Is.True, arguments[5] as string);
        return arguments[4];
    }

    private static bool Settle(object plan, object ledger, IReadOnlyList<object> recipients)
    {
        object[] arguments = { ledger, TestEconomyFactory.ListOf("IMarketOrderRecipient", recipients.ToArray()), null };
        return (bool)plan.GetType().GetMethod("TrySettle").Invoke(plan, arguments);
    }

    private static bool TryPrepare(object recipient, object fills, out object receipt)
    {
        object[] arguments = { fills, null };
        bool prepared = (bool)recipient.GetType().GetMethod("TryPrepareReceipt").Invoke(recipient, arguments);
        receipt = arguments[1];
        return prepared;
    }

    private static void Commit(object receipt) =>
        ReflectionTestHelpers.Find("IPreparedMarketReceipt").GetMethod("Commit").Invoke(receipt, null);

    private static IReadOnlyList<object> Items(object source, string property) =>
        ((IEnumerable)Get(source, property)).Cast<object>().ToList().AsReadOnly();
    private static object Get(object instance, string member) => ReflectionTestHelpers.Get(instance, member);
    private static void Set(object instance, string member, object value) => ReflectionTestHelpers.Set(instance, member, value);
    private static long Balance(object populationOrAccount) => (long)Get(
        populationOrAccount.GetType().Name == "MoneyAccount" ? populationOrAccount : Get(populationOrAccount, "Account"), "Balance");

    private sealed class Context : IDisposable
    {
        private readonly object nation = TestEconomyFactory.NewNation("PopulationOrderNation", 0L);
        private readonly IDictionary categories;
        private readonly object previousFoods;
        private readonly IList foods;
        public object Province { get; }
        public object Ledger { get; private set; }
        public object Seller { get; private set; }
        public object Treasury => Get(nation, "Account");
        public IDictionary Products => (IDictionary)Get(Get(Province, "market"), "Products");
        public object LocalAccess => ReflectionTestHelpers.New("MarketAccessContext", "population-order:local", Products, Ledger);
        public IDictionary Budgets { get; }

        public Context()
        {
            Province = TestEconomyFactory.NewProvince(1, "PopulationOrderProvince");
            Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", Province), Is.True);
            Set(Province, "market", ReflectionTestHelpers.New("ProvinceMarket", "PopulationOrderProvince"));
            Type account = ReflectionTestHelpers.Find("MoneyAccount");
            Budgets = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(account, typeof(long)));
            categories = (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField("CATEGORIES").GetValue(null);
            previousFoods = categories.Contains("basic_food") ? categories["basic_food"] : null;
            foods = new List<string>();
            categories["basic_food"] = foods;
        }

        public object AddPopulation(string cultureName, int count, long balance, double livingStandard)
        {
            object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
            Set(species, "name", "Human");
            object group = ReflectionTestHelpers.New("EthnicGroup", species, ReflectionTestHelpers.New("Culture", cultureName));
            object population = ReflectionTestHelpers.New("ProvinceEthnicPop", Province, group,
                new List<int> { 0, count, 0, 0 }, balance, livingStandard);
            ((IList)Get(Province, "provinceEthnicPops")).Add(population);
            return population;
        }

        public object AddProduct(string name, int price, int stock)
        {
            object product = ReflectionTestHelpers.New("ProductState", name, price);
            Products[name] = product;
            product.GetType().GetMethod("AddSupply").Invoke(product, new[] { Seller, (object)stock });
            return product;
        }

        public object AddNationalProduct(string name, int price, int stock)
        {
            IDictionary products = (IDictionary)Get(Get(nation, "market"), "Products");
            object product = ReflectionTestHelpers.New("ProductState", name, price);
            products[name] = product;
            product.GetType().GetMethod("AddSupply").Invoke(product, new[] { Seller, (object)stock });
            return product;
        }

        public void SetFoods(params string[] names)
        {
            foods.Clear();
            foreach (string name in names) foods.Add(name);
        }

        public void Initialize()
        {
            ReflectionTestHelpers.Call<object>(Province, "InitializePopulation");
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
                new[] { TestEconomyFactory.ListOf("Nation", nation), TestEconomyFactory.ListOf("Province", Province) });
            Ledger = Get(Province, "ActiveLedger");
            Seller = ReflectionTestHelpers.New("MoneyAccount", "population-order-seller", 0L);
            Assert.That(ReflectionTestHelpers.Call<bool>(Ledger, "RegisterEmptyAccount", Seller), Is.True);
            Set(Ledger, "SalesTaxBasisPoints", 1000);
            foreach (object population in ((IEnumerable)Get(Province, "provinceEthnicPops")).Cast<object>())
                Budgets.Add(Get(population, "Account"), Balance(population));
        }

        public string Snapshot(object population) => string.Join("|", new[]
        {
            Balance(population).ToString(), Get(population, "livingStandard").ToString(),
            Get(Ledger, "WeeklyTaxRevenue").ToString(), ((ICollection)Get(Ledger, "Transactions")).Count.ToString()
        }.Concat(Products.Values.Cast<object>().Select(product => $"{Get(product, "Stock")}:{Get(product, "LastDemand")}")));

        public void Dispose()
        {
            if (previousFoods == null)
                categories.Remove("basic_food");
            else
                categories["basic_food"] = previousFoods;
        }
    }
}
