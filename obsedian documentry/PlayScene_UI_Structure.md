# PlayScene UI 구조 정리

분석 대상: `Assets/Scenes/PlayScene.unity`

참고 스크립트:

- `Assets/Scripts/UI/UIManager.cs`
- `Assets/Scripts/UI/NationUI.cs`
- `Assets/Scripts/UI/MarketUI.cs`
- `Assets/Scripts/UI/FinanceUI.cs`
- `Assets/Scripts/UI/ProvinceDetailUI.cs`
- `Assets/Scripts/UI/Army/ArmyUI.cs`
- `Assets/Scripts/UI/Army/RegimentDetailUI.cs`
- `Assets/Scripts/UI/Building/BuildUI.cs`
- `Assets/Scripts/UI/Building/BuildProvinceUI.cs`
- `Assets/Scripts/UI/SaveUI.cs`
- `Assets/Scripts/UI/Research/ResearchUI.cs`

관련 문서:

- [[UI 시스템]]: `PlayScene`의 전체 UI 흐름과 팝업 관리 구조
- [[GameManager]]: 날짜 진행, 플레이어 상태, `dayUIEvent` 갱신 흐름
- [[GlobalVariables]]: UI가 참조하는 전역 데이터와 JSON 기반 런타임 데이터
- [[Nation]]: 국가 UI, 시장 UI, 재정 UI, 건설 UI의 중심 데이터
- [[Province]]: 지역 목록, 지역 상세, 지역 시장/건물 UI의 중심 데이터
- [[Market]]: 시장 목록, 상품 가격, 지역/국가 시장 표시와 연결되는 데이터
- [[GovernmentBudget]]: 재정 UI의 세금, 통화량, 정책 예산 데이터
- [[Building]]: 건설 UI와 지역 건물 목록에서 표시되는 건물 데이터
- [[ConstructionRequest]]: 건설 예약과 건설 대기열 UI의 기반 데이터
- [[Diplomacy]]: 외교 목록과 전체 화면 외교 UI의 기반 데이터
- [[Regiment]]: 군대 목록과 연대 상세 UI의 기반 데이터
- [[SaveManager]]: 저장 UI의 저장 실행 흐름
- [[EconomicEngine]]: 경제/시장 갱신 결과가 UI에 반영되는 상위 흐름

## 1. 현재 구조

`PlayScene`의 UI는 하나의 `Canvas` 아래에 크게 네 그룹으로 나뉜다.

```text
Canvas
├─ EventSystem
├─ Buttons
├─ TopUI
├─ PopupUI
├─ FullUI
└─ OpenSaveButton
```

### Buttons

게임 화면에 항상 떠 있는 메인 메뉴 버튼 영역이다.

```text
Buttons
├─ ArmyButton
├─ BuildButton
├─ MarketButton
├─ NationButton
├─ ResearchButton
└─ SaveButton
```

각 버튼은 `Button` 계열 UI와 아이콘 오브젝트를 가진다. 버튼 클릭 시 대응되는 UI의 `Open...UI()` 메서드를 호출하는 용도로 보인다.

관련 UI:

- `ArmyButton` -> `ArmyUI.OpenArmyUI()`
- `BuildButton` -> `BuildUI.OpenBuildUI()`
- `MarketButton` -> `MarketUI.OpenMarketUI()`
- `NationButton` -> `NationUI.OpenNationUI()`
- `SaveButton` 또는 `OpenSaveButton` -> `SaveUI`

### TopUI

상단 상태 표시줄이다. `UIManager`가 [[GameManager]]의 `GameManager.Instance.dayUIEvent`에 연결되어 날짜 단위로 값을 갱신한다.

```text
TopUI
├─ CurrencyUI
│  └─ CurrencyText
├─ GDPUI
│  └─ GDPText
├─ PopulationUI
│  └─ PopulationText
└─ DateFrame
   ├─ Decoration
   └─ DateText
```

표시 값:

- 재화: `CurrencyText`
- GDP: `GDPText`
- 인구: `PopulationText`
- 시간/속도: `DateText`

### PopupUI

일반 팝업 패널 묶음이다. [[UI 시스템]]의 중심 역할을 하는 `UIManager.ReplacePopUp()`이 현재 팝업을 닫고 새 팝업을 열며, 내부적으로 이전 팝업을 스택에 저장한다.

