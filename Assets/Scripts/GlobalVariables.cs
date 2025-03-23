using System.Collections.Generic;
using UnityEngine;

public static class GlobalVariables
{
    public static readonly Dictionary<string, Nation> NATIONS = new Dictionary<string, Nation>()
    {
        {"First", new Nation(1, "First") },
        {"Second", new Nation(2, "Second") }
    };
    public static readonly Dictionary<string, Province> PROVINCES = new Dictionary<string, Province>()
    {
        {"Sea", new Province(1, "Sea", 0, Topography.Sea) },
        {"Land", new Province(2, "Land", 1550022, Topography.Plane) }
    };

    public static readonly Dictionary<Color32, Province> COLORTOPROVINCE = new Dictionary<Color32, Province>()
    {
        {new Color32(56, 250, 250, 255), PROVINCES["Sea"]},
        {new Color32(202, 46, 173, 255), PROVINCES["Land"] }
    };
}
