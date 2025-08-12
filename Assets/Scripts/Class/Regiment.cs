using System;
using System.Collections.Generic;

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
        foreach (UnitType type in units.Keys)
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
        if (units.Count == 0)
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