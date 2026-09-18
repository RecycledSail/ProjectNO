using System.Collections.Generic;

public readonly struct MarketAccessContext
{
    public string StableId { get; }
    public Dictionary<string, ProductState> Products { get; }
    public MoneyLedger Ledger { get; }

    public MarketAccessContext(
        string stableId,
        Dictionary<string, ProductState> products,
        MoneyLedger ledger)
    {
        StableId = stableId;
        Products = products;
        Ledger = ledger;
    }
}

public static class MarketAccess
{
    public static bool TryResolve(Province province, out MarketAccessContext context)
    {
        context = default;
        if (province?.ActiveLedger == null)
            return false;

        if (province.isConnectedToCapital)
        {
            if (province.nation?.market?.Products == null)
                return false;

            context = new MarketAccessContext(
                "nation:" + province.nation.name,
                province.nation.market.Products,
                province.ActiveLedger);
            return true;
        }

        if (province.market?.Products == null)
            return false;

        context = new MarketAccessContext(
            "province:" + province.name,
            province.market.Products,
            province.ActiveLedger);
        return true;
    }
}
