using System.Collections;
using System.Reflection;
using System;
using NUnit.Framework;

public class MarketAccessTests
{
    [Test]
    public void TryResolve_ConnectedProvinceUsesNationMarketAndActiveLedger()
    {
        object nation = NewNation("ConnectedNation");
        object province = InitializeProvince("ConnectedProvince", nation);
        ReflectionTestHelpers.Set(province, "isConnectedToCapital", true);
        object localMarket = ReflectionTestHelpers.New("ProvinceMarket", "ConnectedProvince");
        ReflectionTestHelpers.Set(province, "market", localMarket);
        object nationMarket = ReflectionTestHelpers.Get(nation, "market");
        object nationalProducts = ReflectionTestHelpers.Get(nationMarket, "Products");
        object localProducts = ReflectionTestHelpers.Get(localMarket, "Products");
        AsDictionary(nationalProducts).Add("Iron", ReflectionTestHelpers.New("ProductState", "Iron", 10));
        AsDictionary(localProducts).Add("Iron", ReflectionTestHelpers.New("ProductState", "Iron", 20));

        Assert.That(TryResolve(province, out object context), Is.True);
        Assert.That(ReflectionTestHelpers.Get(context, "Products"), Is.SameAs(nationalProducts));
        Assert.That(ReflectionTestHelpers.Get(context, "Ledger"),
            Is.SameAs(ReflectionTestHelpers.Get(province, "ActiveLedger")));
        Assert.That(ReflectionTestHelpers.Get(context, "StableId"), Is.EqualTo("nation:ConnectedNation"));
    }

    [Test]
    public void TryResolve_IsolatedProvinceUsesLocalMarket()
    {
        object nation = NewNation("IsolatedOwner");
        object province = InitializeProvince("IsolatedProvince", nation);
        ReflectionTestHelpers.Set(province, "isConnectedToCapital", false);
        object market = ReflectionTestHelpers.New("ProvinceMarket", "IsolatedProvince");
        ReflectionTestHelpers.Set(province, "market", market);

        Assert.That(TryResolve(province, out object context), Is.True);
        Assert.That(ReflectionTestHelpers.Get(context, "Products"),
            Is.SameAs(ReflectionTestHelpers.Get(market, "Products")));
        Assert.That(ReflectionTestHelpers.Get(context, "Ledger"),
            Is.SameAs(ReflectionTestHelpers.Get(province, "ActiveLedger")));
        Assert.That(ReflectionTestHelpers.Get(context, "StableId"), Is.EqualTo("province:IsolatedProvince"));
    }

    [Test]
    public void TryResolve_NeutralProvinceUsesLocalMarket()
    {
        object province = InitializeProvince("NeutralProvince");
        object market = ReflectionTestHelpers.New("ProvinceMarket", "NeutralProvince");
        ReflectionTestHelpers.Set(province, "market", market);

        Assert.That(TryResolve(province, out object context), Is.True);
        Assert.That(ReflectionTestHelpers.Get(context, "Products"),
            Is.SameAs(ReflectionTestHelpers.Get(market, "Products")));
        Assert.That(ReflectionTestHelpers.Get(context, "Ledger"),
            Is.SameAs(ReflectionTestHelpers.Get(province, "ActiveLedger")));
        Assert.That(ReflectionTestHelpers.Get(context, "StableId"), Is.EqualTo("province:NeutralProvince"));
    }

    [Test]
    public void TryResolve_NullProvinceFails()
    {
        Assert.That(TryResolve(null, out _), Is.False);
    }

    [Test]
    public void TryResolve_ProvinceWithoutMarketFails()
    {
        object province = InitializeProvince("MissingMarket");

        Assert.That(TryResolve(province, out _), Is.False);
    }

    [Test]
    public void TryResolve_ProvinceWithoutActiveLedgerFails()
    {
        object province = TestEconomyFactory.NewProvince(1, "MissingLedger");
        ReflectionTestHelpers.Set(province, "market",
            ReflectionTestHelpers.New("ProvinceMarket", "MissingLedger"));

        Assert.That(TryResolve(province, out _), Is.False);
    }

    [Test]
    public void TryResolve_ConnectedProvinceWithoutNationFails()
    {
        object province = InitializeProvince("ConnectedWithoutNation");
        ReflectionTestHelpers.Set(province, "isConnectedToCapital", true);
        ReflectionTestHelpers.Set(province, "market",
            ReflectionTestHelpers.New("ProvinceMarket", "ConnectedWithoutNation"));

        Assert.That(TryResolve(province, out _), Is.False);
    }

    [Test]
    public void TryResolve_ConnectedProvinceWithoutNationMarketFails()
    {
        object nation = NewNation("MissingNationMarket");
        object province = InitializeProvince("ProvinceWithMissingNationMarket", nation);
        ReflectionTestHelpers.Set(province, "isConnectedToCapital", true);
        ReflectionTestHelpers.Set(nation, "market", null);

        Assert.That(TryResolve(province, out _), Is.False);
    }

    private static object NewNation(string name) =>
        TestEconomyFactory.NewNation(name, 0L);

    private static object InitializeProvince(string name, object nation = null)
    {
        object province = TestEconomyFactory.NewProvince(1, name);
        if (nation != null)
            Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);

        IList nations = (IList)TestEconomyFactory.ListOf("Nation",
            nation is null ? Array.Empty<object>() : new[] { nation });
        IList provinces = (IList)TestEconomyFactory.ListOf("Province", province);
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
            new object[] { nations, provinces });
        return province;
    }

    private static IDictionary AsDictionary(object dictionary) => (IDictionary)dictionary;

    private static bool TryResolve(object province, out object context)
    {
        MethodInfo method = ReflectionTestHelpers.Find("MarketAccess").GetMethod(
            "TryResolve", BindingFlags.Public | BindingFlags.Static);
        object[] arguments = { province, null };
        bool result = (bool)method.Invoke(null, arguments);
        context = arguments[1];
        return result;
    }
}
