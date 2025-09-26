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

    public long population { get; private set; }
    public long Population => provinceEthnicPops.Sum(p => p.population);
    public Topography topo { get; }
    public Nation nation { get; set; } = null;
    public ProvinceMarket market { get; set; }

    [Obsolete]
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
        }
    }
}




