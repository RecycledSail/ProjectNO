using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 국가 클래스
/// ID, 이름, 소유 프로빈스, 재산 정의
/// </summary>
public class Nation : IBuildingInvestor
{
    public int id;
    public string name { get; }
    public Color32 color { get; set; }
    public List<Province> provinces { get; set; }
    public Province capital { get; set; } = null;
    public long balance { get; set; }
    public List<Regiment> regiments { get; set; }
    public Dictionary<(SpeciesSpec, Culture), EthnicGroup> ethnicGroups { get; set; }
    public Dictionary<Nation, Diplomacy> allies;
    public Dictionary<Nation, Diplomacy> enemies;
    public List<ResearchNode> doneResearches;
    public Dictionary<BuffKind, double> buffs;
    public ConstructionRequest constructionRequest;
    public NationMarket market;
    public GovernmentBudget governmentBudget;

    /// <summary>이번 주 GDP (생산 기준: Σ LastSupply × Price)</summary>
    public long GDP { get; set; }

    /// <summary>세금 계산 등 안정적 기준값으로 쓸 4주 이동평균 GDP</summary>
    public long GDPAverage { get; set; }

    /// <summary>최근 4주 GDP 기록 (이동평균 산출용)</summary>
    public readonly Queue<long> GDPHistory = new();

    /// <summary>연구 투자 정책으로 적립된 연구 포인트 (연구 엔진에서 소진)</summary>
    public long researchFund { get; set; } = 0;



    // Getter
    public long Population => provinces.Sum(x => x.population);

    // 건축 관련 멤버    
    public double nationManhour = 0.0; // 국가가 현재 가지는 건축 노동력 (인시)
    public Queue<Building> buildingsInProgress = new Queue<Building>(); // 현재 국가에서 건축 중인 빌딩들 

    /// <summary>
    /// 국가 생성자
    /// </summary>
    /// <param name="id">국가의 ID</param>
    /// <param name="name">국가의 이름(코드)</param>
    public Nation(int id, string name, List<ResearchNode> researches)
    {
        this.id = id;
        this.name = name;
        provinces = new List<Province>();
        regiments = new List<Regiment>();
        doneResearches = researches;
        buffs = new Dictionary<BuffKind, double>();
        foreach (ResearchNode research in doneResearches)
        {
            foreach (Buff buff in research.buffs)
            {
                double prevValue = 0;
                buffs.TryGetValue(buff.baseBuff, out prevValue);
                buffs.Add(buff.baseBuff, prevValue + buff.power);
            }
        }
        ethnicGroups = new();
        allies = new();
        enemies = new();
        constructionRequest = new ConstructionRequest(this);
        market = new NationMarket(this.name);
        governmentBudget = new GovernmentBudget(market, this);
    }

    /// <summary>
    /// 해당 프로빈스가 있는지 검사하는 메서드
    /// </summary>
    /// <param name="province">검사하고자 하는 프로빈스</param>
    /// <returns>프로빈스 보유 중이면 true, 아니면 false</returns>
    public bool HasProvinces(Province province)
    {
        return provinces.Find(x => x.Equals(province)) != null;
    }

    /// <summary>
    /// 프로빈스를 추가하는 메서드
    /// 추가 시도 후 성공 여부에 따라 boolean 반환
    /// </summary>
    /// <param name="province">추가하고자 하는 프로빈스</param>
    /// <returns>추가 가능하면 true, 아니면 false</returns>
    public bool AddProvinces(Province province)
    {
        if (!HasProvinces(province))
        {
            provinces.Add(province);
            province.AddNation(this);
            return true;
        }
        else return false;
    }

    /// <summary>
    /// 프로빈스를 제거하는 메서드
    /// 제거 시도 후 성공 여부에 따라 boolean 반환
    /// </summary>
    /// <param name="province">제거하고자 하는 프로빈스</param>
    /// <returns>제거 가능하면 true, 아니면 false</returns>
    public bool RemoveProvinces(Province province)
    {
        bool avail = provinces.Remove(province);
        if (avail)
        {
            return province.RemoveNation(this);
        }
        else return false;
    }

