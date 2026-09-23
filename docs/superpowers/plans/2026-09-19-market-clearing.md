# Unified Market Clearing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 주민·공장·건설의 돈으로 뒷받침된 주문을 기존 국가/지역 시장에서 단일가격으로 함께 청산하고, 실제 주문 압력으로 다음 주 가격을 갱신한다.

**Architecture:** `MarketAccess`가 기존 국가 시장과 지역 시장 선택을 단일화하고, 도메인별 주문 생산기가 부작용 없는 `MarketOrder`를 만든다. `MarketClearingEngine`은 주문을 결정적으로 배분한 계획을 만들고 `MarketSettlement`가 명시적 청산가격으로 원자 정산한 뒤 준비된 수령 작업과 가격 통계를 확정한다. 공장은 희소 입력 재고를 보유해 이번 주에 산 원재료를 다음 주 생산에 사용한다.

**Tech Stack:** Unity 6000.0.71f1, C#, NUnit EditMode, Unity `JsonUtility`, 기존 reflection 테스트 어셈블리.

**Spec:** `docs/superpowers/specs/2026-09-19-market-clearing-design.md`

## Global Constraints

- 범위는 가격 갱신, 시장 접근 통일, 주민·공장·건설 주문 동시 청산으로 제한한다.
- 품질, 거리, 운송, 필수품 우선권, 국제무역, 배당과 민간 자동 투자를 추가하지 않는다.
- 식량도 시스템 우선권 없이 구매력과 최대입찰가로 경쟁한다.
- 거래는 `MoneyLedger` 안의 등록 계좌 사이에서만 이뤄지고 총화폐량을 바꾸지 않는다.
- 판매대금은 실제 lot 소유자에게 지급하고 판매세는 같은 원장의 국고로 이동한다.
- 기존 `TryPurchase`, `TryPurchaseBasket`과 생산자별 lot 규칙을 보존한다.
- `LastSupply`는 신규 공급량, `LastDemand`는 실제 판매량 의미를 유지한다.
- 가격 평활 비중 기본값은 3000 basis points, 주간 최대 변동 기본값은 2500 basis points, 최소 가격은 1이다.
- 연결 프로빈스는 기존 `NationMarket`, 고립 프로빈스는 기존 `ProvinceMarket`을 사용한다.
- `ConstructionCompanyBuilding`은 일반 공장 원재료 주문에서 제외한다.
- 주간 결과는 주문 열거 순서와 무관하고 안정적인 ID로 정수 나머지를 배분한다.
- 구조적 검증 실패는 해당 시장 정산을 무변경으로 거부하며 다른 독립 시장은 계속 처리한다.
- 저장 형식은 버전 2로 올리고 버전 1을 명시된 기본값으로 마이그레이션한다.
- 사용자 소유의 기존 작업 트리 변경과 `TestResults/`는 작업 커밋에 포함하지 않는다.
- 실행 시작 시 `superpowers:using-git-worktrees`로 별도 작업 트리를 만들고, 열린 Unity 프로젝트와 같은 경로에 배치 테스트를 실행하지 않는다.

## 공통 Unity 테스트 명령

실행 세션을 시작할 때 다음 PowerShell 함수를 한 번 정의하고 각 Task에 적힌 실제 인자로 호출한다.

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.0.71f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path
function Invoke-UnityEditMode([string]$testFilter, [string]$resultName) {
    $xmlPath = Join-Path $projectRoot ("TestResults\{0}.xml" -f $resultName)
    $logPath = Join-Path $projectRoot ("TestResults\{0}.log" -f $resultName)
    New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'TestResults') | Out-Null
    $arguments = @('-batchmode','-nographics','-projectPath',$projectRoot,'-runTests','-testPlatform','EditMode')
    if (![string]::IsNullOrWhiteSpace($testFilter)) { $arguments += @('-testFilter',$testFilter) }
    $arguments += @('-testResults',$xmlPath,'-logFile',$logPath)
    $process = Start-Process -FilePath $unityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode)" }
    [xml]$result = Get-Content -LiteralPath $xmlPath
    $run = $result.'test-run'
    $run | Select-Object result,total,passed,failed,skipped
    if ($run.failed -ne '0' -or $run.result -ne 'Passed') { throw "Unity tests failed: $resultName" }
}
```

전체 검증은 `Invoke-UnityEditMode -testFilter '' -resultName 'market-clearing-full'`로 실행한다.

---

### Task 1: 가격 상태와 순수 가격 계산기

**Files:**
- Create: `Assets/Scripts/Class/MarketPricing.cs`
- Create: `Assets/Scripts/Class/MarketPricing.cs.meta`
- Modify: `Assets/Scripts/Class/Market.cs:103-121`
- Modify: `Assets/Scripts/GlobalVariables.cs:1-320, 800-880`
- Modify: `Assets/Resources/GlobalVariables.json`
- Test: `Assets/Tests/EditMode/MarketPriceTests.cs`
- Test: `Assets/Tests/EditMode/MarketPriceTests.cs.meta`

**Interfaces:**
- Produces: `MarketPriceSettings(int smoothingBasisPoints, int maxWeeklyChangeBasisPoints)`.
- Produces: `MarketPriceCalculator.CalculateNextPrice(int previousPrice, int requestedDemand, int availableStock, float elasticity, MarketPriceSettings settings) : int`.
- Produces on `ProductState`: `RequestedDemand`, `UnmetDemand`, `LastClearingPrice`, `BeginWeek()`, `CommitClearingStatistics(int requested, int sold, int available, int clearingPrice, MarketPriceSettings settings)`.
- Preserves: `LastDemand` continues to be incremented only by actual inventory commits.

- [ ] **Step 1: Write failing price tests**

Create `MarketPriceTests.cs` with reflection tests covering exact defaults and boundaries:

```csharp
[TestCase(100, 0, 0, 100)]
[TestCase(100, 100, 0, 125)]
[TestCase(100, 0, 100, 75)]
public void CalculateNextPrice_HandlesEmptyAndOneSidedMarkets(
    int previous, int demand, int stock, int expected)
{
    object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
    MethodInfo method = ReflectionTestHelpers.Find("MarketPriceCalculator")
        .GetMethod("CalculateNextPrice");
    Assert.That(method.Invoke(null, new object[] { previous, demand, stock, 1f, settings }),
        Is.EqualTo(expected));
}

