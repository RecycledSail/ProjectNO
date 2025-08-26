using TMPro;
using UnityEngine;

public class NationButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // �̸� �ؽ�Ʈ
   // public TMP_Text populationText; // �α� �ؽ�Ʈ
    private Nation nationData; // ���κ� ������ �ؽ�Ʈ
   
    /// <summary>
    /// Province �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
    /// </summary>
    public void SetProvinceData(Nation nation)
    {
        nationData = nation;
        nameText.text = nation.name;
        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    /// <summary>
    /// Province ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
        NationUI.Instance.OpenNationUI(nationData);
    }
}