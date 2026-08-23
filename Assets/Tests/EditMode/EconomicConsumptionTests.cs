using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EconomicConsumptionTests
{
    [Test]
    public void ConsumeFoodsWeekly_NationMarketChargesEachPopulationWithoutPoolingWealth()
    {
        const string food = "consumption-test-wheat";
        WithCategory("basic_food", food, () =>
        {
            ConsumptionContext context = CreateNationalContext(food);
            object first = AddPopulation(context.Province, "First", 10_000, 20L, 1.0);
            object second = AddPopulation(context.Province, "Second", 10_000, 100L, 1.0);
            Initialize(context, first, second);
            object seller = AddSeller(context, "food-seller");
            object product = AddProduct(context.NationMarket, food, seller, 20);
            long supplyBefore = GetLong(context.Ledger, "MoneySupply");

            ConsumeFoods(context.Province, true);

            Assert.That(Balance(first), Is.EqualTo(0L));
            Assert.That(Balance(second), Is.EqualTo(0L));
            Assert.That(Balance(seller), Is.EqualTo(108L));
            Assert.That(Balance(context.Treasury), Is.EqualTo(12L));
            Assert.That(GetInt(product, "Stock"), Is.EqualTo(8));
            Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(12));
            Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
            Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(12L));
        });
    }

    [Test]
    public void ConsumeFoodsWeekly_NoAvailableFoodChangesLivingStandardButNotMoney()
    {
        const string food = "consumption-test-missing-wheat";
        WithCategory("basic_food", food, () =>
        {
            ConsumptionContext context = CreateNationalContext(food);
            object population = AddPopulation(context.Province, "Hungry", 1_000, 30L, 2.0);
            Initialize(context, population);

            ConsumeFoods(context.Province, false);

            Assert.That(Balance(population), Is.EqualTo(30L));
            Assert.That((double)ReflectionTestHelpers.Get(population, "livingStandard"), Is.EqualTo(1.95));
            Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(30L));
        });
    }

    [Test]
    public void ConsumeGoodsWeekly_ProvinceMarketSettlesIndividualPurchases()
    {
        const string good = "consumption-test-cloth";
        WithCategory("clothing_material", good, () =>
        {
            ConsumptionContext context = CreateNationalContext(good);
            object population = AddPopulation(context.Province, "ClothBuyer", 10_000, 50L, 1.0);
            Initialize(context, population);
            object seller = AddSeller(context, "cloth-seller");
            object product = AddProduct(context.ProvinceMarket, good, seller, 5);

            ConsumeGoods(context.Province, "clothing_material", false);

            Assert.That(Balance(population), Is.EqualTo(0L));
            Assert.That(Balance(seller), Is.EqualTo(45L));
            Assert.That(Balance(context.Treasury), Is.EqualTo(5L));
            Assert.That(GetInt(product, "Stock"), Is.Zero);
            Assert.That(GetInt(product, "LastDemand"), Is.EqualTo(5));
            Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(50L));
        });
    }

    private static void WithCategory(string category, string product, Action test)
    {
        IDictionary categories = (IDictionary)ReflectionTestHelpers.Find("GlobalVariables")
            .GetField("CATEGORIES", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        object previous = categories.Contains(category) ? categories[category] : null;
        categories[category] = new List<string> { product };
        try
        {
            test();
        }
        finally
        {
            if (previous == null)
                categories.Remove(category);
            else
                categories[category] = previous;
        }
    }

    private static ConsumptionContext CreateNationalContext(string product)
    {
        object nation = TestEconomyFactory.NewNation("ConsumptionNation", 0L);
        object province = TestEconomyFactory.NewProvince(1, "ConsumptionProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);

        object provinceMarket = ReflectionTestHelpers.New("ProvinceMarket", "ConsumptionProvince");
        ReflectionTestHelpers.Set(province, "market", provinceMarket);
        return new ConsumptionContext(nation, province, provinceMarket);
    }

    private static object AddPopulation(
        object province,
        string cultureName,
        int count,
        long balance,
        double livingStandard)
    {
        object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
        ReflectionTestHelpers.Set(species, "name", "Human");
        object culture = ReflectionTestHelpers.New("Culture", cultureName);
        object group = ReflectionTestHelpers.New("EthnicGroup", species, culture);
        object population = ReflectionTestHelpers.New("ProvinceEthnicPop", province, group,
            new List<int> { 0, count, 0, 0 }, balance, livingStandard);
        ((IList)ReflectionTestHelpers.Get(province, "provinceEthnicPops")).Add(population);
        return population;
    }

    private static void Initialize(ConsumptionContext context, params object[] populations)
    {
        ReflectionTestHelpers.Call<object>(context.Province, "InitializePopulation");
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
            new[]
            {
                TestEconomyFactory.ListOf("Nation", context.Nation),
                TestEconomyFactory.ListOf("Province", context.Province)
            });
        context.Ledger = ReflectionTestHelpers.Get(context.Province, "ActiveLedger");
        context.Treasury = ReflectionTestHelpers.Get(context.Nation, "Account");
        ReflectionTestHelpers.Set(context.Ledger, "SalesTaxBasisPoints", 1_000);
    }

    private static object AddSeller(ConsumptionContext context, string id)
    {
        object seller = ReflectionTestHelpers.New("MoneyAccount", id, 0L);
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger, "RegisterEmptyAccount", seller), Is.True);
        return seller;
    }

    private static object AddProduct(object market, string name, object seller, int quantity)
    {
        ReflectionTestHelpers.Call<object>(market, "AddProduct", name, 10);
        object product = ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))[name];
        ReflectionTestHelpers.Call<object>(product, "AddSupply", seller, quantity);
        return product;
    }

    private static void ConsumeFoods(object province, bool connectedToCapital) =>
        InvokeEngine("ConsumeFoodsWeekly", province, connectedToCapital);

    private static void ConsumeGoods(object province, string category, bool connectedToCapital) =>
        InvokeEngine("ConsumeGoodsWeekly", province, category, connectedToCapital);

    private static void InvokeEngine(string method, params object[] arguments)
    {
        GameObject gameObject = new("EconomicConsumptionTests");
        try
        {
            object engine = gameObject.AddComponent(ReflectionTestHelpers.Find("EconomicEngine"));
            engine.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
                .Invoke(engine, arguments);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static long Balance(object populationOrAccount)
    {
        object account = populationOrAccount.GetType().Name == "MoneyAccount"
            ? populationOrAccount
            : ReflectionTestHelpers.Get(populationOrAccount, "Account");
        return GetLong(account, "Balance");
    }

    private static int GetInt(object instance, string member) =>
        (int)ReflectionTestHelpers.Get(instance, member);

    private static long GetLong(object instance, string member) =>
        (long)ReflectionTestHelpers.Get(instance, member);

    private sealed class ConsumptionContext
    {
        public object Nation { get; }
        public object Province { get; }
        public object ProvinceMarket { get; }
        public object NationMarket => ReflectionTestHelpers.Get(Nation, "market");
        public object Ledger { get; set; }
        public object Treasury { get; set; }

        public ConsumptionContext(object nation, object province, object provinceMarket)
        {
            Nation = nation;
            Province = province;
            ProvinceMarket = provinceMarket;
        }
    }
}
