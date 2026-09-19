using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class FactoryMarketOrderTests
{
    private const string Iron = "factory-order-test-iron";
    private const string Wood = "factory-order-test-wood";
    private const string Output = "factory-order-test-output";
    private readonly List<string> recipeNames = new();

    [TearDown]
    public void RemoveTestRecipes()
    {
        IDictionary recipes = (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        foreach (string name in recipeNames) recipes.Remove(name);
    }

    [Test]
    public void Collect_MultiInputFactoryRequestsOnlyNextWeekShortfall()
    {
        Context context = new(1_000L, recipeNames);
        object building = context.AddFactory("shortfall", 1L, 1L,
            new Dictionary<string, int> { [Iron] = 20, [Wood] = 5 });
        object iron = context.AddProduct(Iron, 10);
        object wood = context.AddProduct(Wood, 20);
        RestoreInputs(building, new Dictionary<string, long> { [Iron] = 8L });
        context.Initialize();

        IReadOnlyList<object> orders = Collect(context, true);
        object ironOrder = orders.Single(order => (string)Get(Get(order, "Product"), "ProductName") == Iron);
        object woodOrder = orders.Single(order => (string)Get(Get(order, "Product"), "ProductName") == Wood);
        Assert.That(orders.Count, Is.EqualTo(2));
        Assert.That(Get(ironOrder, "Product"), Is.SameAs(iron));
        Assert.That(Get(woodOrder, "Product"), Is.SameAs(wood));
        Assert.That(Get(ironOrder, "Quantity"), Is.EqualTo(12));
        Assert.That(Get(woodOrder, "Quantity"), Is.EqualTo(5));
        Assert.That(Get(ironOrder, "MaximumUnitPrice"), Is.EqualTo(13));
        Assert.That(Get(woodOrder, "MaximumUnitPrice"), Is.EqualTo(25));
        Assert.That(InputQuantities(building), Is.EqualTo(new Dictionary<string, long> { [Iron] = 8L }));
    }

    [Test]
    public void Collect_SplitsSharedBudgetByShortfallReferenceCostAndRetainsRemainder()
    {
        Context context = new(100L, recipeNames);
        object building = context.AddFactory("budget", 1L, 1L,
            new Dictionary<string, int> { [Iron] = 12, [Wood] = 5 });
        context.AddProduct(Iron, 10);
        context.AddProduct(Wood, 20);
        context.Initialize();

        IReadOnlyList<object> orders = Collect(context, true);
        Assert.That(Get(orders.Single(order => (string)Get(Get(order, "Product"), "ProductName") == Iron), "Quantity"), Is.EqualTo(5));
        Assert.That(Get(orders.Single(order => (string)Get(Get(order, "Product"), "ProductName") == Wood), "Quantity"), Is.EqualTo(2));
        Assert.That(orders.Sum(order => (long)Get(order, "ReservedBudget")), Is.EqualTo(94L));
        Assert.That(context.Budgets[Get(building, "Account")], Is.EqualTo(6L));
    }

    [TestCase(false, 1L, true, false)]
    [TestCase(true, 0L, true, false)]
    [TestCase(true, 1L, false, false)]
    [TestCase(true, 1L, true, true)]
    public void Collect_ExcludesUnpaidOrIneligibleBuildings(bool payrollPaid, long workers,
        bool hasInputs, bool constructionCompany)
    {
        Context context = new(100L, recipeNames);
        context.AddFactory("ineligible" + workers + hasInputs + constructionCompany,
            workers, 1L, hasInputs ? new Dictionary<string, int> { [Iron] = 1 } : new Dictionary<string, int>(),
            constructionCompany);
        context.AddProduct(Iron, 10);
        context.Initialize();

        Assert.That(Collect(context, payrollPaid), Is.Empty);
    }

    [Test]
    public void Recipient_AggregatesPartialFillsAndZeroFillsAreNoOps()
    {
        Context context = new(100L, recipeNames);
        object building = context.AddFactory("receipt", 2L, 1L,
            new Dictionary<string, int> { [Iron] = 2 });
        context.AddProduct(Iron, 10);
        context.Initialize();
        object order = Collect(context, true).Single();
        object recipient = Get(order, "Recipient");

        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill"), out object empty), Is.True);
        Commit(empty);
        Assert.That(InputQuantities(building), Is.Empty);

        object first = ReflectionTestHelpers.New("MarketOrderFill", order, 1, 10);
        object second = ReflectionTestHelpers.New("MarketOrderFill", order, 1, 10);
        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill", first, second), out object receipt), Is.True);
        Assert.That(InputQuantities(building), Is.Empty);
        Commit(receipt);
        Assert.That(InputQuantities(building), Is.EqualTo(new Dictionary<string, long> { [Iron] = 2L }));
    }

    [Test]
    public void Recipient_ZeroFillPreflightSupersedesAnEarlierReceiptWithoutChangingInventory()
    {
        Context context = new(100L, recipeNames);
        object building = context.AddFactory("zero-supersedes", 2L, 1L,
            new Dictionary<string, int> { [Iron] = 2 });
        context.AddProduct(Iron, 10);
        context.Initialize();
        object order = Collect(context, true).Single();
        object recipient = Get(order, "Recipient");
        object fill = ReflectionTestHelpers.New("MarketOrderFill", order, 1, 10);

        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill", fill), out object stale), Is.True);
        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill"), out object current), Is.True);
        Commit(stale);
        Commit(current);

        Assert.That(InputQuantities(building), Is.Empty);
    }

    [Test]
    public void Recipient_RejectsForeignAndUnemittedOrdersWithoutPreparingInput()
    {
        Context context = new(100L, recipeNames);
        object building = context.AddFactory("recipient-ownership", 1L, 1L,
            new Dictionary<string, int> { [Iron] = 1 });
        context.AddProduct(Iron, 10);
        context.Initialize();
        object order = Collect(context, true).Single();
        object recipient = Get(order, "Recipient");
        object product = Get(order, "Product");
        object foreignBuyer = ReflectionTestHelpers.New("MoneyAccount", "factory-order-test-foreign", 0L);
        object foreignOrder = ReflectionTestHelpers.New("MarketOrder", "foreign", foreignBuyer,
            product, 1, 10, 10L, 1, recipient);
        object unemittedOrder = ReflectionTestHelpers.New("MarketOrder", "unemitted", Get(order, "Buyer"),
            product, 1, 10, 10L, 1, recipient);

        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill",
            ReflectionTestHelpers.New("MarketOrderFill", foreignOrder, 1, 10)), out object foreignReceipt), Is.False);
        Assert.That(foreignReceipt, Is.Null);
        Assert.That(TryPrepare(recipient, TestEconomyFactory.ListOf("MarketOrderFill",
            ReflectionTestHelpers.New("MarketOrderFill", unemittedOrder, 1, 10)), out object unemittedReceipt), Is.False);
        Assert.That(unemittedReceipt, Is.Null);
        Assert.That(InputQuantities(building), Is.Empty);
    }

    [Test]
    public void Collect_DuplicateEligibleBuildingLeavesBudgetsAndBuildingsUnchanged()
    {
        Context context = new(100L, recipeNames);
        object building = context.AddFactory("duplicate", 1L, 1L,
            new Dictionary<string, int> { [Iron] = 1 });
        context.AddProduct(Iron, 10);
        context.Initialize();
        long budgetBefore = (long)context.Budgets[Get(building, "Account")];
        context.DuplicateParticipant(building);

        Assert.That(Collect(context, true), Is.Empty);
        Assert.That(context.Budgets[Get(building, "Account")], Is.EqualTo(budgetBefore));
        Assert.That(InputQuantities(building), Is.Empty);
    }

    private static IReadOnlyList<object> Collect(Context context, bool payrollPaid)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("FactoryMarketOrders"))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Missing FactoryMarketOrders collector.");
        MethodInfo method = type.GetMethod("Collect", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Missing FactoryMarketOrders.Collect.");
        return ((IEnumerable)method.Invoke(null,
            new[] { context.Province, context.Access, (object)payrollPaid, context.Budgets }))
            .Cast<object>().ToList().AsReadOnly();
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

    private static void RestoreInputs(object building, IReadOnlyDictionary<string, long> quantities) =>
        building.GetType().GetMethod("RestoreInputInventory", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(building, new object[] { quantities });

    private static Dictionary<string, long> InputQuantities(object building) =>
        ((IEnumerable)Get(building, "InputInventory")).Cast<object>().ToDictionary(
            pair => (string)Get(pair, "Key"), pair => (long)Get(pair, "Value"));

    private static object Get(object instance, string member) => ReflectionTestHelpers.Get(instance, member);
    private static void Set(object instance, string member, object value) => ReflectionTestHelpers.Set(instance, member, value);

    private sealed class Context
    {
        private readonly object nation;
        private readonly object seller = ReflectionTestHelpers.New("MoneyAccount", "factory-order-test-seller", 0L);
        private readonly IDictionary products;
        private readonly IList recipeNames;
        public object Province { get; }
        public object Ledger { get; private set; }
        public object Access => ReflectionTestHelpers.New("MarketAccessContext", "factory-order-test", products, Ledger);
        public IDictionary Budgets { get; }

        public Context(long initialCapital, IList recipeNames)
        {
            this.recipeNames = recipeNames;
            nation = TestEconomyFactory.NewNation("FactoryOrderNation", initialCapital);
            Province = TestEconomyFactory.NewProvince(1, "FactoryOrderProvince");
            Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", Province), Is.True);
            object market = ReflectionTestHelpers.New("ProvinceMarket", Get(Province, "name"));
            Set(Province, "market", market);
            products = (IDictionary)Get(market, "Products");
            Type account = ReflectionTestHelpers.Find("MoneyAccount");
            Budgets = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(account, typeof(long)));
        }

        public object AddFactory(string suffix, long workers, long workerNeeded,
            Dictionary<string, int> inputs, bool constructionCompany = false)
        {
            string name = "FactoryOrder" + suffix;
            object type = ReflectionTestHelpers.New("BuildingType", name);
            Set(type, "workerNeeded", workerNeeded);
            Set(type, "requireItems", inputs);
            Set(type, "produceItems", new Dictionary<string, int> { [Output] = 1 });
            object recipe = ReflectionTestHelpers.New("BuildingRecipe", name);
            Set(recipe, "InitialCapital", (long)Get(nation, "balance"));
            IDictionary recipes = (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
                "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            recipes[name] = recipe;
            recipeNames.Add(name);
            object building = constructionCompany
                ? ReflectionTestHelpers.New("ConstructionCompanyBuilding", type, Province, 1)
                : ReflectionTestHelpers.New("Building", type, Province);
            Set(building, "level", 1);
            Set(building, "currentWorkers", workers);
            ((IDictionary)Get(Province, "buildings")).Add(type, building);
            return building;
        }

        public object AddProduct(string name, int price)
        {
            object product = ReflectionTestHelpers.New("ProductState", name, price);
            products.Add(name, product);
            return product;
        }

        public void DuplicateParticipant(object building)
        {
            object duplicateType = ReflectionTestHelpers.New("BuildingType", "FactoryOrderDuplicateKey");
            ((IDictionary)Get(Province, "buildings")).Add(duplicateType, building);
        }

        public void Initialize()
        {
            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
                new[] { TestEconomyFactory.ListOf("Nation", nation), TestEconomyFactory.ListOf("Province", Province) });
            Ledger = Get(Province, "ActiveLedger");
            Assert.That(ReflectionTestHelpers.Call<bool>(Ledger, "RegisterEmptyAccount", seller), Is.True);
            foreach (object building in ((IDictionary)Get(Province, "buildings")).Values)
            {
                object account = Get(building, "Account");
                Budgets.Add(account, (long)Get(account, "Balance"));
            }
        }
    }
}
