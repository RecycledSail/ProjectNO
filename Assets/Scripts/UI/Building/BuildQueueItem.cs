using TMPro;
using UnityEngine;

public class BuildQueueItem : MonoBehaviour
{
    public TMP_Text nameText; // 빌딩 이름 텍스트
    public TMP_Text provinceText; // 프로빈스 이름 텍스트
    public TMP_Text countText; // 남은 인시
    private Building _building;
    public Building Building { get { return _building; } }
   
    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetBuildingData(Building building)
    {
        _building = building;
        nameText.text = building.buildingType.name;
        provinceText.text = building.province.name;
        UpdateManhour();
    }

    private void Update()
    {
        UpdateManhour();
    }

    private void UpdateManhour()
    {
        countText.text = _building.manhoursLeft.ToString();
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}