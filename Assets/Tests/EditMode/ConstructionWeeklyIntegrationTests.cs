using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using static ReflectionTestHelpers;

public class ConstructionWeeklyIntegrationTests
{
    [SetUp]
    public void Setup()
    {
        ((IDictionary)Find("GlobalVariables").GetField("BUILDING_RECIPE").GetValue(null)).Clear();
        ((IDictionary)Find("GlobalVariables").GetField("ADJACENT_PROVINCES").GetValue(null)).Clear();
    }

    [TearDown]
    public void Cleanup()
    {
        // Other fixtures may use an unbacked Target mandate with no recipe.
        ((IDictionary)Find("GlobalVariables").GetField("BUILDING_RECIPE").GetValue(null)).Clear();
        ((IDictionary)Find("GlobalVariables").GetField("ADJACENT_PROVINCES").GetValue(null)).Clear();
    }

    [Test]
    public void WeeklyPhase_SharedMarketBuysBeforePaidWorkAndConservesEveryCashFlow()
    {
        Context c = new();
        object a = c.Place(0), b = c.Place(1);
        c.Audit();
        c.Supply(10);
        c.Payroll(1);
        Assert.That(Get(c.Populations[0], "property"), Is.EqualTo(10L));
        c.Run(10d);
        foreach (object project in new[] { a, b })
        {
            Assert.That(c.Amount(project), Is.EqualTo(5));
            Assert.That(Get(project, "RemainingManhours"), Is.EqualTo(5d));
            Assert.That(Get(project, "PaidConstructionFee"), Is.EqualTo(50L));
        }
        Assert.That(Get(c.Product, "Stock"), Is.Zero);
        Assert.That(Get(c.Product, "LastDemand"), Is.EqualTo(10));
        Assert.That(Get(c.Seller, "Balance"), Is.EqualTo(90L));
        Assert.That(Get(c.Ledger, "WeeklyTaxRevenue"), Is.EqualTo(10L));
        c.Audit();
        c.Supply(10); // Production after construction only becomes available to the next call.
        Assert.That(c.Amount(a), Is.EqualTo(5));
        c.Payroll(2);
        c.Run(10d);
        foreach (object project in new[] { a, b })
        {
            Assert.That(Get(project, "Status").ToString(), Is.EqualTo("Completed"));
            Assert.That(((IReadOnlyDictionary<string, long>)Get(project, "ConsumedMaterials"))["Iron"], Is.EqualTo(10));
            object building = ((IDictionary)Get(Get(project, "TargetProvince"), "buildings"))[Get(project, "BuildingType")];
            Assert.That(Get(Get(building, "Account"), "Balance"), Is.EqualTo(50L));
            Assert.That(Get(building, "Owner"), Is.SameAs(c.Nation));
        }
        c.Audit();
    }

    [Test]
    public void FailedPayroll_DoesNotUseLegacyOrRetainedWorkersEvenAfterProcurement()
    {
        Context c = new();
        object project = c.Place(0);
        c.Supply(10);
        Set(Get(c.Companies[0], "buildingType"), "weeklyWage", 0L);
        c.Payroll(1);
        c.Run(10d);
        Assert.That(c.Amount(project), Is.EqualTo(10));
        Assert.That(Get(project, "RemainingManhours"), Is.EqualTo(10d));
        Assert.That(Get(project, "PaidConstructionFee"), Is.Zero);
        Set(c.Provinces[0], "Employment", null);
        c.Paid.Clear();
        c.Run(10d);
        Assert.That(Get(project, "RemainingManhours"), Is.EqualTo(10d));
        c.Audit();
    }

