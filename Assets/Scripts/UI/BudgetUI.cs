using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 국가 예산 UI — 정책별 화폐 발행 배분
///
/// 각 정책 채널(연구/군사/산업/부동산)에 금액을 슬라이더 또는 입력 필드로 설정한 뒤
/// 확정 버튼을 누르면 다음 주 턴에 해당 금액이 발행·투입됩니다.
///
/// ────────── 인스펙터 연결 목록 ──────────
/// [경제 현황]
///   gdpText             — GDP (4주 이동평균)
///   inflationRateText   — 인플레이션율 (%)
///   inflationStatusText — 인플레이션 상태
///   moneySupplyText     — 총 통화 공급량
///   taxRevenueText      — 이번 주 세입
///   nationBalanceText   — 현재 국고 잔액
///
/// [발행 총액 표시]
///   totalPrintText      — 현재 4개 채널 합계
///   maxPrintText        — 발행 허용 한도 (GDP×50%)
///
/// [연구 투자]
///   researchSlider / researchInput / researchPreviewText
///
/// [군인/공무원 월급]
///   militarySlider / militaryInput / militaryPreviewText
///
/// [산업 지원]
///   industrySlider / industryInput / industryPreviewText
///
/// [부동산 정책]
///   realEstateSlider / realEstateInput / realEstatePreviewText
///
/// [버튼]
///   confirmButton       — 발행 확정
///   cancelButton        — UI 닫기
/// ────────────────────────────────────────
/// </summary>
public class BudgetUI : MonoBehaviour
{
    [Header("패널")]
    public GameObject uiPanel;

    [Header("경제 현황")]
    public TMP_Text gdpText;
    public TMP_Text inflationRateText;
    public TMP_Text inflationStatusText;
    public TMP_Text moneySupplyText;
    public TMP_Text taxRevenueText;
    public TMP_Text nationBalanceText;

    [Header("발행 합계")]
    public TMP_Text totalPrintText;
    public TMP_Text maxPrintText;

    [Header("연구 투자")]
    public Slider    researchSlider;
    public TMP_InputField researchInput;
    public TMP_Text  researchPreviewText;

    [Header("군인 / 공무원 월급")]
    public Slider    militarySlider;
    public TMP_InputField militaryInput;
    public TMP_Text  militaryPreviewText;

    [Header("산업 지원")]
    public Slider    industrySlider;
    public TMP_InputField industryInput;
    public TMP_Text  industryPreviewText;

    [Header("부동산 정책")]
    public Slider    realEstateSlider;
    public TMP_InputField realEstateInput;
    public TMP_Text  realEstatePreviewText;

    [Header("버튼")]
    public Button confirmButton;
    public Button cancelButton;

    // 슬라이더 최대 한도: GDP 이동평균의 50%
    private const float MAX_PRINT_GDP_RATIO = 0.5f;

    private GovernmentBudget _budget;
    private long _maxPrint;

    // ─── Unity Lifecycle ─────────────────────────────────────────

    private void Start()
    {
        BindSlider(researchSlider,  researchInput,  researchPreviewText,  "연구 투자");
        BindSlider(militarySlider,  militaryInput,  militaryPreviewText,  "군인/공무원 월급");
        BindSlider(industrySlider,  industryInput,  industryPreviewText,  "산업 지원");
        BindSlider(realEstateSlider,realEstateInput,realEstatePreviewText,"부동산 정책");

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Close);

        if (uiPanel != null) uiPanel.SetActive(false);

