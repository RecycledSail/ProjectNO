using System;
using System.Collections.Generic;
using System.Numerics;

public static class ProportionalAllocator
{
    public static Dictionary<T, long> Allocate<T>(
        long total,
        IReadOnlyDictionary<T, long> weights,
        Func<T, string> stableKey)
    {
        if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
        if (weights == null) throw new ArgumentNullException(nameof(weights));
        if (stableKey == null) throw new ArgumentNullException(nameof(stableKey));

        BigInteger totalWeight = BigInteger.Zero;
        Dictionary<T, long> result = new();
        foreach (KeyValuePair<T, long> pair in weights)
        {
            if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(weights));
            totalWeight += pair.Value;
            result.Add(pair.Key, 0);
        }

        if (total == 0 || totalWeight.IsZero)
            return result;

        List<AllocationPart<T>> parts = new();
        long allocated = 0;
        HashSet<string> stableKeys = new(StringComparer.Ordinal);
        foreach (KeyValuePair<T, long> pair in weights)
        {
            string key = stableKey(pair.Key);
            if (key == null) throw new ArgumentException("Stable keys cannot be null.", nameof(stableKey));
            if (!stableKeys.Add(key))
                throw new ArgumentException("Stable keys must be unique.", nameof(stableKey));

            BigInteger numerator = (BigInteger)total * pair.Value;
            BigInteger floorShare = numerator / totalWeight;
            long share = (long)floorShare;
            result[pair.Key] = share;
            allocated = checked(allocated + share);
            parts.Add(new AllocationPart<T>(pair.Key, numerator % totalWeight, key));
        }

        parts.Sort((left, right) =>
        {
            int remainderComparison = right.Remainder.CompareTo(left.Remainder);
            return remainderComparison != 0
                ? remainderComparison
                : string.CompareOrdinal(left.StableKey, right.StableKey);
        });

        long remainderUnits = total - allocated;
        for (long index = 0; index < remainderUnits; index++)
            result[parts[(int)index].Key]++;

        return result;
    }

    private sealed class AllocationPart<T>
    {
        public T Key { get; }
        public BigInteger Remainder { get; }
        public string StableKey { get; }

        public AllocationPart(T key, BigInteger remainder, string stableKey)
        {
            Key = key;
            Remainder = remainder;
            StableKey = stableKey;
        }
    }
}
