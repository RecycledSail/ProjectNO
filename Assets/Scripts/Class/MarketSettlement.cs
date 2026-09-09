using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class PurchaseRequest
{
    public ProductState Product { get; }
    public int Quantity { get; }

    public PurchaseRequest(ProductState product, int quantity)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
    }
}

public sealed class PurchaseResult
{
    public bool Success { get; }
    public ProductState Product { get; }
    public int RequestedQuantity { get; }
    public int PurchasedQuantity { get; }
    public long GrossAmount { get; }
    public long TaxAmount { get; }

    internal PurchaseResult(
        bool success,
        ProductState product,
        int requestedQuantity,
        int purchasedQuantity,
        long grossAmount,
        long taxAmount)
    {
        Success = success;
        Product = product;
        RequestedQuantity = requestedQuantity;
        PurchasedQuantity = purchasedQuantity;
        GrossAmount = grossAmount;
        TaxAmount = taxAmount;
    }

    internal static PurchaseResult Failed(ProductState product, int requestedQuantity) =>
        new(false, product, requestedQuantity, 0, 0, 0);
}

public sealed class BasketPurchaseResult
{
    public bool Success { get; }
    public IReadOnlyList<PurchaseResult> Purchases { get; }
    public long TotalPurchasedQuantity { get; }
    public long GrossAmount { get; }
    public long TaxAmount { get; }

    internal BasketPurchaseResult(
        bool success,
        IReadOnlyList<PurchaseResult> purchases,
        long totalPurchasedQuantity,
        long grossAmount,
        long taxAmount)
    {
        Success = success;
        Purchases = new ReadOnlyCollection<PurchaseResult>(new List<PurchaseResult>(purchases));
        TotalPurchasedQuantity = totalPurchasedQuantity;
        GrossAmount = grossAmount;
        TaxAmount = taxAmount;
    }

    internal static BasketPurchaseResult Failed() =>
        new(false, Array.Empty<PurchaseResult>(), 0, 0, 0);
}

public static class MarketSettlement
{
    public static bool TryPurchaseBatch(
        IReadOnlyList<MarketBuyerRequest> requests,
        MoneyLedger ledger)
    {
        if (requests == null || requests.Count == 0 || ledger == null ||
            !ledger.OwnsAccount(ledger.TreasuryAccount))
        {
            return false;
        }

        try
        {
            HashSet<string> requestIds = new(StringComparer.Ordinal);
            Dictionary<ProductState, int> quantities = new();
            Dictionary<MoneyAccount, long> buyerGrossAmounts = new();
            Dictionary<MoneyAccount, long> deltas = new();
            List<PlannedBatchPurchase> plans = new();
            long totalTax = 0;

            foreach (MarketBuyerRequest request in requests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RequestId) ||
                    request.Buyer == null || request.Product == null || request.Quantity <= 0 ||
                    request.Product.Price <= 0 || !ledger.OwnsAccount(request.Buyer) ||
                    !requestIds.Add(request.RequestId))
                {
                    return false;
                }

                quantities.TryGetValue(request.Product, out int currentQuantity);
                quantities[request.Product] = checked(currentQuantity + request.Quantity);
                long requestGross = checked((long)request.Quantity * request.Product.Price);
                AddDelta(buyerGrossAmounts, request.Buyer, requestGross);
            }

            foreach (KeyValuePair<MoneyAccount, long> buyerGross in buyerGrossAmounts)
            {
                if (buyerGross.Key.Balance < buyerGross.Value)
                    return false;
                AddDelta(deltas, buyerGross.Key, -buyerGross.Value);
            }

            foreach (KeyValuePair<ProductState, int> productQuantity in quantities)
            {
                ProductState product = productQuantity.Key;
                int quantity = productQuantity.Value;
                if (!TryPlanProductSale(
                        product,
                        quantity,
                        ledger,
                        out IReadOnlyList<SupplierSale> sale,
                        out _,
                        out long tax,
                        out Dictionary<MoneyAccount, long> sellerCredits) ||
                    !product.CanCommitPurchase(sale))
                {
                    return false;
                }

                foreach (KeyValuePair<MoneyAccount, long> credit in sellerCredits)
                    AddDelta(deltas, credit.Key, credit.Value);

                totalTax = checked(totalTax + tax);
                plans.Add(new PlannedBatchPurchase(product, sale));
            }

