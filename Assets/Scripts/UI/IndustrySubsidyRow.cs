using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 산업 보조금 패널의 행 하나 — BuildingType 하나를 나타냅니다.
/// </summary>
public class IndustrySubsidyRow : MonoBehaviour
{
    [Header("표시")]
    public TMP_Text nameText;
    public Image saturationFill;     // Image Type: Filled, Fill Method: Horizontal
    public TMP_Text saturationText;  // "38%"
    public TMP_Text gainText;        // "+1,200" / "-200"

    [Header("입력")]
    public TMP_InputField amountInput;

    private string _typeName;
    private IndustrySubsidyPanel _panel;

    public string TypeName => _typeName;

    public void Setup(string typeName, float avgSaturation, long totalGain,
                      long currentAmount, IndustrySubsidyPanel panel)
    {
        _typeName = typeName;
        _panel    = panel;

        nameText.text = typeName;

        // 포화도 바 (색상: 빨강=인력부족, 노랑=정상, 초록=포화)
        if (saturationFill != null)
        {
            saturationFill.fillAmount = Mathf.Clamp01(avgSaturation);
            saturationFill.color = avgSaturation >= 0.9f ? Color.green
                                 : avgSaturation >= 0.6f ? Color.yellow
                                 : Color.red;
        }
        if (saturationText != null)
            saturationText.text = $"{avgSaturation * 100f:F0}%";

        // 수익성
        if (gainText != null)
        {
            gainText.text  = totalGain >= 0 ? $"+{totalGain:N0}" : $"{totalGain:N0}";
            gainText.color = totalGain >= 0 ? Color.green : Color.red;
        }

        amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        amountInput.text        = currentAmount > 0 ? currentAmount.ToString() : "0";

        amountInput.onEndEdit.RemoveAllListeners();
        amountInput.onEndEdit.AddListener(_ => _panel.OnRowAmountChanged());
    }

    public long GetAmount()
    {
        return long.TryParse(amountInput.text, out long v) ? System.Math.Max(0L, v) : 0L;
    }
}
