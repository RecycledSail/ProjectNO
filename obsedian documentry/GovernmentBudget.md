# GovernmentBudget

## 관련 문서

- [[프로젝트 개요]]
- [[Nation]]
- [[GlobalVariables]]

## 역할

`GovernmentBudget`는 국가의 재정 정책을 처리하는 클래스이다.

국가의 GDP를 기준으로 세금을 걷고, UI에서 설정한 정책 예산을 통화 발행 형태로 투입하며, 국가 시장 가격을 이용해 인플레이션을 계산한다.

이 클래스는 [[Nation]]에 포함되어 있으며, 국가별로 하나씩 생성된다.

## 위치

- 코드: `Assets/Scripts/Class/GovernmentBudget.cs`
- 생성 위치: [[Nation]] 생성자
- 주간 실행 위치: `GameManager.ProcessWeeklyEvents()`
- 주요 UI:
  - `BudgetUI`
  - `FinanceUI`
  - `IndustrySubsidyPanel`

## 전체 흐름

`GovernmentBudget`는 매주 `GameManager.ProcessWeeklyEvents()`의 마지막 단계에서 실행된다.

```text
nation.governmentBudget.CollectTaxes()
nation.governmentBudget.PrintMoney()
nation.governmentBudget.UpdateInflation()
```

순서의 의미:

1. `CollectTaxes()`가 국가의 최근 평균 GDP를 기준으로 세금을 걷는다.
2. `PrintMoney()`가 정책 배정액만큼 돈을 발행하고 경제에 투입한다.
3. `UpdateInflation()`이 국가 시장 가격을 기준으로 물가 지수와 인플레이션을 갱신한다.

## PolicyAllocation

`PolicyAllocation`은 이번 주에 정부가 어디에 돈을 투입할지 저장하는 정책 배정 데이터이다.

`BudgetUI`나 `FinanceUI`에서 값이 설정되고, 다음 주간 처리의 `PrintMoney()`에서 실제로 적용된다.

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `ResearchFund` | `long` | 연구 자금으로 투입할 금액 |
| `MilitarySalary` | `long` | 군인 급여 또는 군 관련 민간 지급으로 투입할 금액 |
| `IndustrySubsidy` | `Dictionary<string, long>` | 건물 타입 이름별 산업 보조금 |
| `RealEstateFund` | `long` | 부동산/생활 수준 지원금 |
| `Total` | `long` | 네 정책 항목의 총합 |

### `IndustryTotal()`

산업 보조금 딕셔너리에 들어 있는 금액 합계를 반환한다.

## GovernmentBudget 주요 필드

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `market` | `NationMarket` | 인플레이션 계산에 사용할 국가 시장 |
| `nation` | `Nation` | 이 예산 객체가 속한 [[Nation]] |
| `taxRate` | `float` | 세율. 기본값은 `0.1f` |
| `MoneySupply` | `long` | 통화량. 생성 시 `1,000,000`으로 시작 |
| `WeeklyTaxRevenue` | `long` | 최근 주간 세입 |
| `InflationRate` | `float` | 최근 가격 지수 변화율 |
| `CurrentPriceIndex` | `float` | 현재 가격 지수 |
| `Policy` | `PolicyAllocation` | 다음 `PrintMoney()`에서 적용할 정책 배정 |
| `PendingMoneyPrint` | `long` | 현재 `Policy.Total` |

## 생성자

```csharp
public GovernmentBudget(NationMarket market, Nation nation)
```

[[Nation]] 생성자에서 다음 형태로 만들어진다.

```csharp
governmentBudget = new GovernmentBudget(market, this);
```

초기화 내용:

- 국가 시장 참조 저장
- 국가 참조 저장
- 통화량을 `1_000_000L`로 설정

## 세금 징수

### `CollectTaxes()`

```csharp
long revenue = (long)(nation.GDPAverage * taxRate);
```

최근 4주 평균 GDP인 `nation.GDPAverage`에 세율을 곱해 세입을 계산한다.

동작:

1. `GDPAverage * taxRate`로 세입 계산
2. `WeeklyTaxRevenue` 갱신
3. `nation.balance`에 세입 추가
4. 계산된 세입 반환

현재 기본 세율은 10%이다.

## 통화 발행과 정책 적용

### `PrintMoney()`

`Policy.Total`만큼 돈을 발행하고, 정책 항목별로 경제에 투입한다.

동작:

1. `Policy.Total`을 구한다.
2. 총액이 0 이하이면 아무것도 하지 않는다.
3. `nation.balance`에 총액을 더한다.
4. `MoneySupply`에 총액을 더한다.
5. 정책별 적용 함수를 호출한다.
6. `Policy`를 새 `PolicyAllocation`으로 초기화한다.

호출되는 정책 적용 함수:

- `ApplyResearchPolicy()`
- `ApplyMilitarySalary()`
- `ApplyIndustrySubsidy()`
- `ApplyRealEstatePolicy()`

