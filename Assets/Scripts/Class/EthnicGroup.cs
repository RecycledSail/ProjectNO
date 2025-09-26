using System.Collections.Generic;

// 민족집단
public class EthnicGroup
{
    public SpeciesSpec species;
    public Culture culture;
    public List<ProvinceEthnicPop> provincePops = new List<ProvinceEthnicPop>();

    public EthnicGroup(SpeciesSpec species, Culture culture)
    {
        this.species = species;
        this.culture = culture;
    }
}



public class ProvinceEthnicPop
{
    public Province province;
    public EthnicGroup ethnicGroup;
    public long population;

    public ProvinceEthnicPop(Province province, EthnicGroup ethnicGroup, long populationCount)
    {
        this.province = province;
        this.ethnicGroup = ethnicGroup;
        this.population = populationCount;
    }

    public void PopulationGrowth()
    {
        // 단순한 인구 증가 -앞으로 문화에 따른 변화도 고려해야 함
        double growthRate = ethnicGroup.species.baseBirthRate; // 종족당 설정되어 있는 값을 사용
        population = (long)(population * (1 + growthRate));
    }


}


public class SpeciesSpec
{
    public string name; // 종족 이름 정의 
    public double baseBirthRate = 1.0;  //출생률
}


