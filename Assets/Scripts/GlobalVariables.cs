using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.VisualScripting;

/// <summary>
/// GlobalVariables ??????? ???? ?? ?????????? ????? ??????? ??????? ???? ?????????.
/// ????, ??(Province), ????? ?????? ?? ?????? ??????? ??????.
/// </summary>
public static class GlobalVariables
{

    /// <summary>
    /// ?????? ??????? Dictionary
    /// Key: ???? ???, Value: Buff ???
    /// </summary>
    public static Dictionary<string, Buff> BUFF = new();

    /// <summary>
    /// ???? ??? ??????? Dictionary
    /// Key: ???? ??? ???, Value: ResearchNode ???
    /// </summary>
    public static Dictionary<string, ResearchNode> RESEARCH_NODE = new();

    /// <summary>
    /// ???? ???? Type?? ??????? Dictionary
    /// Key: ???? ???? ???, Value: UnitType ???
    /// </summary>
    public static Dictionary<string, UnitType> UNIT_TYPE = new();


    /// <summary>
    /// ???? ?????? ??????? Dictionary
    /// Key: ???? ???, Value: Nation ???
    /// </summary>
    public static Dictionary<string, Nation> NATIONS = new();

    /// <summary>
    /// ?? ?????? ???? ?? ??????? ?? ???
    /// Key: ???? ???, Value: ?? ??? ?????
    /// </summary>
    public static Dictionary<string, List<string>> INITIAL_PROVINCES = new();

    /// <summary>
    /// ???? ?? ??? ?? ?????? ??????? Dictionary
    /// Key: ?? ???, Value: Province ???
    /// </summary>
    public static Dictionary<string, Province> PROVINCES = new();

    /// <summary>
    /// ?? ??? ?????? ????? ?????? ??????? Dictionary
    /// Key: ?? ???, Value: ?????? ???? ?????
    /// </summary>
    public static Dictionary<string, List<Province>> ADJACENT_PROVINCES = new();

    /// <summary>
    /// ?? ???? ??????? ?? ?????? ??????? Dictionary
    /// Key: Color32 (?? ???? ????), Value: ?????? Province ???
    /// </summary>
    //public static Dictionary<Color32, Province> COLORTOPROVINCE = new Dictionary<Color32, Province>();

    /// <summary>
    /// ????? ????? 
    /// </summary>
    public static string saveFileName = null;

    /// <summary>
    /// ???? ????? ??????? Dictionary
    /// Key: string (???? ????? name), Value: ???? ????? ??
    /// </summary>
    public static Dictionary<string, UnitType> UNIT_TYPES = new();

    /// <summary>
    /// ???????? ??????? Dictionary
    /// Key: string (??????), Value: ??????
    /// </summary>
    public static Dictionary<string, SpeciesSpec> SPECIES_SPEC = new();

    /// <summary>
    /// ???? ????? ??????? Dictionary
    /// Key: string (???? ????), Value: ??????? ?????
    /// </summary>
    public static Dictionary<string, BuildingType> BUILDING_TYPE = new();


    /// <summary>
    /// ?????????? ??????? Dictionary
    /// Key: string (??????), Value: ??????? ?????
    /// </summary>
    public static Dictionary<string, JobType> JOB_TYPE = new();


    /// <summary>
    /// ??? ????? ??? ??????? Dictionary
    /// Key: string (??????), Value: ?????????
    /// </summary>
    public static Dictionary<string, Products> Products = new();



