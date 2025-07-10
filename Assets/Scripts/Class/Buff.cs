using System;
using UnityEngine;


//버프 종류 enum
public enum BuffKind
{
    FOOD_PRODUCE_PER_UP,
};

/// <summary>
/// Buff 클래스
/// 버프 ID, 이름, 적용되는 버프, 버프의 강도를 정의 
/// 버프의 강도는 BuffKind마다 정의가 다름
/// </summary>
public class Buff
{
    public int id { get; }
    public string name { get; }
    public BuffKind baseBuff { get; }
    public double power { get; }

    public Buff(int id, string name, BuffKind baseBuff, double power)
    {
        this.id = id;
        this.name = name;
        this.baseBuff = baseBuff;
        this.power = power;
    }

}
