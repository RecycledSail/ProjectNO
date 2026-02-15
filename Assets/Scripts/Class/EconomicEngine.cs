// EconomyManager.cs (MonoBehaviour)
using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
/// <summary>
/// 경제 엔진을 관리하는 클래스}
public partial class EconomicEngine : MonoBehaviour
{
    // GC 줄이려면 임시 리스트를 멤버로 재사용 추천
    // private readonly List<ProvinceEthnicPop> _tmpAvailablePops = new();


        public void ConsumeFoodsWeekly(Province province, bool isConnectedToCapital)
    {
        // 캐시된 연결 상태 사용
        if (isConnectedToCapital)
        {
            // Nation market에서 소비 시도
            CalculateTotalFoodsNeedsFromNationMarket(province);
        }
        else
        {
            // 자신의 province market에서만 소비
            CalculateTotalFoodsNeeds(province);
        }
    }






   public void CalculateTotalFoodsNeedsFromNationMarket(Province province)
    {
        // 1. 필요 음식 수 계산
        int totalFoodNeeds = 0;
        long totalMoney = 0;
        List<ProvinceEthnicPop> availableEthnicPops = new();
        foreach (ProvinceEthnicPop pep in province.provinceEthnicPops)
        {
            if (pep.property > 0)
            {
                totalFoodNeeds += pep.GetNeededFood();
                totalMoney += pep.property;
                availableEthnicPops.Add(pep);
            }
            else if (pep.property < 0)
            {
                pep.BuyFood(0);
            }
        }

        if (totalFoodNeeds == 0) return;

        // 2. 필요 음식 수 대비 가지고 있는 음식의 비율 계산 (nation market)
        int currentFoods = 0;
        foreach (string prodName in GlobalVariables.CATEGORIES["basic_food"])
        {
            if (province.nation.market.Products.TryGetValue(prodName, out var ps))
            {
                currentFoods += ps.Stock;
            }
        }

        double percentage = Math.Min((double)currentFoods / (double)totalFoodNeeds, 1.0);
        totalFoodNeeds = (int)(totalFoodNeeds * percentage);

        long totalCost = 0;
        // 저번턴 수요량이 많은 순으로 리스트 정렬
        List<string> sortedFoodNames = GlobalVariables.CATEGORIES["basic_food"]
            .Select(name =>
            {
                province.nation.market.Products.TryGetValue(name, out var ps);
                return new { Name = name, PS = ps };
            })
            .OrderByDescending(x => x.PS != null ? x.PS.LastDemand : 0)
            .ThenByDescending(x => x.PS != null ? x.PS.Stock : 0)
            .Select(x => x.Name)
            .ToList();

        Dictionary<string, int> foodBuyAmount = new();
        int totalBoughtFoods = 0;
        while (totalFoodNeeds > totalBoughtFoods && totalCost < totalMoney)
        {
            foreach (string foodName in sortedFoodNames)
            {
                if (province.nation.market.Products.TryGetValue(foodName, out var ps))
                {
                    int pricefluctuation = ps.LastPrice / ps.Price;
                    int consumeAmount = Math.Min(ps.LastDemand * pricefluctuation + 1, ps.Stock);

                    if (consumeAmount > totalFoodNeeds - totalBoughtFoods)
                    {
                        consumeAmount = totalFoodNeeds - totalBoughtFoods;
                    }
                    if (totalCost + consumeAmount * ps.Price > totalMoney)
                    {
                        consumeAmount = (int)(totalMoney - totalCost) / ps.Price;
                        foodBuyAmount[foodName] = consumeAmount;
                        totalBoughtFoods += consumeAmount;
                        totalCost = totalMoney;
                    }
                    else
                    {
                        foodBuyAmount[foodName] = consumeAmount;
                        totalCost += consumeAmount * ps.Price;
                        totalBoughtFoods += consumeAmount;
                    }
                }
            }
        }

        // 3. nation market에서 음식 재고 차감
        foreach (string foodName in foodBuyAmount.Keys)
        {
            province.nation.market.Products[foodName].Stock -= foodBuyAmount[foodName];
        }

        // 4. pep에서 돈 차감
        foreach (ProvinceEthnicPop pep in availableEthnicPops)
        {
            int neededFood = pep.GetNeededFood();
            double neededFoodRatio = neededFood / (double)totalFoodNeeds;
            int remainingFood = (int)(neededFoodRatio * totalBoughtFoods);
            pep.BuyFood(remainingFood);
            pep.property -= (long)(neededFoodRatio * totalCost);
        }
    }

