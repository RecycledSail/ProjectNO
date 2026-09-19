using System.Collections.Generic;
using System.Linq;

public static class ConstructionWeeklySimulation
{
    public static IReadOnlyList<ConstructionMandate> ActiveProjects(
        IEnumerable<Nation> nations, IEnumerable<Province> provinces)
    {
        List<ConstructionCompanyBuilding> companies = provinces
            .Where(province => province != null)
            .SelectMany(province => province.buildings.Values)
            .OfType<ConstructionCompanyBuilding>()
            .Distinct()
            .ToList();
        return nations
            .Where(nation => nation != null)
            .SelectMany(nation => nation.ConstructionMandates)
            .Concat(companies.SelectMany(company => company.ActiveProjects))
            .Where(mandate => mandate != null && mandate.IsActive)
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    public static void ProgressPaidCompanies(IEnumerable<Province> provinces,
        IEnumerable<Province> paidProvinces, double weeklyManhoursPerLevel)
    {
        HashSet<Province> paid = new(paidProvinces);
        List<ConstructionCompanyBuilding> companies = provinces
            .Where(province => province != null)
            .SelectMany(province => province.buildings.Values)
            .OfType<ConstructionCompanyBuilding>()
            .Distinct()
            .ToList();
        foreach (ConstructionCompanyBuilding company in companies)
            if (paid.Contains(company.province))
                company.ProgressWeekly(weeklyManhoursPerLevel);
    }

    // Kept until the shared weekly market coordinator replaces the live-loop call.
    public static void Process(IEnumerable<Nation> nations, IEnumerable<Province> provinces,
        IEnumerable<Province> paidProvinces, double weeklyManhoursPerLevel) =>
        ProgressPaidCompanies(provinces, paidProvinces, weeklyManhoursPerLevel);
}
