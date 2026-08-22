# Monetary Circulation Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build nation and neutral-province monetary ledgers that conserve money through initialization, construction investment, production, market purchases, sales tax, and government policy.

**Architecture:** Every economic actor owns a `MoneyAccount`, while one `MoneyLedger` per nation or neutral province is the only component allowed to mutate account balances. Markets retain aggregate price data but add supplier-owned inventory; `MarketSettlement` plans proportional sales, commits one balanced monetary batch, and only then mutates inventory. Existing weekly systems are migrated one flow at a time, and the final task removes all legacy balance setters.

**Tech Stack:** Unity 6.0 (`6000.0.71f1`), C# 9, Unity Test Framework/NUnit EditMode tests, Unity `JsonUtility`, JSON resources.

**Spec:** `docs/superpowers/specs/2026-08-22-monetary-circulation-design.md`

## Global Constraints

- Starting wealth is explicitly authored and intentionally unequal; no runtime equality formula may replace it.
- `ProvinceEthnicPop.property` is total group wealth, not per-capita wealth.
- A normal transfer must debit and credit the same amount atomically.
- Only a national issuing authority may increase or decrease money supply through `Mint` or `Burn`.
- Neutral ledgers cannot issue or destroy currency.
- Cross-ledger market payments are rejected; neutral absorption is the only 1:1 ledger migration in this phase.
- Buildings start at zero balance and receive `initialCapital * level` from the applicable treasury.
- Construction operating capital is separate from materials, construction-company fees, and wages.
- Aggregate product stock must equal the sum of supplier-owned quantities.
- Supplier sales and net proceeds are allocated proportionally with deterministic integer remainders.
- Sales tax uses integer basis points; `1000` means 10%.
- GDP is a statistic and must not directly create tax revenue.
- Employment, wages, dividends, autonomous population investment, quality, distance, transport, foreign exchange, and international settlement remain outside this plan.
- Preserve the user's existing `ProjectNO.slnx` modification and do not stage it.

---

## File Structure

- Create `Assets/Scripts/Class/MoneyAccount.cs`: account state, transfer entries, transaction record types.
- Create `Assets/Scripts/Class/MoneyLedger.cs`: registration, atomic transfer batches, issuance, destruction, audit, and account migration primitives.
- Create `Assets/Scripts/Class/ProportionalAllocator.cs`: deterministic integer allocation shared by basic production and market settlement.
- Create `Assets/Scripts/Class/EconomicInitializer.cs`: national and neutral ledger creation, account registration, validation, and starting-building capitalization.
- Create `Assets/Scripts/Class/ProvinceCurrencyMigration.cs`: atomic 1:1 absorption of a neutral province.
- Create `Assets/Scripts/Class/MarketInventory.cs`: supplier-owned quantities and planned inventory mutations.
- Create `Assets/Scripts/Class/MarketSettlement.cs`: single-product and basket purchase planning and atomic settlement.
- Modify `Assets/Scripts/Class/Nation.cs`: national account, ledger, sales-tax setting, construction investment, and policy-facing account access.
- Modify `Assets/Scripts/Class/EthnicGroup.cs`: JSON-provided wealth/living standard and population money account.
- Modify `Assets/Scripts/Class/Building.cs`: zero-based `long` balance account and recipe `InitialCapital`.
- Modify `Assets/Scripts/Class/Province.cs`: neutral treasury/ledger, owned basic production, paid building inputs, owned building outputs.
- Modify `Assets/Scripts/Class/ConstructionMandate.cs`: registered escrow, refund, and one-time capitalization.
- Modify `Assets/Scripts/Class/BuildingFactory.cs`: create buildings with zero money and stable account IDs.
- Modify `Assets/Scripts/Class/Market.cs`: owned inventory integration and a common product-market interface.
- Modify `Assets/Scripts/Class/EconomicEngine.cs`: per-pop food purchases through settlement.
- Modify `Assets/Scripts/Class/EconomicEngine.Production.cs`: per-pop category purchases through settlement.
- Modify `Assets/Scripts/Class/GovernmentBudget.cs`: ledger-backed supply/tax views and conserving policy distribution.
- Modify `Assets/Scripts/GlobalVariables.cs`: JSON schemas, validated data loading, and removal of `buildingStartBalance`.
- Modify `Assets/Scripts/Manager/GameManager.cs`: initialize economies, move owned inventory, reset weekly tax totals, and audit ledgers.
- Modify `Assets/Scripts/UI/Building/BuildProvinceButtonUI.cs`: expose insufficient-investment-capital failures and use domain validation.
- Modify `Assets/Resources/Provinces.json`: explicit population wealth/living standards and neutral treasuries.
- Modify `Assets/Resources/Nations.json`: unequal national starting balances.
- Modify `Assets/Resources/BuildingRecipes.json`: explicit initial capital for every recipe.
- Create `Assets/Tests/EditMode/ReflectionTestHelpers.cs`: shared reflection helpers for the isolated EditMode assembly.
- Create focused test files under `Assets/Tests/EditMode/` for each task.
- Modify `Assets/Tests/EditMode/ConstructionMandateTests.cs`: construct funded mandates and preserve existing lifecycle coverage.
- Modify `obsedian documentry/GovernmentBudget.md`: document ledger-backed supply, actual sales tax, and issuance behavior.

---

### Task 1: Monetary account, atomic ledger, and deterministic allocation

**Files:**
- Create: `Assets/Scripts/Class/MoneyAccount.cs`
- Create: `Assets/Scripts/Class/MoneyLedger.cs`
- Create: `Assets/Scripts/Class/ProportionalAllocator.cs`
- Create: `Assets/Tests/EditMode/ReflectionTestHelpers.cs`
- Create: `Assets/Tests/EditMode/MoneyLedgerTests.cs`
- Create: `Assets/Tests/EditMode/ProportionalAllocatorTests.cs`

**Interfaces:**
- Produces: `MoneyAccount(string id, long openingBalance = 0)`
- Produces: `long MoneyAccount.Balance`
- Produces: `MoneyLedger(string currencyId, object issuanceAuthority, MoneyAccount treasuryAccount)`
- Produces: `bool MoneyLedger.RegisterInitialAccount(MoneyAccount account)`
- Produces: `bool MoneyLedger.RegisterEmptyAccount(MoneyAccount account)`
- Produces: `bool MoneyLedger.UnregisterEmptyAccount(MoneyAccount account)`
- Produces: `void MoneyLedger.SealInitialization()`
- Produces: `bool MoneyLedger.TryTransfer(MoneyAccount from, MoneyAccount to, long amount, string reason)`
- Produces: `bool MoneyLedger.TryTransferBatch(IReadOnlyList<MoneyTransferEntry> entries, string reason)`
- Produces: `bool MoneyLedger.TryMint(object authority, MoneyAccount target, long amount, string reason)`
- Produces: `bool MoneyLedger.TryBurn(object authority, MoneyAccount source, long amount, string reason)`
- Produces: `bool MoneyLedger.Audit(out long registeredBalance)`
- Produces: `IReadOnlyList<MoneyTransactionRecord> MoneyLedger.Transactions`
- Produces: `Dictionary<T, long> ProportionalAllocator.Allocate<T>(long total, IReadOnlyDictionary<T, long> weights, Func<T, string> stableKey)`

