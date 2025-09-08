using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.VisualScripting;
using System;
using Unity.Collections;

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
    public static Dictionary<string, Buff> BUFF = new();

    /// <summary>
    /// ���� ���? �����ϴ� Dictionary
    /// Key: ���� ���? �̸�, Value: ResearchNode ��ü
    /// </summary>
    public static Dictionary<string, ResearchNode> RESEARCH_NODE = new();

    /// <summary>
    /// ���� Ÿ���� �����ϴ� Dictionary
    /// Key: string (���� Ÿ���� name), Value: ���� Ÿ���� ��
    /// </summary>
    public static Dictionary<string, UnitType> UNIT_TYPE = new();


    /// <summary>
    /// ���� ������ �����ϴ� Dictionary
    /// Key: ���� �̸�, Value: Nation ��ü
    /// </summary>
    public static Dictionary<string, Nation> NATIONS = new();

    /// <summary>
    /// �� ������ ���� �� �����ϴ� �� ���?
    /// Key: ���� �̸�, Value: �� �̸� ����Ʈ
    /// </summary>
    public static Dictionary<string, List<string>> INITIAL_PROVINCES = new();

    /// <summary>
    /// ���� �� ���? �� ������ �����ϴ� Dictionary
    /// Key: �� �̸�, Value: Province ��ü
    /// </summary>
    public static Dictionary<string, Province> PROVINCES = new();

    /// <summary>
    /// �� �ֿ� ������ �ֵ��� ������ �����ϴ� Dictionary
    /// Key: �� �̸�, Value: ������ ���� ����Ʈ
    /// </summary>
    public static Dictionary<string, List<Province>> ADJACENT_PROVINCES = new();

    /// <summary>
    /// ���̺� ���ϸ� 
    /// </summary>
    public static string saveFileName = null;

    /// <summary>
    /// ���� Ÿ���� �����ϴ� Dictionary
    /// Key: string (���� Ÿ���� name), Value: ���� Ÿ���� ��
    /// </summary>
    public static Dictionary<string, UnitType> UNIT_TYPES = new();

    /// <summary>
    /// �������� �����ϴ� Dictionary
    /// Key: string (������), Value: ������
    /// </summary>
    public static Dictionary<string, SpeciesSpec> SPECIES_SPEC = new();

    /// <summary>
    /// ���� Ÿ���� �����ϴ� Dictionary
    /// Key: string (���� ����), Value: ����Ÿ�� Ŭ����
    /// </summary>
    public static Dictionary<string, BuildingType> BUILDING_TYPE = new();


    /// <summary>
    /// �����з��� �����ϴ� Dictionary
    /// Key: string (������), Value: ����Ÿ�� Ŭ����
    /// </summary>
    public static Dictionary<string, JobType> JOB_TYPE = new();


    /// <summary>
    /// ��ǰ �̸��� �ʱ� ���ݼ��� Dictionary
    /// Key: string (��ǰ�̸�), Value: �ʱ��ǰ����?
    /// </summary>
    public static Dictionary<string, Products> PRODUCTS = new();



    /// <summary>
    /// ���� �����Ǹ� �����ϴ� Dictionary
    /// Key: string (���� ������ �̸�), Value: BuildingRecipe Ŭ����
    /// </summary>
    public static Dictionary<string, BuildingRecipe> BUILDING_RECIPE = new();


    /// <summary>
    /// Assets/Resources/*.json�� �ҷ��� GlobalVariables class�� static �������? ä���? �Լ�
    /// JSON ������ �ϴ� GameDataFormat ����
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void LoadData()
    {
        try
        {
            //LoadDefaultData();
            LoadBuff();
            LoadResearchNodes();
            LoadUnitTypes();
            LoadSpeciesSpecs();
            LoadJobTypes();
            LoadBuildingTypes();
            LoadProvinces();
            LoadAdjacentProvinces();
            LoadNations();
            LoadInitialProvinces();
            LoadProducts();
            LoadInitialDiplomacies();
        }
        catch(Exception e)
        {
            Debug.LogError(e);
        }
    }

    /// <summary>
    /// {JsonName}.json�� �ҷ��ͼ� T Ŭ�������� �����ϴ� �Լ�
    /// </summary>
    /// <typeparam name="T">Wrapper Ŭ���� (���� List<~~~Data>)</typeparam>
    /// <param name="jsonName">{jsonName}.json</param>
    /// <returns>ä���� Ŭ���� T</returns>
    /// <exception cref="Exception">jsonFile�� ���� �� ����</exception>
    public static T LoadJsonFile<T>(string jsonName)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(jsonName);
        if (jsonFile == null)
        {
            Debug.LogError(jsonName+".json not found in Resources.");
            throw new Exception(jsonName+".json not found in Resources.");
        }

        T gameData = JsonUtility.FromJson<T>(jsonFile.text);
        return gameData;
    }
    public static void LoadBuff()
    {
        var gameData = LoadJsonFile<GameDataFormat.BuffsWrapper>("Buffs");
        // Load Buffs
        foreach (var b in gameData.buffs)
        {
            var buff = new Buff(b.id, b.name, (BuffKind)System.Enum.Parse(typeof(BuffKind), b.kind), b.value);
            BUFF[b.name] = buff;
        }
    }
    public static void LoadResearchNodes()
    {
        var gameData = LoadJsonFile<GameDataFormat.ResearchNodesWrapper>("ResearchNodes");
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
    }
    public static void LoadUnitTypes()
    {
        var gameData = LoadJsonFile<GameDataFormat.UnitTypesWrapper>("UnitTypes");
        // Load Unit Types
        foreach (var data in gameData.unitTypes)
        {
            var unitType = new UnitType(data.id, data.name, data.attackPerUnit, data.defensePerUnit, data.moveSpeedPerUnit);
            UNIT_TYPE[data.name] = unitType;
        }
    }
    public static void LoadSpeciesSpecs()
    {
        var gameData = LoadJsonFile<GameDataFormat.SpeciesSpecsWrapper>("SpeciesSpecs");
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
    }
    public static void LoadJobTypes()
    {
        var gameData = LoadJsonFile<GameDataFormat.JobTypesWrapper>("JobTypes");
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
    }
    public static void LoadBuildingTypes()
    {
        var gameData = LoadJsonFile<GameDataFormat.BuildingTypesWrapper>("BuildingTypes");
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
    }
    public static void LoadProvinces()
    {
        var gameData = LoadJsonFile<GameDataFormat.ProvincesWrapper>("Provinces");
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
    }
    public static void LoadAdjacentProvinces()
    {
        var gameData = LoadJsonFile<GameDataFormat.AdjacentProvincesWrapper>("AdjacentProvinces");
        // Load Adjacent Provinces
        foreach (var pair in gameData.adjacentProvinces)
        {
            var list = new List<Province>();
            foreach (var pname in pair.adjacents)
                list.Add(PROVINCES[pname]);
            ADJACENT_PROVINCES[pair.province] = list;
        }
    }
    public static void LoadNations()
    {
        var gameData = LoadJsonFile<GameDataFormat.NationsWrapper>("Nations");
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
    }
    public static void LoadInitialProvinces()
    {
        var gameData = LoadJsonFile<GameDataFormat.InitialProvincesWrapper>("InitialProvinces");
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
    }
    public static void LoadProducts()
    {
        var gameData = LoadJsonFile<GameDataFormat.ProductsWrapper>("Products");
        // Products �ε� (īŻ�α�/���ذ�)
        foreach (var prod in gameData.products)
        {
            GlobalVariables.PRODUCTS[prod.name] = new Products(prod.InitialPrice);
            // or: GlobalVariables.Products[prod.name] = new ProductSpec(prod.name, prod.InitialPrice);
        }
    }
    public static void LoadInitialDiplomacies()
    {
        var gameData = LoadJsonFile<GameDataFormat.InitialDiplomaciesWrapper>("InitialDiplomacies");
        // Diplomacy �ε�
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

    public static void LoadBuildingRecipes()
    {
        var gameData = LoadJsonFile<GameDataFormat.BuildingrecipesWrapper>("Buildingrecipes");
        foreach (var data in gameData.buildingrecipes)
        {
            var required = new Dictionary<string,int>();
            foreach (var it in data.requireItems)
                required[it.name] = it.amount;

            var recipe = new BuildingRecipe(data.name) { requireItems = required };
            BUILDING_RECIPE[data.name] = recipe;
        }
    }





    [Obsolete("Replaced by micro loads")]
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

        //Load Building Recipes
        foreach (var data in gameData.buildingrecipes)
        {
            Dictionary<string, int> requiredItems = new();
            foreach (var requiredItem in data.requireItems)
            {
                requiredItems[requiredItem.name] = requiredItem.amount;
            }
            BuildingRecipe buildingRecipe = new BuildingRecipe(data.name)
            {
                requireItems = requiredItems
            };
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
        // Products �ε� (īŻ�α�/���ذ�)
        foreach (var prod in gameData.products)
        {
            GlobalVariables.PRODUCTS[prod.name] = new Products(prod.InitialPrice);
            // or: GlobalVariables.Products[prod.name] = new ProductSpec(prod.name, prod.InitialPrice);
        }

        // Diplomacy �ε�
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
    /// ���? ���̺� ���ϸ��� �������� �޼���
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
    /// JSON���� �ҷ� �� ������ �����ϴ� Ŭ����
    /// List -> ���� Ŭ������ ����, System.Serializable�� ����ȭ�ؾ� ����/�ҷ����� ����
    /// </summary>
    [System.Serializable]
    public class GameDataFormat
    {
        [System.Serializable]
        public class BuffsWrapper
        {
            public List<GameDataFormat.BuffData> buffs;
        }

        [System.Serializable]
        public class ResearchNodesWrapper
        {
            public List<GameDataFormat.ResearchNodeData> researchNodes;
        }

        [System.Serializable]
        public class UnitTypesWrapper
        {
            public List<GameDataFormat.UnitTypeData> unitTypes;
        }

        [System.Serializable]
        public class SpeciesSpecsWrapper
        {
            public List<GameDataFormat.SpeciesSpecData> speciesSpecs;
        }

        [System.Serializable]
        public class JobTypesWrapper
        {
            public List<GameDataFormat.JobTypeData> jobTypes;
        }

        [System.Serializable]
        public class BuildingTypesWrapper
        {
            public List<GameDataFormat.BuildingTypeData> buildingTypes;
        }

        [System.Serializable]
        public class ProvincesWrapper
        {
            public List<GameDataFormat.ProvinceData> provinces;
        }

        [System.Serializable]
        public class AdjacentProvincesWrapper
        {
            public List<GameDataFormat.AdjacentProvinceWrapper> adjacentProvinces;
        }

        [System.Serializable]
        public class NationsWrapper
        {
            public List<GameDataFormat.NationData> nations;
        }

        [System.Serializable]
        public class InitialProvincesWrapper
        {
            public List<GameDataFormat.InitialProvinceWrapper> initialProvinces;
        }

        [System.Serializable]
        public class ProductsWrapper
        {
            public List<GameDataFormat.ProductsData> products;
        }

        [System.Serializable]
        public class InitialDiplomaciesWrapper
        {
            public List<GameDataFormat.InitialDiplomacyWrapper> initialDiplomacies;
        }

        [System.Serializable]
        public class BuildingrecipesWrapper
        {
            public List<GameDataFormat.BuildingrecipeData> buildingrecipes;
        }


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
        public List<InitialDiplomacyWrapper> initialDiplomacies;
        public List<BuildingrecipeData> buildingrecipes;



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
        public sealed class ProductsData { public string name; public int InitialPrice; }
        [System.Serializable]
        public sealed class InitialDiplomacyWrapper { public List<string> lnations; public List<string> rnations; public string type; }
        [System.Serializable]
        public sealed class BuildingrecipeData { public string name; public List<ItemData> requireItems; }
    }
}
