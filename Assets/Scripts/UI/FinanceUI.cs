using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinanceUI : MonoBehaviour
{
    public GameObject uiPanel;

    public TMP_Text totalGoldText;
    public TMP_Text inflationText;
    public TMP_Text weeklyRevenueText;  // 주간 세수 (배분 기준)

    public Slider militarySlider;
    public Slider industrySlider;
    public Slider realEstateSlide;
    public Slider researchSlider;

    // 각 슬라이더의 퍼센트 표시 텍스트
    public TMP_Text militaryPctText;
    public TMP_Text industryPctText;
    public TMP_Text realEstatePctText;
    public TMP_Text researchPctText;
    public TMP_Text totalAllocPctText;  // 합계 % 표시

    // Panels to change
    public List<GameObject> subUIs;
    private GameObject currentOpenSubUI;
    private Nation currentNation;
    public Nation CurrentNation { get { return currentNation; } }

    private bool _suppressSliderEvents = false;

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

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
    }

    private void Start()
    {
        for (int i = 0; i < subUIs.Count; i++)
        {
            if (i != 0)
                subUIs[i].SetActive(false);
            else
            {
                subUIs[i].SetActive(true);
                currentOpenSubUI = subUIs[i];
            }
        }

        // 슬라이더 범위 0~100% 고정
        SetupSlider(militarySlider);
        SetupSlider(industrySlider);
        SetupSlider(realEstateSlide);
        SetupSlider(researchSlider);

        if (militarySlider != null)  militarySlider.onValueChanged.AddListener(_ => OnSliderChanged());
        if (industrySlider != null)  industrySlider.onValueChanged.AddListener(_ => OnSliderChanged());
        if (realEstateSlide != null) realEstateSlide.onValueChanged.AddListener(_ => OnSliderChanged());
        if (researchSlider != null)  researchSlider.onValueChanged.AddListener(_ => OnSliderChanged());

        uiPanel.SetActive(false);
        currentNation = null;
        GameManager.Instance.dayUIEvent.AddListener(UpdateFinanceUI);
    }

    private void SetupSlider(Slider s)
    {
        if (s == null) return;
        s.minValue = 0f;
        s.maxValue = 100f;
        s.wholeNumbers = true;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateFinanceUI);
    }

    private void UpdateFinanceUI()
    {
        if (currentNation != null)
            InitFinanceView();
    }

    public void OpenFinanceUI(Nation nation)
    {
        if (nation == null) { Debug.LogWarning("[FinanceUI] nation is null"); return; }
        currentNation = nation;
        UIManager.Instance.ReplacePopUp(gameObject);
        InitFinanceView();
    }

    public void OpenFinanceUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null) return;
        Nation nation = GameManager.Instance.player.nation;
        if (nation == null) { Debug.LogWarning("[FinanceUI] player or nation is null"); return; }
        OpenFinanceUI(nation);
    }

    public void InitFinanceView()
    {
        GovernmentBudget budget = currentNation.governmentBudget;

        if (totalGoldText != null)    totalGoldText.text    = $"Total Gold: {budget.MoneySupply:N0}";
        if (inflationText != null)    inflationText.text    = $"Inflation: {budget.InflationRate:F2}%";

        long revenue = budget.WeeklyTaxRevenue;
        if (weeklyRevenueText != null) weeklyRevenueText.text = $"Weekly Revenue: {revenue:N0}";

        _suppressSliderEvents = true;

        if (revenue > 0)
        {
            if (militarySlider != null)  militarySlider.value  = Mathf.Round((float)budget.Policy.MilitarySalary  / revenue * 100f);
            if (industrySlider != null)  industrySlider.value  = Mathf.Round((float)budget.Policy.IndustryTotal() / revenue * 100f);
            if (realEstateSlide != null) realEstateSlide.value = Mathf.Round((float)budget.Policy.RealEstateFund  / revenue * 100f);
            if (researchSlider != null)  researchSlider.value  = Mathf.Round((float)budget.Policy.ResearchFund    / revenue * 100f);
        }
        else
        {
            if (militarySlider != null)  militarySlider.value  = 0f;
            if (industrySlider != null)  industrySlider.value  = 0f;
            if (realEstateSlide != null) realEstateSlide.value = 0f;
            if (researchSlider != null)  researchSlider.value  = 0f;
        }

        _suppressSliderEvents = false;
        RefreshPctLabels();
    }

    // 슬라이더가 움직일 때마다 Policy에 퍼센트 → 금액 적용
    private void OnSliderChanged()
    {
        if (_suppressSliderEvents || currentNation == null) return;

        RefreshPctLabels();
        ApplyPolicyFromSliders();
    }

    private void RefreshPctLabels()
    {
        float mil   = militarySlider  != null ? militarySlider.value  : 0f;
        float ind   = industrySlider  != null ? industrySlider.value  : 0f;
        float real  = realEstateSlide != null ? realEstateSlide.value : 0f;
        float res   = researchSlider  != null ? researchSlider.value  : 0f;
        float total = mil + ind + real + res;

        if (militaryPctText)  militaryPctText.text  = $"{mil:F0}%";
        if (industryPctText)  industryPctText.text  = $"{ind:F0}%";
        if (realEstatePctText) realEstatePctText.text = $"{real:F0}%";
        if (researchPctText)  researchPctText.text  = $"{res:F0}%";

        if (totalAllocPctText)
        {
            totalAllocPctText.text = $"Total: {total:F0}%";
            // 100% 초과 시 빨간색 경고
            totalAllocPctText.color = total > 100f ? Color.red : Color.white;
        }
    }

    private void ApplyPolicyFromSliders()
    {
        long revenue = currentNation.governmentBudget.WeeklyTaxRevenue;
        if (revenue <= 0) return;

        PolicyAllocation policy = currentNation.governmentBudget.Policy;
        policy.MilitarySalary  = PctToAmount(militarySlider.value,   revenue);
        policy.RealEstateFund  = PctToAmount(realEstateSlide.value,   revenue);
        policy.ResearchFund    = PctToAmount(researchSlider.value,    revenue);
        currentNation.governmentBudget.SetIndustrySubsidyTotal(PctToAmount(industrySlider.value, revenue));
    }

    private static long PctToAmount(float pct, long total) =>
        (long)(pct / 100f * total);

    public void ChangeSubUI(int index)
    {
        currentOpenSubUI.SetActive(false);
        subUIs[index].SetActive(true);
        currentOpenSubUI = subUIs[index];
    }

    public void CloseFinanceUI()
    {
        uiPanel.SetActive(false);
    }
}
