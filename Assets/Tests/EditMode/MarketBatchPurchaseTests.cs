using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public class MarketBatchPurchaseTests
{
    [Test]
    public void EmptyBatch_DoesNotSucceed()
    {
        object requests = TestEconomyFactory.ListOf("MarketBuyerRequest");
        var method = ReflectionTestHelpers.Find("MarketSettlement").GetMethod("TryPurchaseBatch");
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { requests, null }), Is.False);
    }

    [Test]
    public void MarketBuyerRequest_BlankRequestIdIsRejected()
    {
        BatchContext context = CreateContext();
        object product = NewProduct("Wheat", 10);

        AssertConstructorRejects(" ", context.BuyerA, product, 1, typeof(ArgumentException));
    }

    [Test]
    public void MarketBuyerRequest_MissingBuyerIsRejected()
    {
        object product = NewProduct("Wheat", 10);

        AssertConstructorRejects("request", null, product, 1, typeof(ArgumentNullException));
    }

    [Test]
    public void MarketBuyerRequest_MissingProductIsRejected()
    {
        BatchContext context = CreateContext();

        AssertConstructorRejects("request", context.BuyerA, null, 1, typeof(ArgumentNullException));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void MarketBuyerRequest_NonPositiveQuantityIsRejected(int quantity)
    {
        BatchContext context = CreateContext();
        object product = NewProduct("Wheat", 10);

        AssertConstructorRejects(
            "request", context.BuyerA, product, quantity, typeof(ArgumentOutOfRangeException));
    }

    [Test]
    public void TryPurchaseBatch_TwoBuyersPurchaseOneProductAtomically()
    {
        BatchContext context = CreateContext();
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("Wheat", 10, (seller, 10));
        Seal(context);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");
        int transactionsBefore = TransactionCount(context.Ledger);
        IList requests = Requests(
            Request("A", context.BuyerA, product, 5),
            Request("B", context.BuyerB, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.True);
        Assert.That(Balance(context.BuyerA), Is.EqualTo(50L));
        Assert.That(Balance(context.BuyerB), Is.EqualTo(50L));
        Assert.That(Balance(seller), Is.EqualTo(100L));
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(GetInt(product, "Stock"), Is.Zero);
        Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(10));
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore + 1));
    }

    [Test]
    public void TryPurchaseBatch_WhenSecondBuyerCannotAffordRequestChangesNothing()
    {
        BatchContext context = CreateContext(100L, 49L);
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("Wheat", 10, (seller, 10));
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, context.BuyerB, seller }, product);
        IList requests = Requests(
            Request("A", context.BuyerA, product, 5),
            Request("B", context.BuyerB, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, context.BuyerB, seller }, product);
    }

    [Test]
    public void TryPurchaseBatch_UsesProductTotalWhenCalculatingTax()
    {
        BatchContext context = CreateContext(100L, 100L, salesTaxBasisPoints: 1000);
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("Copper", 5, (seller, 2));
        Seal(context);
        IList requests = Requests(
            Request("A", context.BuyerA, product, 1),
            Request("B", context.BuyerB, product, 1));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.True);
        Assert.That(Balance(context.BuyerA), Is.EqualTo(95L));
        Assert.That(Balance(context.BuyerB), Is.EqualTo(95L));
        Assert.That(Balance(seller), Is.EqualTo(9L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(1L));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(1L));
    }

    [Test]
    public void TryPurchaseBatch_WhenSupplierBelongsToAnotherLedgerChangesNothing()
    {
        BatchContext context = CreateContext();
        BatchContext foreign = CreateContext(0L, 0L, "foreign");
        object foreignSeller = AddAccount(foreign, "foreign:seller", 0L);
        object product = NewProduct("Iron", 10, (foreignSeller, 10));
        Seal(context);
        Seal(foreign);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, foreignSeller }, product);
        int foreignTransactionsBefore = TransactionCount(foreign.Ledger);
        IList requests = Requests(Request("foreign-supplier", context.BuyerA, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, foreignSeller }, product);
        Assert.That(TransactionCount(foreign.Ledger), Is.EqualTo(foreignTransactionsBefore));
    }

    [Test]
    public void TryPurchaseBatch_WhenBuyerBelongsToAnotherLedgerChangesNothing()
    {
        BatchContext context = CreateContext();
        BatchContext foreign = CreateContext(100L, 0L, "foreign");
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("Iron", 10, (seller, 10));
        Seal(context);
        Seal(foreign);
        BatchSnapshot before = Snapshot(context, new[] { foreign.BuyerA, seller }, product);
        IList requests = Requests(Request("foreign-buyer", foreign.BuyerA, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { foreign.BuyerA, seller }, product);
    }

    [Test]
    public void TryPurchaseBatch_DuplicateRequestIdChangesNothing()
    {
        BatchContext context = CreateContext();
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("Wheat", 10, (seller, 10));
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, context.BuyerB, seller }, product);
        IList requests = Requests(
            Request("duplicate", context.BuyerA, product, 5),
            Request("duplicate", context.BuyerB, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, context.BuyerB, seller }, product);
    }

    [Test]
    public void TryPurchaseBatch_CombinedRequestsExceedingStockChangeNothing()
    {
        BatchContext context = CreateContext();
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("Wheat", 10, (seller, 9));
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, context.BuyerB, seller }, product);
        IList requests = Requests(
            Request("A", context.BuyerA, product, 5),
            Request("B", context.BuyerB, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, context.BuyerB, seller }, product);
    }

    [Test]
    public void TryPurchaseBatch_OneBuyerCannotSplitUnaffordableTotalAcrossRequests()
    {
        BatchContext context = CreateContext(100L, 0L);
        object seller = AddAccount(context, "seller", 0L);
        object wheat = NewProduct("Wheat", 10, (seller, 6));
        object coal = NewProduct("Coal", 10, (seller, 6));
        Seal(context);
        BatchSnapshot before = Snapshot(
            context, new[] { context.BuyerA, seller }, wheat, coal);
        IList requests = Requests(
            Request("wheat", context.BuyerA, wheat, 6),
            Request("coal", context.BuyerA, coal, 6));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, seller }, wheat, coal);
    }

    [Test]
    public void TryPurchaseBatch_SaleIncomeDoesNotIncreaseBuyerAffordability()
    {
        BatchContext context = CreateContext(49L, 0L);
        object product = NewProduct("SelfSupplied", 10, (context.BuyerA, 5));
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA }, product);
        IList requests = Requests(Request("self-supplied", context.BuyerA, product, 5));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA }, product);
    }

    [Test]
    public void TryPurchaseBatch_WhenLastDemandWouldOverflowChangesNothing()
    {
        BatchContext context = CreateContext();
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("DemandOverflow", 10, (seller, 1));
        ReflectionTestHelpers.Set(product, "LastDemand", int.MaxValue);
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, seller }, product);
        IList requests = Requests(Request("demand-overflow", context.BuyerA, product, 1));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, seller }, product);
    }

    [Test]
    public void TryPurchaseBatch_WhenTaxCalculationOverflowsChangesNothing()
    {
        BatchContext context = CreateContext(long.MaxValue, 0L, salesTaxBasisPoints: 10_000);
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("TaxOverflow", int.MaxValue, (seller, int.MaxValue));
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, seller }, product);
        IList requests = Requests(
            Request("tax-overflow", context.BuyerA, product, int.MaxValue));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, seller }, product);
    }

    [Test]
    public void TryPurchaseBatch_NonPositivePriceChangesNothing()
    {
        BatchContext context = CreateContext();
        object seller = AddAccount(context, "seller", 0L);
        object product = NewProduct("InvalidPrice", 10, (seller, 1));
        ReflectionTestHelpers.Set(product, "Price", 0);
        Seal(context);
        BatchSnapshot before = Snapshot(context, new[] { context.BuyerA, seller }, product);
        IList requests = Requests(Request("invalid-price", context.BuyerA, product, 1));

        bool success = TryPurchaseBatch(requests, context.Ledger);

        Assert.That(success, Is.False);
        AssertUnchanged(before, context, new[] { context.BuyerA, seller }, product);
    }

    private static BatchContext CreateContext(
        long buyerABalance = 100L,
        long buyerBBalance = 100L,
        string prefix = "domestic",
        int salesTaxBasisPoints = 0)
    {
        object treasury = New("MoneyAccount", $"{prefix}:treasury", 0L);
        object buyerA = New("MoneyAccount", $"{prefix}:buyer-a", buyerABalance);
        object buyerB = New("MoneyAccount", $"{prefix}:buyer-b", buyerBBalance);
        object ledger = New("MoneyLedger", prefix, new object(), treasury);
        Assert.That(Call<bool>(ledger, "RegisterInitialAccount", treasury), Is.True);
        Assert.That(Call<bool>(ledger, "RegisterInitialAccount", buyerA), Is.True);
        Assert.That(Call<bool>(ledger, "RegisterInitialAccount", buyerB), Is.True);
        ReflectionTestHelpers.Set(ledger, "SalesTaxBasisPoints", salesTaxBasisPoints);
        return new BatchContext(ledger, treasury, buyerA, buyerB);
    }

    private static object AddAccount(BatchContext context, string id, long balance)
    {
        object account = New("MoneyAccount", id, balance);
        Assert.That(Call<bool>(context.Ledger, "RegisterInitialAccount", account), Is.True);
        return account;
    }

    private static object NewProduct(
        string name,
        int price,
        params (object Supplier, int Quantity)[] lots)
    {
        object product = New("ProductState", name, price);
        foreach ((object supplier, int quantity) in lots)
            Call(product, "AddSupply", supplier, quantity);
        return product;
    }

    private static object Request(
        string requestId,
        object buyer,
        object product,
        int quantity) =>
        New("MarketBuyerRequest", requestId, buyer, product, quantity);

    private static IList Requests(params object[] requests) =>
        (IList)TestEconomyFactory.ListOf("MarketBuyerRequest", requests);

    private static bool TryPurchaseBatch(IList requests, object ledger)
    {
        MethodInfo method = ReflectionTestHelpers.Find("MarketSettlement").GetMethod(
            "TryPurchaseBatch", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(null, new[] { (object)requests, ledger });
    }

    private static BatchSnapshot Snapshot(
        BatchContext context,
        object[] accounts,
        params object[] products) =>
        new(
            Array.ConvertAll(accounts, Balance),
            Array.ConvertAll(products, SnapshotProduct),
            GetLong(context.Ledger, "WeeklyTaxRevenue"),
            TransactionCount(context.Ledger));

    private static ProductSnapshot SnapshotProduct(object product) =>
        new(GetInt(product, "Stock"), GetInt(product, "LastDemand"), GetInt(product, "LastSupply"));

    private static void AssertUnchanged(
        BatchSnapshot before,
        BatchContext context,
        object[] accounts,
        params object[] products)
    {
        Assert.That(Array.ConvertAll(accounts, Balance), Is.EqualTo(before.AccountBalances));
        Assert.That(Array.ConvertAll(products, SnapshotProduct), Is.EqualTo(before.Products));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(before.WeeklyTaxRevenue));
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(before.TransactionCount));
    }

    private static void AssertConstructorRejects(
        string requestId,
        object buyer,
        object product,
        int quantity,
        Type expectedException)
    {
        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
            New("MarketBuyerRequest", requestId, buyer, product, quantity));
        Assert.That(thrown.InnerException, Is.TypeOf(expectedException));
    }

    private static void Seal(BatchContext context) => Call(context.Ledger, "SealInitialization");
    private static int TransactionCount(object ledger) =>
        ((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count;
    private static long Balance(object account) => GetLong(account, "Balance");
    private static int GetInt(object instance, string name) =>
        (int)ReflectionTestHelpers.Get(instance, name);
    private static long GetLong(object instance, string name) =>
        (long)ReflectionTestHelpers.Get(instance, name);
    private static object New(string name, params object[] arguments) =>
        ReflectionTestHelpers.New(name, arguments);
    private static T Call<T>(object instance, string name, params object[] arguments) =>
        ReflectionTestHelpers.Call<T>(instance, name, arguments);
    private static object Call(object instance, string name, params object[] arguments) =>
        ReflectionTestHelpers.Call<object>(instance, name, arguments);

    private sealed class BatchContext
    {
        public object Ledger { get; }
        public object Treasury { get; }
        public object BuyerA { get; }
        public object BuyerB { get; }

        public BatchContext(object ledger, object treasury, object buyerA, object buyerB)
        {
            Ledger = ledger;
            Treasury = treasury;
            BuyerA = buyerA;
            BuyerB = buyerB;
        }
    }

    private sealed class BatchSnapshot
    {
        public long[] AccountBalances { get; }
        public ProductSnapshot[] Products { get; }
        public long WeeklyTaxRevenue { get; }
        public int TransactionCount { get; }

        public BatchSnapshot(
            long[] accountBalances,
            ProductSnapshot[] products,
            long weeklyTaxRevenue,
            int transactionCount)
        {
            AccountBalances = accountBalances;
            Products = products;
            WeeklyTaxRevenue = weeklyTaxRevenue;
            TransactionCount = transactionCount;
        }
    }

    private sealed class ProductSnapshot
    {
        public int Stock { get; }
        public int LastDemand { get; }
        public int LastSupply { get; }

        public ProductSnapshot(int stock, int lastDemand, int lastSupply)
        {
            Stock = stock;
            LastDemand = lastDemand;
            LastSupply = lastSupply;
        }

        public override bool Equals(object other) =>
            other is ProductSnapshot snapshot &&
            Stock == snapshot.Stock &&
            LastDemand == snapshot.LastDemand &&
            LastSupply == snapshot.LastSupply;

        public override int GetHashCode() => HashCode.Combine(Stock, LastDemand, LastSupply);
    }
}
