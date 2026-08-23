using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EconomicEngine : MonoBehaviour
{
    public void ConsumeFoodsWeekly(Province province, bool isConnectedToCapital)
    {
        if (isConnectedToCapital)
            CalculateTotalFoodsNeedsFromNationMarket(province);
        else
            CalculateTotalFoodsNeeds(province);
    }

    public void CalculateTotalFoodsNeedsFromNationMarket(Province province) =>
        ConsumeFoodFromMarket(province, province?.nation?.market?.Products);

    public void CalculateTotalFoodsNeeds(Province province) =>
        ConsumeFoodFromMarket(province, province?.market?.Products);

    private void ConsumeFoodFromMarket(
        Province province,
        IDictionary<string, ProductState> market)
    {
        if (province?.provinceEthnicPops == null)
            return;

        IReadOnlyList<string> foods = GlobalVariables.CATEGORIES.TryGetValue(
            "basic_food", out List<string> products)
            ? products
            : Array.Empty<string>();

        foreach (ProvinceEthnicPop pop in province.provinceEthnicPops)
        {
            if (pop == null)
                continue;

            int purchased = PurchaseCategoryForPopulation(
                pop, foods, pop.GetNeededFood(), market);
            pop.BuyFood(purchased);
        }
    }

    private int PurchaseCategoryForPopulation(
        ProvinceEthnicPop pop,
        IReadOnlyList<string> products,
        int needed,
        IDictionary<string, ProductState> market)
    {
        if (pop?.Account == null || pop.province?.ActiveLedger == null ||
            products == null || needed <= 0 || market == null)
        {
            return 0;
        }

        MoneyLedger ledger = pop.province.ActiveLedger;
        List<string> orderedProducts = products
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => market.TryGetValue(name, out ProductState product)
                ? product.LastDemand
                : 0)
            .ThenByDescending(name => market.TryGetValue(name, out ProductState product)
                ? product.Stock
                : 0)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();

        int purchased = 0;
        while (purchased < needed)
        {
            bool madeProgress = false;
            foreach (string productName in orderedProducts)
            {
                if (!market.TryGetValue(productName, out ProductState product))
                    continue;

                PurchaseResult result = MarketSettlement.TryPurchase(
                    product, pop.Account, needed - purchased, ledger);
                if (!result.Success || result.PurchasedQuantity <= 0)
                    continue;

                purchased += result.PurchasedQuantity;
                madeProgress = true;
                if (purchased == needed)
                    break;
            }

            if (!madeProgress)
                break;
        }

        return purchased;
    }
}
