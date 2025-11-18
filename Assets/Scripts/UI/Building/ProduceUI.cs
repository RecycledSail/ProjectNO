using TMPro;
using UnityEngine;

public class ProduceUI : MonoBehaviour
{
    public TMP_Text produceTypeText; // 빌딩 이름 텍스트
    public TMP_Text produceSupplyDemandText; // 빌딩 수 텍스트
    private Nation nationData; // 프로빈스 데이터 텍스트
    private string productName;
    private long productSupplyCount;
    private long productDemandCount;

    /// <summary>
    /// Province 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetProduceData(Nation nation, string productName)
    {
        nationData = nation;
        this.productName = productName;
        produceTypeText.text = productName;
        UpdateCount();

        //populationText.text = $"Pop: {UIManager.ShortenValue(province.population)}"; // Format population
    }

    private void Update()
    {
        //UpdateCount();
    }

    private void UpdateCount()
    {
        productSupplyCount = 0; productDemandCount = 0;
        foreach(Province province in nationData.provinces)
        {
            if (province.market.Products.TryGetValue(productName, out ProductState product))
            {
                productSupplyCount += product.LastSupply;
                productDemandCount += product.LastDemand;
            }
        }
        produceSupplyDemandText.text = (productSupplyCount - productDemandCount).ToString();
    }

    /// <summary>
    /// Province 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }

    private void OnDestroy()
    {
        
    }
}