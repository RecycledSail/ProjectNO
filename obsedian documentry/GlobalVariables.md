# GlobalVariables

## 관련 문서

- [[프로젝트 개요]]
- [[Nation]]
- [[GovernmentBudget]]
- [[건설 시스템 현황]]

## 역할

`GlobalVariables`는 게임 시작 시 `Assets/Resources` 아래의 JSON 데이터를 읽어 런타임에서 사용할 전역 데이터 저장소를 구성하는 정적 클래스이다.

이 클래스는 국가, 지역, 상품, 건물, 연구, 병종, 문화, 종족, 외교 초기값 같은 게임의 기본 데이터를 `Dictionary` 형태로 보관한다. 다른 시스템은 이곳에 저장된 데이터를 참조해서 UI를 구성하거나, 경제/전투/저장 로직을 실행한다.

## 위치

- 코드: `Assets/Scripts/GlobalVariables.cs`
- 데이터 원본: `Assets/Resources/*.json`
- 주요 호출 위치:
  - `GameManager`
  - `MainMenu`
  - `SaveManager`
  - 각종 UI 스크립트
  - 경제/국가/건물 관련 클래스

## 핵심 책임

1. `Resources.Load<TextAsset>()`로 JSON 파일을 읽는다.
2. `JsonUtility.FromJson<T>()`로 JSON을 임시 데이터 구조체로 역직렬화한다.
3. 임시 데이터 구조체를 실제 게임 모델 객체로 변환한다.
4. 변환된 객체를 전역 `Dictionary`에 저장한다.
5. 데이터 간 참조 관계를 연결한다.

## 초기화 흐름

`LoadData()`는 Unity의 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 속성으로 등록되어 있다. 따라서 씬 로드 전에 자동 실행된다.

현재 로드 순서는 다음과 같다.

```text
LoadBuff()
LoadResearchNodes()
LoadUnitTypes()
LoadSpeciesSpecs()
LoadCulture()
LoadJobTypes()
LoadBuildingTypes()
LoadSpecialBuildingTypes()
LoadCategories()
LoadBuildingRecipes()
LoadProducts()
LoadProvinces()
LoadNations()
LoadAdjacentProvinces()
LoadInitialProvinces()
LoadInitialDiplomacies()
```

이 순서는 중요하다. 뒤쪽 데이터가 앞쪽 데이터에 의존하기 때문이다.

예를 들어:

- `ResearchNodes`는 `Buffs`를 참조한다.
- `Products`는 `Categories`를 참조한다.
- `Provinces`는 `SpeciesSpecs`, `Culture`, `BuildingTypes`, `SpecialBuildingTypes`, `Products`를 참조한다.
- `Nations`는 `ResearchNodes`, `UnitTypes`, `Provinces`를 참조하고 [[Nation]] 객체를 생성한다.
- `AdjacentProvinces`는 이미 생성된 `Provinces`를 참조한다.

