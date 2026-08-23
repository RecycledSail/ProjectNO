using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class BuildingProductionEconomyTests
{
    private const string Input = "building-production-test-input";
    private const string MissingInput = "building-production-test-missing-input";
    private const string Output = "building-production-test-output";
    private const string BuildingName = "BuildingProductionEconomyTestBuilding";

    [TearDown]
    public void RemoveTestRecipe()
    {
        Recipes.Remove(BuildingName);
    }

    [Test]
    public void ProduceGoodsWeekly_ClampsToBuildingFundsSettlesInputsAndRegistersOwnedOutputs()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 2 },
            new Dictionary<string, int> { [Output] = 3 });
        object supplier = AddSeller(context, "building-production-test-supplier");
        object input = AddProduct(context, Input, 5, supplier, 4);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");

        Call(context.Province, "ProduceGoodsWeekly");

        object output = Products(context)[Output];
        Assert.That(Balance(context.Building), Is.EqualTo(0L));
        Assert.That(Balance(supplier), Is.EqualTo(9L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(1L));
        Assert.That(GetInt(input, "Stock"), Is.EqualTo(2));
        Assert.That(GetInt(input, "LastDemand"), Is.EqualTo(2));
        Assert.That(GetInt(output, "Stock"), Is.EqualTo(3));
        Assert.That(LotQuantities(output), Is.EqualTo(new Dictionary<string, int>
        {
            [AccountId(context.Building)] = 3,
        }));
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(1L));
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger, "Audit", (object)null), Is.True);
    }

    [Test]
    public void ProduceGoodsWeekly_WithMissingRecipeInputLeavesInputsMoneyDemandAndOutputUnchanged()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 1, [MissingInput] = 1 },
            new Dictionary<string, int> { [Output] = 3 });
        object supplier = AddSeller(context, "building-production-test-supplier");
        object input = AddProduct(context, Input, 5, supplier, 2);
        int transactionsBefore = TransactionCount(context.Ledger);

        Call(context.Province, "ProduceGoodsWeekly");

        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(supplier), Is.Zero);
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(GetInt(input, "Stock"), Is.EqualTo(2));
        Assert.That(GetInt(input, "LastDemand"), Is.Zero);
        Assert.That(Products(context).Contains(Output), Is.False);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
    }

    [Test]
    public void ProduceGoodsWeekly_WithCrossLedgerInputSellerRejectsTheCompleteBasketWithoutMutation()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 2 },
            new Dictionary<string, int> { [Output] = 3 });
        object foreignSeller = CreateForeignSeller("building-production-test-foreign-seller");
        object input = AddProduct(context, Input, 5, foreignSeller, 4);
        int transactionsBefore = TransactionCount(context.Ledger);

        Call(context.Province, "ProduceGoodsWeekly");

        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(foreignSeller), Is.Zero);
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(GetInt(input, "Stock"), Is.EqualTo(4));
        Assert.That(GetInt(input, "LastDemand"), Is.Zero);
        Assert.That(Products(context).Contains(Output), Is.False);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
    }

    [Test]
    public void ProduceGoodsWeekly_WithUnrepresentableOutputQuantitySkipsInputPurchase()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 1 },
            new Dictionary<string, int> { [Output] = int.MaxValue });
        object supplier = AddSeller(context, "building-production-test-supplier");
        object input = AddProduct(context, Input, 1, supplier, 2);
        int transactionsBefore = TransactionCount(context.Ledger);

        Call(context.Province, "ProduceGoodsWeekly");

        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(supplier), Is.Zero);
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(GetInt(input, "Stock"), Is.EqualTo(2));
        Assert.That(GetInt(input, "LastDemand"), Is.Zero);
        Assert.That(Products(context).Contains(Output), Is.False);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
    }

    [Test]
    public void ProduceGoodsWeekly_WithZeroInputsRegistersOutputUnderTheBuildingAccount()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int>(),
            new Dictionary<string, int> { [Output] = 2 });

        Call(context.Province, "ProduceGoodsWeekly");

        object output = Products(context)[Output];
        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(GetInt(output, "Stock"), Is.EqualTo(4));
        Assert.That(LotQuantities(output), Is.EqualTo(new Dictionary<string, int>
        {
            [AccountId(context.Building)] = 4,
        }));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
    }

    private static ProductionContext CreateContext(
        Dictionary<string, int> requiredItems,
        Dictionary<string, int> producedItems)
    {
        object nation = TestEconomyFactory.NewNation("BuildingProductionNation", 10L);
        object province = TestEconomyFactory.NewProvince(1, "BuildingProductionProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);

        object market = ReflectionTestHelpers.New("ProvinceMarket", "BuildingProductionProvince");
        ReflectionTestHelpers.Set(province, "market", market);

        object type = ReflectionTestHelpers.New("BuildingType", BuildingName);
        ReflectionTestHelpers.Set(type, "requireItems", requiredItems);
        ReflectionTestHelpers.Set(type, "produceItems", producedItems);
        ReflectionTestHelpers.Set(type, "workerNeeded", 1L);
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", BuildingName);
        ReflectionTestHelpers.Set(recipe, "InitialCapital", 10L);
        Recipes[BuildingName] = recipe;

        object building = ReflectionTestHelpers.Find("BuildingFactory").GetMethod("Create")
            .Invoke(null, new[] { type, province, (object)1, 2L });
        ((IDictionary)ReflectionTestHelpers.Get(province, "buildings"))[type] = building;

        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
            null, new[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });
        object ledger = ReflectionTestHelpers.Get(province, "ActiveLedger");
        ReflectionTestHelpers.Set(ledger, "SalesTaxBasisPoints", 1_000);
        return new ProductionContext(nation, province, market, building, ledger);
    }

    private static object AddSeller(ProductionContext context, string id)
    {
        object seller = ReflectionTestHelpers.New("MoneyAccount", id, 0L);
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger, "RegisterEmptyAccount", seller), Is.True);
        return seller;
    }

    private static object CreateForeignSeller(string id)
    {
        object treasury = ReflectionTestHelpers.New("MoneyAccount", "building-production-test-foreign-treasury", 0L);
        object seller = ReflectionTestHelpers.New("MoneyAccount", id, 0L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "building-production-test-foreign", new object(), treasury);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury), Is.True);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", seller), Is.True);
        Call(ledger, "SealInitialization");
        return seller;
    }

    private static object AddProduct(
        ProductionContext context,
        string name,
        int price,
        object supplier,
        int quantity)
    {
        Call(context.Market, "AddProduct", name, price);
        object product = Products(context)[name];
        Call(product, "AddSupply", supplier, quantity);
        return product;
    }

    private static IDictionary Recipes => (IDictionary)ReflectionTestHelpers.Find("GlobalVariables")
        .GetField("BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static IDictionary Products(ProductionContext context) =>
        (IDictionary)ReflectionTestHelpers.Get(context.Market, "Products");

    private static Dictionary<string, int> LotQuantities(object product)
    {
        object inventory = ReflectionTestHelpers.Get(product, "Inventory");
        IEnumerable lots = (IEnumerable)ReflectionTestHelpers.Get(inventory, "Lots");
        return lots.Cast<object>().ToDictionary(
            lot => AccountId(ReflectionTestHelpers.Get(lot, "Key")),
            lot => GetInt(lot, "Value"));
    }

    private static string AccountId(object owner)
    {
        object account = owner.GetType().Name == "MoneyAccount"
            ? owner
            : ReflectionTestHelpers.Get(owner, "Account");
        return (string)ReflectionTestHelpers.Get(account, "Id");
    }

    private static long Balance(object owner) => GetLong(
        owner.GetType().Name == "MoneyAccount" ? owner : ReflectionTestHelpers.Get(owner, "Account"),
        "Balance");

    private static int TransactionCount(object ledger) =>
        ((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count;

    private static int GetInt(object instance, string member) =>
        (int)ReflectionTestHelpers.Get(instance, member);

    private static long GetLong(object instance, string member) =>
        (long)ReflectionTestHelpers.Get(instance, member);

    private static object Call(object instance, string method, params object[] arguments) =>
        ReflectionTestHelpers.Call<object>(instance, method, arguments);

    private sealed class ProductionContext
    {
        public object Nation { get; }
        public object Province { get; }
        public object Market { get; }
        public object Building { get; }
        public object Ledger { get; }
        public object Treasury => ReflectionTestHelpers.Get(Nation, "Account");

        public ProductionContext(
            object nation,
            object province,
            object market,
            object building,
            object ledger)
        {
            Nation = nation;
            Province = province;
            Market = market;
            Building = building;
            Ledger = ledger;
        }
    }
}
