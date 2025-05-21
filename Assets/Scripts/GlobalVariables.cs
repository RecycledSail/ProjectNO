using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.VisualScripting;

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
    public static Dictionary<string, Buff> BUFF = new Dictionary<string, Buff>();

    /// <summary>
    /// ���� ��带 �����ϴ� Dictionary
    /// Key: ���� ��� �̸�, Value: ResearchNode ��ü
    /// </summary>
    public static Dictionary<string, ResearchNode> RESEARCH_NODE = new Dictionary<string, ResearchNode>();

    /// <summary>
    /// ���� ������ �����ϴ� Dictionary
    /// Key: ���� �̸�, Value: Nation ��ü
    /// </summary>
    public static Dictionary<string, Nation> NATIONS = new Dictionary<string, Nation>();

    /// <summary>
    /// �� ������ ���� �� �����ϴ� �� ���
    /// Key: ���� �̸�, Value: �� �̸� ����Ʈ
    /// </summary>
    public static Dictionary<string, List<string>> INITIAL_PROVINCES = new Dictionary<string, List<string>>();

    /// <summary>
    /// ���� �� ��� �� ������ �����ϴ� Dictionary
    /// Key: �� �̸�, Value: Province ��ü
    /// </summary>
    public static Dictionary<string, Province> PROVINCES = new Dictionary<string, Province>();

    /// <summary>
    /// �� �ֿ� ������ �ֵ��� ������ �����ϴ� Dictionary
    /// Key: �� �̸�, Value: ������ ���� ����Ʈ
    /// </summary>
    public static Dictionary<string, List<Province>> ADJACENT_PROVINCES = new Dictionary<string, List<Province>>();

    /// <summary>
    /// �� ���� �����ϴ� �� ������ �����ϴ� Dictionary
    /// Key: Color32 (�� ���� ����), Value: �ش��ϴ� Province ��ü
    /// </summary>
    public static Dictionary<Color32, Province> COLORTOPROVINCE = new Dictionary<Color32, Province>();

    /// <summary>
    /// ���̺� ���ϸ� 
    /// </summary>
    public static string saveFileName = "save";

    /// <summary>
    /// Assets/Resources/GlovalVariables.json�� �ҷ��� GlobalVariables�� ������� ä��� �Լ�
    /// JSON ������ �ϴ� GameDataFormat ����
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
    }

    /// <summary>
    /// JSON���� �ҷ� �� ������ �����ϴ� Ŭ����
    /// List -> ���� Ŭ������ ����, System.Serializable�� ����ȭ�ؾ� ����/�ҷ����� ����
    /// </summary>
    [System.Serializable]
    public class GameDataFormat
    {
        public List<BuffData> buffs;
        public List<ResearchNodeData> researchNodes;
        public List<NationData> nations;
        public List<ProvinceData> provinces;
        public List<InitialProvinceWrapper> initialProvinces;
        public List<AdjacentProvinceWrapper> adjacentProvinces;

        [System.Serializable]
        public sealed class BuffData { public int id; public string name; public string kind; public double value; }

        [System.Serializable]
        public sealed class ResearchNodeData { public int id; public string name; public double cost; public List<string> buffNames; }

        [System.Serializable]
        public sealed class NationData { public int id; public string name; public List<string> researchNodeNames; }

        [System.Serializable]
        public sealed class ProvinceData { public int id; public string name; public int population; public string topography; public List<byte> color; }

        [System.Serializable]
        public sealed class InitialProvinceWrapper { public string nation; public List<string> provinces; }

        [System.Serializable]
        public sealed class AdjacentProvinceWrapper { public string province; public List<string> adjacents; }
    }

    


}
