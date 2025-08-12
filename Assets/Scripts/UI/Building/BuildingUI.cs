using TMPro;
using UnityEngine;

public class BuildingUI : MonoBehaviour
{
    public TMP_Text nameText; // 빌딩 이름 텍스트
    public TMP_Text countText; // 빌딩 수 텍스트
    private Nation nationData; // 프로빈스 데이터 텍스트
    private BuildingType buildingType;
   
    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetBuildingData(Nation nation, BuildingType buildingType)
    {
        nationData = nation;
        nameText.text = buildingType.name;
        this.buildingType = buildingType;
        UpdateCount();

        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    private void Update()
    {
        UpdateCount();
    }

    private void UpdateCount()
    {
        int count = 0;
        foreach(Province province in nationData.provinces)
        {
            Building building;
            if(province.buildings.TryGetValue(buildingType, out building))
            {
                count += building.level;
            }
        }
        countText.text = count.ToString();
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}