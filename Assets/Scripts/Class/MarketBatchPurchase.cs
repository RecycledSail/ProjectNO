using System;

public sealed class MarketBuyerRequest
{
    public string RequestId { get; }
    public MoneyAccount Buyer { get; }
    public ProductState Product { get; }
    public int Quantity { get; }

    public MarketBuyerRequest(
        string requestId,
        MoneyAccount buyer,
        ProductState product,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException(nameof(requestId));
        RequestId = requestId;
        Buyer = buyer ?? throw new ArgumentNullException(nameof(buyer));
        Product = product ?? throw new ArgumentNullException(nameof(product));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
    }
}
