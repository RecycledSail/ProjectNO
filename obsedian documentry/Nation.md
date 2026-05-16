# Nation

## 관련 문서

- [[프로젝트 개요]]
- [[GlobalVariables]]
- [[GovernmentBudget]]

## 역할

`Nation`은 게임 안의 국가 하나를 표현하는 핵심 모델 클래스이다.

국가는 소유 지역, 수도, 인구, 자금, 연구, 버프, 군대, 외교 관계, 국가 시장, 정부 예산, 건설 요청과 건설 진행 큐를 가진다. 즉 단순한 데이터 객체가 아니라 국가 단위의 경제, 외교, 건설, 군사 시스템이 연결되는 중심 엔티티이다.

## 위치

- 코드: `Assets/Scripts/Class/Nation.cs`
- 초기 생성: [[GlobalVariables]]의 `LoadNations()`
- 실제 게임 등록: `GameManager.StartNewGame()`
- 저장/불러오기: `SaveManager`
- 주요 참조:
  - `GameManager`
  - `Diplomacy`
  - `GovernmentBudget`
  - `Market`
  - `ConstructionRequest`
  - `EconomicEngine`
  - 국가/군대/건설/예산 UI

## 생성 흐름

`Nation` 객체는 기본 데이터 로드 단계에서 먼저 생성된다.

```csharp
Nation nation = new(n.id, n.name, rnodes);
```

이때 `GlobalVariables.LoadNations()`는 `Nations.json`을 읽고 다음 데이터를 연결한다.

- 국가 ID
- 국가 이름
- 초기 연구 목록
- 국가 색상
- 초기 연대와 분대

하지만 이 시점에는 소유 지역과 수도가 완전히 설정되지 않는다. 실제 새 게임 시작 시 `GameManager.StartNewGame()`에서 `INITIAL_PROVINCES`, `INITIAL_CAPITALS`를 이용해 국가의 지역과 수도를 채운다.

## 주요 필드

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `id` | `int` | 국가 ID |
| `name` | `string` | 국가 이름. 생성 후 변경 불가 |
| `color` | `Color32` | 지도와 UI에서 사용할 국가 색상 |
| `provinces` | `List<Province>` | 국가가 소유한 지역 목록 |
| `capital` | `Province` | 국가 수도 지역 |
| `balance` | `long` | 국가 보유 자금 |
| `regiments` | `List<Regiment>` | 국가 소속 연대 목록 |
| `ethnicGroups` | `Dictionary<(SpeciesSpec, Culture), EthnicGroup>` | 국가 내 종족/문화 조합별 민족 그룹 |
| `allies` | `Dictionary<Nation, Diplomacy>` | 동맹 국가와 해당 외교 관계 |
| `enemies` | `Dictionary<Nation, Diplomacy>` | 적대 국가와 해당 외교 관계 |
| `doneResearches` | `List<ResearchNode>` | 완료된 연구 목록 |
| `buffs` | `Dictionary<BuffKind, double>` | 연구로 얻은 버프 합산값 |
| `constructionRequest` | `ConstructionRequest` | 국가의 건설 예약 목록 |
| `market` | `NationMarket` | 국가 단위 시장 |
| `governmentBudget` | `GovernmentBudget` | [[GovernmentBudget]] 참조. 조세, 보조금, 통화 발행, 인플레이션 처리 |
| `GDP` | `long` | 현재 주 GDP |
| `GDPAverage` | `long` | 최근 4주 평균 GDP |
| `GDPHistory` | `Queue<long>` | GDP 평균 계산용 최근 GDP 기록 |
| `researchFund` | `long` | 연구 자금 |
| `nationManhour` | `double` | 국가가 이번 주 사용할 수 있는 건설 노동력 |
| `buildingsInProgress` | `Queue<Building>` | 현재 건설 중인 일반 건물 큐 |

## 계산 프로퍼티

### `Population`

```csharp
public long Population => provinces.Sum(x => x.population);
```

국가 소유 지역의 인구를 모두 합산한다.

`provinces`가 비어 있으면 0을 반환한다.

## 생성자

```csharp
public Nation(int id, string name, List<ResearchNode> researches)
```

생성자는 국가의 기본 컨테이너와 하위 시스템을 초기화한다.

초기화 내용:

- `provinces`, `regiments`, `ethnicGroups` 생성
- 완료 연구 목록 저장
- 연구 버프를 `buffs`에 합산
- `allies`, `enemies` 생성
- `ConstructionRequest` 생성
- `NationMarket` 생성
- [[GovernmentBudget]] 생성

버프 합산 방식:

1. 완료 연구 목록을 순회한다.
2. 각 연구의 `buffs`를 순회한다.
3. 같은 `BuffKind`가 있으면 기존 값에 더한다.
4. 합산 결과를 `buffs`에 저장한다.

주의: 현재 코드는 `TryGetValue()`로 이전 값을 읽은 뒤 `buffs.Add()`를 호출한다. 같은 `BuffKind`가 두 번 등장하면 이미 키가 존재하므로 예외가 발생할 수 있다. 누적 의도라면 인덱서 대입 방식이 더 안전하다.