- [ ] **Step 1: Run the existing EditMode suite as the baseline**

Run:

```powershell
New-Item -ItemType Directory -Force -Path 'D:\ProjectNO\TestResults' | Out-Null
& 'C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\ProjectNO' -runTests -testPlatform EditMode -testResults 'D:\ProjectNO\TestResults\monetary-baseline.xml' -logFile 'D:\ProjectNO\TestResults\monetary-baseline.log' -quit
```

Expected: the existing 8 construction mandate tests pass before monetary work begins.

- [ ] **Step 2: Add shared reflection helpers and failing ledger tests**

Create `ReflectionTestHelpers.cs` with public helpers so every later test file can access predefined `Assembly-CSharp` types without an asmdef reference:

```csharp
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class ReflectionTestHelpers
{
    public static Type Find(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Missing runtime type {name}");
        return type;
    }

    public static object New(string name, params object[] args) =>
        Activator.CreateInstance(Find(name), args);

    public static object Get(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public);
        if (property != null) return property.GetValue(instance);
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"Missing member {name} on {type.Name}");
        return field.GetValue(instance);
    }

    public static void Set(object instance, string name, object value)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public);
        if (property != null) { property.SetValue(instance, value); return; }
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"Missing member {name} on {type.Name}");
        field.SetValue(instance, value);
    }

    public static T Call<T>(object instance, string name, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public)
            .Single(candidate => candidate.Name == name &&
                                 candidate.GetParameters().Length == args.Length);
        return (T)method.Invoke(instance, args);
    }
}
```

Create `MoneyLedgerTests.cs` containing these cases:

```csharp
[Test]
public void TransferBatch_IsAtomicAndPreservesSupply()
{
    object authority = new object();
    object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 1000L);
    object seller = ReflectionTestHelpers.New("MoneyAccount", "seller", 0L);
    object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, treasury);
    ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury);
    ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", seller);
    ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

    Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryTransfer",
        treasury, seller, 250L, "test"), Is.True);
    Assert.That(ReflectionTestHelpers.Get(treasury, "Balance"), Is.EqualTo(750L));
    Assert.That(ReflectionTestHelpers.Get(seller, "Balance"), Is.EqualTo(250L));
    Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(1000L));

    Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryTransfer",
        treasury, seller, 751L, "reject"), Is.False);
    Assert.That(ReflectionTestHelpers.Get(treasury, "Balance"), Is.EqualTo(750L));
    Assert.That(ReflectionTestHelpers.Get(seller, "Balance"), Is.EqualTo(250L));
}

[Test]
public void MintAndBurn_RequireAuthorityAndChangeSupplyExactly()
{
    object authority = new object();
    object stranger = new object();
    object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 100L);
    object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, treasury);
    ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury);
    ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

    Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryMint",
        stranger, treasury, 50L, "unauthorized"), Is.False);
    Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryMint",
        authority, treasury, 50L, "authorized"), Is.True);
    Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(150L));
    Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryBurn",
        authority, treasury, 20L, "burn"), Is.True);
    Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(130L));
}
```

Add two more tests: a neutral ledger constructed with `issuanceAuthority == null` rejects `TryMint`/`TryBurn`, and a batch whose deltas do not sum to zero changes no accounts.

- [ ] **Step 3: Add the failing proportional-allocation tests**

Create `ProportionalAllocatorTests.cs` with a 60/40 allocation and a stable remainder case:

```csharp
[Test]
public void Allocate_UsesWeightsAndStableRemainders()
{
    Type allocator = ReflectionTestHelpers.Find("ProportionalAllocator");
    MethodInfo method = allocator.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(candidate => candidate.Name == "Allocate")
        .MakeGenericMethod(typeof(string));
    var weights = new Dictionary<string, long> { ["B"] = 1, ["A"] = 1 };
    var result = (Dictionary<string, long>)method.Invoke(null,
        new object[] { 3L, weights, (Func<string, string>)(value => value) });

    Assert.That(result["A"], Is.EqualTo(2L));
    Assert.That(result["B"], Is.EqualTo(1L));
    Assert.That(result.Values.Sum(), Is.EqualTo(3L));
}
```

- [ ] **Step 4: Run the focused tests and verify red**

Run the Unity command from Step 1 with `-testFilter 'MoneyLedgerTests|ProportionalAllocatorTests'` and result name `monetary-core-red.xml`.

Expected: FAIL because `MoneyAccount`, `MoneyLedger`, and `ProportionalAllocator` do not exist.

- [ ] **Step 5: Implement account and ledger state**

Implement `MoneyAccount` with a read-only public balance and ledger-only mutation:

```csharp
public sealed class MoneyAccount
{
    public string Id { get; }
    public long Balance { get; private set; }
    public MoneyLedger Ledger { get; internal set; }

    public MoneyAccount(string id, long openingBalance = 0)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
        if (openingBalance < 0) throw new ArgumentOutOfRangeException(nameof(openingBalance));
        Id = id;
        Balance = openingBalance;
    }

    internal void ApplyDelta(long delta) => Balance = checked(Balance + delta);
    internal void ReplaceForLoading(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Balance = value;
    }
}
```

Define `MoneyTransferEntry` as an account plus signed `Delta`; a valid batch has a checked sum of zero. `MoneyLedger` must validate every resulting balance and the entire batch before applying any delta. Registration before `SealInitialization()` adds opening balances to `MoneySupply`; post-seal registration accepts only zero-balance accounts. `UnregisterEmptyAccount` succeeds only for a registered zero-balance account. `TryMint` and `TryBurn` compare the supplied authority with the constructor authority by reference.

After each successful operation append a `MoneyTransactionRecord` containing operation kind (`Transfer`, `Mint`, `Burn`, or `Migration`), source IDs, destination IDs, amount, and reason. Failed operations append no record. Keep this diagnostic history read-only to callers.

- [ ] **Step 6: Implement deterministic proportional allocation**

Use `System.Numerics.BigInteger` for `total * weight`, allocate each floor share, then award remaining units by descending fractional remainder and ascending `stableKey`. Reject negative totals or weights and return zero for every key when total or total weight is zero.

- [ ] **Step 7: Run focused and full tests**

Run the focused filter and then the full EditMode command.

