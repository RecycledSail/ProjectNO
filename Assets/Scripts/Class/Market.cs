using System.Collections.Generic;

/// <summary>
/// 프로빈스 내 시장 클래스
/// 프로빈스와 프로빈스가 보유한 작물들을 정의
/// </summary>
public class Market
{
    public Province province { get; }
    public List<Product> products { get; }

    public Market(Province province)
    {
        this.province = province;
        this.products = new List<Product>
        {
            new Product("Wheat", 0),
            new Product("Log", 0)
        };
    }

    /// <summary>
    /// 인구 수에 비례하여 작물을 생산합니다.
    /// </summary>
    public void ProduceCrops()
    {
        Nation curNation = this.province.nation;
        double modifier = 1;
        if (curNation != null)
        {
            double addMod = curNation.buffs.GetValueOrDefault(BuffKind.FOOD_PRODUCE_PER_UP, 0.0);
            modifier += addMod;
        }
        int scale = (int)(modifier * province.population / 1); // 인구 1명당 생산
        foreach (var crop in products)
        {
            crop.Produce(scale);
        }
    }

    public Product GetProduct(string cropName)
    {
        return products.Find(c => c.name == cropName);
    }
}