[Test]
public void ProductState_CommitClearingStatistics_PreservesActualDemandMeaning()
{
    object product = ReflectionTestHelpers.New("ProductState", "Iron", 100);
    ReflectionTestHelpers.Set(product, "LastDemand", 4);
    object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
    ReflectionTestHelpers.Call<object>(product, "CommitClearingStatistics", 10, 4, 4, 100, settings);
    Assert.That(ReflectionTestHelpers.Get(product, "LastDemand"), Is.EqualTo(4));
    Assert.That(ReflectionTestHelpers.Get(product, "RequestedDemand"), Is.EqualTo(10));
    Assert.That(ReflectionTestHelpers.Get(product, "UnmetDemand"), Is.EqualTo(6));
}
```

Also test constructor validation, price minimum 1, integer-division avoidance, NaN/infinite elasticity rejection, `BeginWeek()` reset, and ±25% clamp.

- [ ] **Step 2: Run the price tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'MarketPriceTests' -resultName 'task1-price-red'`.

Expected: FAIL because `MarketPriceSettings`, `MarketPriceCalculator` and new `ProductState` members do not exist.

- [ ] **Step 3: Implement settings loading and pure price calculation**

Implement the public contracts without Unity scene dependencies:

```csharp
public sealed class MarketPriceSettings
{
    public int SmoothingBasisPoints { get; }
    public int MaxWeeklyChangeBasisPoints { get; }
    public MarketPriceSettings(int smoothingBasisPoints, int maxWeeklyChangeBasisPoints)
    {
        if (smoothingBasisPoints < 0 || smoothingBasisPoints > 10_000)
            throw new ArgumentOutOfRangeException(nameof(smoothingBasisPoints));
        if (maxWeeklyChangeBasisPoints < 1 || maxWeeklyChangeBasisPoints > 10_000)
            throw new ArgumentOutOfRangeException(nameof(maxWeeklyChangeBasisPoints));
        SmoothingBasisPoints = smoothingBasisPoints;
        MaxWeeklyChangeBasisPoints = maxWeeklyChangeBasisPoints;
    }
}

public static class MarketPriceCalculator
{
    public static int CalculateNextPrice(int previousPrice, int requestedDemand,
        int availableStock, float elasticity, MarketPriceSettings settings)
    {
        if (previousPrice < 1 || requestedDemand < 0 || availableStock < 0 ||
            float.IsNaN(elasticity) || float.IsInfinity(elasticity) || elasticity < 0f)
            throw new ArgumentOutOfRangeException();
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (requestedDemand == 0 && availableStock == 0) return previousPrice;
        double ratio = ((double)requestedDemand + 1d) / (availableStock + 1d);
        double pressure = previousPrice * Math.Pow(ratio, elasticity);
        double smooth = settings.SmoothingBasisPoints / 10_000d;
        double candidate = previousPrice * (1d - smooth) + pressure * smooth;
        int lower = Math.Max(1, (int)Math.Floor(previousPrice *
            (1d - settings.MaxWeeklyChangeBasisPoints / 10_000d)));
        int upper = Math.Max(1, (int)Math.Ceiling(previousPrice *
            (1d + settings.MaxWeeklyChangeBasisPoints / 10_000d)));
        if (double.IsNaN(candidate) || double.IsInfinity(candidate))
            throw new OverflowException();
        candidate = Math.Max(lower, Math.Min(upper, candidate));
        return Math.Max(1, checked((int)Math.Round(candidate, MidpointRounding.AwayFromZero)));
    }
}
```

Add optional JSON data with defaults:

```json
"marketSettings": {
  "smoothingBasisPoints": 3000,
  "maxWeeklyPriceChangeBasisPoints": 2500
}
```

Expose the validated loaded value as `GlobalVariables.MARKET_PRICE_SETTINGS`.

- [ ] **Step 4: Add ProductState weekly statistics**

`BeginWeek()` must set `LastPrice = Price` and reset `LastSupply`, `LastDemand`, `RequestedDemand`, `UnmetDemand`, and `LastClearingPrice`. `CommitClearingStatistics` validates `sold == LastDemand`, sets requested/unmet/clearing fields, and calculates the next `Price` without changing inventory or money.

- [ ] **Step 5: Run Task 1 tests and existing market inventory tests**

Run: `Invoke-UnityEditMode -testFilter 'MarketPriceTests' -resultName 'task1-price-green'`, then `Invoke-UnityEditMode -testFilter 'MarketInventoryTests' -resultName 'task1-inventory-regression'`.

Expected: both runs PASS with zero failures.

- [ ] **Step 6: Commit Task 1**

```powershell
git add -- Assets/Scripts/Class/MarketPricing.cs Assets/Scripts/Class/MarketPricing.cs.meta Assets/Scripts/Class/Market.cs Assets/Scripts/GlobalVariables.cs Assets/Resources/GlobalVariables.json Assets/Tests/EditMode/MarketPriceTests.cs Assets/Tests/EditMode/MarketPriceTests.cs.meta
git commit -m "feat: track weekly market price pressure"
```

### Task 2: 공통 시장 접근 정책

**Files:**
- Create: `Assets/Scripts/Class/MarketAccess.cs`
- Create: `Assets/Scripts/Class/MarketAccess.cs.meta`
- Modify: `Assets/Scripts/Class/ConstructionMandate.cs:100-180`
- Test: `Assets/Tests/EditMode/MarketAccessTests.cs`
- Test: `Assets/Tests/EditMode/MarketAccessTests.cs.meta`

**Interfaces:**
- Produces: `MarketAccessContext(string stableId, Dictionary<string, ProductState> products, MoneyLedger ledger)`.
- Produces: `MarketAccess.TryResolve(Province province, out MarketAccessContext context) : bool`.
- Rule: connected owned province resolves to `nation.market.Products`; isolated or neutral province resolves to `province.market.Products`.

- [ ] **Step 1: Write failing access tests**

```csharp
[Test]
public void TryResolve_ConnectedProvinceUsesNationMarketAndActiveLedger()
{
    // Build nation + province, set isConnectedToCapital=true and distinct markets.
    // Assert Products is nation.market.Products, Ledger is province.ActiveLedger,
    // and StableId is "nation:" + nation.name.
}

[Test]
public void TryResolve_IsolatedProvinceUsesLocalMarket()
{
    // Set isConnectedToCapital=false and assert ProvinceMarket identity and
    // StableId "province:" + province.name.
}
```

