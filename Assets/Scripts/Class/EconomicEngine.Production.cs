

// EconomicEngine.Consumption.cs
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EconomicEngine
{
    // 카테고리별 기본 소비 계수 (인구 1000명당 수요량)
    private static readonly Dictionary<string, double> CATEGORY_BASE_COEFFICIENT = new()
    {
        { "clothing_material",     0.5  },
        { "construction_material", 0.1  },
        { "industrial_material",   0.05 },
        { "monster_material",      0.1  },
        { "military_equipment",    0.05 },
        { "luxury_basic",          0.2  },
        { "luxury_premium",        0.05 },
    };

    // 카테고리별 생활수준(livingStandard) 지수 (0 = 무관, 1 = 선형, 1.5 = 그 이상)
    private static readonly Dictionary<string, double> CATEGORY_LS_EXPONENT = new()
    {
        { "clothing_material",     0.0 },
        { "construction_material", 0.0 },
        { "industrial_material",   0.0 },
        { "monster_material",      1.0 },
        { "military_equipment",    0.0 },
        { "luxury_basic",          1.0 },
        { "luxury_premium",        1.5 },
    };

    /// <summary>
    /// pep의 카테고리별 주간 필요 수량 계산
    /// </summary>
    private int GetNeededByCategory(ProvinceEthnicPop pep, string category)
    {
        if (!CATEGORY_BASE_COEFFICIENT.TryGetValue(category, out double coeff)) return 0;
        double lsExp = CATEGORY_LS_EXPONENT.TryGetValue(category, out double exp) ? exp : 0.0;
        double lsFactor = lsExp == 0.0 ? 1.0 : Math.Pow(pep.livingStandard, lsExp);
        return (int)(coeff * (pep.population / 1000.0) * lsFactor);
    }

    /// <summary>
    /// Province의 특정 카테고리 상품 주간 소비 처리.
    /// 수도 연결 여부에 따라 Nation/Province 마켓에서 소비.
    /// </summary>
    public void ConsumeGoodsWeekly(Province province, string category, bool isConnectedToCapital)
    {
        if (!GlobalVariables.CATEGORIES.ContainsKey(category)) return;
        if (!CATEGORY_BASE_COEFFICIENT.ContainsKey(category)) return;

        if (isConnectedToCapital)
            ConsumeGoodsFromNationMarket(province, category);
        else
            ConsumeGoodsFromProvinceMarket(province, category);
    }

    private void ConsumeGoodsFromNationMarket(Province province, string category)
    {
        // 1. 총 필요량 및 총 자산 계산
        int totalNeeds = 0;
        long totalMoney = 0;
        List<ProvinceEthnicPop> availablePops = new();

        foreach (ProvinceEthnicPop pep in province.provinceEthnicPops)
        {
            if (pep.property > 0)
            {
                totalNeeds += GetNeededByCategory(pep, category);
                totalMoney += pep.property;
                availablePops.Add(pep);
            }
        }

        if (totalNeeds == 0 || totalMoney == 0) return;

        // 2. 재고 대비 구매 가능 비율로 실제 목표 수량 결정
        int currentStock = 0;
        foreach (string prodName in GlobalVariables.CATEGORIES[category])
        {
            if (province.nation.market.Products.TryGetValue(prodName, out var ps))
                currentStock += ps.Stock;
        }

        double percentage = Math.Min((double)currentStock / totalNeeds, 1.0);
        totalNeeds = (int)(totalNeeds * percentage);
        if (totalNeeds == 0) return;

        // 3. 지난 턴 수요 많은 순으로 상품 정렬
        List<string> sortedNames = GlobalVariables.CATEGORIES[category]
            .Select(name =>
            {
                province.nation.market.Products.TryGetValue(name, out var ps);
                return new { Name = name, PS = ps };
            })
            .OrderByDescending(x => x.PS != null ? x.PS.LastDemand : 0)
            .ThenByDescending(x => x.PS != null ? x.PS.Stock : 0)
            .Select(x => x.Name)
            .ToList();

        // 4. 예산 제약 하에 구매 (돈이 부족하면 가진 예산만큼만 소비)
        Dictionary<string, int> buyAmount = new();
        int totalBought = 0;
        long totalCost = 0;

        while (totalNeeds > totalBought && totalCost < totalMoney)
        {
            foreach (string name in sortedNames)
            {
                if (province.nation.market.Products.TryGetValue(name, out var ps))
                {
                    int priceFluctuation = ps.LastPrice / Math.Max(1, ps.Price);
                    int amount = Math.Min(ps.LastDemand * priceFluctuation + 1, ps.Stock);

                    if (amount > totalNeeds - totalBought)
                        amount = totalNeeds - totalBought;

                    if (totalCost + (long)amount * ps.Price > totalMoney)
                    {
                        amount = (int)((totalMoney - totalCost) / Math.Max(1, ps.Price));
                        buyAmount[name] = buyAmount.TryGetValue(name, out int prev) ? prev + amount : amount;
                        totalBought += amount;
                        totalCost = totalMoney;
                        break;
                    }
                    else
                    {
                        buyAmount[name] = buyAmount.TryGetValue(name, out int prev) ? prev + amount : amount;
                        totalCost += (long)amount * ps.Price;
                        totalBought += amount;
                    }
                }

                if (totalNeeds <= totalBought || totalCost >= totalMoney) break;
            }
        }

        // 5. Nation 마켓 재고 차감
        foreach (var kv in buyAmount)
            province.nation.market.Products[kv.Key].Stock -= kv.Value;

        // 6. pep별 인구 비율에 따라 돈 차감
        foreach (ProvinceEthnicPop pep in availablePops)
        {
            int needed = GetNeededByCategory(pep, category);
            double ratio = needed / (double)totalNeeds;
            pep.property -= (long)(ratio * totalCost);
        }
    }

    private void ConsumeGoodsFromProvinceMarket(Province province, string category)
    {
        // 1. 총 필요량 및 총 자산 계산
        int totalNeeds = 0;
        long totalMoney = 0;
        List<ProvinceEthnicPop> availablePops = new();

        foreach (ProvinceEthnicPop pep in province.provinceEthnicPops)
        {
            if (pep.property > 0)
            {
                totalNeeds += GetNeededByCategory(pep, category);
                totalMoney += pep.property;
                availablePops.Add(pep);
            }
        }

        if (totalNeeds == 0 || totalMoney == 0) return;

        // 2. 재고 대비 구매 가능 비율로 실제 목표 수량 결정
        int currentStock = 0;
        foreach (string prodName in GlobalVariables.CATEGORIES[category])
        {
            if (province.market.Products.TryGetValue(prodName, out var ps))
                currentStock += ps.Stock;
        }

        double percentage = Math.Min((double)currentStock / totalNeeds, 1.0);
        totalNeeds = (int)(totalNeeds * percentage);
        if (totalNeeds == 0) return;

        // 3. 지난 턴 수요 많은 순으로 상품 정렬
        List<string> sortedNames = GlobalVariables.CATEGORIES[category]
            .Select(name =>
            {
                province.market.Products.TryGetValue(name, out var ps);
                return new { Name = name, PS = ps };
            })
            .OrderByDescending(x => x.PS != null ? x.PS.LastDemand : 0)
            .ThenByDescending(x => x.PS != null ? x.PS.Stock : 0)
            .Select(x => x.Name)
            .ToList();

        // 4. 예산 제약 하에 구매 (돈이 부족하면 가진 예산만큼만 소비)
        Dictionary<string, int> buyAmount = new();
        int totalBought = 0;
        long totalCost = 0;

        while (totalNeeds > totalBought && totalCost < totalMoney)
        {
            foreach (string name in sortedNames)
            {
                if (province.market.Products.TryGetValue(name, out var ps))
                {
                    int priceFluctuation = ps.LastPrice / Math.Max(1, ps.Price);
                    int amount = Math.Min(ps.LastDemand * priceFluctuation + 1, ps.Stock);

                    if (amount > totalNeeds - totalBought)
                        amount = totalNeeds - totalBought;

                    if (totalCost + (long)amount * ps.Price > totalMoney)
                    {
                        amount = (int)((totalMoney - totalCost) / Math.Max(1, ps.Price));
                        buyAmount[name] = buyAmount.TryGetValue(name, out int prev) ? prev + amount : amount;
                        totalBought += amount;
                        totalCost = totalMoney;
                        break;
                    }
                    else
                    {
                        buyAmount[name] = buyAmount.TryGetValue(name, out int prev) ? prev + amount : amount;
                        totalCost += (long)amount * ps.Price;
                        totalBought += amount;
                    }
                }

                if (totalNeeds <= totalBought || totalCost >= totalMoney) break;
            }
        }

        // 5. Province 마켓 재고 차감
        foreach (var kv in buyAmount)
            province.market.Products[kv.Key].Stock -= kv.Value;

        // 6. pep별 인구 비율에 따라 돈 차감
        foreach (ProvinceEthnicPop pep in availablePops)
        {
            int needed = GetNeededByCategory(pep, category);
            double ratio = needed / (double)totalNeeds;
            pep.property -= (long)(ratio * totalCost);
        }
    }
}
