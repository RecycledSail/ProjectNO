using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class MarketInventoryTests
{
    [Test]
    public void ProductState_PlanAndCommitSale_PreservesSupplierLotsAndReconcilesStock()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object alpha = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);
        object beta = ReflectionTestHelpers.New("MoneyAccount", "supplier:beta", 0L);

        Call(product, "AddSupply", alpha, 60);
        AssertReconciled(product, 60);
        Call(product, "AddSupply", beta, 40);
        AssertReconciled(product, 100);

        IReadOnlyList<object> sale = Items(Call(product, "PlanSale", 50));
        Assert.That(SaleQuantities(sale), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 30,
            ["supplier:beta"] = 20,
        }));
        AssertReconciled(product, 100);

        Call(product, "CommitSale", Call(product, "PlanSale", 50));
        Assert.That(LotQuantities(product), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 30,
            ["supplier:beta"] = 20,
        }));
        AssertReconciled(product, 50);
    }

    [Test]
    public void ProductState_PlanSale_AssignsEqualFractionalRemainderBySupplierAccountId()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object beta = ReflectionTestHelpers.New("MoneyAccount", "supplier:beta", 0L);
        object alpha = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);

        Call(product, "AddSupply", beta, 1);
        Call(product, "AddSupply", alpha, 1);

        IReadOnlyList<object> sale = Items(Call(product, "PlanSale", 1));

        Assert.That(SaleQuantities(sale), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 1,
        }));
        AssertReconciled(product, 2);
    }

    [Test]
    public void ProductState_CommitSale_RejectsStalePlanWithoutMutatingAnyLot()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object supplier = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);
        Call(product, "AddSupply", supplier, 10);
        object planned = Call(product, "PlanSale", 7);

        Call(product, "CommitSale", planned);
        Assert.That(() => Call(product, "CommitSale", planned), Throws.TypeOf<TargetInvocationException>());

        Assert.That(LotQuantities(product), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 3,
        }));
        AssertReconciled(product, 3);
    }

    [Test]
    public void ProductState_CommitSale_RejectsForgedNonProportionalAllocationWithoutMutation()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object alpha = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);
        object beta = ReflectionTestHelpers.New("MoneyAccount", "supplier:beta", 0L);
        Call(product, "AddSupply", alpha, 60);
        Call(product, "AddSupply", beta, 40);
        object forgedSale = TestEconomyFactory.ListOf("SupplierSale",
            ReflectionTestHelpers.New("SupplierSale", alpha, 50));

        Assert.That(() => Call(product, "CommitSale", forgedSale),
            Throws.TypeOf<TargetInvocationException>());

        Assert.That(LotQuantities(product), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 60,
            ["supplier:beta"] = 40,
        }));
        AssertReconciled(product, 100);
    }

    [Test]
    public void ProductState_CommitSale_RejectsPlanStaleAfterInventoryChangesWithoutMutation()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object alpha = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);
        object beta = ReflectionTestHelpers.New("MoneyAccount", "supplier:beta", 0L);
        Call(product, "AddSupply", alpha, 60);
        Call(product, "AddSupply", beta, 40);
        object planned = Call(product, "PlanSale", 50);
        Call(product, "AddSupply", alpha, 40);

        Assert.That(() => Call(product, "CommitSale", planned),
            Throws.TypeOf<TargetInvocationException>());

        Assert.That(LotQuantities(product), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 100,
            ["supplier:beta"] = 40,
        }));
        AssertReconciled(product, 140);
    }

    [Test]
    public void ProductState_AddSupply_RejectsInvalidLotsWithoutMutation()
    {
        object product = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object supplier = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);

        Assert.That(() => Call(product, "AddSupply", supplier, 0),
            Throws.TypeOf<TargetInvocationException>());
        Assert.That(() => Call(product, "AddSupply", (object)null, 1),
            Throws.TypeOf<TargetInvocationException>());

        Assert.That(LotQuantities(product), Is.Empty);
        Assert.That(ReflectionTestHelpers.Get(product, "LastSupply"), Is.EqualTo(0));
        AssertReconciled(product, 0);
    }

    [Test]
    public void ProductState_TransferAllStockTo_MovesEverySupplierLotAndClearsSource()
    {
        object source = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object destination = ReflectionTestHelpers.New("ProductState", "Wheat", 10);
        object alpha = ReflectionTestHelpers.New("MoneyAccount", "supplier:alpha", 0L);
        object beta = ReflectionTestHelpers.New("MoneyAccount", "supplier:beta", 0L);

        Call(source, "AddSupply", alpha, 4);
        Call(source, "AddSupply", beta, 6);
        Call(source, "TransferAllStockTo", destination);

        Assert.That(LotQuantities(destination), Is.EqualTo(new Dictionary<string, int>
        {
            ["supplier:alpha"] = 4,
            ["supplier:beta"] = 6,
        }));
        Assert.That(ReflectionTestHelpers.Get(destination, "LastSupply"), Is.EqualTo(10));
        Assert.That(LotQuantities(source), Is.Empty);
        AssertReconciled(source, 0);
        AssertReconciled(destination, 10);
    }

    [Test]
    public void Province_ProduceGoodsWeekly_AttributesBasicFoodToPopulationsWithoutChangingMoney()
    {
        const string food = "market-inventory-test-food";
        IDictionary categories = (IDictionary)ReflectionTestHelpers.Find("GlobalVariables")
            .GetField("CATEGORIES", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        object previousCategory = categories.Contains("basic_food") ? categories["basic_food"] : null;
        categories["basic_food"] = new List<string> { food };

        try
        {
            object plane = Enum.Parse(ReflectionTestHelpers.Find("Topography"), "Plane");
            object province = ReflectionTestHelpers.New("Province", 1,
                "MarketInventoryTestProvince", plane);
            object alpha = AddPopulation(province, "Alpha", 40, 101L);
            object beta = AddPopulation(province, "Beta", 60, 202L);
            Call(province, "InitializePopulation");
            object market = ReflectionTestHelpers.New("ProvinceMarket", "MarketInventoryTestProvince");
            ReflectionTestHelpers.Set(province, "market", market);
            Call(market, "AddProduct", food, 1);
            object product = ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))[food];

            Call(province, "ProduceGoodsWeekly");

            Assert.That(AccountBalance(alpha), Is.EqualTo(101L));
            Assert.That(AccountBalance(beta), Is.EqualTo(202L));
            Assert.That(LotQuantities(product), Is.EqualTo(new Dictionary<string, int>
            {
                [AccountId(alpha)] = 40,
                [AccountId(beta)] = 60,
            }));
            AssertReconciled(product, 100);
        }
        finally
        {
            if (previousCategory == null)
                categories.Remove("basic_food");
            else
                categories["basic_food"] = previousCategory;
        }
    }

    private static object AddPopulation(object province, string cultureName, int count, long balance)
    {
        object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
        ReflectionTestHelpers.Set(species, "name", "Human");
        object culture = ReflectionTestHelpers.New("Culture", cultureName);
        object group = ReflectionTestHelpers.New("EthnicGroup", species, culture);
        object population = ReflectionTestHelpers.New("ProvinceEthnicPop", province, group,
            new List<int> { 0, count, 0, 0 }, balance, 1.0);
        ((IList)ReflectionTestHelpers.Get(province, "provinceEthnicPops")).Add(population);
        return population;
    }

    private static object Call(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(candidate => candidate.Name == name &&
                candidate.GetParameters().Length == arguments.Length);
        Assert.That(method, Is.Not.Null, $"Missing public method {name} on {instance.GetType().Name}");
        return method.Invoke(instance, arguments);
    }

    private static IReadOnlyList<object> Items(object value) => ((IEnumerable)value).Cast<object>().ToList();

    private static Dictionary<string, int> SaleQuantities(IEnumerable<object> sale) => sale.ToDictionary(
        entry => AccountId(ReflectionTestHelpers.Get(entry, "Supplier")),
        entry => (int)ReflectionTestHelpers.Get(entry, "Quantity"));

    private static Dictionary<string, int> LotQuantities(object product)
    {
        object inventory = ReflectionTestHelpers.Get(product, "Inventory");
        IEnumerable lots = (IEnumerable)ReflectionTestHelpers.Get(inventory, "Lots");
        return lots.Cast<object>().ToDictionary(
            pair => AccountId(ReflectionTestHelpers.Get(pair, "Key")),
            pair => (int)ReflectionTestHelpers.Get(pair, "Value"));
    }

    private static string AccountId(object populationOrAccount)
    {
        object account = populationOrAccount.GetType().Name == "MoneyAccount"
            ? populationOrAccount
            : ReflectionTestHelpers.Get(populationOrAccount, "Account");
        return (string)ReflectionTestHelpers.Get(account, "Id");
    }

    private static long AccountBalance(object population) => (long)ReflectionTestHelpers.Get(
        ReflectionTestHelpers.Get(population, "Account"), "Balance");

    private static void AssertReconciled(object product, int expectedStock)
    {
        Assert.That(ReflectionTestHelpers.Get(product, "Stock"), Is.EqualTo(expectedStock));
        object inventory = ReflectionTestHelpers.Get(product, "Inventory");
        Assert.That(ReflectionTestHelpers.Get(inventory, "TotalQuantity"), Is.EqualTo(expectedStock));
    }
}
