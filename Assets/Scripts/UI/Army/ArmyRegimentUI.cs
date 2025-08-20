using TMPro;
using UnityEngine;

public class ArmyRegimentUI : MonoBehaviour
{
    public TMP_Text nameText; // ���� �̸� �ؽ�Ʈ
    public TMP_Text popText; // ���� �� �ؽ�Ʈ
    public TMP_Text hometownText;
    private Nation nationData; // ���� ������ �ؽ�Ʈ
    private Regiment regiment;
   
    /// <summary>
    /// Regiment �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
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
    /// Regiment ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }
}