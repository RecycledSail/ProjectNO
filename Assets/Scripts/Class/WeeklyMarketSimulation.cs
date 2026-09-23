using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

public sealed class WeeklyMarketReport
{
    public IReadOnlyList<string> SuccessfulMarketIds { get; }
    public IReadOnlyList<string> FailedMarketIds { get; }
    public int OrderCount { get; }
    public int SortedOrderCount { get; }
    public long ElapsedTicks { get; }

    internal WeeklyMarketReport(
        IEnumerable<string> successfulMarketIds,
        IEnumerable<string> failedMarketIds,
        int orderCount,
        int sortedOrderCount,
        long elapsedTicks)
    {
        SuccessfulMarketIds = new ReadOnlyCollection<string>(
            successfulMarketIds.OrderBy(id => id, StringComparer.Ordinal).ToList());
        FailedMarketIds = new ReadOnlyCollection<string>(
            failedMarketIds.OrderBy(id => id, StringComparer.Ordinal).ToList());
        OrderCount = orderCount;
        SortedOrderCount = sortedOrderCount;
        ElapsedTicks = elapsedTicks;
    }
}

public static class WeeklyMarketSimulation
{
    public static WeeklyMarketReport Process(
        IEnumerable<Nation> nations,
        IEnumerable<Province> provinces,
        ISet<Province> paidProvinces,
        EconomicEngine economicEngine,
        MarketPriceSettings settings)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        List<Nation> nationList = CanonicalNations(nations);
        List<Province> provinceList = CanonicalProvinces(provinces);
        HashSet<Province> paid = paidProvinces == null
            ? new HashSet<Province>()
            : new HashSet<Province>(paidProvinces.Where(province => province != null));
        var markets = new Dictionary<Dictionary<string, ProductState>, MarketBatch>(
            ReferenceComparer<Dictionary<string, ProductState>>.Instance);
        var directFailures = new HashSet<string>(StringComparer.Ordinal);

        foreach (Nation nation in nationList)
        {
            if (nation.market?.Products != null)
                RegisterMarket(markets, "nation:" + nation.name, nation.market.Products, nation.Ledger);
        }
        foreach (Province province in provinceList)
        {
            if (province.market?.Products != null)
                RegisterMarket(markets, "province:" + province.name,
                    province.market.Products, province.ActiveLedger);
        }

        var accessByProvince = new Dictionary<Province, MarketAccessContext>();
        foreach (Province province in provinceList)
        {
            if (!MarketAccess.TryResolve(province, out MarketAccessContext access))
            {
                directFailures.Add(ExpectedAccessId(province));
                Dictionary<string, ProductState> unresolvedProducts =
                    province.isConnectedToCapital
                        ? province.nation?.market?.Products ?? province.market?.Products
                        : province.market?.Products;
                if (unresolvedProducts != null &&
                    markets.TryGetValue(unresolvedProducts, out MarketBatch unresolvedMarket))
                {
                    unresolvedMarket.Fail();
                }
                continue;
            }

            accessByProvince.Add(province, access);
            RegisterMarket(markets, access.StableId, access.Products, access.Ledger);
        }

        IReadOnlyList<ConstructionMandate> projects = ConstructionWeeklySimulation
            .ActiveProjects(nationList, provinceList)
            .OrderBy(project => project.Id, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
        foreach (ConstructionMandate project in projects)
            project.ProcurementFailed = false;

        Dictionary<MoneyAccount, long> remainingBudgets = StartingBudgets(
            nationList, provinceList, projects);

        if (economicEngine == null || settings == null)
        {
            foreach (MarketBatch market in markets.Values)
                market.Fail();
        }
        else
        {
            CollectPopulationOrders(provinceList, accessByProvince, markets,
                remainingBudgets, economicEngine, settings);
            CollectFactoryOrders(provinceList, accessByProvince, markets,
                remainingBudgets, paid, settings);
            CollectConstructionOrders(projects, accessByProvince, markets,
                remainingBudgets, directFailures, settings);
        }

        int orderCount = 0;
        int sortedOrderCount = 0;
        var successful = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(directFailures, StringComparer.Ordinal);
        foreach (MarketBatch market in markets.Values.OrderBy(
                     batch => batch.StableId, StringComparer.Ordinal))
        {
            orderCount = AddSaturated(orderCount, market.Orders.Count);
            if (!market.StructurallyValid)
            {
                MarkFailed(market, failed);
                continue;
            }

            List<MarketOrder> orders = market.Orders
                .OrderBy(order => order.Id, StringComparer.Ordinal)
                .ToList();
            List<ProductState> products = market.Products.Values
                .OrderBy(product => product?.ProductName, StringComparer.Ordinal)
                .ToList();
            bool settled;
            try
            {
                settled = false;
                if (MarketClearingEngine.TryPlan(orders, products, market.Ledger, settings,
                        out MarketClearingPlan plan, out _))
                {
                    sortedOrderCount = AddSaturated(sortedOrderCount, plan.SortedOrderCount);
                    settled = plan.TrySettle(market.Ledger, market.AdditionalRecipients, out _);
                }
            }
            catch (Exception exception) when (IsStructuralFailure(exception))
            {
                settled = false;
            }

            if (!settled)
            {
                market.Fail();
                MarkFailed(market, failed);
                continue;
            }

            foreach (ConstructionMandate project in market.Projects)
                project.ProcurementFailed = false;
            foreach (string id in market.StableIds)
                successful.Add(id);
        }

        successful.ExceptWith(failed);
        elapsed.Stop();
        return new WeeklyMarketReport(
            successful, failed, orderCount, sortedOrderCount, elapsed.ElapsedTicks);
    }