```text
PopupUI
├─ ArmyUI
├─ RegimentDetailUI
├─ BuildUI
├─ BuildProvinceUI
├─ MarketUI
├─ NationUI
├─ FinanceUI
├─ ProvinceDetailUI
└─ SaveUI
```

공통 패턴:

- 루트 패널 오브젝트가 있고, 해당 스크립트의 `uiPanel`에 자기 자신 또는 패널이 연결된다.
- `Start()`에서 대부분 `uiPanel.SetActive(false)`로 닫힌 상태를 만든다.
- 제목 영역, 배경, 닫기 버튼, 탭 버튼, 리스트용 `ScrollRect`를 조합한다.
- 여러 화면을 가진 UI는 `subUIs` 리스트와 `ChangeSubUI(int index)`로 하위 패널을 전환한다.

### ArmyUI

[[Nation]]이 보유한 [[Regiment]] 목록을 보여주는 군대 목록 팝업이다.

```text
ArmyUI
├─ ArmyTitle
│  └─ ArmyTitleText
├─ ArmyBack
├─ ArmyClose
└─ RegimentListView
   └─ Viewport
      └─ Content
```

동작:

- `OpenArmyUI()`에서 [[GameManager]]를 통해 플레이어 [[Nation]]을 가져온다.
- `InitRegimentList()`에서 `Content` 자식을 비우고 `regimentItemPrefab`을 생성한다.
- 생성된 항목은 `RegimentButtonUI.SetRegimentData()`로 데이터를 받는다.

### RegimentDetailUI

부대 상세 팝업이다.

```text
RegimentDetailUI
├─ RegimentDetailTitle
│  └─ RegimentTitleText
├─ RegimentDetailBack
├─ TotalPopArea
├─ HometownArea
├─ RegimentDetailClose
└─ SquadListView
   └─ Viewport
      └─ Content
```

역할:

- 선택한 [[Regiment]]의 총 병력, 출신지, 분대 목록을 표시하는 상세 화면이다.
- `SquadListView`의 `Content`에 분대 프리팹을 동적으로 넣는 구조다.

### BuildUI

건설/생산 관련 메인 팝업이다.

```text
BuildUI
├─ ToBuildButton
├─ ToQueueButton
├─ ToRoadButton
├─ BuildTitle
├─ BuildBack
├─ BuildClose
├─ BuildProduct
│  ├─ BuildListView
│  │  └─ Viewport
│  │     └─ Content
│  └─ BuildDetail
│     ├─ Background
│     ├─ Frame
│     ├─ DetailedProductUI
│     ├─ AutoBuildButton
│     └─ ManualBuildButton
├─ BuildQueue
│  └─ Viewport
│     └─ Content
└─ BuildRoad
   ├─ RoadInfoText
   └─ Viewport
      └─ Content
```

동작:

- `BuildProduct`, `BuildQueue`, `BuildRoad`가 `subUIs`로 전환된다.
- `InitBuildList()`가 [[GlobalVariables]]의 `GlobalVariables.PRODUCTS.Keys`를 순회하며 `BuildItemPrefab`을 `BuildListParent`에 생성한다.
- `ManualBuildButton`은 `OnManualButtonClick()`으로 `BuildProvinceUI`를 연다.
- `UpdateQueue()`는 [[ConstructionRequest]]의 건설 예약 목록을 `BuildQueueItemPrefab`으로 표시한다.

### BuildProvinceUI

특정 [[Building]]을 지을 [[Province]]를 선택하는 팝업이다.

```text
BuildProvinceUI
├─ BuildProvinceTitle
│  └─ BuildProvinceTitleText
├─ BuildProvinceBack
├─ BuildProvinceClose
└─ BuildProvinceListView
   └─ Viewport
      └─ Content
```

동작:

- `OpenBuildProvinceUI(BuildingType buildingType)`에서 대상 건물 타입을 받는다.
- `UpdateBuildProvinceList()`가 현재 [[Nation]]의 [[Province]] 목록을 비교하며 필요한 항목만 추가/삭제한다.
- 항목 프리팹은 `BuildProvinceButtonUI.SetBuildingData(province, currentBuildingType)`로 초기화된다.

### MarketUI

[[Market]] 정보를 보여주는 팝업이다.

