using System.Collections.Generic;
using UnityEngine;
using TMPro;  // <-- 반드시 추가

public class GameManager : MonoBehaviour
{
    // Singleton instance
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

    // Users
    public List<User> users { get; private set; }
    public User player { get; private set; }
    public bool paused { get; private set; } = true;

    // 날짜 관리 변수
    public int year { get; private set; } = 1836;
    public int month { get; private set; } = 1;
    public int day { get; private set; } = 1;
    private float dayInterval = 1.0f;

    [SerializeField] private TextMeshProUGUI dateText;


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        users = new List<User>();
        GameManager.Instance.StartNewGame("First");
        paused = false;

        StartDayCycle(); // 날짜 진행 시작
    }

    void StartNewGame(string nationCode)
    {
        int id = 0;
        foreach (string nationStr in GlobalVariables.NATIONS.Keys)
        {
            Nation nation = GlobalVariables.NATIONS[nationStr];
            foreach (string provinceStr in GlobalVariables.INITIAL_PROVINCES[nationStr])
            {
                Province province = GlobalVariables.PROVINCES[provinceStr];
                nation.AddProvinces(province);
            }
            User user = new User(id++, nation);
            users.Add(user);

            if (nationStr == nationCode)
                player = user;
        }
    }

    // 날짜 진행 메서드
    public void AdvanceDay()
    {
        day++;
        int daysInMonth = GetDaysInMonth(month, year);

        if (day > daysInMonth)
        {
            day = 1;
            month++;

            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        UpdateUIDate(); // 날짜가 바뀔 때 UI 갱신
    }
    
    private void UpdateUIDate()
    {
        if(dateText != null)
            dateText.text = GetCurrentDate();
    }

    public string GetCurrentDate()
    {
        return $"{year:D4}-{month:D2}-{day:D2}";
    }


    private int GetDaysInMonth(int month, int year)
    {
        switch (month)
        {
            case 2:
                return IsLeapYear(year) ? 29 : 28;
            case 4: case 6: case 9: case 11:
                return 30;
            default:
                return 31;
        }
    }

    private bool IsLeapYear(int year)
    {
        return year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    }

    private void StartDayCycle()
    {
        StartCoroutine(DayCycleCoroutine());
    }

    private IEnumerator<WaitForSeconds> DayCycleCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(dayInterval);
            if (!paused)
            {
                AdvanceDay();
                ProcessDailyEvents();
            }
        }
    }

    private void ProcessDailyEvents()
    {
        // 하루마다 실행될 게임 로직을 이곳에서 처리
        Debug.Log($"Daily Update: {year}-{month:00}-{day:00}");
    }


}
