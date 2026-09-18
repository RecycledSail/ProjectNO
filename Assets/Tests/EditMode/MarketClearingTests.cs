using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class MarketClearingTests
{
    private object ledger, treasury, seller, buyer, product, settings;

    [SetUp]
    public void SetUp()
    {
        treasury = Account("treasury", 0);
        seller = Account("seller", 0);
        buyer = Account("buyer", 1000);
        ledger = ReflectionTestHelpers.New("MoneyLedger", "coin", new object(), treasury);
        foreach (object account in new[] { treasury, seller, buyer })
            Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", account), Is.True);
        product = ReflectionTestHelpers.New("ProductState", "Iron", 10);
        settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
    }

    [Test]
    public void TryPlan_FullSupplyUsesReferencePriceAndCapturesUnorderedProduct()
    {
        Supply(20);
        object unused = ReflectionTestHelpers.New("ProductState", "Coal", 20);
        ReflectionTestHelpers.Call<object>(unused, "AddSupply", seller, 7);
        object plan = Plan(new[] { Order("B", 6, 12), Order("A", 6, 15) }, new[] { product, unused });
        Assert.That(FillSummary(plan), Is.EquivalentTo(new[] { "A:6:10", "B:6:10" }));
        object iron = Result(plan, product);
        AssertResult(iron, 12, 12, 20, 10, 9);
        AssertResult(Result(plan, unused), 0, 0, 7, 20, 15);
        Assert.That(Items(plan, "Purchases").Sum(p => Number(p, "Quantity")), Is.EqualTo(12));
    }

    [Test]
    public void TryPlan_ShortageUsesMarginalBidAndProRatesBoundaryTieWithoutMutation()
    {
        Supply(10);
        SeedStatistics();
        object recipient = Recipient("recipient");
        string before = Snapshot();
        object plan = Plan(new[] { Order("C", 6, 12), Order("A", 6, 15, recipient: recipient), Order("B", 6, 12) });
        Assert.That(FillSummary(plan), Is.EqualTo(new[] { "A:6:12", "B:2:12", "C:2:12" }));
        AssertResult(Result(plan, product), 18, 10, 10, 12, 12);
        Assert.That(Items(plan, "Fills").Sum(f => (long)ReflectionTestHelpers.Get(f, "GrossAmount")), Is.EqualTo(120));
        Assert.That(Snapshot(), Is.EqualTo(before));
        Assert.That(((MarketClearingRecipientProxy)recipient).Calls, Is.Zero);
    }

    [Test]
    public void TryPlan_HigherBidsExcludeLowerBidsAndUseLastWinningBid()
    {
        Supply(6);
        object plan = Plan(new[] { Order("low", 5, 11), Order("high", 6, 20) });
        Assert.That(FillSummary(plan), Is.EqualTo(new[] { "high:6:20" }));
    }

    [Test]
    public void TryPlan_BelowReferenceBidsDoNotCompeteOrForceShortage()
    {
        Supply(5);
        object plan = Plan(new[] { Order("low", 10, 9), Order("eligible", 5, 15) });
        Assert.That(FillSummary(plan), Is.EqualTo(new[] { "eligible:5:10" }));
        Assert.That(Number(Result(plan, product), "RequestedDemand"), Is.EqualTo(5));
    }

    [Test]
    public void TryPlan_BoundaryRemainderUsesOrdinalIds()
    {
        Supply(5);
        object plan = Plan(new[] { Order("a", 3, 12), Order("B", 3, 12) });
        Assert.That(FillSummary(plan), Is.EqualTo(new[] { "B:3:12", "a:2:12" }));
    }

    [Test]
    public void TryPlan_MinimumFillDropsInvalidSharesAndRedistributesWithinBoundary()
    {
        Supply(5);
        object plan = Plan(new[] { Order("A", 4, 12, minimum: 4), Order("B", 6, 12) });
        Assert.That(FillSummary(plan), Is.EqualTo(new[] { "B:5:12" }));
    }

    [Test]
    public void TryPlan_MinimumFillRedistributionNeverExceedsRemainingOrderQuantity()
    {
        Supply(5);
        object plan = Plan(new[] { Order("A", 4, 12, minimum: 4), Order("B", 3, 12) });
        Assert.That(FillSummary(plan), Is.EqualTo(new[] { "B:3:12" }));
    }

    [Test]
    public void TryPlan_AllBoundaryOrdersBelowMinimumProduceNoFills()
    {
        Supply(3);
        object plan = Plan(new[] { Order("A", 4, 12, minimum: 4), Order("B", 4, 12, minimum: 4) });
        Assert.That(Items(plan, "Fills"), Is.Empty);
        Assert.That(Number(Result(plan, product), "SoldQuantity"), Is.Zero);
    }

    [Test]
    public void TryPlan_ZeroStockAndNoOrdersHaveReferenceClearingPrice()
    {
        object plan = Plan(new[] { Order("A", 6, 15) });
        AssertResult(Result(plan, product), 6, 0, 0, 10, 13);
        Assert.That(Items(plan, "Purchases"), Is.Empty);
        AssertResult(Result(Plan(Array.Empty<object>()), product), 0, 0, 0, 10, 10);
    }

    [Test]
    public void TryPlan_IsInvariantUnderOrderProductAndSupplierInsertionOrder()
    {
        object otherSeller = Account("another", 0);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", otherSeller), Is.True);
        ReflectionTestHelpers.Call<object>(product, "AddSupply", otherSeller, 3);
        Supply(4);
        object coal = ReflectionTestHelpers.New("ProductState", "Coal", 10);
        ReflectionTestHelpers.Call<object>(coal, "AddSupply", seller, 4);
        ReflectionTestHelpers.Call<object>(coal, "AddSupply", otherSeller, 3);
        object[] orders = { Order("A", 4, 15), Order("B", 4, 12), Order("C", 4, 12), Order("D", 5, 12, item: coal) };
        string[] first = FillSummary(Plan(orders, new[] { coal, product }));
        for (int shift = 0; shift < orders.Length; shift++)
        {
            object[] rotated = orders.Skip(shift).Concat(orders.Take(shift)).Reverse().ToArray();
            object plan = Plan(rotated, new[] { product, coal });
            Assert.That(FillSummary(plan), Is.EqualTo(first));
            Assert.That(Items(plan, "ProductResults").Select(r => ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(r, "Product"), "ProductName")),
                Is.EqualTo(new[] { "Coal", "Iron" }));
        }
    }

    [TestCase("duplicate-order")]
    [TestCase("duplicate-product")]
    [TestCase("missing-product")]
    [TestCase("foreign-buyer")]
    [TestCase("foreign-supplier")]
    [TestCase("null-order")]
    [TestCase("null-product")]
    [TestCase("bad-price")]
    [TestCase("bad-elasticity")]
    [TestCase("duplicate-recipient")]
    public void TryPlan_StructuralFailureChangesNothing(string failure)
    {
        Supply(10);
        SeedStatistics();
        object[] orders = { Order("A", 2, 12) };
        object[] products = { product };
        switch (failure)
        {
            case "duplicate-order": orders = new[] { orders[0], Order("A", 1, 12) }; break;
            case "duplicate-product": products = new[] { product, product }; break;
            case "missing-product": products = Array.Empty<object>(); break;
            case "foreign-buyer": orders = new[] { Order("foreign", 1, 12, account: Account("foreign", 100)) }; break;
            case "foreign-supplier": ReflectionTestHelpers.Call<object>(product, "AddSupply", Account("foreign", 0), 1); break;
            case "null-order": orders = new object[] { null }; break;
            case "null-product": products = new object[] { null }; break;
            case "bad-price": ReflectionTestHelpers.Set(product, "Price", 0); break;
            case "bad-elasticity": ReflectionTestHelpers.Set(product, "Elasticity", float.NaN); break;
            case "duplicate-recipient": orders = new[] { Order("A", 1, 12, recipient: Recipient("same")), Order("B", 1, 12, recipient: Recipient("same")) }; break;
        }
        string before = Snapshot();
        Assert.That(TryPlan(orders, products, out object plan, out string error), Is.False);
        Assert.That(plan, Is.Null);
        Assert.That(error, Is.Not.Empty);
        Assert.That(Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public void TryPlan_AggregateReservationAboveBuyerBalanceFailsWithoutMutation()
    {
        Supply(10);
        string before = Snapshot();
        Assert.That(TryPlan(new[] { Order("A", 1, 12, budget: 600), Order("B", 1, 12, budget: 600) }, new[] { product }, out object plan, out string error), Is.False);
        Assert.That(error, Does.Contain("reserved budget").IgnoreCase);
        Assert.That(plan, Is.Null);
        Assert.That(Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public void TryPlan_ReservationOverflowReturnsFalse()
    {
        Assert.That(TryPlan(new[] { Order("A", 1, 12, budget: long.MaxValue), Order("B", 1, 12, budget: long.MaxValue) }, new[] { product }, out _, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryPlan_DemandOverflowReturnsFalseWithoutMutation()
    {
        object rich = Account("rich", long.MaxValue - 1000);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", rich), Is.True);
        string before = Snapshot();
        Assert.That(TryPlan(new[] { Order("A", int.MaxValue, 10, account: rich), Order("B", 1, 10, account: rich) }, new[] { product }, out _, out string error), Is.False);
        Assert.That(error, Does.Contain("overflow").IgnoreCase);
        Assert.That(Snapshot(), Is.EqualTo(before));
    }

    [TestCase("id")]
    [TestCase("buyer")]
    [TestCase("product")]
    [TestCase("quantity")]
    [TestCase("price")]
    [TestCase("budget")]
    [TestCase("minimum-zero")]
    [TestCase("minimum-large")]
    [TestCase("recipient")]
    [TestCase("recipient-id")]
    [TestCase("underfunded")]
    public void MarketOrder_RejectsInvalidOrUnfundedValues(string invalid)
    {
        object[] values = { "order", buyer, product, 2, 12, 24L, 1, Recipient("recipient") };
        switch (invalid)
        {
            case "id": values[0] = " "; break;
            case "buyer": values[1] = null; break;
            case "product": values[2] = null; break;
            case "quantity": values[3] = 0; break;
            case "price": values[4] = 0; break;
            case "budget": values[5] = 0L; break;
            case "minimum-zero": values[6] = 0; break;
            case "minimum-large": values[6] = 3; break;
            case "recipient": values[7] = null; break;
            case "recipient-id": values[7] = Recipient(" "); break;
            case "underfunded": values[5] = 23L; break;
        }
        var exception = Assert.Throws<TargetInvocationException>(() => ReflectionTestHelpers.New("MarketOrder", values));
        Assert.That(exception.InnerException, Is.InstanceOf<ArgumentException>());
    }

    [Test]
    public void MarketOrder_UsesInt64ForQuantityPriceProduct()
    {
        object order = Order("large", int.MaxValue, int.MaxValue, budget: 4611686014132420609L);
        Assert.That(ReflectionTestHelpers.Get(order, "ReservedBudget"), Is.EqualTo(4611686014132420609L));
    }

    [Test]
    public void PlanAndReceiptCollectionsAreReadOnlyAndPreparationDoesNotCommit()
    {
        Supply(10);
        object recipient = Recipient("one");
        object extra = Recipient("zero");
        object plan = Plan(new[] { Order("A", 2, 12, recipient: recipient), Order("B", 2, 12, recipient: recipient) });
        foreach (string propertyName in new[] { "Fills", "Purchases", "ProductResults" })
            Assert.That(((IList)ReflectionTestHelpers.Get(plan, propertyName)).IsReadOnly, Is.True);
        object[] args = { TestEconomyFactory.ListOf("IMarketOrderRecipient", extra, recipient, extra), null };
        Assert.That((bool)plan.GetType().GetMethod("PrepareReceipts").Invoke(plan, args), Is.True);
        Assert.That(((IList)args[1]).IsReadOnly, Is.True);
        Assert.That(((IList)args[1]).Count, Is.EqualTo(2));
        var one = (MarketClearingRecipientProxy)recipient;
        var zero = (MarketClearingRecipientProxy)extra;
        Assert.That(one.Calls, Is.EqualTo(1));
        Assert.That(one.Fills.Count, Is.EqualTo(2));
        Assert.That(one.Fills.IsReadOnly, Is.True);
        Assert.That(zero.Calls, Is.EqualTo(1));
        Assert.That(zero.Fills, Is.Empty);
        Assert.That(((IEnumerable)args[1]).Cast<MarketClearingRecipientProxy>().Sum(receipt => receipt.Commits), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PrepareReceipts_RejectsFailedOrNullPreparationWithoutCommitting(bool nullReceipt)
    {
        object recipient = Recipient("Z:reject");
        object accepted = Recipient("A:accepted");
        var proxy = (MarketClearingRecipientProxy)recipient;
        proxy.Reject = !nullReceipt;
        proxy.NullReceipt = nullReceipt;
        object plan = Plan(new[] { Order("A", 1, 12, recipient: recipient), Order("B", 1, 12, recipient: accepted) });
        object[] args = { TestEconomyFactory.ListOf("IMarketOrderRecipient"), null };
        Assert.That((bool)plan.GetType().GetMethod("PrepareReceipts").Invoke(plan, args), Is.False);
        Assert.That(args[1], Is.Null);
        Assert.That(((MarketClearingRecipientProxy)accepted).Prepared.Commits, Is.Zero);
    }

    private object Order(string id, int quantity, int price, int minimum = 1, long? budget = null, object account = null, object item = null, object recipient = null) =>
        ReflectionTestHelpers.New("MarketOrder", id, account ?? buyer, item ?? product, quantity, price, budget ?? (long)quantity * price, minimum, recipient ?? Recipient(id));
    private static object Account(string id, long balance) => ReflectionTestHelpers.New("MoneyAccount", id, balance);
    private static object Recipient(string id)
    {
        object proxy = typeof(DispatchProxy).GetMethod("Create").MakeGenericMethod(
            ReflectionTestHelpers.Find("IMarketOrderRecipient"), typeof(MarketClearingRecipientProxy)).Invoke(null, null);
        ((MarketClearingRecipientProxy)proxy).Id = id;
        return proxy;
    }
    private void Supply(int amount) => ReflectionTestHelpers.Call<object>(product, "AddSupply", seller, amount);
    private object Plan(object[] orders, object[] products = null)
    {
        Assert.That(TryPlan(orders, products ?? new[] { product }, out object plan, out string error), Is.True, error);
        return plan;
    }
    private bool TryPlan(object[] orders, object[] products, out object plan, out string error)
    {
        object[] args = { TestEconomyFactory.ListOf("MarketOrder", orders), TestEconomyFactory.ListOf("ProductState", products), ledger, settings, null, null };
        bool success = (bool)ReflectionTestHelpers.Find("MarketClearingEngine").GetMethod("TryPlan").Invoke(null, args);
        plan = args[4]; error = (string)args[5]; return success;
    }
    private static IEnumerable<object> Items(object plan, string property) => ((IEnumerable)ReflectionTestHelpers.Get(plan, property)).Cast<object>();
    private static int Number(object obj, string name) => (int)ReflectionTestHelpers.Get(obj, name);
    private static string[] FillSummary(object plan) => Items(plan, "Fills").Select(f => $"{ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(f, "Order"), "Id")}:{Number(f, "Quantity")}:{Number(f, "UnitPrice")}").ToArray();
    private static object Result(object plan, object item) => Items(plan, "ProductResults").Single(r => ReferenceEquals(ReflectionTestHelpers.Get(r, "Product"), item));
    private static void AssertResult(object result, int requested, int sold, int available, int price, int next)
    {
        Assert.That(Number(result, "RequestedDemand"), Is.EqualTo(requested));
        Assert.That(Number(result, "SoldQuantity"), Is.EqualTo(sold));
        Assert.That(Number(result, "AvailableStock"), Is.EqualTo(available));
        Assert.That(Number(result, "ClearingPrice"), Is.EqualTo(price));
        Assert.That(Number(result, "NextPrice"), Is.EqualTo(next));
    }
    private void SeedStatistics()
    {
        foreach (var pair in new Dictionary<string, int> { ["LastPrice"] = 8, ["LastDemand"] = 2, ["RequestedDemand"] = 3, ["UnmetDemand"] = 1, ["LastClearingPrice"] = 9 })
            ReflectionTestHelpers.Set(product, pair.Key, pair.Value);
    }
    private string Snapshot() => string.Join("|", new[] { "Price", "LastPrice", "Stock", "LastDemand", "LastSupply", "RequestedDemand", "UnmetDemand", "LastClearingPrice", "Elasticity" }.Select(n => ReflectionTestHelpers.Get(product, n)))
        + ";" + string.Join("|", new[] { treasury, seller, buyer }.Select(a => ReflectionTestHelpers.Get(a, "Balance")))
        + ";" + ReflectionTestHelpers.Get(ledger, "MoneySupply") + ";" + ReflectionTestHelpers.Get(ledger, "WeeklyTaxRevenue")
        + ";" + ((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count
        + ";" + string.Join("|", ((IEnumerable)ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(product, "Inventory"), "Lots")).Cast<object>().Select(l => ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(l, "Key"), "Id") + ":" + ReflectionTestHelpers.Get(l, "Value")));
}

// The game assembly is intentionally accessed through reflection by this test assembly.
public class MarketClearingRecipientProxy : DispatchProxy
{
    public string Id;
    public int Calls, Commits;
    public IList Fills;
    public MarketClearingRecipientProxy Prepared;
    public bool Reject, NullReceipt;
    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name == "get_Id") return Id;
        if (method.Name == "Commit") { Commits++; return null; }
        Calls++;
        Fills = (IList)args[0];
        args[1] = NullReceipt || Reject ? null : typeof(DispatchProxy).GetMethod("Create").MakeGenericMethod(
            ReflectionTestHelpers.Find("IPreparedMarketReceipt"), typeof(MarketClearingRecipientProxy)).Invoke(null, null);
        Prepared = (MarketClearingRecipientProxy)args[1];
        return !Reject;
    }
}
