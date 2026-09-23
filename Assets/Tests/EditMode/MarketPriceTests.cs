using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class MarketPriceTests
{
    [TestCase(100, 1, 1000L, 110)]
    [TestCase(101, 1, 1000L, 112)]
    [TestCase(100, 2, 219L, 109)]
    [TestCase(2_000_000_000, 1, long.MaxValue, int.MaxValue)]
    [TestCase(int.MaxValue, int.MaxValue, long.MaxValue, int.MaxValue)]
    public void CalculateMaximumBid_UsesConfiguredCeilingAndAffordableInt64Budget(
        int reference, int quantity, long budget, int expected)
    {
        MethodInfo method = ReflectionTestHelpers.Find("MarketPriceCalculator").GetMethod("CalculateMaximumBid");
        Assert.That(method, Is.Not.Null, "Missing shared configurable bid calculation.");
        Assert.That(method.Invoke(null, new object[] { reference, quantity, budget,
            ReflectionTestHelpers.New("MarketPriceSettings", 3000, 1000) }), Is.EqualTo(expected));
    }

    [TestCase(2_000_000_000, 1, 1, 2_000_000_000)]
    [TestCase(2_000_000_000, 100, 0, int.MaxValue)]
    [TestCase(1_717_986_917, 100, 0, int.MaxValue)]
    [TestCase(1_717_986_918, 100, 0, int.MaxValue)]
    [TestCase(int.MaxValue, 1, 1, int.MaxValue)]
    [TestCase(int.MaxValue, 100, 0, int.MaxValue)]
    [TestCase(int.MaxValue, 0, 100, 1_610_612_735)]
    public void CalculateNextPrice_LargePricesSaturateWithoutWrapping(
        int previous, int demand, int stock, int expected)
    {
        Assert.That(Calculate(previous, demand, stock, 1f,
            ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500)), Is.EqualTo(expected));
    }

    [TestCase(100, 0, 0, 100)]
    [TestCase(100, 100, 0, 125)]
    [TestCase(100, 0, 100, 75)]
    public void CalculateNextPrice_HandlesEmptyAndOneSidedMarkets(
        int previous, int demand, int stock, int expected)
    {
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
        MethodInfo method = ReflectionTestHelpers.Find("MarketPriceCalculator")
            .GetMethod("CalculateNextPrice");

        Assert.That(method.Invoke(null, new object[] { previous, demand, stock, 1f, settings }),
            Is.EqualTo(expected));
    }

    [Test]
    public void MarketPriceSettings_RejectsValuesOutsideBasisPointBounds()
    {
        AssertConstructorRejects(-1, 2500, "smoothingBasisPoints");
        AssertConstructorRejects(10_001, 2500, "smoothingBasisPoints");
        AssertConstructorRejects(3000, 0, "maxWeeklyChangeBasisPoints");
        AssertConstructorRejects(3000, 10_001, "maxWeeklyChangeBasisPoints");
    }

    [Test]
    public void GlobalMarketPriceSettings_UsesConfiguredDefaults()
    {
        Type globals = ReflectionTestHelpers.Find("GlobalVariables");
        FieldInfo field = globals.GetField("MARKET_PRICE_SETTINGS",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);

        object settings = field.GetValue(null);
        Assert.That(ReflectionTestHelpers.Get(settings, "SmoothingBasisPoints"), Is.EqualTo(3000));
        Assert.That(ReflectionTestHelpers.Get(settings, "MaxWeeklyChangeBasisPoints"), Is.EqualTo(2500));
    }

    [Test]
    public void CalculateNextPrice_UsesFractionalDemandToStockRatio()
    {
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 10_000, 10_000);

        Assert.That(Calculate(100, 1, 2, 1f, settings), Is.EqualTo(67));
    }

    [Test]
    public void CalculateNextPrice_NeverDropsBelowOne()
    {
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 10_000, 10_000);

        Assert.That(Calculate(1, 0, int.MaxValue, 1f, settings), Is.EqualTo(1));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void CalculateNextPrice_RejectsNonFiniteElasticity(float elasticity)
    {
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            Calculate(100, 1, 1, elasticity, settings));
        Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void CalculateNextPrice_ClampsWeeklyChangeToTwentyFivePercent()
    {
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 10_000, 2500);

        Assert.That(Calculate(100, 10_000, 0, 1f, settings), Is.EqualTo(125));
        Assert.That(Calculate(100, 0, 10_000, 1f, settings), Is.EqualTo(75));
    }

    [Test]
    public void ProductState_BeginWeek_ResetsWeeklyMarketStateAndRecordsReferencePrice()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Iron", 100);
        ReflectionTestHelpers.Set(product, "Price", 120);
        ReflectionTestHelpers.Set(product, "LastSupply", 8);
        ReflectionTestHelpers.Set(product, "LastDemand", 4);
        ReflectionTestHelpers.Set(product, "RequestedDemand", 10);
        ReflectionTestHelpers.Set(product, "UnmetDemand", 6);
        ReflectionTestHelpers.Set(product, "LastClearingPrice", 115);

        ReflectionTestHelpers.Call<object>(product, "BeginWeek");

        Assert.That(ReflectionTestHelpers.Get(product, "LastPrice"), Is.EqualTo(120));
        Assert.That(ReflectionTestHelpers.Get(product, "LastSupply"), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(product, "LastDemand"), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(product, "RequestedDemand"), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(product, "UnmetDemand"), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(product, "LastClearingPrice"), Is.Zero);
    }

    [Test]
    public void ProductState_CommitClearingStatistics_PreservesActualDemandMeaning()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Iron", 100);
        ReflectionTestHelpers.Set(product, "LastDemand", 4);
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);

        ReflectionTestHelpers.Call<object>(product, "CommitClearingStatistics", 10, 4, 4, 100, settings);

        Assert.That(ReflectionTestHelpers.Get(product, "LastDemand"), Is.EqualTo(4));
        Assert.That(ReflectionTestHelpers.Get(product, "RequestedDemand"), Is.EqualTo(10));
        Assert.That(ReflectionTestHelpers.Get(product, "UnmetDemand"), Is.EqualTo(6));
        Assert.That(ReflectionTestHelpers.Get(product, "LastClearingPrice"), Is.EqualTo(100));
        Assert.That(ReflectionTestHelpers.Get(product, "Price"), Is.EqualTo(125));
    }

    [Test]
    public void ProductState_CommitClearingStatistics_RejectsSoldQuantityDifferentFromActualSales()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Iron", 100);
        ReflectionTestHelpers.Set(product, "LastDemand", 4);
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            ReflectionTestHelpers.Call<object>(product, "CommitClearingStatistics", 10, 3, 4, 100, settings));

        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void ProductState_CommitClearingStatistics_RejectsSalesBeyondAvailableStockWithoutMutation()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Iron", 100);
        object supplier = ReflectionTestHelpers.New("MoneyAccount", "supplier:iron", 0L);
        ReflectionTestHelpers.Call<object>(product, "AddSupply", supplier, 3);
        ReflectionTestHelpers.Set(product, "LastPrice", 99);
        ReflectionTestHelpers.Set(product, "LastDemand", 4);
        ReflectionTestHelpers.Set(product, "RequestedDemand", 5);
        ReflectionTestHelpers.Set(product, "UnmetDemand", 1);
        ReflectionTestHelpers.Set(product, "LastClearingPrice", 98);
        ReflectionTestHelpers.Set(product, "Elasticity", 1.5f);
        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
        ProductStateSnapshot before = Snapshot(product);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            ReflectionTestHelpers.Call<object>(product, "CommitClearingStatistics", 4, 4, 3, 100, settings));

        Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(Snapshot(product), Is.EqualTo(before));
    }

    private static int Calculate(
        int previousPrice,
        int requestedDemand,
        int availableStock,
        float elasticity,
        object settings)
    {
        MethodInfo method = ReflectionTestHelpers.Find("MarketPriceCalculator")
            .GetMethod("CalculateNextPrice");
        Assert.That(method, Is.Not.Null);
        return (int)method.Invoke(null,
            new object[] { previousPrice, requestedDemand, availableStock, elasticity, settings });
    }

    private static void AssertConstructorRejects(
        int smoothingBasisPoints,
        int maxWeeklyChangeBasisPoints,
        string parameterName)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            ReflectionTestHelpers.New("MarketPriceSettings",
                smoothingBasisPoints, maxWeeklyChangeBasisPoints));
        Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(((ArgumentOutOfRangeException)exception.InnerException).ParamName,
            Is.EqualTo(parameterName));
    }

    private static ProductStateSnapshot Snapshot(object product) => new(
        (string)ReflectionTestHelpers.Get(product, "ProductName"),
        (int)ReflectionTestHelpers.Get(product, "Stock"),
        (int)ReflectionTestHelpers.Get(product, "Price"),
        (int)ReflectionTestHelpers.Get(product, "LastPrice"),
        (int)ReflectionTestHelpers.Get(product, "LastDemand"),
        (int)ReflectionTestHelpers.Get(product, "LastSupply"),
        (int)ReflectionTestHelpers.Get(product, "RequestedDemand"),
        (int)ReflectionTestHelpers.Get(product, "UnmetDemand"),
        (int)ReflectionTestHelpers.Get(product, "LastClearingPrice"),
        (float)ReflectionTestHelpers.Get(product, "Elasticity"),
        InventoryLots(product));

    private static IReadOnlyList<InventoryLotSnapshot> InventoryLots(object product) =>
        ((IEnumerable)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(product, "Inventory"), "Lots"))
            .Cast<object>()
            .Select(entry => new InventoryLotSnapshot(
                ReflectionTestHelpers.Get(entry, "Key"),
                (int)ReflectionTestHelpers.Get(entry, "Value")))
            .OrderBy(lot => (string)ReflectionTestHelpers.Get(lot.supplier, "Id"))
            .ToArray();

    private sealed class ProductStateSnapshot
    {
        private readonly string productName;
        private readonly int stock;
        private readonly int price;
        private readonly int lastPrice;
        private readonly int lastDemand;
        private readonly int lastSupply;
        private readonly int requestedDemand;
        private readonly int unmetDemand;
        private readonly int lastClearingPrice;
        private readonly float elasticity;
        private readonly IReadOnlyList<InventoryLotSnapshot> inventoryLots;

        public ProductStateSnapshot(
            string productName,
            int stock,
            int price,
            int lastPrice,
            int lastDemand,
            int lastSupply,
            int requestedDemand,
            int unmetDemand,
            int lastClearingPrice,
            float elasticity,
            IReadOnlyList<InventoryLotSnapshot> inventoryLots)
        {
            this.productName = productName;
            this.stock = stock;
            this.price = price;
            this.lastPrice = lastPrice;
            this.lastDemand = lastDemand;
            this.lastSupply = lastSupply;
            this.requestedDemand = requestedDemand;
            this.unmetDemand = unmetDemand;
            this.lastClearingPrice = lastClearingPrice;
            this.elasticity = elasticity;
            this.inventoryLots = inventoryLots;
        }

        public override bool Equals(object other) =>
            other is ProductStateSnapshot snapshot &&
            productName == snapshot.productName &&
            stock == snapshot.stock &&
            price == snapshot.price &&
            lastPrice == snapshot.lastPrice &&
            lastDemand == snapshot.lastDemand &&
            lastSupply == snapshot.lastSupply &&
            requestedDemand == snapshot.requestedDemand &&
            unmetDemand == snapshot.unmetDemand &&
            lastClearingPrice == snapshot.lastClearingPrice &&
            elasticity.Equals(snapshot.elasticity) &&
            inventoryLots.SequenceEqual(snapshot.inventoryLots);

        public override int GetHashCode() => 0;
    }

    private sealed class InventoryLotSnapshot
    {
        public readonly object supplier;
        private readonly int quantity;

        public InventoryLotSnapshot(object supplier, int quantity)
        {
            this.supplier = supplier;
            this.quantity = quantity;
        }

        public override bool Equals(object other) =>
            other is InventoryLotSnapshot snapshot &&
            ReferenceEquals(supplier, snapshot.supplier) &&
            quantity == snapshot.quantity;

        public override int GetHashCode() => 0;
    }
}
