# 건설 자재 조달·공사대금 정산 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 국가 발주가 실제 자재 구매와 회사 매출을 발생시키고, 고용된 건설 노동자의 공수로 완공되도록 연결한다.

**Architecture:** `ConstructionMandate`가 계약·자재·지급의 단일 원본이다. `ConstructionProcurement`는 같은 시장의 요청을 모아 배분하고 `MarketSettlement`가 다중 구매자 거래를 원자적으로 정산한다. 기존 고용·임금 처리와 화폐 원장을 재사용한다.

**Tech Stack:** Unity 6000.0.71f1, C#, NUnit EditMode, 기존 reflection 테스트 어셈블리.

**Spec:** `docs/superpowers/specs/2026-09-09-construction-procurement-design.md`

## Global Constraints

- 국가 발주만 활성화한다. 민족집단 자동 투자와 운송회사는 제외한다.
- 같은 국가 공유 시장의 건설 수요를 함께 배분한다. 소비·생산 구매와의 공통 배분은 제외한다.
- 기본 착공 확보율은 각 품목 필요량의 30%이다.
- 공사대금과 운영자금은 예치하고, 자재비는 발주자의 가용 잔액에서 구매 시 지급한다.
- 자동 발행, 대출, 음수 잔고, 자재 구매대금의 가상 환불을 금지한다.
- 취소한 미사용 자재는 발주자 소유 재고로 반환한다.
- 같은 종류 건물의 다른 소유자 증설을 금지한다.
- 고용 기반은 `feature/ethnic-employment`의 `46469e6`이다. `market`에 자동 병합하지 않는다.
- 기존 작업 트리의 사용자 변경과 Unity 생성 파일은 보존한다.

## 실행 환경과 검증 명령

`using-git-worktrees` 스킬에 따라 `46469e6`에서 `feature/construction-procurement` 브랜치와 `D:/ProjectNO/.worktrees/construction-procurement` 작업 트리를 만든다. 설계 커밋 `c74f27c`와 이 계획 파일을 실행 브랜치에도 포함한다. 이후 모든 명령은 새 작업 트리에서 실행한다.

Unity 테스트 명령은 다음과 같다. 같은 프로젝트를 에디터에서 열어 둔 상태로 배치 실행하지 않는다. `-quit`을 넣지 않는다.

```powershell
$unityExe = 'C:/Program Files/Unity/Hub/Editor/6000.0.71f1/Editor/Unity.exe'
$testRoot = 'D:/ProjectNO/.worktrees/construction-procurement'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$testXml = "$testRoot/TestResults/construction-$runId.xml"
$testLog = "$testRoot/TestResults/construction-$runId.log"
New-Item -ItemType Directory -Force -Path "$testRoot/TestResults" | Out-Null
$unityArgs = @('-batchmode','-nographics','-projectPath',$testRoot,
    '-runTests','-testPlatform','EditMode','-testResults',$testXml,'-logFile',$testLog)
$process = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
```

비동기 실행 중 상태와 로그를 확인하고 사용자에게 진행 상황을 알린다. 프로세스 종료 후 XML을 반드시 읽는다. 종료 코드만으로 통과를 판정하지 않는다.

```powershell
[xml]$results = Get-Content -Raw $testXml
$results.'test-run' | Select-Object result,total,passed,failed,skipped
$results.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
    $_.fullname
    $_.failure.message.InnerText
}
```

각 단계의 첫 실행에는 `-testFilter`와 해당 테스트 클래스 이름을 추가한다. 마지막에는 필터 없이 전체 실행한다. 새 `.cs`의 Unity `.meta`는 생성 후 해당 코드와 함께 추적한다.

## Task 1: 계약 설정과 소유권

**Files:**
- Modify: `Assets/Scripts/Class/Building.cs`
- Modify: `Assets/Scripts/GlobalVariables.cs`
- Modify: `Assets/Resources/BuildingRecipes.json`
- Modify: `Assets/Scripts/Class/EconomicInitializer.cs`
- Test: `Assets/Tests/EditMode/EconomicInitializationTests.cs`
- Create: `Assets/Tests/EditMode/ConstructionContractTests.cs`