            AddDelta(deltas, ledger.TreasuryAccount, totalTax);
            long batchTotal = 0;
            foreach (long delta in deltas.Values)
                batchTotal = checked(batchTotal + delta);
            if (batchTotal != 0)
                return false;

            List<MoneyTransferEntry> entries = deltas
                .OrderBy(pair => pair.Key.Id, StringComparer.Ordinal)
                .Select(pair => new MoneyTransferEntry(pair.Key, pair.Value))
                .ToList();
            if (!ledger.TryTransferBatch(entries, "Market batch purchase", totalTax))
                return false;

            foreach (PlannedBatchPurchase plan in plans)
                plan.Product.CommitPurchase(plan.Sale);

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
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static PurchaseResult TryPurchase(
        ProductState product,
        MoneyAccount buyer,
        int requestedQuantity,
        MoneyLedger ledger)
    {
        if (product == null || requestedQuantity <= 0)
            return PurchaseResult.Failed(product, requestedQuantity);

        BasketPurchaseResult basket = TryPurchaseBasket(
            new[] { new PurchaseRequest(product, requestedQuantity) },
            buyer,
            ledger,
            false);
        return basket.Success && basket.Purchases.Count == 1
            ? basket.Purchases[0]
            : PurchaseResult.Failed(product, requestedQuantity);
    }

    public static BasketPurchaseResult TryPurchaseBasket(
        IReadOnlyList<PurchaseRequest> requests,
        MoneyAccount buyer,
        MoneyLedger ledger,
        bool requireFullQuantity = false)
    {
        if (requests == null || requests.Count == 0 || buyer == null || ledger == null ||
            !ledger.OwnsAccount(buyer) || !ledger.OwnsAccount(ledger.TreasuryAccount))
        {
            return BasketPurchaseResult.Failed();
        }

        try
        {
            HashSet<ProductState> requestedProducts = new();
            List<PlannedPurchase> plans = new();
            Dictionary<MoneyAccount, long> deltas = new();
            long remainingFunds = buyer.Balance;
            long totalQuantity = 0;
            long totalGross = 0;
            long totalTax = 0;

            foreach (PurchaseRequest request in requests)
            {
                if (request == null || request.Product == null || request.Quantity <= 0 ||
                    request.Product.Price <= 0 || !requestedProducts.Add(request.Product))
                {
                    return BasketPurchaseResult.Failed();
                }

                int quantity = Math.Min(request.Quantity, request.Product.Stock);
                long affordableQuantity = remainingFunds / request.Product.Price;
                if (affordableQuantity < quantity)
                    quantity = checked((int)affordableQuantity);

                if (requireFullQuantity && quantity != request.Quantity)
                    return BasketPurchaseResult.Failed();
                if (quantity == 0)
                    continue;

                if (!TryPlanProductSale(
                        request.Product,
                        quantity,
                        ledger,
                        out IReadOnlyList<SupplierSale> sale,
                        out long gross,
                        out long tax,
                        out Dictionary<MoneyAccount, long> sellerCredits))
                {
                    return BasketPurchaseResult.Failed();
                }

                AddDelta(deltas, buyer, -gross);
                AddDelta(deltas, ledger.TreasuryAccount, tax);
                foreach (KeyValuePair<MoneyAccount, long> credit in sellerCredits)
                    AddDelta(deltas, credit.Key, credit.Value);

                plans.Add(new PlannedPurchase(request, quantity, gross, tax, sale));
                remainingFunds -= gross;
                totalQuantity = checked(totalQuantity + quantity);
                totalGross = checked(totalGross + gross);
                totalTax = checked(totalTax + tax);
            }

            if (plans.Count == 0)
                return BasketPurchaseResult.Failed();

            long batchTotal = 0;
            foreach (long delta in deltas.Values)
                batchTotal = checked(batchTotal + delta);
            if (batchTotal != 0 || plans.Any(plan => !plan.Request.Product.CanCommitPurchase(plan.Sale)))
                return BasketPurchaseResult.Failed();

            List<MoneyTransferEntry> entries = deltas
                .OrderBy(pair => pair.Key.Id, StringComparer.Ordinal)
                .Select(pair => new MoneyTransferEntry(pair.Key, pair.Value))
                .ToList();
            if (!ledger.TryTransferBatch(entries, "Market purchase", totalTax))
                return BasketPurchaseResult.Failed();

            foreach (PlannedPurchase plan in plans)
                plan.Request.Product.CommitPurchase(plan.Sale);

            List<PurchaseResult> results = plans.Select(plan => new PurchaseResult(
                true,
                plan.Request.Product,
                plan.Request.Quantity,
                plan.Quantity,
                plan.Gross,
                plan.Tax)).ToList();
            return new BasketPurchaseResult(true, results, totalQuantity, totalGross, totalTax);
        }
        catch (OverflowException)
        {
            return BasketPurchaseResult.Failed();
        }
        catch (ArgumentException)
        {
            return BasketPurchaseResult.Failed();
        }
        catch (InvalidOperationException)
        {
            return BasketPurchaseResult.Failed();
        }
    }

