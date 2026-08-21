using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 민족 집단을 정의하는 클래스
/// Nation 하나에 species, culture가 같은 EthincGroup은 단 하나 있어야 함
/// </summary>
public class EthnicGroup
{
    public SpeciesSpec species;
    public Culture culture;
    public List<ProvinceEthnicPop> provincePops = new List<ProvinceEthnicPop>();
    public long Population => provincePops.Sum(p => p.population);

    public EthnicGroup(SpeciesSpec species, Culture culture)
    {
        this.species = species;
        this.culture = culture;
    }


}



/// <summary>
/// 프로빈스에 속한 민족 집단의 인구를 구하는 클래스
/// Province 하나에 같은 EthnicGroup을 가진 ProvinceEthnicPop은 단 하나 있어야 함
/// </summary>
public enum AgeGroupType
{
    Childhood,
    YoungAdulthood,
    MiddleAge,
    OlderAdulthood
}

public class ProvinceEthnicPop : IBuildingInvestor
{
    public Province province;
    public EthnicGroup ethnicGroup;
    public long population;
    public long dividend; //배당수익
    public long property; // 재산
    public double livingStandard; // 생활 수준 (1.0 = 평균)
    public List<AgeGroup> ageGroups = new List<AgeGroup>();
    public long EmployablePopulation => ageGroups.Sum(ageGroup => ageGroup.EmployablePopulation);

    public ProvinceEthnicPop(Province province, EthnicGroup ethnicGroup, List<int> populationCount)
    {
        this.province = province;
        this.ethnicGroup = ethnicGroup;
        this.population = populationCount.Sum();
        this.property = 100000;
        this.livingStandard = 1.0; 
        // 기본 연령대 분포 설정
        ageGroups.Add(new AgeGroup(AgeGroupType.Childhood, populationCount[0])); //유년기
        ageGroups.Add(new AgeGroup(AgeGroupType.YoungAdulthood, populationCount[1])); //청년기
        ageGroups.Add(new AgeGroup(AgeGroupType.MiddleAge, populationCount[2])); //중년기
        ageGroups.Add(new AgeGroup(AgeGroupType.OlderAdulthood, populationCount[3])); //노년기

    }



    public class AgeGroup
    {
        public AgeGroupType type;
        public string name; // 연령대 이름 유년기 /청년기 /중년기 /노년기
        public long agepopulation; // 해당 연령대의 인구 
        public double LaborParticipationRate => type switch
        {
            AgeGroupType.Childhood => 0.25,
            AgeGroupType.YoungAdulthood => 1.0,
            AgeGroupType.MiddleAge => 0.85,
            AgeGroupType.OlderAdulthood => 0.0,
            _ => 0.0
        };
        public double AnnualTransitionRate => type switch
        {
            AgeGroupType.Childhood => 0.05,
            AgeGroupType.YoungAdulthood => 0.04,
            AgeGroupType.MiddleAge => 0.03,
            AgeGroupType.OlderAdulthood => 0.0,
            _ => 0.0
        };
        public long EmployablePopulation => (long)(agepopulation * LaborParticipationRate);

        public AgeGroup(AgeGroupType type, long agepopulation)
        {
            this.type = type;
            this.name = type switch
            {
                AgeGroupType.Childhood => "Childhood",
                AgeGroupType.YoungAdulthood => "Young Adulthood",
                AgeGroupType.MiddleAge => "Middle Age",
                AgeGroupType.OlderAdulthood => "Older Adulthood",
                _ => type.ToString()
            };
            this.agepopulation = agepopulation;
        }
    }

    /// <summary>
    /// 이동 전 인구를 기준으로 연령계층 인구를 다음 계층으로 이동시킨다.
    /// </summary>
    public void AdvanceAgeGroupsOneYear()
    {
        AgeGroup childhood = ageGroups.First(group => group.type == AgeGroupType.Childhood);
        AgeGroup youngAdulthood = ageGroups.First(group => group.type == AgeGroupType.YoungAdulthood);
        AgeGroup middleAge = ageGroups.First(group => group.type == AgeGroupType.MiddleAge);
        AgeGroup olderAdulthood = ageGroups.First(group => group.type == AgeGroupType.OlderAdulthood);

        long childhoodToYoung = (long)(childhood.agepopulation * childhood.AnnualTransitionRate);
        long youngToMiddle = (long)(youngAdulthood.agepopulation * youngAdulthood.AnnualTransitionRate);
        long middleToOlder = (long)(middleAge.agepopulation * middleAge.AnnualTransitionRate);

        childhood.agepopulation -= childhoodToYoung;
        youngAdulthood.agepopulation += childhoodToYoung - youngToMiddle;
        middleAge.agepopulation += youngToMiddle - middleToOlder;
        olderAdulthood.agepopulation += middleToOlder;
        population = ageGroups.Sum(group => group.agepopulation);
    }


    /// <summary>
    /// 인구수 증가 및 감소 처리
    /// </summary>
    /// <returns>증감 이후의 현재 인구</returns>
    public long PopulationGrowth()
    {
        // 단순한 인구 증가 -앞으로 문화에 따른 변화도 고려해야 함
        double growthRate = ethnicGroup.species.baseBirthRate; // 종족당 설정되어 있는 값을 사용
        //AS-IS: 지금 1초당 2배씩 늘어남!!!!
        foreach (AgeGroup ageGroup in ageGroups)
        {
            ageGroup.agepopulation = (long)(ageGroup.agepopulation * (1 + growthRate));
        }
        population = ageGroups.Sum(ageGroup => ageGroup.agepopulation);
        return population;
    }

    /// <summary>
    /// 필요한 음식 갯수를 구하는 함수
    /// </summary>
    /// <returns>필요한 음식 갯수</returns>
    public int GetNeededFood()
    {
        int basecoefficient = 1; // 기본 수요 계수
        if (basecoefficient > 0)
        {
            return (int)(basecoefficient * (population / 1000));
        }
        return 0;
    }

    /// <summary>
    /// 음식을 구매하는 함수
    /// </summary>
    /// <returns>property로 음식을 전부 살 수 있으면 true, 아니면 false</returns>
    public void BuyFood(int foodAmount)
    {
        // 음식을 샀다는 것만 확인하면 됨 
        // 생활수준이 10 이하라면 0.01 증가
        if (foodAmount >= GetNeededFood() && livingStandard < 10.0)
        {
            livingStandard += 0.01;
        }
        else if (foodAmount < GetNeededFood() && livingStandard > 5.0)
        {
            livingStandard -= 0.01;
        }
        else if (foodAmount == 0 && livingStandard > 1.0)
        {
            livingStandard -= 0.05;
        }
    }

    /// <summary>
    /// 필요한 사치품 갯수를 구하는 함수
    /// </summary>
    /// <returns>사치품 갯수</returns>
    public int GetNeededLuxury()
    {
        int basecoefficient = 1; // 기본 수요 계수
        if (basecoefficient > 0)
        {
            return (int)(basecoefficient * (population / 1000) * livingStandard);
        }
        return 0;
    }

}

/// <summary>
/// 종족을 정의하는 클래스
/// </summary>
public class SpeciesSpec
{
    public string name; // 종족 이름 정의 
    public double baseBirthRate = 1.0;  //출생률
}