**Interfaces:** 레시피의 `long ConstructionFee`, `int StartMaterialBasisPoints = 3000`; 건물의 `IBuildingInvestor Owner { get; internal set; }`. JSON은 `constructionFee`, `startMaterialBasisPoints`를 사용한다.

- [ ] 기본값 테스트를 먼저 작성한다.

```csharp
[Test]
public void NewRecipe_DefaultsToThirtyPercentStartThreshold()
{
    object recipe = ReflectionTestHelpers.New("BuildingRecipe", "Test");
    Assert.That(ReflectionTestHelpers.Get(recipe, "StartMaterialBasisPoints"), Is.EqualTo(3000));
    Assert.That(ReflectionTestHelpers.Get(recipe, "ConstructionFee"), Is.EqualTo(0L));
}
```

- [ ] 테스트 실행으로 필드가 없어 실패하는지 확인한다.
- [ ] 위 필드와 로더 매핑을 추가한다. 도메인 공사비는 0 이상, 확보율은 1~10000, 필요 자재는 양수, 공수는 유한한 양수만 허용한다. JSON 누락 확보율은 3000, 누락 공사비는 0으로 처리하여 기존 데이터 로딩을 유지한다.
- [ ] 배포 JSON에는 각 레시피 공사비를 명시한다. 초기 조정값은 기존 `TimeToBuild`와 같은 정수 금액으로 기록하며 런타임 자동 가격 공식으로 사용하지 않는다. 운영자금은 변경하지 않는다.
- [ ] 초기 국가 건물은 초기화 시 국가를 소유자로 설정한다. 중립 초기 건물은 소유자 미지정으로 두며 임의의 민족집단에 귀속하지 않는다.
- [ ] 음수 공사비, 잘못된 확보율, 국가 초기 소유권, 기존 초기 자본 이체 테스트를 실행한다.
- [ ] 관련 파일만 커밋한다: `feat: define construction contract settings and building ownership`.

## Task 2: 다중 구매자 원자적 시장 결제

**Files:**
- Create: `Assets/Scripts/Class/MarketBatchPurchase.cs`
- Modify: `Assets/Scripts/Class/MarketSettlement.cs`
- Test: `Assets/Tests/EditMode/MarketBatchPurchaseTests.cs`

**Interfaces:**

```csharp
// 생성자 검증과 읽기 전용 속성을 갖는 값 객체로 구현한다.
public sealed class MarketBuyerRequest
{
    public string RequestId { get; }
    public MoneyAccount Buyer { get; }
    public ProductState Product { get; }
    public int Quantity { get; }
    public MarketBuyerRequest(string requestId, MoneyAccount buyer, ProductState product, int quantity);
}
// MarketSettlement에 추가. 요청 수량 전부를 구매하거나 전체 실패한다.
public static bool TryPurchaseBatch(IReadOnlyList<MarketBuyerRequest> requests, MoneyLedger ledger);
```

위 선언은 인터페이스 명세이며 본문 없는 선언 그대로 컴파일 파일에 넣지 않는다. 호출자는 성공 시 요청량과 당시 가격으로 프로젝트 확보량·누적 비용을 갱신한다.

- [ ] `MarketBatchPurchaseTests`에 먼저 빈 요청 거부 테스트를 추가한다.

```csharp
[Test]
public void EmptyBatch_DoesNotSucceed()
{
    object requests = TestEconomyFactory.ListOf("MarketBuyerRequest");
    var method = ReflectionTestHelpers.Find("MarketSettlement").GetMethod("TryPurchaseBatch");
    Assert.That(method, Is.Not.Null);
    Assert.That(method.Invoke(null, new object[] { requests, null }), Is.False);
}
```

