using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static GlobalVariables;

public static class SaveManager
{
    public static void OnLoad()
    {
        
        if (saveFileName == null)
        {
            Debug.LogError("saveFileName is null.");
            return;
        }
        string jsonPath = Path.Combine(Application.persistentDataPath, GlobalVariables.saveFileName + ".json");
        if (!File.Exists(jsonPath))
        {
            Debug.LogError(saveFileName + ".json not found.");
        }

        string json = File.ReadAllText(jsonPath);
        var gameData = JsonUtility.FromJson<GameDataFormat>(json);

        // Load Buffs
        foreach (var b in gameData.buffs)
        {
            var buff = new Buff(b.id, b.name, (BuffKind)System.Enum.Parse(typeof(BuffKind), b.kind), b.value);
            BUFF[b.name] = buff;
        }

        // Load Research Nodes
        foreach (var r in gameData.researchNodes)
        {
            var buffs = new List<Buff>();
            foreach (var bname in r.buffNames)
            {
                if (BUFF.TryGetValue(bname, out var buff))
                    buffs.Add(buff);
            }
            var node = new ResearchNode(r.id, r.name, r.cost, buffs);
            RESEARCH_NODE[r.name] = node;
        }

        // Load Provinces & Color-to-province
        foreach (var p in gameData.provinces)
        {
            var color = new Color32(p.color[0], p.color[1], p.color[2], p.color[3]);
            var province = new Province(p.id, p.name, p.population, (Topography)System.Enum.Parse(typeof(Topography), p.topography), color);
            PROVINCES[p.name] = province;
            COLORTOPROVINCE[color] = province;
        }

        // Load Adjacent Provinces
        foreach (var pair in gameData.adjacentProvinces)
        {
            var list = new List<Province>();
            foreach (var pname in pair.adjacents)
                list.Add(PROVINCES[pname]);
            ADJACENT_PROVINCES[pair.province] = list;
        }

        // Load Nations
        foreach (var n in gameData.nations)
        {
            var rnodes = new List<ResearchNode>();
            foreach (var rname in n.researchNodeNames)
            {
                if (RESEARCH_NODE.TryGetValue(rname, out var rnode))
                    rnodes.Add(rnode);
            }
            var nation = new Nation(n.id, n.name, rnodes);
            NATIONS[n.name] = nation;
        }

        // Initial Provinces
        foreach (var data in gameData.initialProvinces)
        {
            var rnodes = new List<string>();
            foreach (var provinceStr in data.provinces)
            {
                rnodes.Add(provinceStr);
            }
            INITIAL_PROVINCES[data.nation] = rnodes;
        }
        Debug.Log("Load Done!");
    }
    public static void OnSave()
    {
        if (saveFileName == null)
        {
            Debug.LogError("saveFileName is null.");
            return;
        }
        string jsonPath = Path.Combine(Application.persistentDataPath, GlobalVariables.saveFileName + ".json");


        GameDataFormat saveData = new GameDataFormat();

        // Save Buffs
        saveData.buffs = new List<GameDataFormat.BuffData>();
        foreach (var b in BUFF.Values)
        {
            GameDataFormat.BuffData buff = new GameDataFormat.BuffData();
            buff.id = b.id;
            buff.name = b.name;
            buff.kind = b.baseBuff.ToString();
            buff.value = b.power;
            saveData.buffs.Add(buff);
        }

        // Save Research Nodes
        saveData.researchNodes = new List<GameDataFormat.ResearchNodeData>();
        foreach (var r in RESEARCH_NODE.Values)
        {
            var node = new GameDataFormat.ResearchNodeData();
            List<string> buffNames = new List<string>();
            foreach (Buff b in r.buffs)
            {
                buffNames.Add(b.name);
            }
            node.id = r.id;
            node.name = r.name;
            node.cost = r.requiredPoint;
            node.buffNames = buffNames;
            saveData.researchNodes.Add(node);
        }

        // Save Provinces & Color-to-province
        saveData.provinces = new List<GameDataFormat.ProvinceData>();
        foreach (var p in PROVINCES.Values)
        {
            //var color = new Color32(p.color[0], p.color[1], p.color[2], p.color[3]);
            //var province = new Province(p.id, p.name, p.population, (Topography)System.Enum.Parse(typeof(Topography), p.topography), color);
            //PROVINCES[p.name] = province;
            //COLORTOPROVINCE[color] = province;
            GameDataFormat.ProvinceData provinceData = new GameDataFormat.ProvinceData();
            provinceData.id = p.id;
            provinceData.name = p.name;
            provinceData.population = (int)p.population;
            provinceData.topography = p.topo.ToString();
            List<byte> color = new()
            {
                p.color.r,
                p.color.g,
                p.color.b,
                p.color.a
            };
            provinceData.color = color;
            saveData.provinces.Add(provinceData);
        }

        // Save Adjacent Provinces
        saveData.adjacentProvinces = new();
        foreach (var (province, adjacents) in ADJACENT_PROVINCES)
        {
            //var list = new List<Province>();
            //foreach (var pname in pair.adjacents)
            //    list.Add(PROVINCES[pname]);
            //ADJACENT_PROVINCES[pair.province] = list;
            GameDataFormat.AdjacentProvinceWrapper adjacentProvinceWrapper = new();
            adjacentProvinceWrapper.province = province;
            List<string> adjacentList = new();
            foreach (var adjacent in adjacents)
            {
                adjacentList.Add(adjacent.name);
            }
            adjacentProvinceWrapper.adjacents = adjacentList;
            saveData.adjacentProvinces.Add(adjacentProvinceWrapper);
        }

        // Save Nations
        saveData.nations = new();
        foreach (var n in NATIONS.Values)
        {
            //var rnodes = new List<ResearchNode>();
            //foreach (var rname in n.researchNodeNames)
            //{
            //    if (RESEARCH_NODE.TryGetValue(rname, out var rnode))
            //        rnodes.Add(rnode);
            //}
            //var nation = new Nation(n.id, n.name, rnodes);
            //NATIONS[n.name] = nation;
            GameDataFormat.NationData nationData = new();
            nationData.id = n.id;
            nationData.name = n.name;
            List<string> researchNodeNames = new();
            foreach(ResearchNode node in n.doneResearches)
            {
                researchNodeNames.Add(node.name);
            }
            nationData.researchNodeNames = researchNodeNames;
            saveData.nations.Add(nationData);
        }

        string json = JsonUtility.ToJson(saveData);
        File.WriteAllText(jsonPath, json);
        Debug.Log("Save complete at " + jsonPath);
    }
}
