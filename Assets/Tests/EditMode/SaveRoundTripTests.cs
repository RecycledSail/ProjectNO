using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ReflectionTestHelpers;

public class SaveRoundTripTests
{
    private GameObject gameObject, battleObject;
    private object game, battle;
    private readonly List<string> files = new();

    private static object Static(string type, string method, params object[] args) => Find(type)
        .GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == method && m.GetParameters().Length == args.Length)
        .Invoke(null, args);
    private static object Private(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static IDictionary Global(string name) => (IDictionary)Find("GlobalVariables").GetField(name).GetValue(null);
    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private object Nation => ((IDictionary)Get(game, "nations"))["Nation1"];
    private object Province => ((IDictionary)Get(game, "provinces"))["Bebino"];
    private object Capture() => Static("GameSaveState", "Capture", game, battle);
    private static object Clone(object data) => JsonUtility.FromJson(JsonUtility.ToJson(data), data.GetType());
    private void Apply(object data) => Call<object>(Static("GameSaveState", "Restore", Clone(data)), "Apply", game, battle);

    [SetUp]
    public void SetUp()
    {
        Static("GlobalVariables", "LoadData");
        gameObject = new GameObject("Save test game");
        game = gameObject.AddComponent(Find("GameManager"));
        Private(game, "Awake");
        Set(game, "users", TestEconomyFactory.ListOf("User"));
        var engine = gameObject.AddComponent(Find("EconomicEngine"));
        game.GetType().GetField("economicEngine", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, engine);
        battle = Find("BattleManager").GetProperty("Instance").GetValue(null);
        battleObject = ((Component)battle).gameObject;
        Private(battle, "Awake");
        Private(game, "StartNewGame", "Nation1");
        Set(game, "dayoftheWeek", 6);
        Call<object>(game, "SetGameSpeed", 4);
        Call<object>(game, "TogglePause");
    }

    [TearDown]
    public void TearDown()
    {
        if (battleObject != null) UnityEngine.Object.DestroyImmediate(battleObject);
        if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
        foreach (string path in files)
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        files.Clear();
        Find("GlobalVariables").GetField("saveFileName").SetValue(null, null);
        Static("GlobalVariables", "LoadData");
    }

    [Test]
    public void FullWorld_RoundTripKeepsAllStateAndSharedReferences()
    {
        Set(Province, "road", 1);
        Private(game, "ProcessWeeklyEvents");
        object budget = Get(Nation, "governmentBudget");
        Set(Get(budget, "Policy"), "ResearchFund", 123L);
        Set(Get(budget, "Policy"), "RealEstateFund", 321L);
        ((IDictionary)Get(Get(budget, "Policy"), "IndustrySubsidy"))["WheatField"] = 789L;
        object snapshot = Capture();
        string before = JsonUtility.ToJson(snapshot);
        object oldProvince = Province;
        Apply(snapshot);
        Assert.That(JsonUtility.ToJson(Capture()), Is.EqualTo(before));
        Assert.That(Province, Is.Not.SameAs(oldProvince));
        Assert.That(Global("PROVINCES")["Bebino"], Is.SameAs(Province));
        Assert.That(Get(Province, "nation"), Is.SameAs(Nation));
        Assert.That(Get(Province, "ActiveLedger"), Is.SameAs(Get(Nation, "Ledger")));
        Assert.That(Get(Get(game, "player"), "nation"), Is.SameAs(Nation));
        Assert.That(Get(game, "paused"), Is.True);
        Assert.That(Get(game, "dayoftheWeek"), Is.EqualTo(6));
    }

    [Test]
    public void NationalProvinceOrder_IsPreservedForSubsequentSimulation()
    {
        IList owned = (IList)Get(Nation, "provinces");
        object first = owned[0];
        owned.RemoveAt(0);
        owned.Add(first);
        string[] order = owned.Cast<object>().Select(p => (string)Get(p, "name")).ToArray();
        Apply(Capture());
        Assert.That(((IEnumerable)Get(Nation, "provinces")).Cast<object>().Select(p => (string)Get(p, "name")), Is.EqualTo(order));
    }

    [Test]
    public void ResumedWeeklySimulation_MatchesUninterruptedEconomy()
    {
        Private(game, "ProcessWeeklyEvents");
        object checkpoint = Capture();
        Private(game, "ProcessWeeklyEvents");
        string uninterrupted = JsonUtility.ToJson(Capture());
        Apply(checkpoint);
        Private(game, "ProcessWeeklyEvents");
        Assert.That(JsonUtility.ToJson(Capture()), Is.EqualTo(uninterrupted));
    }

    [Test]
    public void PopulationAboveInt32_RoundTripsWithoutTruncation()
    {
        object pop = ((IList)Get(Province, "provinceEthnicPops"))[0];
        Set(((IList)Get(pop, "ageGroups"))[0], "agepopulation", 3_000_000_000L);
        long total = ((IEnumerable)Get(pop, "ageGroups")).Cast<object>().Sum(a => (long)Get(a, "agepopulation"));
        Set(pop, "population", total);
        Call<object>(Province, "InitializePopulation");
        Apply(Capture());
        Assert.That(Get(Province, "population"), Is.EqualTo(total));
    }

    private object PrepareConstruction()
    {
        object mandate = Call<object>(Nation, "PlaceConstructionMandate", Global("BUILDING_TYPE")["WheatField"], Province);
        Assert.That(mandate, Is.Not.Null);
        IDictionary products = (IDictionary)Get(Get(Province, "market"), "Products");
        Set(products["Wood"], "Price", 1);
        Call<object>(products["Wood"], "AddSupply", Get(Nation, "Account"), 800);
        Assert.That(Static("ConstructionProcurement", "TryProcessMarket", TestEconomyFactory.ListOf("ConstructionMandate", mandate),
            products, Get(Nation, "Ledger")), Is.True);
        Assert.That(Call<double>(mandate, "ApplyManhours", 5d), Is.EqualTo(5d));
        return mandate;
    }

    [Test]
    public void PartialConstruction_ResumesAndSettlesExactlyOnce()
    {
        object mandate = PrepareConstruction();
        object checkpoint = Capture();
        Call<double>(mandate, "ApplyManhours", 100d);
        string completed = JsonUtility.ToJson(Capture());
        Apply(checkpoint);
        mandate = ((IEnumerable)Get(Nation, "ConstructionMandates")).Cast<object>().Single();
        Assert.That(Get(mandate, "RemainingManhours"), Is.EqualTo(35d));
        Call<double>(mandate, "ApplyManhours", 100d);
        Assert.That(JsonUtility.ToJson(Capture()), Is.EqualTo(completed));
        Assert.That(Call<double>(mandate, "ApplyManhours", 100d), Is.Zero);
        Apply(Capture()); // Terminal escrow and stale company slot must also round-trip.
    }

    [Test]
    public void PartialConstruction_CancellationReturnsTheSameMoneyAndMaterials()
    {
        object mandate = PrepareConstruction();
        object checkpoint = Capture();
        Assert.That(Call<bool>(mandate, "Cancel"), Is.True);
        string cancelled = JsonUtility.ToJson(Capture());
        Apply(checkpoint);
        mandate = ((IEnumerable)Get(Nation, "ConstructionMandates")).Cast<object>().Single();
        Assert.That(Call<bool>(mandate, "Cancel"), Is.True);
        Assert.That(JsonUtility.ToJson(Capture()), Is.EqualTo(cancelled));
        Apply(Capture());
    }

    [Test]
    public void ActiveBattle_RoundTripsAndUiRefreshDoesNotInflictDamage()
    {
        object enemy = ((IDictionary)Get(game, "nations"))["Nation2"];
        object regiment = ((IList)Get(enemy, "regiments"))[0];
        Set(regiment, "location", Province);
        Private(battle, "UpdateBattleEvent");
        Assert.That(((IDictionary)Field(battle, "battleInProvinces")).Count, Is.GreaterThan(0));
        object checkpoint = Capture();
        Apply(checkpoint);
        Call<object>(Get(game, "dayUIEvent"), "Invoke");
        Assert.That(JsonUtility.ToJson(Capture()), Is.EqualTo(JsonUtility.ToJson(checkpoint)));
        Private(battle, "UpdateBattleEvent");
        Assert.That(JsonUtility.ToJson(Capture()), Is.Not.EqualTo(JsonUtility.ToJson(checkpoint)));
    }

    [Test]
    public void InvalidLedger_DoesNotReplaceLiveWorldOrTracking()
    {
        PrepareConstruction();
        object before = Province;
        int nextId = (int)Find("Regiment").GetField("global_id").GetValue(null);
        object snapshot = Capture();
        object ledger = ((IList)Get(snapshot, "ledgers"))[0];
        Set(ledger, "supply", (long)Get(ledger, "supply") + 1);
        Assert.Throws<TargetInvocationException>(() => Static("GameSaveState", "Restore", snapshot));
        Assert.That(Province, Is.SameAs(before));
        Assert.That(Global("PROVINCES")["Bebino"], Is.SameAs(before));
        Assert.That(Find("Regiment").GetField("global_id").GetValue(null), Is.EqualTo(nextId));
    }

    private string NewSlot()
    {
        string name = "save-test-" + Guid.NewGuid().ToString("N");
        files.Add((string)Static("SaveManager", "GetSavePath", name));
        return name;
    }

    [Test]
    public void FileSave_OverwriteKeepsBackupAndLoadsSuccessfully()
    {
        string slot = NewSlot();
        Assert.That(Static("SaveManager", "TrySave", slot), Is.True);
        string first = File.ReadAllText(files.Last());
        Set(game, "day", 2);
        Assert.That(Static("SaveManager", "TrySave", slot), Is.True);
        Assert.That(File.ReadAllText(files.Last() + ".bak"), Is.EqualTo(first));
        Set(game, "day", 9);
        Assert.That(Static("SaveManager", "TryLoad", slot), Is.True);
        Assert.That(Get(game, "day"), Is.EqualTo(2));
    }

    [Test]
    public void FailedSave_PreservesExistingFileAndSelectedSlot()
    {
        string slot = NewSlot();
        Assert.That(Static("SaveManager", "TrySave", slot), Is.True);
        string original = File.ReadAllText(files.Last());
        object ledger = Get(Nation, "Ledger");
        Set(Nation, "Ledger", null);
        LogAssert.Expect(LogType.Error, new Regex("저장 실패"));
        Assert.That(Static("SaveManager", "TrySave", slot), Is.False);
        Assert.That(File.ReadAllText(files.Last()), Is.EqualTo(original));
        Assert.That(Find("GlobalVariables").GetField("saveFileName").GetValue(null), Is.EqualTo(slot));
        Set(Nation, "Ledger", ledger);
    }

    [Test]
    public void NeutralProvince_KeepsLocalCurrencyTreasuryAndPayroll()
    {
        object neutral = ((IDictionary)Get(game, "provinces"))["Tarantsusi"];
        object checkpoint = Capture();
        long balance = (long)Get(Get(neutral, "LocalTreasuryAccount"), "Balance");
        Apply(checkpoint);
        neutral = ((IDictionary)Get(game, "provinces"))["Tarantsusi"];
        Assert.That(Get(neutral, "nation"), Is.Null);
        Assert.That(Get(neutral, "ActiveLedger"), Is.SameAs(Get(neutral, "LocalLedger")));
        Assert.That(Get(Get(neutral, "LocalTreasuryAccount"), "Balance"), Is.EqualTo(balance));
        Assert.That(Call<bool>(Get(neutral, "Employment"), "TryProcessWeek", 1L), Is.True);
    }

    [Test]
    public void NewSession_ClearsOldConstructionTrackingAndRegimentIds()
    {
        PrepareConstruction();
        Static("GlobalVariables", "LoadData");
        var tracking = (IDictionary)Find("ConstructionMandate").GetField("ActiveByProvince", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        Assert.That(tracking.Count, Is.Zero);
        int count = Global("NATIONS").Values.Cast<object>().Sum(n => ((IList)Get(n, "regiments")).Count);
        Assert.That(Find("Regiment").GetField("global_id").GetValue(null), Is.EqualTo(count));
    }

    [TestCase("")]
    [TestCase("../escape")]
    [TestCase("folder/name")]
    [TestCase("bad:name")]
    public void InvalidFileName_IsRejected(string name)
    {
        Assert.Throws<TargetInvocationException>(() => Static("SaveManager", "GetSavePath", name));
    }

    [Test]
    public void MissingOldAndCorruptFiles_AreRejectedWithoutChangingSession()
    {
        string slot = NewSlot();
        object before = Province;
        foreach (string json in new[] { null, "{\"provinces\":[],\"nations\":[]}", "not-json" })
        {
            if (json != null) File.WriteAllText(files.Last(), json);
            LogAssert.Expect(LogType.Error, new Regex("불러오기 실패"));
            Assert.That(Static("SaveManager", "TryLoad", slot), Is.False);
            Assert.That(Province, Is.SameAs(before));
        }
    }
}
