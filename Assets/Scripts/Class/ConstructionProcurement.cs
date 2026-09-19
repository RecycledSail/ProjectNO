using System;
using System.Collections.Generic;
using System.Numerics;

public static class ConstructionProcurement
{
    public static IReadOnlyList<MarketOrder> CollectOrders(
        IReadOnlyList<ConstructionMandate> mandates,
        MarketAccessContext access,
        IDictionary<MoneyAccount, long> remainingBudgets)
    {
        if (mandates == null) throw new ArgumentNullException(nameof(mandates));
        if (access.Products == null || access.Ledger == null)
            throw new ArgumentException("A complete market access context is required.", nameof(access));
        if (remainingBudgets == null) throw new ArgumentNullException(nameof(remainingBudgets));

        ValidateProducts(access.Products);
        HashSet<string> ids = new(StringComparer.Ordinal);
        Dictionary<MoneyAccount, BuyerPlan> buyers = new();
        foreach (ConstructionMandate mandate in mandates)
        {
            if (mandate == null || !mandate.IsActive || string.IsNullOrWhiteSpace(mandate.Id) ||
                !ids.Add(mandate.Id) || !ReferenceEquals(mandate.TargetProvince.ActiveLedger, access.Ledger) ||
                !ReferenceEquals(mandate.GetAccessibleProducts(), access.Products) ||
                !access.Ledger.OwnsAccount(mandate.Investor?.InvestmentAccount) ||
                (mandate.EscrowAccount != null && !access.Ledger.OwnsAccount(mandate.EscrowAccount)))
                throw new ArgumentException("Every mandate must be an active, unique participant in the supplied market.", nameof(mandates));

            MoneyAccount buyer = mandate.Investor.InvestmentAccount;
            if (!remainingBudgets.TryGetValue(buyer, out long remainingBudget) ||
                remainingBudget < 0 || remainingBudget > buyer.Balance)
                throw new ArgumentException("Every investor requires a valid remaining budget.", nameof(remainingBudgets));

            if (!buyers.TryGetValue(buyer, out BuyerPlan buyerPlan))
            {
                buyerPlan = new BuyerPlan(buyer, remainingBudget);
                buyers.Add(buyer, buyerPlan);
            }
            else if (buyerPlan.StartingBudget != remainingBudget)
            {
                throw new ArgumentException("One investor must have one starting remaining budget.", nameof(remainingBudgets));
            }

            ProjectPlan project = new(mandate);
            foreach (KeyValuePair<string, long> material in mandate.RequiredMaterials)
            {
                if (!mandate.AcquiredMaterials.TryGetValue(material.Key, out long acquired) ||
                    material.Value <= 0 || acquired < 0 || acquired > material.Value)
                    throw new ArgumentException("A mandate contains invalid material state.", nameof(mandates));

                long remaining = material.Value - acquired;
                if (remaining == 0 || !access.Products.TryGetValue(material.Key, out ProductState product))
                    continue;
                if (product.Price <= 0)
                    continue;

                long valuation = checked(remaining * product.Price);
                ItemPlan item = new(project, product, remaining, valuation);
                buyerPlan.Items.Add(item);
                buyerPlan.Valuation = checked(buyerPlan.Valuation + valuation);
            }
        }

        List<MarketOrder> orders = new();
        Dictionary<MoneyAccount, long> nextBudgets = new();
        foreach (BuyerPlan buyer in buyers.Values)
        {
            long reserved = 0;
            if (buyer.Valuation > 0 && buyer.StartingBudget > 0)
            {
                foreach (ItemPlan item in buyer.Items)
                {
                    long weightedBudget = FloorShare(buyer.StartingBudget, item.Valuation, buyer.Valuation);
                    long quantity = Math.Min(item.Remaining, weightedBudget / item.Product.Price);
                    quantity = Math.Min(quantity, int.MaxValue);
                    if (quantity == 0)
                        continue;

                    long priceCap = Math.Min(int.MaxValue,
                        checked((checked((long)item.Product.Price * 5L) + 3L) / 4L));
                    int maximumUnitPrice = checked((int)Math.Min(weightedBudget / quantity, priceCap));
                    long orderBudget = checked(quantity * maximumUnitPrice);
                    orders.Add(new MarketOrder(OrderId(item.Project.Mandate.Id, item.Product.ProductName),
                        buyer.Account, item.Product, checked((int)quantity), maximumUnitPrice,
                        orderBudget, 1, item.Project.Recipient));
                    reserved = checked(reserved + orderBudget);
                }
            }
            nextBudgets.Add(buyer.Account, checked(buyer.StartingBudget - reserved));
        }

        foreach (KeyValuePair<MoneyAccount, long> budget in nextBudgets)
            remainingBudgets[budget.Key] = budget.Value;
        return orders.AsReadOnly();
    }

