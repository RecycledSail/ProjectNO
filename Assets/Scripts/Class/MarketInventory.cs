using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class SupplierSale
{
    public MoneyAccount Supplier { get; }
    public int Quantity { get; }

    public SupplierSale(MoneyAccount supplier, int quantity)
    {
        if (supplier == null) throw new ArgumentNullException(nameof(supplier));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Supplier = supplier;
        Quantity = quantity;
    }
}

public sealed class ProductInventory
{
    private readonly Dictionary<MoneyAccount, int> _lots = new();
    private readonly ReadOnlyDictionary<MoneyAccount, int> _readOnlyLots;
    private int _totalQuantity;

    public ProductInventory()
    {
        _readOnlyLots = new ReadOnlyDictionary<MoneyAccount, int>(_lots);
    }

    public IReadOnlyDictionary<MoneyAccount, int> Lots => _readOnlyLots;
    public int TotalQuantity => _totalQuantity;

    internal void Add(MoneyAccount supplier, int amount)
    {
        ValidateAddition(supplier, amount);
        _lots.TryGetValue(supplier, out int current);
        int next = checked(current + amount);
        int nextTotal = checked(_totalQuantity + amount);
        _lots[supplier] = next;
        _totalQuantity = nextTotal;
    }

    internal IReadOnlyList<SupplierSale> PlanSale(int amount)
    {
        int requested = Math.Clamp(amount, 0, _totalQuantity);
        if (requested == 0)
            return Array.Empty<SupplierSale>();

        Dictionary<MoneyAccount, long> weights = _lots.ToDictionary(
            pair => pair.Key,
            pair => (long)pair.Value);
        Dictionary<MoneyAccount, long> allocated = ProportionalAllocator.Allocate(
            requested,
            weights,
            supplier => supplier.Id);

        return allocated
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key.Id, StringComparer.Ordinal)
            .Select(pair => new SupplierSale(pair.Key, checked((int)pair.Value)))
            .ToList();
    }

    internal void CommitSale(IReadOnlyList<SupplierSale> sale)
    {
        if (sale == null) throw new ArgumentNullException(nameof(sale));

        int totalRemoved = 0;
        foreach (SupplierSale entry in sale)
        {
            if (entry == null) throw new ArgumentException("Sale entries cannot be null.", nameof(sale));
            if (entry.Supplier == null || entry.Quantity <= 0)
                throw new ArgumentException("Sale entries must have a supplier and positive quantity.", nameof(sale));

            totalRemoved = checked(totalRemoved + entry.Quantity);
        }

        IReadOnlyList<SupplierSale> expected = PlanSale(totalRemoved);
        if (sale.Count != expected.Count)
            throw new InvalidOperationException("Sale must match the current proportional allocation.");

        for (int index = 0; index < sale.Count; index++)
        {
            if (!ReferenceEquals(sale[index].Supplier, expected[index].Supplier) ||
                sale[index].Quantity != expected[index].Quantity)
            {
                throw new InvalidOperationException("Sale must match the current proportional allocation.");
            }
        }

        foreach (SupplierSale entry in expected)
        {
            int remaining = _lots[entry.Supplier] - entry.Quantity;
            if (remaining == 0)
                _lots.Remove(entry.Supplier);
            else
                _lots[entry.Supplier] = remaining;
        }

        _totalQuantity -= totalRemoved;
    }

    internal void ValidateCanReceive(IReadOnlyDictionary<MoneyAccount, int> incoming)
    {
        if (incoming == null) throw new ArgumentNullException(nameof(incoming));

        int nextTotal = _totalQuantity;
        foreach (KeyValuePair<MoneyAccount, int> lot in incoming)
        {
            ValidateAddition(lot.Key, lot.Value);
            _lots.TryGetValue(lot.Key, out int current);
            _ = checked(current + lot.Value);
            nextTotal = checked(nextTotal + lot.Value);
        }
    }

    internal void Clear()
    {
        _lots.Clear();
        _totalQuantity = 0;
    }

    private void ValidateAddition(MoneyAccount supplier, int amount)
    {
        if (supplier == null) throw new ArgumentNullException(nameof(supplier));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        foreach (MoneyAccount existingSupplier in _lots.Keys)
        {
            if (!ReferenceEquals(existingSupplier, supplier) &&
                string.Equals(existingSupplier.Id, supplier.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("Supplier account ids must be unique within inventory.", nameof(supplier));
            }
        }
    }
}
