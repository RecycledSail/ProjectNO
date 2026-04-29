using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────────
// 국가 정책 배분을 정의하는 데이터 클래스
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 발행된 화폐를 주입하는 4가지 국가 정책 채널.
/// 각 필드에 금액을 설정하면 PrintMoney() 시 해당 경로로 경제에 투입됩니다.
/// </summary>
public class PolicyAllocation
{
    /// <summary>대학 연구 투자 — 국가 연구 포인트(researchFund)에 직접 적립</summary>
    public long ResearchFund = 0;

    /// <summary>
    /// 군인/공무원 월급 — 연대 주둔지 팝의 property 증가.
    /// 연대가 없으면 수도 팝에 분배.
    /// </summary>
    public long MilitarySalary = 0;

    /// <summary>
    /// 산업 지원 — BuildingType.name → 보조금 금액.
    /// 해당 타입의 모든 건물에 레벨 비례로 balance 추가.
    /// </summary>
    public Dictionary<string, long> IndustrySubsidy = new();

    /// <summary>부동산 정책 — 전체 팝 property 분배 + 생활수준 소폭 상승</summary>
    public long RealEstateFund = 0;

    /// <summary>4개 채널의 합계 = 이번 주 총 발행액</summary>
    public long Total =>
        ResearchFund + MilitarySalary +
        (IndustrySubsidy.Count > 0 ? IndustrySubsidy.Values.Sum() : 0L) +
        RealEstateFund;

    /// <summary>산업 보조금 합계</summary>
    public long IndustryTotal() =>
        IndustrySubsidy.Count > 0 ? IndustrySubsidy.Values.Sum() : 0L;
}

// ───────────────────────────────────────────────────────────────────────────────
// 예산 관리 본체
// ───────────────────────────────────────────────────────────────────────────────

public class GovernmentBudget
{
    public NationMarket market;
    public Nation nation;

    public float taxRate = 0.1f;

    /// <summary>경제 내 총 통화 공급량</summary>
    public long MoneySupply { get; private set; }

    /// <summary>이번 주 세입</summary>
    public long WeeklyTaxRevenue { get; private set; }

    /// <summary>인플레이션율 (%) — 최근 4주 가격지수 기준 변화율</summary>
    public float InflationRate { get; private set; }

    /// <summary>현재 가격지수 (가중 평균 물가)</summary>
    public float CurrentPriceIndex { get; private set; }

    /// <summary>다음 주 발행 및 정책 배분 설정. UI에서 이 객체를 채워서 전달합니다.</summary>
    public PolicyAllocation Policy { get; set; } = new();

    /// <summary>이번 주 총 발행 예정액 (Policy.Total의 읽기 전용 뷰)</summary>
    public long PendingMoneyPrint => Policy.Total;

    private readonly Queue<float> _priceIndexHistory = new();
    private const int PRICE_HISTORY_WEEKS = 4;

    public GovernmentBudget(NationMarket market, Nation nation)
    {
        this.market = market;
        this.nation = nation;
        MoneySupply = 1_000_000L;
    }

    // ─── 공개 턴 처리 메서드 ──────────────────────────────────────

    /// <summary>매주 GDP 이동평균 × 세율로 세금을 징수합니다.</summary>
    public long CollectTaxes()
    {
        long revenue = (long)(nation.GDPAverage * taxRate);
        WeeklyTaxRevenue = revenue;
        nation.balance += revenue;
        return revenue;
    }

    /// <summary>
    /// Policy에 설정된 4가지 채널로 화폐를 발행하고 각 정책 효과를 적용합니다.
    /// 처리 후 Policy는 초기화됩니다.
    /// </summary>
    public void PrintMoney()
    {
        long total = Policy.Total;
        if (total <= 0) return;

        nation.balance += total;
        MoneySupply += total;

        ApplyResearchPolicy();
        ApplyMilitarySalary();
        ApplyIndustrySubsidy();
        ApplyRealEstatePolicy();

        Policy = new PolicyAllocation();
    }

    /// <summary>
    /// 매주 가격지수와 인플레이션율을 갱신합니다.
    /// 가격 업데이트가 끝난 뒤 호출하세요.
    /// </summary>
    public void UpdateInflation()
    {
        float priceIndex = CalculatePriceIndex();
        CurrentPriceIndex = priceIndex;

        _priceIndexHistory.Enqueue(priceIndex);
        if (_priceIndexHistory.Count > PRICE_HISTORY_WEEKS)
            _priceIndexHistory.Dequeue();

        if (_priceIndexHistory.Count >= 2 && _priceIndexHistory.Peek() > 0f)
        {
            float oldest = _priceIndexHistory.Peek();
            InflationRate = (priceIndex - oldest) / oldest * 100f;
        }
        else
        {
            InflationRate = 0f;
        }
    }

    /// <summary>현재 인플레이션 상태를 텍스트로 반환합니다.</summary>
    public string GetInflationStatus()
    {
        if (InflationRate > 10f) return "Hyperinflation";
        if (InflationRate >  5f) return "High Inflation";
        if (InflationRate >  2f) return "Inflation";
        if (InflationRate >  0f) return "Mild Rise";
        if (InflationRate < -5f) return "Deflation";
        if (InflationRate < -2f) return "Mild Fall";
        return "Stable";
    }

