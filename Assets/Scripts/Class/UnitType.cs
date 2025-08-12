/// <summary>
/// 유닛 타입 클래스
/// </summary>
public class UnitType
{
    public int id { get; }
    public string name { get; }
    public int attackPerUnit { get; }
    public int defensePerUnit { get; }
    public int moveSpeedPerUnit { get; }

    public UnitType(int id, string name, int attackPerUnit, int defensePerUnit, int moveSpeedPerUnit)
    {
        this.id = id;
        this.name = name;
        this.attackPerUnit = attackPerUnit;
        this.defensePerUnit = defensePerUnit;
        this.moveSpeedPerUnit = moveSpeedPerUnit;
    }
}