## 지역 관리

### `HasProvinces(Province province)`

국가가 특정 지역을 소유하고 있는지 확인한다.

현재 구현은 `List.Find()`와 `Equals()`를 사용한다.

### `AddProvinces(Province province)`

국가에 지역을 추가한다.

동작:

1. 이미 보유한 지역인지 확인한다.
2. 없으면 `provinces`에 추가한다.
3. `province.AddNation(this)`를 호출해 지역 쪽의 `nation` 참조도 갱신한다.
4. 추가 성공 시 `true`, 중복이면 `false`를 반환한다.

이 함수는 국가와 지역의 양방향 연결을 맞추는 역할을 한다.

### `RemoveProvinces(Province province)`

국가에서 지역을 제거한다.

동작:

1. `provinces.Remove(province)`로 국가 목록에서 제거한다.
2. 제거에 성공하면 `province.RemoveNation(this)`를 호출한다.
3. 지역 쪽 참조 제거까지 성공하면 `true`를 반환한다.

## 군대 관리

### `AddRegiment(Regiment regiment)`

국가의 `regiments` 리스트에 연대를 추가한다.

`regiment`가 `null`이면 `false`를 반환하고, 정상 추가되면 `true`를 반환한다.

현재 중복 체크는 하지 않는다.

## 주간 처리

### `SimulateWeeklyTurn()`

국가 단위의 주간 처리를 수행한다.

현재 처리 순서:

```text
CalculateManhour()
ProgressBuild()
```

`GameManager.ProcessWeeklyEvents()`에서 모든 국가에 대해 호출된다.

## 건설 노동력

### `CalculateManhour()`

국가의 이번 주 건설 노동력을 계산한다.

현재 구현은 [[GlobalVariables]]의 `minimumNationManHour`를 그대로 사용한다.

```csharp
currentManhour = GlobalVariables.minimumNationManHour;
nationManhour = currentManhour;
```

추후 인구, 직업, 예산, 기술, 건설 회사 같은 요소가 이 계산에 들어갈 가능성이 있다.

## 건설 진행

### `ProgressBuild()`

`buildingsInProgress` 큐의 맨 앞 건물을 진행한다.

동작:

1. 이번 주 사용 가능한 노동력 `nationManhour`를 가져온다.
2. 건설 큐가 비어 있지 않으면 맨 앞 건물을 확인한다.
3. `building.manhoursLeft`를 노동력만큼 감소시킨다.
4. 남은 노동력이 0 이하가 되면 해당 건물을 지역의 `buildings`에 등록한다.
5. 완료된 건물을 큐에서 제거한다.

현재 한 주에 큐의 첫 번째 건물만 처리한다. 노동력이 남아도 다음 건물로 넘어가지 않는다.

주의: `spentManhour` 계산에 `building.manhoursLeft + 1`이 사용된다.

```csharp
double spentManhour = Math.Min(remainingManhour, building.manhoursLeft + 1);
```

일반적으로는 `building.manhoursLeft`까지만 쓰는 것이 자연스럽다. `+ 1`이 의도된 여유 처리인지 확인이 필요하다.

### `AddToBuildQueue(Building building)`

일반 건물을 건설 큐에 추가한다.

동작:

1. `GlobalVariables.BUILDING_RECIPE[building.buildingType.name].TimeToBuild`에서 건설 시간을 가져온다.
2. `building.manhoursLeft`에 설정한다.
3. `buildingsInProgress` 큐에 넣는다.

건물 타입 이름과 건설 레시피 이름이 일치해야 한다.

### `AddSpecialBuildingToQueue(SpecialBuilding building)`

특수 건물의 `manhoursLeft`를 설정한다.

현재 특수 건물 큐는 주석 처리되어 있어 실제로 큐에 들어가거나 진행되지는 않는다.

## 외교 연결

`Nation`은 외교 관계를 직접 생성하지 않는다.

외교 관계는 `Diplomacy` 객체 생성 시 자동으로 각 국가의 `allies` 또는 `enemies`에 등록된다.

예:

```csharp
Diplomacy diplomacy = new(lnations, rnations, DiplomacyType.ALLY);
```

`Diplomacy.SetNationDiplomacy()`가 실행되면서:

- 동맹이면 서로의 `allies`에 등록하고 `enemies`에서 제거
- 적대이면 서로의 `enemies`에 등록하고 `allies`에서 제거

전투 판정에서는 `regimentA.nation.enemies.ContainsKey(regimentB.nation)` 형태로 적대 관계를 확인한다.

## 시장과 예산

생성자에서 국가 시장과 정부 예산이 함께 생성된다.

```csharp
market = new NationMarket(this.name);
governmentBudget = new GovernmentBudget(market, this);
```

`NationMarket`은 상품별 재고, 가격, 최근 공급량, 최근 수요량을 가진다. 이 시장은 [[GovernmentBudget]]의 인플레이션 계산에도 사용된다.