        GameManager.Instance.dayUIEvent.AddListener(RefreshIfOpen);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayUIEvent.RemoveListener(RefreshIfOpen);
    }

    // ─── Public API ──────────────────────────────────────────────

    public void Open()
    {
        Nation nation = GameManager.Instance.player.nation;
        _budget = nation.governmentBudget;
        _maxPrint = (long)(nation.GDPAverage * MAX_PRINT_GDP_RATIO);
        if (_maxPrint < 1) _maxPrint = 1;

        SetSliderMax(researchSlider,   _maxPrint);
        SetSliderMax(militarySlider,   _maxPrint);
        SetSliderMax(industrySlider,   _maxPrint);
        SetSliderMax(realEstateSlider, _maxPrint);

        if (uiPanel != null) uiPanel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
    }

    // ─── Private ─────────────────────────────────────────────────

    private void RefreshIfOpen()
    {
        if (uiPanel != null && uiPanel.activeSelf)
            Refresh();
    }

    private void Refresh()
    {
        if (_budget == null) return;

        Nation nation = GameManager.Instance.player.nation;

        Set(gdpText,          $"GDP (4주 평균): {nation.GDPAverage:N0}");
        Set(moneySupplyText,  $"통화 공급량: {_budget.MoneySupply:N0}");
        Set(taxRevenueText,   $"주간 세입: {_budget.WeeklyTaxRevenue:N0}");
        Set(nationBalanceText,$"국고: {nation.balance:N0}");
        Set(maxPrintText,     $"발행 한도: {_maxPrint:N0}");

        if (inflationRateText != null)
        {
            string sign = _budget.InflationRate >= 0 ? "+" : "";
            inflationRateText.text  = $"인플레이션: {sign}{_budget.InflationRate:F2}%";
            inflationRateText.color = InflationColor(_budget.InflationRate);
        }
        Set(inflationStatusText, _budget.GetInflationStatus());

        RefreshTotal();
    }

    private void RefreshTotal()
    {
        long total = GetSliderValue(researchSlider)
                   + GetSliderValue(militarySlider)
                   + GetSliderValue(industrySlider)
                   + GetSliderValue(realEstateSlider);

        Set(totalPrintText, $"총 발행 예정: {total:N0}");

        // 한도 초과 시 경고 색상
        if (totalPrintText != null)
            totalPrintText.color = total > _maxPrint ? Color.red : Color.white;
    }

    private void OnConfirm()
    {
        if (_budget == null) return;

        long research   = GetSliderValue(researchSlider);
        long military   = GetSliderValue(militarySlider);
        long industry   = GetSliderValue(industrySlider);
        long realEstate = GetSliderValue(realEstateSlider);

        long total = research + military + industry + realEstate;
        if (total > _maxPrint)
        {
            // 한도 초과 — 비율 유지하며 스케일 다운
            double scale = (double)_maxPrint / total;
            research   = (long)(research   * scale);
            military   = (long)(military   * scale);
            industry   = (long)(industry   * scale);
            realEstate = (long)(realEstate * scale);
        }

        PolicyAllocation policy = new()
        {
            ResearchFund   = research,
            MilitarySalary = military,
            RealEstateFund = realEstate,
        };

        // 산업 지원: 균등 분배 헬퍼를 사용
        _budget.Policy = policy;
        _budget.SetIndustrySubsidyTotal(industry);
        // SetIndustrySubsidyTotal은 내부적으로 _budget.Policy.IndustrySubsidy를 채움

        ResetSliders();
        Refresh();
    }

    // ─── Slider / Input binding helpers ──────────────────────────

    private void BindSlider(Slider slider, TMP_InputField input, TMP_Text preview, string label)
    {
        if (slider != null)
            slider.onValueChanged.AddListener(v =>
            {
                if (input != null) input.text = ((long)v).ToString();
                Set(preview, $"{label}: {(long)v:N0}");
                RefreshTotal();
            });

        if (input != null)
            input.onEndEdit.AddListener(text =>
            {
                if (!long.TryParse(text, out long value)) return;
                long clamped = System.Math.Max(0, System.Math.Min(value, _maxPrint));
                if (slider != null)
                {
                    slider.onValueChanged.RemoveAllListeners();
                    slider.value = (float)clamped;
                    BindSlider(slider, input, preview, label);
                }
                Set(preview, $"{label}: {clamped:N0}");
                RefreshTotal();
            });
    }

    private static void SetSliderMax(Slider slider, long max)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = (float)max;
        slider.value    = 0f;
    }

    private void ResetSliders()
    {
        SetSliderValue(researchSlider,   researchInput,   0);
        SetSliderValue(militarySlider,   militaryInput,   0);
        SetSliderValue(industrySlider,   industryInput,   0);
        SetSliderValue(realEstateSlider, realEstateInput, 0);
    }

    private static void SetSliderValue(Slider slider, TMP_InputField input, long value)
    {
        if (slider != null) slider.value = (float)value;
        if (input  != null) input.text   = value.ToString();
    }

    private static long GetSliderValue(Slider slider) =>
        slider != null ? (long)slider.value : 0L;

    // ─── Misc helpers ─────────────────────────────────────────────

    private static void Set(TMP_Text label, string text)
    {
        if (label != null) label.text = text;
    }

    private static Color InflationColor(float rate)
    {
        if (rate >  5f) return Color.red;
        if (rate >  2f) return new Color(1f, 0.5f, 0f);
        if (rate < -2f) return new Color(0.3f, 0.8f, 1f);
        return Color.white;
    }
}

