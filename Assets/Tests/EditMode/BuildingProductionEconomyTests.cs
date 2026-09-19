using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class BuildingProductionEconomyTests
{
    private const string Input = "building-production-test-input";
    private const string SecondInput = "building-production-test-second-input";
    private const string MissingInput = "building-production-test-missing-input";
    private const string Output = "building-production-test-output";
    private const string BuildingName = "BuildingProductionEconomyTestBuilding";

    [TearDown]
    public void RemoveTestRecipe()
    {
        Recipes.Remove(BuildingName);
    }

    [Test]
    public void ProduceGoodsWeekly_WithMarketStockButEmptyInputInventoryProducesNothingAndChangesNoSettlementState()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 2 },
            new Dictionary<string, int> { [Output] = 3 });
        object supplier = AddSeller(context, "building-production-test-supplier");
        object input = AddProduct(context, Input, 5, supplier, 4);
        int transactionsBefore = TransactionCount(context.Ledger);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");

        Call(context.Province, "ProduceGoodsWeekly");

        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(supplier), Is.Zero);
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(GetInt(input, "Stock"), Is.EqualTo(4));
        Assert.That(GetInt(input, "LastDemand"), Is.Zero);
        Assert.That(Products(context).Contains(Output), Is.False);
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger, "Audit", (object)null), Is.True);
    }

    [Test]
    public void ProduceGoodsWeekly_WithMissingComplementaryStoredInputLeavesAllInputsAndOutputUnchanged()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 1, [MissingInput] = 1 },
            new Dictionary<string, int> { [Output] = 3 });
        RestoreInputs(context.Building, new Dictionary<string, long> { [Input] = 2L });
        int transactionsBefore = TransactionCount(context.Ledger);

        Call(context.Province, "ProduceGoodsWeekly");

        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(InputQuantities(context.Building), Is.EqualTo(new Dictionary<string, long>
        {
            [Input] = 2L,
        }));
        Assert.That(Products(context).Contains(Output), Is.False);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
    }

    [Test]
    public void ProduceGoodsWeekly_ConsumesCompleteStoredRecipeAndLeavesRemainders()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 2, [SecondInput] = 3 },
            new Dictionary<string, int> { [Output] = 4 },
            currentWorkers: 1L);
        RestoreInputs(context.Building, new Dictionary<string, long>
        {
            [Input] = 3L,
            [SecondInput] = 5L,
        });
        int transactionsBefore = TransactionCount(context.Ledger);

        Call(context.Province, "ProduceGoodsWeekly");

        object output = Products(context)[Output];
        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(InputQuantities(context.Building), Is.EqualTo(new Dictionary<string, long>
        {
            [Input] = 1L,
            [SecondInput] = 2L,
        }));
        Assert.That(GetInt(output, "Stock"), Is.EqualTo(4));
        Assert.That(LotQuantities(output), Is.EqualTo(new Dictionary<string, int>
        {
            [AccountId(context.Building)] = 4,
        }));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
    }

    [Test]
    public void ProduceGoodsWeekly_WithUnrepresentableOutputQuantityPreservesStoredInputs()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int> { [Input] = 1 },
            new Dictionary<string, int> { [Output] = int.MaxValue });
        RestoreInputs(context.Building, new Dictionary<string, long> { [Input] = 2L });
        int transactionsBefore = TransactionCount(context.Ledger);

        Call(context.Province, "ProduceGoodsWeekly");

        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(Balance(context.Treasury), Is.Zero);
        Assert.That(InputQuantities(context.Building), Is.EqualTo(new Dictionary<string, long>
        {
            [Input] = 2L,
        }));
        Assert.That(Products(context).Contains(Output), Is.False);
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(TransactionCount(context.Ledger), Is.EqualTo(transactionsBefore));
    }

    [Test]
    public void ProduceGoodsWeekly_WithZeroInputsRetainsContinuousProduction()
    {
        ProductionContext context = CreateContext(
            new Dictionary<string, int>(),
            new Dictionary<string, int> { [Output] = 3 },
            currentWorkers: 2L,
            workerNeeded: 4L);

        Call(context.Province, "ProduceGoodsWeekly");

        object output = Products(context)[Output];
        Assert.That(Balance(context.Building), Is.EqualTo(10L));
        Assert.That(GetInt(output, "Stock"), Is.EqualTo(1));
        Assert.That(LotQuantities(output), Is.EqualTo(new Dictionary<string, int>
        {
            [AccountId(context.Building)] = 1,
        }));
        Assert.That(GetLong(context.Ledger, "WeeklyTaxRevenue"), Is.Zero);
    }

    private static ProductionContext CreateContext(
        Dictionary<string, int> requiredItems,
        Dictionary<string, int> producedItems,
        long initialCapital = 10L,
        long currentWorkers = 2L,
        long workerNeeded = 1L)
    {
        object nation = TestEconomyFactory.NewNation(
            "BuildingProductionNation", initialCapital);
        object province = TestEconomyFactory.NewProvince(1, "BuildingProductionProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);

        object market = ReflectionTestHelpers.New("ProvinceMarket", "BuildingProductionProvince");
        ReflectionTestHelpers.Set(province, "market", market);

        object type = ReflectionTestHelpers.New("BuildingType", BuildingName);
        ReflectionTestHelpers.Set(type, "requireItems", requiredItems);
        ReflectionTestHelpers.Set(type, "produceItems", producedItems);
        ReflectionTestHelpers.Set(type, "workerNeeded", workerNeeded);
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", BuildingName);
        ReflectionTestHelpers.Set(recipe, "InitialCapital", initialCapital);
        Recipes[BuildingName] = recipe;

        object building = ReflectionTestHelpers.Find("BuildingFactory").GetMethod("Create")
            .Invoke(null, new[] { type, province, (object)1, currentWorkers });
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

    private static void RestoreInputs(object building, IReadOnlyDictionary<string, long> quantities)
    {
        MethodInfo method = building.GetType().GetMethod("RestoreInputInventory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing Building.RestoreInputInventory");
        method.Invoke(building, new object[] { quantities });
    }

    private static Dictionary<string, long> InputQuantities(object building) =>
        ((IEnumerable)ReflectionTestHelpers.Get(building, "InputInventory"))
            .Cast<object>()
            .ToDictionary(
                entry => (string)ReflectionTestHelpers.Get(entry, "Key"),
                entry => (long)ReflectionTestHelpers.Get(entry, "Value"));

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
