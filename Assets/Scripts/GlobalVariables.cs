using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.VisualScripting;
using System;
using Unity.Collections;

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
    public static Dictionary<string, Buff> BUFF = new();

    /// <summary>
    /// 연구 노드를 저장하는 Dictionary
    /// Key: 연구 노드 이름, Value: ResearchNode 객체
    /// </summary>
    public static Dictionary<string, ResearchNode> RESEARCH_NODE = new();

    /// <summary>
    /// 유닛 타입을 저장하는 Dictionary
    /// Key: string (유닛 타입의 name), Value: 유닛 타입의 값
    /// </summary>
    public static Dictionary<string, UnitType> UNIT_TYPE = new();


    /// <summary>
    /// 국가 정보를 저장하는 Dictionary
    /// Key: 국가 이름, Value: Nation 객체
    /// </summary>
    public static Dictionary<string, Nation> NATIONS = new();

    /// <summary>
    /// 각 국가가 시작 시 소유하는 주 목록
    /// Key: 국가 이름, Value: 주 이름 리스트
    /// </summary>
    public static Dictionary<string, List<string>> INITIAL_PROVINCES = new();

    /// <summary>
    /// 게임 내 모든 주 정보를 저장하는 Dictionary
    /// Key: 주 이름, Value: Province 객체
    /// </summary>
    public static Dictionary<string, Province> PROVINCES = new();

    /// <summary>
    /// 각 주와 인접한 주들의 정보를 저장하는 Dictionary
    /// Key: 주 이름, Value: 인접한 주의 리스트
    /// </summary>
    public static Dictionary<string, List<Province>> ADJACENT_PROVINCES = new();

    /// <summary>
    /// 세이브 파일명 
    /// </summary>
    public static string saveFileName = null;

    /// <summary>
    /// 유닛 타입을 저장하는 Dictionary
    /// Key: string (유닛 타입의 name), Value: 유닛 타입의 값
    /// </summary>
    public static Dictionary<string, UnitType> UNIT_TYPES = new();

    /// <summary>
    /// 종족값을 저장하는 Dictionary
    /// Key: string (종족명), Value: 종족값
    /// </summary>
    public static Dictionary<string, SpeciesSpec> SPECIES_SPEC = new();

    /// <summary>
    /// 빌딩 타입을 저장하는 Dictionary
    /// Key: string (빌딩 종류), Value: 빌딩타입 클래스
    /// </summary>
    public static Dictionary<string, BuildingType> BUILDING_TYPE = new();


    /// <summary>
    /// 직업분류를 저장하는 Dictionary
    /// Key: string (직업명), Value: 직업타입 클래스
    /// </summary>
    public static Dictionary<string, JobType> JOB_TYPE = new();


    /// <summary>
    /// 상품 이름과 초기 가격설정 Dictionary
    /// Key: string (상품이름), Value: 초기상품가격
    /// </summary>
    public static Dictionary<string, Products> Products = new();



    /// <summary>
    /// Assets/Resources/GlovalVariables.json을 불러와 GlobalVariables의 멤버들을 채우는 함수
    /// JSON 구성은 하단 GameDataFormat 참조
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]

    public static void LoadData()
    {
        try
        {
            LoadDefaultData();
        }
        catch(Exception e)
        {
            Debug.LogError(e);
        }
    }
    public static void LoadDefaultData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("GlobalVariables");
        if (jsonFile == null)
        {
            Debug.LogError("GlobalVariables.json not found in Resources.");
            throw new Exception("GlobalVariables.json not found in Resources.");
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

        // Load Provinces
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

        // Diplomacy 로딩
        foreach (var diplomacyData in gameData.initialDiplomacies)
        {
            try
            {
                HashSet<Nation> lnations = new();
                foreach (var nationData in diplomacyData.lnations)
                {
                    Nation nation = NATIONS[nationData];
                    lnations.Add(nation);

                }
                HashSet<Nation> rnations = new();
                foreach (var nationData in diplomacyData.rnations)
                {
                    Nation nation = NATIONS[nationData];
                    rnations.Add(nation);
                }
                DiplomacyType type;
                if (diplomacyData.type == "ALLY")
                {
                    type = DiplomacyType.ALLY;
                }
                else if (diplomacyData.type == "ENEMY")
                {
                    type = DiplomacyType.ENEMY;
                }
                else
                {
                    throw new Exception("Parse Error while parsing diplomacyData");
                }
                Diplomacy diplomacy = new(lnations, rnations, type);

            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    /// <summary>
    /// 모든 세이브 파일명을 가져오는 메서드
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
    /// JSON에서 불러 올 포맷을 저장하는 클래스
    /// List -> 개별 클래스로 정의, System.Serializable로 직렬화해야 저장/불러오기 가능
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
        public List<InitialDiplomaciesWrapper> initialDiplomacies;



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
        [System.Serializable]
        public sealed class InitialDiplomaciesWrapper { public List<string> lnations; public List<string> rnations; public string type; }
    }
}
