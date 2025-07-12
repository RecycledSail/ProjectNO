using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
    public List<Species> pops { get; set; } = null;
    public List<Building> buildings { get; set; } = null;

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
        foreach(Species species in pops) {
            species.Consume(market);
            market.ProduceCrops();
            species.UpdateGrowth();

            population += species.population; // 이 줄로 동기화도 함께
        }
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

/// <summary>
/// 프로빈스 내 작물의 갯수 클래스
/// 작물 ID, 작물 이름을 정의
/// </summary>
public class Crop
{
    public string name { get; }
    public int amount { get; set; }

    public Crop(string name, int initialAmount)
    {
        this.name = name;
        this.amount = initialAmount;
    }

    /// <summary>
    /// 작물 생산 메서드
    /// </summary>
    /// <param name="quantity"></param>
    public void Produce(int quantity)
    {
        this.amount += quantity;
    }
}

/// <summary>
/// 프로빈스 내 시장 클래스
/// 프로빈스와 프로빈스가 보유한 작물들을 정의
/// </summary>
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

/// <summary>
/// 유닛 타입 클래스
/// </summary>
public class UnitType
{
    public int id { get; }
    public string name { get;}
    public int attackPerUnit { get; }
    public int defensePerUnit { get; }
    public int moveSpeedPerUnit { get; }

    public UnitType(int id, string name, int attackPerUnit, int defensePerUnit, int moveSpeedPerUnit)
    {
        this.id = id;
        this.name = name;
        this.attackPerUnit = attackPerUnit;
        this.defensePerUnit = defensePerUnit;
        this.moveSpeedPerUnit = moveSpeedPerUnit;
    }
}

public enum RegimentState
{
    IDLE,
    MOVE,
    BATTLE
}
public class Regiment
{
    public Nation nation { get; }
    public int id { get; }
    public string name { get; set; }
    public Dictionary<UnitType, int> units { get; set; }
    public Province location;
    public RegimentState state;

    public Regiment(Nation nation, int id, string name, Province location)
    {
        this.nation = nation;
        this.id = id;
        this.name = name;
        this.location = location;
        this.units = new();
        this.state = RegimentState.IDLE;
    }

    public int GetUnitCount()
    {
        int unitCount = 0;
        foreach (UnitType type in units.Keys)
        {
            int curCount = units[type];
            unitCount += curCount;
        }
        return unitCount;
    }
    public double GetAttackPower()
    {
        double totalAttack = 0, unitCount = 0;
        foreach(UnitType type in units.Keys)
        {
            double curCount = units[type];
            totalAttack += curCount * type.attackPerUnit;
            unitCount += curCount;
        }
        double result = totalAttack / unitCount;
        return result < 0.1 ? 0.1 : result;
    }

    public double GetDefensePower()
    {
        double totalDefense = 0, unitCount = 0;
        foreach (UnitType type in units.Keys)
        {
            double curCount = units[type];
            totalDefense += curCount * type.defensePerUnit;
            unitCount += curCount;
        }
        double result = totalDefense / unitCount;
        return result < 0.1 ? 0.1 : result;
    }

    public double GetMoveSpeed()
    {
        if(units.Count == 0)
        {
            return 0;
        }
        else
        {
            double minMoveSpeed = -1;
            foreach (UnitType type in units.Keys)
            {
                if (minMoveSpeed == -1)
                    minMoveSpeed = type.moveSpeedPerUnit;
                else
                    minMoveSpeed = Math.Min(minMoveSpeed, type.moveSpeedPerUnit);
            }
            return minMoveSpeed < 0.1 ? 0.1 : minMoveSpeed;
        }
    }
}

public class Trait
{
    public int id { get; }
    public string name { get; }
    public Buff buff { get; }

    public Trait(int id, string name, Buff buff)
    {
        this.id = id;
        this.name = name;
        this.buff = buff;
    }
}

public class NamedPerson
{
    public int id { get; }
    public string firstName { get; }
    public string lastName { get; }

    public Species species { get; }
    public List<Trait> traits { get; set; }

    public NamedPerson(int id, string firstName, string lastName, Species species)
    {
        this.id = id;
        this.firstName = firstName;
        this.lastName = lastName;
        this.species = species;
        this.traits = new();
    }

    public bool AddTrait(Trait trait)
    {
        if (traits.Contains(trait))
        {
            return false;
        }
        else
        {
            traits.Add(trait);
            return true;
        }
    }

    public bool RemoveTrait(Trait trait)
    {
        return traits.Remove(trait);
    }

    public string FullName()
    {
        return firstName + " " + lastName;
    }
}

public class Battle
{
    public List<Regiment> attackRegiments { get; set; }
    public List<Regiment> defenseRegiments { get; set; }
    public Province battleArea { get; }


    public double winProbability { get; set; }

    public Battle(List<Regiment> attackRegiments, List<Regiment> defenseRegiments, Province battleArea)
    {
        this.attackRegiments = attackRegiments;
        this.defenseRegiments = defenseRegiments;
        this.battleArea = battleArea;
        this.winProbability = 50.0;
    }

    public bool AddAttackRegiment(Regiment regiment)
    {
        if(attackRegiments.Contains(regiment) || defenseRegiments.Contains(regiment))
        {
            return false;
        }
        else
        {
            attackRegiments.Add(regiment);
            return true;
        }
    }

    public bool AddDefenseRegiment(Regiment regiment)
    {
        if (attackRegiments.Contains(regiment) || defenseRegiments.Contains(regiment))
        {
            return false;
        }
        else
        {
            defenseRegiments.Add(regiment);
            return true;
        }
    }

    public bool RemoveAttackRegiment(Regiment regiment)
    {
        return attackRegiments.Remove(regiment);
    }

    public bool RemoveDefenseRegiment(Regiment regiment)
    {
        return defenseRegiments.Remove(regiment);
    }

    public void Attack()
    {

    }
}