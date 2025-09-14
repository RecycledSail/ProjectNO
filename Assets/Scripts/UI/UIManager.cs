using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    // Singleton instance
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindFirstObjectByType(typeof(UIManager)) as UIManager;
            }
            return _instance;
        }
    }

    private Stack<GameObject> popUpVisited;
    private GameObject currentPopUp;

    /// <summary>
    /// 유저가 속한 국가의 재산(화폐)를 표기하는 텍스트 UI
    /// </summary>
    public TMP_Text CurrencyText;

    /// <summary>
    /// 유저가 속한 국가의 인구를 표기하는 텍스트 UI
    /// </summary>
    public TMP_Text PopulationText;

    /// <summary>
    /// 게임 내 날짜 및 시간 속도를 표기하는 텍스트 UI
    /// </summary>
    public TMP_Text dateText;

    /// <summary>
    /// 싱글톤 패턴을 적용하여 UIManager의 중복 생성을 방지
    /// </summary>
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Start는 첫 프레임 업데이트 전에 한 번 호출됨
    /// </summary>
    private void Start()
    {
        popUpVisited = new Stack<GameObject>();
        GameManager.Instance.dayEvent.AddListener(UpdateTopUI);
    }

    private void OnDestroy()
    {
        GameManager.Instance.dayEvent.RemoveListener(UpdateTopUI);
    }

    /// <summary>
    /// 매 프레임마다 호출되는 Update 메서드
    /// 게임 UI를 최신 상태로 유지함
    /// </summary>
    private void Update()
    {
        //UpdateCurrencyText();  // 재산(화폐) UI 업데이트
        //UpdatePopulation();    // 인구 UI 업데이트
        //UpdateUIDate();        // 날짜 및 시간 속도 UI 업데이트
    }

    private void UpdateTopUI()
    {
        UpdateCurrencyText();  // 재산(화폐) UI 업데이트
        UpdatePopulation();    // 인구 UI 업데이트
        UpdateUIDate();        // 날짜 및 시간 속도 UI 업데이트
    }

    /// <summary>
    /// 숫자를 축약된 형태(M, K 단위)로 변환하는 메서드
    /// 예: 1,500 -> "1.5K", 2,000,000 -> "2.0M"
    /// </summary>
    /// <param name="val">변환할 숫자</param>
    /// <returns>축약된 문자열 값</returns>
    public static string ShortenValue(long val)
    {
        string returnText;
        if (val >= 1000000)
        {
            returnText = (val / 1000000) + "." + ((val % 1000000) / 100000) + "M";
        }
        else if (val >= 1000)
        {
            returnText = (val / 1000) + "." + ((val % 1000) / 100) + "K";
        }
        else
        {
            returnText = val.ToString();
        }
        return returnText;
    }

    /// <summary>
    /// 유저가 보유한 재산(화폐) 정보를 UI에 업데이트하는 메서드
    /// </summary>
    void UpdateCurrencyText()
    {
        long currency = GameManager.Instance.player.GetCurrentBalance();
        string curText = ShortenValue(currency);
        CurrencyText.text = "$: " + curText;
    }

    /// <summary>
    /// 유저가 속한 국가의 총 인구 정보를 UI에 업데이트하는 메서드
    /// </summary>
    void UpdatePopulation()
    {
        long population = GameManager.Instance.player.nation.GetPopulation();
        string popText = ShortenValue(population);
        PopulationText.text = "POP: " + popText;
    }

    /// <summary>
    /// 게임의 현재 날짜와 시간 속도를 UI에 업데이트하는 메서드
    /// </summary>
    private void UpdateUIDate()
    {
        if (dateText != null)
        {
            dateText.text = GameManager.Instance.GetTimeSpeed() + " " + GameManager.Instance.GetCurrentDate();
        }
    }

    /// <summary>
    /// 팝업 뒤로가기 버튼을 눌렀을 때 이전 팝업을 불러오는 메서드
    /// </summary>
    /// <returns>성공 시 true, 실패 시 false</returns>
    public void GoBackPopUp()
    {
        if (popUpVisited.Count != 0)
        {
            currentPopUp.SetActive(false);
            GameObject prev = popUpVisited.Pop();
            currentPopUp = prev;
            currentPopUp.SetActive(true);
            return;
        }
        else return; 
    }

    /// <summary>
    /// 현재 팝업을 교체하는 메서드
    /// </summary>
    /// <param name="go">교체(활성화할) 메서드</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    public bool ReplacePopUp(GameObject go)
    {
        try
        {
            if (SavePopUp())
            {
                currentPopUp = go;
                go.SetActive(true);
                return true;
            }
            else
            {
                currentPopUp = go;
                go.SetActive(true);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 현재 팝업 UI를 저장하고 비활성화하는 메서드
    /// </summary>
    /// <returns>성공 시 true, 실패 시 false</returns>
    private bool SavePopUp()
    {
        if (currentPopUp != null)
        {
            popUpVisited.Push(currentPopUp);
            currentPopUp.SetActive(false);
            currentPopUp = null;
            return true;
        }
        else return false;
    }

    /// <summary>
    /// 현재 팝업 UI를 닫고 방문 목록을 비우는 메서드
    /// </summary>
    public void ClosePopUp()
    {
        if(currentPopUp != null)
        {
            currentPopUp.SetActive(false);
            currentPopUp = null;
            popUpVisited.Clear();
            return;
        }
        else return;
    }

    /// <summary>
    /// DEPRECATED: 현재 게임을 저장하고 바깥으로 나가는 메서드
    /// </summary>
    public void SaveGame()
    {
        SaveManager.OnSave();
        SceneManager.LoadScene("MainMenuScene");
    }
}