    private static bool IsCompleteSalePlan(IReadOnlyList<SupplierSale> sale, int quantity)
    {
        if (sale == null)
            return false;

        int total = 0;
        foreach (SupplierSale entry in sale)
        {
            if (entry == null || entry.Supplier == null || entry.Quantity <= 0)
                return false;
            total = checked(total + entry.Quantity);
        }

        return total == quantity;
    }

    private static bool TryPlanProductSale(
        ProductState product,
        int quantity,
        MoneyLedger ledger,
        out IReadOnlyList<SupplierSale> sale,
        out long gross,
        out long tax,
        out Dictionary<MoneyAccount, long> sellerCredits)
    {
        sale = null;
        gross = 0;
        tax = 0;
        sellerCredits = null;
        if (product == null || quantity <= 0 || product.Price <= 0 || ledger == null)
            return false;

        gross = checked((long)quantity * product.Price);
        tax = checked(gross * ledger.SalesTaxBasisPoints / 10_000L);
        long sellerNet = checked(gross - tax);
        sale = product.PlanSale(quantity);
        if (!IsCompleteSalePlan(sale, quantity) ||
            sale.Any(entry => !ledger.OwnsAccount(entry.Supplier)))
        {
            return false;
        }

        Dictionary<MoneyAccount, long> weights = sale.ToDictionary(
            entry => entry.Supplier,
            entry => (long)entry.Quantity);
        sellerCredits = ProportionalAllocator.Allocate(
            sellerNet,
            weights,
            account => account.Id);
        return true;
    }

    private static void AddDelta(
        IDictionary<MoneyAccount, long> deltas,
        MoneyAccount account,
        long delta)
    {
        deltas.TryGetValue(account, out long current);
        deltas[account] = checked(current + delta);
    }

    private sealed class PlannedPurchase
    {
        public PurchaseRequest Request { get; }
        public int Quantity { get; }
        public long Gross { get; }
        public long Tax { get; }
        public IReadOnlyList<SupplierSale> Sale { get; }

        public PlannedPurchase(
            PurchaseRequest request,
            int quantity,
            long gross,
            long tax,
            IReadOnlyList<SupplierSale> sale)
        {
            Request = request;
            Quantity = quantity;
            Gross = gross;
            Tax = tax;
            Sale = sale;
        }
    }

    private sealed class PlannedBatchPurchase
    {
        public ProductState Product { get; }
        public IReadOnlyList<SupplierSale> Sale { get; }

        public PlannedBatchPurchase(ProductState product, IReadOnlyList<SupplierSale> sale)
        {
            Product = product;
            Sale = sale;
        }
    }
}