주의: 정책 금액은 세입 지출이 아니라 통화 발행으로 처리된다. 즉 `nation.balance`가 줄어드는 구조가 아니라, `MoneySupply`와 `nation.balance`가 함께 증가한다.

## 정책별 적용

### `ApplyResearchPolicy()`

`Policy.ResearchFund`가 0보다 크면 `nation.researchFund`에 더한다.

연구 진행 로직이 `researchFund`를 소비한다면 이 값이 연구 속도나 완료에 영향을 준다.

### `ApplyMilitarySalary()`

`Policy.MilitarySalary`를 인구 집단의 `property`에 분배한다.

분배 대상:

1. 국가 연대가 위치한 지역의 `ProvinceEthnicPop`
2. 연대 위치 대상이 없으면 수도의 `ProvinceEthnicPop`

분배 가중치:

- 연대 위치 지역의 인구 집단은 해당 연대 병력 수를 가중치로 사용한다.
- 대체 대상인 수도 인구 집단은 각 인구 집단의 population을 가중치로 사용한다.

최종적으로 각 대상의 `pep.property`가 증가한다.

### `ApplyIndustrySubsidy()`

`Policy.IndustrySubsidy`에 들어 있는 건물 타입별 보조금을 실제 건물에 분배한다.

처리 순서:

1. 보조금이 지정된 건물 타입 이름을 순회한다.
2. 국가 소유 지역에서 같은 타입의 건물을 찾는다.
3. 각 건물의 고용 포화도를 계산한다.
4. 건물 레벨과 포화도 보정값으로 분배 가중치를 계산한다.
5. 가중치 비율대로 건물 `balance`에 보조금을 추가한다.
6. `building.HireWorkers()`를 호출해 고용을 시도한다.

고용 포화도에 따른 효율:

| 포화도 | 효율 |
| --- | --- |
| 90% 이상 | `0.5` |
| 60% 이상 90% 미만 | `1.0` |
| 60% 미만 | `1.5` |

즉, 인력이 부족한 건물일수록 더 많은 보조금을 받도록 설계되어 있다.

### `ApplyRealEstatePolicy()`

`Policy.RealEstateFund`를 국가 전체 인구 비율에 따라 각 인구 집단에 분배한다.

효과:

- `pep.property` 증가
- `pep.livingStandard` 증가

생활 수준 증가량:

```csharp
double lsBoost = (double)amount / Math.Max(1L, nation.GDPAverage) * 0.1;
```

`livingStandard`는 최대 `10.0`으로 제한된다.

## 산업 보조금 자동 배분

### `SetIndustrySubsidyTotal(long totalAmount)`

산업 보조금 총액을 국가가 보유한 건물 타입별로 균등 배분한다.

동작:

1. 기존 `Policy.IndustrySubsidy`를 비운다.
2. 총액이 0 이하이면 종료한다.
3. 국가 소유 지역의 건물 타입 이름을 수집한다.
4. 건물 타입 수로 총액을 나눈다.
5. 나머지는 첫 번째 타입에 더한다.
6. `Policy.IndustrySubsidy[typeName]`에 금액을 저장한다.

`BudgetUI`와 `FinanceUI`는 산업 보조금을 슬라이더로 설정할 때 이 함수를 사용한다.

`IndustrySubsidyPanel`은 더 세부적으로 건물 타입별 금액을 직접 수정할 수 있도록 `Policy.IndustrySubsidy`를 다시 구성한다.

## 인플레이션 계산

### `UpdateInflation()`

국가 시장의 상품 가격을 이용해 현재 가격 지수를 계산하고, 최근 가격 지수 기록과 비교해 인플레이션을 갱신한다.

동작:

1. `CalculatePriceIndex()`로 현재 가격 지수 계산
2. `CurrentPriceIndex` 갱신
3. 최근 가격 지수 큐에 추가
4. 4주를 넘으면 가장 오래된 값 제거
5. 가장 오래된 가격 지수와 현재 가격 지수를 비교해 변화율 계산

계산식:

```csharp
InflationRate = (priceIndex - oldest) / oldest * 100f;
```

### `CalculatePriceIndex()`

국가 시장의 상품별 가격을 공급량과 수요량으로 가중 평균한다.

상품별 가중치:

```csharp
int weight = ps.LastSupply + ps.LastDemand;
```

가중치가 있는 상품만 계산에 포함된다.

가중치 총합이 0이면 가격 지수는 `1f`를 반환한다.

### `GetInflationStatus()`

`InflationRate` 값에 따라 문자열 상태를 반환한다.

| 조건 | 반환값 |
| --- | --- |
| `> 10%` | `Hyperinflation` |
| `> 5%` | `High Inflation` |
| `> 2%` | `Inflation` |
| `> 0%` | `Mild Rise` |
| `< -5%` | `Deflation` |
| `< -2%` | `Mild Fall` |
| 그 외 | `Stable` |

## UI 연결

### `BudgetUI`

`BudgetUI`는 GDP 평균을 기준으로 이번에 발행할 수 있는 최대 금액을 계산한다.

