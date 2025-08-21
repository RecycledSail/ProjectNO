/// <summary>
/// 유닛 타입 클래스
/// </summary>
public class UnitType
{
    public int id { get; }
    public string name { get; }
    public double attackPerUnit { get; }
    public double defensePerUnit { get; }
    public double moveSpeedPerUnit { get; }

    public UnitType(int id, string name, double attackPerUnit, double defensePerUnit, double moveSpeedPerUnit)
    {
        this.id = id;
        this.name = name;
        this.attackPerUnit = attackPerUnit;
        this.defensePerUnit = defensePerUnit;
        this.moveSpeedPerUnit = moveSpeedPerUnit;
    }
}