Add invalid cases for null province, missing market, missing ledger, and connected province without nation.

- [ ] **Step 2: Run access tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'MarketAccessTests' -resultName 'task2-access-red'`.

Expected: FAIL because the access types do not exist.

- [ ] **Step 3: Implement MarketAccess and route construction through it**

```csharp
public readonly struct MarketAccessContext
{
    public string StableId { get; }
    public Dictionary<string, ProductState> Products { get; }
    public MoneyLedger Ledger { get; }
}

public static class MarketAccess
{
    public static bool TryResolve(Province province, out MarketAccessContext context)
    {
        context = default;
        if (province?.ActiveLedger == null) return false;
        if (province.isConnectedToCapital)
        {
            if (province.nation?.market?.Products == null) return false;
            context = new MarketAccessContext("nation:" + province.nation.name,
                province.nation.market.Products, province.ActiveLedger);
            return true;
        }
        if (province.market?.Products == null) return false;
        context = new MarketAccessContext("province:" + province.name,
            province.market.Products, province.ActiveLedger);
        return true;
    }
}
```

Make `ConstructionMandate.GetAccessibleProducts()` call this policy and return null on failure. Do not yet change resident or factory weekly behavior.

- [ ] **Step 4: Run access and construction contract tests**

Run: `Invoke-UnityEditMode -testFilter 'MarketAccessTests' -resultName 'task2-access-green'`, then `Invoke-UnityEditMode -testFilter 'ConstructionContractTests' -resultName 'task2-construction-regression'`.

Expected: both PASS.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- Assets/Scripts/Class/MarketAccess.cs Assets/Scripts/Class/MarketAccess.cs.meta Assets/Scripts/Class/ConstructionMandate.cs Assets/Tests/EditMode/MarketAccessTests.cs Assets/Tests/EditMode/MarketAccessTests.cs.meta
git commit -m "refactor: centralize market access selection"
```

### Task 3: 부작용 없는 단일가격 경매 계획

**Files:**
- Create: `Assets/Scripts/Class/MarketOrder.cs`
- Create: `Assets/Scripts/Class/MarketOrder.cs.meta`
- Create: `Assets/Scripts/Class/MarketClearingEngine.cs`
- Create: `Assets/Scripts/Class/MarketClearingEngine.cs.meta`
- Test: `Assets/Tests/EditMode/MarketClearingTests.cs`
- Test: `Assets/Tests/EditMode/MarketClearingTests.cs.meta`

**Interfaces:**
- Produces: `MarketOrder(string id, MoneyAccount buyer, ProductState product, int quantity, int maximumUnitPrice, long reservedBudget, int minimumFill, IMarketOrderRecipient recipient)`.
- Produces: `IMarketOrderRecipient.Id` and `TryPrepareReceipt(IReadOnlyList<MarketOrderFill> fills, out IPreparedMarketReceipt receipt) : bool`.
- Produces: `IPreparedMarketReceipt.Commit() : void` where `Commit` is guaranteed not to throw after successful preparation.
- Produces: `MarketClearingEngine.TryPlan(IReadOnlyList<MarketOrder> orders, IReadOnlyCollection<ProductState> marketProducts, MoneyLedger ledger, MarketPriceSettings settings, out MarketClearingPlan plan, out string error) : bool`.
- Produces on plan: `Purchases`, `Fills`, `ProductResults`, and `PrepareReceipts(IEnumerable<IMarketOrderRecipient> additionalRecipients, out IReadOnlyList<IPreparedMarketReceipt>)`. Additional recipients receive an empty fill list and support zero-food outcomes without invalid zero-quantity orders.

- [ ] **Step 1: Write failing auction tests**

Cover full supply, high-bid precedence, marginal-price ties, deterministic remainder, minimum-fill redistribution, duplicate IDs, aggregate reservations, cross-ledger buyers, overflow, and input-order invariance.

```csharp
[Test]
public void TryPlan_ShortageUsesMarginalBidAndProRatesBoundaryTie()
{
    // Stock 10. A bids 15 for 6, B and C bid 12 for 6 each.
    // Assert A receives 6, B+C share 4 deterministically, clearing price is 12,
    // and planning changes no account, inventory, demand, or price.
}

[Test]
public void TryPlan_AggregateReservationAboveBuyerBalanceFailsWithoutMutation()
{
    // Same buyer submits two individually valid orders whose reservations exceed balance.
    // Assert false, error identifies reserved budget, and every snapshot is unchanged.
}
```

- [ ] **Step 2: Run clearing tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'MarketClearingTests' -resultName 'task3-clearing-red'`.

Expected: FAIL because the order and clearing types do not exist.

- [ ] **Step 3: Implement immutable order/result types**

Constructors must reject blank IDs, null references, nonpositive values, `minimumFill > quantity`, multiplication overflow, and `quantity * maximumUnitPrice > reservedBudget`. Results expose read-only collections.

```csharp
public sealed class MarketOrderFill
{
    public MarketOrder Order { get; }
    public int Quantity { get; }
    public int UnitPrice { get; }
    public long GrossAmount => checked((long)Quantity * UnitPrice);
}
```

- [ ] **Step 4: Implement deterministic planning**

Validate that every order product occurs exactly once in `marketProducts`. Group by `ProductState`, capture every market product's stock including products with no orders, filter bids below `Product.Price`, skip sorting when supply covers eligible demand, otherwise sort by descending maximum price then ordinal ID. Find the marginal price, allocate higher bids fully, and use `ProportionalAllocator` plus ordinal IDs for the boundary group. Re-run boundary allocation after dropping fills below `MinimumFill`.

Planning must not call `CommitSale`, transfer money, alter statistics, or invoke receipts.

- [ ] **Step 5: Run clearing tests and determinism repetition**

Run `Invoke-UnityEditMode -testFilter 'MarketClearingTests' -resultName 'task3-clearing-green-a'` and `Invoke-UnityEditMode -testFilter 'MarketClearingTests' -resultName 'task3-clearing-green-b'`, then compare both XML totals.

Expected: all tests PASS with identical counts.

- [ ] **Step 6: Commit Task 3**

```powershell
git add -- Assets/Scripts/Class/MarketOrder.cs Assets/Scripts/Class/MarketOrder.cs.meta Assets/Scripts/Class/MarketClearingEngine.cs Assets/Scripts/Class/MarketClearingEngine.cs.meta Assets/Tests/EditMode/MarketClearingTests.cs Assets/Tests/EditMode/MarketClearingTests.cs.meta
git commit -m "feat: plan deterministic uniform-price auctions"
```

### Task 4: 명시적 단일가격 원자 정산

**Files:**
- Modify: `Assets/Scripts/Class/MarketBatchPurchase.cs:3-25`
- Modify: `Assets/Scripts/Class/MarketSettlement.cs:74-390`
- Modify: `Assets/Scripts/Class/MarketClearingEngine.cs`
- Modify: `Assets/Tests/EditMode/MarketBatchPurchaseTests.cs`
- Modify: `Assets/Tests/EditMode/MarketSettlementTests.cs`
- Modify: `Assets/Tests/EditMode/MarketClearingTests.cs`

**Interfaces:**
- Extends: `MarketBuyerRequest(..., int quantity, int unitPrice)` and preserves the four-argument constructor using `product.Price`.
- Produces: `MarketBuyerRequest.UnitPrice`.
- Produces: `MarketClearingPlan.TrySettle(MoneyLedger ledger, IEnumerable<IMarketOrderRecipient> additionalRecipients, out string error) : bool`.
- Constraint: all requests for the same `ProductState` in one batch must carry one identical unit price.

- [ ] **Step 1: Add failing explicit-price settlement tests**

```csharp
[Test]
public void TryPurchaseBatch_UsesUniformExplicitPriceForBuyerSellerAndTax()
{
    // Product reference price 10, two explicit requests at clearing price 12,
    // sales tax 10%. Assert buyer debits 12 each, seller total 22, treasury 2,
    // stock -2, demand +2, and money supply unchanged.
}

