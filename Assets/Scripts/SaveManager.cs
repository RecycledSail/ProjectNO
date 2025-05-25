using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using static GlobalVariables;

public static class SaveManager
{
    public static void OnLoad()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null) return;

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
        var gameData = JsonUtility.FromJson<SaveDataFormat>(json);


        // Load Provinces
        gameManager.provinces = new();
        foreach (var p in gameData.provinces)
        {
            var province = PROVINCES[p.name];
            province.population = p.population;
            gameManager.provinces[p.name] = province;
        }

        // Load Nations
        gameManager.nations = new();
        foreach (var n in gameData.nations)
        {
            var nation = NATIONS[n.name];
            var rnodes = new List<ResearchNode>();
            foreach (var rname in n.researchNodeNames)
            {
                if (RESEARCH_NODE.TryGetValue(rname, out var rnode))
                    rnodes.Add(rnode);
            }
            nation.doneResearches = rnodes;
            foreach (var pname in n.provinces)
            {
                if (gameManager.provinces.TryGetValue(pname, out var province))
                    nation.AddProvinces(province);
            }
            gameManager.nations[n.name] = nation;
        }

        // DateTime
        gameManager.year = gameData.dateTime.year;
        gameManager.month = gameData.dateTime.month;
        gameManager.day = gameData.dateTime.day;

        //Users and user
        gameManager.users = new();
        int playerId = gameData.player.id;
        foreach (var u in gameData.users)
        {
            User user = new(u.id, gameManager.nations[u.nation]);
            if (user.id == playerId) gameManager.player = user;
            gameManager.users.Add(user);
        }


        Debug.Log("Load Done!");
    }
    public static void OnSave()
    {

        GameManager gameManager = GameManager.Instance;
        if (saveFileName == null)
        {
            Debug.LogError("saveFileName is null.");
            return;
        }
        string jsonPath = Path.Combine(Application.persistentDataPath, GlobalVariables.saveFileName + ".json");


        SaveDataFormat saveData = new SaveDataFormat();

        // Save Provinces & Color-to-province
        saveData.provinces = new List<SaveDataFormat.ProvinceData>();
        foreach (var p in gameManager.provinces.Values)
        {
            SaveDataFormat.ProvinceData provinceData = new SaveDataFormat.ProvinceData();
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

        // Save Nations
        saveData.nations = new();
        foreach (var n in gameManager.nations.Values)
        {
            SaveDataFormat.NationData nationData = new();
            nationData.id = n.id;
            nationData.name = n.name;
            List<string> researchNodeNames = new();
            foreach(ResearchNode node in n.doneResearches)
            {
                researchNodeNames.Add(node.name);
            }
            nationData.researchNodeNames = researchNodeNames;
            List<string> provinces = new();
            foreach(Province province in n.provinces)
            {
                provinces.Add(province.name);
            }
            nationData.provinces = provinces;
            saveData.nations.Add(nationData);
        }


        // Save datetime
        saveData.dateTime = new();
        saveData.dateTime.year = gameManager.year;
        saveData.dateTime.month = gameManager.month;
        saveData.dateTime.day = gameManager.day;

        saveData.users = new();
        foreach(var u in gameManager.users)
        {
            SaveDataFormat.UserData user = new();
            user.id = u.id;
            user.nation = u.nation.name;
            saveData.users.Add(user);
        }

        saveData.player = new();
        saveData.player.id = gameManager.player.id;
        saveData.player.nation = gameManager.player.nation.name;

        Debug.Log(saveData);

        string json = JsonUtility.ToJson(saveData);
        File.WriteAllText(jsonPath, json);
        Debug.Log("Save complete at " + jsonPath);
    }

    [System.Serializable]
    public class SaveDataFormat
    {
        public List<UserData> users;
        public UserData player;
        public List<BuffData> buffs;
        public List<ResearchNodeData> researchNodes;
        public List<NationData> nations;
        public List<ProvinceData> provinces;
        public DateTimeWrapper dateTime;

        [System.Serializable]
        public sealed class UserData { public int id; public string nation; }

        [System.Serializable]
        public sealed class BuffData { public int id; public string name; public string kind; public double value; }

        [System.Serializable]
        public sealed class ResearchNodeData { public int id; public string name; public double cost; public List<string> buffNames; }

        [System.Serializable]
        public sealed class NationData { public int id; public string name; public List<string> researchNodeNames; public List<string> provinces; }

        [System.Serializable]
        public sealed class ProvinceData { public int id; public string name; public int population; public string topography; public List<byte> color; }

        [System.Serializable]
        public sealed class DateTimeWrapper { public int year; public int month; public int day; }
    }
}
