using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MarketMacroStatisticsTests
{
    private GameObject engineObject;
    private object engine;

    [SetUp]
    public void SetUp()
    {
        engineObject = new GameObject("Market macro statistics test");
        engine = engineObject.AddComponent(ReflectionTestHelpers.Find("EconomicEngine"));
    }

    [TearDown]
    public void TearDown()
    {
        if (engineObject != null)
            Object.DestroyImmediate(engineObject);
    }

    [Test]
    public void CalculateGDP_UsesObservedClearingPriceInsteadOfNextReferencePrice()
    {
        object nation = TestEconomyFactory.NewNation("MacroGdp", 0L);
        object product = Product("Iron", 15, lastPrice: 8, lastClearingPrice: 12,
            lastSupply: 10, lastDemand: 0);
        Products(nation).Add("Iron", product);

        long gdp = ReflectionTestHelpers.Call<long>(engine, "CalculateGDP", nation);

        Assert.That(gdp, Is.EqualTo(120L));
    }

    [Test]
    public void CalculatePriceIndex_UsesClearingPriceAndLastPriceForUntradedProducts()
    {
        object nation = TestEconomyFactory.NewNation("MacroCpi", 0L);
        Products(nation).Add("Iron", Product("Iron", 50, lastPrice: 8,
            lastClearingPrice: 12, lastSupply: 3, lastDemand: 1));
        Products(nation).Add("Wood", Product("Wood", 40, lastPrice: 6,
            lastClearingPrice: 0, lastSupply: 2, lastDemand: 0));
        object budget = ReflectionTestHelpers.Get(nation, "governmentBudget");
        MethodInfo calculate = budget.GetType().GetMethod(
            "CalculatePriceIndex", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(calculate, Is.Not.Null);
        Assert.That((float)calculate.Invoke(budget, null), Is.EqualTo(10f));
    }

    private static IDictionary Products(object nation) =>
        (IDictionary)ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(nation, "market"), "Products");

    private static object Product(
        string name,
        int price,
        int lastPrice,
        int lastClearingPrice,
        int lastSupply,
        int lastDemand)
    {
        object product = ReflectionTestHelpers.New("ProductState", name, price);
        ReflectionTestHelpers.Set(product, "LastPrice", lastPrice);
        ReflectionTestHelpers.Set(product, "LastClearingPrice", lastClearingPrice);
        ReflectionTestHelpers.Set(product, "LastSupply", lastSupply);
        ReflectionTestHelpers.Set(product, "LastDemand", lastDemand);
        return product;
    }
}
