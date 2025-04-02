using TMPro;
using UnityEngine;
using UnityEngine.Rendering.UI;
using UnityEngine.UI;

public class ProvinceUI : MonoBehaviour
{
    public TMP_Text nameText; // 이름 텍스트
   // public TMP_Text populationText; // 인구 텍스트
    private Province provinceData; // 프로빈스 데이터 텍스트
   
    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetProvinceData(Province province)
    {
        provinceData = province;
        nameText.text = province.name;
        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        ProvinceDetailUI.Instance.OpenProvinceDetailUI(provinceData);
    }
}