## 전역 데이터 목록

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `buildingStartBalance` | `int` | 건물이 생성될 때 가지는 초기 자금 |
| `minimumNationManHour` | `double` | 국가가 최소로 가지는 노동력 기준값 |
| `BUFF` | `Dictionary<string, Buff>` | 버프 이름을 키로 하는 버프 목록 |
| `RESEARCH_NODE` | `Dictionary<string, ResearchNode>` | 연구 이름을 키로 하는 연구 노드 목록 |
| `UNIT_TYPE` | `Dictionary<string, UnitType>` | 병종 이름을 키로 하는 병종 목록 |
| `UNIT_TYPES` | `Dictionary<string, UnitType>` | 병종 저장소로 보이나 현재 로드 로직에서는 사용되지 않음 |
| `SPECIES_SPEC` | `Dictionary<string, SpeciesSpec>` | 종족 이름을 키로 하는 종족 기본 스펙 |
| `CULTURE` | `Dictionary<string, Culture>` | 문화 이름을 키로 하는 문화 목록 |
| `NATIONS` | `Dictionary<string, Nation>` | 국가 이름을 키로 하는 국가 목록 |
| `INITIAL_PROVINCES` | `Dictionary<string, List<string>>` | 국가별 초기 소유 지역 이름 목록 |
| `INITIAL_CAPITALS` | `Dictionary<string, string>` | 국가별 초기 수도 지역 이름 |
| `CATEGORIES` | `Dictionary<string, List<string>>` | 상품 카테고리별 상품 이름 목록 |
| `PROVINCES` | `Dictionary<string, Province>` | 지역 이름을 키로 하는 지역 목록 |
| `ADJACENT_PROVINCES` | `Dictionary<string, List<Province>>` | 지역별 인접 지역 목록 |
| `saveFileName` | `string` | 현재 선택된 저장 파일 이름 |
| `BUILDING_TYPE` | `Dictionary<string, BuildingType>` | 일반 건물 타입 목록 |
| `SPECIAL_BUILDING_TYPE` | `Dictionary<string, SpecialBuildingType>` | 특수 건물 타입 목록 |
| `PRODUCT_TO_BUILDING` | `Dictionary<string, string>` | 생산품 이름에서 생산 건물 이름으로 가는 역참조 |
| `JOB_TYPE` | `Dictionary<string, JobType>` | 직업 타입 목록 |
| `PRODUCTS` | `Dictionary<string, Products>` | 상품 이름을 키로 하는 상품 가격 정보 |
| `BUILDING_RECIPE` | `Dictionary<string, BuildingRecipe>` | 건물 이름을 키로 하는 건설 레시피 |

## JSON 로드 구조

공통 로더는 `LoadJsonFile<T>()`이다.

```csharp
public static T LoadJsonFile<T>(string jsonName)
```

동작:

1. `Resources.Load<TextAsset>(jsonName)`으로 `{jsonName}.json`을 찾는다.
2. 파일이 없으면 에러 로그를 남기고 예외를 던진다.
3. 파일이 있으면 `JsonUtility.FromJson<T>()`로 변환한다.
4. 변환된 wrapper 객체를 반환한다.

Unity `JsonUtility`는 최상위 배열을 직접 읽지 못하므로, `GameDataFormat` 내부에 `BuffsWrapper`, `ProvincesWrapper` 같은 wrapper 클래스가 정의되어 있다.

## 주요 로드 함수

### `LoadBuff()`

`Buffs.json`을 읽어 `Buff` 객체를 생성하고 `BUFF`에 저장한다.

키는 `b.name`이다.

### `LoadResearchNodes()`

`ResearchNodes.json`을 읽어 `ResearchNode`를 생성한다.

각 연구 노드는 `buffNames`를 통해 이미 로드된 `BUFF`에서 버프를 찾아 연결한다.

### `LoadUnitTypes()`

`UnitTypes.json`을 읽어 병종별 공격력, 방어력, 이동 속도를 가진 `UnitType`을 생성한다.

현재 데이터는 `UNIT_TYPE`에 저장된다.

### `LoadSpeciesSpecs()`

`SpeciesSpecs.json`을 읽어 종족별 기본 출생률 정보를 `SPECIES_SPEC`에 저장한다.

### `LoadCulture()`

`Culture.json`을 읽어 `Culture` 객체를 만든다.

현재는 문화 이름과 빈 `Traits` 리스트만 초기화한다.

### `LoadJobTypes()`

`JobTypes.json`을 읽어 직업의 문해력 요구 여부와 급여 정보를 `JOB_TYPE`에 저장한다.

### `LoadBuildingTypes()`

`BuildingTypes.json`을 읽어 일반 건물 타입을 생성한다.

처리 내용:

