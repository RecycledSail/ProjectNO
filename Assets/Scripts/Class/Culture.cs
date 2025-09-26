using UnityEngine;
using System;



// 문화(문명)을 정의하는 클래스
// 각 문화는 여러 특성을 가질수 있음

public class Culture
{
    public string Name;
    public List<Culturetrait> Traits = new List<Culturetrait>();

    public Culture(string name)
    {
        Name = name;
    }

    public void AddTrait(Culturetrait trait)
    {
        Traits.Add(trait);
    }
}






public class Culturetrait
{
    public String Name;


}