using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Called after payroll, road-cache refresh and nation assignment, before production.
public static class ConstructionWeeklySimulation
{
    public static void Process(IEnumerable<Nation> nations, IEnumerable<Province> provinces,
        IEnumerable<Province> paidProvinces, double weeklyManhoursPerLevel)
    {
        var companies = provinces.SelectMany(province => province.buildings.Values)
            .OfType<ConstructionCompanyBuilding>().Distinct().ToList();
        var projects = nations.SelectMany(nation => nation.ConstructionMandates)
            .Concat(companies.SelectMany(company => company.ActiveProjects))
            .Where(mandate => mandate.IsActive).Distinct().ToList();

        foreach (ConstructionMandate project in projects) project.ProcurementFailed = false;
        // Dictionary identity defines a shared market; grouping by province would spend its stock twice.
        foreach (var market in projects.Where(project => project.RequiredMaterials.Any(
                item => project.AcquiredMaterials[item.Key] < item.Value))
            .GroupBy(project => project.GetAccessibleProducts()))
        {
            var batch = market.ToList();
            if (ConstructionProcurement.TryProcessMarket(batch, market.Key, batch[0].TargetProvince.ActiveLedger))
                continue;
            foreach (ConstructionMandate project in batch) project.ProcurementFailed = true;
            Debug.LogError($"Construction procurement failed; projects: {string.Join(", ", batch.Select(project => project.Id))}");
        }

        var paid = new HashSet<Province>(paidProvinces);
        foreach (ConstructionCompanyBuilding company in companies)
            if (paid.Contains(company.province)) company.ProgressWeekly(weeklyManhoursPerLevel);
    }
}
