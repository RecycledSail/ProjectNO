using TMPro;
using UnityEngine;

public class SquadUI : MonoBehaviour
{
    public TMP_Text nameText; // 분대 이름 텍스트
    public TMP_Text popText; // 분대 수 텍스트
    private Squad squadData; // 연대 데이터 텍스트
   
    /// <summary>
    /// Regiment 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetSquadData(Squad squad)
    {
        this.squadData = squad;
        nameText.text = squadData.unitType.name;
        popText.text = squadData.population.ToString();
        GameManager.Instance.dayEvent.AddListener(UpdatePopCount);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayEvent.RemoveListener(UpdatePopCount);
    }

    private void Update()
    {
        
    }

    private void UpdatePopCount()
    {
        popText.text = squadData.population.ToString() + "/" + squadData.capacity.ToString();
    }

    /// <summary>
    /// Regiment 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
    }
}