using System.Collections.Generic;
using System;

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
        if (attackRegiments.Contains(regiment) || defenseRegiments.Contains(regiment))
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

    public int GetAttackNumber()
    {
        int unitCount = 0;
        foreach (Regiment regiment in attackRegiments)
        {
            if (regiment != null)
            {
                unitCount += regiment.GetUnitCount();
            }
        }
        return unitCount;
    }

    public int GetDefenseNumber()
    {
        int unitCount = 0;
        foreach (Regiment regiment in defenseRegiments)
        {
            if (regiment != null)
            {
                unitCount += regiment.GetUnitCount();
            }
        }
        return unitCount;
    }

    public int GetBattleAreaDesolation()
    {
        return battleArea == null ? 0 : battleArea.desolation;
    }

    public void SetBattleAreaDesolation(int percent)
    {
        if (battleArea == null)
        {
            return;
        }

        battleArea.SetDesolation(percent);
    }

    public double ChangeWinProbability(double add)
    {
        winProbability = Math.Clamp(winProbability + add, 0.0, 100.0);
        return winProbability;
    }

    public void Attack()
    {

    }
}