Expected: all new core tests and the existing 8 tests pass; Unity compilation reports zero errors.

- [ ] **Step 8: Commit the monetary core**

```powershell
git add -- 'Assets/Scripts/Class/MoneyAccount.cs' 'Assets/Scripts/Class/MoneyLedger.cs' 'Assets/Scripts/Class/ProportionalAllocator.cs' 'Assets/Tests/EditMode/ReflectionTestHelpers.cs' 'Assets/Tests/EditMode/MoneyLedgerTests.cs' 'Assets/Tests/EditMode/ProportionalAllocatorTests.cs'
git commit -m "feat: add conserving monetary ledger"
```

---

### Task 2: Unequal JSON wealth and economic initialization

**Files:**
- Create: `Assets/Scripts/Class/EconomicInitializer.cs`
- Modify: `Assets/Scripts/Class/Nation.cs`
- Modify: `Assets/Scripts/Class/EthnicGroup.cs`
- Modify: `Assets/Scripts/Class/Building.cs`
- Modify: `Assets/Scripts/Class/Province.cs`
- Modify: `Assets/Scripts/Class/BuildingFactory.cs`
- Modify: `Assets/Scripts/Class/GovernmentBudget.cs`
- Modify: `Assets/Scripts/GlobalVariables.cs`
- Modify: `Assets/Scripts/Manager/GameManager.cs`
- Modify: `Assets/Resources/Provinces.json`
- Modify: `Assets/Resources/Nations.json`
- Modify: `Assets/Resources/BuildingRecipes.json`
- Create: `Assets/Tests/EditMode/EconomicInitializationTests.cs`

**Interfaces:**
- Consumes: Task 1 `MoneyAccount`, `MoneyLedger.TryTransfer`, registration, and sealing.
- Produces: `MoneyAccount Nation.Account`
- Produces: `MoneyLedger Nation.Ledger`
- Produces: `MoneyAccount ProvinceEthnicPop.Account`
- Produces: `MoneyAccount Building.Account`
- Produces: `MoneyAccount Province.LocalTreasuryAccount`
- Produces: `MoneyLedger Province.LocalLedger`
- Produces: `MoneyLedger Province.ActiveLedger`
- Produces: `long BuildingRecipe.InitialCapital`
- Produces: `void EconomicInitializer.Initialize(IEnumerable<Nation> nations, IEnumerable<Province> provinces)`

- [ ] **Step 1: Write failing data and capitalization tests**

Add tests that read the three resource files and assert every population has nonnegative `property` and positive `livingStandard`, every nation has nonnegative `initialBalance`, and every recipe has positive `initialCapital`. Add a domain test with one nation, one pop, and a level-2 building:

```csharp
[Test]
public void Initialize_CapitalizesStartingBuildingWithoutChangingSupply()
{
    object nation = TestEconomyFactory.NewNation("N1", 1000L);
    object province = TestEconomyFactory.NewProvince(1, "P1");
    ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province);
    object pop = TestEconomyFactory.AddPop(province, 300L, 1.0);
    object recipe = TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);

    ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
        .Invoke(null, new object[] {
            TestEconomyFactory.ListOf("Nation", nation),
            TestEconomyFactory.ListOf("Province", province)
        });

    object building = TestEconomyFactory.GetOnlyBuilding(province);
    Assert.That(ReflectionTestHelpers.Get(nation, "balance"), Is.EqualTo(800L));
    Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(200L));
    Assert.That(ReflectionTestHelpers.Get(pop, "property"), Is.EqualTo(300L));
    object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
    Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(1300L));
}
```

Add this `TestEconomyFactory` to `ReflectionTestHelpers.cs`, plus a failure test asserting an `InvalidOperationException` names the nation when its treasury cannot fund starting buildings:

```csharp
public static class TestEconomyFactory
{
    public static object NewNation(string name, long openingBalance)
    {
        object researches = Activator.CreateInstance(
            typeof(List<>).MakeGenericType(ReflectionTestHelpers.Find("ResearchNode")));
        return Activator.CreateInstance(ReflectionTestHelpers.Find("Nation"),
            new[] { (object)1, name, researches, openingBalance });
    }

    public static object NewProvince(int id, string name) =>
        Activator.CreateInstance(ReflectionTestHelpers.Find("Province"),
            id, name, Enum.Parse(ReflectionTestHelpers.Find("Topography"), "Plane"));

    public static object AddPop(object province, long property, double livingStandard)
    {
        object species = Activator.CreateInstance(ReflectionTestHelpers.Find("SpeciesSpec"));
        ReflectionTestHelpers.Set(species, "name", "Human");
        object culture = ReflectionTestHelpers.New("Culture", "TestCulture");
        object group = ReflectionTestHelpers.New("EthnicGroup", species, culture);
        object pop = ReflectionTestHelpers.New("ProvinceEthnicPop", province, group,
            new List<int> { 0, 100, 0, 0 }, property, livingStandard);
        ((IList)ReflectionTestHelpers.Get(province, "provinceEthnicPops")).Add(pop);
        ReflectionTestHelpers.Call<object>(province, "InitializePopulation");
        return pop;
    }

    public static object AddBuilding(
        object province, string typeName, int level, long initialCapital)
    {
        object type = ReflectionTestHelpers.New("BuildingType", typeName);
        object recipe = ReflectionTestHelpers.New("BuildingRecipe", typeName);
        ReflectionTestHelpers.Set(recipe, "InitialCapital", initialCapital);
        FieldInfo recipes = ReflectionTestHelpers.Find("GlobalVariables").GetField(
            "BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static);
        ((IDictionary)recipes.GetValue(null))[typeName] = recipe;
        MethodInfo create = ReflectionTestHelpers.Find("BuildingFactory").GetMethod("Create");
        object building = create.Invoke(null, new[] { type, province, (object)level, 0L });
        ((IDictionary)ReflectionTestHelpers.Get(province, "buildings"))[type] = building;
        return recipe;
    }

    public static object GetOnlyBuilding(object province) =>
        ((IDictionary)ReflectionTestHelpers.Get(province, "buildings")).Values
            .Cast<object>().Single();

    public static object ListOf(string runtimeType, params object[] values)
    {
        IList list = (IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(ReflectionTestHelpers.Find(runtimeType)));
        foreach (object value in values) list.Add(value);
        return list;
    }
}
```

- [ ] **Step 2: Run initialization tests and verify red**

Run the Unity command with `-testFilter EconomicInitializationTests`.

Expected: FAIL because JSON fields, actor accounts, and `EconomicInitializer` are absent.

- [ ] **Step 3: Add JSON fields and constructors**

Update the runtime data formats exactly:

