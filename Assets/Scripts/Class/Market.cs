using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * 마켓 설계 요약
 * - ProvinceMarket : 주(Province) 하나의 마켓 상태(제품별 재고/가격/최근 수요공급)를 가짐
 * - Market : 전체 월드의 마켓 관리자. 초기화, 생산/소비 처리, 가격 업데이트, 조회 API 제공
 * - 가격 공식(간단):  price_t = clamp( basePrice * ((demand+β)/(supply+β))^α  을 지수평활로 섞음 )
 *      - α(elasticity) : 수요/공급 변화에 대한 민감도
 *      - β(small)      : 0 분모/분자 보호 상수
 *      - smoothing     : 이전 가격과 새 가격을 혼합(급변 방지)
 *
 */

public class Market
{

    // 주별 마켓(제품별 상태)이 들어감
    private static readonly Dictionary<string, ProvinceMarket> _marketsByProvince = new();

    // --------- 초기화 ----------
    // GlobalVariables.LoadDefaultData()가 끝난 뒤 한 번 호출하세요.

}

// ===== 주 단위 마켓 =====
[Serializable]

public class ProvinceMarket
{
    public string ProvinceName;
    public Dictionary<string, ProductState> Products = new();

    public ProvinceMarket(string provinceName)
    {
        ProvinceName = provinceName;
    }

    public void AddProduct(string productName, int basePrice)
    {
        if (!Products.ContainsKey(productName))
        {
            Products[productName] = new ProductState(productName, basePrice);
        }
    }

    public void ConsumeBasicFoods(string productName, int amount)
    {
        // 소비 처리 로직 구현
        if (Products.ContainsKey(productName))
        {
            Products[productName].LastDemand += amount;
            Products[productName].Stock = Math.Max(0, Products[productName].Stock - amount);
        }

    }

    public void UpdatePrices()
    {
        // 가격 업데이트 로직 구현
        foreach (var product in Products.Values)
        {
            int demand = product.LastDemand;
            int supply = product.LastSupply;

            // 가격 계산 공식 적용
            float PrePrice = product.Price;
            float newPrice = PrePrice * Mathf.Pow((demand + 1) / (supply + 1), product.Elasticity);

            // 지수 평활화 적용
            product.Price = Mathf.RoundToInt(0.7f * product.Price + 0.3f * newPrice);


        }
    }






}

// ===== 제품별 상태(한 주 내) =====
[Serializable]
public class ProductState
{
    public string ProductName;
    public int Stock;        // 현재 재고
    public int Price;        // 현재 가격
    public int LastPrice;    // 지난 턴 가격(가격 업데이트용)
    public int LastDemand;   // 최근 턴 소비량(가격 계산용)
    public int LastSupply;   // 최근 턴 생산량(가격 계산용)
    public float Elasticity = 1.0f; // α: 수요/공급 변화에 대한 민감도

    public ProductState(string name, int basePrice)
    {
        ProductName = name;
        Price = Math.Max(1, basePrice);
        Stock = 0;
        LastDemand = 0;
        LastSupply = 0;
    }
}
