using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ConstructionContractTests
{
    private const string TestRecipeName = "ConstructionContractTestRecipe";

    [TearDown]
    public void RemoveLoadedTestRecipe()
    {
        Recipes().Remove(TestRecipeName);
    }

    [Test]
    public void NewRecipe_DefaultsToThirtyPercentStartThreshold()
    {
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", "Test");

        Assert.That(
            ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"),
            Is.EqualTo(3000));
        Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(0L));
    }

    [Test]
    public void LoadBuildingRecipes_MapsExplicitConstructionContractSettings()
    {
        LoadRecipe(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10," +
            "\"constructionFee\":25,\"startMaterialBasisPoints\":4500}]}");

        object recipe = Recipes()[TestRecipeName];
        Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(25L));
        Assert.That(
            ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"),
            Is.EqualTo(4500));
    }

    [Test]
    public void LoadBuildingRecipes_MissingConstructionSettingsUseCompatibleDefaults()
    {
        LoadRecipe(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10}]}");

        object recipe = Recipes()[TestRecipeName];
        Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(0L));
        Assert.That(
            ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"),
            Is.EqualTo(3000));
    }

    [Test]
    public void LoadBuildingRecipes_NegativeConstructionFeeIsRejectedBeforeRegistration()
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10,\"constructionFee\":-1}]}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("constructionFee"));
        Assert.That(Recipes().Contains(TestRecipeName), Is.False);
    }

    [TestCase(0)]
    [TestCase(10001)]
    public void LoadBuildingRecipes_StartMaterialThresholdOutsideContractRangeIsRejected(
        int basisPoints)
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":2}]," +
            $"\"TimeToBuild\":4,\"initialCapital\":10," +
            $"\"startMaterialBasisPoints\":{basisPoints}}}]}}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("startMaterialBasisPoints"));
        Assert.That(Recipes().Contains(TestRecipeName), Is.False);
    }

    [Test]
    public void LoadBuildingRecipes_NonpositiveRequiredMaterialIsRejected()
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[{\"item\":\"Wood\",\"amount\":0}]," +
            "\"TimeToBuild\":4,\"initialCapital\":10}]}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("Wood"));
        Assert.That(exception.Message, Does.Contain("amount"));
    }

    [Test]
    public void LoadBuildingRecipes_NonpositiveManhoursIsRejected()
    {
        InvalidOperationException exception = AssertRecipeRejected(
            $"{{\"buildingrecipes\":[{{\"name\":\"{TestRecipeName}\"," +
            "\"buildRequirements\":[],\"TimeToBuild\":0,\"initialCapital\":10}]}");

        Assert.That(exception.Message, Does.Contain(TestRecipeName));
        Assert.That(exception.Message, Does.Contain("TimeToBuild"));
    }

    [Test]
    public void ResourceRecipes_UsePositiveAuthoredFeesMatchingInitialTuning()
    {
        RecipeWrapper data = JsonUtility.FromJson<RecipeWrapper>(File.ReadAllText(
            Path.Combine(Application.dataPath, "Resources", "BuildingRecipes.json")));

        Assert.That(data.buildingrecipes, Is.Not.Empty);
        Assert.That(data.buildingrecipes, Has.All.Matches<RecipeData>(recipe =>
            recipe.constructionFee > 0 && recipe.constructionFee == recipe.TimeToBuild));
    }

    [Test]
    public void LegacyMandates_CaptureDistinctStableIdentifiers()
    {
        object buildingType = ReflectionTestHelpers.New("BuildingType", "IdentifierTest");
        object province = TestEconomyFactory.NewProvince(99, "IdentifierProvince");
        object first = ReflectionTestHelpers.New(
            "ConstructionMandate", null, buildingType, province, 1d);
        object second = ReflectionTestHelpers.New(
            "ConstructionMandate", null, buildingType, province, 1d);

        string firstId = (string)ReflectionTestHelpers.Get(first, "Id");
        Assert.That(firstId, Is.Not.Empty);
        Assert.That(ReflectionTestHelpers.Get(first, "Id"), Is.EqualTo(firstId));
        Assert.That(ReflectionTestHelpers.Get(second, "Id"), Is.Not.EqualTo(firstId));
    }

    [Test]
    public void Cancellation_HalfProgressReturnsOnlyUnusedInvestorStockAndRemainingEscrowOnce()
    {
        CancellationTestContext c = new();
        c.BuyAndProgress();
        Assert.That(c.Cancel(), Is.True);
        Assert.That(c.Balance(c.Buyer), Is.EqualTo(910L));
        // Procurement pays 36 after 10% sales tax, followed by the earned fee of 50.
        Assert.That(c.Balance(c.CompanyAccount), Is.EqualTo(86L));
        Assert.That(c.Balance(c.Escrow), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(c.Escrow, "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(c.Mandate, "PaidConstructionFee"), Is.EqualTo(50L));
        Assert.That(ReflectionTestHelpers.Get(c.Mandate, "MaterialSpending"), Is.EqualTo(40L));
        foreach (string item in new[] { "Iron", "Wood" })
        {
            Assert.That(c.Lot(c.Products[item], c.Buyer), Is.EqualTo(5));
            Assert.That(c.Amount("AcquiredMaterials", item), Is.EqualTo(10));
            Assert.That(c.Amount("ConsumedMaterials", item), Is.EqualTo(5));
            Assert.That(ReflectionTestHelpers.Get(c.Products[item], "LastSupply"), Is.EqualTo(15));
        }
        string after = c.Snapshot();
        Assert.That(c.Cancel(), Is.False);
        Assert.That(ReflectionTestHelpers.Call<double>(c.Mandate, "ApplyManhours", 5d), Is.Zero);
        Assert.That(c.Snapshot(), Is.EqualTo(after));
        c.Audit();
    }

    [TestCase("missing-market")]
    [TestCase("missing-second-product")]
    [TestCase("wrong-product-name")]
    [TestCase("aliased-product")]
    [TestCase("stock-overflow")]
    [TestCase("supply-overflow")]
    [TestCase("duplicate-supplier-id")]
    [TestCase("foreign-supplier")]
    [TestCase("foreign-investor")]
    [TestCase("foreign-ledger")]
    [TestCase("self-refund")]
    public void Cancellation_InvalidReturnPreservesEveryMaterialAndAccount(string invalid)
    {
        CancellationTestContext c = new();
        c.BuyAndProgress();
        object wood = c.Products["Wood"];
        if (invalid == "missing-market") ReflectionTestHelpers.Set(c.Province, "market", null);
        if (invalid == "missing-second-product") c.Products.Remove("Wood");
        if (invalid == "wrong-product-name") ReflectionTestHelpers.Set(wood, "ProductName", "Other");
        if (invalid == "aliased-product") c.Products["Alias"] = wood;
        if (invalid == "stock-overflow")
        {
            ReflectionTestHelpers.Set(wood, "LastSupply", 0);
            ReflectionTestHelpers.Call<object>(wood, "AddSupply", c.Buyer, int.MaxValue);
            ReflectionTestHelpers.Set(wood, "LastSupply", 0);
        }
        if (invalid == "supply-overflow") ReflectionTestHelpers.Set(wood, "LastSupply", int.MaxValue);
        if (invalid == "duplicate-supplier-id" || invalid == "foreign-supplier")
        {
            object supplier = ReflectionTestHelpers.New("MoneyAccount", invalid == "duplicate-supplier-id"
                ? ReflectionTestHelpers.Get(c.Buyer, "Id") : "foreign:supplier", 0L);
            ReflectionTestHelpers.Call<object>(wood, "AddSupply", supplier, 1);
        }
        if (invalid == "foreign-investor") c.ReplaceInvestor(c.Nation);
        if (invalid == "foreign-ledger") ReflectionTestHelpers.Set(c.Province, "ActiveLedger", ReflectionTestHelpers.Get(c.Nation, "Ledger"));
        if (invalid == "self-refund") c.Mandate.GetType().GetField("<EscrowAccount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(c.Mandate, c.Buyer);
        string before = c.Snapshot();
        Assert.That(c.Cancel(), Is.False);
        Assert.That(c.Snapshot(), Is.EqualTo(before));
        c.Audit();
    }

    [Test]
    public void Cancellation_UsesCurrentConnectedMarketAfterConnectionChanges()
    {
        CancellationTestContext c = new();
        c.BuyAndProgress();
        Assert.That(ReflectionTestHelpers.Find("ProvinceCurrencyMigration").GetMethod("TryAbsorbNeutralProvince")
            .Invoke(null, new object[] { c.Province, c.Nation, null }), Is.True);
        ReflectionTestHelpers.Set(c.Province, "isConnectedToCapital", true);
        IDictionary nationalProducts = (IDictionary)ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(c.Nation, "market"), "Products");
        foreach (string item in new[] { "Iron", "Wood" })
            nationalProducts[item] = ReflectionTestHelpers.New("ProductState", item, 2);
        Assert.That(c.Cancel(), Is.True);
        Assert.That(c.Lot(nationalProducts["Iron"], c.Buyer), Is.EqualTo(5));
        Assert.That(ReflectionTestHelpers.Get(c.Products["Iron"], "Stock"), Is.Zero);
        c.Audit();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Cancellation_ZeroEscrowStillValidatesInvestorRegistration(bool foreignInvestor)
    {
        CancellationTestContext c = new(false, 0L, 0L);
        if (foreignInvestor) c.ReplaceInvestor(c.Nation);
        string before = c.Snapshot();
        Assert.That(c.Cancel(), Is.EqualTo(!foreignInvestor));
        if (foreignInvestor) Assert.That(c.Snapshot(), Is.EqualTo(before));
        c.Audit();
    }

    private static void LoadRecipe(string json)
    {
        Type wrapperType = ReflectionTestHelpers.Find(
            "GlobalVariables+GameDataFormat+BuildingrecipesWrapper");
        object data = JsonUtility.FromJson(json, wrapperType);
        MethodInfo loader = ReflectionTestHelpers.Find("GlobalVariables").GetMethod(
            "LoadBuildingRecipes",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { wrapperType },
            null);
        Assert.That(loader, Is.Not.Null);
        loader.Invoke(null, new[] { data });
    }

    private static InvalidOperationException AssertRecipeRejected(string json)
    {
        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => LoadRecipe(json));
        Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
        return (InvalidOperationException)invocation.InnerException;
    }

    private static IDictionary Recipes() =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    [Serializable]
    private sealed class RecipeWrapper
    {
        public RecipeData[] buildingrecipes;
    }

    [Serializable]
    private sealed class RecipeData
    {
        public int TimeToBuild;
        public long constructionFee;
    }
}

// Real neutral economy shared by cancellation and migration regression tests.
internal sealed class CancellationTestContext
{
    internal readonly object Nation, Province, Investor, Buyer, Company, CompanyAccount, Ledger, Escrow, Mandate;
    internal readonly IDictionary Products;
    internal CancellationTestContext(bool materials = true, long capital = 300L, long fee = 100L)
    {
        Nation = TestEconomyFactory.NewNation("CancellationDestination", 1000L);
        Province = TestEconomyFactory.NewProvince(501, "CancellationProvince");
        ReflectionTestHelpers.Set(Province, "initialLocalTreasury", 500L);
        Investor = TestEconomyFactory.AddPop(Province, 1000L, 1.0);
        Buyer = ReflectionTestHelpers.Get(Investor, "Account");
        TestEconomyFactory.AddBuilding(Province, "construcntionCompany", 1, 0L);
        Company = TestEconomyFactory.GetOnlyBuilding(Province);
        CompanyAccount = ReflectionTestHelpers.Get(Company, "Account");
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
            new[] { TestEconomyFactory.ListOf("Nation", Nation), TestEconomyFactory.ListOf("Province", Province) });
        Ledger = ReflectionTestHelpers.Get(Province, "LocalLedger");
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", "CancellationTest");
        ReflectionTestHelpers.Set(recipe, "TimeToBuild", 10);
        ReflectionTestHelpers.Set(recipe, "InitialCapital", capital);
        ReflectionTestHelpers.Set(recipe, "ConstructionFee", fee);
        object market = ReflectionTestHelpers.New("ProvinceMarket", "CancellationProvince");
        Products = (IDictionary)ReflectionTestHelpers.Get(market, "Products");
        if (materials)
        {
            ReflectionTestHelpers.Set(Province, "market", market);
            foreach (string item in new[] { "Iron", "Wood" })
            {
                ((IDictionary)ReflectionTestHelpers.Get(recipe, "requireItems"))[item] = 10;
                object product = ReflectionTestHelpers.New("ProductState", item, 2);
                ReflectionTestHelpers.Call<object>(product, "AddSupply", CompanyAccount, 10);
                Products[item] = product;
            }
        }
        ((IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField("BUILDING_RECIPE").GetValue(null))["CancellationTest"] = recipe;
        Escrow = ReflectionTestHelpers.New("MoneyAccount", "cancellation:escrow", 0L);
        Assert.That(ReflectionTestHelpers.Call<bool>(Ledger, "RegisterEmptyAccount", Escrow), Is.True);
        if (capital + fee > 0)
            Assert.That(ReflectionTestHelpers.Call<bool>(Ledger, "TryTransfer", Buyer, Escrow, capital + fee, "Fund contract"), Is.True);
        Mandate = ReflectionTestHelpers.New("ConstructionMandate", Investor,
            ReflectionTestHelpers.New("BuildingType", "CancellationTest"), Province, 10d, Escrow, capital);
        Assert.That(ReflectionTestHelpers.Call<bool>(Mandate, "TryAssign", Company), Is.True);
    }

    internal void BuyAndProgress()
    {
        Assert.That(ReflectionTestHelpers.Find("ConstructionProcurement").GetMethod("TryProcessMarket").Invoke(null,
            new[] { TestEconomyFactory.ListOf("ConstructionMandate", Mandate), Products, Ledger }), Is.True);
        Assert.That(ReflectionTestHelpers.Call<double>(Mandate, "ApplyManhours", 5d), Is.EqualTo(5d));
    }
    internal void ReplaceInvestor(object investor) => Mandate.GetType().GetField("<Investor>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Mandate, investor);
    internal bool Cancel() => ReflectionTestHelpers.Call<bool>(Mandate, "Cancel");
    internal long Balance(object account) => (long)ReflectionTestHelpers.Get(account, "Balance");
    internal long Amount(string field, string item) => ((IReadOnlyDictionary<string, long>)ReflectionTestHelpers.Get(Mandate, field))[item];
    internal int Lot(object product, object supplier)
    {
        foreach (object lot in (IEnumerable)ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(product, "Inventory"), "Lots"))
            if (ReferenceEquals(ReflectionTestHelpers.Get(lot, "Key"), supplier)) return (int)ReflectionTestHelpers.Get(lot, "Value");
        return 0;
    }
    internal void Audit()
    {
        Assert.That(Ledger.GetType().GetMethod("Audit").Invoke(Ledger, new object[] { 0L }), Is.True);
    }
    internal string Snapshot() => string.Join("|", Balance(Buyer), Balance(CompanyAccount), Balance(Escrow),
        ((ICollection)ReflectionTestHelpers.Get(Ledger, "Transactions")).Count,
        ReflectionTestHelpers.Get(Mandate, "Status"), ReflectionTestHelpers.Get(Mandate, "RemainingManhours"),
        ReflectionTestHelpers.Get(Mandate, "PaidConstructionFee"), ReflectionTestHelpers.Get(Escrow, "Ledger"),
        string.Join(";", Products.Values.Cast<object>().Distinct().Select(p => $"{ReflectionTestHelpers.Get(p, "Stock")}:{ReflectionTestHelpers.Get(p, "LastSupply")}:{Lot(p, Buyer)}")),
        string.Join(",", ((IReadOnlyDictionary<string, long>)ReflectionTestHelpers.Get(Mandate, "ConsumedMaterials")).Values));
}
