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
    public static int CalculateMaximumBid(int referencePrice, int quantity,
        long availableBudget, MarketPriceSettings settings)
    {
        if (referencePrice < 1 || quantity < 1 || availableBudget < 0)
            throw new ArgumentOutOfRangeException();
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        return (int)Math.Min(UpperPrice(referencePrice, settings), availableBudget / quantity);
    }

    private static int UpperPrice(int referencePrice, MarketPriceSettings settings) =>
        (int)Math.Min(int.MaxValue,
            ((long)referencePrice * (10_000 + settings.MaxWeeklyChangeBasisPoints) + 9_999) / 10_000);

    public static int ObservedPrice(ProductState product)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        return product.LastClearingPrice > 0
            ? product.LastClearingPrice
            : Math.Max(1, product.LastPrice);
    }

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
        int lower = (int)Math.Max(1,
            (long)previousPrice * (10_000 - settings.MaxWeeklyChangeBasisPoints) / 10_000);
        int upper = UpperPrice(previousPrice, settings);

        if (double.IsNaN(candidate) || double.IsInfinity(candidate))
            throw new OverflowException();

        candidate = Math.Max(lower, Math.Min(upper, candidate));
        return Math.Max(1, checked((int)Math.Round(candidate, MidpointRounding.AwayFromZero)));
    }
}
