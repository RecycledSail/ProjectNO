using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EconomicEngine : MonoBehaviour
{
    public PopulationMarketOrderBatch CollectFoodOrders(Province province,
        MarketAccessContext access, IDictionary<MoneyAccount, long> remainingBudgets)
    {
        if (province == null) throw new ArgumentNullException(nameof(province));
        if (access.Products == null || access.Ledger == null)
            throw new ArgumentException("A complete market access context is required.", nameof(access));
        if (remainingBudgets == null) throw new ArgumentNullException(nameof(remainingBudgets));

        ValidateMarketProducts(access.Products);
        if (province.provinceEthnicPops == null || !ReferenceEquals(province.ActiveLedger, access.Ledger))
            return PopulationMarketOrderBatch.Empty;

        List<ProvinceEthnicPop> populations = province.provinceEthnicPops
            .Where(population => population != null)
            .OrderBy(population => population.Account?.Id, StringComparer.Ordinal)
            .ToList();
        if (populations.Count != province.provinceEthnicPops.Count ||
            !HasUniquePopulationParticipants(populations, province, access.Ledger))
        {
            return PopulationMarketOrderBatch.Empty;
        }

        IReadOnlyList<string> foods = GlobalVariables.CATEGORIES.TryGetValue("basic_food",
            out List<string> configuredFoods)
            ? configuredFoods.Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal).ToList().AsReadOnly()
            : Array.Empty<string>();

        List<PopulationFoodRecipient> recipients = populations
            .Select(population => new PopulationFoodRecipient(population, access.StableId))
            .ToList();
        if (recipients.Select(recipient => recipient.Id).Distinct(StringComparer.Ordinal).Count() != recipients.Count)
            return PopulationMarketOrderBatch.Empty;

        List<FoodOrderDraft> drafts = new();
        Dictionary<MoneyAccount, long> nextBudgets = new();
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        for (int index = 0; index < populations.Count; index++)
        {
            ProvinceEthnicPop population = populations[index];
            MoneyAccount account = population.Account;
            if (!remainingBudgets.TryGetValue(account, out long availableBudget) ||
                availableBudget < 0 || availableBudget > account.Balance)
            {
                continue;
            }

            ProductState product = ChooseFood(foods, access.Products);
            int quantity = QuantityToOrder(population, product, availableBudget);
            if (quantity <= 0)
            {
                nextBudgets.Add(account, availableBudget);
                continue;
            }

            int maximumUnitPrice = MaximumUnitPrice(product.Price, quantity, availableBudget);
            if (maximumUnitPrice < product.Price)
            {
                nextBudgets.Add(account, availableBudget);
                continue;
            }

            long reservedBudget = checked((long)quantity * maximumUnitPrice);
            string orderId = FoodOrderId(access.StableId, account.Id, product.ProductName);
            if (!orderIds.Add(orderId))
                return PopulationMarketOrderBatch.Empty;
            FoodOrderDraft draft = new(orderId, account, product, quantity, maximumUnitPrice,
                reservedBudget, recipients[index]);
            drafts.Add(draft);
            nextBudgets.Add(account, checked(availableBudget - reservedBudget));
        }

        List<MarketOrder> orders = new();
        foreach (FoodOrderDraft draft in drafts)
        {
            MarketOrder order = new(draft.Id, draft.Buyer, draft.Product, draft.Quantity,
                draft.MaximumUnitPrice, draft.ReservedBudget, 1, draft.Recipient);
            draft.Recipient.AddOrder(order);
            orders.Add(order);
        }
        foreach (KeyValuePair<MoneyAccount, long> budget in nextBudgets)
            remainingBudgets[budget.Key] = budget.Value;
        return new PopulationMarketOrderBatch(orders, recipients);
    }

    public void ConsumeFoodsWeekly(Province province, bool isConnectedToCapital)
    {
        if (isConnectedToCapital)
            CalculateTotalFoodsNeedsFromNationMarket(province);
        else
            CalculateTotalFoodsNeeds(province);
    }

    public void CalculateTotalFoodsNeedsFromNationMarket(Province province) =>
        ConsumeFoodFromMarket(province, province?.nation?.market?.Products);

    public void CalculateTotalFoodsNeeds(Province province) =>
        ConsumeFoodFromMarket(province, province?.market?.Products);

    private void ConsumeFoodFromMarket(
        Province province,
        IDictionary<string, ProductState> market)
    {
        if (province?.provinceEthnicPops == null)
            return;

        IReadOnlyList<string> foods = GlobalVariables.CATEGORIES.TryGetValue(
            "basic_food", out List<string> products)
            ? products
            : Array.Empty<string>();

        foreach (ProvinceEthnicPop pop in province.provinceEthnicPops)
        {
            if (pop == null)
                continue;

            int purchased = PurchaseCategoryForPopulation(
                pop, foods, pop.GetNeededFood(), market);
            pop.BuyFood(purchased);
        }
    }

    private int PurchaseCategoryForPopulation(
        ProvinceEthnicPop pop,
        IReadOnlyList<string> products,
        int needed,
        IDictionary<string, ProductState> market)
    {
        if (pop?.Account == null || pop.province?.ActiveLedger == null ||
            products == null || needed <= 0 || market == null)
        {
            return 0;
        }

        MoneyLedger ledger = pop.province.ActiveLedger;
        List<string> orderedProducts = products
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => market.TryGetValue(name, out ProductState product)
                ? product.LastDemand
                : 0)
            .ThenByDescending(name => market.TryGetValue(name, out ProductState product)
                ? product.Stock
                : 0)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();

        int purchased = 0;
        while (purchased < needed)
        {
            bool madeProgress = false;
            foreach (string productName in orderedProducts)
            {
                if (!market.TryGetValue(productName, out ProductState product))
                    continue;

                PurchaseResult result = MarketSettlement.TryPurchase(
                    product, pop.Account, needed - purchased, ledger);
                if (!result.Success || result.PurchasedQuantity <= 0)
                    continue;

                purchased += result.PurchasedQuantity;
                madeProgress = true;
                if (purchased == needed)
                    break;
            }

            if (!madeProgress)
                break;
        }

        return purchased;
    }

    private static bool HasUniquePopulationParticipants(IReadOnlyList<ProvinceEthnicPop> populations,
        Province province, MoneyLedger ledger)
    {
        HashSet<ProvinceEthnicPop> participants = new();
        HashSet<MoneyAccount> accounts = new();
        HashSet<string> accountIds = new(StringComparer.Ordinal);
        foreach (ProvinceEthnicPop population in populations)
        {
            if (!participants.Add(population) || !ReferenceEquals(population.province, province) ||
                population.Account == null || string.IsNullOrWhiteSpace(population.Account.Id) ||
                !ledger.OwnsAccount(population.Account) || !accounts.Add(population.Account) ||
                !accountIds.Add(population.Account.Id))
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidateMarketProducts(Dictionary<string, ProductState> products)
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

    private static ProductState ChooseFood(IReadOnlyList<string> foodNames,
        Dictionary<string, ProductState> products)
    {
        return foodNames
            .Where(products.ContainsKey)
            .Select(name => products[name])
            .Where(product => product != null && product.Price > 0)
            .OrderBy(product => product.Price)
            .ThenByDescending(product => product.Stock)
            .ThenBy(product => product.ProductName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int QuantityToOrder(ProvinceEthnicPop population, ProductState product,
        long availableBudget)
    {
        if (population == null || product == null || product.Price <= 0 || availableBudget <= 0)
            return 0;
        long affordable = availableBudget / product.Price;
        long quantity = Math.Min(population.GetNeededFood(), affordable);
        return quantity <= 0 ? 0 : checked((int)Math.Min(quantity, int.MaxValue));
    }

    private static int MaximumUnitPrice(int referencePrice, int quantity, long availableBudget)
    {
        int priceCap = checked((int)Math.Min(int.MaxValue,
            ((long)referencePrice * 5L + 3L) / 4L));
        return checked((int)Math.Min(priceCap, availableBudget / quantity));
    }

    private static string FoodOrderId(string accessId, string accountId, string productName) =>
        $"population:{Escape(accessId)}{Escape(accountId)}{Escape(productName)}";

    private static string Escape(string value) => $"{value?.Length ?? 0}:{value}";

    private sealed class FoodOrderDraft
    {
        public string Id { get; }
        public MoneyAccount Buyer { get; }
        public ProductState Product { get; }
        public int Quantity { get; }
        public int MaximumUnitPrice { get; }
        public long ReservedBudget { get; }
        public PopulationFoodRecipient Recipient { get; }

        public FoodOrderDraft(string id, MoneyAccount buyer, ProductState product, int quantity,
            int maximumUnitPrice, long reservedBudget, PopulationFoodRecipient recipient)
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

    private sealed class PopulationFoodRecipient : IMarketOrderRecipient
    {
        private readonly ProvinceEthnicPop population;
        private readonly Dictionary<MarketOrder, ProductState> orders = new();
        private long preparationToken;
        public string Id { get; }

        public PopulationFoodRecipient(ProvinceEthnicPop population, string accessId)
        {
            this.population = population;
            Id = $"population-recipient:{Escape(accessId)}{Escape(population.Account.Id)}";
        }

        internal void AddOrder(MarketOrder order) => orders.Add(order, order.Product);

        public bool TryPrepareReceipt(IReadOnlyList<MarketOrderFill> fills,
            out IPreparedMarketReceipt receipt)
        {
            receipt = null;
            if (fills == null)
                return false;
            try
            {
                int purchased = 0;
                foreach (MarketOrderFill fill in fills)
                {
                    if (fill == null || !ReferenceEquals(fill.Order.Recipient, this) ||
                        !ReferenceEquals(fill.Order.Buyer, population.Account) ||
                        !orders.TryGetValue(fill.Order, out ProductState product) ||
                        !ReferenceEquals(fill.Order.Product, product))
                    {
                        return false;
                    }
                    purchased = checked(purchased + fill.Quantity);
                }
                preparationToken = NextToken(preparationToken);
                receipt = new FoodReceipt(this, preparationToken, purchased);
                return true;
            }
            catch (OverflowException)
            {
                receipt = null;
                return false;
            }
        }

        private void Commit(long token, int purchased)
        {
            if (token != preparationToken)
                return;
            population.BuyFood(purchased);
            preparationToken = NextToken(preparationToken);
        }

        private static long NextToken(long token) => token == long.MaxValue ? 1L : token + 1L;

        private sealed class FoodReceipt : IPreparedMarketReceipt
        {
            private PopulationFoodRecipient recipient;
            private readonly long token;
            private readonly int purchased;

            public FoodReceipt(PopulationFoodRecipient recipient, long token, int purchased)
            {
                this.recipient = recipient;
                this.token = token;
                this.purchased = purchased;
            }

            public void Commit()
            {
                PopulationFoodRecipient current = recipient;
                current?.Commit(token, purchased);
                recipient = null;
            }
        }
    }
}

public sealed class PopulationMarketOrderBatch
{
    public static PopulationMarketOrderBatch Empty { get; } = new(
        Array.Empty<MarketOrder>(), Array.Empty<IMarketOrderRecipient>());
    public IReadOnlyList<MarketOrder> Orders { get; }
    public IReadOnlyList<IMarketOrderRecipient> Recipients { get; }

    public PopulationMarketOrderBatch(IEnumerable<MarketOrder> orders,
        IEnumerable<IMarketOrderRecipient> recipients)
    {
        Orders = (orders ?? throw new ArgumentNullException(nameof(orders))).ToList().AsReadOnly();
        Recipients = (recipients ?? throw new ArgumentNullException(nameof(recipients))).ToList().AsReadOnly();
    }
}
