using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public partial class Building
{
    private readonly Dictionary<string, long> _inputInventory =
        new(StringComparer.Ordinal);
    private ReadOnlyDictionary<string, long> _readOnlyInputInventory;
    private int _inputInventoryVersion;
    private PreparedInputReceipt _preparedInputReceipt;

    public IReadOnlyDictionary<string, long> InputInventory =>
        _readOnlyInputInventory ??= new ReadOnlyDictionary<string, long>(_inputInventory);

    internal bool HasInputRecipe => buildingType?.requireItems != null &&
                                    buildingType.requireItems.Count > 0;

    public bool TryPrepareInputReceipt(
        IReadOnlyDictionary<string, long> quantities,
        out IPreparedMarketReceipt receipt)
    {
        receipt = null;
        if (quantities == null)
            return false;

        try
        {
            int nextVersion = checked(_inputInventoryVersion + 1);
            var next = new Dictionary<string, long>(_inputInventory, StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> quantity in quantities)
            {
                if (!IsKnownInput(quantity.Key) || quantity.Value < 0)
                    return false;
                if (quantity.Value == 0)
                    continue;

                next.TryGetValue(quantity.Key, out long current);
                next[quantity.Key] = checked(current + quantity.Value);
            }

            _inputInventory.EnsureCapacity(next.Count);
            var prepared = new PreparedInputReceipt(this, _inputInventoryVersion, nextVersion, next);
            _preparedInputReceipt = prepared;
            receipt = prepared;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal void RestoreInputInventory(IReadOnlyDictionary<string, long> quantities)
    {
        if (quantities == null)
            throw new ArgumentNullException(nameof(quantities));

        int nextVersion = checked(_inputInventoryVersion + 1);
        var restored = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> quantity in quantities)
        {
            if (!IsKnownInput(quantity.Key))
                throw new ArgumentException("Input inventory contains an unknown product.", nameof(quantities));
            if (quantity.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(quantities),
                    "Input inventory quantities must be nonnegative.");
            if (quantity.Value > 0)
                restored.Add(quantity.Key, quantity.Value);
        }

        _inputInventory.EnsureCapacity(restored.Count);
        ReplaceInputInventory(restored);
        _inputInventoryVersion = nextVersion;
        _preparedInputReceipt = null;
    }

    internal bool TryPrepareInputConsumption(
        long workerUnits,
        out long completeUnits,
        out PreparedInputConsumption consumption)
    {
        completeUnits = 0;
        consumption = null;
        if (workerUnits <= 0 || !HasInputRecipe)
            return false;

        try
        {
            long units = workerUnits;
            foreach (KeyValuePair<string, int> required in buildingType.requireItems)
            {
                if (string.IsNullOrWhiteSpace(required.Key) || required.Value <= 0)
                    return false;

                _inputInventory.TryGetValue(required.Key, out long available);
                units = Math.Min(units, available / required.Value);
            }

            if (units <= 0)
                return true;

            int nextVersion = checked(_inputInventoryVersion + 1);
            var remaining = new Dictionary<string, long>(_inputInventory, StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> required in buildingType.requireItems)
            {
                long used = checked((long)required.Value * units);
                long next = remaining[required.Key] - used;
                if (next == 0)
                    remaining.Remove(required.Key);
                else
                    remaining[required.Key] = next;
            }

            completeUnits = units;
            consumption = new PreparedInputConsumption(
                this, _inputInventoryVersion, nextVersion, remaining);
            return true;
        }
        catch (OverflowException)
        {
            completeUnits = 0;
            consumption = null;
            return false;
        }
        catch (KeyNotFoundException)
        {
            completeUnits = 0;
            consumption = null;
            return false;
        }
    }

    private bool IsKnownInput(string productName) =>
        !string.IsNullOrWhiteSpace(productName) &&
        buildingType?.requireItems != null &&
        buildingType.requireItems.TryGetValue(productName, out int required) &&
        required > 0;

    private void CommitPreparedInputReceipt(PreparedInputReceipt receipt)
    {
        if (!ReferenceEquals(_preparedInputReceipt, receipt) ||
            receipt.Version != _inputInventoryVersion)
        {
            return;
        }

        ReplaceInputInventory(receipt.NextInventory);
        _inputInventoryVersion = receipt.NextVersion;
        _preparedInputReceipt = null;
    }

    private bool CommitPreparedInputConsumption(PreparedInputConsumption consumption)
    {
        if (consumption.Version != _inputInventoryVersion)
            return false;

        ReplaceInputInventory(consumption.NextInventory);
        _inputInventoryVersion = consumption.NextVersion;
        _preparedInputReceipt = null;
        return true;
    }

    private void ReplaceInputInventory(IReadOnlyDictionary<string, long> next)
    {
        _inputInventory.Clear();
        foreach (KeyValuePair<string, long> quantity in next)
            if (quantity.Value > 0)
                _inputInventory[quantity.Key] = quantity.Value;
    }

    private sealed class PreparedInputReceipt : IPreparedMarketReceipt
    {
        private readonly Building _owner;
        private bool _finished;

        internal int Version { get; }
        internal int NextVersion { get; }
        internal IReadOnlyDictionary<string, long> NextInventory { get; }

        internal PreparedInputReceipt(
            Building owner,
            int version,
            int nextVersion,
            IReadOnlyDictionary<string, long> nextInventory)
        {
            _owner = owner;
            Version = version;
            NextVersion = nextVersion;
            NextInventory = nextInventory;
        }

        public void Commit()
        {
            if (_finished)
                return;

            _finished = true;
            _owner.CommitPreparedInputReceipt(this);
        }
    }

    internal sealed class PreparedInputConsumption
    {
        private readonly Building _owner;
        private bool _finished;

        internal int Version { get; }
        internal int NextVersion { get; }
        internal IReadOnlyDictionary<string, long> NextInventory { get; }

        internal PreparedInputConsumption(
            Building owner,
            int version,
            int nextVersion,
            IReadOnlyDictionary<string, long> nextInventory)
        {
            _owner = owner;
            Version = version;
            NextVersion = nextVersion;
            NextInventory = nextInventory;
        }

        internal bool Commit()
        {
            if (_finished)
                return false;

            _finished = true;
            return _owner.CommitPreparedInputConsumption(this);
        }
    }
}
