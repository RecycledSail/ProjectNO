using System.Collections.Generic;

/// <summary>
/// ���κ� �� ���� Ŭ����
/// ���κ󽺿� ���κ󽺰� ������ �۹����� ����
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
    /// �α� ���� ����Ͽ� �۹��� �����մϴ�.
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
        int scale = (int)(modifier * province.population / 1); // �α� 1��� ����
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