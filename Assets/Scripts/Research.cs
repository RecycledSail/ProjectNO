using System.Collections.Generic;

// 연구 노드를 정의하는 클래스
public class ResearchNode
{
    //연구 노드의 고유 id
    public int id { get; set; }

    //연구 노드의 이름
    public string name { get; }

    //필요한 연구 수치
    public double requiredPoint { get; }

    //연구 완료시 보유하는 버프들
    public List<Buff> buffs { get; }

    //이 연구노드를 진행하는데 필요한 이전 연구노드들
    private List<ResearchNode> prev;

    //이 연구노드가 필요한 다음 연구노드들
    private List<ResearchNode> next;

    /// <summary>
    /// ResearchNode 생성자
    /// </summary>
    /// <param name="id">ResearchNode의 고유 ID</param>
    /// <param name="name">ResearchNode의 이름</param>
    public ResearchNode(int id, string name, double requiredPoint, List<Buff> buffs)
    {
        this.id = id;
        this.name = name;
        this.requiredPoint = requiredPoint;
        this.buffs = buffs;
    }
}
