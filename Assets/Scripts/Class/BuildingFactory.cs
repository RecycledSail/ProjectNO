public static class BuildingFactory
{
    public const string ConstructionCompanyTypeName = "construcntionCompany";

    public static Building Create(
        BuildingType buildingType,
        Province province,
        int level = 0,
        long currentWorkers = 0)
    {
        Building building = new Building(buildingType, province)
        {
            level = level,
            currentWorkers = currentWorkers
        };

        return building;
    }
}