```text
MarketUI
├─ MarketTitle
├─ ToMarketListButton
├─ ToStatsButton
├─ MarketBack
├─ MarketClose
├─ MarketListView
│  ├─ Viewport
│  │  └─ Content
│  └─ Scrollbar Vertical
└─ NationStatsView
   └─ StatsText
```

동작:

- `MarketListView`와 `NationStatsView`를 `subUIs`로 전환하는 구조다.
- `InitMarketList()`는 현재 비어 있고 TODO 상태다.

### NationUI

[[Nation]] 정보 팝업이다.

```text
NationUI
├─ NationTitle
├─ ToProvinceListButton
├─ ToDiplomacyListButton
├─ ToStatsButton
├─ NationBack
├─ NationClose
├─ ProvinceListView
│  └─ Viewport
│     └─ Content
├─ DiplomacyListView
│  └─ Viewport
│     └─ Content
│        ├─ EnemyContent
│        └─ AllyContent
└─ NationStatsView
   └─ StatsText
```

동작:

- `ProvinceListView`, `DiplomacyListView`, `NationStatsView`가 `subUIs`로 전환된다.
- `InitProvinceList()`는 [[Nation]]의 [[Province]]를 `ProvinceButton` 프리팹으로 생성한다.
- `InitDiplomacyStats()`는 [[Diplomacy]] 관계를 기준으로 `AllyContent`, `EnemyContent` 아래에 국가 버튼을 동적으로 생성한다.
- `InitNationStats()`는 수도, 인구, 재화를 텍스트로 갱신한다.

### FinanceUI

[[GovernmentBudget]] 기반의 재정/예산 팝업이다.

```text
FinanceUI
├─ FinanceTitle
├─ GoBackButton
├─ CloseButton
└─ ListView
   └─ Viewport
      └─ Content
         ├─ GoldInflationSection
         ├─ YearFinanceSection
         └─ FinanceSelectSection
            ├─ MilitaryBudgetArea
            ├─ IndustryBudgetArea
            ├─ RealEstateBudgetArea
            └─ ResearchBudgetArea
```

동작:

- 각 예산 영역은 `Slider`와 텍스트 표시를 가진다.
- `OpenFinanceUI()`에서 현재 [[Nation]]을 설정하고 `InitFinanceView()`를 호출한다.
- 슬라이더 값은 0~100의 정수 퍼센트로 고정된다.
- 값 변경 시 [[GovernmentBudget]]의 `GovernmentBudget.Policy`에 금액으로 환산되어 반영된다.

### ProvinceDetailUI

[[Province]] 상세 팝업이다.

```text
ProvinceDetailUI
├─ ToMarketStatsButton
├─ ToBuildingButton
├─ ProvinceTitle
├─ ProvinceDescText
├─ ProvinceBack
├─ ProvinceClose
├─ ProvinceStatsView
│  └─ Viewport
│     └─ Content
├─ ProvinceMarketView
│  └─ Viewport
│     └─ Content
└─ ProvinceBuildingView
   └─ Viewport
      └─ Content
```

동작:

- `ProvinceStatsView`, `ProvinceMarketView`, `ProvinceBuildingView`가 `subUIs`로 전환된다.
- `OpenProvinceDetailUI(Province province)`에서 [[Province]] 이름, 지형 설명, [[Market]]/[[Building]] 리스트를 초기화한다.
- [[Market]] 항목은 `ProductButtonUI`, [[Building]] 항목은 `ProvinceBuildingButtonUI`로 생성된다.

### SaveUI

세이브 목록 및 저장 입력 팝업이다.

```text
SaveUI
├─ SaveTitle
├─ SaveClose
├─ Scroll View
│  └─ Viewport
│     └─ Content
├─ SaveFileInput
│  └─ Text Area
│     ├─ Placeholder
│     └─ Text
└─ SaveButton
```

동작:

- `OnEnable()`에서 저장 파일 목록을 읽고 `buttonPrefab`을 `content` 아래에 생성한다.
- `OnDisable()`에서 생성된 버튼들을 정리한다.
- `OnSaveButtonClick()`은 입력 필드 값을 [[GlobalVariables]]의 `GlobalVariables.saveFileName`에 넣고 [[SaveManager]]로 저장 후 `MainMenuScene`으로 이동한다.

### FullUI

