using System.Collections.Generic;
using UnityEngine;

public static class GlobalVariables
{
    public static readonly Dictionary<string, Nation> NATIONS = new Dictionary<string, Nation>()
    {
        {"First", new Nation(1, "First") },
        {"Second", new Nation(2, "Second") }
    };
    public static readonly Dictionary<string, List<string>> INITIAL_PROVINCES = new Dictionary<string, List<string>>()
    {
        {"First", new List<string>{"Red", "Green", "Grey" } },
        {"Second", new List<string>{"LightPink", "Pink", "Yellow"} }
    };
    public static readonly Dictionary<string, Province> PROVINCES = new Dictionary<string, Province>()
    {
        {"Red", new Province(1, "Red", 4555, Topography.Plane, new Color32(136, 0, 27, 255))},
        {"Green", new Province(2, "Green", 1550022, Topography.Plane, new Color32(14, 209, 69, 255)) },
        {"Grey", new Province(3, "Grey", 3123, Topography.Mountain, new Color32(88, 88, 88, 255)) },
        {"LightPink", new Province(4, "LightPink", 671243, Topography.Plane, new Color32(255, 174, 200, 255)) },
        {"Pink", new Province(5, "Pink", 3331, Topography.Plane, new Color32(184, 61, 186, 255)) },
        {"Yellow", new Province(6, "Yellow", 3331, Topography.Plane, new Color32(255, 242, 0, 255)) }
    };

    public static readonly Dictionary<string, List<Province>> ADJACENT_PROVINCES = new Dictionary<string, List<Province>>()
    {
        {"Red", new List<Province>{PROVINCES["Green"], PROVINCES["Yellow"], PROVINCES["Grey"] } },
        {"Green", new List<Province>{PROVINCES["Red"], PROVINCES["Grey"], PROVINCES["Pink"] } },
        {"Grey", new List<Province>{PROVINCES["Red"], PROVINCES["Green"], PROVINCES["LightPink"], PROVINCES["Pink"], PROVINCES["Yellow"] } },
        {"LightPink", new List<Province>{PROVINCES["Grey"], PROVINCES["Pink"], PROVINCES["Yellow"] }},
        {"Pink", new List<Province>{PROVINCES["Green"], PROVINCES["LightPink"], PROVINCES["Grey"]}},
        {"Yellow", new List<Province>{PROVINCES["Red"], PROVINCES["LightPink"], PROVINCES["Grey"]}}
    };

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