```csharp
public sealed class NationData
{
    public int id;
    public string name;
    public long initialBalance;
    public ColorData color;
    public List<string> researchNodeNames;
    public List<RegimentData> regiments;
}

public sealed class ProvinceData
{
    public int id;
    public string name;
    public long initialLocalTreasury;
    public List<SpeciesPopData> pops;
    public string topography;
    public List<BuildingData> buildings;
    public List<SpecialBuildingData> specialBuildings = new();
}

public sealed class SpeciesPopData
{
    public string name;
    public List<int> population;
    public string culture;
    public long property;
    public double livingStandard;
}

public sealed class BuildingrecipeData
{
    public string name;
    public List<ItemData> requireItems;
    public List<ItemData> buildRequirements;
    public int TimeToBuild;
    public long initialCapital;
}
```

Load those values into actor constructors and `BuildingRecipe.InitialCapital`. Remove `GlobalVariables.buildingStartBalance`; new and loaded buildings always create `Account` with opening balance zero. During this task only, keep legacy `balance`/`property` setters forwarding to `MoneyAccount.ReplaceForLoading` so untouched systems compile; Task 9 removes those setters after every writer is migrated.

Use overloads that keep existing fixtures compiling while ensuring runtime JSON supplies the real values. Change the existing three-argument nation constructor body into a four-argument constructor, add `Account = new MoneyAccount($"nation:{name}:treasury", initialBalance);` before constructing `GovernmentBudget`, and make the old signature delegate:

```csharp
public Nation(int id, string name, List<ResearchNode> researches)
    : this(id, name, researches, 0L) { }
```

Likewise, move the current population constructor's age-group initialization into a new five-argument overload. Replace the hardcoded property and living-standard assignments with `Account = new MoneyAccount($"pop:{province.name}:{group.species.name}:{group.culture.name}", openingProperty);` and `livingStandard = openingLivingStandard;`. Keep this delegating overload for existing fixtures:

```csharp
public ProvinceEthnicPop(
    Province province, EthnicGroup group, List<int> population)
    : this(province, group, population, 0L, 1.0) { }
```

`Building` creates `Account` with ID `building:{province.name}:{buildingType.name}` and zero opening balance. The current province dictionary permits only one building instance per type, so this ID is stable and unique within the implemented model.

- [ ] **Step 4: Seed explicit unequal resource values**

Use these concrete population totals and living standards in `Provinces.json`:

| Province / population | property | livingStandard |
| --- | ---: | ---: |
| Bebino Human/Paimon | 180000 | 1.20 |
| Stein Human/Paimon | 40000 | 0.90 |
| Eiglepsk Human/Paimon | 15000 | 0.80 |
| Sando Human/Paimon | 140000 | 1.00 |
| Sando Elf/Lisa | 30000 | 0.70 |
| Talem Human/Paimon | 60000 | 0.75 |
| Uzyda Human/Paimon | 65000 | 0.80 |
| Buske Human/Paimon | 55000 | 0.75 |
| Svovoda Human/Paimon | 90000 | 0.90 |
| Tarantsusi Human/Paimon | 80000 | 0.85 |
| Jojisha Human/Paimon | 110000 | 0.95 |
| Matz Human/Paimon | 90000 | 0.85 |
| Zilia Human/Paimon | 70000 | 0.80 |
| Prano Human/Paimon | 75000 | 0.80 |
| Meril Human/Paimon | 100000 | 0.95 |
| Ozisk Human/Paimon | 60000 | 0.75 |

Set `initialLocalTreasury` to `50000` for Tarantsusi, Prano, Meril, and Ozisk and to `0` for nationally assigned provinces. Set national balances to Nation1 `300000`, Nation2 `220000`, Nation3 `160000`.

Use these recipe capitals: WheatField `5000`, LogField `5000`, SlimeFarm `7000`, IronMine `8000`, SwordSmith `15000`, FurnitureShop `10000`, Stable `9000`, GoldMine `15000`, CoalMine `8000`, GlassShop `10000`, PaperShop `9000`, BookShop `9000`, PotionShop `14000`, WoolField `6000`, ClothShop `10000`, LeatherShop `11000`, SilkField `9000`, CatalystShop `14000`, and construcntionCompany `20000`.

- [ ] **Step 5: Implement economic initialization**

Implement `EconomicInitializer.Initialize` after province ownership is assigned. For each nation, create its ledger with `nation` as issuance authority and its national account as treasury; register the national account and all population accounts at opening balance; register buildings empty; transfer `InitialCapital * level` from treasury to each building; seal. For every province whose `nation` is null, create a local treasury from `initialLocalTreasury`, construct a ledger with null authority, register population/building accounts, capitalize buildings, and seal.

Throw record-specific `InvalidOperationException` messages such as:

```text
Nation Nation1 cannot capitalize WheatField level 3 in Sando: need 15000, have 12000.
Neutral province Prano cannot capitalize WheatField level 3: need 15000, have 10000.
```

Call initialization once at the end of `GameManager.StartNewGame`, after all calls to `nation.AddProvinces` and capital assignment.

- [ ] **Step 6: Run focused and full tests**

Expected: initialization and JSON tests pass, all construction tests still pass, and each starting national/neutral ledger audits successfully.

- [ ] **Step 7: Commit initialization and data**

```powershell
git add -- 'Assets/Scripts/Class/EconomicInitializer.cs' 'Assets/Scripts/Class/Nation.cs' 'Assets/Scripts/Class/EthnicGroup.cs' 'Assets/Scripts/Class/Building.cs' 'Assets/Scripts/Class/Province.cs' 'Assets/Scripts/Class/BuildingFactory.cs' 'Assets/Scripts/Class/GovernmentBudget.cs' 'Assets/Scripts/GlobalVariables.cs' 'Assets/Scripts/Manager/GameManager.cs' 'Assets/Resources/Provinces.json' 'Assets/Resources/Nations.json' 'Assets/Resources/BuildingRecipes.json' 'Assets/Tests/EditMode/ReflectionTestHelpers.cs' 'Assets/Tests/EditMode/EconomicInitializationTests.cs'
git commit -m "feat: initialize unequal conserved money supplies"
```

---

### Task 3: Neutral province 1:1 ledger absorption

**Files:**
- Create: `Assets/Scripts/Class/ProvinceCurrencyMigration.cs`
- Modify: `Assets/Scripts/Class/MoneyLedger.cs`
- Modify: `Assets/Scripts/Class/Province.cs`
- Modify: `Assets/Scripts/Class/Nation.cs`
- Create: `Assets/Tests/EditMode/NeutralProvinceLedgerTests.cs`

**Interfaces:**
- Consumes: initialized `Province.LocalLedger`, `Province.LocalTreasuryAccount`, actor accounts, destination `Nation.Ledger`.
- Produces: `bool ProvinceCurrencyMigration.TryAbsorbNeutralProvince(Province province, Nation destination, out string error)`
- Produces: internal atomic ledger migration primitive used only by `ProvinceCurrencyMigration`.