화면 전체를 쓰는 대형 UI 묶음이다. 현재 `ResearchUI`, `DiplomacyUI`는 비활성 상태로 배치되어 있다. 특히 `DiplomacyUI`는 [[Diplomacy]] 흐름과 연결될 UI로 볼 수 있다.

```text
FullUI
├─ ResearchUI
│  ├─ TitlePanel
│  ├─ EraButtons
│  ├─ ReseachClose
│  ├─ Researches
│  └─ DetailTextBackground
└─ DiplomacyUI
   ├─ TitlePanel
   ├─ EraButtons
   ├─ CancelButton
   ├─ ConfirmButton
   ├─ ResetButton
   ├─ Researches
   ├─ PlayerOneBackground
   └─ PlayerTwoBackground
```

특징:

- `PopupUI`처럼 작은 패널이 아니라 중앙의 큰 화면형 UI다.
- `ResearchUI.cs`는 현재 빈 스크립트에 가깝고 실제 동작 구현이 거의 없다.
- `DiplomacyUI`는 씬에는 구성되어 있지만 [[Diplomacy]] 대응 전용 스크립트는 아직 확인되지 않는다.

### 씬에서 직접 연결된 UI 프리팹

`PlayScene.unity`에서 UI 스크립트 필드에 직접 연결된 프리팹은 아래와 같다. 대부분 `ScrollRect > Viewport > Content` 아래에 런타임으로 생성되는 리스트 아이템 프리팹이다.

| 씬 오브젝트 | 스크립트 | 필드 | 연결 프리팹 | 용도 |
| --- | --- | --- | --- | --- |
| `ArmyUI` | `ArmyUI` | `regimentItemPrefab` | `Assets/Prefabs/UI/UI/Army/RegimentButton.prefab` | [[Regiment]] 목록 버튼 |
| `RegimentDetailUI` | `RegimentDetailUI` | `squadPrefab` | `Assets/Prefabs/UI/UI/Army/RegimentSquadUI.prefab` | 분대 상세 행 |
| `BuildUI` | `BuildUI` | `BuildItemPrefab` | `Assets/Prefabs/UI/UI/Build/ProduceButton.prefab` | 생산/건설 상품 버튼 |
| `BuildUI` | `BuildUI` | `BuildQueueItemPrefab` | `Assets/Prefabs/UI/UI/Build/BuildQueueItem.prefab` | [[ConstructionRequest]] 기반 건설 대기열 행 |
| `BuildProvinceUI` | `BuildProvinceUI` | `BuildItemPrefab` | `Assets/Prefabs/UI/UI/Build/BuildProvinceButton.prefab` | 건설 대상 [[Province]] 버튼 |
| `NationUI` | `NationUI` | `provinceItemPrefab` | `Assets/Prefabs/UI/UI/Button/ProvinceButton.prefab` | [[Nation]] 소속 [[Province]] 버튼 |
| `NationUI` | `NationUI` | `nationItemPrefab` | `Assets/Prefabs/UI/UI/Button/NationButton.prefab` | [[Diplomacy]]용 동맹/적국 [[Nation]] 버튼 |
| `ProvinceDetailUI` | `ProvinceDetailUI` | `marketChild` | `Assets/Prefabs/UI/UI/Province/ProductButton.prefab` | [[Province]] [[Market]] 상품 행 |
| `ProvinceDetailUI` | `ProvinceDetailUI` | `buildingChild` | `Assets/Prefabs/UI/UI/Province/BuildingButton.prefab` | [[Province]] [[Building]] 행 |
| `SaveUI` | `SaveUI` | `buttonPrefab` | `Assets/Prefabs/UI/UI/Button/SaveButton.prefab` | 저장 파일 선택 버튼 |

현재 `MarketUI`에는 `MarketItemPrefab` 필드가 있지만, `PlayScene.unity` 기준으로는 `Assets/Prefabs/UI` 아래 프리팹과 직접 연결된 항목이 확인되지 않는다. `InitMarketList()`도 TODO 상태라 [[Market]] 목록 기능을 붙일 때 프리팹 연결과 생성 로직을 함께 정리해야 한다.

프리팹 연결 흐름은 다음과 같다.

```text
UI 루트 오브젝트
└─ UI 스크립트
   ├─ Content Transform 필드
   └─ Item Prefab 필드
      └─ Instantiate(itemPrefab, contentParent)
```

