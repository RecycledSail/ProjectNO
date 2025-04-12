using System;
using UnityEngine;


//���� ���� enum
public enum BuffKind
{
    FOOD_PRODUCE_PER_UP,
};

/// <summary>
/// Buff Ŭ����
/// ���� ID, �̸�, ����Ǵ� ����, ������ ������ ���� 
/// ������ ������ BuffKind���� ���ǰ� �ٸ�
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
