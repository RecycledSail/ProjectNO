using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Events;

/// <summary>
/// 게임 전체를 총괄하는 싱글톤 GameManager 클래스입니다.
/// 날짜 관리, 유저 관리, 하루 단위 게임 이벤트를 처리합니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static GameManager _instance;
    [SerializeField] private EconomicEngine economicEngine;
    public static GameManager Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(GameManager)) as GameManager;
            
            return _instance;
        }
    }

    public UnityEvent dayUIEvent;
    // 모든 유저 목록 및 플레이어 본인 정보
    public List<User> users { get; set; }

    // 현재 플레이중인 플레이어 본인
    public User player { get; set; }

    // 게임이 일시정지 상태인지 나타내는 플래그
    public bool paused { get; private set; } = true;

    /// <summary>
    /// 국가 정보를 저장하는 Dictionary
    /// Key: 국가 이름, Value: Nation 객체
    /// </summary>
    public Dictionary<string, Nation> nations = new Dictionary<string, Nation>();

    /// <summary>
    /// 게임 내 모든 주 정보를 저장하는 Dictionary
    /// Key: 주 이름, Value: Province 객체
    /// </summary>
    public Dictionary<string, Province> provinces = new Dictionary<string, Province>();

    /// <summary>
    /// 각 색상에 대응하는 주 정보를 저장하는 Dictionary
    /// Key: Color32 (각 주의 색상), Value: 해당하는 Province 객체
    /// </summary>
    public Dictionary<Color32, Province> colorToProvince = new Dictionary<Color32, Province>();


    // 게임 내 현재 날짜 관리 (초기 날짜: 1836년 1월 1일)
    public int year { get; set; } = 1836;
    public int month { get; set; } = 1;
    public int day { get; set; } = 1;

    // 게임 일주일 단위 관리
    public int dayoftheWeek = 0;

    // 게임에서 하루가 진행되는 실제 시간 간격(초 단위)
    private int timeSpeed = 1;
    private float dayInterval = 1.0f;

    /// <summary>
    /// 국가별 도로 연결 정보 캐시 (Nation → ConnectedProvinces Set)
    /// </summary>
    private Dictionary<Nation, HashSet<string>> roadConnectedProvinceCache = new();

    /// <summary>
    /// 캐시가 유효한지 여부 (도로가 변경되면 false로 설정)
    /// </summary>
    private Dictionary<Nation, bool> roadCacheValid = new();


    /// <summary>
    /// 게임 시작 전 초기화 메서드 (싱글톤 중복방지 처리)
    /// </summary>
    private void Awake()
    {
        // 싱글톤 중복 방지 로직
        if (_instance == null)
        {
            _instance = this;
            dayUIEvent = new UnityEvent();
            //DontDestroyOnLoad(gameObject);  // 씬 변경 시 유지
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // 중복 시 제거
        }
    }

    /// <summary>
    /// 게임 시작 시 한 번 실행되는 초기화 메서드
    /// </summary>
    void Start()
    {
        users = new List<User>();
        GlobalVariables.LoadData();
        if (GlobalVariables.saveFileName != null && File.Exists(Path.Combine(Application.persistentDataPath, GlobalVariables.saveFileName + ".json")))
        {
            SaveManager.OnLoad();
        }
        else
        {
            // 게임 새로 시작 (기본 국가 코드 "Nation1")
            StartNewGame("Nation1");
        }
        paused = false;

        BattleManager.Instance.RefreshRegimentMarkers();

        dayUIEvent.Invoke(); // 초기 UI 업데이트
        // 날짜 진행 Coroutine 시작
        StartDayCycle();
    }

    /// <summary>
    /// 매 프레임마다 호출되는 메서드
    /// Space 키를 눌러서 일시정지 기능을 토글할 수 있도록 함
    /// </summary>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetGameSpeed(1);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetGameSpeed(2);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetGameSpeed(4);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetGameSpeed(8);
        }
    }

    /// <summary>
    /// 현재 배속을 반환해주는 메서드
    /// </summary>
    /// <returns>정지 중이면 0, 아니면 현재 배속</returns>
    public int GetTimeSpeed()
    {
        return paused ? 0 : timeSpeed;
    }

    /// <summary>
    /// 일시정지 상태를 토글하는 메서드
    /// </summary>
    public void TogglePause()
    {
        paused = !paused;
        dayUIEvent.Invoke(); // 일시정지 상태 변경 시 UI 업데이트
        Debug.Log(paused ? "Game Paused" : "Game Resumed");
    }


    /// <summary>
    /// 시간 배속을 토글하는 메서드
    /// </summary>
    /// <param name="speed">설정하려는 배속</param>
    public void SetGameSpeed(int speed)
    {
        this.timeSpeed = speed;
        dayInterval = 1.0f / this.timeSpeed;
        if (paused)
        {
            TogglePause();
        }
        Debug.Log($"Game Speed Set to {speed}x");
    }

    /// <summary>
    /// 지정된 국가 코드를 플레이어로 설정하고, 게임을 초기화하는 메서드
    /// </summary>
    /// <param name="nationCode">플레이어가 선택한 국가 코드</param>
    void StartNewGame(string nationCode)
    {
        int id = 0;

        // 초기 Province들 init
        foreach (string provinceStr in GlobalVariables.PROVINCES.Keys)
        {
            Province province = GlobalVariables.PROVINCES[provinceStr];
            provinces[provinceStr] = province;
        }

        // GlobalVariables에서 모든 국가 순회
        foreach (string nationStr in GlobalVariables.NATIONS.Keys)
        {
            Nation nation = GlobalVariables.NATIONS[nationStr];
            // 해당 국가의 초기 Province들 추가
            foreach (string provinceStr in GlobalVariables.INITIAL_PROVINCES[nationStr])
            {
                Province province = GlobalVariables.PROVINCES[provinceStr];
                nation.AddProvinces(province);
            }
            // 해당 국가의 수도 설정
            string capitalProvinceStr = GlobalVariables.INITIAL_CAPITALS[nationStr];
            Province capitalProvince = GlobalVariables.PROVINCES[capitalProvinceStr];
            nation.capital = capitalProvince;

            // nations 딕셔너리에 국가 추가
            nations[nationStr] = nation;
            BattleManager.Instance.AddRegiments(nation.regiments);

            // 국가마다 User 객체 생성 및 목록에 추가
            User user = new User(id++, nation);
            users.Add(user);

            // 플레이어가 선택한 국가를 플레이어 유저로 설정
            if (nationStr == nationCode)
                player = user;
        }
    }

    /// <summary>
    /// 게임 날짜를 하루씩 진행시키는 메서드
    /// </summary>
    public void AdvanceDay()
    {
        day++; // 하루 증가
        dayoftheWeek++; // 요일 증가
        int daysInMonth = GetDaysInMonth(month, year);

        // 현재 달의 일수가 넘으면 다음 달로 변경
        if (day > daysInMonth)
        {
            day = 1; // 날짜 초기화
            month++; // 다음 달로 변경

            // 12월이 넘어가면 새해로 변경
            if (month > 12)
            {
                month = 1;  // 월 초기화
                year++;     // 연도 증가
            }
        }
    }

    /// <summary>
    /// 특정 월의 일 수를 계산하는 헬퍼 메서드
    /// </summary>
    /// <param name="month">월</param>
    /// <param name="year">연도</param>
    /// <returns>해당 월의 총 일수</returns>
    private int GetDaysInMonth(int month, int year)
    {
        switch (month)
        {
            case 2:
                // 윤년이면 29일, 아니면 28일
                return IsLeapYear(year) ? 29 : 28;
            case 4: case 6: case 9: case 11:
                return 30;
            default:
                return 31;
        }
    }

    /// <summary>
    /// 주어진 연도가 윤년인지 확인하는 메서드
    /// </summary>
    /// <param name="year">연도</param>
    /// <returns>윤년 여부</returns>
    private bool IsLeapYear(int year)
    {
        // 윤년 공식 적용
        return year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    }

    /// <summary>
    /// 하루씩 게임 날짜를 진행시키는 코루틴(Coroutine)을 시작하는 메서드
    /// </summary>
    private void StartDayCycle()
    {
        StartCoroutine(DayCycleCoroutine());
    }

    /// <summary>
    /// 게임 내 시간(하루)을 일정 간격마다 진행시키는 Coroutine 메서드
    /// </summary>
    private IEnumerator DayCycleCoroutine()
    {
        while (true)
        {
            // 지정된 간격(dayInterval)마다 하루 진행
            yield return new WaitForSeconds(dayInterval);

            // 일시정지 상태가 아닐 때만 하루 진행
            if (!paused)
            {
                AdvanceDay();         // 날짜 진행
                ProcessDailyEvents(); // 매일 실행될 게임 이벤트 처리
                if(dayoftheWeek >= 7)
                {
                    dayoftheWeek = 0;
                    ProcessWeeklyEvents();
                }
                dayUIEvent.Invoke(); // 매일 실행되는 event invoke
            }
        }
    }

    /// <summary>
    /// 매일 하루마다 실행되는 게임 이벤트 로직 처리 메서드
    /// 예) 자원 생산, AI 행동 등
    /// </summary>
    private void ProcessDailyEvents()
    {
        foreach (Province province in provinces.Values)
        {
            province.SimulateDailyTurn(); // 이것만 호출
        }
    }

    /// <summary>
    /// 매주마다 실행되는 게임 이벤트 로직 처리 메서드
    /// 예) 인구 증가 등
    /// </summary>
    private void ProcessWeeklyEvents()
    {
        // 0. 모든 마켓의 LastSupply와 LastDemand 초기화 (새 주 시작)
        foreach (Nation nation in nations.Values)
        {
            foreach (ProductState ps in nation.market.Products.Values)
            {
                ps.LastSupply = 0;
                ps.LastDemand = 0;
            }
        }
        foreach (Province province in provinces.Values)
        {
            foreach (ProductState ps in province.market.Products.Values)
            {
                ps.LastSupply = 0;
                ps.LastDemand = 0;
            }
        }

        // 1. 캐시가 유효하지 않은 국가의 연결 상태 재계산
        foreach (Nation nation in nations.Values)
        {
            if (nation != null && (!roadCacheValid.ContainsKey(nation) || !roadCacheValid[nation]))
            {
                BuildRoadConnectedProvinceCacheForNation(nation);
                roadCacheValid[nation] = true;
            }
        }

        // 2. Nation 주간 처리
        foreach (Nation nation in nations.Values)
        {
            nation.SimulateWeeklyTurn();
        }

        // 3. Province 생산 단계 (생산만 수행)
        foreach (Province province in provinces.Values)
        {
            province.ProduceGoodsWeekly();
        }

        // 4. 도로로 연결된 Province의 생산품을 Nation market으로 이동
        foreach (Nation nation in nations.Values)
        {
            TransferProvinceProductionToNationMarket(nation);
        }

        // 5. Province 소비 단계 (nation market 우선, 실패 시 local market)
        foreach (Province province in provinces.Values)
        {
            economicEngine.ConsumeFoodsWeekly(province, province.isConnectedToCapital);
        }

        // 6. Province 인구 업데이트
        foreach (Province province in provinces.Values)
        {
            province.UpdatePopulation();
        }

        // 7. 내 nation market(player의 nation의 market)의 재고 debug로 출력
        Debug.Log($"--- Nation Market Stock for {player.nation.name} ---");
        foreach (var kv in player.nation.market.Products)
        {
            string productName = kv.Key;
            ProductState pstate = kv.Value;
            Debug.Log($"{productName}: Stock={pstate.Stock}, LastSupply={pstate.LastSupply}, LastDemand={pstate.LastDemand}");

        }

        // 8. GDP 계산 및 업데이트
        economicEngine.UpdateGDPWeekly(nations.Values);

        // 9. 예산 처리: 세금 징수 → 화폐 발행 → 인플레이션 갱신
        foreach (Nation nation in nations.Values)
        {
            nation.governmentBudget.CollectTaxes();
            nation.governmentBudget.PrintMoney();
            nation.governmentBudget.UpdateInflation();
        }
    }

    /// <summary>
    /// 도로 변경 시 호출 - 특정 국가의 캐시를 무효화
    /// </summary>
    /// <param name="nation">도로가 변경된 국가</param>
    public void InvalidateRoadCache(Nation nation)
    {
        if (nation != null)
        {
            roadCacheValid[nation] = false;
        }
    }

    /// <summary>
    /// 특정 국가에 대해 수도와 도로 연결된 모든 프로빈스를 계산하여 캐시
    /// 각 프로빈스의 isConnectedToCapital 필드도 업데이트
    /// </summary>
    private void BuildRoadConnectedProvinceCacheForNation(Nation nation)
    {
        if (nation == null || nation.capital == null)
        {
            roadConnectedProvinceCache[nation] = new HashSet<string>();
            // 모든 프로빈스를 미연결로 설정
            foreach (Province p in nation.provinces)
            {
                p.isConnectedToCapital = false;
            }
            return;
        }

        var connectedProvinces = new HashSet<string>();
        var visited = new HashSet<string>();
        var queue = new Queue<Province>();

        queue.Enqueue(nation.capital);
        visited.Add(nation.capital.name);
        connectedProvinces.Add(nation.capital.name);
        nation.capital.isConnectedToCapital = true;

        while (queue.Count > 0)
        {
            Province current = queue.Dequeue();

            if (GlobalVariables.ADJACENT_PROVINCES.TryGetValue(current.name, out var neighbors))
            {
                foreach (Province neighbor in neighbors)
                {
                    if (neighbor == null) continue;
                    if (visited.Contains(neighbor.name)) continue;
                    if (neighbor.nation != nation) continue;
                    if (neighbor.road != 1) continue;

                    visited.Add(neighbor.name);
                    connectedProvinces.Add(neighbor.name);
                    neighbor.isConnectedToCapital = true;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // 연결되지 않은 프로빈스 업데이트
        foreach (Province p in nation.provinces)
        {
            if (!connectedProvinces.Contains(p.name))
            {
                p.isConnectedToCapital = false;
            }
        }

        roadConnectedProvinceCache[nation] = connectedProvinces;
    }

    /// <summary>
    /// Starting from nation's capital, traverse provinces connected by road (road == 1) and belonging to the nation,
    /// and transfer all produced stock from each province's market into the nation's market.
    /// Uses cached connection information (isConnectedToCapital field).
    /// </summary>
    /// <param name="nation">Nation to collect production for</param>
    private void TransferProvinceProductionToNationMarket(Nation nation)
    {
        if (nation == null || nation.capital == null) return;

        // 캐시된 연결 정보를 사용하여 프로빈스 순회
        foreach (Province province in nation.provinces)
        {
            // 수도이거나 수도와 연결된 프로빈스만 생산품 이동
            if (province.isConnectedToCapital && province.market != null)
            {
                foreach (var kv in new List<KeyValuePair<string, ProductState>>(province.market.Products))
                {
                    string productName = kv.Key;
                    ProductState pstate = kv.Value;
                    int amount = pstate.Stock;
                    if (amount <= 0) continue;

                    // ensure nation market has the product
                    if (!nation.market.Products.ContainsKey(productName))
                    {
                        // use global initial price if available
                        int basePrice = 1;
                        if (GlobalVariables.PRODUCTS.TryGetValue(productName, out var prod))
                            basePrice = prod.InitialPrice;
                        nation.market.AddProduct(productName, basePrice);
                    }

                    // move stock
                    nation.market.Products[productName].Stock += amount;
                    nation.market.Products[productName].LastSupply += amount;

                    // remove from province
                    pstate.Stock = 0;
                }
            }
        }
    }

    /// <summary>
    /// 현재 게임 날짜를 문자열로 얻어오는 메서드
    /// UI 등에 표시할 때 사용
    /// </summary>
    /// <returns>형식화된 날짜 문자열 (예: 1836-01-01)</returns>
    public string GetCurrentDate()
    {
        return $"{year:D4}-{month:D2}-{day:D2}";
    }
}
