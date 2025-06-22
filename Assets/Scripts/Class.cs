using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


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
    public string name { get;}
    public long population { get; set; }
    public Topography topo { get; }
    public Nation nation { get; set; } = null;
    public Market market { get; } 
    public Species species { get; set; } = null;

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
        this.market = new Market(this);
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
        if (species == null) return;

        species.Consume(market);
        market.ProduceCrops();
        species.UpdateGrowth();

        population = species.population; // 이 줄로 동기화도 함께
    }


}

/// <summary>
/// 국가 클래스
/// ID, 이름, 소유 프로빈스, 재산 정의
/// </summary>
public class Nation
{
    public int id;
    public string name { get; }
    public List<Province> provinces { get; set; }
    public long balance { get; set; }

    public List<ResearchNode> doneResearches;
    public Dictionary<BuffKind, double> buffs;

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
        doneResearches = researches;
        buffs = new Dictionary<BuffKind, double>();
        foreach(ResearchNode research in doneResearches)
        {
            foreach(Buff buff in research.buffs)
            {
                double prevValue = 0;
                buffs.TryGetValue(buff.baseBuff, out prevValue);
                buffs.Add(buff.baseBuff, prevValue + buff.power);
            } 
        }
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
    /// 국가의 모든 프로빈스의 인구의 합을 반환
    /// </summary>
    /// <returns>국가의 모든 프로빈스의 인구 합</returns>
    public long GetPopulation()
    {
        long sum = 0;
        foreach(Province province in provinces){
            sum += province.population;
        }
        return sum;
    }
}

/// <summary>
/// 유저 클래스
/// 유저 ID, 국가를 정의
/// </summary>
public class User
{
    public int id { get; }
    public Nation nation { get; set; }

    /// <summary>
    /// 유저 생성자
    /// </summary>
    /// <param name="id">유저의 ID</param>
    /// <param name="nation">유저가 속한 국가</param>
    public User(int id, Nation nation)
    {
        this.id = id;
        this.nation = nation;
    }

    /// <summary>
    /// 유저가 속한 국가의 재산 반환
    /// </summary>
    /// <returns>유저가 속한 국가의 재산</returns>
    public long GetCurrentBalance()
    {
        return this.nation.balance;
    }
}


public class Crop
{
    public string name { get; }
    public int amount { get; set; }

    public Crop(string name, int initialAmount)
    {
        this.name = name;
        this.amount = initialAmount;
    }

    public void Produce(int quantity)
    {
        this.amount += quantity;
    }


}


public class Market
{
    public Province province { get; }
    public List<Crop> crops { get; }

    public Market(Province province)
    {
        this.province = province;
        this.crops = new List<Crop>
        {
            new Crop("Wheat", 0),
            new Crop("Rice", 0),
            new Crop("Corn", 0)
        };
    }

    /// <summary>
    /// 인구 수에 비례하여 작물을 생산합니다.
    /// </summary>
    public void ProduceCrops()
    {
        Nation curNation = this.province.nation;
        double modifier = 1;
        if(curNation != null)
        {
            double addMod = curNation.buffs.GetValueOrDefault(BuffKind.FOOD_PRODUCE_PER_UP, 0.0);
            modifier += addMod;
        }
        int scale = (int)(modifier * province.population / 1); // 인구 1명당 생산
        foreach (var crop in crops)
        {
            crop.Produce(scale); 
        }
    }

    public Crop GetCrop(string cropName)
    {
        return crops.Find(c => c.name == cropName);
    }
}