[Test]
public void TryPurchaseBatch_MixedPricesForOneProductChangeNothing()
{
    // Same product requests at 11 and 12. Assert false and full snapshot unchanged.
}
```

- [ ] **Step 2: Run batch/settlement tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'MarketBatchPurchaseTests' -resultName 'task4-batch-red'` and `Invoke-UnityEditMode -testFilter 'MarketSettlementTests' -resultName 'task4-settlement-red'`.

Expected: new constructor/property tests FAIL.

- [ ] **Step 3: Thread explicit price through settlement planning**

Change `TryPlanProductSale` to accept `int unitPrice`; calculate gross and tax from that value. Legacy `TryPurchase` and `TryPurchaseBasket` pass `Product.Price`. Batch settlement validates one price per product before building any deltas.

- [ ] **Step 4: Make the clearing plan settle and commit in strict order**

`TrySettle` must:

```csharp
// 1. Prepare all recipient receipts from the final fills.
// 2. If Purchases is nonempty, call MarketSettlement.TryPurchaseBatch(Purchases, ledger).
//    An empty purchase list is a valid no-trade market result and skips settlement.
// 3. Commit every prepared receipt (non-throwing contract).
// 4. Commit RequestedDemand/UnmetDemand/LastClearingPrice/next Price.
// 5. Return true. Any failure before step 2 returns false with no mutation.
```

If receipt preparation fails, do not call settlement. Do not catch exceptions from `Commit`; implementations must be prevalidated and tests must enforce that contract.

- [ ] **Step 5: Run market settlement and clearing regressions**

Run `Invoke-UnityEditMode` for `MarketBatchPurchaseTests/task4-batch-green`, `MarketSettlementTests/task4-settlement-green`, and `MarketClearingTests/task4-clearing-green`.

Expected: all PASS, including all legacy price-path tests.

- [ ] **Step 6: Commit Task 4**

```powershell
git add -- Assets/Scripts/Class/MarketBatchPurchase.cs Assets/Scripts/Class/MarketSettlement.cs Assets/Scripts/Class/MarketClearingEngine.cs Assets/Tests/EditMode/MarketBatchPurchaseTests.cs Assets/Tests/EditMode/MarketSettlementTests.cs Assets/Tests/EditMode/MarketClearingTests.cs
git commit -m "feat: settle market batches at explicit clearing prices"
```

### Task 5: 공장 입력 재고와 보유 재고 생산

**Files:**
- Create: `Assets/Scripts/Class/BuildingInputInventory.cs`
- Create: `Assets/Scripts/Class/BuildingInputInventory.cs.meta`
- Modify: `Assets/Scripts/Class/Building.cs:26-127` (make `Building` partial and expose read-only input quantities)
- Modify: `Assets/Scripts/Class/Province.cs:184-360`
- Modify: `Assets/Tests/EditMode/BuildingProductionEconomyTests.cs`
- Test: `Assets/Tests/EditMode/BuildingInputInventoryTests.cs`
- Test: `Assets/Tests/EditMode/BuildingInputInventoryTests.cs.meta`

**Interfaces:**
- Produces on `Building`: `IReadOnlyDictionary<string,long> InputInventory`.
- Produces: `Building.TryPrepareInputReceipt(IReadOnlyDictionary<string,long> quantities, out IPreparedMarketReceipt receipt) : bool`.
- Produces internal restore hook: `Building.RestoreInputInventory(IReadOnlyDictionary<string,long> quantities)`.
- Changes: `Province.ProduceGoodsWeekly()` consumes only building-held inputs and never calls `MarketSettlement`.

- [ ] **Step 1: Replace immediate-purchase expectations with failing inventory tests**

Update `BuildingProductionEconomyTests` so a building with market stock but empty input inventory produces nothing and changes no money/market demand. Add:

```csharp
[Test]
public void PreparedInputReceipt_CommitsAllInputsOnlyAfterPreparation()
{
    // Prepare Iron=20 and Wood=5, assert inventory unchanged before Commit,
    // then exact quantities after Commit.
}

[Test]
public void ProduceGoodsWeekly_ConsumesCompleteStoredRecipeAndLeavesRemainders()
{
    // Seed enough for one full production unit plus partial leftovers.
    // Assert one output quantum and exact remaining inputs without market purchase.
}
```

Also cover overflow, negative restore values, missing complementary input, output preflight failure, and input-free continuous production.

- [ ] **Step 2: Run building tests and verify RED**

Run `Invoke-UnityEditMode` for `BuildingInputInventoryTests/task5-input-red` and `BuildingProductionEconomyTests/task5-production-red`.

Expected: missing inventory API and changed-behavior assertions FAIL.