주간 처리에서 `GameManager`는 도로로 수도와 연결된 지역의 생산품을 국가 시장으로 이동시키고, `EconomicEngine`은 연결 여부에 따라 국가 시장 또는 지역 시장에서 소비를 처리한다.

정부 예산은 주간 처리 마지막 단계에서 다음 처리를 수행한다.

- 세금 징수
- 통화 발행
- 인플레이션 갱신

## GDP

GDP 계산은 `EconomicEngine.Nation.cs`에서 수행한다.

```csharp
gdp += (long)ps.LastSupply * ps.Price;
```

국가 시장에 들어온 상품의 최근 공급량과 현재 가격을 곱해서 주간 GDP를 계산한다.

`UpdateGDPWeekly()`는 각 국가에 대해:

1. 이번 주 GDP 계산
2. `GDPHistory`에 추가
3. 최근 4주를 넘으면 오래된 기록 제거
4. `GDPAverage` 갱신

## 저장/불러오기

`SaveManager`는 국가 저장 시 다음 데이터를 저장한다.

- `id`
- `name`
- 완료 연구 이름 목록
- 소유 지역 이름 목록

불러오기 시에는 [[GlobalVariables]]의 `NATIONS[n.name]`에 있는 기존 국가 객체를 가져와 다음을 갱신한다.

- `doneResearches`
- `provinces`

주의: 저장/불러오기 과정에서 `balance`, `GDP`, `market`, `governmentBudget`, `regiments`, `allies`, `enemies`, `capital` 같은 값은 현재 코드 기준으로 직접 저장되지 않는다.

## 외부 사용 예

| 사용처 | 사용 내용 |
| --- | --- |
| `GameManager.StartNewGame()` | 초기 지역, 수도, 유저 생성 |
| `GameManager.ProcessWeeklyEvents()` | 주간 국가 처리, 시장 이동, GDP, 예산 처리 |
| `Diplomacy` | `allies`, `enemies` 등록/해제 |
| `BattleManager` | 적대 국가 연대끼리 전투 판정 |
| `EconomicEngine` | 국가 시장 소비, GDP 계산 |
| `SaveManager` | 국가 연구/영토 저장 및 복원 |
| `NationUI` | 수도, 인구, 자금, 외교 관계 표시 |
| `FinanceUI` | 정부 예산 정책 조정 |
| `ArmyUI` | 국가 연대 목록 표시 |
| `BuildUI`, `BuildProvinceUI` | 건설 예약과 건설 대상 표시 |

## 주의할 점

### `Nation`은 전역 데이터 객체를 재사용한다

새 게임과 저장 불러오기 모두 [[GlobalVariables]]의 `NATIONS`에 있는 `Nation` 객체를 기반으로 한다.

따라서 `LoadData()`나 새 게임 시작이 여러 번 호출될 때 이전 상태가 남지 않도록 초기화가 중요하다.

### 연구 버프 합산에 예외 가능성이 있다

생성자에서 같은 `BuffKind`가 여러 번 등장하면 `buffs.Add()`가 중복 키 예외를 낼 수 있다.

누적 의도라면 다음 형태가 더 안전하다.

```csharp
buffs[buff.baseBuff] = prevValue + buff.power;
```

### 건설 큐는 한 번에 하나만 진행한다

`ProgressBuild()`는 큐의 첫 건물만 처리한다.

한 주의 노동력이 남아도 다음 건물로 넘어가지 않는다.

### 특수 건물 건설 큐는 아직 미완성이다

`AddSpecialBuildingToQueue()`는 건설 시간만 설정하고 실제 큐에 넣지 않는다. `ProgressBuild()` 안의 특수 건물 처리도 주석 처리되어 있다.

### 저장 데이터가 제한적이다

현재 국가 저장 데이터에는 연구와 영토 중심 정보만 들어간다.

경제 상태, 시장 상태, 외교 상태, 군대 상태, 예산 상태까지 저장해야 한다면 `SaveManager.SaveDataFormat.NationData` 확장이 필요하다.

### `capital` 복원 확인 필요

새 게임에서는 `INITIAL_CAPITALS`로 수도가 설정된다.

하지만 저장 불러오기 흐름에서는 `NationData`에 수도가 없고, 불러오기 코드에서도 수도를 직접 복원하지 않는다. 저장 파일을 불러온 뒤 수도가 유지되는지는 전역 객체 재사용 상태에 의존할 수 있다.

## 개선 후보

- `buffs.Add()`를 안전한 누적 대입으로 변경
- `Nation` 상태 초기화 메서드 추가
- 저장 데이터에 `capital`, `balance`, `market`, `regiments`, `diplomacy`, `governmentBudget` 포함 검토
- 건설 큐가 남은 노동력으로 여러 건물을 이어서 처리하도록 개선
- 특수 건물 건설 큐 구현
- `provinces` 리스트 중복 방지와 `null` 입력 방어 강화
- `Nation`이 너무 많은 책임을 가지므로 경제, 외교, 건설 상태를 별도 컴포넌트로 분리 검토
