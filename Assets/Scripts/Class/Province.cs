using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

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
    public Dictionary<SpecialBuildingType, SpecialBuilding> specialBuildings { get; set; } = null;
    public int desolation { get; private set; } = 0;
    public int road { get; set; } = 0;
    public List<ProvinceEthnicPop> provinceEthnicPops { get; set; } = null;
    public long initialLocalTreasury;
    public MoneyAccount LocalTreasuryAccount { get; internal set; }
    public MoneyLedger LocalLedger { get; internal set; }
    public MoneyLedger ActiveLedger { get; internal set; }

    // 도로 연결 상태 캐시
    public bool isConnectedToCapital { get; set; } = false;

    // 프로빈스 현재 상태 정의 (고용된 인구)
    public long hiredPopulation { get; set; }

    /// <summary>
    /// Province 초기화
    /// </summary>
    /// <param name="id">프로빈스의 ID</param>
    /// <param name="name">프로빈스의 이름</param>
    /// <param name="topo">프로빈스의 토폴로지</param>
    public Province(int id, string name, Topography topo)
        : this(id, name, topo, 0L) { }

    public Province(int id, string name, Topography topo, long initialLocalTreasury)
    {
        // this -> instance의 id
        this.id = id;
        this.name = name;
        this.population = 0;
        this.topo = topo;
        this.initialLocalTreasury = initialLocalTreasury;
        this.provinceEthnicPops = new List<ProvinceEthnicPop>();
        // this.market 할당은 GlobalVariables의 Market.Init()에서 수행
        buildings = new();
        specialBuildings = new();
    }

    /// <summary>
    /// 초기 인구 계산 및 할당 (게임 시작 시에만 호출)
    /// </summary>
    public void InitializePopulation()
    {
        // 전체 인구 계산
        long totalPop = 0;
        foreach (var pep in provinceEthnicPops)
        {
            totalPop += pep.population;
        }

        // 내부 population 값 설정 (private set 우회)
        SetPopulation(totalPop);

        // 인구 할당
        AllocateSpecialBuildingPopulation();
    }

    /// <summary>
    /// 인구를 직접 설정 (초기화용)
    /// </summary>
    private void SetPopulation(long newPopulation)
    {
        typeof(Province).GetProperty("population")?.GetSetMethod(true)?.Invoke(this, new object[] { newPopulation });
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

    internal bool TryCollectMigrationAccounts(
        out List<MoneyAccount> accounts,
        out string error)
    {
        accounts = new List<MoneyAccount>();
        HashSet<MoneyAccount> distinctAccounts = new();

        foreach (ProvinceEthnicPop population in provinceEthnicPops)
        {
            if (population?.Account == null || !distinctAccounts.Add(population.Account))
            {
                error = "The province has an invalid population account.";
                return false;
            }

            accounts.Add(population.Account);
        }

        foreach (Building building in buildings.Values)
        {
            if (building?.Account == null || !distinctAccounts.Add(building.Account))
            {
                error = "The province has an invalid building account.";
                return false;
            }

            accounts.Add(building.Account);
        }

        error = null;
        return true;
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

    public void SetDesolation(int percent)
    {
        desolation = Math.Clamp(percent, 0, 100);
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
    /// 매 주 Product 생산 수행
    /// </summary>
    public void ProduceGoodsWeekly()
    {
        BaseProduction();
        BuildingProduction();
    }

    private void BuildingProduction()
    {
        if (market == null || buildings == null) return;

        foreach (Building building in buildings.Values)
        {
            if (building == null || building.level <= 0 || building.currentWorkers <= 0)
                continue;

            double scale = building.ProduceItem();
            if (scale <= 0.0)
                continue;

            scale = GetAffordableProductionScale(building, scale);
            if (scale <= 0.0)
                continue;

            if (!TryPurchaseBuildingInputs(building, scale))
                continue;

            AddBuildingOutputs(building, scale);
        }
    }

    private double GetAffordableProductionScale(Building building, double requestedScale)
    {
        if (building?.Account == null || building.buildingType?.requireItems == null ||
            requestedScale <= 0.0 || double.IsNaN(requestedScale))
        {
            return 0.0;
        }

        double availableScale = requestedScale;
        long oneScaleCost = 0L;
        foreach (var requiredItem in building.buildingType.requireItems)
        {
            if (requiredItem.Value <= 0)
                continue;

            if (string.IsNullOrEmpty(requiredItem.Key) ||
                !market.Products.TryGetValue(requiredItem.Key, out ProductState product) ||
                product.Price <= 0)
            {
                return 0.0;
            }

            availableScale = Math.Min(availableScale, (double)product.Stock / requiredItem.Value);
            oneScaleCost = checked(oneScaleCost + checked((long)requiredItem.Value * product.Price));
        }

        if (oneScaleCost > 0L)
            availableScale = Math.Min(availableScale, (double)building.Account.Balance / oneScaleCost);

        return availableScale;
    }

    private bool TryPurchaseBuildingInputs(Building building, double scale)
    {
        if (building?.Account == null || ActiveLedger == null ||
            building.buildingType?.requireItems == null)
        {
            return false;
        }

        List<PurchaseRequest> requests = new();
        foreach (var requiredItem in building.buildingType.requireItems)
        {
            int amount = checked((int)Math.Floor(requiredItem.Value * scale));
            if (amount <= 0)
                continue;

            if (string.IsNullOrEmpty(requiredItem.Key) ||
                !market.Products.TryGetValue(requiredItem.Key, out ProductState product))
            {
                return false;
            }

            requests.Add(new PurchaseRequest(product, amount));
        }

        return requests.Count == 0 || MarketSettlement.TryPurchaseBasket(
            requests,
            building.Account,
            ActiveLedger,
            requireFullQuantity: true).Success;
    }

    private void AddBuildingOutputs(Building building, double scale)
    {
        foreach (var produceItem in building.buildingType.produceItems)
        {
            int amount = (int)Math.Floor(produceItem.Value * scale);
            if (amount <= 0)
                continue;

            if (!market.Products.TryGetValue(produceItem.Key, out ProductState product))
            {
                int basePrice = GlobalVariables.PRODUCTS.TryGetValue(produceItem.Key, out Products productData)
                    ? productData.InitialPrice
                    : 1;
                market.AddProduct(produceItem.Key, basePrice);
                product = market.Products[produceItem.Key];
            }

            product.AddSupply(building.Account, amount);
        }
    }

    /// <summary>
    /// 매 주 실행되는 소비 단계
    /// nation market에서 가능하면 소비, 불가능하면 province market에서 소비
    /// 단, 수도와 도로로 연결되어 있어야 nation market에서 소비 가능
    /// </summary>


    /// <summary>
    /// 지역 내 모든 민족 집단의 월간 출생과 사망을 처리한다.
    /// </summary>
    public void ProcessMonthlyDemographics()
    {
        long newPopulation = 0;
        foreach (ProvinceEthnicPop pep in provinceEthnicPops)
        {
            newPopulation += pep.ProcessMonthlyDemographics();
        }
        population = newPopulation;

        AllocateSpecialBuildingPopulation();
    }

    /// <summary>
    /// 지역 내 모든 민족 집단의 연령계층을 1년 진행시킨다.
    /// </summary>
    public void AdvanceAgeGroupsOneYear()
    {
        foreach (ProvinceEthnicPop pep in provinceEthnicPops)
        {
            pep.AdvanceAgeGroupsOneYear();
        }

        population = provinceEthnicPops.Sum(pep => pep.population);
        AllocateSpecialBuildingPopulation();
    }

    /// <summary>
    /// 프로빈스에 자체적으로 달려 있는 기본 생산
    /// 아무리 빈약한 프로빈스여도 이 생산량만큼은 기본으로 가지게 됨
    /// 매 주 실행
    /// </summary>
    private void BaseProduction()
    {
        if (market == null || population <= 0 || provinceEthnicPops.Count == 0)
            return;

        Dictionary<ProvinceEthnicPop, long> populationWeights = provinceEthnicPops.ToDictionary(
            pep => pep,
            pep => pep.population);
        Dictionary<ProvinceEthnicPop, long> productionByPopulation = ProportionalAllocator.Allocate(
            100,
            populationWeights,
            pep => pep.Account.Id);

        foreach (string prodName in GlobalVariables.CATEGORIES["basic_food"])
        {
            ProductState ps;
            if (market.Products.TryGetValue(prodName, out ps))
            {
                // 여기 적절히 수절
                foreach (KeyValuePair<ProvinceEthnicPop, long> share in productionByPopulation)
                {
                    if (share.Value > 0)
                        ps.AddSupply(share.Key.Account, checked((int)share.Value));
                }
            }
        }
    }


    /// <summary>
    /// 우선순위에 따라 특수 건물에 인구를 할당한다.
    /// 각 SpecialBuilding이 수용할 수 있는 인구는 workerNeeded * level
    /// Town의 레벨이 부족하면 자동으로 증가 (Town은 가장 나중에 인구 할당)
    /// </summary>
    public void AllocateSpecialBuildingPopulation()
    {
        if (population == 0 || specialBuildings == null || specialBuildings.Count == 0)
            return;

        // 우선순위 순으로 SpecialBuilding 정렬
        var sortedSpecialBuildings = specialBuildings.Values
            .OrderBy(sb => sb.buildingType.priority)
            .ToList();

        // Town 찾기
        SpecialBuilding townBuilding = sortedSpecialBuildings.FirstOrDefault(sb => sb.buildingType.name == "Town");

        // Town이 없으면 반환
        if (townBuilding == null)
            return;

        long availablePopulation = population - hiredPopulation;
        long totalNonTownCapacity = 0;

        // 1단계: Town을 제외한 모든 건물의 필요 용량 계산
        foreach (SpecialBuilding specialBuilding in sortedSpecialBuildings)
        {
            if (specialBuilding.buildingType.name != "Town")
            {
                totalNonTownCapacity += specialBuilding.buildingType.workerNeeded * specialBuilding.level;
            }
        }

        // 2단계: Town 레벨 자동 조정 (필요시 증가)
        long townCapacity = townBuilding.buildingType.workerNeeded * townBuilding.level;
        while (townCapacity < totalNonTownCapacity && availablePopulation > 0)
        {
            townBuilding.level++;
            townCapacity = townBuilding.buildingType.workerNeeded * townBuilding.level;
        }

        // 3단계: Town을 제외한 건물들부터 인구 할당
        foreach (SpecialBuilding specialBuilding in sortedSpecialBuildings)
        {
            if (specialBuilding.buildingType.name != "Town")
            {
                long capacity = specialBuilding.buildingType.workerNeeded * specialBuilding.level;
                specialBuilding.currentWorkers = System.Math.Min(capacity, availablePopulation);
                availablePopulation -= specialBuilding.currentWorkers;
            }

            if (availablePopulation <= 0)
                break;
        }

        // 4단계: 남은 인구를 Town에 할당
        townCapacity = townBuilding.buildingType.workerNeeded * townBuilding.level;
        townBuilding.currentWorkers = System.Math.Min(townCapacity, availablePopulation);
    }

    /// <summary>
    /// 길이 없다면 건설
    /// </summary>
    /// <returns>도로를 건설했으면 true, 아니면 false</returns>
    public bool BuildRoad()
    {
        if (road == 1) return false; // 이미 도로가 있으면 실패
        road = 1;

        // 도로 건설 시 nation의 캐시 무효화
        if (nation != null && GameManager.Instance != null)
        {
            GameManager.Instance.InvalidateRoadCache(nation);
        }

        return true;
    }

    /// <summary>
    /// 길이 있다면 제거
    /// </summary>
    /// <returns>도로를 제거했으면 true, 아니면 false</returns>
    public bool RemoveRoad()
    {
        if (road == 0) return false; // 도로가 없으면 실패
        road = 0;

        // 도로 제거 시 nation의 캐시 무효화
        if (nation != null && GameManager.Instance != null)
        {
            GameManager.Instance.InvalidateRoadCache(nation);
        }

        return true;
    }

    public void ConstructBuilding(BuildingType buildingType)
    {

        // Timetobuild 이 0이 아니면 건설중인 상태를 반영할것 
        // 아직 미구현

        // Initialbalance를 건물이 다지어지는순간 balance에 반영해야됨
    }
}

