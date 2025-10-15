using System.Collections.Generic;
using System.Linq;

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
public class ProvinceEthnicPop
{
    public Province province;
    public EthnicGroup ethnicGroup;
    public long population;
    public long property; // 재산
    public double livingStandard; // 생활 수준 (1.0 = 평균)
    public ProvinceEthnicPop(Province province, EthnicGroup ethnicGroup, long populationCount)
    {
        this.province = province;
        this.ethnicGroup = ethnicGroup;
        this.population = populationCount;
        this.property = 0;
        this.livingStandard = 1.0;
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
        population = (long)(population * (1 + growthRate));
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
    public bool BuyFood(int remainingFood)
    {


        
        return false;
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