- [ ] **Step 1: Write a failing combined-supply migration test**

Create a neutral province with a local treasury balance of 500, a pop balance of 300, and a building balance of 200; create a nation with supply 1000. Assert absorption succeeds, the neutral supply becomes zero, national supply becomes 2000, every account now references the national ledger, the local treasury becomes zero, the national treasury receives its 500, and no actor balance changes. Add a validation-failure test with one account registered to an unrelated ledger and assert all supplies, balances, and ownership remain unchanged.

- [ ] **Step 2: Run the test and verify red**

Run with `-testFilter NeutralProvinceLedgerTests`.

Expected: FAIL because `ProvinceCurrencyMigration` does not exist.

- [ ] **Step 3: Implement atomic absorption**

Build the complete account set before mutation from every population and building account in the province. Validate that each belongs to `province.LocalLedger`, that the destination ledger is sealed, and that the national treasury can receive the local treasury balance without overflow. Then perform one internal migration that:

```text
source supply -= actor balances + local treasury
destination supply += actor balances + local treasury
actor Ledger references = destination
destination treasury += local treasury
local treasury = 0
province.LocalLedger = null
province.LocalTreasuryAccount = null
```

Only after the ledger operation succeeds should `destination.AddProvinces(province)` change territorial ownership. If `AddProvinces` fails, the migration method must not run.

- [ ] **Step 4: Run focused and full tests**

Expected: neutral absorption preserves combined supply and all initialization/construction tests pass.

- [ ] **Step 5: Commit neutral absorption**

```powershell
git add -- 'Assets/Scripts/Class/ProvinceCurrencyMigration.cs' 'Assets/Scripts/Class/MoneyLedger.cs' 'Assets/Scripts/Class/Province.cs' 'Assets/Scripts/Class/Nation.cs' 'Assets/Tests/EditMode/NeutralProvinceLedgerTests.cs'
git commit -m "feat: absorb neutral province currency ledgers"
```

---

### Task 4: Funded construction mandates and escrow lifecycle

**Files:**
- Modify: `Assets/Scripts/Class/ConstructionMandate.cs`
- Modify: `Assets/Scripts/Class/Nation.cs`
- Modify: `Assets/Scripts/Class/BuildingFactory.cs`
- Modify: `Assets/Scripts/Class/ProvinceCurrencyMigration.cs`
- Modify: `Assets/Scripts/UI/Building/BuildProvinceButtonUI.cs`
- Modify: `Assets/Tests/EditMode/ConstructionMandateTests.cs`
- Modify: `Assets/Tests/EditMode/NeutralProvinceLedgerTests.cs`
- Create: `Assets/Tests/EditMode/ConstructionInvestmentTests.cs`

**Interfaces:**
- Consumes: actor `MoneyAccount`, `MoneyLedger`, and `BuildingRecipe.InitialCapital`.
- Produces: `MoneyAccount IBuildingInvestor.InvestmentAccount`
- Produces: `MoneyAccount ConstructionMandate.EscrowAccount`
- Produces: `long ConstructionMandate.InvestedCapital`
- Produces: `bool Nation.CanPlaceConstructionMandate(BuildingType type, Province target, out string error)`
- Preserves: `ConstructionMandate Nation.PlaceConstructionMandate(BuildingType type, Province target)`

- [ ] **Step 1: Write failing escrow tests**

Cover these exact cases:

```csharp
[Test]
public void PlaceAndComplete_MovesCapitalThroughEscrowExactlyOnce()
{
    FundedConstruction context = FundedConstruction.Create(
        investorBalance: 1000L, initialCapital: 300L, timeToBuild: 10);
    object mandate = context.Place();

    Assert.That(context.InvestorBalance, Is.EqualTo(700L));
    Assert.That(context.EscrowBalance(mandate), Is.EqualTo(300L));
    Assert.That(context.MoneySupply, Is.EqualTo(1000L));

    context.Progress(10d);
    Assert.That(context.CompletedBuildingBalance, Is.EqualTo(300L));
    Assert.That(context.EscrowBalance(mandate), Is.EqualTo(0L));
    Assert.That(context.MoneySupply, Is.EqualTo(1000L));
    context.Progress(10d);
    Assert.That(context.CompletedBuildingBalance, Is.EqualTo(300L));
}
```

Also assert cancellation refunds 100%, insufficient capital creates no mandate/account/material reservation, and an upgrade adds one recipe capital to the existing building.

Extend the neutral absorption fixture with one funded active mandate and assert its escrow account migrates with the province without changing its balance or combined supply.

- [ ] **Step 2: Run construction investment tests and verify red**

Expected: FAIL because mandates have no escrow and placement ignores investor balance.

- [ ] **Step 3: Implement funded placement and lifecycle**

Extend `IBuildingInvestor` with `InvestmentAccount`. `Nation` and `ProvinceEthnicPop` return their own accounts. `CanPlaceConstructionMandate` must centralize every existing UI/domain check: ownership, duplicate active mandate, recipe, accessible material stock minus active reservations, ledger identity, and full capital affordability.

On placement, register a zero-balance escrow with the target ledger, transfer full capital from investor to escrow, then add the mandate. The active mandate itself represents the material reservation; do not remove construction material stock in this task. If assignment capacity is absent, the funded mandate remains `Requested` and its escrow remains intact.

On cancellation, transfer the full escrow back before setting `Cancelled`. On completion, create/register a zero-balance building when absent, increment level once, and transfer the complete escrow to the building. Roll back mandate creation and unregister the empty escrow if the initial transfer fails.

Update `ProvinceCurrencyMigration` in the same step so its validated account set includes every active mandate escrow whose `TargetProvince` is the absorbed province.

- [ ] **Step 4: Update construction UI feedback**

Replace duplicated UI checks with `CanPlaceConstructionMandate`. Return specific messages for missing materials and insufficient capital, for example `Need operating capital: have 2,000, need 5,000.` Existing queue display and assignment behavior remain unchanged.

- [ ] **Step 5: Run construction, initialization, and full tests**

Expected: existing mandate lifecycle tests remain green after using funded fixtures; new escrow tests pass; supply audits pass at placement, cancellation, and completion.

- [ ] **Step 6: Commit funded construction**

```powershell
git add -- 'Assets/Scripts/Class/ConstructionMandate.cs' 'Assets/Scripts/Class/Nation.cs' 'Assets/Scripts/Class/BuildingFactory.cs' 'Assets/Scripts/Class/ProvinceCurrencyMigration.cs' 'Assets/Scripts/UI/Building/BuildProvinceButtonUI.cs' 'Assets/Tests/EditMode/ConstructionMandateTests.cs' 'Assets/Tests/EditMode/NeutralProvinceLedgerTests.cs' 'Assets/Tests/EditMode/ConstructionInvestmentTests.cs'
git commit -m "feat: fund construction through mandate escrow"
```