## 2. 유사한 형태를 만드는 방법

### A. 메인 버튼 구조 만들기

사용 예: `ArmyButton`, `BuildButton`, `MarketButton`

1. `Canvas > Buttons` 아래에 새 `Button` 오브젝트를 만든다.
2. 버튼 안에 아이콘용 `Image` 자식을 둔다.
3. `Button.onClick`에 대상 UI 스크립트의 `Open...UI()` 메서드를 연결한다.
4. 기존 Steampunk UI 프리팹을 쓰려면 `Assets/Gentleland/SteampunkUI/Prefabs/Icon Buttons`의 아이콘 버튼을 기반으로 만든다.
5. 새 버튼이 여는 UI가 팝업이라면 `Open...UI()` 내부에서 `UIManager.Instance.ReplacePopUp(gameObject)`을 호출한다.

권장 구조:

```text
Buttons
└─ NewFeatureButton
   └─ FeatureIcon
```

### B. 상단 상태 표시 UI 만들기

사용 예: `CurrencyUI`, `GDPUI`, `PopulationUI`, `DateFrame`

1. `Canvas > TopUI` 아래에 프레임 또는 이미지 오브젝트를 만든다.
2. 자식으로 `TMP_Text`를 둔다.
3. `UIManager` 또는 별도 상태 UI 스크립트에 `TMP_Text` 필드를 추가한다.
4. [[GameManager]]의 `GameManager.Instance.dayUIEvent`에 갱신 메서드를 연결한다.
5. 숫자 표시는 `UIManager.ShortenValue(long)` 같은 공통 포맷터를 재사용한다.

권장 구조:

```text
TopUI
└─ NewStatusUI
   └─ NewStatusText
```

### C. 일반 팝업 패널 만들기

사용 예: `ArmyUI`, `MarketUI`, `BuildProvinceUI`

1. `Canvas > PopupUI` 아래에 루트 패널을 만든다.
2. 루트 패널에 전용 MonoBehaviour를 붙이고 `uiPanel`에 루트 패널을 연결한다.
3. 제목, 배경, 닫기 버튼, 리스트 영역을 자식으로 구성한다.
4. `Start()`에서 `uiPanel.SetActive(false)`를 호출해 초기에는 닫아둔다.
5. 열기 메서드에서 필요한 데이터를 세팅하고 `UIManager.Instance.ReplacePopUp(gameObject)`을 호출한다.
6. 닫기 버튼은 해당 UI의 `Close...UI()` 또는 `UIManager.Instance.ClosePopUp()`에 연결한다.

권장 구조:

```text
PopupUI
└─ NewPopupUI
   ├─ NewTitle
   │  └─ NewTitleText
   ├─ NewBack
   ├─ NewClose
   └─ NewListView
      └─ Viewport
         └─ Content
```

### D. 탭 전환 팝업 만들기

사용 예: `BuildUI`, `NationUI`, `ProvinceDetailUI`

1. 루트 패널 아래에 탭 버튼들을 만든다.
2. 각 탭에 대응하는 하위 패널을 만든다.
3. 스크립트에 `public List<GameObject> subUIs`와 `currentOpenSubUI`를 둔다.
4. `Start()`에서 첫 번째 패널만 켜고 나머지는 끈다.
5. 탭 버튼의 `onClick`에 `ChangeSubUI(index)`를 연결한다.

권장 구조:

```text
NewTabbedUI
├─ ToFirstTabButton
├─ ToSecondTabButton
├─ FirstTab
└─ SecondTab
```

핵심 코드 패턴:

```csharp
public void ChangeSubUI(int index)
{
    currentOpenSubUI.SetActive(false);
    subUIs[index].SetActive(true);
    currentOpenSubUI = subUIs[index];
}
```

### E. 동적 리스트 만들기

사용 예: `RegimentListView`, `ProvinceListView`, `BuildListView`, `SaveUI`의 `Scroll View`

1. `Scroll View`를 만들고 `Viewport > Content` 구조를 유지한다.
2. `Content`에 `VerticalLayoutGroup` 또는 필요한 Layout Group을 붙인다.
3. 항목 프리팹을 만든 뒤, 항목 스크립트에 `Set...Data()` 초기화 메서드를 둔다.
4. UI 스크립트에 `Transform contentParent`, `GameObject itemPrefab` 필드를 둔다.
5. Inspector에서 `Content` 오브젝트를 부모 Transform 필드에 연결하고, 항목 프리팹을 Prefab 필드에 연결한다.
6. 갱신 시 기존 자식을 제거하거나, `BuildProvinceUI`처럼 기존 항목을 재사용한다.

