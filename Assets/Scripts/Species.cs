using UnityEngine;
using System;


// 종족을 정의하는 추상 클래스
public abstract class Species
{
    public abstract string Name { get; }
    public abstract void Consume(Market market);  // 상품소비 동작 정의
    public abstract void UpdateGrowth();          // 인구성장률 갱신
    public int population { get; set; } = 0;
}




// 인간 종족을 정의하는 클래스 
public class Human : Species
{
    public override string Name => "Human"; // 종족 이름 정의 
    public int happiness { get; set; } = 100;　// 행복도
    public int literacy { get; set; } = 0; // 문해율
    public int culture { get; set; } = 0; // 문화(앞으로 여러 문화를 추가할것 )

    private int foodConsumed = 0;
    private int foodNeeded = 2; // 예: 1명당 2 유닛 필요

    public override void Consume(Market market)
    {
        int requiredFood = population * foodNeeded; // 전체 인구가 필요한 음식의 양 계산

        int totalAvailable = 0; // 실제로 소비한 음식량
        foreach (var crop in market.crops) // market.crops에 있는 각 crop(작물)에 대해 반복
        {
            // eatAmount는 "요구하는 음식량(requiredFood)"과 "작물에서 얻을 수 있는 양(crop.amount)" 중 더 작은 값
            int eatAmount = Math.Min(requiredFood, crop.amount);
            
            crop.amount -= eatAmount;    // 실제 작물에서 소비한 음식만큼 제거
            requiredFood -= eatAmount;   // 필요했던 음식량에서 먹은 양만큼 차감
            totalAvailable += eatAmount; // 총 소비량에 더함

            if (requiredFood <= 0)       // 요구량을 모두 충족했으면 종료
                break;
        }

        foodConsumed = totalAvailable; // 실제로 소비한 총 음식량 저장
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