- [ ] **Step 3: Implement sparse versioned input inventory**

Use `Dictionary<string,long>` and an integer version. Preparation calculates checked next values without mutation and captures the version; commit verifies only its private owner/version relationship, writes positive values, removes zeros, and increments the version. Restore validates known/nonblank product names and nonnegative quantities.

- [ ] **Step 4: Refactor production into preflight then commit**

For input recipes, calculate whole production units from workers and stored inputs. Validate output quantities and destination inventory before subtracting inputs. Then subtract inputs and add outputs in a non-failing commit section. Remove `GetAffordableProductionScale` and `TryPurchaseBuildingInputs` from the production path.

- [ ] **Step 5: Run building, money conservation, and employment regressions**

Run `Invoke-UnityEditMode` for `BuildingInputInventoryTests/task5-input-green`, `BuildingProductionEconomyTests/task5-production-green`, `MoneyConservationIntegrationTests/task5-conservation`, and `EmploymentTests/task5-employment`.

Expected: all PASS; production no longer moves money.

- [ ] **Step 6: Commit Task 5**

```powershell
git add -- Assets/Scripts/Class/BuildingInputInventory.cs Assets/Scripts/Class/BuildingInputInventory.cs.meta Assets/Scripts/Class/Building.cs Assets/Scripts/Class/Province.cs Assets/Tests/EditMode/BuildingInputInventoryTests.cs Assets/Tests/EditMode/BuildingInputInventoryTests.cs.meta Assets/Tests/EditMode/BuildingProductionEconomyTests.cs
git commit -m "feat: produce buildings from stored market inputs"
```

### Task 6: 건설 프로젝트 경매 주문 어댑터

**Files:**
- Modify: `Assets/Scripts/Class/ConstructionProcurement.cs`
- Modify: `Assets/Scripts/Class/ConstructionWeeklySimulation.cs`
- Modify: `Assets/Tests/EditMode/ConstructionProcurementTests.cs`
- Modify: `Assets/Tests/EditMode/ConstructionWeeklyIntegrationTests.cs`

**Interfaces:**
- Produces: `ConstructionProcurement.CollectOrders(IReadOnlyList<ConstructionMandate> mandates, MarketAccessContext access, IDictionary<MoneyAccount,long> remainingBudgets) : IReadOnlyList<MarketOrder>`.
- Produces one `IMarketOrderRecipient` per mandate; it groups positive fills by product and calls existing `TryPrepareMaterialAcquisition` exactly once.
- Preserves: `TryProcessMarket(...)` as a compatibility wrapper that collects, plans and settles only supplied construction mandates.

- [ ] **Step 1: Add failing construction auction-adapter tests**

```csharp
[Test]
public void CollectOrders_ReservesInvestorBudgetAcrossProjectsAndMaterials()
{
    // Two projects share one investor. Assert aggregate ReservedBudget <= balance,
    // order IDs are stable project/product length-prefixed IDs, and no mandate mutates.
}

[Test]
public void Recipient_OnePreparedReceiptCommitsAllProjectMaterialsAndSpending()
{
    // Give fills for two products at explicit prices. Prepare, assert unchanged,
    // commit, then assert acquired quantities and MaterialSpending exactly once.
}
```

Update compatibility tests to assert construction-only clearing still conserves money and proportionally resolves equal bids.

- [ ] **Step 2: Run construction procurement tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'ConstructionProcurementTests' -resultName 'task6-construction-orders-red'`.

Expected: `CollectOrders` and recipient assertions FAIL.

- [ ] **Step 3: Refactor construction procurement into order production**

Reuse the existing BigInteger `FloorShare` for reference-value budget reservations. Set maximum unit price to `min(reservedBudget / quantity, ceil(Product.Price * 1.25))`; reduce quantity first when the reservation cannot cover one reference-price unit. Do not allocate stock in the adapter.

- [ ] **Step 4: Split construction progression from procurement**

Change `ConstructionWeeklySimulation` to expose:

```csharp
public static IReadOnlyList<ConstructionMandate> ActiveProjects(
    IEnumerable<Nation> nations, IEnumerable<Province> provinces);
public static void ProgressPaidCompanies(IEnumerable<Province> provinces,
    IEnumerable<Province> paidProvinces, double weeklyManhoursPerLevel);
```

The weekly coordinator added in Task 9 will own procurement timing.

- [ ] **Step 5: Run construction test suites**

Run `Invoke-UnityEditMode` for `ConstructionProcurementTests/task6-procurement`, `ConstructionMandateTests/task6-mandate`, `ConstructionContractTests/task6-contract`, and `ConstructionWeeklyIntegrationTests/task6-weekly`.

Expected: all PASS after integration tests are updated to explicit production-before-auction semantics.

- [ ] **Step 6: Commit Task 6**

```powershell
git add -- Assets/Scripts/Class/ConstructionProcurement.cs Assets/Scripts/Class/ConstructionWeeklySimulation.cs Assets/Tests/EditMode/ConstructionProcurementTests.cs Assets/Tests/EditMode/ConstructionWeeklyIntegrationTests.cs
git commit -m "refactor: submit construction materials as market orders"
```

### Task 7: 공장 원재료 주문 어댑터

**Files:**
- Create: `Assets/Scripts/Class/FactoryMarketOrders.cs`
- Create: `Assets/Scripts/Class/FactoryMarketOrders.cs.meta`
- Test: `Assets/Tests/EditMode/FactoryMarketOrderTests.cs`
- Test: `Assets/Tests/EditMode/FactoryMarketOrderTests.cs.meta`

**Interfaces:**
- Produces: `FactoryMarketOrders.Collect(Province province, MarketAccessContext access, bool payrollPaid, IDictionary<MoneyAccount,long> remainingBudgets) : IReadOnlyList<MarketOrder>`.
- Rule: only paid ordinary buildings with positive level, workers, outputs and positive recipe inputs submit.
- Recipient: groups fills by product and calls `Building.TryPrepareInputReceipt` once.

- [ ] **Step 1: Write failing factory order tests**

Cover target quantity, stored-input subtraction, budget split by reference cost, maximum-price cap, shared market product identity, unpaid province, zero workers, input-free building, and `ConstructionCompanyBuilding` exclusion.

```csharp
[Test]
public void Collect_MultiInputFactoryRequestsOnlyNextWeekShortfall()
{
    // Worker scale requires Iron=20, Wood=5; building already holds Iron=8.
    // Assert orders Iron=12 and Wood=5 against MarketAccess products.
}
```

- [ ] **Step 2: Run factory order tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'FactoryMarketOrderTests' -resultName 'task7-factory-orders-red'`.