    [Test]
    public void FailedProcurement_ReportsWaitingButPaidWorkCanConsumePreviouslyAcquiredMaterials()
    {
        Context c = new();
        object project = c.Place(0);
        c.Supply(5);
        c.Run(0d);
        c.Supply(5);
        Set(c.Product, "LastDemand", int.MaxValue);
        c.Payroll(1);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Construction procurement failed"));
        c.Run(10d);
        Assert.That(c.Amount(project), Is.EqualTo(5));
        Assert.That(Get(project, "MaterialSpending"), Is.EqualTo(50L));
        Assert.That(Get(project, "RemainingManhours"), Is.EqualTo(5d));
        Assert.That(c.Text(project), Does.Contain("Procurement failed"));
        Set(c.Product, "LastDemand", 0);
        c.Run(0d);
        Assert.That(c.Text(project), Does.Not.Contain("Procurement failed"));
        c.Audit();
    }

    [Test]
    public void RequestedAndIsolatedProjects_UseTheirCurrentMarketAndAreNotLost()
    {
        Context c = new();
        Set(c.Companies[0], "level", 0);
        object a = c.Place(0), b = c.Place(1);
        Assert.That(Get(a, "Status").ToString(), Is.EqualTo("Requested"));
        Set(c.Provinces[1], "isConnectedToCapital", false);
        object local = New("ProductState", "Iron", 20);
        Call<object>(local, "AddSupply", c.Seller, 10);
        ((IDictionary)Get(Get(c.Provinces[1], "market"), "Products"))["Iron"] = local;
        c.Supply(10);
        c.Run(0d);
        Assert.That(c.Amount(a), Is.EqualTo(10));
        Assert.That(Get(a, "MaterialSpending"), Is.EqualTo(100L));
        Assert.That(Get(b, "MaterialSpending"), Is.EqualTo(200L));
        Set(c.Companies[0], "level", 1);
        Call<object>(c.Nation, "SimulateWeeklyTurn");
        c.Payroll(1);
        c.Run(10d);
        Assert.That(Get(a, "Status").ToString(), Is.EqualTo("Completed"));
        c.Audit();
    }

    [Test]
    public void MaterialFreeContract_WithNoMarketStillUsesPaidLabor()
    {
        Context c = new();
        ((IDictionary)Get(c.Recipe, "requireItems")).Clear();
        object project = c.Place(0);
        Set(c.Provinces[0], "isConnectedToCapital", false);
        Set(c.Provinces[0], "market", null);
        c.Payroll(1);
        c.Run(10d);
        Assert.That(Get(project, "Status").ToString(), Is.EqualTo("Completed"));
        c.Audit();
    }

    [Test]
    public void StatusText_SeparatesContractEconomicsAndUnknownPricesFromZero()
    {
        Context c = new();
        object project = c.Place(0);
        Assert.That(c.Text(project), Does.Contain("Waiting for materials"));
        Assert.That(c.Text(project), Does.Contain("Materials spent: 0"));
        Assert.That(c.Text(project), Does.Contain("Construction fee paid: 0 / 100"));
        Assert.That(c.Text(project), Does.Contain("Operating capital: 50"));
        Assert.That(c.Text(project), Does.Contain("Estimated remaining materials: 100"));
        ((IDictionary)Get(Get(c.Nation, "market"), "Products")).Clear();
        Assert.That(c.Text(project), Does.Contain("Estimated remaining materials: Unavailable"));
        Set(c.Recipe, "ConstructionFee", 999L);
        Set(c.Recipe, "InitialCapital", 999L);
        Assert.That(c.Text(project), Does.Contain("Construction fee paid: 0 / 100"));
        Assert.That(c.Text(project), Does.Contain("Operating capital: 50"));
        Call<bool>(project, "Cancel");
        Assert.That(c.Text(project), Does.Contain("Cancelled"));
    }

