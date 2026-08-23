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
    public MoneyAccount InvestmentAccount => Account;
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

    internal bool CanAddProvince(Province province) =>
        province != null && province.nation == null && !HasProvinces(province);

    /// <summary>
    /// 프로빈스를 추가하는 메서드
    /// 추가 시도 후 성공 여부에 따라 boolean 반환
    /// </summary>
    /// <param name="province">추가하고자 하는 프로빈스</param>
    /// <returns>추가 가능하면 true, 아니면 false</returns>
    public bool AddProvinces(Province province)
    {
        if (!CanAddProvince(province)) return false;

        provinces.Add(province);
        province.AddNation(this);
        return true;
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
        if (!CanPlaceConstructionMandate(buildingType, targetProvince, out _))
            return null;

        BuildingRecipe recipe = GlobalVariables.BUILDING_RECIPE[buildingType.name];
        MoneyAccount escrow = new(
            $"mandate:{name}:{targetProvince.name}:{buildingType.name}:{_constructionMandates.Count}");
        MoneyLedger ledger = targetProvince.ActiveLedger;
        if (!ledger.RegisterEmptyAccount(escrow))
            return null;

        if (recipe.InitialCapital > 0 && !ledger.TryTransfer(
            InvestmentAccount,
            escrow,
            recipe.InitialCapital,
            "Construction mandate operating capital"))
        {
            ledger.UnregisterEmptyAccount(escrow);
            return null;
        }

        ConstructionMandate mandate;
        try
        {
            mandate = new ConstructionMandate(
                this,
                buildingType,
                targetProvince,
                Math.Max(1, recipe.TimeToBuild),
                escrow,
                recipe.InitialCapital);
        }
        catch
        {
            ledger.TryTransfer(escrow, InvestmentAccount, escrow.Balance,
                "Construction mandate setup rollback");
            ledger.UnregisterEmptyAccount(escrow);
            throw;
        }

        _constructionMandates.Add(mandate);
        TryAssignConstructionCompany(mandate);
        return mandate;
    }

    public bool CanPlaceConstructionMandate(
        BuildingType buildingType,
        Province targetProvince,
        out string error)
    {
        error = null;
        if (buildingType == null || targetProvince == null)
        {
            error = "A building type and target province are required.";
            return false;
        }

        if (targetProvince.nation != this)
        {
            error = "Cannot build outside the nation.";
            return false;
        }

        if (IsConstructionQueued(buildingType, targetProvince))
        {
            error = "Already queued.";
            return false;
        }

        if (!GlobalVariables.BUILDING_RECIPE.TryGetValue(buildingType.name, out BuildingRecipe recipe))
        {
            error = $"No recipe for {buildingType.name}.";
            return false;
        }

        if (recipe.InitialCapital < 0)
        {
            error = "Operating capital must be nonnegative.";
            return false;
        }

        MoneyLedger ledger = targetProvince.ActiveLedger;
        if (ledger == null || InvestmentAccount?.Ledger != ledger)
        {
            error = "The investor and target must use the same active ledger.";
            return false;
        }

        Dictionary<string, ProductState> products = GetAccessibleProducts(targetProvince);
        if (recipe.requireItems.Count > 0 && products == null)
        {
            error = "No accessible market.";
            return false;
        }

        List<string> missingMaterials = new();
        foreach (KeyValuePair<string, int> requirement in recipe.requireItems)
        {
            long available = products != null && products.TryGetValue(requirement.Key, out ProductState product)
                ? product.Stock - GetReservedAmount(products, requirement.Key)
                : 0L;
            if (available < requirement.Value)
                missingMaterials.Add($"{requirement.Key}: have {Math.Max(0L, available):N0}, need {requirement.Value:N0}");
        }

        if (missingMaterials.Count > 0)
        {
            error = "Need more materials\n" + string.Join("\n", missingMaterials);
            return false;
        }

        if (InvestmentAccount.Balance < recipe.InitialCapital)
        {
            error = $"Need operating capital: have {InvestmentAccount.Balance:N0}, need {recipe.InitialCapital:N0}.";
            return false;
        }

        return true;
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

    private static Dictionary<string, ProductState> GetAccessibleProducts(Province province)
    {
        if (province == null)
            return null;

        return province.isConnectedToCapital && province.nation?.market != null
            ? province.nation.market.Products
            : province.market?.Products;
    }

    private long GetReservedAmount(Dictionary<string, ProductState> products, string productName)
    {
        long reserved = 0;
        foreach (ConstructionMandate mandate in _constructionMandates.Where(mandate => mandate.IsActive))
        {
            if (GetAccessibleProducts(mandate.TargetProvince) != products ||
                !GlobalVariables.BUILDING_RECIPE.TryGetValue(mandate.BuildingType.name, out BuildingRecipe recipe) ||
                !recipe.requireItems.TryGetValue(productName, out int amount))
                continue;

            reserved = checked(reserved + amount);
        }

        return reserved;
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
