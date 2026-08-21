# Construction Mandate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace nation-direct construction with one `ConstructionMandate` object that is assigned to and progressed by a construction company, while leaving ethnic-group investment inactive but type-compatible.

**Architecture:** `Nation` creates and tracks mandates, while `ConstructionCompanyBuilding` owns slots and weekly progress. A small `BuildingFactory` guarantees that construction-company building types load and complete as the correct subtype. UI reads active mandates instead of a separate building queue, so every participant observes the same object.

**Tech Stack:** Unity 6.0, C# 9, Unity Test Framework/NUnit EditMode tests, JSON resources.

**Spec:** `obsedian documentry/건설 위임 시스템 설계.md`

## Global Constraints

- Only nation-issued mandates are activated in this phase.
- `ProvinceEthnicPop` remains an `IBuildingInvestor` but never creates a mandate automatically.
- Construction work comes only from `ConstructionCompanyBuilding`; `Nation` produces no manhours.
- One construction command creates exactly one mandate and one company assignment at most.
- Completed and cancelled mandates cannot receive more work.
- A mandate increases its target building level exactly once.
- Company slots are released on completion or cancellation.
- Missing company capacity leaves a mandate in `Requested` for a later retry.
- `Nation1` starts with one level-1 construction company in `Bebino`.
- The legacy data key `construcntionCompany` remains unchanged in this phase to avoid unrelated data migration.
- Wages, payments, material consumption, bidding, ethnic-group automation, and mandate persistence are out of scope.

---

## File Structure

- Create `Assets/Scripts/Class/ConstructionMandate.cs`: investor marker, mandate state, assignment, progress, cancellation, and one-time completion.
- Create `Assets/Scripts/Class/BuildingFactory.cs`: the only subtype-aware runtime building constructor.
- Modify `Assets/Scripts/Class/ConstructionCompany.cs`: slot ownership and weekly mandate progress.
- Modify `Assets/Scripts/Class/Nation.cs`: mandate creation, duplicate checks, company search, and weekly retry; remove direct construction labor.
- Modify `Assets/Scripts/Manager/GameManager.cs`: progress every construction company once per week.
- Modify `Assets/Scripts/GlobalVariables.cs`: load construction companies through `BuildingFactory`.
- Modify `Assets/Resources/Provinces.json`: seed a level-1 company in `Bebino`.
- Modify `Assets/Scripts/UI/Building/BuildProvinceButtonUI.cs`: place one mandate and reserve materials from active mandates.
- Modify `Assets/Scripts/UI/Building/BuildUI.cs`: list active mandates.
- Modify `Assets/Scripts/UI/Building/BuildQueueItem.cs`: display mandate target, state, and remaining manhours.
- Delete `Assets/Scripts/Class/ConstructionRequset.cs`: remove duplicate request/reservation/progress types after all callers migrate.
- Create `Assets/Tests/EditMode/ProjectNO.EditModeTests.asmdef`: isolated Unity EditMode test assembly.
- Create `Assets/Tests/EditMode/ConstructionMandateTests.cs`: reflection-based domain and orchestration regression tests.
- Modify `obsedian documentry/건설 시스템 현황.md`: mark old queue statements as superseded and link the design.

---

### Task 1: Mandate domain, building factory, and company slots

**Files:**
- Create: `Assets/Scripts/Class/ConstructionMandate.cs`
- Create: `Assets/Scripts/Class/BuildingFactory.cs`
- Modify: `Assets/Scripts/Class/ConstructionCompany.cs`
- Create: `Assets/Tests/EditMode/ProjectNO.EditModeTests.asmdef`
- Create: `Assets/Tests/EditMode/ConstructionMandateTests.cs`