    /// <summary>
    /// 한 Province내에서 필요한 총 음식 수 계산 (로컬 market만 사용)
    /// 매 주 실행
    /// </summary>
    public void CalculateTotalFoodsNeeds(Province province)
    {
        // 플로우
        // 음식이 풍부하면 (1000 필요, 1001개 있음)
        //-> pep별로 구매 시도 (근데 여기서 pep 인원수보다 pep 자산이 없을수 있음)
        //-> 이걸 pep 개별로 전부 다 함

        // 음식이 부족하면 (1000 필요, 999개 있음)
        //-> 일단 999개 전부 다 삼
        //-> pep 순번대로 """"비율만큼(999/1000)""" 구매 시도

        // 1. 필요 음식 수 계산
        int totalFoodNeeds = 0;
        long totalMoney = 0;
        List<ProvinceEthnicPop> availableEthnicPops = new();
        foreach (ProvinceEthnicPop pep in province.provinceEthnicPops)
        {
            if (pep.property > 0)
            {
                totalFoodNeeds += pep.GetNeededFood();
                totalMoney += pep.property;
                availableEthnicPops.Add(pep);
            }
            // 자산이 부족한 경우
            else if (pep.property < 0)
            {
                pep.BuyFood(0);
            }
        }


        // 2. 필요 음식 수 대비 가지고 있는 음식의 비율 계산
        int currentFoods = 0;
        foreach (string prodName in GlobalVariables.CATEGORIES["basic_food"])
        {
            ProductState ps;
            if (province.market.Products.TryGetValue(prodName, out ps))
            {
                currentFoods += ps.Stock;
            }
        }

        // currentFoods 분자, totalFoodNeeds가 분모, Min 1해서 계산하면 될듯
        double percentage = Math.Min((double)currentFoods / (double)totalFoodNeeds, 1.0);
        totalFoodNeeds = (int)(totalFoodNeeds * percentage); // 실제 구매 시도할 음식 수

        long totalCost = 0;
        // 저번턴 수요량이 많은 순으로 리스트 정렬
        List<string> sortedFoodNames = GlobalVariables.CATEGORIES["basic_food"]
            .Select(name =>
            {
                province. market.Products.TryGetValue(name, out var ps);
                return new { Name = name, PS = ps };
            })
            .OrderByDescending(x => x.PS.LastDemand)   // 지난 턴 수요 많은 순
            .ThenByDescending(x => x.PS.Stock)         // (선택) 재고 많은 순으로 동률 정렬
            .Select(x => x.Name)
            .ToList();

        Dictionary<string, int> foodBuyAmount = new();
        int totalBoughtFoods = 0;
        while (totalFoodNeeds > totalBoughtFoods && totalCost < totalMoney)
        {
            foreach (string foodName in sortedFoodNames)
            {
                // 현재 가지고있는 돈이 부족하면 종료
                // 아직 이부분은 구현 못하겟음
                // 각 민족 집단간 가지고 있는 돈이 다르기 때문에 그부분을 반영해야함

                ProductState ps;
                if (province.market.Products.TryGetValue(foodName, out ps))
                {
                    // 지난 턴 소비량으로 이번턴 얼마나 살지에 대해 계산
                    // 속도공식을 이용
                    int pricefluctuation = ps.LastPrice / ps.Price;
                    int consumeAmount = Math.Min(ps.LastDemand * pricefluctuation + 1, ps.Stock);
                    // +1은 전턴 수요가 0일때를 방지하기위해 넣어둔 앞으로 개선필요w
                    if (consumeAmount > totalFoodNeeds - totalBoughtFoods)
                    {
                        consumeAmount = totalFoodNeeds - totalBoughtFoods;
                    }
                    if (totalCost + consumeAmount * ps.Price > totalMoney)
                    {
                        consumeAmount = (int)(totalMoney - totalCost) / ps.Price;
                        foodBuyAmount[foodName] = consumeAmount;
                        totalBoughtFoods += consumeAmount;
                        totalCost = totalMoney;
                    }
                    else
                    {
                        ps.Stock -= consumeAmount;
                        foodBuyAmount[foodName] = consumeAmount;
                        totalCost += consumeAmount * ps.Price;
                        totalBoughtFoods += consumeAmount;
                    }
                }

            }
        }

        if (totalFoodNeeds > 0)
        {
            // 3. 시장에서 음식 재고 차감
            // Dictionary에 들어있는 Key에 저장된 Value만큼 감소
            foreach (string foodName in foodBuyAmount.Keys)
            {
                province.market.Products[foodName].Stock -= foodBuyAmount[foodName];
            }

            // 4. pep에서 돈 차감
            // 인구 비율만큼 맞춰서 차감
            foreach (ProvinceEthnicPop pep in availableEthnicPops)
            {
                int neededFood = pep.GetNeededFood();
                double neededFoodRatio = neededFood / (double)totalFoodNeeds;
                int remainingFood = (int)(neededFoodRatio * totalBoughtFoods);
                pep.BuyFood(remainingFood);
                pep.property -= (long)(neededFoodRatio * totalCost);
            }
        }
    }







}