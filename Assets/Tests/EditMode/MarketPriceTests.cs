using System;
using System.Reflection;
using NUnit.Framework;

public class MarketPriceTests
{
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
}
