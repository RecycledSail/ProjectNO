using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class ProductButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // 제품 이름 텍스트
    public TMP_Text priceText; // 제품 가격 텍스트
    public TMP_Text supplyText; // 공급-수요 텍스트
    private ProductState productData; // 제품 데이터 텍스트

    /// <summary>
    /// Regiment 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void SetProductOrigin(ProductState productState)
    {
        this.productData = productState;
        nameText.text = productData.ProductName;
        UpdateProductButtonUI();
        GameManager.Instance.dayEvent.AddListener(UpdateProductButtonUI);
    }

    private void Update()
    {
        
    }

    private void UpdateProductButtonUI()
    {
        priceText.text = productData.Price.ToString();
        supplyText.text = (productData.LastSupply - productData.LastDemand).ToString();
    }

    /// <summary>
    /// ProductState 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
    }
}
