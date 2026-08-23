public static class BuildingFactory
{
    public const string ConstructionCompanyTypeName = "construcntionCompany";

    public static Building Create(
        BuildingType buildingType,
        Province province,
        int level = 0,
        long currentWorkers = 0)
    {
        Building building = buildingType.name == ConstructionCompanyTypeName
            ? new ConstructionCompanyBuilding(buildingType, province, level)
            : new Building(buildingType, province) { level = level };

        building.currentWorkers = currentWorkers;

        return building;
    }

    public static bool TryCreateAndRegister(
        BuildingType buildingType,
        Province province,
        out Building building)
    {
        building = null;
        if (buildingType == null || province == null || province.ActiveLedger == null)
            return false;

        Building candidate = Create(buildingType, province);
        if (!province.ActiveLedger.RegisterEmptyAccount(candidate.Account))
            return false;

        province.buildings[buildingType] = candidate;
        building = candidate;
        return true;
    }
}