Expected: missing collector FAIL.

- [ ] **Step 3: Implement factory demand and reservation splitting**

Calculate next-week whole production units from paid worker scale, subtract stored inputs, and allocate the building's remaining global budget by current reference-cost weights. For each input set `quantity = min(shortfall, reservedBudget / referencePrice)` and `maximumUnitPrice = min(reservedBudget / quantity, ceil(referencePrice * 1.25))`; omit zero quantities. Stable order ID format is `factory:{accountId}:{productName}` with length-prefix escaping when composing arbitrary IDs.

- [ ] **Step 4: Implement one recipient per building**

The recipient accepts partial fills, aggregates checked `long` quantities, preflights one building receipt, and commits only after settlement. Zero fills are a successful no-op.

- [ ] **Step 5: Run factory, production, and employment tests**

Run `Invoke-UnityEditMode` for `FactoryMarketOrderTests/task7-factory-orders-green`, `BuildingInputInventoryTests/task7-input`, `BuildingProductionEconomyTests/task7-production`, and `EmploymentTests/task7-employment`.

Expected: all PASS.

- [ ] **Step 6: Commit Task 7**

```powershell
git add -- Assets/Scripts/Class/FactoryMarketOrders.cs Assets/Scripts/Class/FactoryMarketOrders.cs.meta Assets/Tests/EditMode/FactoryMarketOrderTests.cs Assets/Tests/EditMode/FactoryMarketOrderTests.cs.meta
git commit -m "feat: submit factory input market orders"
```

### Task 8: 주민 식량 주문 어댑터

**Files:**
- Modify: `Assets/Scripts/Class/EconomicEngine.cs:8-95`
- Test: `Assets/Tests/EditMode/PopulationMarketOrderTests.cs`
- Test: `Assets/Tests/EditMode/PopulationMarketOrderTests.cs.meta`
- Modify: `Assets/Tests/EditMode/EconomicConsumptionTests.cs`

**Interfaces:**
- Produces: `EconomicEngine.CollectFoodOrders(Province province, MarketAccessContext access, IDictionary<MoneyAccount,long> remainingBudgets) : PopulationMarketOrderBatch`.
- Produces: `PopulationMarketOrderBatch.Orders` and `PopulationMarketOrderBatch.Recipients`; every population is present in `Recipients` even when it cannot submit a positive order.
- Preserves: direct `ConsumeFoodsWeekly` and `ConsumeGoodsWeekly` legacy entry points for isolated direct-call tests.
- Recipient: sums the population's fill and invokes `BuyFood(totalPurchased)` once after settlement.

- [ ] **Step 1: Write failing population order tests**

```csharp
[Test]
public void CollectFoodOrders_ChoosesLowestPriceThenHighestStockThenName()
{
    // Three foods: cheapest tie, different stocks. Assert exactly one order for
    // the highest-stock cheapest product and no money/living-standard mutation.
}

[Test]
public void FoodRecipients_CompeteByBidAndEachUpdatesLivingStandardOnce()
{
    // Rich and poor population orders share scarce food. Plan/settle and assert
    // auction fills, balances, seller/tax, and one BuyFood application per pop.
}
```

Also cover zero need, no product definition, no affordable unit, connected/isolated product identity, and stable IDs.

- [ ] **Step 2: Run population order tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'PopulationMarketOrderTests' -resultName 'task8-pop-orders-red'`.

Expected: collection API missing.

- [ ] **Step 3: Implement food selection, budget-backed quantity and recipient**

Use `Price`, then descending `Stock`, then ordinal product name. Cap quantity to `min(GetNeededFood(), balance / referencePrice)`. Reserve at most the remaining account budget and the +25% unit ceiling. Return every population recipient separately from the positive orders so the weekly plan calls `BuyFood(0)` exactly once when it cannot buy.

- [ ] **Step 4: Keep legacy direct consumption behavior explicit**

Do not route `ConsumeFoodsWeekly` through a global batch because callers expect immediate behavior. Share product-selection helpers but keep direct settlement for compatibility. Only `GameManager` stops calling the legacy method in Task 9.

- [ ] **Step 5: Run population and existing consumption tests**

Run `Invoke-UnityEditMode` for `PopulationMarketOrderTests/task8-pop-orders-green` and `EconomicConsumptionTests/task8-consumption`.

Expected: both PASS.

- [ ] **Step 6: Commit Task 8**

```powershell
git add -- Assets/Scripts/Class/EconomicEngine.cs Assets/Tests/EditMode/PopulationMarketOrderTests.cs Assets/Tests/EditMode/PopulationMarketOrderTests.cs.meta Assets/Tests/EditMode/EconomicConsumptionTests.cs
git commit -m "feat: submit population food market orders"
```

### Task 9: 주간 통합 시장과 게임 루프 전환

**Files:**
- Create: `Assets/Scripts/Class/WeeklyMarketSimulation.cs`
- Create: `Assets/Scripts/Class/WeeklyMarketSimulation.cs.meta`
- Modify: `Assets/Scripts/Manager/GameManager.cs:375-467, 578-609`
- Modify: `Assets/Scripts/Class/ConstructionWeeklySimulation.cs`
- Test: `Assets/Tests/EditMode/WeeklyMarketSimulationTests.cs`
- Test: `Assets/Tests/EditMode/WeeklyMarketSimulationTests.cs.meta`
- Modify: `Assets/Tests/EditMode/ConstructionWeeklyIntegrationTests.cs`
- Modify: `Assets/Tests/EditMode/MoneyConservationIntegrationTests.cs`

**Interfaces:**
- Produces: `WeeklyMarketSimulation.Process(IEnumerable<Nation> nations, IEnumerable<Province> provinces, ISet<Province> paidProvinces, EconomicEngine economicEngine, MarketPriceSettings settings) : WeeklyMarketReport`.
- Produces report with successful/failed market IDs, order count, sorted-order count, and elapsed ticks for diagnostics.
- Consumes Task 6/7/8 collectors and Task 3/4 clearing plan.

- [ ] **Step 1: Write failing weekly integration tests**

Add exact scenarios:

```csharp
[Test]
public void Process_ResidentsFactoriesAndConstructionClearOneSharedProductWithoutOrderBias()
{
    // One national Iron product; one factory, one project, two populations.
    // Run with forward and reversed province/order enumeration from equivalent
    // worlds. Assert identical fills, clearing price, tax, seller revenue and supply.
}