- 생산품 목록을 `produceItems`에 저장
- 필요 자원 목록을 `requireItems`에 저장
- 노동자 필요량을 `workerNeeded`에 저장
- 생산품에서 건물로 역참조하기 위해 `PRODUCT_TO_BUILDING`을 채움

### `LoadSpecialBuildingTypes()`

`SpecialBuildingTypes.json`을 읽어 특수 건물 타입을 생성한다.

특수 건물은 노동자 필요량, 우선순위, 버프 이름 목록을 가진다.

### `LoadCategories()`

`Categories.json`을 읽어 상품 카테고리 이름을 등록한다.

각 카테고리의 값은 빈 상품 이름 리스트로 시작한다.

### `LoadProducts()`

`Products.json`을 읽어 상품 가격 정보를 `PRODUCTS`에 저장한다.

또한 각 상품을 해당 카테고리의 리스트에 추가한다. 따라서 `LoadCategories()`가 먼저 실행되어야 한다.

### `LoadBuildingRecipes()`

`Buildingrecipes.json`을 읽어 건설에 필요한 자원과 건설 시간을 `BUILDING_RECIPE`에 저장한다.

주의: 실제 리소스 파일명은 `BuildingRecipes.json`이고, 코드에서 호출하는 이름은 `"Buildingrecipes"`이다. Unity `Resources.Load` 경로가 대소문자를 어떻게 처리하는지 플랫폼별 차이가 생길 수 있으므로 이름 통일이 필요할 수 있다.

### `LoadProvinces()`

`Provinces.json`을 읽어 지역 객체를 생성한다.

처리 내용:

- 지형 문자열을 `Topography` enum으로 변환
- 인구 데이터를 `SpeciesSpec`, `Culture`, `EthnicGroup`, `ProvinceEthnicPop`으로 변환
- 일반 건물 데이터를 `Building` 객체로 변환
- 기본 특수 건물 `City`, `Town`을 추가
- JSON에 명시된 특수 건물을 추가 또는 덮어씀
- `InitializePopulation()` 호출
- 지역 시장 `ProvinceMarket` 생성
- 모든 상품을 지역 시장에 초기 가격으로 등록

### `LoadNations()`

`Nations.json`을 읽어 [[Nation]] 객체를 생성한다.

처리 내용:

- 연구 이름을 `RESEARCH_NODE`에서 찾아 국가 연구 목록으로 연결
- 색상 데이터가 있으면 `Color32`로 변환
- 연대와 분대를 생성
- 분대의 병종은 `UNIT_TYPE`에서 찾음
- 연대 위치는 `PROVINCES`에서 찾음

### `LoadAdjacentProvinces()`

`AdjacentProvinces.json`을 읽어 지역별 인접 지역 목록을 생성한다.

값은 문자열이 아니라 실제 `Province` 객체 리스트이다.

### `LoadInitialProvinces()`

`InitialProvinces.json`을 읽어 국가별 초기 영토와 수도 정보를 저장한다.

`GameManager`는 이 정보를 사용해 각 지역의 초기 소유 국가를 설정한다.

### `LoadInitialDiplomacies()`

`InitialDiplomacies.json`을 읽어 초기 외교 관계를 생성한다.

`ALLY`, `ENEMY` 문자열을 `DiplomacyType`으로 변환한다.

현재 함수 내부에서 `Diplomacy` 객체를 생성하지만, 별도 전역 목록에 저장하지는 않는다. `Diplomacy` 생성자 내부에서 자체 등록을 수행하지 않는다면 생성된 관계가 유지되지 않을 수 있다.

## 저장 파일 목록

`GetAllJsonFileNames()`는 `Application.persistentDataPath`에서 `.json` 파일 목록을 읽어 확장자를 제거한 이름 리스트를 반환한다.

이 함수는 저장/불러오기 UI에서 저장 파일 선택 목록을 만들 때 사용된다.

## GameDataFormat

`GameDataFormat`은 JSON 역직렬화를 위한 내부 데이터 클래스 모음이다.

