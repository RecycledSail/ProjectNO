using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MarketProductClearingResult
{
    public ProductState Product { get; }
    public int RequestedDemand { get; }
    public int SoldQuantity { get; }
    public int AvailableStock { get; }
    public int ClearingPrice { get; }
    public int NextPrice { get; }

    internal MarketProductClearingResult(ProductState product, int requestedDemand, int soldQuantity,
        int availableStock, int clearingPrice, int nextPrice)
    {
        Product = product;
        RequestedDemand = requestedDemand;
        SoldQuantity = soldQuantity;
        AvailableStock = availableStock;
        ClearingPrice = clearingPrice;
        NextPrice = nextPrice;
    }
}

public sealed class MarketClearingPlan
{
    private readonly IReadOnlyList<IMarketOrderRecipient> _recipients;
    internal MarketPriceSettings Settings { get; }
    public IReadOnlyList<MarketBuyerRequest> Purchases { get; }
    public IReadOnlyList<MarketOrderFill> Fills { get; }
    public IReadOnlyList<MarketProductClearingResult> ProductResults { get; }

    internal MarketClearingPlan(IEnumerable<MarketOrderFill> fills,
        IEnumerable<MarketProductClearingResult> productResults,
        IEnumerable<IMarketOrderRecipient> recipients, MarketPriceSettings settings)
    {
        Fills = fills.ToList().AsReadOnly();
        ProductResults = productResults.ToList().AsReadOnly();
        _recipients = recipients.ToList().AsReadOnly();
        Settings = settings;
        Purchases = Fills.Select(fill => new MarketBuyerRequest(
            fill.Order.Id, fill.Order.Buyer, fill.Order.Product, fill.Quantity)).ToList().AsReadOnly();
    }

    public bool PrepareReceipts(IEnumerable<IMarketOrderRecipient> additionalRecipients,
        out IReadOnlyList<IPreparedMarketReceipt> receipts)
    {
        receipts = null;
        if (additionalRecipients == null) return false;

        Dictionary<string, IMarketOrderRecipient> recipients = new(StringComparer.Ordinal);
        foreach (IMarketOrderRecipient recipient in _recipients.Concat(additionalRecipients))
        {
            if (recipient == null || string.IsNullOrWhiteSpace(recipient.Id)) return false;
            if (recipients.TryGetValue(recipient.Id, out IMarketOrderRecipient existing) &&
                !ReferenceEquals(existing, recipient)) return false;
            recipients[recipient.Id] = recipient;
        }

        List<IPreparedMarketReceipt> prepared = new();
        foreach (IMarketOrderRecipient recipient in recipients.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            IReadOnlyList<MarketOrderFill> fills = Fills
                .Where(fill => ReferenceEquals(fill.Order.Recipient, recipient)).ToList().AsReadOnly();
            if (!recipient.TryPrepareReceipt(fills, out IPreparedMarketReceipt receipt) || receipt == null)
                return false;
            prepared.Add(receipt);
        }
        receipts = prepared.AsReadOnly();
        return true;
    }
}

