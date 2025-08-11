using UnityEngine;
using System.Collections.Generic;

// Market.cs
public class Market
{
    public Province province;
    public List<Crop> crops = new List<Crop>();

    public Market(Province province)
    {
        this.province = province;
        crops.Add(new Crop("Wheat", 0));
        crops.Add(new Crop("Rice", 0));
        crops.Add(new Crop("Corn", 0));
    }

    public void ProduceCrops()
    {
        int scale = (int)(province.population);
        foreach (var crop in crops)
            crop.Produce(scale);
    }

    public void TradeWith(Market otherMarket)
    {
        // 간단 예시: Wheat 100개 넘으면 나누기
        Crop myWheat = crops.Find(c => c.name == "Wheat");
        Crop otherWheat = otherMarket.crops.Find(c => c.name == "Wheat");

        if (myWheat.amount > 100)
        {
            int tradeAmount = myWheat.amount / 2;
            myWheat.amount -= tradeAmount;
            otherWheat.amount += tradeAmount;
        }
    }
}



// Crop.cs
public class Crop
{
    public string name;
    public int amount;

    public Crop(string name, int initialAmount)
    {
        this.name = name;
        this.amount = initialAmount;
    }

    public void Produce(int quantity)
    {
        amount += quantity;
    }
}

/// <summary>
/// 프로빈스 내 시장 클래스
/// 프로빈스와 프로빈스가 보유한 작물들을 정의
/// </summary>