    /// <summary>
    /// 산업 지원을 건물 타입을 지정하지 않고 국가 내 모든 건물에 균등 투자할 때 사용하는
    /// 편의 메서드. BudgetUI에서 "산업 총액" 슬라이더를 처리할 때 호출합니다.
    /// </summary>
    public void SetIndustrySubsidyTotal(long totalAmount)
    {
        Policy.IndustrySubsidy.Clear();
        if (totalAmount <= 0) return;

        // 국가 내 존재하는 BuildingType 이름 수집
        HashSet<string> typeNames = new();
        foreach (Province province in nation.provinces)
            foreach (BuildingType bt in province.buildings.Keys)
                typeNames.Add(bt.name);

        if (typeNames.Count == 0) return;

        long perType = totalAmount / typeNames.Count;
        long remainder = totalAmount - perType * typeNames.Count;
        bool first = true;
        foreach (string typeName in typeNames)
        {
            Policy.IndustrySubsidy[typeName] = first ? perType + remainder : perType;
            first = false;
        }
    }

    // ─── 정책 적용 (private) ──────────────────────────────────────

    /// <summary>
    /// [연구 투자] 배분액을 국가 연구 포인트에 적립합니다.
    /// 연구 엔진이 매 턴 nation.researchFund를 소진하여 연구 진행에 사용합니다.
    /// </summary>
    private void ApplyResearchPolicy()
    {
        if (Policy.ResearchFund <= 0) return;
        nation.researchFund += Policy.ResearchFund;
    }

    /// <summary>
    /// [군인/공무원 월급] 배분액을 연대 주둔지 팝에 병사 수 비례로 분배합니다.
    /// 연대가 없으면 수도 팝에 분배합니다.
    /// </summary>
    private void ApplyMilitarySalary()
    {
        if (Policy.MilitarySalary <= 0) return;

        List<(ProvinceEthnicPop pep, long weight)> targets = new();
        long totalWeight = 0;

        foreach (Regiment regiment in nation.regiments)
        {
            if (regiment.location?.provinceEthnicPops == null) continue;
            int soldiers = regiment.GetUnitCount();
            if (soldiers <= 0) continue;

            foreach (ProvinceEthnicPop pep in regiment.location.provinceEthnicPops)
            {
                targets.Add((pep, soldiers));
                totalWeight += soldiers;
            }
        }

        // 연대 없으면 수도 팝으로 대체
        if (targets.Count == 0 && nation.capital?.provinceEthnicPops != null)
        {
            foreach (ProvinceEthnicPop pep in nation.capital.provinceEthnicPops)
            {
                targets.Add((pep, pep.population));
                totalWeight += pep.population;
            }
        }

        if (totalWeight <= 0) return;

        long amount = Policy.MilitarySalary;
        foreach (var (pep, weight) in targets)
            pep.property += (long)((double)weight / totalWeight * amount);
    }

    /// <summary>
    /// [산업 지원] BuildingType별 보조금을 국가 내 동일 타입 건물에 레벨 비례로 분배합니다.
    /// </summary>
    private void ApplyIndustrySubsidy()
    {
        if (Policy.IndustrySubsidy.Count == 0) return;

        foreach (var (typeName, subsidy) in Policy.IndustrySubsidy)
        {
            if (subsidy <= 0) continue;

            List<Building> targets = new();
            foreach (Province province in nation.provinces)
                foreach (var kv in province.buildings)
                    if (kv.Key.name == typeName)
                        targets.Add(kv.Value);

            if (targets.Count == 0) continue;

            long totalLevels = targets.Sum(b => (long)Math.Max(1, b.level));
            foreach (Building building in targets)
            {
                long share = (long)((double)Math.Max(1, building.level) / totalLevels * subsidy);
                // int 오버플로우 방지
                building.balance += (int)Math.Min(share, int.MaxValue - (long)building.balance);
            }
        }
    }

    /// <summary>
    /// [부동산 정책] 배분액을 전체 팝에 인구 비례로 분배하고 생활수준을 소폭 상승시킵니다.
    /// 생활수준 상승분 = (투자액 / GDP평균) × 0.1, 최대 10.0 상한.
    /// </summary>
    private void ApplyRealEstatePolicy()
    {
        if (Policy.RealEstateFund <= 0) return;

        long totalPop = nation.Population;
        if (totalPop <= 0) return;

        long amount = Policy.RealEstateFund;
        double lsBoost = (double)amount / Math.Max(1L, nation.GDPAverage) * 0.1;

        foreach (Province province in nation.provinces)
        {
            foreach (ProvinceEthnicPop pep in province.provinceEthnicPops)
            {
                pep.property += (long)((double)pep.population / totalPop * amount);
                pep.livingStandard = Math.Min(pep.livingStandard + lsBoost, 10.0);
            }
        }
    }

    // ─── Private helpers ─────────────────────────────────────────

    private float CalculatePriceIndex()
    {
        if (market.Products.Count == 0) return 1f;

        float weightedSum = 0f;
        long totalWeight = 0;

        foreach (ProductState ps in market.Products.Values)
        {
            int weight = ps.LastSupply + ps.LastDemand;
            if (weight > 0)
            {
                weightedSum += ps.Price * weight;
                totalWeight += weight;
            }
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 1f;
    }
}