- [ ] API 부재에 따른 실패를 확인한 다음 실제 거래 테스트를 추가한다. 기존 `MarketSettlementTests`의 계좌 생성 패턴으로 구매자 A/B 각 100, 공급자 0, 재고 10, 가격 10, 세율 0을 만든다. A/B가 각각 5개 구매하면 잔액은 50/50/100, 재고는 0이어야 한다. B 잔액을 49로 바꾸면 거래 전 상태 전부가 유지되어야 한다.
- [ ] 품목별 요청량을 합산하여 하나의 경제적 판매 배분과 한 번의 재고 확정을 수행한다. 동일 품목을 구매자마다 별도로 배분하여 공급자 재고를 중복 사용하는 구현은 금지한다. 기존 재고 API가 확정 전 정합성 검사를 위해 같은 배분을 재계산하는 것은 유지한다.
- [ ] 원장 소속, 중복 RequestId, 양수 가격·수량, 구매자별 총부담, 재고, LastDemand, 금액 오버플로를 전부 검증한다. 구매자 잔액 판단에 같은 배치에서 받게 될 판매수입을 더하지 않는다.
- [ ] 품목별 총판매액에서 기존 판매세를 계산하고, 기존 `ProportionalAllocator`로 공급자 순수입을 배분한다. 구매자 차감·판매자 지급·세금의 합계가 0인 단일 `TryTransferBatch` 성공 후 품목마다 한 번 `CommitPurchase`한다.
- [ ] 외부 공급자 계좌, 중복 ID, 재고 부족, 한 구매자의 여러 요청, 오버플로 테스트를 실행한다. 실패 시 재고·잔액·통계가 전부 같아야 한다.
- [ ] 커밋한다: `feat: settle multi-buyer market purchases atomically`.

## Task 3: 프로젝트 자재 장부와 공정별 지급

**Files:**
- Modify: `Assets/Scripts/Class/ConstructionMandate.cs`
- Create: `Assets/Scripts/Class/ConstructionMaterials.cs`
- Modify: `Assets/Scripts/Class/Nation.cs`
- Test: `Assets/Tests/EditMode/ConstructionInvestmentTests.cs`
- Test: `Assets/Tests/EditMode/ConstructionContractTests.cs`

**Interfaces:** `ConstructionMandate`에 `string Id`, `long ConstructionFee`, `long PaidConstructionFee`, `long MaterialSpending`, `decimal MaterialProgressLimit`, `bool CanStart`, 읽기 전용 `AcquiredMaterials`/`ConsumedMaterials` 딕셔너리를 제공한다. 기존 생성자는 유지하되 레시피가 있으면 계약 시 스냅샷을 만든다. 이후 전역 레시피 수정은 진행 중 계약에 영향을 주지 않는다.

- [ ] `ConstructionInvestmentTests`의 기존 fixture에 공사비 인수를 추가한다. 자금 1000, 운영자금 300, 공사비 200, 총공수 10인 공사의 발주 후 잔액 500/예치금 500, 공수 5 후 회사 수입 100/예치금 400, 완공 후 회사 수입 200/건물 운영자금 300을 검증한다.
- [ ] 실패를 확인한다. `Nation`에서 합산 예치금을 checked로 계산하고 기존 전량 자재 예약 검사와 `GetReservedAmount`를 제거한다. 소유자가 다른 기존 건물에는 증설을 거부한다.
- [ ] 착공·소비 계산을 `ConstructionMaterials`에 집중시킨다. 최소 확보량은 `(required * basisPoints + 9999L) / 10000L`, 확보율은 decimal로 계산한다. 소비량은 공정 비율에 필요한 수량을 올림하되 완공 시 정확히 required이다.
- [ ] `ApplyManhours`는 NaN/Infinity/음수를 거부하고 자재 상한으로 공수를 제한한다. 예상 지급액은 아래 누적식으로 산출한다.

```csharp
decimal progress = Math.Min(1m, (decimal)completedManhours / (decimal)RequiredManhours);
long cumulativePayment = progress == 1m
    ? ConstructionFee
    : decimal.ToInt64(decimal.Floor(ConstructionFee * progress));
long payment = checked(cumulativePayment - PaidConstructionFee);
```

- [ ] 지급·소유권·완공 건물 계좌를 사전 검증한다. 마지막 진행에서는 회사 지급과 건물 운영자금 이전을 하나의 원장 배치로 처리한다. 원장 성공 전에 진행도·소비량·레벨을 변경하지 않는다. 신규 건물 준비 실패 시 등록한 빈 계좌만 해제한다.
- [ ] 철 100/목재 10 필요 시 철 30/목재 2는 착공 불가, 철 30/목재 3은 30% 상한, 전량 확보 후 완공을 검증한다. 이미 시작한 공사는 원래 착공율을 재구매 조건으로 요구하지 않는다.
- [ ] 실패 지급, NaN 공수, 레시피 수정, 소유자 불일치, 완공 중복 호출, 무자재 공사 테스트를 실행하고 커밋한다: `feat: tie construction progress to materials and contractor payments`.

