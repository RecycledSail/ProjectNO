using System;
using System.Globalization;

// Reads contract snapshots and the current market without changing simulation state.
public static class ConstructionStatusText
{
    public static string Summary(ConstructionMandate mandate)
    {
        if (mandate == null) return "";
        double progress = 1d - mandate.RemainingManhours / mandate.RequiredManhours;
        return $"{State(mandate)}\n{progress.ToString("0%", CultureInfo.InvariantCulture)}";
    }

    public static string Format(ConstructionMandate mandate)
    {
        if (mandate == null) return "";
        return State(mandate, false) +
            $"\nProgress: {(1d - mandate.RemainingManhours / mandate.RequiredManhours).ToString("0%", CultureInfo.InvariantCulture)}" +
            $"\nMaterial progress limit: {mandate.MaterialProgressLimit.ToString("0.##%", CultureInfo.InvariantCulture)}" +
            $"\nRemaining labor hours: {mandate.RemainingManhours.ToString("0.##", CultureInfo.InvariantCulture)}" +
            $"\nMaterials spent: {Money(mandate.MaterialSpending)}" +
            $"\nConstruction fee paid: {Money(mandate.PaidConstructionFee)} / {Money(mandate.ConstructionFee)}" +
            $"\nOperating capital: {Money(mandate.InvestedCapital)}" +
            $"\nEstimated remaining materials: {EstimateRemaining(mandate)}";
    }

    // Short labels fit the existing 158px cell at its serialized 30pt font size.
    private static string State(ConstructionMandate mandate, bool compact = true)
    {
        if (mandate.Status == ConstructionMandateStatus.Completed) return "Complete";
        if (mandate.Status == ConstructionMandateStatus.Cancelled) return "Cancelled";
        if (mandate.ProcurementFailed) return compact ? "Buy failed" : "Procurement failed";
        double completed = 1d - mandate.RemainingManhours / mandate.RequiredManhours;
        if (!mandate.CanStart || (mandate.MaterialProgressLimit < 1m &&
            completed >= (double)mandate.MaterialProgressLimit)) return compact ? "Wait mats" : "Waiting for materials";
        if (mandate.Status == ConstructionMandateStatus.Requested) return compact ? "Queued" : "Awaiting contractor";
        return mandate.Status == ConstructionMandateStatus.InProgress ? "Building" : "Ready";
    }

    private static string EstimateRemaining(ConstructionMandate mandate)
    {
        long estimate = 0;
        var products = mandate.GetAccessibleProducts();
        try
        {
            foreach (var material in mandate.RequiredMaterials)
            {
                long remaining = material.Value - mandate.AcquiredMaterials[material.Key];
                if (remaining == 0) continue;
                if (products == null || !products.TryGetValue(material.Key, out ProductState product) ||
                    product == null || product.Price <= 0 || product.ProductName != material.Key)
                    return "Unavailable";
                estimate = checked(estimate + checked(remaining * product.Price));
            }
        }
        catch (OverflowException) { return "Unavailable"; }
        return Money(estimate);
    }

    private static string Money(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);
}
