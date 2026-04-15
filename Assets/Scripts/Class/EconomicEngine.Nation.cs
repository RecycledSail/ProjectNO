// EconomicEngine.Nation.cs
using System.Collections.Generic;

public partial class EconomicEngine
{
    private const int GDP_HISTORY_WEEKS = 4;

    /// <summary>
    /// 국가의 주간 GDP를 계산합니다.
    /// 생산 접근법: 이번 주에 새로 공급된 물량의 가치 합산 (Σ LastSupply × Price)
    /// 이미 창고에 쌓인 재고는 포함하지 않아 이중계산을 방지합니다.
    /// </summary>
    public long CalculateGDP(Nation nation)
    {
        long gdp = 0L;

        foreach (ProductState ps in nation.market.Products.Values)
        {
            gdp += (long)ps.LastSupply * ps.Price;
        }

        return gdp;
    }

    /// <summary>
    /// 모든 국가의 GDP를 주간 단위로 갱신합니다.
    /// - GDP        : 이번 주 생산 기반 GDP (국가 간 비교용)
    /// - GDPAverage : 최근 4주 이동평균 (세금 계산 기준값)
    /// 생산/소비 처리가 끝난 뒤 매 주 틱 마지막에 호출하세요.
    /// </summary>
    public void UpdateGDPWeekly(IEnumerable<Nation> nations)
    {
        foreach (Nation nation in nations)
        {
            nation.GDP = CalculateGDP(nation);

            nation.GDPHistory.Enqueue(nation.GDP);
            if (nation.GDPHistory.Count > GDP_HISTORY_WEEKS)
                nation.GDPHistory.Dequeue();

            long sum = 0L;
            foreach (long gdp in nation.GDPHistory)
                sum += gdp;

            nation.GDPAverage = sum / nation.GDPHistory.Count;
        }
    }
}
