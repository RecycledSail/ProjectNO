using TMPro;
using UnityEngine;

public class RegimentButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // 연대 이름 텍스트
    public TMP_Text popText; // 연대 수 텍스트
    public TMP_Text hometownText;
    private Nation nationData; // 연대 데이터 텍스트
    private Regiment regiment;
   
    /// <summary>
    /// Regiment 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetRegimentData(Nation nation, Regiment regiment)
    {
        nationData = nation;
        this.regiment = regiment;
        nameText.text = this.regiment.name;
        popText.text = this.regiment.GetUnitCount().ToString();
        hometownText.text = this.regiment.location.name;
        GameManager.Instance.dayEvent.AddListener(UpdatePopCount);
    }

    private void Update()
    {
        
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayEvent.RemoveListener(UpdatePopCount);
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
        RegimentDetailUI.Instance.OpenRegimentDetail(regiment);
    }
}