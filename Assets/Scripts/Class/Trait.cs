public class Trait
{
    public int id { get; }
    public string name { get; }
    public Buff buff { get; }

    public Trait(int id, string name, Buff buff)
    {
        this.id = id;
        this.name = name;
        this.buff = buff;
    }
}