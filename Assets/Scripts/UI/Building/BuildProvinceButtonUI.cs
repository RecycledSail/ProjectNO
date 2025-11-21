using TMPro;
using UnityEngine;

public class BuildProvinceButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // 프로빈스 이름 텍스트
    public TMP_Text countText; // 프로빈스 내에 존재하는 빌딩 레벨 텍스트
    public TMP_Text unemployedText; // 프로빈스 내에 존재하는 실업자 수 텍스트
    private Province provinceData; // 프로빈스 데이터 텍스트
    private BuildingType buildingType;
   
    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetBuildingData(Province province, BuildingType buildingType)
    {
        provinceData = province;
        nameText.text = province.name;
        this.buildingType = buildingType;
        UpdateCount();
        GameManager.Instance.dayEvent.AddListener(UpdateCount);
        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    private void Update()
    {
        //UpdateCount();
    }

    private void UpdateCount()
    {
        if (provinceData != null)
        {
            int count = 0;
            Building building;
            if (provinceData.buildings.TryGetValue(buildingType, out building))
            {
                count += building.level;
            }
            countText.text = count.ToString() + " || " + "100";

            unemployedText.text = (provinceData.population - provinceData.hiredPopulation).ToString();
        }
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}