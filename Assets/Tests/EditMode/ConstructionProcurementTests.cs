using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class ConstructionProcurementTests
{
    [SetUp]
    public void ClearRecipes() => ((IDictionary)ReflectionTestHelpers.Find("GlobalVariables")
        .GetField("BUILDING_RECIPE").GetValue(null)).Clear();

    [TestCase(false)]
    [TestCase(true)]
    public void SharedNationalStock_IsSplitAcrossProvincesIndependentOfInputOrder(bool reverse)
    {
        Context c = new(1000);
        object iron = c.Product("Iron", 10, 10);
        object a = c.Project("a", ("Iron", 10));
        object b = c.Project("b", ("Iron", 10));
        c.Seal();
        Assert.That(c.Process(reverse ? new[] { b, a } : new[] { a, b }), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(5));
        Assert.That(Amount(b, "Iron"), Is.EqualTo(5));
        Assert.That(Get(a, "MaterialSpending"), Is.EqualTo(50));
        Assert.That(Get(b, "MaterialSpending"), Is.EqualTo(50));
        Assert.That(Get(iron, "Stock"), Is.Zero);
        Assert.That(Get(iron, "LastDemand"), Is.EqualTo(10));
        Assert.That(Get(c.Seller, "Balance"), Is.EqualTo(90));
        Assert.That(Get(c.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(10));
        Assert.That(Get(c.Ledger, "MoneySupply"), Is.EqualTo(1000));
        Assert.That(c.Transactions, Is.EqualTo(c.InitialTransactions + 1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OneStockRemainder_GoesToStableProjectId(bool reverse)
    {
        Context c = new(100);
        c.Product("Iron", 10, 1);
        object a = c.Project("a", ("Iron", 10));
        object b = c.Project("b", ("Iron", 10));
        c.Seal();
        Assert.That(c.Process(reverse ? new[] { b, a } : new[] { a, b }), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(1));
        Assert.That(Amount(b, "Iron"), Is.Zero);
    }

    [Test]
    public void SameInvestor_BudgetIsWeightedAcrossProjectsAndItemsBeforeStockAllocation()
    {
        Context c = new(100);
        c.Product("Iron", 10, 100);
        c.Product("Wood", 10, 100);
        object a = c.Project("a", ("Iron", 10), ("Wood", 10));
        object b = c.Project("b", ("Iron", 20));
        c.Seal();
        Assert.That(c.Process(a, b), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(2));
        Assert.That(Amount(a, "Wood"), Is.EqualTo(2));
        Assert.That(Amount(b, "Iron"), Is.EqualTo(5));
        Assert.That(Get(a, "MaterialSpending"), Is.EqualTo(40));
        Assert.That(Get(b, "MaterialSpending"), Is.EqualTo(50));
        Assert.That(Get(c.Buyer, "Balance"), Is.EqualTo(19)); // 10 unused plus 9 sales tax
    }

    [Test]
    public void DifferentInvestors_HaveIndependentGrossBudgets()
    {
        Context c = new(20);
        object other = c.Investor("other", 80);
        c.Product("Iron", 10, 10);
        object a = c.Project("a", ("Iron", 10));
        object b = c.ProjectFor(other, "b", ("Iron", 10));
        c.Seal();
        Assert.That(c.Process(a, b), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(2));
        Assert.That(Amount(b, "Iron"), Is.EqualTo(8));
        Assert.That(Get(Get(other, "Account"), "Balance"), Is.Zero);
    }

    [Test]
    public void PriceIncrease_ReducesAffordableQuantityWithoutChangingRequirement()
    {
        Context c = new(100);
        object iron = c.Product("Iron", 10, 100);
        object a = c.Project("a", ("Iron", 10));
        Set(iron, "Price", 100);
        c.Seal();
        Assert.That(c.Process(a), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(1));
        Assert.That(Get(a, "MaterialSpending"), Is.EqualTo(100));
        Assert.That(((IReadOnlyDictionary<string, long>)Get(a, "RequiredMaterials"))["Iron"], Is.EqualTo(10));
    }

    [TestCase(0, 10, 10)]
    [TestCase(9, 10, 10)]
    [TestCase(100, 10, 0)]
    [TestCase(100, 0, 10)]
    public void UnaffordableOrUnavailableMaterials_WaitSuccessfullyWithoutMutation(long balance, int price, int stock)
    {
        Context c = new(balance);
        object iron = c.Product("Iron", 10, stock);
        Set(iron, "Price", price);
        object a = c.Project("a", ("Iron", 10));
        c.Seal();
        string before = c.Snapshot(a);
        Assert.That(c.Process(a), Is.True);
        Assert.That(c.Snapshot(a), Is.EqualTo(before));
    }

    [Test]
    public void MissingMaterial_IsExcludedFromBudgetButRetainedInContract()
    {
        Context c = new(100);
        c.Product("Iron", 10, 10);
        object a = c.Project("a", ("Iron", 10), ("Missing", 1000));
        c.Seal();
        Assert.That(c.Process(a), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(10));
        Assert.That(Amount(a, "Missing"), Is.Zero);
        Assert.That(((IReadOnlyDictionary<string, long>)Get(a, "RequiredMaterials"))["Missing"], Is.EqualTo(1000));
    }

    [Test]
    public void ScarceItemBudget_IsNotRedistributedAfterStockAllocation()
    {
        Context c = new(100);
        c.Product("Iron", 10, 1);
        c.Product("Wood", 10, 100);
        object a = c.Project("a", ("Iron", 10), ("Wood", 10));
        c.Seal();
        Assert.That(c.Process(a), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(1));
        Assert.That(Amount(a, "Wood"), Is.EqualTo(5));
        Assert.That(Get(a, "MaterialSpending"), Is.EqualTo(60));
    }

    [Test]
    public void FractionalProjectBudgets_RetainUnallocatedCurrency()
    {
        Context c = new(19);
        c.Product("Iron", 10, 10);
        object a = c.Project("a", ("Iron", 10));
        object b = c.Project("b", ("Iron", 10));
        c.Seal();
        string before = c.Snapshot(a, b);
        Assert.That(c.Process(a, b), Is.True);
        Assert.That(c.Snapshot(a, b), Is.EqualTo(before));
    }

    [Test]
    public void ProjectAndProductNamesContainingSeparators_DoNotCollideInBatch()
    {
        Context c = new(100);
        c.Product("b:c", 10, 1);
        c.Product("c", 10, 1);
        object a = c.Project("a", ("b:c", 1));
        object b = c.Project("a:b", ("c", 1));
        c.Seal();
        Assert.That(c.Process(a, b), Is.True);
        Assert.That(Amount(a, "b:c"), Is.EqualTo(1));
        Assert.That(Amount(b, "c"), Is.EqualTo(1));
    }

    [Test]
    public void EmptyBatchAndMaterialFreeProject_AreSuccessfulNoOps()
    {
        Context c = new(100);
        object a = c.Project("a");
        c.Seal();
        string before = c.Snapshot(a);
        Assert.That(c.Process(), Is.True);
        Assert.That(c.Process(a), Is.True);
        Assert.That(c.Snapshot(a), Is.EqualTo(before));
    }

    [TestCase("duplicate")]
    [TestCase("duplicate-id")]
    [TestCase("foreign-investor")]
    [TestCase("foreign-target-ledger")]
    [TestCase("foreign-escrow")]
    [TestCase("wrong-ledger")]
    [TestCase("wrong-market")]
    [TestCase("null-product")]
    [TestCase("wrong-product-name")]
    [TestCase("aliased-product")]
    [TestCase("foreign-supplier")]
    [TestCase("demand-overflow")]
    [TestCase("spending-overflow")]
    [TestCase("valuation-overflow")]
    [TestCase("terminal")]
    [TestCase("null-mandate")]
    public void InvalidBatch_LeavesEveryProjectMoneyAndMarketUnchanged(string invalid)
    {
        Context c = new(1000);
        object iron = c.Product("Iron", 10, 100);
        object a = c.Project("a", ("Iron", 10));
        object b = c.Project("b", ("Iron", 10));
        object[] input = { a, b };
        if (invalid == "duplicate") input = new[] { a, a };
        if (invalid == "duplicate-id") b.GetType().GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, Get(a, "Id"));
        if (invalid == "null-mandate") input[1] = null;
        if (invalid == "foreign-investor") b.GetType().GetField("<Investor>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, TestEconomyFactory.NewNation("foreign", 100L));
        if (invalid == "foreign-target-ledger") Set(Get(b, "TargetProvince"), "ActiveLedger", ReflectionTestHelpers.New("MoneyLedger", "foreign", new object(), ReflectionTestHelpers.New("MoneyAccount", "foreign", 0L)));
        if (invalid == "foreign-escrow") ReflectionTestHelpers.Call<bool>(c.Ledger, "UnregisterEmptyAccount", Get(b, "EscrowAccount"));
        if (invalid == "wrong-ledger") c.ArgumentLedger = ReflectionTestHelpers.New("MoneyLedger", "foreign", new object(), ReflectionTestHelpers.New("MoneyAccount", "foreign", 0L));
        if (invalid == "wrong-market") Set(Get(b, "TargetProvince"), "isConnectedToCapital", false);
        if (invalid == "null-product") c.Products["Iron"] = null;
        if (invalid == "wrong-product-name") Set(iron, "ProductName", "Wood");
        if (invalid == "aliased-product") c.Products["Wood"] = iron;
        if (invalid == "foreign-supplier") c.Product("Wood", 10, 10, ReflectionTestHelpers.New("MoneyAccount", "unregistered", 0L));
        if (invalid == "foreign-supplier") b = input[1] = c.Project("c", ("Wood", 10));
        if (invalid == "demand-overflow") Set(iron, "LastDemand", int.MaxValue);
        if (invalid == "spending-overflow") Acquire(b, long.MaxValue, ("Iron", 0L));
        if (invalid == "valuation-overflow")
        {
            Set(iron, "Price", int.MaxValue);
            c.Product("Wood", int.MaxValue, 10);
            c.Product("Stone", int.MaxValue, 10);
            b = input[1] = c.Project("c", ("Iron", int.MaxValue), ("Wood", int.MaxValue), ("Stone", int.MaxValue));
        }
        c.Seal();
        if (invalid == "terminal") Assert.That(ReflectionTestHelpers.Call<bool>(b, "Cancel"), Is.True);
        string before = c.Snapshot(a, b);
        Assert.That(c.Process(input), Is.False);
        Assert.That(c.Snapshot(a, b), Is.EqualTo(before));
    }

    [Test]
    public void PriorAcquisition_IsCumulativeAndNeverPurchasedAgain()
    {
        Context c = new(1000);
        c.Product("Iron", 10, 100);
        object a = c.Project("a", ("Iron", 10));
        Acquire(a, 40, ("Iron", 4L));
        c.Seal();
        Assert.That(c.Process(a), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(10));
        Assert.That(Get(a, "MaterialSpending"), Is.EqualTo(100));
        string before = c.Snapshot(a);
        Assert.That(c.Process(a), Is.True);
        Assert.That(c.Snapshot(a), Is.EqualTo(before));
    }

    [Test]
    public void IsolatedProvince_UsesItsLocalMarket()
    {
        Context c = new(100);
        object a = c.Project("a", ("Iron", 10));
        object province = Get(a, "TargetProvince");
        Set(province, "isConnectedToCapital", false);
        object local = ReflectionTestHelpers.New("ProvinceMarket", "a");
        Set(province, "market", local);
        c.Products = (IDictionary)Get(local, "Products");
        c.Product("Iron", 10, 10);
        c.Seal();
        Assert.That(c.Process(a), Is.True);
        Assert.That(Amount(a, "Iron"), Is.EqualTo(10));
    }

    private static object Get(object o, string name) => ReflectionTestHelpers.Get(o, name);
    private static void Set(object o, string name, object value) => ReflectionTestHelpers.Set(o, name, value);
    private static long Amount(object project, string item) => ((IReadOnlyDictionary<string, long>)Get(project, "AcquiredMaterials"))[item];
    private static void Acquire(object project, long spending, params (string Name, long Amount)[] items)
    {
        object[] args = { items.ToDictionary(i => i.Name, i => i.Amount), spending, null };
        Assert.That(project.GetType().GetMethod("TryPrepareMaterialAcquisition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(project, args), Is.True);
        project.GetType().GetMethod("CommitMaterialAcquisition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(project, new[] { args[2] });
    }

    private sealed class Context
    {
        public readonly object Nation;
        public readonly object Buyer;
        public readonly object Seller;
        public readonly object Ledger;
        public object ArgumentLedger;
        public IDictionary Products;
        public int InitialTransactions;
        public int Transactions => ((ICollection)Get(Ledger, "Transactions")).Count;
        private readonly List<object> accounts = new();

        public Context(long balance)
        {
            Nation = TestEconomyFactory.NewNation("procurement", balance);
            Buyer = Get(Nation, "Account");
            Ledger = ReflectionTestHelpers.New("MoneyLedger", "test", Nation, Buyer);
            ArgumentLedger = Ledger;
            Register(Buyer);
            Seller = ReflectionTestHelpers.New("MoneyAccount", "seller", 0L);
            Register(Seller);
            Products = (IDictionary)Get(Get(Nation, "market"), "Products");
        }

        private void Register(object account)
        {
            Assert.That(ReflectionTestHelpers.Call<bool>(Ledger, "RegisterInitialAccount", account), Is.True);
            accounts.Add(account);
        }

        public object Investor(string id, long balance)
        {
            object investor = TestEconomyFactory.NewNation(id, balance);
            Register(Get(investor, "Account"));
            return investor;
        }

        public object Product(string name, int price, int stock, object supplier = null)
        {
            object product = ReflectionTestHelpers.New("ProductState", name, price);
            if (stock > 0) ReflectionTestHelpers.Call<object>(product, "AddSupply", supplier ?? Seller, stock);
            Products[name] = product;
            return product;
        }

        public object Project(string id, params (string Name, int Amount)[] items) => ProjectFor(Nation, id, items);

        public object ProjectFor(object investor, string id, params (string Name, int Amount)[] items)
        {
            object recipe = ReflectionTestHelpers.New("BuildingRecipe", id);
            Set(recipe, "TimeToBuild", 10);
            Set(recipe, "InitialCapital", 0L);
            foreach (var item in items) ((IDictionary)Get(recipe, "requireItems"))[item.Name] = item.Amount;
            ((IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField("BUILDING_RECIPE").GetValue(null))[id] = recipe;
            object province = TestEconomyFactory.NewProvince(1, id);
            Set(province, "nation", Nation);
            Set(province, "isConnectedToCapital", true);
            Set(province, "ActiveLedger", Ledger);
            object escrow = ReflectionTestHelpers.New("MoneyAccount", "project:" + id, 0L);
            Register(escrow);
            return ReflectionTestHelpers.New("ConstructionMandate", investor,
                ReflectionTestHelpers.New("BuildingType", id), province, 10d, escrow, 0L);
        }

        public void Seal()
        {
            ReflectionTestHelpers.Call<object>(Ledger, "SealInitialization");
            InitialTransactions = Transactions;
        }

        public bool Process(params object[] projects)
        {
            bool result = (bool)ReflectionTestHelpers.Find("ConstructionProcurement").GetMethod("TryProcessMarket")
                .Invoke(null, new[] { TestEconomyFactory.ListOf("ConstructionMandate", projects), Products, ArgumentLedger });
            object[] auditArgs = { 0L };
            Assert.That(Ledger.GetType().GetMethod("Audit").Invoke(Ledger, auditArgs), Is.True);
            return result;
        }

        public string Snapshot(params object[] projects) => string.Join("|",
            accounts.Select(a => Get(a, "Balance").ToString())
                .Concat(new[] { Transactions.ToString(), Get(Ledger, "WeeklyTaxRevenue").ToString(), Get(Ledger, "MoneySupply").ToString() })
                .Concat(Products.Values.Cast<object>().Where(p => p != null).Select(p => $"{Get(p, "Stock")}:{Get(p, "LastDemand")}"))
                .Concat(projects.Select(p => $"{Get(p, "Status")}:{Get(p, "MaterialSpending")}:{string.Join(",", ((IReadOnlyDictionary<string, long>)Get(p, "AcquiredMaterials")).Values)}")));
    }
}
