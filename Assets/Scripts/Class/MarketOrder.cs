using System;
using System.Collections.Generic;

public interface IMarketOrderRecipient
{
    string Id { get; }

    // Preparation must validate the receipt without changing recipient state.
    bool TryPrepareReceipt(IReadOnlyList<MarketOrderFill> fills, out IPreparedMarketReceipt receipt);
}

public interface IPreparedMarketReceipt
{
    // Successful preparation guarantees that Commit will not throw.
    void Commit();
}

public sealed class MarketOrder
{
    public string Id { get; }
    public MoneyAccount Buyer { get; }
    public ProductState Product { get; }
    public int Quantity { get; }
    public int MaximumUnitPrice { get; }
    public long ReservedBudget { get; }
    public int MinimumFill { get; }
    public IMarketOrderRecipient Recipient { get; }

    public MarketOrder(string id, MoneyAccount buyer, ProductState product, int quantity,
        int maximumUnitPrice, long reservedBudget, int minimumFill, IMarketOrderRecipient recipient)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An order id is required.", nameof(id));
        Buyer = buyer ?? throw new ArgumentNullException(nameof(buyer));
        Product = product ?? throw new ArgumentNullException(nameof(product));
        Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
        if (string.IsNullOrWhiteSpace(recipient.Id))
            throw new ArgumentException("A recipient id is required.", nameof(recipient));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (maximumUnitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(maximumUnitPrice));
        if (reservedBudget <= 0) throw new ArgumentOutOfRangeException(nameof(reservedBudget));
        if (minimumFill <= 0 || minimumFill > quantity) throw new ArgumentOutOfRangeException(nameof(minimumFill));
        if (checked((long)quantity * maximumUnitPrice) > reservedBudget)
            throw new ArgumentException("The reserved budget must cover the full order at its maximum price.", nameof(reservedBudget));
        Id = id;
        Quantity = quantity;
        MaximumUnitPrice = maximumUnitPrice;
        ReservedBudget = reservedBudget;
        MinimumFill = minimumFill;
    }
}

public sealed class MarketOrderFill
{
    public MarketOrder Order { get; }
    public int Quantity { get; }
    public int UnitPrice { get; }
    public long GrossAmount => checked((long)Quantity * UnitPrice);

    public MarketOrderFill(MarketOrder order, int quantity, int unitPrice)
    {
        Order = order ?? throw new ArgumentNullException(nameof(order));
        if (quantity < order.MinimumFill || quantity > order.Quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice <= 0 || unitPrice > order.MaximumUnitPrice)
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
