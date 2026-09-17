using TMPro;
using UnityEngine;

public class BuildQueueItem : MonoBehaviour
{
    public TMP_Text nameText; // 빌딩 이름 텍스트
    public TMP_Text provinceText; // 프로빈스 이름 텍스트
    public TMP_Text countText; // 남은 인시
    private ConstructionMandate _mandate;
    private BuildRequirementTooltip _tooltip;
    public ConstructionMandate Mandate => _mandate;
   
    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetMandate(ConstructionMandate mandate)
    {
        _mandate = mandate;
        nameText.text = mandate.BuildingType.name;
        provinceText.text = mandate.TargetProvince.name;
        _tooltip = GetComponent<BuildRequirementTooltip>() ?? gameObject.AddComponent<BuildRequirementTooltip>();
        UpdateManhour();
    }

    private void Update()
    {
        UpdateManhour();
    }

    private void UpdateManhour()
    {
        if (_mandate == null || countText == null)
            return;

        countText.text = ConstructionStatusText.Summary(_mandate);
        _tooltip?.SetMessage(ConstructionStatusText.Format(_mandate));
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}