[Test]
public void Process_IsolatedMarketFailureDoesNotRollbackSuccessfulNationalMarket()
{
    // Corrupt only isolated supplier ledger. Assert national settlement commits,
    // isolated money/inventory/stats unchanged, and report identifies the failure.
}
```

Also test same-week produced construction material, next-week factory input use, zero-stock price pressure, and material-free construction progression.

- [ ] **Step 2: Run weekly tests and verify RED**

Run: `Invoke-UnityEditMode -testFilter 'WeeklyMarketSimulationTests' -resultName 'task9-weekly-red'`.

Expected: missing coordinator and old ordering FAIL.

- [ ] **Step 3: Implement global reservation then per-market planning**

Resolve every participating province once. Build one `remainingBudgets` dictionary from starting balances, let each collector reserve from it, and group orders by `MarketAccessContext.Products` object identity. For each market, plan all product fills, prepare all recipients, and settle one atomic batch. Record failures without retrying in the same week.

- [ ] **Step 4: Replace GameManager weekly order**

Implement the approved sequence:

```csharp
Payroll -> BeginLedgerWeeks/ProductState.BeginWeek -> road cache/mandate assignment
-> paid province production -> transfer connected outputs
-> WeeklyMarketSimulation.Process -> ProgressPaidCompanies
-> finalize macro statistics -> policy issuance -> ledger audit
```

Remove the weekly call to `ConsumeFoodsWeekly`; the coordinator now applies food recipients. Ensure all markets, including isolated markets and products with no orders, finalize price pressure exactly once.

- [ ] **Step 5: Run weekly, construction, conservation and save round-trip regressions**

Run `Invoke-UnityEditMode` for `WeeklyMarketSimulationTests/task9-weekly-green`, `ConstructionWeeklyIntegrationTests/task9-construction`, `MoneyConservationIntegrationTests/task9-conservation`, and `SaveRoundTripTests/task9-save-regression`.

Expected: weekly economic tests PASS; save tests may remain unchanged until Task 10 only if they do not construct new input inventory. Any actual failure must be diagnosed before continuing.

- [ ] **Step 6: Commit Task 9**

```powershell
git add -- Assets/Scripts/Class/WeeklyMarketSimulation.cs Assets/Scripts/Class/WeeklyMarketSimulation.cs.meta Assets/Scripts/Manager/GameManager.cs Assets/Scripts/Class/ConstructionWeeklySimulation.cs Assets/Tests/EditMode/WeeklyMarketSimulationTests.cs Assets/Tests/EditMode/WeeklyMarketSimulationTests.cs.meta Assets/Tests/EditMode/ConstructionWeeklyIntegrationTests.cs Assets/Tests/EditMode/MoneyConservationIntegrationTests.cs
git commit -m "feat: clear weekly economy through shared markets"
```

### Task 10: 거시 가격 통계와 저장 형식 버전 2

**Files:**
- Modify: `Assets/Scripts/Class/EconomicEngine.Nation.cs:6-47`
- Modify: `Assets/Scripts/Class/GovernmentBudget.cs:92-320`
- Modify: `Assets/Scripts/Manager/SaveManager.cs:8-100, 109-223`
- Modify: `Assets/Scripts/Manager/GameSaveState.cs:35-348`
- Modify: `Assets/Scripts/Manager/GameSaveHooks.cs`
- Modify: `Assets/Tests/EditMode/SaveRoundTripTests.cs`
- Test: `Assets/Tests/EditMode/MarketMacroStatisticsTests.cs`
- Test: `Assets/Tests/EditMode/MarketMacroStatisticsTests.cs.meta`

**Interfaces:**
- GDP valuation: `LastClearingPrice > 0 ? LastClearingPrice : LastPrice`.
- CPI valuation uses the same observed price and existing supply+demand weights.
- Save version: `SaveManager.CurrentVersion = 2`.
- `ProductData`: add `requestedDemand`, `unmetDemand`, `lastClearingPrice`.
- `BuildingData`: add `List<AmountData> inputInventory`.

- [ ] **Step 1: Write failing macro valuation tests**

```csharp
[Test]
public void CalculateGDP_UsesObservedClearingPriceInsteadOfNextReferencePrice()
{
    // LastSupply=10, LastPrice=8, LastClearingPrice=12, Price=15.
    // Assert GDP=120, not 150.
}
```

Add CPI test with a traded and untraded product.

- [ ] **Step 2: Write failing save round-trip and v1 migration tests**

Extend `SaveRoundTripTests` to seed two factory inputs and nonzero requested/unmet/clearing statistics, save, load and compare exact values. Serialize a version-1 fixture without new fields and assert migration yields empty input inventory, requested=demand, unmet=0, clearing=price.

- [ ] **Step 3: Run macro/save tests and verify RED**

Run `Invoke-UnityEditMode` for `MarketMacroStatisticsTests/task10-macro-red` and `SaveRoundTripTests/task10-save-red`.

Expected: valuation and missing serialized fields FAIL.

- [ ] **Step 4: Switch macro valuation to observed prices**

Add one helper used by GDP and CPI:

```csharp
private static int ObservedPrice(ProductState product) =>
    product.LastClearingPrice > 0 ? product.LastClearingPrice : Math.Max(1, product.LastPrice);
```

Keep GDP as production approach and keep existing CPI weights.

- [ ] **Step 5: Capture and restore new state**

Capture product statistics in `CaptureMarket` and building inputs in province building records. Restore validates nonnegative quantities, `unmet <= requested`, positive observed prices, known products, and checked conversions before publishing the detached world.

- [ ] **Step 6: Add version-1 migration before version rejection**

```csharp
if (data.version == 1) MigrateVersion1To2(data);
if (data.version != CurrentVersion)
    throw new InvalidDataException($"지원하지 않는 저장 버전입니다: {data.version}");