    /// <summary>
    /// Assets/Resources/GlovalVariables.json?? ????? GlobalVariables?? ??????? ???? ???
    /// JSON ?????? ??? GameDataFormat ????
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]


    public static void LoadDefaultData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("GlobalVariables");
        if (jsonFile == null)
        {
            Debug.LogError("GlobalVariables.json not found in Resources.");
            return;
        }

        var gameData = JsonUtility.FromJson<GameDataFormat>(jsonFile.text);

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

        // Load Unit Types
        foreach (var data in gameData.unitTypes)
        {
            var unitType = new UnitType(data.id, data.name, data.attackPerUnit, data.defensePerUnit, data.moveSpeedPerUnit);
            UNIT_TYPE[data.name] = unitType;
        }

        // Load Species Spec
        foreach (var data in gameData.speciesSpecs)
        {
            SpeciesSpec speciesSpec = new SpeciesSpec
            {
                name = data.name,
                foodNeeded = data.foodNeeded
            };
            SPECIES_SPEC[data.name] = speciesSpec;
        }

        // Load Job Types
        foreach (var data in gameData.jobTypes)
        {
            JobType jobType = new JobType
            {
                name = data.name,
                literacyNeeded = data.literacyNeeded,
                salary = data.salary
            };
            JOB_TYPE[data.name] = jobType;
        }

        // Load Building Types
        foreach (var data in gameData.buildingTypes)
        {
            Dictionary<string, int> produceItems = new();
            foreach (var produceItem in data.produceItems)
            {
                produceItems[produceItem.name] = produceItem.amount;
            }

            Dictionary<string, int> requiredItems = new();
            foreach (var requiredItem in data.requireItems)
            {
                requiredItems[requiredItem.name] = requiredItem.amount;
            }

            Dictionary<string, int> workerNeeded = new();
            foreach (var workerNeed in data.workerNeeded)
            {
                workerNeeded[workerNeed.name] = workerNeed.amount;
            }

            BuildingType buildingType = new BuildingType(data.name)
            {
                produceItems = produceItems,
                requireItems = requiredItems,
                workerNeeded = workerNeeded
            };
            BUILDING_TYPE[data.name] = buildingType;
        }

        // Load Provinces & Color-to-province
        foreach (var p in gameData.provinces)
        {
            List<Species> loadPops = new();
            int population = 0;
            foreach (var speciesData in p.pops)
            {
                Species species = new(speciesData.name)
                {
                    population = speciesData.population,
                    happiness = speciesData.happiness,
                    literacy = speciesData.literacy,
                    culture = speciesData.culture
                };
                loadPops.Add(species);
                population += species.population;
            }

            var province = new Province(p.id, p.name, population, (Topography)System.Enum.Parse(typeof(Topography), p.topography));

            Dictionary<BuildingType, Building> buildings = new();
            foreach (var building in p.buildings)
            {
                BuildingType buildingType = BUILDING_TYPE[building.buildingTypeName];
                buildings.Add(buildingType, new(buildingType, province)
                {
                    workerScale = building.workerScale,
                    level = building.level
                });
            }

            province.buildings = buildings;

            province.pops = loadPops;
            PROVINCES[p.name] = province;
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
            Nation nation = new(n.id, n.name, rnodes);
            foreach (var regimentData in n.regiments)
            {

                Regiment newRegiment = new(nation, regimentData.name, PROVINCES[regimentData.location]);
                foreach (var squad in regimentData.squads)
                {
                    Squad newSquad = new(UNIT_TYPE[squad.unitType], squad.capacity, squad.population);
                    newRegiment.units[UNIT_TYPE[squad.unitType]] = newSquad;
                }
                nation.AddRegiment(newRegiment);
            }
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
        // Products 로딩 (카탈로그/기준가)
        foreach (var prod in gameData.products)
        {
            GlobalVariables.Products[prod.name] = new Products(prod.InitialPrice);
            // or: GlobalVariables.Products[prod.name] = new ProductSpec(prod.name, prod.InitialPrice);
        }


        
    }

    /// <summary>
    /// ??? ????? ??????? ???????? ?????
    /// </summary>
    /// <returns></returns>
    public static List<string> GetAllJsonFileNames()
    {
        List<string> jsonFileNames = new List<string>();
        string path = Application.persistentDataPath;

        if (Directory.Exists(path))
        {
            string[] fileList = Directory.GetFiles(path, "*.json");
            foreach (string filePath in fileList)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                jsonFileNames.Add(fileNameWithoutExtension);
                //Debug.Log(fileNameWithoutExtension);
            }

        }
        else
        {
            Debug.LogWarning("Persistent data path does not exist: " + path);
        }

        return jsonFileNames;
    }

    /// <summary>
    /// JSON???? ??? ?? ?????? ??????? ?????
    /// List -> ???? ??????? ????, System.Serializable?? ???????? ????/??????? ????
    /// </summary>
    [System.Serializable]
    public class GameDataFormat
    {
        public List<BuffData> buffs;
        public List<ResearchNodeData> researchNodes;
        public List<UnitTypeData> unitTypes;
        public List<NationData> nations;
        public List<ProvinceData> provinces;
        public List<InitialProvinceWrapper> initialProvinces;
        public List<AdjacentProvinceWrapper> adjacentProvinces;
        public List<SpeciesSpecData> speciesSpecs;
        public List<JobTypeData> jobTypes;
        public List<BuildingTypeData> buildingTypes;

        public List<ProductsData> products;



        [System.Serializable]
        public sealed class ItemData { public string name; public int amount; }

        [System.Serializable]
        public sealed class BuffData { public int id; public string name; public string kind; public double value; }

        [System.Serializable]
        public sealed class ResearchNodeData { public int id; public string name; public double cost; public List<string> buffNames; }

        [System.Serializable]
        public sealed class UnitTypeData { public int id; public string name; public double attackPerUnit; public double defensePerUnit; public double moveSpeedPerUnit; }

        [System.Serializable]
        public sealed class RegimentData { public string name; public string location; public List<SquadData> squads; }

        [System.Serializable]
        public sealed class SquadData { public string unitType; public int capacity; public int population; }

        [System.Serializable]
        public sealed class NationData { public int id; public string name; public List<string> researchNodeNames; public List<RegimentData> regiments; }

        [System.Serializable]
        public sealed class ProvinceData { public int id; public string name; public List<SpeciesPopData> pops; public string topography; public List<BuildingData> buildings; }

        [System.Serializable]
        public sealed class InitialProvinceWrapper { public string nation; public List<string> provinces; }

        [System.Serializable]
        public sealed class AdjacentProvinceWrapper { public string province; public List<string> adjacents; }

        [System.Serializable]
        public sealed class SpeciesSpecData { public string name; public int foodNeeded; }

        [System.Serializable]
        public sealed class SpeciesPopData { public string name; public int population; public int happiness; public int literacy; public int culture; }

        [System.Serializable]
        public sealed class BuildingTypeData { public string name; public List<ItemData> requireItems; public List<ItemData> produceItems; public List<ItemData> workerNeeded; }

        [System.Serializable]
        public sealed class JobTypeData { public string name; public bool literacyNeeded; public int salary; }

        [System.Serializable]
        public sealed class BuildingData { public string buildingTypeName; public double workerScale; public int level; }

        [System.Serializable]
        public sealed class ProductsData{ public string name; public int InitialPrice;}
    }
}
