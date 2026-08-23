using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public class MarketSettlementTests
{
    [Test]
    public void TryPurchase_SettlesGrossTaxAndSellerNetWithoutChangingSupply()
    {
        SettlementContext context = CreateContext(1000L);
        object alpha = AddSeller(context, "seller:alpha", 0L);
        object beta = AddSeller(context, "seller:beta", 0L);
        object product = NewProduct("Wheat", 10, (alpha, 60), (beta, 40));
        Seal(context);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");

        object result = TryPurchase(product, context.Buyer, 50, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.True);
        Assert.That(GetInt(result, "PurchasedQuantity"), Is.EqualTo(50));
        Assert.That(GetLong(result, "GrossAmount"), Is.EqualTo(500L));
        Assert.That(GetLong(result, "TaxAmount"), Is.EqualTo(50L));
        Assert.That(Balance(context.Buyer), Is.EqualTo(500L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(50L));
        Assert.That(Balance(alpha), Is.EqualTo(270L));
        Assert.That(Balance(beta), Is.EqualTo(180L));
        Assert.That(GetInt(product, "Stock"), Is.EqualTo(50));
        Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(50));
        Assert.That(GetInt(product, "LastSupply"), Is.EqualTo(100));
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(50L));
    }

    [Test]
    public void TryPurchase_ClampsQuantityToAffordableGross()
    {
        SettlementContext context = CreateContext(95L);
        object seller = AddSeller(context, "seller", 0L);
        object product = NewProduct("Tools", 10, (seller, 20));
        Seal(context);

        object result = TryPurchase(product, context.Buyer, 20, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.True);
        Assert.That(GetInt(result, "PurchasedQuantity"), Is.EqualTo(9));
        Assert.That(Balance(context.Buyer), Is.EqualTo(5L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(9L));
        Assert.That(Balance(seller), Is.EqualTo(81L));
        Assert.That(GetInt(product, "Stock"), Is.EqualTo(11));
        Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(9));
        Assert.That(GetInt(product, "LastSupply"), Is.EqualTo(20));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(9L));
    }

    [Test]
    public void TryPurchase_WithZeroStock_ChangesNoSettlementState()
    {
        SettlementContext context = CreateContext(1000L);
        object product = NewProduct("Coal", 10);
        Seal(context);
        SettlementSnapshot before = Snapshot(context, product);

        object result = TryPurchase(product, context.Buyer, 5, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.False);
        Assert.That(GetInt(result, "PurchasedQuantity"), Is.Zero);
        AssertUnchanged(before, context, product);
    }

    [Test]
    public void TryPurchase_WithCrossLedgerSeller_RejectsBeforeAnyMutation()
    {
        SettlementContext context = CreateContext(1000L);
        SettlementContext foreign = CreateContext(0L, "foreign");
        object foreignSeller = AddSeller(foreign, "foreign:seller", 0L);
        object product = NewProduct("Iron", 10, (foreignSeller, 10));
        Seal(context);
        Seal(foreign);
        SettlementSnapshot before = Snapshot(context, product, foreignSeller);
        int foreignTransactions = TransactionCount(foreign.Ledger);

        object result = TryPurchase(product, context.Buyer, 5, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(before, context, product, foreignSeller);
        Assert.That(TransactionCount(foreign.Ledger), Is.EqualTo(foreignTransactions));
    }

    [Test]
    public void TryPurchase_WhenSellerCreditOverflows_ChangesNoSettlementState()
    {
        SettlementContext context = CreateContext(1000L);
        object seller = AddSeller(context, "seller", 0L);
        object product = NewProduct("Gold", 10, (seller, 1));
        Seal(context);
        CorruptBalanceForAuditTest(seller, long.MaxValue);
        SettlementSnapshot before = Snapshot(context, product, seller);

        object result = TryPurchase(product, context.Buyer, 1, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(before, context, product, seller);
    }

    [Test]
    public void TryPurchaseBasket_RequireFullQuantityRejectsEntireClampedBasket()
    {
        SettlementContext context = CreateContext(1000L);
        object seller = AddSeller(context, "seller", 0L);
        object wheat = NewProduct("Wheat", 10, (seller, 10));
        object coal = NewProduct("Coal", 20, (seller, 1));
        Seal(context);
        SettlementSnapshot wheatBefore = Snapshot(context, wheat, seller);
        ProductSnapshot coalBefore = SnapshotProduct(coal);
        IList requests = ListOf(
            "PurchaseRequest",
            New("PurchaseRequest", wheat, 5),
            New("PurchaseRequest", coal, 2));

        object result = InvokeStatic("MarketSettlement", "TryPurchaseBasket",
            requests, context.Buyer, context.Ledger, true);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(wheatBefore, context, wheat, seller);
        AssertProductUnchanged(coalBefore, coal);
    }

    [Test]
    public void BeginWeek_ResetsOnlyWeeklyTaxRevenue()
    {
        SettlementContext context = CreateContext(1000L);
        object seller = AddSeller(context, "seller", 0L);
        object product = NewProduct("Wheat", 10, (seller, 10));
        Seal(context);
        TryPurchase(product, context.Buyer, 5, context.Ledger);
        long buyerBalance = Balance(context.Buyer);
        long treasuryBalance = Balance(context.Treasury);
        long sellerBalance = Balance(seller);
        int transactions = TransactionCount(context.Ledger);

        Call(context.Ledger, "BeginWeek");

        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(Balance(context.Buyer), Is.EqualTo(buyerBalance));
        Assert.That(Balance(context.Treasury), Is.EqualTo(treasuryBalance));
        Assert.That(Balance(seller), Is.EqualTo(sellerBalance));
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactions));
    }

    [Test]
    public void TryPurchaseBasket_SettlesMultipleProductsAsOneExactBasket()
    {
        SettlementContext context = CreateContext(1000L);
        object wheatSeller = AddSeller(context, "basket:wheat-seller", 0L);
        object coalSeller = AddSeller(context, "basket:coal-seller", 0L);
        object wheat = NewProduct("BasketWheat", 10, (wheatSeller, 5));
        object coal = NewProduct("BasketCoal", 20, (coalSeller, 3));
        Seal(context);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");
        IList requests = ListOf(
            "PurchaseRequest",
            New("PurchaseRequest", wheat, 2),
            New("PurchaseRequest", coal, 3));

        object result = InvokeStatic(
            "MarketSettlement", "TryPurchaseBasket", requests, context.Buyer, context.Ledger, true);

        Assert.That(GetBool(result, "Success"), Is.True);
        Assert.That(((ICollection)ReflectionTestHelpers.Get(result, "Purchases")).Count,
            Is.EqualTo(2));
        Assert.That(GetLong(result, "TotalPurchasedQuantity"), Is.EqualTo(5L));
        Assert.That(GetLong(result, "GrossAmount"), Is.EqualTo(80L));
        Assert.That(GetLong(result, "TaxAmount"), Is.EqualTo(8L));
        Assert.That(Balance(context.Buyer), Is.EqualTo(920L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(8L));
        Assert.That(Balance(wheatSeller), Is.EqualTo(18L));
        Assert.That(Balance(coalSeller), Is.EqualTo(54L));
        Assert.That(GetInt(wheat, "Stock"), Is.EqualTo(3));
        Assert.That(GetInt(wheat, "LastDemand"), Is.EqualTo(2));
        Assert.That(GetInt(coal, "Stock"), Is.Zero);
        Assert.That(GetInt(coal, "LastDemand"), Is.EqualTo(3));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(8L));
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
    }

    [Test]
    public void TryPurchaseBasket_DuplicateProductRejectsEverythingAtomically()
    {
        SettlementContext context = CreateContext(1000L);
        object seller = AddSeller(context, "duplicate:seller", 0L);
        object product = NewProduct("DuplicateProduct", 10, (seller, 10));
        Seal(context);
        SettlementSnapshot before = Snapshot(context, product, seller);
        IList requests = ListOf(
            "PurchaseRequest",
            New("PurchaseRequest", product, 2),
            New("PurchaseRequest", product, 3));

        object result = InvokeStatic(
            "MarketSettlement", "TryPurchaseBasket", requests, context.Buyer, context.Ledger, false);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(before, context, product, seller);
    }

    [Test]
    public void TryPurchase_AggregatesBuyerAndTreasuryWhenBothAlsoOwnSellerLots()
    {
        SettlementContext context = CreateContext(1000L);
        object product = NewProduct(
            "AliasedActors",
            10,
            (context.Buyer, 10),
            (context.Treasury, 10));
        Seal(context);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");

        object result = TryPurchase(product, context.Buyer, 10, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.True);
        Assert.That(GetLong(result, "GrossAmount"), Is.EqualTo(100L));
        Assert.That(GetLong(result, "TaxAmount"), Is.EqualTo(10L));
        Assert.That(Balance(context.Buyer), Is.EqualTo(945L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(55L));
        Assert.That(GetInt(product, "Stock"), Is.EqualTo(10));
        Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(10));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(10L));
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
    }

    [Test]
    public void TryPurchase_WhenTaxMultiplicationOverflowsRejectsEverythingAtomically()
    {
        SettlementContext context = CreateContext(long.MaxValue);
        object seller = AddSeller(context, "tax-overflow:seller", 0L);
        object product = NewProduct(
            "TaxOverflow", int.MaxValue, (seller, int.MaxValue));
        Seal(context);
        SettlementSnapshot before = Snapshot(context, product, seller);

        object result = TryPurchase(
            product, context.Buyer, int.MaxValue, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(before, context, product, seller);
    }

    [Test]
    public void TryPurchase_WhenWeeklyTaxWouldOverflowRejectsEverythingAtomically()
    {
        SettlementContext context = CreateContext(1000L);
        object seller = AddSeller(context, "weekly-overflow:seller", 0L);
        object product = NewProduct("WeeklyTaxOverflow", 10, (seller, 10));
        Seal(context);
        IList seedEntries = ListOf(
            "MoneyTransferEntry",
            New("MoneyTransferEntry", context.Buyer, -1L),
            New("MoneyTransferEntry", seller, 1L));
        Assert.That(Call<bool>(
            context.Ledger, "TryTransferBatch", seedEntries, "Seed weekly tax capacity", long.MaxValue),
            Is.True);
        SettlementSnapshot before = Snapshot(context, product, seller);

        object result = TryPurchase(product, context.Buyer, 1, context.Ledger);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(before, context, product, seller);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void TryPurchaseBasket_RequireFullQuantityRejectsFundsDrivenClampAtomically()
    {
        SettlementContext context = CreateContext(25L);
        object seller = AddSeller(context, "full-funds:seller", 0L);
        object product = NewProduct("FullFunds", 10, (seller, 10));
        Seal(context);
        SettlementSnapshot before = Snapshot(context, product, seller);
        IList requests = ListOf(
            "PurchaseRequest", New("PurchaseRequest", product, 3));

        object result = InvokeStatic(
            "MarketSettlement", "TryPurchaseBasket", requests, context.Buyer, context.Ledger, true);

        Assert.That(GetBool(result, "Success"), Is.False);
        AssertUnchanged(before, context, product, seller);
    }

    private static SettlementContext CreateContext(long buyerBalance, string prefix = "domestic")
    {
        object treasury = New("MoneyAccount", $"{prefix}:treasury", 0L);
        object buyer = New("MoneyAccount", $"{prefix}:buyer", buyerBalance);
        object ledger = New("MoneyLedger", prefix, new object(), treasury);
        Assert.That(Call<bool>(ledger, "RegisterInitialAccount", treasury), Is.True);
        Assert.That(Call<bool>(ledger, "RegisterInitialAccount", buyer), Is.True);
        ReflectionTestHelpers.Set(ledger, "SalesTaxBasisPoints", 1000);
        return new SettlementContext(ledger, treasury, buyer);
    }

    private static object AddSeller(SettlementContext context, string id, long balance)
    {
        object seller = New("MoneyAccount", id, balance);
        Assert.That(Call<bool>(context.Ledger, "RegisterInitialAccount", seller), Is.True);
        return seller;
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

    private static void Seal(SettlementContext context) => Call(context.Ledger, "SealInitialization");

    private static object TryPurchase(object product, object buyer, int quantity, object ledger) =>
        InvokeStatic("MarketSettlement", "TryPurchase", product, buyer, quantity, ledger);

    private static SettlementSnapshot Snapshot(
        SettlementContext context,
        object product,
        params object[] sellers) =>
        new(
            Balance(context.Buyer),
            Balance(context.Treasury),
            Array.ConvertAll(sellers, Balance),
            SnapshotProduct(product),
            GetLong(context.Ledger, "WeeklyTaxRevenue"),
            TransactionCount(context.Ledger));

    private static ProductSnapshot SnapshotProduct(object product) =>
        new(GetInt(product, "Stock"), GetInt(product, "LastDemand"), GetInt(product, "LastSupply"));

    private static void AssertUnchanged(
        SettlementSnapshot before,
        SettlementContext context,
        object product,
        params object[] sellers)
    {
        Assert.That(Balance(context.Buyer), Is.EqualTo(before.BuyerBalance));
        Assert.That(Balance(context.Treasury), Is.EqualTo(before.TreasuryBalance));
        Assert.That(Array.ConvertAll(sellers, Balance), Is.EqualTo(before.SellerBalances));
        AssertProductUnchanged(before.Product, product);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(before.WeeklyTaxRevenue));
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(before.TransactionCount));
    }

    private static void AssertProductUnchanged(ProductSnapshot before, object product)
    {
        Assert.That(GetInt(product, "Stock"), Is.EqualTo(before.Stock));
        Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(before.LastDemand));
        Assert.That(GetInt(product, "LastSupply"), Is.EqualTo(before.LastSupply));
    }

    private static void CorruptBalanceForAuditTest(object account, long balance)
    {
        FieldInfo field = account.GetType().GetField(
            "<Balance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(account, balance);
    }

    private static int TransactionCount(object ledger) =>
        ((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count;

    private static long Balance(object account) => GetLong(account, "Balance");
    private static bool GetBool(object instance, string name) => (bool)ReflectionTestHelpers.Get(instance, name);
    private static int GetInt(object instance, string name) => (int)ReflectionTestHelpers.Get(instance, name);
    private static long GetLong(object instance, string name) => (long)ReflectionTestHelpers.Get(instance, name);

    private static object New(string name, params object[] arguments) =>
        ReflectionTestHelpers.New(name, arguments);

    private static T Call<T>(object instance, string name, params object[] arguments) =>
        ReflectionTestHelpers.Call<T>(instance, name, arguments);

    private static object Call(object instance, string name, params object[] arguments) =>
        ReflectionTestHelpers.Call<object>(instance, name, arguments);

    private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
    {
        MethodInfo method = ReflectionTestHelpers.Find(typeName).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            Array.ConvertAll(arguments, argument => argument.GetType()),
            null);
        if (method == null)
        {
            foreach (MethodInfo candidate in ReflectionTestHelpers.Find(typeName).GetMethods(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (candidate.Name == methodName &&
                    candidate.GetParameters().Length == arguments.Length)
                {
                    method = candidate;
                    break;
                }
            }
        }

        Assert.That(method, Is.Not.Null, $"Missing public static method {methodName}");
        return method.Invoke(null, arguments);
    }

    private static IList ListOf(string runtimeType, params object[] values) =>
        (IList)TestEconomyFactory.ListOf(runtimeType, values);

    private sealed class SettlementContext
    {
        public object Ledger { get; }
        public object Treasury { get; }
        public object Buyer { get; }

        public SettlementContext(object ledger, object treasury, object buyer)
        {
            Ledger = ledger;
            Treasury = treasury;
            Buyer = buyer;
        }
    }

    private sealed class SettlementSnapshot
    {
        public long BuyerBalance { get; }
        public long TreasuryBalance { get; }
        public long[] SellerBalances { get; }
        public ProductSnapshot Product { get; }
        public long WeeklyTaxRevenue { get; }
        public int TransactionCount { get; }

        public SettlementSnapshot(
            long buyerBalance,
            long treasuryBalance,
            long[] sellerBalances,
            ProductSnapshot product,
            long weeklyTaxRevenue,
            int transactionCount)
        {
            BuyerBalance = buyerBalance;
            TreasuryBalance = treasuryBalance;
            SellerBalances = sellerBalances;
            Product = product;
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
    }
}
