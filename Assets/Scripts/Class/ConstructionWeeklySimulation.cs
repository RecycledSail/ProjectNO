using System.Collections.Generic;
using System.Linq;

public static class ConstructionWeeklySimulation
{
    public static IReadOnlyList<ConstructionMandate> ActiveProjects(
        IEnumerable<Nation> nations, IEnumerable<Province> provinces)
    {
        List<ConstructionCompanyBuilding> companies = (provinces ?? Enumerable.Empty<Province>())
            .Where(province => province != null)
            .SelectMany(province => province.buildings?.Values ?? Enumerable.Empty<Building>())
            .OfType<ConstructionCompanyBuilding>()
            .Distinct()
            .ToList();
        return (nations ?? Enumerable.Empty<Nation>())
            .Where(nation => nation != null)
            .SelectMany(nation => nation.ConstructionMandates)
            .Concat(companies.SelectMany(company => company.ActiveProjects))
            .Where(mandate => mandate != null && mandate.IsActive)
            .Distinct()
            .OrderBy(mandate => mandate.Id, System.StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    public static void ProgressPaidCompanies(IEnumerable<Province> provinces,
        IEnumerable<Province> paidProvinces, double weeklyManhoursPerLevel)
    {
        HashSet<Province> paid = new(paidProvinces ?? Enumerable.Empty<Province>());
        List<ConstructionCompanyBuilding> companies = (provinces ?? Enumerable.Empty<Province>())
            .Where(province => province != null)
            .SelectMany(province => province.buildings?.Values ?? Enumerable.Empty<Building>())
            .OfType<ConstructionCompanyBuilding>()
            .Distinct()
            .OrderBy(company => company.Account?.Id, System.StringComparer.Ordinal)
            .ToList();
        foreach (ConstructionCompanyBuilding company in companies)
            if (paid.Contains(company.province))
                company.ProgressWeekly(weeklyManhoursPerLevel);
    }

    // Legacy direct entry point retained for callers that only need progression.
    public static void Process(IEnumerable<Nation> nations, IEnumerable<Province> provinces,
        IEnumerable<Province> paidProvinces, double weeklyManhoursPerLevel) =>
        ProgressPaidCompanies(provinces, paidProvinces, weeklyManhoursPerLevel);
}