    public static bool TryProcessMarket(IReadOnlyList<ConstructionMandate> mandates,
        Dictionary<string, ProductState> products, MoneyLedger ledger)
    {
        if (mandates == null || products == null || ledger == null ||
            !ledger.OwnsAccount(ledger.TreasuryAccount))
            return false;

        try
        {
            Dictionary<MoneyAccount, long> remainingBudgets = new();
            foreach (ConstructionMandate mandate in mandates)
            {
                MoneyAccount account = mandate?.Investor?.InvestmentAccount;
                if (account != null && !remainingBudgets.ContainsKey(account))
                    remainingBudgets.Add(account, account.Balance);
            }
            MarketAccessContext access = new("construction", products, ledger);
            IReadOnlyList<MarketOrder> orders = CollectOrders(mandates, access, remainingBudgets);
            if (orders.Count == 0)
                return true;
            List<ProductState> orderedProducts = new();
            HashSet<ProductState> seenProducts = new();
            foreach (MarketOrder order in orders)
                if (seenProducts.Add(order.Product))
                    orderedProducts.Add(order.Product);
            if (!MarketClearingEngine.TryPlan(orders, orderedProducts, ledger,
                    GlobalVariables.MARKET_PRICE_SETTINGS, out MarketClearingPlan plan, out _))
                return false;
            return plan.TrySettle(ledger, Array.Empty<IMarketOrderRecipient>(), out _);
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

    private static void ValidateProducts(Dictionary<string, ProductState> products)
    {
        HashSet<ProductState> mappedProducts = new();
        foreach (KeyValuePair<string, ProductState> item in products)
            if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null ||
                !string.Equals(item.Key, item.Value.ProductName, StringComparison.Ordinal) ||
                !mappedProducts.Add(item.Value))
                throw new ArgumentException("Market product mappings must be unique and canonical.", nameof(products));
    }

    private static string OrderId(string projectId, string productName) =>
        $"{projectId.Length}:{projectId}{productName.Length}:{productName}";

    private static long FloorShare(long total, long weight, long totalWeight) =>
        (long)((BigInteger)total * weight / totalWeight);

    private sealed class BuyerPlan
    {
        public readonly MoneyAccount Account;
        public readonly long StartingBudget;
        public readonly List<ItemPlan> Items = new();
        public long Valuation;

        public BuyerPlan(MoneyAccount account, long startingBudget)
        {
            Account = account;
            StartingBudget = startingBudget;
        }
    }

    private sealed class ProjectPlan
    {
        public readonly ConstructionMandate Mandate;
        public readonly ProjectRecipient Recipient;

        public ProjectPlan(ConstructionMandate mandate)
        {
            Mandate = mandate;
            Recipient = new ProjectRecipient(mandate);
        }
    }

    private sealed class ItemPlan
    {
        public readonly ProjectPlan Project;
        public readonly ProductState Product;
        public readonly long Remaining;
        public readonly long Valuation;

        public ItemPlan(ProjectPlan project, ProductState product, long remaining, long valuation)
        {
            Project = project;
            Product = product;
            Remaining = remaining;
            Valuation = valuation;
        }
    }

    private sealed class ProjectRecipient : IMarketOrderRecipient
    {
        private readonly ConstructionMandate _mandate;
        public string Id { get; }

        public ProjectRecipient(ConstructionMandate mandate)
        {
            _mandate = mandate;
            Id = $"construction:{mandate.Id.Length}:{mandate.Id}";
        }

        public bool TryPrepareReceipt(IReadOnlyList<MarketOrderFill> fills,
            out IPreparedMarketReceipt receipt)
        {
            receipt = null;
            if (fills == null)
                return false;
            try
            {
                Dictionary<string, long> quantities = new(StringComparer.Ordinal);
                long spending = 0;
                foreach (MarketOrderFill fill in fills)
                {
                    if (fill == null || !ReferenceEquals(fill.Order.Recipient, this))
                        return false;
                    string productName = fill.Order.Product.ProductName;
                    quantities.TryGetValue(productName, out long quantity);
                    quantities[productName] = checked(quantity + fill.Quantity);
                    spending = checked(spending + fill.GrossAmount);
                }
                if (!_mandate.TryPrepareMaterialAcquisition(quantities, spending,
                        out ConstructionMaterials.Acquisition acquisition))
                    return false;
                receipt = new ProjectReceipt(_mandate, acquisition);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }

    private sealed class ProjectReceipt : IPreparedMarketReceipt
    {
        private ConstructionMandate _mandate;
        private ConstructionMaterials.Acquisition _acquisition;

        public ProjectReceipt(ConstructionMandate mandate, ConstructionMaterials.Acquisition acquisition)
        {
            _mandate = mandate;
            _acquisition = acquisition;
        }

        public void Commit()
        {
            if (_mandate == null)
                return;
            _mandate.CommitMaterialAcquisition(_acquisition);
            _mandate = null;
            _acquisition = null;
        }
    }
}
