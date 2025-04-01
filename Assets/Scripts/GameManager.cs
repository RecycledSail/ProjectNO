using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 게임 전체를 총괄하는 싱글톤 GameManager 클래스입니다.
/// 날짜 관리, 유저 관리, 하루 단위 게임 이벤트를 처리합니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(GameManager)) as GameManager;
            
            return _instance;
        }
    }

    // 모든 유저 목록 및 플레이어 본인 정보
    public List<User> users { get; private set; }
    public User player { get; private set; }

    // 게임이 일시정지 상태인지 나타내는 플래그
    public bool paused { get; private set; } = true;

    // 게임 내 현재 날짜 관리 (초기 날짜: 1836년 1월 1일)
    public int year { get; private set; } = 1836;
    public int month { get; private set; } = 1;
    public int day { get; private set; } = 1;

    // 게임에서 하루가 진행되는 실제 시간 간격(초 단위)
    private float dayInterval = 1.0f;

    /// <summary>
    /// 게임 시작 전 초기화 메서드 (싱글톤 중복방지 처리)
    /// </summary>
    private void Awake()
    {
        // 싱글톤 중복 방지 로직
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);  // 씬 변경 시 유지
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

        // 게임 새로 시작 (기본 국가 코드 "First")
        GameManager.Instance.StartNewGame("First");
        paused = false;

        // 날짜 진행 Coroutine 시작
        StartDayCycle();
        // Start()에도 한 줄 추가
        UpdateUIDate();
    }
    

    /// <summary>
    /// 지정된 국가 코드를 플레이어로 설정하고, 게임을 초기화하는 메서드
    /// </summary>
    /// <param name="nationCode">플레이어가 선택한 국가 코드</param>
    void StartNewGame(string nationCode)
    {
        int id = 0;

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
        UpdateUIDate(); // ← 추가
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
            }
        }
    }

    /// <summary>
    /// 매일 하루마다 실행되는 게임 이벤트 로직 처리 메서드
    /// 예) 자원 생산, 인구 증가, AI 행동 등
    /// </summary>
    private void ProcessDailyEvents()
    {
        // 현재는 예시 로그 출력 (향후 게임 로직 추가 필요)
        Debug.Log($"Daily Update: {year}-{month:00}-{day:00}");
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
    [SerializeField] private TextMeshProUGUI dateText; 

    private void UpdateUIDate() 
    {
    if (dateText != null)
        dateText.text = GetCurrentDate();
    }






}