    /// <summary>
    /// 연대를 추가하는 메서드
    /// 추가 시도 후 성공 여부에 따라 boolean 반환
    /// </summary>
    /// <param name="regiment">추가하고자 하는 연대</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    public bool AddRegiment(Regiment regiment)
    {
        if (regiment == null) return false;
        else
        {
            this.regiments.Add(regiment);
            return true;
        }
    }

    /// <summary>
    /// 매 주 실행되는 작업
    /// </summary>
    public void SimulateWeeklyTurn()
    {
        CalculateManhour();
        ProgressBuild();
    }


    /// <summary>
    /// 건축 노동력 (인시) 재계산
    /// 매 주 수행
    /// </summary>
    private void CalculateManhour()
    {
        double currentManhour = 0.0;
        currentManhour = GlobalVariables.minimumNationManHour;

        //TODO: 건축업체의 노동력 반영

        nationManhour = currentManhour;
    }

    /// <summary>
    /// 건축 중인 건물에서 건축 시도
    /// 매 주 수행
    /// </summary>
    private void ProgressBuild()
    {
        double remainingManhour = nationManhour;
        while (buildingsInProgress.Count > 0 && remainingManhour > 0.0)
        {
            if (buildingsInProgress.TryPeek(out Building building))
            {
                double spentManhour = Math.Min(remainingManhour, building.manhoursLeft);
                building.manhoursLeft -= spentManhour;
                if (building.manhoursLeft <= 0.0)
                {
                    building.level++;
                    building.province.buildings[building.buildingType] = building;
                    buildingsInProgress.Dequeue();
                    constructionRequest.RemoveFirstBuildingReservation(building.buildingType, building.province);
                }
                remainingManhour -= spentManhour;
            }
        }
        // if (specialBuildingsInProgress.Count > 0 && remainingManhour > 0.0)
        // {
        //     if (specialBuildingsInProgress.TryPeek(out SpecialBuilding specialBuilding))
        //     {
        //         double spentManhour = Math.Min(remainingManhour, specialBuilding.manhoursLeft + 1);
        //         specialBuilding.manhoursLeft -= spentManhour;
        //         if (specialBuilding.manhoursLeft <= 0.0)
        //         {
        //             specialBuilding.province.specialBuildings[specialBuilding.buildingType] = specialBuilding;
        //             specialBuildingsInProgress.Dequeue();
        //         }
        //         remainingManhour -= spentManhour;
        //     }
        // }
    }

    /// <summary>
    /// buildingsInProgress에 building을 enqueue
    /// dequeue는 ProgressBuild에서 수행
    /// </summary>
    /// <param name="building">Queue에 집어넣을 buildings</param>
    public void AddToBuildQueue(Building building)
    {
        building.manhoursLeft = GetBuildTime(building.buildingType);
        buildingsInProgress.Enqueue(building);
    }

    public bool IsInBuildQueue(BuildingType buildingType, Province province)
    {
        return buildingsInProgress.Any(building =>
            building.buildingType == buildingType &&
            building.province == province);
    }

    private int GetBuildTime(BuildingType buildingType)
    {
        if (buildingType != null &&
            GlobalVariables.BUILDING_RECIPE.TryGetValue(buildingType.name, out BuildingRecipe recipe))
            return Math.Max(1, recipe.TimeToBuild);

        Debug.LogWarning($"[BuildQueue] Missing building recipe for {buildingType?.name ?? "NULL"}. Using default build time.");
        return 20;
    }

    /// <summary>
    /// specialBuildingsInProgress에 specialBuilding을 enqueue
    /// dequeue는 ProgressBuild에서 수행
    /// </summary>
    /// <param name="building">Queue에 집어넣을 특수 건물</param>
    public void AddSpecialBuildingToQueue(SpecialBuilding building)
    {
        building.manhoursLeft = GlobalVariables.BUILDING_RECIPE[building.buildingType.name].TimeToBuild;
        // specialBuildingsInProgress.Enqueue(building);
    }
}
