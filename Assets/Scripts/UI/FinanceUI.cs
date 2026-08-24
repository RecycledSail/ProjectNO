using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinanceUI : MonoBehaviour
{
    private const float TOTAL_BUDGET_PERCENT = 100f;
    private const float MIN_BUDGET_PERCENT = 5f;
    private const float MAX_BUDGET_PERCENT = 40f;

    public GameObject uiPanel;

    public TMP_Text totalGoldText;
    public TMP_Text inflationText;
    
    // Remove this section
    public TMP_Text weeklyRevenueText;  // 주간 세수 (배분 기준)

    public Slider militarySlider;
    public Slider industrySlider;
    public Slider realEstateSlide;
    public Slider researchSlider;

    // 각 슬라이더의 퍼센트 표시 텍스트
    // Remove this section
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
    private readonly Dictionary<Nation, float[]> _budgetPercentagesByNation = new();

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

        if (militarySlider != null)  militarySlider.onValueChanged.AddListener(_ => OnSliderChanged(militarySlider));
        if (industrySlider != null)  industrySlider.onValueChanged.AddListener(_ => OnSliderChanged(industrySlider));
        if (realEstateSlide != null) realEstateSlide.onValueChanged.AddListener(_ => OnSliderChanged(realEstateSlide));
        if (researchSlider != null)  researchSlider.onValueChanged.AddListener(_ => OnSliderChanged(researchSlider));

        uiPanel.SetActive(false);
        currentNation = null;
        GameManager.Instance.dayUIEvent.AddListener(UpdateFinanceUI);
    }

    private void SetupSlider(Slider s)
    {
        if (s == null) return;
        s.minValue = MIN_BUDGET_PERCENT;
        s.maxValue = MAX_BUDGET_PERCENT;
        s.wholeNumbers = false;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateFinanceUI);
    }

    private void UpdateFinanceUI()
    {
        if (currentNation == null)
            return;

        RefreshFinanceTexts();
        RefreshPctLabels();
        ApplyPolicyFromSliders();
    }

    public void OpenFinanceUI(Nation nation)
    {
        if (nation == null) { Debug.LogWarning("[FinanceUI] nation is null"); return; }
        bool nationChanged = currentNation != nation;
        currentNation = nation;
        UIManager.Instance.ReplacePopUp(gameObject);
        InitFinanceView(nationChanged);
    }

    public void OpenFinanceUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null) return;
        Nation nation = GameManager.Instance.player.nation;
        if (nation == null) { Debug.LogWarning("[FinanceUI] player or nation is null"); return; }
        OpenFinanceUI(nation);
    }

    public void InitFinanceView() =>
        InitFinanceView(false);

    private void InitFinanceView(bool reloadSliders)
    {
        RefreshFinanceTexts();

        if (reloadSliders || !TryHasValidSliderTotal())
            LoadBudgetSliders();

        RefreshPctLabels();
        ApplyPolicyFromSliders();
    }

    private void RefreshFinanceTexts()
    {
        GovernmentBudget budget = currentNation.governmentBudget;

        if (totalGoldText != null) totalGoldText.text = $"Total Gold: {budget.MoneySupply:N0}";
        if (inflationText != null) inflationText.text = $"Inflation: {budget.InflationRate:F2}%";
        if (weeklyRevenueText != null) weeklyRevenueText.text = $"Weekly Revenue: {budget.WeeklyTaxRevenue:N0}";
    }

    private void LoadBudgetSliders()
    {
        _suppressSliderEvents = true;

        if (!_budgetPercentagesByNation.TryGetValue(currentNation, out float[] values))
            values = GetPolicyPercentagesOrDefault(currentNation.governmentBudget);

        ApplyBudgetSliderValues(GetBudgetSliders(), values);
        NormalizeBudgetSliders(null);
        SaveBudgetSliderValues();

        _suppressSliderEvents = false;
    }

    private float[] GetPolicyPercentagesOrDefault(GovernmentBudget budget)
    {
        long total = budget.Policy.Total;
        if (total <= 0)
            return new[] { 25f, 25f, 25f, 25f };

        return new[]
        {
            (float)budget.Policy.MilitarySalary / total * 100f,
            (float)budget.Policy.IndustryTotal() / total * 100f,
            (float)budget.Policy.RealEstateFund / total * 100f,
            (float)budget.Policy.ResearchFund / total * 100f,
        };
    }

    private bool TryHasValidSliderTotal()
    {
        float total = 0f;
        foreach (float value in GetBudgetSliderValues(GetBudgetSliders()))
            total += value;

        return Mathf.Abs(total - TOTAL_BUDGET_PERCENT) < 0.01f;
    }

    // 슬라이더가 움직일 때마다 Policy에 퍼센트 → 금액 적용
    private void OnSliderChanged(Slider changedSlider)
    {
        if (_suppressSliderEvents || currentNation == null) return;

        _suppressSliderEvents = true;
        NormalizeBudgetSliders(changedSlider);
        _suppressSliderEvents = false;

        SaveBudgetSliderValues();
        RefreshPctLabels();
        ApplyPolicyFromSliders();
    }

    private void NormalizeBudgetSliders(Slider changedSlider)
    {
        Slider[] sliders = GetBudgetSliders();
        float[] values = GetBudgetSliderValues(sliders);
        int changedIndex = System.Array.IndexOf(sliders, changedSlider);

        if (changedIndex < 0)
        {
            float total = 0f;
            foreach (float value in values)
                total += value;

            values = total <= 0f
                ? new[] { 25f, 25f, 25f, 25f }
                : AllocateBoundedPercentages(values, new[] { 0, 1, 2, 3 }, TOTAL_BUDGET_PERCENT);
        }
        else
        {
            values[changedIndex] = Mathf.Clamp(
                changedSlider.value,
                MIN_BUDGET_PERCENT,
                MAX_BUDGET_PERCENT);

            List<int> remainingIndices = new();
            for (int index = 0; index < sliders.Length; index++)
            {
                if (index != changedIndex)
                    remainingIndices.Add(index);
            }

            float[] redistributed = AllocateBoundedPercentages(
                values,
                remainingIndices,
                TOTAL_BUDGET_PERCENT - values[changedIndex]);

            foreach (int index in remainingIndices)
                values[index] = redistributed[index];
        }

        ApplyBudgetSliderValues(sliders, values);
    }

    private Slider[] GetBudgetSliders() =>
        new[] { militarySlider, industrySlider, realEstateSlide, researchSlider };

    private static float[] GetBudgetSliderValues(Slider[] sliders)
    {
        float[] values = new float[sliders.Length];
        for (int index = 0; index < sliders.Length; index++)
        {
            values[index] = sliders[index] == null
                ? MIN_BUDGET_PERCENT
                : Mathf.Clamp(
                    sliders[index].value,
                    MIN_BUDGET_PERCENT,
                    MAX_BUDGET_PERCENT);
        }

        return values;
    }

    private static void ApplyBudgetSliderValues(Slider[] sliders, float[] values)
    {
        for (int index = 0; index < sliders.Length; index++)
        {
            if (sliders[index] != null)
                sliders[index].value = values[index];
        }
    }

    private void SaveBudgetSliderValues()
    {
        if (currentNation == null)
            return;

        _budgetPercentagesByNation[currentNation] = GetBudgetSliderValues(GetBudgetSliders());
    }

    private static float[] AllocateBoundedPercentages(
        float[] currentValues,
        IReadOnlyList<int> targetIndices,
        float targetTotal)
    {
        float[] result = (float[])currentValues.Clone();
        List<int> activeIndices = new(targetIndices);
        float remainingTotal = targetTotal;

        while (activeIndices.Count > 0)
        {
            float totalWeight = GetTotalWeight(currentValues, activeIndices);
            bool clampedAny = false;

            for (int index = activeIndices.Count - 1; index >= 0; index--)
            {
                int sliderIndex = activeIndices[index];
                float share = remainingTotal * GetWeight(currentValues[sliderIndex]) / totalWeight;

                if (share < MIN_BUDGET_PERCENT)
                {
                    result[sliderIndex] = MIN_BUDGET_PERCENT;
                    remainingTotal -= MIN_BUDGET_PERCENT;
                    activeIndices.RemoveAt(index);
                    clampedAny = true;
                }
                else if (share > MAX_BUDGET_PERCENT)
                {
                    result[sliderIndex] = MAX_BUDGET_PERCENT;
                    remainingTotal -= MAX_BUDGET_PERCENT;
                    activeIndices.RemoveAt(index);
                    clampedAny = true;
                }
            }

            if (!clampedAny)
                break;
        }

        if (activeIndices.Count == 0)
            return result;

        float activeWeight = GetTotalWeight(currentValues, activeIndices);
        foreach (int sliderIndex in activeIndices)
        {
            float exactShare = remainingTotal * GetWeight(currentValues[sliderIndex]) / activeWeight;
            result[sliderIndex] = Mathf.Clamp(
                exactShare,
                MIN_BUDGET_PERCENT,
                MAX_BUDGET_PERCENT);
        }

        return result;
    }

    private static float GetTotalWeight(float[] values, IReadOnlyList<int> indices)
    {
        float total = 0f;
        foreach (int index in indices)
            total += GetWeight(values[index]);
        return total;
    }

    private static float GetWeight(float value) =>
        Mathf.Max(1f, value);

    private void RefreshPctLabels()
    {
        float mil   = militarySlider  != null ? militarySlider.value  : 0f;
        float ind   = industrySlider  != null ? industrySlider.value  : 0f;
        float real  = realEstateSlide != null ? realEstateSlide.value : 0f;
        float res   = researchSlider  != null ? researchSlider.value  : 0f;
        float total = mil + ind + real + res;

        if (militaryPctText)  militaryPctText.text  = $"{mil:F1}%";
        if (industryPctText)  industryPctText.text  = $"{ind:F1}%";
        if (realEstatePctText) realEstatePctText.text = $"{real:F1}%";
        if (researchPctText)  researchPctText.text  = $"{res:F1}%";

        if (totalAllocPctText)
        {
            totalAllocPctText.text = $"Total: {total:F1}%";
            // 100% 초과 시 빨간색 경고
            totalAllocPctText.color = total > TOTAL_BUDGET_PERCENT + 0.01f ? Color.red : Color.white;
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