---

### Task 5: Producer-owned inventory and ownership-preserving market transfer

**Files:**
- Create: `Assets/Scripts/Class/MarketInventory.cs`
- Modify: `Assets/Scripts/Class/Market.cs`
- Modify: `Assets/Scripts/Class/Province.cs`
- Modify: `Assets/Scripts/Manager/GameManager.cs`
- Create: `Assets/Tests/EditMode/MarketInventoryTests.cs`

**Interfaces:**
- Consumes: `MoneyAccount`, `ProportionalAllocator`.
- Produces: `ProductInventory ProductState.Inventory`
- Produces: `int ProductState.Stock`
- Produces: `void ProductState.AddSupply(MoneyAccount supplier, int amount)`
- Produces: `IReadOnlyList<SupplierSale> ProductState.PlanSale(int amount)`
- Produces: `void ProductState.CommitSale(IReadOnlyList<SupplierSale> sale)`
- Produces: `void ProductState.TransferAllStockTo(ProductState destination)`

- [ ] **Step 1: Write failing ownership tests**

Test two suppliers adding 60 and 40 units, planning a 50-unit sale as 30/20, committing it to leave 30/20, and maintaining `Stock == Inventory.TotalQuantity` after every action. Test equal fractional remainder ordering by `MoneyAccount.Id`. Test `TransferAllStockTo` moves every supplier lot and clears the source without changing combined stock.

Add a province production test asserting `ProduceGoodsWeekly` changes no money balance and attributes basic-food output to province populations in proportion to population.

- [ ] **Step 2: Run inventory tests and verify red**

Expected: FAIL because product stock has no supplier ownership.

- [ ] **Step 3: Implement owned inventory**

`ProductInventory` stores `Dictionary<MoneyAccount, int>`. `PlanSale` is non-mutating and clamps to total stock. `CommitSale` validates every supplier still owns the planned quantity before changing anything. `ProductState.Stock` becomes a read-only view of `Inventory.TotalQuantity`; during Tasks 5–8, keep an internal legacy stock setter only where unmigrated consumers still compile, and remove it in Task 9.

`ProductState.AddSupply` updates inventory and `LastSupply`. Basic production divides the existing fixed 100 units of each basic food among province populations by population weight and calls `AddSupply`; remove `pep.property = ethnicProduction` completely. Building outputs call `AddSupply(building.Account, amount)`.

- [ ] **Step 4: Preserve ownership when moving province stock**

Change `TransferProvinceProductionToNationMarket` to call `pstate.TransferAllStockTo(nationProduct)` rather than writing aggregate stock. Preserve each supplier account and add the moved amount once to the nation product's `LastSupply`, while clearing the source inventory.

- [ ] **Step 5: Run focused and full tests**

Expected: ownership, transfer, and no-money-production tests pass; existing price/UI reads of `Stock` still compile.

- [ ] **Step 6: Commit producer inventory**

```powershell
git add -- 'Assets/Scripts/Class/MarketInventory.cs' 'Assets/Scripts/Class/Market.cs' 'Assets/Scripts/Class/Province.cs' 'Assets/Scripts/Manager/GameManager.cs' 'Assets/Tests/EditMode/MarketInventoryTests.cs'
git commit -m "feat: track producer-owned market inventory"
```

---

### Task 6: Atomic market settlement and sales tax

**Files:**
- Create: `Assets/Scripts/Class/MarketSettlement.cs`
- Modify: `Assets/Scripts/Class/MoneyLedger.cs`
- Modify: `Assets/Scripts/Class/Market.cs`
- Create: `Assets/Tests/EditMode/MarketSettlementTests.cs`

**Interfaces:**
- Consumes: ledger transfer batches and product sale plans.
- Produces: `PurchaseRequest(ProductState product, int quantity)`
- Produces: `PurchaseResult MarketSettlement.TryPurchase(ProductState product, MoneyAccount buyer, int requestedQuantity, MoneyLedger ledger)`
- Produces: `BasketPurchaseResult MarketSettlement.TryPurchaseBasket(IReadOnlyList<PurchaseRequest> requests, MoneyAccount buyer, MoneyLedger ledger, bool requireFullQuantity = false)`
- Produces: `bool MoneyLedger.TryTransferBatch(IReadOnlyList<MoneyTransferEntry> entries, string reason, long taxRevenue)`
- Produces: `int MoneyLedger.SalesTaxBasisPoints`
- Produces: `long MoneyLedger.WeeklyTaxRevenue`
- Produces: `void MoneyLedger.BeginWeek()`

- [ ] **Step 1: Write failing settlement tests**

Use a 60/40 supplier inventory, price 10, buyer balance 1000, and 10% tax. Purchasing 50 must produce buyer `-500`, treasury `+50`, sellers `+270/+180`, stock `-50`, demand `+50`, and unchanged supply. Add tests for affordability clamping, zero stock, cross-ledger seller rejection, recipient overflow, and an invalid basket; every failed case must leave all balances, inventories, demand, and weekly tax unchanged.

- [ ] **Step 2: Run settlement tests and verify red**

Expected: FAIL because `MarketSettlement` and tax-aware batch settlement do not exist.

- [ ] **Step 3: Implement purchase planning**

For each request, clamp quantity by stock and by the buyer's remaining affordable gross. Use checked `long` multiplication for `quantity * Price`. Build supplier quantity plans without mutation. Calculate tax exactly:

```csharp
long tax = checked(gross * ledger.SalesTaxBasisPoints / 10_000L);
long sellerNet = gross - tax;
```

Allocate seller net by sold quantities with `ProportionalAllocator`. Aggregate repeated account deltas, including cases where buyer, seller, or treasury are the same account. The final signed deltas must sum to zero.

- [ ] **Step 4: Commit money and inventory atomically**

When `requireFullQuantity` is true, reject the entire basket before transfer if stock or funds would clamp any request. Validate all sale plans and call the tax-aware `TryTransferBatch` overload. Only after it succeeds, commit every inventory sale and increment `LastDemand`. The overload records the supplied nonnegative tax in `WeeklyTaxRevenue` as part of the successful ledger operation. `BeginWeek` resets weekly revenue to zero. A single-product purchase delegates to the basket method with `requireFullQuantity: false`.

- [ ] **Step 5: Run focused and full tests**

Expected: all settlement arithmetic is exact and full tests pass.

- [ ] **Step 6: Commit market settlement**

```powershell
git add -- 'Assets/Scripts/Class/MarketSettlement.cs' 'Assets/Scripts/Class/MoneyLedger.cs' 'Assets/Scripts/Class/Market.cs' 'Assets/Tests/EditMode/MarketSettlementTests.cs'
git commit -m "feat: settle market purchases without changing supply"
```