```

Migration normalizes null new lists, sets each product's new defaults from existing fields, then sets `version = 2`. Do not modify economy-external save data.

- [ ] **Step 7: Run macro/save and complete economic regression group**

Run `Invoke-UnityEditMode` for `MarketMacroStatisticsTests/task10-macro-green`, `SaveRoundTripTests/task10-save-green`, `GovernmentBudgetLedgerTests/task10-budget`, `EconomicInitializationTests/task10-initialization`, and `MoneyConservationIntegrationTests/task10-conservation`.

Expected: all PASS.

- [ ] **Step 8: Commit Task 10**

```powershell
git add -- Assets/Scripts/Class/EconomicEngine.Nation.cs Assets/Scripts/Class/GovernmentBudget.cs Assets/Scripts/Manager/SaveManager.cs Assets/Scripts/Manager/GameSaveState.cs Assets/Scripts/Manager/GameSaveHooks.cs Assets/Tests/EditMode/SaveRoundTripTests.cs Assets/Tests/EditMode/MarketMacroStatisticsTests.cs Assets/Tests/EditMode/MarketMacroStatisticsTests.cs.meta
git commit -m "feat: persist clearing prices and factory inputs"
```

### Task 11: 장기 결정성·성능·전체 문서 검증

**Files:**
- Test: `Assets/Tests/EditMode/MarketLongRunTests.cs`
- Test: `Assets/Tests/EditMode/MarketLongRunTests.cs.meta`
- Test: `Assets/Tests/EditMode/MarketClearingPerformanceTests.cs`
- Test: `Assets/Tests/EditMode/MarketClearingPerformanceTests.cs.meta`
- Modify: `obsedian documentry/화폐 순환 및 경제 시스템 구현 현황.md`
- Create: `obsedian documentry/국가 공유시장 경매와 가격 형성.md`
- Create: `obsedian documentry/국가 공유시장 경매와 가격 형성.md.meta`
- Modify: `docs/superpowers/specs/2026-09-19-market-clearing-design.md`
- Modify: `docs/superpowers/plans/2026-09-19-market-clearing.md`

**Interfaces:**
- No new runtime interface.
- Completion evidence: 52-week invariant result, 5,000-order measurement, full EditMode result, and manual PlayMode status recorded separately.

- [x] **Step 1: Add a 52-week invariant test**

Build a deterministic small world containing connected and isolated markets, multiple populations, a multi-input factory and a construction project. For each week assert:

```csharp
Assert.That(ledger.Audit(out long total), Is.True);
Assert.That(total, Is.EqualTo(initialMoneySupply));
Assert.That(products.All(p => p.Price >= 1 && p.Stock >= 0), Is.True);
Assert.That(buildings.SelectMany(b => b.InputInventory.Values).All(q => q >= 0), Is.True);
```

Run the same initial world twice with reversed enumerable order and compare week-by-week balances, inventories, prices and fills.

- [x] **Step 2: Add a 5,000-order performance characterization**

Construct 18 products, registered supplier/buyer accounts, and 5,000 valid orders distributed deterministically. Measure planning and settlement separately with `Stopwatch`; assert result correctness, money audit and exact order count, but log elapsed milliseconds instead of using a hardware-sensitive time assertion.

- [x] **Step 3: Run long-run and performance tests**

Run `Invoke-UnityEditMode -testFilter 'MarketLongRunTests' -resultName 'task11-longrun'` and `Invoke-UnityEditMode -testFilter 'MarketClearingPerformanceTests' -resultName 'task11-performance'`.

Expected: invariant tests PASS; performance log reports order count, sorted count, plan time and settlement time.

- [x] **Step 4: Run the full EditMode suite**

Run: `Invoke-UnityEditMode -testFilter '' -resultName 'market-clearing-full'`.

Expected: result `Passed`, failed `0`, skipped `0`. Record the actual total rather than assuming the previous 217 count.

- [x] **Step 5: Inspect compilation and runtime diagnostics**

```powershell
Select-String -LiteralPath 'TestResults\market-clearing-full.log' -Pattern 'error CS|warning CS|Unhandled|Test run completed' | ForEach-Object { $_.Line }
git diff --check
git status --short
```

Expected: no C# compiler errors; any pre-existing warning is identified separately from new warnings; no whitespace errors.

- [x] **Step 6: Update durable documentation with measured evidence**

Document:

- weekly order/production/clearing/construction order;
- `Price`, `LastPrice`, `LastClearingPrice`, `RequestedDemand`, `UnmetDemand` meanings;
- connected vs isolated market access;
- factory input one-week lag;
- uniform-price and tie rules;
- actual test counts and performance measurement;
- persistence version 2 and version 1 migration;
- quality, transport, policy priority, international trade, dividends and private investment remain outside scope;
- manual PlayMode is unverified unless it was actually performed.

Set the spec status to implemented only after full verification succeeds, and check plan boxes only for completed work.

- [x] **Step 7: Commit tests and documentation**

```powershell
git add -- Assets/Tests/EditMode/MarketLongRunTests.cs Assets/Tests/EditMode/MarketLongRunTests.cs.meta Assets/Tests/EditMode/MarketClearingPerformanceTests.cs Assets/Tests/EditMode/MarketClearingPerformanceTests.cs.meta 'obsedian documentry/화폐 순환 및 경제 시스템 구현 현황.md' 'obsedian documentry/국가 공유시장 경매와 가격 형성.md' 'obsedian documentry/국가 공유시장 경매와 가격 형성.md.meta docs/superpowers/specs/2026-09-19-market-clearing-design.md docs/superpowers/plans/2026-09-19-market-clearing.md
git commit -m "test: verify unified market clearing"
```

- [x] **Step 8: Request final review and verify the reviewed head**

Use `superpowers:requesting-code-review` against the complete branch. Fix validated findings with targeted tests, then invoke `superpowers:verification-before-completion` and rerun the full EditMode command on the final reviewed commit.

## Dependency Summary

- Task 1 establishes price state and settings used by every later task.
- Task 2 establishes market identity and ledger selection.
- Task 3 produces side-effect-free auction plans.
- Task 4 makes those plans financially atomic at an explicit price.
- Task 5 makes partial factory input fills safe to retain.
- Tasks 6, 7 and 8 adapt construction, factories and populations independently.
- Task 9 is the only task that switches the live weekly loop.
- Task 10 updates macro consumers and current persistence.
- Task 11 supplies long-run, performance, documentation and final review evidence.

Tasks must execute in this order. Do not switch `GameManager` to the new path before Tasks 1-8 pass their focused tests.
