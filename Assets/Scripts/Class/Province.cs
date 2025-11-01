using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

/// <summary>
/// Topography Enum 
/// 평지, 산, 바다 정의
/// </summary>
public enum Topography
{
    Plane,
    Mountain,
    Sea
}

/// <summary>
/// 프로빈스 클래스
/// ID, 이름, 인구, 토폴로지, 대응 컬러 정의
/// </summary>
public class Province
{
    // 프로빈스 불변값 정의 (id, 이름)
    public int id { get; }
    public string name { get; }

    // 프로빈스 값 정의 (인구수, 나라, 마켓)
    public long population { get; private set; }
    public Topography topo { get; }
    public Nation nation { get; set; } = null;
    public ProvinceMarket market { get; set; }
    public Dictionary<BuildingType, Building> buildings { get; set; } = null;
    public List<ProvinceEthnicPop> provinceEthnicPops { get; set; } = null;

    // 프로빈스 현재 상태 정의 (고용된 인구)
    public long hiredPopulation { get; set; }

    /// <summary>
    /// Province 초기화
    /// </summary>
    /// <param name="id">프로빈스의 ID</param>
    /// <param name="name">프로빈스의 이름</param>
    /// <param name="topo">프로빈스의 토폴로지</param>
    public Province(int id, string name, Topography topo)
    {
        // this -> instance의 id
        this.id = id;
        this.name = name;
        this.population = 0;
        this.topo = topo;
        this.provinceEthnicPops = new List<ProvinceEthnicPop>();
        // this.market 할당은 GlobalVariables의 Market.Init()에서 수행
        buildings = new();
    }

    /// <summary>
    /// Province에 국가를 추가하는 함수
    /// Province 클래스에서 직접 실행되지 않음에 주의
    /// </summary>
    /// <param name="nation">추가할 국가</param>
    public void AddNation(Nation nation)
    {
        this.nation = nation;
    }

    /// <summary>
    /// Province에 국가를 제거하는 함수
    /// Province 클래스에서 직접 실행되지 않음에 주의
    /// </summary>
    /// <param name="nation">제거할 국가</param>
    /// <returns>현재 Province의 국가가 일치하면 제거하고 true, 아니면 false</returns>
    public bool RemoveNation(Nation nation)
    {
        if (this.nation == nation)
        {
            this.nation = null;
            return true;
        }
        else return false;
    }

    /// <summary>
    /// 매 일 실행되는 작업
    /// </summary>
    public void SimulateDailyTurn()
    {
        if (population == 0) return;
        // TODO: buildings 처리
    }

    /// <summary>
    /// 매 주 실행되는 작업
    /// </summary>
    public void SimulateWeeklyTurn()
    {
        //if (population == 0) return;
        CalculateTotalFoodsNeeds();
        BaseProduction();
        UpdatePopulation();
    }

    /// <summary>
    /// 인구수 재조사
    /// 매 주 실행
    /// </summary>
    public void UpdatePopulation()
    {
        // 각 pep에 대해서 인구 증감
        long newPopulation = 0;
        foreach (ProvinceEthnicPop pep in provinceEthnicPops)
        {
            newPopulation += pep.PopulationGrowth();
        }
        population = newPopulation;
    }


    /// <summary>
    /// 프로빈스에 자체적으로 달려 있는 기본 생산
    /// 아무리 빈약한 프로빈스여도 이 생산량만큼은 기본으로 가지게 됨
    /// 매 주 실행
    /// </summary>
    public void BaseProduction()

    {
        long provinceProduction = 1000;

        foreach (ProvinceEthnicPop pep in provinceEthnicPops)
        {
            long ethnicProduction = (long)(provinceProduction * ((double)pep.population / population));
            pep.property = ethnicProduction;
        }

        foreach (string prodName in GlobalVariables.CATEGORIES["basic_food"])
        {
            ProductState ps;
            if (market.Products.TryGetValue(prodName, out ps))
            {
                // 여기 적절히 수절
                ps.Stock += 100;
            }
        }
    }

    /// <summary>
    /// 한 Province내에서 필요한 총 음식 수 계산
    /// 매 주 실행
    /// </summary>
    public void CalculateTotalFoodsNeeds()
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
        foreach (ProvinceEthnicPop pep in provinceEthnicPops)
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
            if (market.Products.TryGetValue(prodName, out ps))
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
                market.Products.TryGetValue(name, out var ps);
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
                if (market.Products.TryGetValue(foodName, out ps))
                {
                    // 지난 턴 소비량으로 이번턴 얼마나 살지에 대해 계산
                    // 속도공식을 이용
                    int pricefluctuation = ps.LastPrice / ps.Price;
                    int consumeAmount = Math.Min(ps.LastDemand * pricefluctuation + 1, ps.Stock);
                    // +1은 전턴 수요가 0일때를 방지하기위해 넣어둠 앞으로 개선필요w
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
                market.Products[foodName].Stock -= foodBuyAmount[foodName];
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

    public void ConstructBuilding(BuildingType buildingType)
    {
        
        // Timetobuild 이 0이 아니면 건설중인 상태를 반영할것 
        // 아직 미구현

        // Initialbalance를 건물이 다지어지는순간 balance에 반영해야됨
    }


    




}

