using TMPro;
using UnityEngine;

public class BuildingUI : MonoBehaviour
{
    public TMP_Text nameText; // ���� �̸� �ؽ�Ʈ
    public TMP_Text countText; // ���� �� �ؽ�Ʈ
    private Nation nationData; // ���κ� ������ �ؽ�Ʈ
    private BuildingType buildingType;
   
    /// <summary>
    /// Province �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
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
    /// Province ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}