---

### Task 7: Per-pop food and category consumption

**Files:**
- Modify: `Assets/Scripts/Class/Market.cs`
- Modify: `Assets/Scripts/Class/EconomicEngine.cs`
- Modify: `Assets/Scripts/Class/EconomicEngine.Production.cs`
- Create: `Assets/Tests/EditMode/EconomicConsumptionTests.cs`

**Interfaces:**
- Consumes: `MarketSettlement.TryPurchase`, `Province.ActiveLedger`, nation/local product markets.
- Produces: private `int EconomicEngine.PurchaseCategoryForPopulation(ProvinceEthnicPop pop, IReadOnlyList<string> products, int needed, IDictionary<string, ProductState> market)`
- Preserves: `ConsumeFoodsWeekly` and `ConsumeGoodsWeekly` public entry points.

- [ ] **Step 1: Write failing per-pop consumption tests**

Create two populations with balances 20 and 100, a seller with 20 food at price 10, and a 10% sales tax. Assert the first population buys at most 2 and never uses the second population's money; the second buys from its own account. Assert seller and treasury receive all spending and total supply is unchanged. Add a no-food test that calls `BuyFood(0)` and changes only living standard, not money.

- [ ] **Step 2: Run consumption tests and verify red**

Expected: FAIL because current consumption pools wealth and directly destroys buyer money.

- [ ] **Step 3: Replace pooled consumption with per-pop purchases**

Select the accessible market exactly as today: nation market when connected to capital, otherwise province market. For each population in stable province list order, calculate its own need and iterate category products ordered by descending `LastDemand`, descending `Stock`, then product name. Call `MarketSettlement.TryPurchase` until need is met, funds are exhausted, or no product can make progress. Food consumption passes the actual purchased total to `BuyFood`; other categories require no living-standard mutation in this phase.

Delete every direct `pep.property -=`, aggregate `totalMoney`, and direct market stock decrement from both economic engine files.

- [ ] **Step 4: Run focused and full tests**

Expected: per-pop affordability, seller payment, tax, and conservation tests pass; both nation and local market paths are covered.

- [ ] **Step 5: Commit conserving consumption**

```powershell
git add -- 'Assets/Scripts/Class/Market.cs' 'Assets/Scripts/Class/EconomicEngine.cs' 'Assets/Scripts/Class/EconomicEngine.Production.cs' 'Assets/Tests/EditMode/EconomicConsumptionTests.cs'
git commit -m "feat: route population consumption through market settlement"
```

---

### Task 8: Paid building inputs and owned outputs

**Files:**
- Modify: `Assets/Scripts/Class/Province.cs`
- Modify: `Assets/Scripts/Class/MarketSettlement.cs`
- Create: `Assets/Tests/EditMode/BuildingProductionEconomyTests.cs`

**Interfaces:**
- Consumes: basket purchases, `Building.Account`, and owned output registration.
- Produces: private `double Province.GetAffordableProductionScale(Building building, double requestedScale)`
- Produces: private `bool Province.TryPurchaseBuildingInputs(Building building, double scale)`

- [ ] **Step 1: Write failing building production tests**

Create an input supplier, an input product at price 5, and a building needing 2 inputs per scale. With building balance 10, requested scale 2 must clamp to scale 1, pay supplier/tax through its account, consume exactly 2 inputs, and add owned outputs under the building account. Add a basket failure case with one missing input and assert no input, money, demand, or output changes.

- [ ] **Step 2: Run tests and verify red**

Expected: FAIL because inputs are removed for free and outputs lack settlement coordination.

- [ ] **Step 3: Compute affordable scale before mutation**

Start with worker-requested scale. Clamp by every required product's `Stock / requiredAmount`. Compute one-scale gross cost as the checked sum of `requiredAmount * Price` and clamp by `building.balance / oneScaleCost`. Floor concrete input quantities only after scale is final.

- [ ] **Step 4: Purchase the full input basket and produce outputs**

Build all `PurchaseRequest` values and call `TryPurchaseBasket(requests, building.Account, ActiveLedger, requireFullQuantity: true)` once. A failed call changes no input or money. On success, add each floored output amount with `product.AddSupply(building.Account, amount)`. Remove `ConsumeBuildingInputs` direct stock mutation.

- [ ] **Step 5: Run focused and full tests**

Expected: building production is limited by workers, stock, and money; all input sellers are paid; output ownership and money supply audits pass.

- [ ] **Step 6: Commit paid production inputs**

```powershell
git add -- 'Assets/Scripts/Class/Province.cs' 'Assets/Scripts/Class/MarketSettlement.cs' 'Assets/Tests/EditMode/BuildingProductionEconomyTests.cs'
git commit -m "feat: make buildings pay for production inputs"
```

---

### Task 9: Government policy migration and legacy mutation removal

**Files:**
- Modify: `Assets/Scripts/Class/GovernmentBudget.cs`
- Modify: `Assets/Scripts/Class/Nation.cs`
- Modify: `Assets/Scripts/Class/EthnicGroup.cs`
- Modify: `Assets/Scripts/Class/Building.cs`
- Modify: `Assets/Scripts/Class/Market.cs`
- Modify: `Assets/Scripts/Manager/GameManager.cs`
- Create: `Assets/Tests/EditMode/GovernmentBudgetLedgerTests.cs`

**Interfaces:**
- Consumes: `MoneyLedger.TryMint`, `TryBurn`, `TryTransferBatch`, `BeginWeek`, and audit.
- Produces: `long GovernmentBudget.MoneySupply => nation.Ledger.MoneySupply`
- Produces: `long GovernmentBudget.WeeklyTaxRevenue => nation.Ledger.WeeklyTaxRevenue`
- Produces: `int GovernmentBudget.SalesTaxBasisPoints` forwarding to the ledger.
- Replaces: GDP-based `CollectTaxes()` with actual settlement tax only.

- [ ] **Step 1: Write failing government policy tests**

Create a nation with treasury 100, one population, and one building. Configure a policy totaling 300 across military salary, industry subsidy, and real-estate distribution. Assert `PrintMoney` increases supply by exactly 300, mints once into treasury, transfers recipient allocations from treasury, and never credits both treasury and recipients beyond the 300 issued. Assert `GDPAverage` changes and the removed weekly tax path do not alter any balance. Assert `BeginWeek` resets only weekly tax statistics.

- [ ] **Step 2: Run government tests and verify red**

Expected: FAIL because the current budget initializes independent supply, creates GDP tax money, and directly adds recipient balances.

- [ ] **Step 3: Migrate issuance and policy distributions**

