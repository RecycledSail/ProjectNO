using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GlobalVariables 클래스는 게임 내 전역적으로 사용되는 데이터를 저장하는 정적 클래스입니다.
/// 국가, 주(Province), 그리고 인접한 주 정보를 포함하고 있습니다.
/// </summary>
public static class GlobalVariables
{

    /// <summary>
    /// 버프를 저장하는 Dictionary
    /// Key: 버프 이름, Value: Buff 객체
    /// </summary>
    public static readonly Dictionary<string, Buff> BUFF = new Dictionary<string, Buff>()
    {
        {"FOOD_UP_PER_20",  new Buff(1, "FOOD_UP_PER_20", BuffKind.FOOD_PRODUCE_PER_UP, 0.2)},
    };

    /// <summary>
    /// 연구 노드를 저장하는 Dictionary
    /// Key: 연구 노드 이름, Value: ResearchNode 객체
    /// </summary>
    public static readonly Dictionary<string, ResearchNode> RESEARCH_NODE = new Dictionary<string, ResearchNode>()
    {
        {"FarmTools", new ResearchNode(1, "FarmTools", 350.0, new List<Buff>(){BUFF["FOOD_UP_PER_20"] }) },
    };

    /// <summary>
    /// 국가 정보를 저장하는 Dictionary
    /// Key: 국가 이름, Value: Nation 객체
    /// </summary>
    public static readonly Dictionary<string, Nation> NATIONS = new Dictionary<string, Nation>()
    {
        {"First", new Nation(1, "First", new List<ResearchNode>(){
            RESEARCH_NODE["FarmTools"] })
        },
        {"Second", new Nation(2, "Second", new List<ResearchNode>())  }
    };

    /// <summary>
    /// 각 국가가 시작 시 소유하는 주 목록
    /// Key: 국가 이름, Value: 주 이름 리스트
    /// </summary>
    public static readonly Dictionary<string, List<string>> INITIAL_PROVINCES = new Dictionary<string, List<string>>()
    {
        {"First", new List<string>{"Red", "Green", "Grey" } },
        {"Second", new List<string>{"LightPink", "Pink", "Yellow"} }
    };

    /// <summary>
    /// 게임 내 모든 주 정보를 저장하는 Dictionary
    /// Key: 주 이름, Value: Province 객체
    /// </summary>
    public static readonly Dictionary<string, Province> PROVINCES = new Dictionary<string, Province>()
    {
        {"Red", new Province(1, "Red", 4555, Topography.Plane, new Color32(136, 0, 27, 255))},
        {"Green", new Province(2, "Green", 1550022, Topography.Plane, new Color32(14, 209, 69, 255)) },
        {"Grey", new Province(3, "Grey", 3123, Topography.Mountain, new Color32(88, 88, 88, 255)) },
        {"LightPink", new Province(4, "LightPink", 671243, Topography.Plane, new Color32(255, 174, 200, 255)) },
        {"Pink", new Province(5, "Pink", 3331, Topography.Plane, new Color32(184, 61, 186, 255)) },
        {"Yellow", new Province(6, "Yellow", 3331, Topography.Plane, new Color32(255, 242, 0, 255)) }
    };

    /// <summary>
    /// 각 주와 인접한 주들의 정보를 저장하는 Dictionary
    /// Key: 주 이름, Value: 인접한 주의 리스트
    /// </summary>
    public static readonly Dictionary<string, List<Province>> ADJACENT_PROVINCES = new Dictionary<string, List<Province>>()
    {
        {"Red", new List<Province>{PROVINCES["Green"], PROVINCES["Yellow"], PROVINCES["Grey"] } },
        {"Green", new List<Province>{PROVINCES["Red"], PROVINCES["Grey"], PROVINCES["Pink"] } },
        {"Grey", new List<Province>{PROVINCES["Red"], PROVINCES["Green"], PROVINCES["LightPink"], PROVINCES["Pink"], PROVINCES["Yellow"] } },
        {"LightPink", new List<Province>{PROVINCES["Grey"], PROVINCES["Pink"], PROVINCES["Yellow"] }},
        {"Pink", new List<Province>{PROVINCES["Green"], PROVINCES["LightPink"], PROVINCES["Grey"]}},
        {"Yellow", new List<Province>{PROVINCES["Red"], PROVINCES["LightPink"], PROVINCES["Grey"]}}
    };

    /// <summary>
    /// 각 색상에 대응하는 주 정보를 저장하는 Dictionary
    /// Key: Color32 (각 주의 색상), Value: 해당하는 Province 객체
    /// </summary>
    public static readonly Dictionary<Color32, Province> COLORTOPROVINCE = new Dictionary<Color32, Province>()
    {
        {new Color32(136, 0, 27, 255), PROVINCES["Red"]},
        {new Color32(14, 209, 69, 255), PROVINCES["Green"] },
        {new Color32(88, 88, 88, 255), PROVINCES["Grey"] },
        {new Color32(255, 174, 200, 255), PROVINCES["LightPink"] },
        {new Color32(184, 61, 186, 255), PROVINCES["Pink"] },
        {new Color32(255, 242, 0, 255), PROVINCES["Yellow"] }
    };


}