public static class MarketClearingEngine
{
    public static bool TryPlan(IReadOnlyList<MarketOrder> orders,
        IReadOnlyCollection<ProductState> marketProducts, MoneyLedger ledger,
        MarketPriceSettings settings, out MarketClearingPlan plan, out string error)
    {
        plan = null;
        error = null;
        if (orders == null || marketProducts == null || ledger == null || settings == null)
            return Fail("Orders, market products, ledger and price settings are required.", out error);

        try
        {
            Dictionary<ProductState, List<MarketOrder>> eligible = new();
            Dictionary<ProductState, int> demand = new();
            Dictionary<ProductState, int> stock = new();
            HashSet<string> productNames = new(StringComparer.Ordinal);
            foreach (ProductState product in marketProducts)
            {
                if (product == null || eligible.ContainsKey(product) ||
                    string.IsNullOrWhiteSpace(product.ProductName) || !productNames.Add(product.ProductName))
                    return Fail("Every market product and product name must occur exactly once.", out error);
                if (product.Price < 1 || float.IsNaN(product.Elasticity) ||
                    float.IsInfinity(product.Elasticity) || product.Elasticity < 0)
                    return Fail("A product has an invalid reference price or elasticity.", out error);
                foreach (MoneyAccount supplier in product.Inventory.Lots.Keys)
                    if (!ledger.OwnsAccount(supplier))
                        return Fail("Every inventory supplier must belong to the supplied ledger.", out error);
                eligible.Add(product, new List<MarketOrder>());
                demand.Add(product, 0);
                stock.Add(product, product.Stock);
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            Dictionary<MoneyAccount, long> reservations = new();
            Dictionary<string, IMarketOrderRecipient> recipients = new(StringComparer.Ordinal);
            foreach (MarketOrder order in orders)
            {
                if (order == null || !ids.Add(order.Id))
                    return Fail("Orders must be non-null with unique ids.", out error);
                if (!eligible.ContainsKey(order.Product))
                    return Fail("Every order product must belong to the supplied market exactly once.", out error);
                if (!ledger.OwnsAccount(order.Buyer))
                    return Fail("Every buyer must belong to the supplied ledger.", out error);
                if (string.IsNullOrWhiteSpace(order.Recipient.Id) ||
                    (recipients.TryGetValue(order.Recipient.Id, out IMarketOrderRecipient existing) &&
                     !ReferenceEquals(existing, order.Recipient)))
                    return Fail("Recipient ids must identify one recipient each.", out error);
                recipients[order.Recipient.Id] = order.Recipient;
                reservations.TryGetValue(order.Buyer, out long reserved);
                reservations[order.Buyer] = checked(reserved + order.ReservedBudget);
                if (order.MaximumUnitPrice < order.Product.Price) continue;
                eligible[order.Product].Add(order);
                demand[order.Product] = checked(demand[order.Product] + order.Quantity);
            }

            foreach (KeyValuePair<MoneyAccount, long> reservation in reservations)
                if (reservation.Value > reservation.Key.Balance)
                    return Fail("Aggregate reserved budget exceeds the buyer's starting balance.", out error);

            List<MarketOrderFill> fills = new();
            List<MarketProductClearingResult> results = new();
            foreach (ProductState product in eligible.Keys.OrderBy(p => p.ProductName, StringComparer.Ordinal))
            {
                List<MarketOrder> productOrders = eligible[product];
                int available = stock[product];
                Dictionary<MarketOrder, int> quantities = new();
                int clearingPrice = product.Price;
                if (available >= demand[product])
                {
                    // Supply covers eligible demand: no bid sorting is needed.
                    foreach (MarketOrder order in productOrders) quantities.Add(order, order.Quantity);
                }
                else if (available > 0)
                {
                    productOrders.Sort((left, right) =>
                    {
                        int price = right.MaximumUnitPrice.CompareTo(left.MaximumUnitPrice);
                        return price != 0 ? price : string.CompareOrdinal(left.Id, right.Id);
                    });
                    int remaining = available;
                    int index = 0;
                    while (remaining > 0 && index < productOrders.Count)
                    {
                        int end = index;
                        int groupDemand = 0;
                        clearingPrice = productOrders[index].MaximumUnitPrice;
                        while (end < productOrders.Count && productOrders[end].MaximumUnitPrice == clearingPrice)
                            groupDemand = checked(groupDemand + productOrders[end++].Quantity);
                        if (groupDemand <= remaining)
                        {
                            for (int i = index; i < end; i++) quantities.Add(productOrders[i], productOrders[i].Quantity);
                            remaining -= groupDemand;
                        }
                        else
                        {
                            AllocateBoundary(productOrders.GetRange(index, end - index), remaining, quantities);
                            break;
                        }
                        index = end;
                    }
                }

                int sold = 0;
                foreach (KeyValuePair<MarketOrder, int> quantity in quantities)
                {
                    fills.Add(new MarketOrderFill(quantity.Key, quantity.Value, clearingPrice));
                    sold = checked(sold + quantity.Value);
                }
                int nextPrice = MarketPriceCalculator.CalculateNextPrice(
                    product.Price, demand[product], available, product.Elasticity, settings);
                results.Add(new MarketProductClearingResult(product, demand[product], sold,
                    available, clearingPrice, nextPrice));
            }

            // Canonical presentation order is separate from bid priority sorting.
            fills.Sort((left, right) => string.CompareOrdinal(left.Order.Id, right.Order.Id));
            plan = new MarketClearingPlan(fills, results,
                recipients.Values.OrderBy(r => r.Id, StringComparer.Ordinal), settings);
            return true;
        }
        catch (OverflowException)
        {
            return Fail("Market demand, reserved budget or price arithmetic overflow.", out error);
        }
        catch (ArgumentException exception)
        {
            return Fail(exception.Message, out error);
        }
    }

    private static void AllocateBoundary(List<MarketOrder> boundary, int available,
        IDictionary<MarketOrder, int> quantities)
    {
        while (boundary.Count > 0)
        {
            int requested = 0;
            foreach (MarketOrder order in boundary) requested = checked(requested + order.Quantity);
            Dictionary<MarketOrder, long> shares = ProportionalAllocator.Allocate(
                Math.Min(available, requested), boundary.ToDictionary(o => o, o => (long)o.Quantity), o => o.Id);
            int removed = boundary.RemoveAll(order => shares[order] < order.MinimumFill);
            if (removed > 0) continue;
            foreach (MarketOrder order in boundary) quantities.Add(order, checked((int)shares[order]));
            return;
        }
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