크게 두 종류로 나뉜다.

- Wrapper 클래스: JSON 최상위 객체를 감싸는 클래스
- Data 클래스: JSON의 실제 항목 하나를 표현하는 클래스

예:

```json
{
  "products": [
    {
      "name": "grain",
      "initialPrice": 10,
      "category": "basic_food"
    }
  ]
}
```

위 JSON은 `ProductsWrapper`와 `ProductsData`로 읽힌다.

## 외부 사용 예

| 사용처 | 사용 데이터 |
| --- | --- |
| `GameManager` | 국가, 지역, 초기 영토, 인접 지역, 상품 |
| `SaveManager` | 저장 데이터 복원 시 국가/지역/연구 참조 |
| `BuildUI` | 상품, 건물 타입, 상품-건물 역참조 |
| `BuildProvinceUI` | 건물 타입 목록 |
| `EconomicEngine` | 상품 카테고리, 상품 가격 |
| `Nation` | 최소 노동력, 건설 레시피 |
| `Building` | 건물 초기 자금 |
| `MainMenu`, `SaveUI`, `LoadSelectUI` | 저장 파일 이름 |

## 주의할 점

### 정적 Dictionary가 누적될 수 있음

`LoadData()`는 여러 번 호출될 수 있다. 예를 들어 메인 메뉴에서도 직접 호출한다.

현재 대부분의 Dictionary는 로드 전에 `Clear()`하지 않는다. 같은 키는 덮어쓰지만, 데이터 파일에서 삭제된 항목은 기존 Dictionary에 남을 수 있다. 세션 중 재로드를 안전하게 하려면 `LoadData()` 시작 시 전역 저장소를 초기화하는 함수가 필요하다.

### 데이터 로드 순서 의존성이 강함

여러 로드 함수가 이미 생성된 객체를 직접 참조한다.

예를 들어 `LoadProvinces()`는 `PRODUCTS`, `SPECIES_SPEC`, `CULTURE`, `BUILDING_TYPE`, `SPECIAL_BUILDING_TYPE`이 먼저 준비되어 있어야 한다.

### 키는 대부분 이름 문자열이다

대부분의 Dictionary 키가 `id`가 아니라 `name`이다. JSON에서 이름을 바꾸면 다른 JSON이나 코드의 참조도 같이 바꿔야 한다.

### 일부 참조 실패는 조용히 무시된다

`ResearchNodes`의 버프 연결처럼 `TryGetValue()` 실패 시 그냥 넘어가는 경우가 있다. 반면 `LoadProvinces()`, `LoadNations()`처럼 인덱서로 바로 접근하는 경우는 키가 없으면 예외가 발생한다.

### `UNIT_TYPE`과 `UNIT_TYPES`가 중복된다

둘 다 `Dictionary<string, UnitType>`이지만 현재 로더는 `UNIT_TYPE`만 채운다.

둘 중 하나로 통일하는 것이 좋다.

### 파일명 대소문자 확인 필요

`LoadBuildingRecipes()`는 `"Buildingrecipes"`를 로드하지만 리소스 파일은 `BuildingRecipes.json`이다.

Windows 환경에서는 문제가 드러나지 않을 수 있지만, 대소문자 구분이 있는 환경에서는 로드 실패 가능성이 있다.

## 개선 후보

- `LoadData()` 시작 시 모든 전역 Dictionary 초기화
- `UNIT_TYPE` / `UNIT_TYPES` 중복 제거
- JSON 이름 상수화
- 로드 실패 시 어떤 JSON의 어떤 필드가 문제인지 더 구체적인 로그 출력
- 이름 문자열 대신 안정적인 ID 기반 참조 검토
- `LoadInitialDiplomacies()`에서 생성한 외교 관계가 어디에 저장되는지 명확화
- `GameDataFormat`을 별도 파일로 분리해 `GlobalVariables`의 책임 축소
