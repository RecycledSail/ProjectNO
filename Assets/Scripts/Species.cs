using UnityEngine;
using System;


public abstract class Species
{
    public abstract string Name { get; }
    public abstract void Consume(Market market);  // 소비 동작 정의
    public abstract void UpdateGrowth();          // 성장률 갱신
    public int population { get; set; } = 0;
}





public class Human : Species
{
    public override string Name => "Human";
    public int happiness { get; set; } = 100;
    public int literacy { get; set; } = 0;
    public int culture { get; set; } = 0;

    private int foodConsumed = 0;
    private int foodNeeded = 2; // 예: 1명당 2 유닛 필요

    public override void Consume(Market market)
    {
        int requiredFood = population * foodNeeded;

        int totalAvailable = 0;
        foreach (var crop in market.crops)
        {
            int eatAmount = Math.Min(requiredFood, crop.amount);
            crop.amount -= eatAmount;
            requiredFood -= eatAmount;
            totalAvailable += eatAmount;

            if (requiredFood <= 0)
                break;
        }

        foodConsumed = totalAvailable;
    }

    public override void UpdateGrowth()
    {
        float foodRatio = (float)foodConsumed / (population * foodNeeded);
        int growthBase = (int)(foodRatio * 5);  // 0 ~ 5%
        population += (int)(population * (growthBase / 100f));

        if (foodRatio < 0.5f)
        {
            // 영양 부족 시 인구 감소
            int death = (int)(population * (0.02f * (1 - foodRatio)));  // 최대 2% 감소
            population -= death;
        }

        foodConsumed = 0; // 초기화
    }
}


// public class Elf
// {
//     public string name { get; set; } = "Elf";
//     public int population { get; set; } = 0;
//     public int maxPopulation { get; set; } = 1000000;
//     public int growthRate { get; set; } = 1;
//     public int happiness { get; set; } = 100;
//     public int literacy { get; set; } = 0;
//     public int culture { get; set; } = 0;
//     public int religion { get; set; } = 0;
//     public int technology { get; set; } = 0;
// }


// public class Dwarf
// {
//     public string name { get; set; } = "Dwarf";
//     public int population { get; set; } = 0;
//     public int maxPopulation { get; set; } = 1000000;
//     public int growthRate { get; set; } = 1;
//     public int happiness { get; set; } = 100;
//     public int literacy { get; set; } = 0;
//     public int culture { get; set; } = 0;
//     public int religion { get; set; } = 0;
//     public int technology { get; set; } = 0;
// }



