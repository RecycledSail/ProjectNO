using TMPro;
using UnityEngine;

public class ProvinceBuildingButtonUI : MonoBehaviour
{
    [Header("Name")]
    public TMP_Text nameText; // 빌딩 이름 텍스트

    [Header("Count")]
    public TMP_Text countText; // 빌딩 수 텍스트

    [Header("Worker")]
    public TMP_Text workerText; // 빌딩 수 텍스트


    private Building building;
   
    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetBuildingData(Building building)
    {
        this.building = building;
        nameText.text = building.buildingType.name;
    }

    private void Update()
    {
        
    }

    private void OnDestroy()
    {
        GameManager.Instance.dayEvent.RemoveListener(UpdateBuilding);
    }

    private void UpdateBuilding()
    {
        countText.text = building.level.ToString();

        int totalWorkers = 0;
        foreach (var workerType in building.buildingType.workerNeeded.Keys)
        {
            totalWorkers += (int)building.GetWorkers(workerType);
        }
        workerText.text = totalWorkers.ToString();
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}