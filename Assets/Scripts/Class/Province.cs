using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    public int id { get; }
    public string name { get; }
    
    private readonly List<PopSliceByCulture> _popsByCulture = new();
    public IReadOnlyList<PopSliceByCulture> PopsByCulture => _popsByCulture;
    public long population { get; private set; }

    public long Population => _popsByCulture.Sum(p => p.Count);
    public Topography topo { get; }
    public Nation nation { get; set; } = null;
    public ProvinceMarket market { get; set; }
    public List<Species> pops { get; set; } = null;
    public Dictionary<BuildingType, Building> buildings { get; set; } = null;
    public List<ProvinceEthnicPop> provinceEthnicPops { get; set; } = null;




    /// <summary>
    /// Province 초기화
    /// </summary>
    /// <param name="id">프로빈스의 ID</param>
    /// <param name="name">프로빈스의 이름</param>
    /// <param name="population">프로빈스의 인구 수</param>
    /// <param name="topo">프로빈스의 토폴로지</param>
    /// <param name="color">프로빈스의 색상</param>
    public Province(int id, string name, long population, Topography topo)
    {
        // this -> instance의 id
        this.id = id;
        this.name = name;
        this.population = population;
        this.topo = topo;
        // this.market 할당은 GlobalVariables의 Market.Init()에서 수행
        buildings = new();
        pops = new();
    }

    public void AddNation(Nation nation)
    {
        this.nation = nation;
    }

    public bool RemoveNation(Nation nation)
    {
        if (this.nation == nation)
        {
            this.nation = null;
            return true;
        }
        else return false;
    }
    public void SimulateTurn()
    {
        if (pops.Count == 0) return;

        population = 0;
        foreach (Species species in pops)
        {
            //species.Consume(market);
            //market.ProduceCrops();
            foreach (Building building in buildings.Values)
            {
                building.ProduceItem();
            }
            species.UpdateGrowth();

            population += species.population; // 이 줄로 동기화도 함께
        }
    }
    public void AddPop(Culture culture, long amount, float? birthOverride = null)
    {
        var s = _popsByCulture.FirstOrDefault(p => p.Culture.Id == culture.Id);
        if (s == null) _popsByCulture.Add(new PopSliceByCulture(culture, amount, birthOverride));
        else s.Add(amount);
    }

    public long GetPopulationByCulture(Culture culture)
        => _popsByCulture.Where(p => p.Culture.Id == culture.Id).Sum(p => p.Count);

    // (선택) 프로빈스 고유 보정치: 시장/건물/환경 등
    private float GetProvinceBirthRateModifier()
    {
        float mod = 0f;
        mod += market?.GetBirthRateModifier(this) ?? 0f;
        if (buildings != null)
            foreach (var b in buildings.Values) mod += b.GetBirthRateModifier(this);
        return mod;
    }

    // 한 턴 시뮬레이션: 문화별로 "그 슬라이스"의 출생률로 증가
    public void SimulateTurnPopulationGrowth()
    {
        if (_popsByCulture.Count == 0) return;

        float provinceMod = GetProvinceBirthRateModifier();

        foreach (var slice in _popsByCulture)
        {
            // (필수) 문화 기본률 + (선택) 슬라이스 오버라이드 + 프로빈스 보정 + (선택) 국가/정책 보정
            float baseRate = slice.BirthRateOverride ?? slice.Culture.BaseBirthRate;
            float nationPolicyBonus = nation?.GetBirthRateBonusForCulture(slice.Culture) ?? 0f; // 필요 시 Nation에 구현

            float effectiveRate = Math.Max(0f, baseRate + provinceMod + nationPolicyBonus);
            long births = (long)Math.Floor(slice.Count * effectiveRate);

            slice.Add(births);
        }
    }
}




public class PopSliceByCulture
{
    public Culture Culture;
    public long populationCount;
    public double BirthRateOverride;

    public PopSliceByCulture(Culture culture, long count, double birthOverride)
    {
        Culture = culture; populationCount = count; BirthRateOverride = birthOverride;
    }

    public void Add(long delta)    => populationCount = Math.Max(0, populationCount + delta);
    public void Remove(long delta) => populationCount = Math.Max(0, populationCount - delta);
}