    [Test]
    public void QueuePrefab_CompactSummaryFitsExistingCellAndTooltipWrapsLongEconomics()
    {
        Context c = new();
        object project = c.Place(0);
        GameObject canvas = new("TestCanvas", typeof(Canvas));
        GameObject instance = null;
        try
        {
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/UI/Build/BuildQueueItem.prefab"));
            instance.transform.SetParent(canvas.transform, false);
            Component queue = instance.GetComponent(Find("BuildQueueItem"));
            Call<object>(queue, "SetMandate", project);
            Call<object>(queue, "SetMandate", project);
            Component count = (Component)Get(queue, "countText");
            Canvas.ForceUpdateCanvases();
            string summary = (string)Get(count, "text");
            Assert.That(summary, Does.Contain("Wait mats"));
            Assert.That(summary, Does.Not.Contain("Operating capital"));
            Vector2 preferred = (Vector2)count.GetType().GetMethod("GetPreferredValues", new[] { typeof(string) }).Invoke(count, new object[] { summary });
            Rect rect = ((RectTransform)count.transform).rect;
            Assert.That(preferred.x, Is.LessThanOrEqualTo(rect.width));
            Assert.That(preferred.y, Is.LessThanOrEqualTo(rect.height));
            AssertGlyphs(count, summary + c.Text(project));
            Set(project, "ProcurementFailed", true);
            AssertSummaryFits(queue, count, project, rect);
            Set(project, "ProcurementFailed", false);
            c.Supply(10);
            c.Run(0d);
            foreach (string status in new[] { "Assigned", "Requested", "InProgress", "Completed", "Cancelled" })
            {
                Set(project, "Status", Enum.Parse(Find("ConstructionMandateStatus"), status));
                AssertSummaryFits(queue, count, project, rect);
            }
            Assert.That(instance.GetComponents(Find("BuildRequirementTooltip")).Length, Is.EqualTo(1));
            Component tooltip = instance.GetComponent(Find("BuildRequirementTooltip"));
            Call<object>(tooltip, "SetMessage", "Estimated remaining materials: 9,223,372,036,854,775,807\n" + new string('W', 100));
            Call<object>(tooltip, "OnPointerEnter", new PointerEventData(null));
            Transform popup = canvas.transform.Find("BuildRequirementTooltip");
            Assert.That(popup, Is.Not.Null);
            Component text = popup.Find("Text").GetComponent(Find("TMPro.TextMeshProUGUI"));
            Assert.That(Get(text, "textWrappingMode").ToString(), Is.EqualTo("Normal"));
            AssertGlyphs(text, (string)Get(text, "text") + c.Text(project));
            Assert.That(((RectTransform)popup).rect.width, Is.LessThanOrEqualTo(388f));
            TestContext.WriteLine($"Prefab row={((RectTransform)instance.transform).rect.size}; count={rect.size}; preferred={preferred}; tooltip={((RectTransform)popup).rect.size}");
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); if (instance != null) UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void AssertSummaryFits(Component queue, Component count, object project, Rect rect)
    {
        Call<object>(queue, "SetMandate", project);
        string summary = (string)Get(count, "text");
        Vector2 preferred = (Vector2)count.GetType().GetMethod("GetPreferredValues", new[] { typeof(string) }).Invoke(count, new object[] { summary });
        Assert.That(preferred.x, Is.LessThanOrEqualTo(rect.width), summary);
        Assert.That(preferred.y, Is.LessThanOrEqualTo(rect.height), summary);
        AssertGlyphs(count, summary + (string)Find("ConstructionStatusText").GetMethod("Format").Invoke(null, new[] { project }));
    }

    private static void AssertGlyphs(Component text, string message)
    {
        object font = Get(text, "font");
        MethodInfo hasCharacter = font.GetType().GetMethod("HasCharacter", new[] { typeof(char), typeof(bool), typeof(bool) });
        foreach (char glyph in message.Distinct().Where(glyph => !char.IsWhiteSpace(glyph)))
            Assert.That((bool)hasCharacter.Invoke(font, new object[] { glyph, true, false }), Is.True,
                $"Font {((UnityEngine.Object)font).name} cannot render '{glyph}' (U+{(int)glyph:X4})");
    }

    private sealed class Context
    {
        public readonly object Nation, Ledger, Seller, Product, Recipe;
        public readonly object[] Provinces = new object[2], Companies = new object[2], Populations = new object[2];
        public readonly IList Paid = (IList)TestEconomyFactory.ListOf("Province");
        private readonly object targetType = New("BuildingType", "Target");
        public Context()
        {
            Nation = TestEconomyFactory.NewNation("Weekly", 2000L);
            IDictionary recipes = (IDictionary)Find("GlobalVariables").GetField("BUILDING_RECIPE").GetValue(null);
            Recipe = New("BuildingRecipe", "Target");
            Set(Recipe, "TimeToBuild", 10); Set(Recipe, "InitialCapital", 50L); Set(Recipe, "ConstructionFee", 100L);
            ((IDictionary)Get(Recipe, "requireItems"))["Iron"] = 10;
            recipes["Target"] = Recipe;
            for (int i = 0; i < 2; i++)
            {
                Provinces[i] = TestEconomyFactory.NewProvince(i + 1, "P" + i);
                Call<bool>(Nation, "AddProvinces", Provinces[i]);
                Set(Provinces[i], "market", New("ProvinceMarket", "P" + i));
                Set(Provinces[i], "isConnectedToCapital", true);
                Populations[i] = TestEconomyFactory.AddPop(Provinces[i], 0L, 1d);
                object type = New("BuildingType", "Company" + i);
                Set(type, "workerNeeded", 10); Set(type, "weeklyWage", 1L);
                object recipe = New("BuildingRecipe", "Company" + i);
                Set(recipe, "InitialCapital", 100L); recipes["Company" + i] = recipe;
                Companies[i] = New("ConstructionCompanyBuilding", type, Provinces[i], 1);
                Set(Companies[i], "currentWorkers", 10L);
                ((IDictionary)Get(Provinces[i], "buildings"))[type] = Companies[i];
            }
            Find("EconomicInitializer").GetMethod("Initialize").Invoke(null, new[] { TestEconomyFactory.ListOf("Nation", Nation), TestEconomyFactory.ListOf("Province", Provinces) });
            Ledger = Get(Nation, "Ledger");
            Seller = New("MoneyAccount", "seller", 0L);
            Assert.That(Call<bool>(Ledger, "RegisterEmptyAccount", Seller), Is.True);
            Product = New("ProductState", "Iron", 10);
            ((IDictionary)Get(Get(Nation, "market"), "Products"))["Iron"] = Product;
            foreach (object province in Provinces) Find("ProvinceEmployment").GetMethod("Initialize").Invoke(null, new[] { province });
            Audit();
        }
        public object Place(int index) { object project = Call<object>(Nation, "PlaceConstructionMandate", targetType, Provinces[index]); Assert.That(project, Is.Not.Null); Audit(); return project; }
        public void Supply(int quantity) => Call<object>(Product, "AddSupply", Seller, quantity);
        public long Amount(object project) => ((IReadOnlyDictionary<string, long>)Get(project, "AcquiredMaterials"))["Iron"];
        public string Text(object project) => (string)Find("ConstructionStatusText").GetMethod("Format").Invoke(null, new[] { project });
        public void Payroll(long week)
        {
            Paid.Clear();
            foreach (object province in Provinces) if (Call<bool>(Get(province, "Employment"), "TryProcessWeek", week)) Paid.Add(province);
            Audit();
        }
        public void Run(double hours)
        {
            Find("ConstructionWeeklySimulation").GetMethod("Process").Invoke(null, new[] {
                TestEconomyFactory.ListOf("Nation", Nation, Nation), TestEconomyFactory.ListOf("Province", Provinces.Concat(Provinces).ToArray()), Paid, (object)hours });
            Audit();
        }
        public void Audit()
        {
            object[] args = { 0L };
            Assert.That(Ledger.GetType().GetMethod("Audit").Invoke(Ledger, args), Is.True);
            Assert.That(args[0], Is.EqualTo(2000L));
        }
    }
}