현재 씬의 대표 연결 예:

| 리스트 | Content 필드 | 프리팹 필드 | 연결 프리팹 |
| --- | --- | --- | --- |
| 연대 목록 | `ArmyUI.regimentListParent` | `ArmyUI.regimentItemPrefab` | `RegimentButton.prefab` |
| 분대 목록 | `RegimentDetailUI.squadParent` | `RegimentDetailUI.squadPrefab` | `RegimentSquadUI.prefab` |
| 생산 목록 | `BuildUI.BuildListParent` | `BuildUI.BuildItemPrefab` | `ProduceButton.prefab` |
| 건설 대기열 | `BuildUI.BuildQueueParent` | `BuildUI.BuildQueueItemPrefab` | `BuildQueueItem.prefab` |
| 건설 [[Province]] 목록 | `BuildProvinceUI.BuildListParent` | `BuildProvinceUI.BuildItemPrefab` | `BuildProvinceButton.prefab` |
| [[Nation]] 소속 [[Province]] 목록 | `NationUI.provinceListParent` | `NationUI.provinceItemPrefab` | `ProvinceButton.prefab` |
| [[Diplomacy]] 국가 목록 | `NationUI.allyListParent`, `NationUI.enemyListParent` | `NationUI.nationItemPrefab` | `NationButton.prefab` |
| [[Province]] [[Market]] 목록 | `ProvinceDetailUI.marketPanel` | `ProvinceDetailUI.marketChild` | `ProductButton.prefab` |
| [[Province]] [[Building]] 목록 | `ProvinceDetailUI.buildingPanel` | `ProvinceDetailUI.buildingChild` | `BuildingButton.prefab` |
| 세이브 파일 목록 | `SaveUI.content` | `SaveUI.buttonPrefab` | `SaveButton.prefab` |

기본 생성 패턴:

```csharp
foreach (Transform child in contentParent)
{
    Destroy(child.gameObject);
}

foreach (var data in dataList)
{
    GameObject item = Instantiate(itemPrefab, contentParent);
    item.GetComponent<ItemUI>().SetData(data);
}
```

### F. 슬라이더 기반 설정 UI 만들기

사용 예: `FinanceUI`

1. 각 설정 항목을 `Text + Slider + ValueText` 형태로 만든다.
2. `Slider.minValue = 0`, `Slider.maxValue = 100`, `Slider.wholeNumbers = true`로 설정한다.
3. 스크립트에서 `onValueChanged`를 연결한다.
4. 데이터에서 UI로 값을 넣을 때는 이벤트 중복 반영을 막는 `_suppressSliderEvents` 같은 플래그를 둔다.
5. 총합이 제한을 넘는 경우 텍스트 색상이나 경고 표시를 갱신한다.

권장 구조:

```text
FinanceSelectSection
└─ NewBudgetArea
   ├─ Text
   ├─ Slider
   │  ├─ Background
   │  ├─ Fill
   │  └─ ForeGround
   └─ PercentText
```

### G. 화면 전체형 UI 만들기

사용 예: `ResearchUI`, `DiplomacyUI`

1. `Canvas > FullUI` 아래에 루트 패널을 만든다.
2. 팝업보다 큰 기준 크기를 사용하고 중앙 배치한다.
3. 제목, 좌우 리스트, 상세 패널, 확인/취소 버튼을 분리한다.
4. 일반 팝업과 동시에 열리지 않게 [[UI 시스템]]의 `UIManager`와 연동하거나 별도 전체 화면 UI 관리자를 둔다.
5. `ResearchUI`처럼 씬 구조만 먼저 만들 경우에도 최소한 열기/닫기/선택 상태 초기화 메서드는 준비해두는 편이 좋다.

## 3. 개선점

### 구조 개선

