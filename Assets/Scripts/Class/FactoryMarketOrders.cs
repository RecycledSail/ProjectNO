using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public static class FactoryMarketOrders
{
    public static IReadOnlyList<MarketOrder> Collect(
        Province province,
        MarketAccessContext access,
        bool payrollPaid,
        IDictionary<MoneyAccount, long> remainingBudgets)
        => Collect(province, access, payrollPaid, remainingBudgets, GlobalVariables.MARKET_PRICE_SETTINGS);

    public static IReadOnlyList<MarketOrder> Collect(
        Province province, MarketAccessContext access, bool payrollPaid,
        IDictionary<MoneyAccount, long> remainingBudgets, MarketPriceSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (province == null) throw new ArgumentNullException(nameof(province));
        if (access.Products == null || access.Ledger == null)
            throw new ArgumentException("A complete market access context is required.", nameof(access));
        if (remainingBudgets == null) throw new ArgumentNullException(nameof(remainingBudgets));

        ValidateProducts(access.Products);
        if (!payrollPaid || province.buildings == null || !ReferenceEquals(province.ActiveLedger, access.Ledger))
            return Array.Empty<MarketOrder>();

        List<Building> participants = province.buildings.Values
            .Where(candidate => candidate != null && CanSubmit(candidate, province, access.Ledger))
            .OrderBy(candidate => candidate.Account.Id, StringComparer.Ordinal)
            .ToList();
        if (!HasUniqueParticipants(participants))
            return Array.Empty<MarketOrder>();

        List<OrderDraft> drafts = new();
        Dictionary<MoneyAccount, long> nextBudgets = new();
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        HashSet<string> recipientIds = new(StringComparer.Ordinal);
        foreach (Building building in participants)
        {
            if (!remainingBudgets.TryGetValue(building.Account, out long availableBudget) ||
                availableBudget < 0 || availableBudget > building.Account.Balance)
            {
                continue;
            }

            List<InputPlan> inputs = PlanInputs(building, access.Products);
            if (inputs.Count == 0 || availableBudget == 0)
                continue;

            BigInteger totalWeight = inputs.Aggregate(BigInteger.Zero,
                (total, input) => total + input.ReferenceCost);
            if (totalWeight <= 0)
                continue;

            FactoryRecipient recipient = new(building);
            long reserved = 0;
            int draftCount = drafts.Count;
            foreach (InputPlan input in inputs.OrderBy(item => item.Product.ProductName, StringComparer.Ordinal))
            {
                long allocation = FloorShare(availableBudget, input.ReferenceCost, totalWeight);
                long quantity = Math.Min(input.Shortfall, allocation / input.Product.Price);
                quantity = Math.Min(quantity, int.MaxValue);
                if (quantity <= 0)
                    continue;

                int maximumUnitPrice = MarketPriceCalculator.CalculateMaximumBid(
                    input.Product.Price, (int)quantity, allocation, settings);
                long orderBudget = checked(quantity * maximumUnitPrice);
                string orderId = OrderId(building.Account.Id, input.Product.ProductName);
                if (!orderIds.Add(orderId))
                    return Array.Empty<MarketOrder>();
                drafts.Add(new OrderDraft(orderId, building.Account, input.Product,
                    checked((int)quantity), maximumUnitPrice, orderBudget, recipient));
                reserved = checked(reserved + orderBudget);
            }
            if (drafts.Count > draftCount && !recipientIds.Add(recipient.Id))
                return Array.Empty<MarketOrder>();
            nextBudgets.Add(building.Account, checked(availableBudget - reserved));
        }

        List<MarketOrder> orders = new();
        foreach (OrderDraft draft in drafts)
        {
            MarketOrder order = new(draft.Id, draft.Buyer, draft.Product, draft.Quantity,
                draft.MaximumUnitPrice, draft.ReservedBudget, 1, draft.Recipient);
            draft.Recipient.AddOrder(order);
            orders.Add(order);
        }
        foreach (KeyValuePair<MoneyAccount, long> budget in nextBudgets)
            remainingBudgets[budget.Key] = budget.Value;
        return orders.AsReadOnly();
    }

    private static bool HasUniqueParticipants(IReadOnlyList<Building> participants)
    {
        HashSet<Building> buildings = new();
        HashSet<MoneyAccount> accounts = new();
        HashSet<string> accountIds = new(StringComparer.Ordinal);
        foreach (Building participant in participants)
        {
            if (!buildings.Add(participant) || participant.Account == null ||
                string.IsNullOrWhiteSpace(participant.Account.Id) ||
                !accounts.Add(participant.Account) || !accountIds.Add(participant.Account.Id))
            {
                return false;
            }
        }
        return true;
    }

    private static bool CanSubmit(Building building, Province province, MoneyLedger ledger)
    {
        if (building is ConstructionCompanyBuilding || !ReferenceEquals(building.province, province) ||
            building.Account == null || !ledger.OwnsAccount(building.Account) || building.level <= 0 ||
            building.currentWorkers <= 0 || building.buildingType?.workerNeeded <= 0 ||
            building.buildingType.produceItems == null ||
            !building.buildingType.produceItems.Any(item => item.Value > 0) ||
            building.buildingType.requireItems == null || building.buildingType.requireItems.Count == 0)
        {
            return false;
        }

        return building.buildingType.requireItems.All(item =>
            !string.IsNullOrWhiteSpace(item.Key) && item.Value > 0);
    }

    private static List<InputPlan> PlanInputs(Building building, Dictionary<string, ProductState> products)
    {
        var inputs = new List<InputPlan>();
        try
        {
            long units = building.currentWorkers / building.buildingType.workerNeeded;
            if (units <= 0)
                return inputs;

            foreach (KeyValuePair<string, int> required in building.buildingType.requireItems)
            {
                if (!products.TryGetValue(required.Key, out ProductState product) || product.Price <= 0)
                    continue;

                long target = checked(units * required.Value);
                building.InputInventory.TryGetValue(required.Key, out long held);
                if (held < 0)
                    return new List<InputPlan>();
                long shortfall = target > held ? target - held : 0L;
                if (shortfall == 0)
                    continue;

                inputs.Add(new InputPlan(product, shortfall,
                    (BigInteger)shortfall * product.Price));
            }
        }
        catch (OverflowException)
        {
            return new List<InputPlan>();
        }

        return inputs;
    }

    private static void ValidateProducts(Dictionary<string, ProductState> products)
    {
        HashSet<ProductState> canonical = new();
        foreach (KeyValuePair<string, ProductState> item in products)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null ||
                !string.Equals(item.Key, item.Value.ProductName, StringComparison.Ordinal) ||
                !canonical.Add(item.Value))
            {
                throw new ArgumentException("Market product mappings must be unique and canonical.", nameof(products));
            }
        }
    }

    private static long FloorShare(long budget, BigInteger weight, BigInteger totalWeight) =>
        (long)((BigInteger)budget * weight / totalWeight);

    private static string OrderId(string accountId, string productName) =>
        $"factory:{Escape(accountId)}{Escape(productName)}";

    private static string Escape(string value) => $"{value.Length}:{value}";

    private sealed class InputPlan
    {
        public ProductState Product { get; }
        public long Shortfall { get; }
        public BigInteger ReferenceCost { get; }

        public InputPlan(ProductState product, long shortfall, BigInteger referenceCost)
        {
            Product = product;
            Shortfall = shortfall;
            ReferenceCost = referenceCost;
        }
    }

    private sealed class OrderDraft
    {
        public string Id { get; }
        public MoneyAccount Buyer { get; }
        public ProductState Product { get; }
        public int Quantity { get; }
        public int MaximumUnitPrice { get; }
        public long ReservedBudget { get; }
        public FactoryRecipient Recipient { get; }

        public OrderDraft(string id, MoneyAccount buyer, ProductState product, int quantity,
            int maximumUnitPrice, long reservedBudget, FactoryRecipient recipient)
        {
            Id = id;
            Buyer = buyer;
            Product = product;
            Quantity = quantity;
            MaximumUnitPrice = maximumUnitPrice;
            ReservedBudget = reservedBudget;
            Recipient = recipient;
        }
    }

    private sealed class FactoryRecipient : IMarketOrderRecipient
    {
        private readonly Building building;
        private readonly HashSet<MarketOrder> orders = new();
        public string Id { get; }

        public FactoryRecipient(Building building)
        {
            this.building = building;
            Id = "factory-recipient:" + Escape(building.Account.Id);
        }

        internal void AddOrder(MarketOrder order) => orders.Add(order);

        public bool TryPrepareReceipt(IReadOnlyList<MarketOrderFill> fills,
            out IPreparedMarketReceipt receipt)
        {
            receipt = null;
            if (fills == null)
                return false;

            try
            {
                Dictionary<string, long> quantities = new(StringComparer.Ordinal);
                foreach (MarketOrderFill fill in fills)
                {
                    if (fill == null || !ReferenceEquals(fill.Order.Recipient, this) ||
                        !ReferenceEquals(fill.Order.Buyer, building.Account) || !orders.Contains(fill.Order) ||
                        fill.Order.Product == null || string.IsNullOrWhiteSpace(fill.Order.Product.ProductName))
                    {
                        return false;
                    }

                    string productName = fill.Order.Product.ProductName;
                    quantities.TryGetValue(productName, out long quantity);
                    quantities[productName] = checked(quantity + fill.Quantity);
                }

                return building.TryPrepareInputReceipt(quantities, out receipt);
            }
            catch (OverflowException)
            {
                receipt = null;
                return false;
            }
        }
    }
}