    private static void CollectPopulationOrders(
        IEnumerable<Province> provinces,
        IReadOnlyDictionary<Province, MarketAccessContext> accessByProvince,
        IReadOnlyDictionary<Dictionary<string, ProductState>, MarketBatch> markets,
        IDictionary<MoneyAccount, long> remainingBudgets,
        EconomicEngine economicEngine, MarketPriceSettings settings)
    {
        foreach (Province province in provinces)
        {
            if (!TryGetBatch(province, accessByProvince, markets, out MarketAccessContext access,
                    out MarketBatch market) || !market.StructurallyValid)
                continue;
            try
            {
                PopulationMarketOrderBatch batch = economicEngine.CollectFoodOrders(
                    province, access, remainingBudgets, settings);
                market.Orders.AddRange(batch.Orders);
                market.AdditionalRecipients.AddRange(batch.Recipients);
            }
            catch (Exception exception) when (IsStructuralFailure(exception))
            {
                market.Fail();
            }
        }
    }

    private static void CollectFactoryOrders(
        IEnumerable<Province> provinces,
        IReadOnlyDictionary<Province, MarketAccessContext> accessByProvince,
        IReadOnlyDictionary<Dictionary<string, ProductState>, MarketBatch> markets,
        IDictionary<MoneyAccount, long> remainingBudgets,
        ISet<Province> paidProvinces, MarketPriceSettings settings)
    {
        foreach (Province province in provinces)
        {
            if (!TryGetBatch(province, accessByProvince, markets, out MarketAccessContext access,
                    out MarketBatch market) || !market.StructurallyValid)
                continue;
            try
            {
                market.Orders.AddRange(FactoryMarketOrders.Collect(
                    province, access, paidProvinces.Contains(province), remainingBudgets, settings));
            }
            catch (Exception exception) when (IsStructuralFailure(exception))
            {
                market.Fail();
            }
        }
    }

    private static void CollectConstructionOrders(
        IEnumerable<ConstructionMandate> projects,
        IReadOnlyDictionary<Province, MarketAccessContext> accessByProvince,
        IReadOnlyDictionary<Dictionary<string, ProductState>, MarketBatch> markets,
        IDictionary<MoneyAccount, long> remainingBudgets,
        ISet<string> directFailures, MarketPriceSettings settings)
    {
        foreach (ConstructionMandate project in projects)
        {
            if (!TryGetBatch(project.TargetProvince, accessByProvince, markets,
                    out _, out MarketBatch market))
            {
                project.ProcurementFailed = true;
                directFailures.Add(ExpectedAccessId(project.TargetProvince));
                continue;
            }
            market.Projects.Add(project);
        }

        foreach (MarketBatch market in markets.Values.OrderBy(
                     batch => batch.StableId, StringComparer.Ordinal))
        {
            if (market.Projects.Count == 0 || !market.StructurallyValid)
                continue;
            try
            {
                MarketAccessContext access = new(
                    market.StableId, market.Products, market.Ledger);
                market.Orders.AddRange(ConstructionProcurement.CollectOrders(
                    market.Projects.OrderBy(project => project.Id, StringComparer.Ordinal)
                        .ToList().AsReadOnly(),
                    access,
                    remainingBudgets, settings));
            }
            catch (Exception exception) when (IsStructuralFailure(exception))
            {
                market.Fail();
            }
        }
    }

