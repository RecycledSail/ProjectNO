using TMPro;
using UnityEngine;

public class SquadUI : MonoBehaviour
{
    public TMP_Text nameText; // �д� �̸� �ؽ�Ʈ
    public TMP_Text popText; // �д� �� �ؽ�Ʈ
    private Squad squadData; // ���� ������ �ؽ�Ʈ
   
    /// <summary>
    /// Regiment �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
    /// </summary>
    public void SetSquadData(Squad squad)
    {
        this.squadData = squad;
        nameText.text = squadData.unitType.name;
        popText.text = squadData.population.ToString(); 
    }

    private void Update()
    {
        UpdatePopCount();
    }

    private void UpdatePopCount()
    {
        popText.text = squadData.population.ToString() + "/" + squadData.capacity.ToString();
    }

    /// <summary>
    /// Regiment ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
    }
}