using System;

public sealed class MarketPriceSettings
{
    public int SmoothingBasisPoints { get; }
    public int MaxWeeklyChangeBasisPoints { get; }

    public MarketPriceSettings(int smoothingBasisPoints, int maxWeeklyChangeBasisPoints)
    {
        if (smoothingBasisPoints < 0 || smoothingBasisPoints > 10_000)
            throw new ArgumentOutOfRangeException(nameof(smoothingBasisPoints));
        if (maxWeeklyChangeBasisPoints < 1 || maxWeeklyChangeBasisPoints > 10_000)
            throw new ArgumentOutOfRangeException(nameof(maxWeeklyChangeBasisPoints));

        SmoothingBasisPoints = smoothingBasisPoints;
        MaxWeeklyChangeBasisPoints = maxWeeklyChangeBasisPoints;
    }
}

public static class MarketPriceCalculator
{
    public static int CalculateNextPrice(
        int previousPrice,
        int requestedDemand,
        int availableStock,
        float elasticity,
        MarketPriceSettings settings)
    {
        if (previousPrice < 1 || requestedDemand < 0 || availableStock < 0 ||
            float.IsNaN(elasticity) || float.IsInfinity(elasticity) || elasticity < 0f)
        {
            throw new ArgumentOutOfRangeException();
        }
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (requestedDemand == 0 && availableStock == 0) return previousPrice;

        double ratio = ((double)requestedDemand + 1d) / (availableStock + 1d);
        double pressure = previousPrice * Math.Pow(ratio, elasticity);
        double smooth = settings.SmoothingBasisPoints / 10_000d;
        double candidate = previousPrice * (1d - smooth) + pressure * smooth;
        int lower = Math.Max(1, (int)Math.Floor(previousPrice *
            (1d - settings.MaxWeeklyChangeBasisPoints / 10_000d)));
        int upper = Math.Max(1, (int)Math.Ceiling(previousPrice *
            (1d + settings.MaxWeeklyChangeBasisPoints / 10_000d)));

        if (double.IsNaN(candidate) || double.IsInfinity(candidate))
            throw new OverflowException();

        candidate = Math.Max(lower, Math.Min(upper, candidate));
        return Math.Max(1, checked((int)Math.Round(candidate, MidpointRounding.AwayFromZero)));
    }
}
