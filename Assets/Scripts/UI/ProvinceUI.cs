using TMPro;
using UnityEngine;

public class ProvinceUI : MonoBehaviour
{
    public TMP_Text nameText; // �̸� �ؽ�Ʈ
   // public TMP_Text populationText; // �α� �ؽ�Ʈ
    private Province provinceData; // ���κ� ������ �ؽ�Ʈ
   
    /// <summary>
    /// Province �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
    /// </summary>
    public void SetProvinceData(Province province)
    {
        provinceData = province;
        nameText.text = province.name;
        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    /// <summary>
    /// Province ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
        ProvinceDetailUI.Instance.OpenProvinceDetailUI(provinceData);
    }
}