```csharp
_maxPrint = (long)(nation.GDPAverage * MAX_PRINT_GDP_RATIO);
```

현재 `MAX_PRINT_GDP_RATIO`는 `0.5f`이다. 즉 4주 평균 GDP의 50%까지 발행 UI에서 설정할 수 있다.

확정 시:

1. 연구, 군사, 산업, 부동산 슬라이더 값을 읽는다.
2. 총액이 최대치를 넘으면 비율대로 축소한다.
3. 새 `PolicyAllocation`을 만든다.
4. `_budget.Policy`에 저장한다.
5. 산업 보조금 총액은 `SetIndustrySubsidyTotal()`로 건물 타입별 배분한다.

### `FinanceUI`

`FinanceUI`는 `WeeklyTaxRevenue`를 기준으로 정책 비율을 조정한다.

슬라이더 값은 세입 대비 퍼센트로 표시되고, 실제 금액은 다음 방식으로 변환된다.

```csharp
(long)(pct / 100f * revenue)
```

즉 `BudgetUI`는 GDP 기반 통화 발행 예산을 다루고, `FinanceUI`는 세입 대비 정책 배분 UI처럼 동작한다.

### `IndustrySubsidyPanel`

산업 보조금을 건물 타입별로 직접 조정하는 패널이다.

국가 소유 건물들을 타입별로 집계하고:

- 평균 고용 포화도
- 총 이전 수익
- 기존 배정액

을 표시한다.

행 금액이 바뀌면 `Budget.Policy.IndustrySubsidy`를 다시 구성한다.

## 외부 사용 예

| 사용처 | 사용 내용 |
| --- | --- |
| [[Nation]] | 국가 생성 시 `GovernmentBudget` 생성 |
| `GameManager` | 주간 세금, 통화 발행, 인플레이션 처리 |
| `BudgetUI` | 발행 가능 금액 표시, 정책 배정 |
| `FinanceUI` | 세입 기준 정책 비율 조정 |
| `IndustrySubsidyPanel` | 산업 보조금 세부 배정 |
| `UIManager` | 플레이어 국가 GDP/재정 정보 표시와 연계 |

## 주의할 점

### 정책 지출은 실제 지출이 아니라 통화 발행이다

`PrintMoney()`는 `Policy.Total`만큼 `nation.balance`와 `MoneySupply`를 모두 증가시킨다.

따라서 현재 구조에서는 정책 자금 투입이 국가 재정 지출이라기보다 신규 화폐 발행에 가깝다.

### `BudgetUI`와 `FinanceUI`의 기준 금액이 다르다

`BudgetUI`는 평균 GDP의 50%를 최대 발행액으로 쓴다.

`FinanceUI`는 `WeeklyTaxRevenue`를 기준으로 정책 금액을 계산한다.

두 UI가 같은 정책 객체를 수정하므로, 플레이어 입장에서는 두 화면의 정책 기준이 다르게 느껴질 수 있다.

### 산업 보조금은 건물 타입 이름 문자열에 의존한다

`Policy.IndustrySubsidy`의 키는 `BuildingType.name`이다.

건물 이름이 바뀌면 저장된 정책 배정이나 UI 표시와 연결이 깨질 수 있다.

### 인플레이션은 국가 시장의 최근 공급/수요가 있어야 의미가 있다

`CalculatePriceIndex()`는 `LastSupply + LastDemand`가 0인 상품을 제외한다.

주간 시장 처리 순서나 상품 이동이 제대로 이루어지지 않으면 가격 지수가 `1f`로 유지될 수 있다.

### 저장/불러오기 대상이 아니다

현재 `SaveManager` 기준으로 `GovernmentBudget`의 상태는 별도로 저장되지 않는다.

저장되지 않는 주요 값:

- `MoneySupply`
- `WeeklyTaxRevenue`
- `InflationRate`
- `CurrentPriceIndex`
- `Policy`
- 가격 지수 히스토리

게임을 불러온 뒤 재정/물가 상태가 초기화되거나 전역 객체 재사용 상태에 의존할 수 있다.

### `MoneySupply`와 시장 가격의 직접 연결은 아직 약하다

`MoneySupply`는 증가하지만, 그 자체가 가격 계산식에 직접 들어가지는 않는다.

현재 인플레이션은 시장의 상품 가격 변화로만 계산된다.

## 개선 후보

- 재정 정책이 세입 지출인지 통화 발행인지 개념 분리
- `BudgetUI`와 `FinanceUI`의 정책 기준 통일
- `PolicyAllocation` 저장/불러오기 지원
- `MoneySupply`, 인플레이션, 가격 지수 히스토리 저장
- 세율 조정 UI와 정책 효과 추가
- 산업 보조금 키를 문자열 대신 안정적인 건물 타입 ID로 관리
- `MoneySupply`가 가격, 수요, 인플레이션에 미치는 영향 모델링
- 정책 적용 결과 로그 또는 UI 피드백 추가
