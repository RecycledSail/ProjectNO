using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// Quantities are cumulative: acquired includes material already consumed.
internal sealed class ConstructionMaterials
{
    private readonly Dictionary<string, long> _required = new(StringComparer.Ordinal);
    private Dictionary<string, long> _acquired = new(StringComparer.Ordinal);
    private Dictionary<string, long> _consumed = new(StringComparer.Ordinal);
    private readonly int _startBasisPoints;
    private long _version;

    public IReadOnlyDictionary<string, long> Required { get; }
    public IReadOnlyDictionary<string, long> Acquired => new ReadOnlyDictionary<string, long>(_acquired);
    public IReadOnlyDictionary<string, long> Consumed => new ReadOnlyDictionary<string, long>(_consumed);
    public long Spending { get; private set; }

    internal ConstructionMaterials(BuildingRecipe recipe)
    {
        _startBasisPoints = recipe?.StartMaterialBasisPoints ?? 3000;
        if (recipe != null)
        {
            foreach (KeyValuePair<string, int> item in recipe.requireItems)
            {
                if (item.Value <= 0)
                    throw new ArgumentException("Construction materials must have positive requirements.");
                _required.Add(item.Key, item.Value);
                _acquired.Add(item.Key, 0);
                _consumed.Add(item.Key, 0);
            }
        }
        Required = new ReadOnlyDictionary<string, long>(_required);
    }

    public bool CanStart
    {
        get
        {
            foreach (KeyValuePair<string, long> item in _required)
                if (_acquired[item.Key] < (item.Value * _startBasisPoints + 9999L) / 10000L)
                    return false;
            return true;
        }
    }

    public decimal ProgressLimit
    {
        get
        {
            decimal limit = 1m;
            foreach (KeyValuePair<string, long> item in _required)
                limit = Math.Min(limit, (decimal)_acquired[item.Key] / item.Value);
            return limit;
        }
    }

    internal sealed class Acquisition
    {
        internal readonly ConstructionMaterials Owner;
        internal readonly long Version;
        internal readonly Dictionary<string, long> Quantities;
        internal readonly long Spending;

        internal Acquisition(ConstructionMaterials owner, long version,
            Dictionary<string, long> quantities, long spending)
        {
            Owner = owner;
            Version = version;
            Quantities = quantities;
            Spending = spending;
        }
    }

    internal bool TryPrepareAcquisition(IReadOnlyDictionary<string, long> quantities,
        long spending, out Acquisition acquisition)
    {
        acquisition = null;
        if (quantities == null || spending < 0)
            return false;
        try
        {
            Dictionary<string, long> next = new(_acquired, StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> item in quantities)
            {
                if (item.Value < 0 || !_required.TryGetValue(item.Key, out long required))
                    return false;
                long total = checked(next[item.Key] + item.Value);
                if (total > required)
                    return false;
                next[item.Key] = total;
            }
            acquisition = new Acquisition(this, _version, next, checked(Spending + spending));
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    // The procurement phase prepares every project before settling the market,
    // then commits each token once without intervening project mutations.
    internal void CommitAcquisition(Acquisition acquisition)
    {
        if (acquisition == null || acquisition.Owner != this || acquisition.Version != _version)
            throw new InvalidOperationException("The material acquisition is stale or belongs to another project.");
        _acquired = acquisition.Quantities;
        Spending = acquisition.Spending;
        _version++;
    }

    internal Dictionary<string, long> PrepareConsumption(decimal progress)
    {
        Dictionary<string, long> next = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> item in _required)
        {
            long consumed = progress == 1m ? item.Value :
                decimal.ToInt64(decimal.Ceiling(item.Value * progress));
            // A repeating decimal boundary must not round one unit above stock.
            next.Add(item.Key, Math.Min(consumed, _acquired[item.Key]));
        }
        return next;
    }

    internal void CommitConsumption(Dictionary<string, long> consumed) => _consumed = consumed;
}