## Task 4: 시장별 공정한 조달

**Files:**
- Create: `Assets/Scripts/Class/ConstructionProcurement.cs`
- Modify: `Assets/Scripts/Class/ConstructionMandate.cs`
- Test: `Assets/Tests/EditMode/ConstructionProcurementTests.cs`

**Interfaces:** `public static bool ConstructionProcurement.TryProcessMarket(IReadOnlyList<ConstructionMandate> mandates, Dictionary<string, ProductState> products, MoneyLedger ledger)`. 입력은 동일 시장·원장의 활성 계약 목록이다. 구매 불가능한 정상 대기는 성공한 무처리, 데이터 무결성 오류는 false로 구분한다.

- [ ] 서로 다른 두 프로빈스의 공사가 같은 국가 시장 철 재고 10에서 각각 10개를 요청하는 테스트를 작성한다. 결과는 각각 5개 확보이며 입력 목록을 역순으로 다시 구성한 독립 fixture에서도 같아야 한다.
- [ ] 실패를 확인한 뒤 발주자별 전체 잔여 자재의 현재 평가액을 구한다. 시장에 가격이나 재고가 없는 품목은 이번 구매 요청에서 제외하되 계약 필요량에서 삭제하지 않는다.
- [ ] 같은 투자자의 가용 잔액을 평가액 가중치로 공사에 비례 배분한다. 한 공사 예산도 품목 평가액 비율로 나누고 `budget / price`만 요청한다. 소액 단위의 미배분 예산은 이번 주 보유한다. 별도 반복 재배분 루프는 두지 않는다.
- [ ] 품목별 `min(stock, sum(requests))`를 요청량 가중치로 배분한다. `ProportionalAllocator.Allocate`에 계약 ID를 안정키로 사용한다.
- [ ] 최종 요청 배열을 Task 2 API로 한 번 결제한다. 프로젝트 장부의 증가량·비용도 결제 전에 오버플로와 계약 필요량 상한을 검사하고, 성공 후에만 반영한다.
- [ ] 동일 투자자의 여러 공사, 1개 재고의 나머지 처리, 가격 10→100 상승, 잔액 0, 누락 자재, 복수 품목, 입력 중복 계약, 외부 원장 계좌를 검증한다. 실패 시 모든 프로젝트 장부도 그대로여야 한다.
- [ ] 커밋한다: `feat: allocate construction supplies across shared markets`.

## Task 5: 취소·반환·계좌 수명

**Files:**
- Modify: `Assets/Scripts/Class/ConstructionMandate.cs`
- Modify: `Assets/Scripts/Class/Market.cs`
- Modify: `Assets/Scripts/Class/ProvinceCurrencyMigration.cs`
- Test: `Assets/Tests/EditMode/ConstructionContractTests.cs`
- Test: `Assets/Tests/EditMode/NeutralProvinceLedgerTests.cs`

**Interfaces:** 기존 `Cancel()`의 bool 계약을 유지한다. 예치 계좌는 기존 `GetActiveEscrowAccountsFor`를 통해 계속 추적한다. 원자적 반환을 위해 `ProductInventory.ValidateCanReceive`와 checked LastSupply 사전 검사를 사용한다.

- [ ] 50% 진행 후 취소하는 테스트를 작성한다. 미지급 공사대금+운영자금만 환불되고, 확보량-소비량만 발주자 소유 lot로 반환되어야 한다. 이미 지급한 회사 수입은 유지되어야 한다.
- [ ] 실패 확인 후 모든 반환 품목과 계좌의 사전 검증을 수행한다. 시장 또는 상품이 없으면 계약과 예치금을 그대로 보존하고 false를 반환한다. 반환 품목 하나만 먼저 넣고 다음 품목에서 실패하는 처리를 금지한다.
- [ ] 예치금 환불 원장 배치 성공 후 반환 재고를 확정하고 Cancelled로 바꾼다. 실패 가능 조건은 모두 확정 이전에 검사한다.
- [ ] 취소 2회, 취소 후 진행, 반환 재고 int 오버플로, 투자자 계좌 원장 불일치, 시장 연결 변경 테스트를 추가한다.
- [ ] 기존 통화 이전의 활성 예치금 수집을 유지하고, 프로젝트 발주자/반환 공급자 계좌가 이전 대상 검증에서 빠지지 않는지 테스트한다. 안전하게 이전할 수 없는 계약은 무시하지 말고 이전을 거부한다.
- [ ] 커밋한다: `feat: refund unfinished construction and return unused materials`.