- `PopupUI`와 `FullUI`의 열기/닫기 방식이 섞여 있으므로 `BasePopupUI` 또는 `UIPanelController` 같은 공통 기반 클래스를 만들면 중복이 줄어든다.
- `uiPanel`이 대부분 자기 자신을 가리키므로, 필드가 비어 있으면 `gameObject`를 자동 사용하도록 하면 Inspector 연결 실수를 줄일 수 있다.
- `ChangeSubUI(int index)` 패턴이 여러 스크립트에 반복된다. 공통 탭 컨트롤러로 분리하면 탭 추가/순서 변경이 쉬워진다.
- 닫기 버튼은 각 UI별 `Close...UI()`와 `UIManager.ClosePopUp()` 중 하나로 규칙을 통일하는 편이 좋다.

### 리스트 성능 개선

- `InitProvinceList()`, `InitRegimentList()`, `InitBuildList()`는 매번 모든 자식을 삭제하고 다시 만든다. [[Province]], [[Regiment]], 상품 항목 수가 늘어나면 GC와 프레임 드랍이 생길 수 있다.
- `BuildProvinceUI.UpdateBuildProvinceList()`처럼 기존 항목을 재사용하는 방식으로 다른 리스트도 바꾸면 좋다.
- 자주 갱신되는 목록은 오브젝트 풀링을 적용하면 안정적이다.

### Inspector 안정성 개선

- 동적 리스트 필드는 이름이 제각각이다. `BuildListParent`, `MarketListParent`, `provinceListParent` 등을 `contentParent` 또는 `xxxContent` 규칙으로 맞추면 유지보수가 쉽다.
- `realEstateSlide`는 다른 필드와 맞춰 `realEstateSlider`로 이름을 고치는 것이 좋다.
- `MarketUI.InitMarketList()`가 TODO 상태이고 `MarketItemPrefab`도 현재 씬에서 프리팹 연결이 확인되지 않는다. [[Market]] 목록용 프리팹을 정하고 Inspector 연결까지 완료해야 한다.
- `ResearchUI.cs`가 비어 있어 `FullUI > ResearchUI`의 씬 구성과 실제 동작 사이에 차이가 있다.

### UI/UX 개선

- 일반 팝업들은 대체로 `650 x 800` 크기와 좌측 배치가 반복된다. 공통 패널 프리셋을 프리팹화하면 화면 톤을 맞추기 쉽다.
- `TopUI` 텍스트가 날짜 이벤트에서만 갱신되므로, 씬 시작 직후 첫 이벤트 전까지 값이 비어 있거나 오래된 값일 수 있다. `Start()`에서 한 번 즉시 갱신하는 편이 좋다.
- 탭 버튼의 선택 상태가 시각적으로 드러나는지 확인이 필요하다. 현재 구조만 보면 활성 탭 강조용 오브젝트나 색상 변경 로직은 뚜렷하지 않다.
- 스크롤뷰가 많은데 빈 리스트일 때 안내 텍스트가 없다. `No items` 상태를 두면 디버깅과 UX 모두 좋아진다.

### 코드 품질 개선

- 여러 UI 스크립트에 Singleton 코드가 반복된다. 공통 베이스나 간단한 유틸로 줄일 수 있다.
- [[GameManager]]의 `GameManager.Instance.dayUIEvent` 구독은 대부분 `OnDestroy()`에서 해제하고 있어 좋다. 다만 `GameManager.Instance` 접근 전에 null 체크가 필요한 곳이 일부 있다.
- 주석 인코딩이 깨진 파일이 많다. UTF-8로 복구하거나 한글 주석을 새로 정리하면 이후 협업 시 이해 비용이 크게 줄어든다.
- `UpdateMarketUI()`는 `currentNation` 체크 없이 `InitMarketList()`를 호출한다. 현재 메서드가 비어 있어 문제는 작지만, 구현이 추가되면 null 접근 위험이 생길 수 있다.

### 추천 리팩터링 순서

1. `UIManager`의 팝업 열기/닫기 규칙을 정리한다.
2. `TabbedPanelController`를 만들어 `subUIs` 전환 중복을 제거한다.
3. `ScrollListBinder<T>` 또는 각 UI별 재사용 리스트 패턴을 만든다.
4. 반복 패널인 제목/닫기/배경/스크롤뷰를 프리팹화한다.
5. `MarketUI`, `ResearchUI`, `DiplomacyUI`처럼 씬 구성은 있으나 동작이 부족한 UI부터 기능을 연결한다.
