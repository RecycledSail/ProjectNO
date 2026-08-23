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
    public MoneyAccount Account { get; }
    public MoneyLedger Ledger { get; internal set; }
    public long balance
    {
        get => Account.Balance;
        set => Account.ReplaceForLoading(value);
    }
    public List<Regiment> regiments { get; set; }
    public Dictionary<(SpeciesSpec, Culture), EthnicGroup> ethnicGroups { get; set; }
    public Dictionary<Nation, Diplomacy> allies;
    public Dictionary<Nation, Diplomacy> enemies;
    public List<ResearchNode> doneResearches;
    public Dictionary<BuffKind, double> buffs;
    public NationMarket market;
    public GovernmentBudget governmentBudget;

    private readonly List<ConstructionMandate> _constructionMandates = new();
    public IReadOnlyList<ConstructionMandate> ConstructionMandates => _constructionMandates;

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

    /// <summary>
    /// 국가 생성자
    /// </summary>
    /// <param name="id">국가의 ID</param>
    /// <param name="name">국가의 이름(코드)</param>
    public Nation(int id, string name, List<ResearchNode> researches)
        : this(id, name, researches, 0L) { }

    public Nation(int id, string name, List<ResearchNode> researches, long initialBalance)
    {
        this.id = id;
        this.name = name;
        Account = new MoneyAccount($"nation:{name}:treasury", initialBalance);
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
        RetryPendingConstructionMandates();
    }

    public ConstructionMandate PlaceConstructionMandate(
        BuildingType buildingType,
        Province targetProvince)
    {
        if (buildingType == null ||
            targetProvince == null ||
            targetProvince.nation != this ||
            IsConstructionQueued(buildingType, targetProvince) ||
            !GlobalVariables.BUILDING_RECIPE.TryGetValue(
                buildingType.name,
                out BuildingRecipe recipe))
            return null;

        ConstructionMandate mandate = new(
            this,
            buildingType,
            targetProvince,
            Math.Max(1, recipe.TimeToBuild));

        _constructionMandates.Add(mandate);
        TryAssignConstructionCompany(mandate);
        return mandate;
    }

    public bool IsConstructionQueued(BuildingType buildingType, Province targetProvince)
    {
        return _constructionMandates.Any(mandate =>
            mandate.IsActive &&
            mandate.BuildingType == buildingType &&
            mandate.TargetProvince == targetProvince);
    }

    public void RetryPendingConstructionMandates()
    {
        foreach (ConstructionMandate mandate in _constructionMandates
            .Where(item => item.Status == ConstructionMandateStatus.Requested))
        {
            TryAssignConstructionCompany(mandate);
        }
    }

    private bool TryAssignConstructionCompany(ConstructionMandate mandate)
    {
        IEnumerable<Province> adjacentProvinces =
            GlobalVariables.ADJACENT_PROVINCES.TryGetValue(
                mandate.TargetProvince.name,
                out List<Province> adjacent)
                ? adjacent
                : Enumerable.Empty<Province>();

        IEnumerable<Province> candidateProvinces =
            new[] { mandate.TargetProvince }
                .Concat(adjacentProvinces)
                .Where(province => province != null)
                .Distinct();

        foreach (Province candidateProvince in candidateProvinces)
        {
            if (candidateProvince.nation != this)
                continue;

            foreach (ConstructionCompanyBuilding constructionCompany in
                candidateProvince.buildings.Values.OfType<ConstructionCompanyBuilding>())
            {
                if (constructionCompany.TryAssign(mandate))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 특수 건물의 건설 시간을 초기화한다.
    /// </summary>
    /// <param name="building">초기화할 특수 건물</param>
    public void AddSpecialBuildingToQueue(SpecialBuilding building)
    {
        building.manhoursLeft = GlobalVariables.BUILDING_RECIPE[building.buildingType.name].TimeToBuild;
        // specialBuildingsInProgress.Enqueue(building);
    }
}