    private static Dictionary<MoneyAccount, long> StartingBudgets(
        IEnumerable<Nation> nations,
        IEnumerable<Province> provinces,
        IEnumerable<ConstructionMandate> projects)
    {
        var budgets = new Dictionary<MoneyAccount, long>();
        foreach (Nation nation in nations)
            AddStartingBudget(budgets, nation?.InvestmentAccount);
        foreach (Province province in provinces)
        {
            if (province?.provinceEthnicPops != null)
                foreach (ProvinceEthnicPop population in province.provinceEthnicPops)
                    AddStartingBudget(budgets, population?.Account);
            if (province?.buildings != null)
                foreach (Building building in province.buildings.Values)
                    AddStartingBudget(budgets, building?.Account);
        }
        foreach (ConstructionMandate project in projects)
            AddStartingBudget(budgets, project?.Investor?.InvestmentAccount);
        return budgets;
    }

    private static void AddStartingBudget(
        IDictionary<MoneyAccount, long> budgets, MoneyAccount account)
    {
        if (account != null && !budgets.ContainsKey(account))
            budgets.Add(account, account.Balance);
    }

    private static int AddSaturated(int left, int right) =>
        left > int.MaxValue - right ? int.MaxValue : left + right;

    private static bool TryGetBatch(
        Province province,
        IReadOnlyDictionary<Province, MarketAccessContext> accessByProvince,
        IReadOnlyDictionary<Dictionary<string, ProductState>, MarketBatch> markets,
        out MarketAccessContext access,
        out MarketBatch market)
    {
        access = default;
        market = null;
        return province != null && accessByProvince.TryGetValue(province, out access) &&
               markets.TryGetValue(access.Products, out market);
    }

    private static void RegisterMarket(
        IDictionary<Dictionary<string, ProductState>, MarketBatch> markets,
        string stableId,
        Dictionary<string, ProductState> products,
        MoneyLedger ledger)
    {
        if (!markets.TryGetValue(products, out MarketBatch market))
        {
            market = new MarketBatch(products, ledger);
            markets.Add(products, market);
        }
        market.Register(stableId, ledger);
    }

    private static void MarkFailed(MarketBatch market, ISet<string> failed)
    {
        foreach (ConstructionMandate project in market.Projects)
            project.ProcurementFailed = true;
        foreach (string id in market.StableIds)
            failed.Add(id);
    }

    private static string ExpectedAccessId(Province province) =>
        province?.isConnectedToCapital == true && province.nation != null
            ? "nation:" + province.nation.name
            : "province:" + (province?.name ?? "unknown");

    private static List<Nation> CanonicalNations(IEnumerable<Nation> nations) =>
        (nations ?? Enumerable.Empty<Nation>())
        .Where(nation => nation != null)
        .Distinct()
        .OrderBy(nation => nation.name, StringComparer.Ordinal)
        .ThenBy(nation => nation.id)
        .ToList();

    private static List<Province> CanonicalProvinces(IEnumerable<Province> provinces) =>
        (provinces ?? Enumerable.Empty<Province>())
        .Where(province => province != null)
        .Distinct()
        .OrderBy(province => province.name, StringComparer.Ordinal)
        .ThenBy(province => province.id)
        .ToList();

    private static bool IsStructuralFailure(Exception exception) =>
        exception is ArgumentException ||
        exception is InvalidOperationException ||
        exception is OverflowException ||
        exception is NullReferenceException;

    private sealed class MarketBatch
    {
        private readonly HashSet<string> stableIds = new(StringComparer.Ordinal);
        public Dictionary<string, ProductState> Products { get; }
        public MoneyLedger Ledger { get; private set; }
        public List<MarketOrder> Orders { get; } = new();
        public List<IMarketOrderRecipient> AdditionalRecipients { get; } = new();
        public List<ConstructionMandate> Projects { get; } = new();
        public bool StructurallyValid { get; private set; } = true;
        public IEnumerable<string> StableIds => stableIds.OrderBy(id => id, StringComparer.Ordinal);
        public string StableId => stableIds.OrderBy(id => id, StringComparer.Ordinal).First();

        public MarketBatch(Dictionary<string, ProductState> products, MoneyLedger ledger)
        {
            Products = products;
            Ledger = ledger;
            if (products == null || ledger == null)
                StructurallyValid = false;
        }

        public void Register(string stableId, MoneyLedger ledger)
        {
            if (string.IsNullOrWhiteSpace(stableId) || ledger == null ||
                (Ledger != null && !ReferenceEquals(Ledger, ledger)))
            {
                StructurallyValid = false;
            }
            else if (Ledger == null)
            {
                Ledger = ledger;
            }
            if (!string.IsNullOrWhiteSpace(stableId))
                stableIds.Add(stableId);
        }

        public void Fail()
        {
            StructurallyValid = false;
            foreach (ConstructionMandate project in Projects)
                project.ProcurementFailed = true;
        }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();
        public bool Equals(T left, T right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