**Interfaces:**
- Produces: `ConstructionMandate(IBuildingInvestor, BuildingType, Province, double)`
- Produces: `bool ConstructionMandate.TryAssign(ConstructionCompanyBuilding)`
- Produces: `double ConstructionMandate.ApplyManhours(double)`
- Produces: `bool ConstructionMandate.Cancel()`
- Produces: `Building BuildingFactory.Create(BuildingType, Province, int, long)`
- Produces: `bool ConstructionCompanyBuilding.TryAssign(ConstructionMandate)`
- Produces: `void ConstructionCompanyBuilding.ProgressWeekly(double)`

- [ ] **Step 1: Restore the EditMode test assembly**

Create `ProjectNO.EditModeTests.asmdef` with:

```json
{
  "name": "ProjectNO.EditModeTests",
  "rootNamespace": "",
  "references": [],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 2: Write failing mandate lifecycle tests**

Create `ConstructionMandateTests.cs` using reflection because the test asmdef cannot directly reference Unity's predefined `Assembly-CSharp` assembly:

```csharp
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class ConstructionMandateTests
{
    [Test]
    public void AssignedMandate_CompletesOnce_AndReleasesCompanySlot()
    {
        dynamic buildingType = New("BuildingType", "WheatField");
        dynamic province = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        dynamic companyType = New("BuildingType", "construcntionCompany");
        dynamic companyProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        dynamic company = New("ConstructionCompanyBuilding", companyType, companyProvince, 1);
        dynamic mandate = New("ConstructionMandate", null, buildingType, province, 10d);

        Assert.That(company.TryAssign(mandate), Is.True);
        Assert.That(company.TryAssign(mandate), Is.False);

        company.ProgressWeekly(10d);

        Assert.That(mandate.Status.ToString(), Is.EqualTo("Completed"));
        Assert.That((int)province.buildings[buildingType].level, Is.EqualTo(1));
        Assert.That((int)company.ActiveProjects.Count, Is.EqualTo(0));

        company.ProgressWeekly(10d);
        Assert.That((int)province.buildings[buildingType].level, Is.EqualTo(1));
    }

    [Test]
    public void CancelledMandate_RejectsFurtherProgress()
    {
        dynamic buildingType = New("BuildingType", "WheatField");
        dynamic province = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
        dynamic companyType = New("BuildingType", "construcntionCompany");
        dynamic companyProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
        dynamic company = New("ConstructionCompanyBuilding", companyType, companyProvince, 1);
        dynamic mandate = New("ConstructionMandate", null, buildingType, province, 10d);

        Assert.That(company.TryAssign(mandate), Is.True);
        Assert.That(mandate.Cancel(), Is.True);
        company.ProgressWeekly(10d);

        Assert.That(mandate.Status.ToString(), Is.EqualTo("Cancelled"));
        Assert.That((int)company.ActiveProjects.Count, Is.EqualTo(0));
        Assert.That((int)province.buildings.Count, Is.EqualTo(0));
    }

    private static dynamic New(string typeName, params object[] args) =>
        Activator.CreateInstance(Find(typeName), args);

    private static object EnumValue(string typeName, string value) =>
        Enum.Parse(Find(typeName), value);

    private static Type Find(string typeName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .First(type => type != null);
}
```

- [ ] **Step 3: Run the focused tests and verify failure**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\ProjectNO' -runTests -testPlatform EditMode -testFilter ConstructionMandateTests -testResults 'D:\ProjectNO\TestResults\construction-mandate-red.xml' -logFile 'D:\ProjectNO\TestResults\construction-mandate-red.log' -quit
```

Expected: FAIL because `ConstructionMandate` does not exist and `ConstructionCompanyBuilding.TryAssign` still accepts `BuildingReservation`.

- [ ] **Step 4: Implement the mandate lifecycle**

Create the enum and class with exact public state and guarded transitions:

```csharp
public enum ConstructionMandateStatus
{
    Requested,
    Assigned,
    InProgress,
    Completed,
    Cancelled
}

public interface IBuildingInvestor { }

public sealed class ConstructionMandate
{
    public IBuildingInvestor Investor { get; }
    public BuildingType BuildingType { get; }
    public Province TargetProvince { get; }
    public ConstructionCompanyBuilding AssignedCompany { get; private set; }
    public double RequiredManhours { get; }
    public double RemainingManhours { get; private set; }
    public ConstructionMandateStatus Status { get; private set; }
    public bool IsActive => Status != ConstructionMandateStatus.Completed &&
                            Status != ConstructionMandateStatus.Cancelled;

    public ConstructionMandate(IBuildingInvestor investor, BuildingType buildingType,
        Province targetProvince, double requiredManhours)
    {
        Investor = investor;
        BuildingType = buildingType ?? throw new System.ArgumentNullException(nameof(buildingType));
        TargetProvince = targetProvince ?? throw new System.ArgumentNullException(nameof(targetProvince));
        RequiredManhours = System.Math.Max(1d, requiredManhours);
        RemainingManhours = RequiredManhours;
        Status = ConstructionMandateStatus.Requested;
    }

    public bool TryAssign(ConstructionCompanyBuilding company)
    {
        if (company == null || Status != ConstructionMandateStatus.Requested)
            return false;
        AssignedCompany = company;
        Status = ConstructionMandateStatus.Assigned;
        return true;
    }

    public double ApplyManhours(double availableManhours)
    {
        if (availableManhours <= 0d ||
            (Status != ConstructionMandateStatus.Assigned &&
             Status != ConstructionMandateStatus.InProgress))
            return 0d;

        Status = ConstructionMandateStatus.InProgress;
        double spent = System.Math.Min(availableManhours, RemainingManhours);
        RemainingManhours -= spent;
        if (RemainingManhours <= 0d)
            Complete();
        return spent;
    }

    public bool Cancel()
    {
        if (!IsActive) return false;
        Status = ConstructionMandateStatus.Cancelled;
        return true;
    }

    private void Complete()
    {
        if (Status == ConstructionMandateStatus.Completed) return;
        if (!TargetProvince.buildings.TryGetValue(BuildingType, out Building building))
        {
            building = BuildingFactory.Create(BuildingType, TargetProvince, 0, 0);
            TargetProvince.buildings[BuildingType] = building;
        }
        building.level++;
        RemainingManhours = 0d;
        Status = ConstructionMandateStatus.Completed;
    }
}
```

- [ ] **Step 5: Implement subtype-aware building creation**

Create `BuildingFactory` and keep the misspelled legacy key isolated in one constant:

```csharp
public static class BuildingFactory
{
    public const string ConstructionCompanyTypeName = "construcntionCompany";

    public static Building Create(BuildingType type, Province province, int level = 0,
        long currentWorkers = 0)
    {
        Building building = type.name == ConstructionCompanyTypeName
            ? new ConstructionCompanyBuilding(type, province, level)
            : new Building(type, province) { level = level };
        building.currentWorkers = currentWorkers;
        return building;
    }
}
```

- [ ] **Step 6: Replace company reservation wrappers with direct mandate slots**

Use `Dictionary<int, ConstructionMandate>`, expose ordered active mandates, reject duplicate assignment, and distribute `weeklyManhoursPerLevel * level` in slot order. Remove terminal mandates from slots after every weekly pass.

```csharp
public bool TryAssign(ConstructionMandate mandate)
{
    if (mandate == null || !HasFreeSlot || _active.Values.Contains(mandate)) return false;
    int slot = Enumerable.Range(0, level).First(index => !_active.ContainsKey(index));
    if (!mandate.TryAssign(this)) return false;
    _active[slot] = mandate;
    return true;
}

public void ProgressWeekly(double weeklyManhoursPerLevel)
{
    double remaining = System.Math.Max(0d, weeklyManhoursPerLevel) * level;
    foreach (int slot in _active.Keys.OrderBy(index => index).ToList())
    {
        ConstructionMandate mandate = _active[slot];
        if (!mandate.IsActive)
        {
            _active.Remove(slot);
            continue;
        }
        remaining -= mandate.ApplyManhours(remaining);
        if (!mandate.IsActive) _active.Remove(slot);
        if (remaining <= 0d) break;
    }
}
```

- [ ] **Step 7: Run tests and verify green**

Run the Step 3 command with result names `construction-mandate-green.xml` and `construction-mandate-green.log`.

Expected: both lifecycle tests PASS.

- [ ] **Step 8: Commit the domain slice**

```powershell
git add -- 'Assets/Scripts/Class/ConstructionMandate.cs' 'Assets/Scripts/Class/BuildingFactory.cs' 'Assets/Scripts/Class/ConstructionCompany.cs' 'Assets/Tests/EditMode'
git commit -m "feat: add construction mandate lifecycle"
```

---

### Task 2: Nation issuance, assignment, and retry

**Files:**
- Modify: `Assets/Scripts/Class/Nation.cs`
- Modify: `Assets/Tests/EditMode/ConstructionMandateTests.cs`

**Interfaces:**
- Consumes: `ConstructionMandate`, `ConstructionCompanyBuilding.TryAssign`
- Produces: `IReadOnlyList<ConstructionMandate> Nation.ConstructionMandates`
- Produces: `ConstructionMandate Nation.PlaceConstructionMandate(BuildingType, Province)`
- Produces: `bool Nation.IsConstructionQueued(BuildingType, Province)`
- Produces: `void Nation.RetryPendingConstructionMandates()`

- [ ] **Step 1: Write failing nation orchestration tests**

Add tests proving that one command produces one mandate, uses the exact same instance in the company slot, rejects duplicates, and leaves an unassigned order requested. Set `GlobalVariables.BUILDING_RECIPE` and `GlobalVariables.ADJACENT_PROVINCES` through reflection before invoking `PlaceConstructionMandate`.

```csharp
[Test]
public void NationPlacesOneMandate_AndCompanyReferencesSameObject()
{
    dynamic nation = NewNation("Nation1");
    dynamic target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
    dynamic builderProvince = New("Province", 2, "Builder", EnumValue("Topography", "Plane"));
    nation.AddProvinces(target);
    nation.AddProvinces(builderProvince);
    dynamic companyType = New("BuildingType", "construcntionCompany");
    dynamic company = New("ConstructionCompanyBuilding", companyType, builderProvince, 1);
    builderProvince.buildings[companyType] = company;
    dynamic buildingType = New("BuildingType", "WheatField");
    ConfigureRecipeAndAdjacency(buildingType, target, builderProvince, 20);

    dynamic mandate = nation.PlaceConstructionMandate(buildingType, target);

    Assert.That(mandate, Is.Not.Null);
    Assert.That((int)nation.ConstructionMandates.Count, Is.EqualTo(1));
    Assert.That(ReferenceEquals(mandate, company.ActiveProjects[0]), Is.True);
    Assert.That(nation.PlaceConstructionMandate(buildingType, target), Is.Null);
}

[Test]
public void MissingCompany_LeavesMandateRequested()
{
    dynamic nation = NewNation("Nation1");
    dynamic target = New("Province", 1, "Target", EnumValue("Topography", "Plane"));
    nation.AddProvinces(target);
    dynamic buildingType = New("BuildingType", "WheatField");
    ConfigureRecipeAndAdjacency(buildingType, target, null, 20);

    dynamic mandate = nation.PlaceConstructionMandate(buildingType, target);

    Assert.That(mandate.Status.ToString(), Is.EqualTo("Requested"));
    Assert.That((int)nation.ConstructionMandates.Count, Is.EqualTo(1));
}
```

Add these reflection helpers and imports so each test constructs `List<ResearchNode>`, installs its recipe and adjacency, and clears shared dictionaries:

```csharp
using System.Collections;
using System.Collections.Generic;

[SetUp]
public void ClearConstructionGlobals()
{
    GetStaticDictionary("BUILDING_RECIPE").Clear();
    GetStaticDictionary("ADJACENT_PROVINCES").Clear();
}

private static dynamic NewNation(string name)
{
    Type researchType = Find("ResearchNode");
    object researches = Activator.CreateInstance(
        typeof(List<>).MakeGenericType(researchType));
    return Activator.CreateInstance(Find("Nation"),
        new object[] { 1, name, researches });
}

private static void ConfigureRecipeAndAdjacency(dynamic buildingType,
    dynamic target, dynamic builderProvince, int timeToBuild)
{
    dynamic recipe = New("BuildingRecipe", (string)buildingType.name);
    recipe.TimeToBuild = timeToBuild;
    GetStaticDictionary("BUILDING_RECIPE")[(string)buildingType.name] = recipe;

    IList neighbors = (IList)Activator.CreateInstance(
        typeof(List<>).MakeGenericType(Find("Province")));
    if (builderProvince != null) neighbors.Add(builderProvince);
    GetStaticDictionary("ADJACENT_PROVINCES")[(string)target.name] = neighbors;
}

private static IDictionary GetStaticDictionary(string fieldName) =>
    (IDictionary)Find("GlobalVariables")
        .GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
        .GetValue(null);
```

- [ ] **Step 2: Run tests and verify failure**

Run the focused Unity command from Task 1.

Expected: FAIL because the nation still exposes `ConstructionRequest` and `buildingsInProgress`.

- [ ] **Step 3: Replace nation-direct construction with mandate issuance**

Remove `constructionRequest`, `nationManhour`, `buildingsInProgress`, `CalculateManhour`, `ProgressBuild`, `AddToBuildQueue`, and the old queue lookup. Add a private list and read-only projection:

```csharp
private readonly List<ConstructionMandate> _constructionMandates = new();
public IReadOnlyList<ConstructionMandate> ConstructionMandates => _constructionMandates;

public ConstructionMandate PlaceConstructionMandate(BuildingType type, Province target)
{
    if (type == null || target == null || target.nation != this ||
        IsConstructionQueued(type, target) ||
        !GlobalVariables.BUILDING_RECIPE.TryGetValue(type.name, out BuildingRecipe recipe))
        return null;

    ConstructionMandate mandate = new(this, type, target,
        System.Math.Max(1, recipe.TimeToBuild));
    _constructionMandates.Add(mandate);
    TryAssignConstructionCompany(mandate);
    return mandate;
}

public bool IsConstructionQueued(BuildingType type, Province target) =>
    _constructionMandates.Any(mandate => mandate.IsActive &&
        mandate.BuildingType == type && mandate.TargetProvince == target);

public void RetryPendingConstructionMandates()
{
    foreach (ConstructionMandate mandate in _constructionMandates
        .Where(item => item.Status == ConstructionMandateStatus.Requested))
        TryAssignConstructionCompany(mandate);
}
```

`TryAssignConstructionCompany` must search the target Province first, then its adjacency list, preserve deterministic list order, restrict candidates to `candidate.nation == this`, and stop after the first successful `TryAssign(mandate)`.

`SimulateWeeklyTurn()` becomes a single call to `RetryPendingConstructionMandates()`.

- [ ] **Step 4: Run tests and verify green**

Run the focused Unity command.

Expected: all mandate and nation orchestration tests PASS.

- [ ] **Step 5: Commit nation issuance**

```powershell
git add -- 'Assets/Scripts/Class/Nation.cs' 'Assets/Tests/EditMode/ConstructionMandateTests.cs'
git commit -m "feat: route nation construction through mandates"
```

---

### Task 3: Weekly company scheduling and initial company bootstrap

**Files:**
- Modify: `Assets/Scripts/Manager/GameManager.cs`
- Modify: `Assets/Scripts/GlobalVariables.cs`
- Modify: `Assets/Resources/Provinces.json`
- Modify: `Assets/Tests/EditMode/ConstructionMandateTests.cs`

**Interfaces:**
- Consumes: `BuildingFactory.Create`, `ConstructionCompanyBuilding.ProgressWeekly`
- Produces: one weekly company progress call per loaded company.

- [ ] **Step 1: Write failing factory and bootstrap tests**

Add a factory test:

```csharp
[Test]
public void BuildingFactory_CreatesConstructionCompanySubtype()
{
    dynamic type = New("BuildingType", "construcntionCompany");
    dynamic province = New("Province", 1, "Bebino", EnumValue("Topography", "Plane"));
    dynamic factoryType = Find("BuildingFactory");
    dynamic building = factoryType.GetMethod("Create").Invoke(null,
        new object[] { type, province, 1, 0L });

    Assert.That(building.GetType().Name, Is.EqualTo("ConstructionCompanyBuilding"));
    Assert.That((int)building.level, Is.EqualTo(1));
}
```

Add an EditMode test that reads `Assets/Resources/Provinces.json`, finds `Bebino`, and asserts exactly one level-1 company entry:

```csharp
[Test]
public void ProvinceData_SeedsOneLevelOneCompanyInBebino()
{
    string path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
        "Resources", "Provinces.json");
    ProvinceJson wrapper = UnityEngine.JsonUtility.FromJson<ProvinceJson>(
        System.IO.File.ReadAllText(path));
    ProvinceJsonData bebino = wrapper.provinces.Single(item => item.name == "Bebino");

    Assert.That(bebino.buildings.Count(item =>
        item.buildingTypeName == "construcntionCompany" && item.level == 1),
        Is.EqualTo(1));
}

[Serializable]
private sealed class ProvinceJson
{
    public ProvinceJsonData[] provinces;
}

[Serializable]
private sealed class ProvinceJsonData
{
    public string name;
    public BuildingJsonData[] buildings;
}

[Serializable]
private sealed class BuildingJsonData
{
    public string buildingTypeName;
    public int level;
}
```

- [ ] **Step 2: Run tests and verify the bootstrap test fails**

Run the focused Unity command.

Expected: factory test passes after Task 1; JSON bootstrap test FAILS because Bebino has no construction company.

- [ ] **Step 3: Route initial building loading through the factory**

In `GlobalVariables.LoadProvinces()`, replace direct `new Building(...)` construction with:

```csharp
long workers = (long)(buildingType.workerNeeded * building.workerScale);
Building loadedBuilding = BuildingFactory.Create(
    buildingType, province, building.level, workers);
buildings.Add(buildingType, loadedBuilding);
```

- [ ] **Step 4: Seed Bebino's construction company**

Append this object to Bebino's existing `buildings` array:

```json
{
  "buildingTypeName": "construcntionCompany",
  "workerScale": 0.0,
  "level": 1
}
```

The zero worker scale is intentional because employment-driven company output is outside this phase; temporary construction capacity comes from the fixed weekly company value.

- [ ] **Step 5: Move weekly progress to companies**

Rename `GlobalVariables.minimumNationManHour` to `minimumConstructionCompanyManHour` while preserving its value `10d`.

In `GameManager.ProcessWeeklyEvents()`, after every nation retries pending assignments, enumerate each Province's `buildings.Values.OfType<ConstructionCompanyBuilding>()` exactly once and call:

```csharp
company.ProgressWeekly(GlobalVariables.minimumConstructionCompanyManHour);
```

Do not call progress from `Nation`; this prevents a company serving multiple investors from receiving duplicate weekly turns.

- [ ] **Step 6: Run tests and compile**

Run the focused Unity tests, then:

```powershell
dotnet build 'D:\ProjectNO\Assembly-CSharp.csproj' --no-restore
```

Expected: all focused tests PASS and build exits with code 0.

- [ ] **Step 7: Commit bootstrap and scheduling**

```powershell
git add -- 'Assets/Scripts/Manager/GameManager.cs' 'Assets/Scripts/GlobalVariables.cs' 'Assets/Resources/Provinces.json' 'Assets/Tests/EditMode/ConstructionMandateTests.cs'
git commit -m "feat: bootstrap and progress construction companies"
```

---

### Task 4: Build UI and material reservation migration

**Files:**
- Modify: `Assets/Scripts/UI/Building/BuildProvinceButtonUI.cs`
- Modify: `Assets/Scripts/UI/Building/BuildUI.cs`
- Modify: `Assets/Scripts/UI/Building/BuildQueueItem.cs`

**Interfaces:**
- Consumes: `Nation.PlaceConstructionMandate`, `Nation.IsConstructionQueued`, `Nation.ConstructionMandates`
- Produces: UI queue items backed directly by `ConstructionMandate`.

- [ ] **Step 1: Migrate build eligibility and reserved-material lookup**

Replace calls to `IsInBuildQueue` with `IsConstructionQueued`. Calculate reserved materials from:

```csharp
nation.ConstructionMandates
    .Where(mandate => mandate.IsActive)
    .Where(mandate => GetAccessibleProducts(mandate.TargetProvince) == products)
    .Where(mandate => GlobalVariables.BUILDING_RECIPE.ContainsKey(mandate.BuildingType.name))
```

Use each matching recipe's `requireItems` exactly as the existing reservation calculation does.

- [ ] **Step 2: Replace the click handler with one issuance call**

Remove premature `Building` creation, `AddBuildingReservation`, `AddToBuildQueue`, and `AssignAdjacentConstructionCompany`. The successful path becomes:

```csharp
ConstructionMandate mandate = nation.PlaceConstructionMandate(buildingType, provinceData);
if (mandate == null)
{
    Debug.LogWarning($"[ConstructionMandate] Could not issue {buildingType.name} in {provinceData.name}.");
    UpdateBuildButtonState();
    return;
}

BuildUI.Instance.UpdateQueue();
UpdateBuildButtonState();
```

- [ ] **Step 3: Bind queue UI to mandates**

In `BuildUI.UpdateQueue()`, use:

```csharp
List<ConstructionMandate> queuedMandates = currentNation.ConstructionMandates
    .Where(mandate => mandate.IsActive)
    .ToList();
```

Change `BuildQueueItem.SetBuildingData(Building)` to `SetMandate(ConstructionMandate)`. Display `BuildingType.name`, `TargetProvince.name`, `RemainingManhours`, and `Status`; guard `UpdateManhour()` when no mandate has been assigned to the component.

- [ ] **Step 4: Compile and inspect call-site removal**

Run:

```powershell
dotnet build 'D:\ProjectNO\Assembly-CSharp.csproj' --no-restore
rg -n "AddBuildingReservation|AddToBuildQueue|AssignAdjacentConstructionCompany|buildingsInProgress|BuildingReservation|BuildingInProgress" Assets/Scripts
```

Expected: build exits 0. Search results may only remain in `ConstructionRequset.cs`, which Task 5 deletes.

- [ ] **Step 5: Commit UI migration**

```powershell
git add -- 'Assets/Scripts/UI/Building/BuildProvinceButtonUI.cs' 'Assets/Scripts/UI/Building/BuildUI.cs' 'Assets/Scripts/UI/Building/BuildQueueItem.cs'
git commit -m "feat: display construction mandates in build UI"
```

---

### Task 5: Remove legacy request types, update documentation, and verify

**Files:**
- Delete: `Assets/Scripts/Class/ConstructionRequset.cs`
- Delete: `Assets/Scripts/Class/ConstructionRequset.cs.meta`
- Modify: `obsedian documentry/건설 시스템 현황.md`
- Verify: `obsedian documentry/건설 위임 시스템 설계.md`

**Interfaces:**
- Consumes: all mandate interfaces from Tasks 1–4.
- Produces: no legacy parallel construction path.

- [ ] **Step 1: Delete the obsolete construction request source and metadata**

Remove `ConstructionRequest`, `BuildingReservation`, and `BuildingInProgress` by deleting their source file and `.meta` after search confirms no callers outside that file.

- [ ] **Step 2: Update the construction status document**

Add a dated notice at the top of `건설 시스템 현황.md` linking `[[건설 위임 시스템 설계]]` and stating:

```markdown
> 2026-08-22 개편: 국가 직접 건설 큐는 `ConstructionMandate`와 건설회사 진행 방식으로 교체되었다. 아래의 기존 구조 분석은 변경 배경을 설명하기 위한 기록이며, 현재 동작은 [[건설 위임 시스템 설계]]를 기준으로 한다.
```

- [ ] **Step 3: Run placeholder and legacy scans**

Run:

```powershell
rg -n "ConstructionRequest|BuildingReservation|BuildingInProgress|nationManhour|minimumNationManHour|buildingsInProgress|AddToBuildQueue|ProgressBuild" Assets/Scripts
rg -n "ProvinceEthnicPop.*PlaceConstructionMandate|PlaceConstructionMandate.*ProvinceEthnicPop" Assets/Scripts
```

Expected: both commands return no matches. The second scan demonstrates that ethnic-group automatic issuance remains inactive.

- [ ] **Step 4: Run complete verification**

Run the entire EditMode suite:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\ProjectNO' -runTests -testPlatform EditMode -testResults 'D:\ProjectNO\TestResults\editmode-final.xml' -logFile 'D:\ProjectNO\TestResults\editmode-final.log' -quit
dotnet build 'D:\ProjectNO\Assembly-CSharp.csproj' --no-restore
git diff --check
git status --short
```

Expected: Unity reports all tests passed, C# build exits 0, `git diff --check` prints nothing, and status contains only the planned source, data, test, and documentation changes.

- [ ] **Step 5: Perform a Play Mode smoke check**

Open `PlayScene` and verify this exact scenario:

1. Start a new `Nation1` game.
2. Confirm Bebino's `construcntionCompany` is a `ConstructionCompanyBuilding` at level 1.
3. Issue one construction order in Bebino or Eiglepsk.
4. Confirm exactly one queue item appears and its state changes from `Assigned` to `InProgress` after a weekly tick.
5. Advance until remaining manhours reaches zero.
6. Confirm the target building level increases once and the queue item disappears.
7. Fill the only company slot, issue another eligible order, and confirm the second mandate remains `Requested` until the slot is released.

- [ ] **Step 6: Commit cleanup and documentation**

```powershell
git add -A -- 'Assets/Scripts/Class/ConstructionRequset.cs' 'Assets/Scripts/Class/ConstructionRequset.cs.meta' 'obsedian documentry/건설 시스템 현황.md' 'obsedian documentry/건설 위임 시스템 설계.md'
git commit -m "docs: finalize construction mandate migration"
```

---

## Self-Review Results

- Spec coverage: nation issuance, single object identity, assignment, retry, company-owned progress, one-time completion, slot release, UI migration, initial Bebino company, and inactive ethnic investment each map to an explicit task and test or scan.
- Scope exclusions: wages, payment, material consumption, bidding, profitability, ethnic automation, and persistence are not introduced by any task.
- Placeholder scan: the plan contains no deferred implementation markers; every production change names an exact type, method, state transition, or data object.
- Type consistency: all callers use `ConstructionMandate`, `PlaceConstructionMandate`, `IsConstructionQueued`, `TryAssign`, `ProgressWeekly`, and `BuildingFactory.Create` with the signatures defined in Tasks 1–3.
- Scheduling safety: construction companies advance in `GameManager` once per week, not once per investor or mandate.
- Bootstrap safety: Bebino receives the legacy-key company data, and `BuildingFactory` is used both when loading and when completing new construction-company mandates.
