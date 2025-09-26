using UnityEngine;
using System;
using System.Collections.Generic;



// 문화(문명)을 정의하는 클래스
// 각 문화는 여러 특성을 가질수 있음

public class Culture
{
    public string name;
    public List<CultureTrait> Traits = new List<CultureTrait>();

    public Culture(string name)
    {
        this.name = name;
    }

    public void AddTrait(CultureTrait trait)
    {
        Traits.Add(trait);
    }
}






public class CultureTrait
{
    public String Name;


}