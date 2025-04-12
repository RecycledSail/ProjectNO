using System.Collections.Generic;

// ���� ��带 �����ϴ� Ŭ����
public class ResearchNode
{
    //���� ����� ���� id
    public int id { get; set; }

    //���� ����� �̸�
    public string name { get; }

    //�ʿ��� ���� ��ġ
    public double requiredPoint { get; }

    //���� �Ϸ�� �����ϴ� ������
    public List<Buff> buffs { get; }

    //�� ������带 �����ϴµ� �ʿ��� ���� ��������
    private List<ResearchNode> prev;

    //�� ������尡 �ʿ��� ���� ��������
    private List<ResearchNode> next;

    /// <summary>
    /// ResearchNode ������
    /// </summary>
    /// <param name="id">ResearchNode�� ���� ID</param>
    /// <param name="name">ResearchNode�� �̸�</param>
    public ResearchNode(int id, string name, double requiredPoint, List<Buff> buffs)
    {
        this.id = id;
        this.name = name;
        this.requiredPoint = requiredPoint;
        this.buffs = buffs;
        prev = new List<ResearchNode>();
        next = new List<ResearchNode>();
    }

    public void AddPrevResearch(ResearchNode node)
    {
        prev.Add(node);
    }

    public void AddNextResearch(ResearchNode node)
    {
        next.Add(node);
    }
}
