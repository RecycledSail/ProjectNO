using System;
using System.Collections.Generic;
using System.Numerics;

// One synchronous market phase: plan every debit and acquisition before settling.
public static class ConstructionProcurement
{
    public static bool TryProcessMarket(IReadOnlyList<ConstructionMandate> mandates,
        Dictionary<string, ProductState> products, MoneyLedger ledger)
    {
        if (mandates == null || products == null || ledger == null ||
            !ledger.OwnsAccount(ledger.TreasuryAccount))
            return false;

        try
        {
            HashSet<ProductState> mappedProducts = new();
            foreach (KeyValuePair<string, ProductState> item in products)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null ||
                    !string.Equals(item.Key, item.Value.ProductName, StringComparison.Ordinal) ||
                    !mappedProducts.Add(item.Value))
                    return false;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            Dictionary<MoneyAccount, List<ProjectPlan>> buyers = new();
            List<ProjectPlan> projects = new();
            foreach (ConstructionMandate mandate in mandates)
            {
                if (mandate == null || !mandate.IsActive || string.IsNullOrWhiteSpace(mandate.Id) ||
                    !ids.Add(mandate.Id) || !ReferenceEquals(mandate.TargetProvince.ActiveLedger, ledger) ||
                    !ReferenceEquals(mandate.GetAccessibleProducts(), products) ||
                    !ledger.OwnsAccount(mandate.Investor?.InvestmentAccount) ||
                    (mandate.EscrowAccount != null && !ledger.OwnsAccount(mandate.EscrowAccount)))
                    return false;

                ProjectPlan project = new(mandate);
                foreach (KeyValuePair<string, long> material in mandate.RequiredMaterials)
                {
                    if (!mandate.AcquiredMaterials.TryGetValue(material.Key, out long acquired) ||
                        material.Value <= 0 || acquired < 0 || acquired > material.Value)
                        return false;
                    long remaining = material.Value - acquired;
                    if (remaining == 0 || !products.TryGetValue(material.Key, out ProductState product) ||
                        product.Price <= 0 || product.Stock == 0)
                        continue;

                    long valuation = checked(remaining * product.Price);
                    project.Items.Add(material.Key, new ItemPlan(product, remaining, valuation));
                    project.Valuation = checked(project.Valuation + valuation);
                }
                projects.Add(project);
                MoneyAccount buyer = mandate.Investor.InvestmentAccount;
                if (!buyers.TryGetValue(buyer, out List<ProjectPlan> ownedProjects))
                    buyers.Add(buyer, ownedProjects = new List<ProjectPlan>());
                ownedProjects.Add(project);
            }

            Dictionary<ProductState, Dictionary<ProjectPlan, long>> requestsByProduct = new();
            foreach (KeyValuePair<MoneyAccount, List<ProjectPlan>> buyer in buyers)
            {
                long valuation = 0;
                foreach (ProjectPlan project in buyer.Value)
                    valuation = checked(valuation + project.Valuation);
                if (valuation == 0)
                    continue;

                long available = Math.Min(buyer.Key.Balance, valuation);
                foreach (ProjectPlan project in buyer.Value)
                {
                    if (project.Valuation == 0)
                        continue;
                    long budget = FloorShare(available, project.Valuation, valuation);
                    foreach (ItemPlan item in project.Items.Values)
                    {
                        long itemBudget = FloorShare(budget, item.Valuation, project.Valuation);
                        long quantity = Math.Min(item.Remaining, itemBudget / item.Product.Price);
                        if (quantity == 0)
                            continue;
                        if (!requestsByProduct.TryGetValue(item.Product, out Dictionary<ProjectPlan, long> requests))
                            requestsByProduct.Add(item.Product, requests = new Dictionary<ProjectPlan, long>());
                        requests.Add(project, quantity);
                    }
                }
            }

            List<MarketBuyerRequest> purchases = new();
            foreach (KeyValuePair<ProductState, Dictionary<ProjectPlan, long>> product in requestsByProduct)
            {
                long requested = 0;
                foreach (long quantity in product.Value.Values)
                    requested = checked(requested + quantity);
                Dictionary<ProjectPlan, long> quotas = ProportionalAllocator.Allocate(
                    Math.Min(product.Key.Stock, requested), product.Value, project => project.Mandate.Id);
                foreach (KeyValuePair<ProjectPlan, long> quota in quotas)
                {
                    if (quota.Value == 0)
                        continue;
                    ProjectPlan project = quota.Key;
                    string name = product.Key.ProductName;
                    project.Quantities.Add(name, quota.Value);
                    project.Spending = checked(project.Spending + checked(quota.Value * product.Key.Price));
                    // Length prefixes prevent ambiguous project/product pairs from sharing an ID.
                    string id = project.Mandate.Id;
                    purchases.Add(new MarketBuyerRequest($"{id.Length}:{id}{name.Length}:{name}",
                        project.Mandate.Investor.InvestmentAccount, product.Key, checked((int)quota.Value)));
                }
            }

            if (purchases.Count == 0)
                return true;
            foreach (ProjectPlan project in projects)
            {
                if (project.Quantities.Count > 0 && !project.Mandate.TryPrepareMaterialAcquisition(
                        project.Quantities, project.Spending, out project.Acquisition))
                    return false;
            }
            if (!MarketSettlement.TryPurchaseBatch(purchases, ledger))
                return false;

            foreach (ProjectPlan project in projects)
                if (project.Acquisition != null)
                    project.Mandate.CommitMaterialAcquisition(project.Acquisition);
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

    // Budget remainders remain in the investor account for a later phase.
    private static long FloorShare(long total, long weight, long totalWeight) =>
        (long)((BigInteger)total * weight / totalWeight);

    private sealed class ProjectPlan
    {
        public readonly ConstructionMandate Mandate;
        public readonly Dictionary<string, ItemPlan> Items = new(StringComparer.Ordinal);
        public readonly Dictionary<string, long> Quantities = new(StringComparer.Ordinal);
        public long Valuation;
        public long Spending;
        public ConstructionMaterials.Acquisition Acquisition;

        public ProjectPlan(ConstructionMandate mandate) => Mandate = mandate;
    }

    private sealed class ItemPlan
    {
        public readonly ProductState Product;
        public readonly long Remaining;
        public readonly long Valuation;

        public ItemPlan(ProductState product, long remaining, long valuation)
        {
            Product = product;
            Remaining = remaining;
            Valuation = valuation;
        }
    }
}
