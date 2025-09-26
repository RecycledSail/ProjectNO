using UnityEngine;
using System;
using System.Globalization;

// 

using System.Collections.Generic;

// 민족집단
public class EthnicGroup
{

    public Nation Nation { get; private set; } = null;

    public Species MainSpecies;
    public Culture NationCulture;
    public List<ProvinceEthnicPop> ProvincePops = new List<ProvinceEthnicPop>();

    public EthnicGroup( Species mainSpecies, Culture culture)
    {
        
        MainSpecies = mainSpecies;
        NationCulture = culture;
    }
    



}



public class ProvinceEthnicPop
{
    public Province province;
    public EthnicGroup ethnicGroup;
    public long populationCount;

    public ProvinceEthnicPop(Province province, EthnicGroup ethnicGroup, long populationCount)
    {
        this.province = province;
        this.ethnicGroup = ethnicGroup;
        this.populationCount = populationCount;
    }

    public void PopulationGrowth()
    {
        // 단순한 인구 증가 -앞으로 문화에 따른 변화도 고려해야 함
        double growthRate = ethnicGroup.MainSpecies.baseBirthRate; // 종족당 설정되어 있는 값을 사용
        populationCount = (long)(populationCount * (1 + growthRate));
    }


}


public class SpeciesSpec
{
    public string name; // 종족 이름 정의 
    public double baseBirthRate = 1.0;  //출생률
}


