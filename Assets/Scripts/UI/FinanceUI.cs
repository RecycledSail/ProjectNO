using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinanceUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI 패널

    //Province List 관련
    public TMP_Text totalGoldText;
    public TMP_Text inflationText;
    public TMP_Text currentStepFinanceText;

    public Slider militarySlider;
    public Slider industrySlider;
    public Slider realEstateSlide;
    public Slider researchSlider;

    // Panels to change
    public List<GameObject> subUIs;
    private GameObject currentOpenSubUI;
    private Nation currentNation;
    public Nation CurrentNation { get { return currentNation; } }

    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static FinanceUI _instance;
    public static FinanceUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(FinanceUI)) as FinanceUI;

            return _instance;
        }
    }

    /// <summary>
    /// 게임 시작 전 초기화 메서드 (싱글톤 중복방지 처리)
    /// </summary>
    private void Awake()
    {
        // 싱글톤 중복 방지 로직
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // 중복 시 제거
        }
    }

    private void Start()
    {
        for(int i=0; i<subUIs.Count; i++)
        {
            if(i != 0)
            {
                subUIs[i].SetActive(false);
            }
            else
            {
                subUIs[i].SetActive(true);
                currentOpenSubUI = subUIs[i];
            }
        }
        uiPanel.SetActive(false); // 처음에는 UI를 숨김
        currentNation = null;
        GameManager.Instance.dayUIEvent.AddListener(UpdateFinanceUI);
    }

    private void Update()
    {
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateFinanceUI);
    }

    private void UpdateFinanceUI()
    {
        if (currentNation != null)
        {
            InitFinanceView();
        }
    }

    /// <summary>
    /// 특정 Nation의 UI를 열고 현재 재정상태를 출력합니다.
    /// </summary>
    /// <param name="nation">선택한 국가</param>
    public void OpenFinanceUI(Nation nation)
    {
        // Nation 이름 표시
        currentNation = nation;

        InitFinanceView();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// 플레이어의 Nation의 UI를 열고 현재 재정상태를 출력합니다.
    /// </summary>
    public void OpenFinanceUI()
    {
        Nation nation = GameManager.Instance.player.nation;
        OpenFinanceUI(nation);
    }

    /// <summary>
    /// 특정 Nation의 재정 정보를 초기화하고 UI를 업데이트합니다.
    /// </summary>
    public void InitFinanceView()
    {
        // Text 업데이트
        GovernmentBudget currentBudget = currentNation.governmentBudget;
        long totalGold = currentBudget.MoneySupply;
        double inflation = currentBudget.InflationRate * 100;
        long currentStepFinance = 0; // TODO

        totalGoldText.text = $"Total Gold: {totalGold:N0}";
        inflationText.text = $"Inflation: {inflation:F2}%";
        currentStepFinanceText.text = $"Current Step Finance: {currentStepFinance:N0}";

        // 슬라이더 값 업데이트
        militarySlider.value = (float)currentBudget.Policy.MilitarySalary;
        industrySlider.value = 0.0f; //TODO
        realEstateSlide.value = (float)currentBudget.Policy.RealEstateFund;
        researchSlider.value = (float)currentBudget.Policy.ResearchFund;
    }

    /// <summary>
    /// 현재 활성화된 SubUI를 변경한다.
    /// </summary>
    /// <param name="index">변경할 SubUI의 index</param>
    public void ChangeSubUI(int index)
    {
        currentOpenSubUI.SetActive(false);
        subUIs[index].SetActive(true);
        currentOpenSubUI = subUIs[index];
    }

    /// <summary>
    /// Nation UI를 닫습니다.
    /// </summary>
    public void CloseFinanceUI()
    {
        uiPanel.SetActive(false);
    }
}
