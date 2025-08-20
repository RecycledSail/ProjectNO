using TMPro;
using UnityEngine;

public class ArmyRegimentUI : MonoBehaviour
{
    public TMP_Text nameText; // 연대 이름 텍스트
    public TMP_Text popText; // 연대 수 텍스트
    public TMP_Text hometownText;
    private Nation nationData; // 연대 데이터 텍스트
    private Regiment regiment;
   
    /// <summary>
    /// Regiment 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetBuildingData(Nation nation, Regiment regiment)
    {
        nationData = nation;
        this.regiment = regiment;
        popText.text = this.regiment.GetUnitCount().ToString();
        this.hometownText.text = regiment.location.name;
        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    private void Update()
    {
        UpdatePopCount();
    }

    private void UpdatePopCount()
    {
        popText.text = regiment.GetUnitCount().ToString();
    }

    /// <summary>
    /// Regiment 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}