using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GlobalVariables Ŭ������ ���� �� ���������� ���Ǵ� �����͸� �����ϴ� ���� Ŭ�����Դϴ�.
/// ����, ��(Province), �׸��� ������ �� ������ �����ϰ� �ֽ��ϴ�.
/// </summary>
public static class GlobalVariables
{

    /// <summary>
    /// ������ �����ϴ� Dictionary
    /// Key: ���� �̸�, Value: Buff ��ü
    /// </summary>
    public static readonly Dictionary<string, Buff> BUFF = new Dictionary<string, Buff>()
    {
        {"FOOD_UP_PER_20",  new Buff(1, "FOOD_UP_PER_20", BuffKind.FOOD_PRODUCE_PER_UP, 0.2)},
    };

    /// <summary>
    /// ���� ��带 �����ϴ� Dictionary
    /// Key: ���� ��� �̸�, Value: ResearchNode ��ü
    /// </summary>
    public static readonly Dictionary<string, ResearchNode> RESEARCH_NODE = new Dictionary<string, ResearchNode>()
    {
        {"FarmTools", new ResearchNode(1, "FarmTools", 350.0, new List<Buff>(){BUFF["FOOD_UP_PER_20"] }) },
    };

    /// <summary>
    /// ���� ������ �����ϴ� Dictionary
    /// Key: ���� �̸�, Value: Nation ��ü
    /// </summary>
    public static readonly Dictionary<string, Nation> NATIONS = new Dictionary<string, Nation>()
    {
        {"Nation1", new Nation(1, "Nation1", new List<ResearchNode>(){
            RESEARCH_NODE["FarmTools"] })
        },
        {"Nation2", new Nation(2, "Nation2", new List<ResearchNode>())  }
    };

    /// <summary>
    /// �� ������ ���� �� �����ϴ� �� ���
    /// Key: ���� �̸�, Value: �� �̸� ����Ʈ
    /// </summary>
    public static readonly Dictionary<string, List<string>> INITIAL_PROVINCES = new Dictionary<string, List<string>>()
    {
        {"Nation1", new List<string>{"Province1", "Province2"} },
        {"Nation2", new List<string>{"Province3"} }
    };

    /// <summary>
    /// ���� �� ��� �� ������ �����ϴ� Dictionary
    /// Key: �� �̸�, Value: Province ��ü
    /// </summary>
    public static readonly Dictionary<string, Province> PROVINCES = new Dictionary<string, Province>()
    {
        {"Province1", new Province(1, "Province1", 4555, Topography.Plane, new Color32(114, 185, 90, 255))},
        {"Province2", new Province(2, "Province2", 5000, Topography.Plane, new Color32(66, 76, 163, 255))},
        {"Province3", new Province(3, "Province3", 6500, Topography.Plane, new Color32(175, 182, 188, 255))},
        {"Province4", new Province(4, "Province4", 6500, Topography.Plane, new Color32(57, 238, 124, 255))},
        {"Province5", new Province(5, "Province5", 6500, Topography.Plane, new Color32(85, 183, 83, 255))},
        {"Province6", new Province(6, "Province6", 6500, Topography.Plane, new Color32(145, 215, 209, 255))},
        {"Province7", new Province(7, "Province7", 6500, Topography.Plane, new Color32(184, 221, 56, 255))},
    };

    /// <summary>
    /// �� �ֿ� ������ �ֵ��� ������ �����ϴ� Dictionary
    /// Key: �� �̸�, Value: ������ ���� ����Ʈ
    /// </summary>
    public static readonly Dictionary<string, List<Province>> ADJACENT_PROVINCES = new Dictionary<string, List<Province>>()
    {
        {"Province1", new List<Province>{PROVINCES["Province2"]} },
        {"Province2", new List<Province>{PROVINCES["Province1"], PROVINCES["Province3"] } },
        {"Province3", new List<Province>{PROVINCES["Province2"], PROVINCES["Province4"], PROVINCES["Province5"] } },
        {"Province4", new List<Province>{PROVINCES["Province3"], PROVINCES["Province5"], PROVINCES["Province6"] } },
        {"Province5", new List<Province>{PROVINCES["Province2"], PROVINCES["Province3"], PROVINCES["Province4"], PROVINCES["Province6"], PROVINCES["Province7"] } },
        {"Province6", new List<Province>{PROVINCES["Province4"], PROVINCES["Province5"], PROVINCES["Province7"]} },
        {"Province7", new List<Province>{PROVINCES["Province5"], PROVINCES["Province6"]} },
    };

    /// <summary>
    /// �� ���� �����ϴ� �� ������ �����ϴ� Dictionary
    /// Key: Color32 (�� ���� ����), Value: �ش��ϴ� Province ��ü
    /// </summary>
    public static Dictionary<Color32, Province> COLORTOPROVINCE = new Dictionary<Color32, Province>();
}