## Task 6: 주간 실행·UI·통합 검증

**Files:**
- Modify: `Assets/Scripts/Manager/GameManager.cs`
- Modify: `Assets/Scripts/UI/Building/BuildQueueItem.cs`
- Create: `Assets/Scripts/Class/ConstructionStatusText.cs`
- Test: `Assets/Tests/EditMode/ConstructionWeeklyIntegrationTests.cs`
- Create: `obsedian documentry/건설 자재 조달과 공사대금.md`

**Interfaces:** `ConstructionStatusText.Format(ConstructionMandate mandate)`는 자재 대기/공정, 자재비, 지급 공사비, 운영자금, 현재 잔여 자재 예상 비용을 반환하는 순수 문자열 함수이다. 누락 가격이 있으면 예상 비용을 0이 아닌 산정 불가로 표시한다.

- [ ] 초기 자본→고용·임금→자재 구매→공사 지급→완공을 수동 주간 단계로 호출하는 통합 테스트를 작성한다. 단계마다 `Ledger.Audit(out total)`과 최초 통화량을 비교한다. 임금이 지급되지 않은 회사는 공수를 제공하지 못해야 한다.
- [ ] 기존 순서를 최대한 유지하여 연결 캐시 갱신과 국가 대기 공사 배정 후, 건설회사 시공 전에 시장별 조달을 한 번 실행한다. 이번 주 생산품은 기존처럼 시공 이후 시장으로 들어가므로 다음 주 조달 대상이라는 점을 문서화한다.
- [ ] 조달 실패는 로그와 대기 표시로 드러내고, 실패한 배치의 확보량을 갱신하지 않는다. 이미 보유한 자재로 가능한 공정은 임금 지급 조건을 만족하면 진행할 수 있다.
- [ ] BuildQueueItem의 기존 countText를 재사용한다. 신규 씬 참조를 요구하지 않는다. 장문 텍스트가 기존 프리팹 크기를 넘지 않도록 Unity에서 확인하고 필요하면 요약행과 툴팁을 기존 UI 방식에 맞춰 분리한다.
- [ ] 통합 테스트, UI 문자열 테스트, 전체 EditMode 테스트를 실행한다. 이전 테스트가 예약 전량 확보를 기대하면 새 대기 규칙에 맞게 기대값을 수정하되 원장·투자금 검증은 삭제하지 않는다.
- [ ] `requesting-code-review` 스킬로 변경을 검토하고 중요한 지적을 해결한다. `verification-before-completion` 스킬에 따라 최종 전체 테스트를 다시 실행한다.
- [ ] 문서에 실제 브랜치/작업 트리, Unity에서 확인할 화면, 실행한 테스트 수와 실패 수, 운송·민간 투자·자재비 상한의 미구현 상태를 기록한다. 직접 확인하지 않은 Play Mode 결과는 통과라고 쓰지 않는다.
- [ ] 관련 코드·테스트·문서·meta만 커밋한다: `feat: integrate construction procurement into weekly simulation`.

## 계획 자체 검토

- 계약·소유권·데이터: Task 1과 3.
- 공유 시장, 동일 투자자 중복 잔액, 원자적 구매: Task 2와 4.
- 착공율, 자재 소비, 공정 지급, 완공: Task 3.
- 취소, 반환 공급자, 예치금 수명, 통화 이전: Task 5.
- 고용 연결, 화면 확인, 보존법칙과 회귀 테스트: Task 6.
- 이 문서는 실행 전 계획이다. 체크박스는 실제 검증 전까지 완료로 바꾸지 않는다.
