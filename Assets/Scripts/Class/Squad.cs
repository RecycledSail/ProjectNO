public class Squad
{
    public UnitType unitType { get; private set; }
    public int capacity { get; private set; }
    public int population {  get; private set; }

    public Squad(UnitType unitType)
    {
        this.unitType = unitType; 
        this.capacity = 100;
        this.population = 5;
    }

    public Squad(UnitType unitType, int capacity, int population)
    {
        this.unitType = unitType;
        this.capacity = capacity;
        this.population = population;
    }

    public void UpgradeCapacity()
    {
        capacity += 100;
    }

    public bool DowngradeCapacity()
    {
        capacity -= 100;
        return capacity > 0;
    }

    public void SetPopulation(int population)
    {
        this.population = population;
    }
    
}