using System.Collections.Generic;

/// <summary>
/// ���� Ŭ����
/// ID, �̸�, ���� ���κ�, ��� ����
/// </summary>
public class Nation
{
    public int id;
    public string name { get; }
    public List<Province> provinces { get; set; }
    public long balance { get; set; }
    public List<Regiment> regiments { get; set; }
    public Dictionary<Nation, Diplomacy> allies;
    public Dictionary<Nation, Diplomacy> enemies;
    public List<ResearchNode> doneResearches;
    public Dictionary<BuffKind, double> buffs;

    /// <summary>
    /// ���� ������
    /// </summary>
    /// <param name="id">������ ID</param>
    /// <param name="name">������ �̸�(�ڵ�)</param>
    public Nation(int id, string name, List<ResearchNode> researches)
    {
        this.id = id;
        this.name = name;
        provinces = new List<Province>();
        regiments = new List<Regiment>();
        doneResearches = researches;
        buffs = new Dictionary<BuffKind, double>();
        foreach (ResearchNode research in doneResearches)
        {
            foreach (Buff buff in research.buffs)
            {
                double prevValue = 0;
                buffs.TryGetValue(buff.baseBuff, out prevValue);
                buffs.Add(buff.baseBuff, prevValue + buff.power);
            }
        }
        allies = new();
        enemies = new();
    }

    /// <summary>
    /// �ش� ���κ󽺰� �ִ��� �˻��ϴ� �޼���
    /// </summary>
    /// <param name="province">�˻��ϰ��� �ϴ� ���κ�</param>
    /// <returns>���κ� ���� ���̸� true, �ƴϸ� false</returns>
    public bool HasProvinces(Province province)
    {
        return provinces.Find(x => x.Equals(province)) != null;
    }

    /// <summary>
    /// ���κ󽺸� �߰��ϴ� �޼���
    /// �߰� �õ� �� ���� ���ο� ���� boolean ��ȯ
    /// </summary>
    /// <param name="province">�߰��ϰ��� �ϴ� ���κ�</param>
    /// <returns>�߰� �����ϸ� true, �ƴϸ� false</returns>
    public bool AddProvinces(Province province)
    {
        if (!HasProvinces(province))
        {
            provinces.Add(province);
            province.AddNation(this);
            return true;
        }
        else return false;
    }

    /// <summary>
    /// ���κ󽺸� �����ϴ� �޼���
    /// ���� �õ� �� ���� ���ο� ���� boolean ��ȯ
    /// </summary>
    /// <param name="province">�����ϰ��� �ϴ� ���κ�</param>
    /// <returns>���� �����ϸ� true, �ƴϸ� false</returns>
    public bool RemoveProvinces(Province province)
    {
        bool avail = provinces.Remove(province);
        if (avail)
        {
            return province.RemoveNation(this);
        }
        else return false;
    }

    /// <summary>
    /// ������ ��� ���κ��� �α��� ���� ��ȯ
    /// </summary>
    /// <returns>������ ��� ���κ��� �α� ��</returns>
    public long GetPopulation()
    {
        long sum = 0;
        foreach (Province province in provinces)
        {
            sum += province.population;
        }
        return sum;
    }

    /// <summary>
    /// ���븦 �߰��ϴ� �޼���
    /// �߰� �õ� �� ���� ���ο� ���� boolean ��ȯ
    /// </summary>
    /// <param name="regiment">�߰��ϰ��� �ϴ� ����</param>
    /// <returns>���� �� true, ���� �� false</returns>
    public bool AddRegiment(Regiment regiment)
    {
        if (regiment == null) return false;
        else
        {
            this.regiments.Add(regiment);
            return true;
        }
    }
}