Make supply and weekly tax read-only ledger views. Replace `taxRate` with `SalesTaxBasisPoints`, default `1000`. Remove `CollectTaxes` from `GameManager`. At the start of each weekly economy pass call `nation.Ledger.BeginWeek()`.

`PrintMoney` must:

1. Validate `Policy.Total` with checked arithmetic.
2. Call `TryMint(nation, nation.Account, total, "policy issuance")` once.
3. Build deterministic proportional recipient allocations for military salary, industry subsidies, and real estate.
4. Transfer each funded channel from the national account through ledger batches.
5. Increase `researchFund` for the research channel while leaving that channel's issued money in the national treasury because research points are not a monetary account.
6. Reset `Policy` only after issuance succeeds.

If a recipient category has no valid targets, its money remains in the treasury. Preserve existing living-standard effects only after the corresponding real-estate transfer succeeds.

- [ ] **Step 4: Remove every legacy balance and stock writer**

Change actor views to getter-only properties:

```csharp
public long Nation.balance => Account.Balance;
public long ProvinceEthnicPop.property => Account.Balance;
public long Building.balance => Account.Balance;
public int ProductState.Stock => Inventory.TotalQuantity;
```

Remove loading/legacy setters. Run these scans and migrate any remaining result before continuing:

```powershell
rg -n "\.(balance|property|Stock)\s*(\+=|-=|=)" Assets/Scripts --glob '*.cs'
rg -n "MoneySupply\s*(\+=|-=|=)" Assets/Scripts --glob '*.cs'
```

Expected: no gameplay assignment to actor balances, product stock, or money supply remains outside account, inventory, and ledger internals.

- [ ] **Step 5: Add weekly audit diagnostics**

After policy processing, audit every nation ledger and every neutral ledger in development/editor builds. Log an error containing ledger ID, recorded supply, and registered balance when an audit fails. Do not change balances to hide a mismatch.

- [ ] **Step 6: Run focused and full tests**

Expected: government tests pass, all direct-writer scans are empty, and the full EditMode suite has zero failures and zero compile errors.

- [ ] **Step 7: Commit government migration and hardening**

```powershell
git add -- 'Assets/Scripts/Class/GovernmentBudget.cs' 'Assets/Scripts/Class/Nation.cs' 'Assets/Scripts/Class/EthnicGroup.cs' 'Assets/Scripts/Class/Building.cs' 'Assets/Scripts/Class/Market.cs' 'Assets/Scripts/Manager/GameManager.cs' 'Assets/Tests/EditMode/GovernmentBudgetLedgerTests.cs'
git commit -m "feat: enforce ledger-only monetary mutations"
```

---

### Task 10: End-to-end conservation, documentation, and final verification

**Files:**
- Create: `Assets/Tests/EditMode/MoneyConservationIntegrationTests.cs`
- Modify: `obsedian documentry/GovernmentBudget.md`

**Interfaces:**
- Consumes: all prior monetary, construction, inventory, settlement, production, and policy interfaces.
- Produces: regression proof for a representative full domestic economy cycle.

- [ ] **Step 1: Write the integration test**

Build one nation with two populations, an input supplier, a producing building, a construction company, and market stock. Capture supply, then execute this sequence without issuance:

```text
starting-building capitalization already complete
basic and building production
province-to-nation owned-inventory transfer
population food purchase with sales tax
building input basket purchase
construction placement and completion
weekly sales-tax statistic update
GDP update
ledger audit
```

Assert final `MoneySupply` equals captured supply, `Audit` succeeds, construction escrow is zero, every aggregate stock equals owned stock, sellers received purchase proceeds, and treasury growth equals actual sales tax only.

Add a second sequence that calls one explicit mint and asserts final supply equals captured supply plus exactly the issued amount.

- [ ] **Step 2: Run integration test and verify red if any flow is missing**

Run with `-testFilter MoneyConservationIntegrationTests`.

Expected before final fixes: any uncovered non-ledger mutation fails an exact balance or audit assertion.

- [ ] **Step 3: Fix only integration gaps proven by the test**

For each failing assertion, route the identified mutation through an already defined ledger, inventory, or settlement interface. Do not introduce a second monetary path or relax equality assertions.

- [ ] **Step 4: Update the Obsidian budget document**

Document these exact user-facing facts in `GovernmentBudget.md`:

- Money supply is the sum of registered accounts, not an arbitrary starting constant.
- Normal transfers do not change supply.
- Sales tax is collected from real purchases in basis points.
- GDP is statistical and does not create tax income.
- Issuance increases supply once, then policy transfers redistribute it.
- Neutral provinces have provisional local ledgers and are absorbed 1:1.
- Employment, wages, construction fees, quality, distance, exchange, and save compatibility remain outside this completed phase.

- [ ] **Step 5: Run final automated verification**

Run the complete EditMode suite:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\ProjectNO' -runTests -testPlatform EditMode -testResults 'D:\ProjectNO\TestResults\monetary-final.xml' -logFile 'D:\ProjectNO\TestResults\monetary-final.log' -quit
```

Expected: zero failed tests, zero compilation errors, and all earlier construction tests remain green.

Run the mutation and document scans:

```powershell
rg -n "\.(balance|property|Stock)\s*(\+=|-=|=)" Assets/Scripts --glob '*.cs'
rg -n "MoneySupply\s*(\+=|-=|=)" Assets/Scripts --glob '*.cs'
git diff --check
```

Expected: mutation scans show only the controlled internals explicitly reviewed in Tasks 1, 5, and 6; `git diff --check` is silent.

- [ ] **Step 6: Record save compatibility truthfully**

Inspect `Assets/Scripts/Manager/SaveManager.cs` and verify it still does not serialize monetary accounts, escrows, and supplier inventory. Do not claim save/load compatibility in the handoff. Record this as the first requirement of a future save-format migration rather than partially serializing the new state here.

- [ ] **Step 7: Commit integration tests and documentation**

```powershell
git add -- 'Assets/Tests/EditMode/MoneyConservationIntegrationTests.cs' 'obsedian documentry/GovernmentBudget.md'
git commit -m "test: verify end-to-end money conservation"
```

---

## Completion Checklist

- [ ] Every task commit contains only its listed files plus Unity-generated `.meta` files for those files.
- [ ] `ProjectNO.slnx` remains unstaged and unchanged by this work.
- [ ] Every nation and neutral province ledger passes `Audit` after initialization and a representative week.
- [ ] Only `TryMint` and `TryBurn` change recorded money supply.
- [ ] Every market stock unit has a supplier account.
- [ ] Every successful purchase balances buyer debit, supplier credits, and tax credit exactly.
- [ ] Construction placement, cancellation, completion, and upgrades conserve money.
- [ ] Existing construction mandate behavior and UI tests remain green.
- [ ] Full EditMode test results and Unity log are retained under `TestResults` for final evidence.
