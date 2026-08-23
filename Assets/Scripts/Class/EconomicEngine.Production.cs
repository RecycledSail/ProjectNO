using System;
using System.Collections.Generic;

public partial class EconomicEngine
{
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

    private int GetNeededByCategory(ProvinceEthnicPop pop, string category)
    {
        if (!CATEGORY_BASE_COEFFICIENT.TryGetValue(category, out double coefficient))
            return 0;

        double exponent = CATEGORY_LS_EXPONENT.TryGetValue(category, out double value)
            ? value
            : 0.0;
        double livingStandardFactor = exponent == 0.0
            ? 1.0
            : Math.Pow(pop.livingStandard, exponent);
        return (int)(coefficient * (pop.population / 1000.0) * livingStandardFactor);
    }

    public void ConsumeGoodsWeekly(Province province, string category, bool isConnectedToCapital)
    {
        if (province?.provinceEthnicPops == null ||
            !GlobalVariables.CATEGORIES.TryGetValue(category, out List<string> products) ||
            !CATEGORY_BASE_COEFFICIENT.ContainsKey(category))
        {
            return;
        }

        IDictionary<string, ProductState> market = isConnectedToCapital
            ? province.nation?.market?.Products
            : province.market?.Products;

        foreach (ProvinceEthnicPop pop in province.provinceEthnicPops)
        {
            if (pop == null)
                continue;

            PurchaseCategoryForPopulation(
                pop, products, GetNeededByCategory(pop, category), market);
        }
    }
}
