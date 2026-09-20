using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class MarketClearingPerformanceTests
{
    private const int ProductCount = 18;
    private const int BuyerCount = 100;
    private const int OrderCount = 5_000;

    [Test]
    public void FiveThousandOrders_ReportPlannerAndSettlementMeasurementsWithoutTimeThreshold()
    {
        object treasury = Account("performance-treasury", 0L);
        object ledger = ReflectionTestHelpers.New(
            "MoneyLedger", "performance-coin", new object(), treasury);
        Assert.That(RegisterInitial(ledger, treasury), Is.True);

        object[] suppliers = Enumerable.Range(0, ProductCount)
            .Select(index => Account($"performance-supplier-{index:D2}", 0L))
            .ToArray();
        object[] buyers = Enumerable.Range(0, BuyerCount)
            .Select(index => Account($"performance-buyer-{index:D3}", 100_000L))
            .ToArray();
        foreach (object account in suppliers.Concat(buyers))
            Assert.That(RegisterInitial(ledger, account), Is.True);

        object[] products = new object[ProductCount];
        int expectedFillCount = 0;
        for (int index = 0; index < ProductCount; index++)
        {
            int price = 10 + index % 3;
            int stock = 100 + index;
            object product = ReflectionTestHelpers.New(
                "ProductState", $"performance-product-{index:D2}", price);
            ReflectionTestHelpers.Call<object>(product, "AddSupply", suppliers[index], stock);
            products[index] = product;
            expectedFillCount += stock;
        }

        object[] recipients = buyers.Select((_, index) =>
            Recipient($"performance-recipient-{index:D3}")).ToArray();
        IList orders = (IList)TestEconomyFactory.ListOf("MarketOrder");
        for (int index = 0; index < OrderCount; index++)
        {
            int productIndex = index % ProductCount;
            int buyerIndex = index % BuyerCount;
            object product = products[productIndex];
            int maximumUnitPrice = (int)ReflectionTestHelpers.Get(product, "Price") + 1 + index % 5;
            orders.Add(ReflectionTestHelpers.New(
                "MarketOrder",
                $"performance-order-{index:D4}",
                buyers[buyerIndex],
                product,
                1,
                maximumUnitPrice,
                (long)maximumUnitPrice,
                1,
                recipients[buyerIndex]));
        }

        Assert.That(orders, Has.Count.EqualTo(OrderCount));
        int sortedOrderCount = CountOrdersInShortageBooks(orders, products);
        Assert.That(sortedOrderCount, Is.EqualTo(OrderCount));
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");
        long initialMoneySupply = (long)ReflectionTestHelpers.Get(ledger, "MoneySupply");
        AssertAudit(ledger, initialMoneySupply);

        object settings = ReflectionTestHelpers.New("MarketPriceSettings", 3000, 2500);
        object[] planArguments =
        {
            orders,
            TestEconomyFactory.ListOf("ProductState", products),
            ledger,
            settings,
            null,
            null
        };
        MethodInfo planMethod = ReflectionTestHelpers.Find("MarketClearingEngine")
            .GetMethod("TryPlan", BindingFlags.Public | BindingFlags.Static);
        Assert.That(planMethod, Is.Not.Null);
        Stopwatch planner = Stopwatch.StartNew();
        bool planned = (bool)planMethod.Invoke(null, planArguments);
        planner.Stop();

        Assert.That(planned, Is.True, planArguments[5] as string);
        object plan = planArguments[4];
        IList fills = (IList)ReflectionTestHelpers.Get(plan, "Fills");
        IList purchases = (IList)ReflectionTestHelpers.Get(plan, "Purchases");
        IList results = (IList)ReflectionTestHelpers.Get(plan, "ProductResults");
        Assert.That(results, Has.Count.EqualTo(ProductCount));
        Assert.That(fills, Has.Count.EqualTo(expectedFillCount));
        Assert.That(purchases, Has.Count.EqualTo(expectedFillCount));
        Assert.That(fills.Cast<object>().Sum(fill =>
            (int)ReflectionTestHelpers.Get(fill, "Quantity")), Is.EqualTo(expectedFillCount));
        string[] fillIds = fills.Cast<object>()
            .Select(fill => (string)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(fill, "Order"), "Id"))
            .ToArray();
        Assert.That(fillIds,
            Is.EqualTo(fillIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        AssertUniformPrices(fills);
        AssertPlannedProductTotals(results, products);

        object[] settlementArguments =
        {
            ledger,
            TestEconomyFactory.ListOf("IMarketOrderRecipient"),
            null
        };
        MethodInfo settlementMethod = plan.GetType().GetMethod("TrySettle");
        Assert.That(settlementMethod, Is.Not.Null);
        Stopwatch settlement = Stopwatch.StartNew();
        bool settled = (bool)settlementMethod.Invoke(plan, settlementArguments);
        settlement.Stop();

        Assert.That(settled, Is.True, settlementArguments[2] as string);
        Assert.That(products.All(product => (int)ReflectionTestHelpers.Get(product, "Stock") == 0),
            Is.True);
        Assert.That(products.Sum(product =>
            (int)ReflectionTestHelpers.Get(product, "LastDemand")), Is.EqualTo(expectedFillCount));
        Assert.That(buyers.All(buyer => (long)ReflectionTestHelpers.Get(buyer, "Balance") >= 0L),
            Is.True);
        Assert.That(recipients.Cast<MarketClearingRecipientProxy>().All(recipient =>
            recipient.Calls == 1 && recipient.Prepared != null && recipient.Prepared.Commits == 1),
            Is.True);
        AssertAudit(ledger, initialMoneySupply);

        double totalMillisecondsPerOrder =
            (planner.Elapsed.TotalMilliseconds + settlement.Elapsed.TotalMilliseconds) / OrderCount;
        TestContext.WriteLine(
            $"Market clearing performance: orders={OrderCount}, sortedOrders={sortedOrderCount}, " +
            $"fills={expectedFillCount}, planMs={planner.Elapsed.TotalMilliseconds:F3}, " +
            $"settlementMs={settlement.Elapsed.TotalMilliseconds:F3}, " +
            $"totalMsPerOrder={totalMillisecondsPerOrder:F6}");
    }

    private static int CountOrdersInShortageBooks(IList orders, object[] products)
    {
        int sorted = 0;
        foreach (object product in products)
        {
            List<object> eligible = orders.Cast<object>()
                .Where(order => ReferenceEquals(
                    ReflectionTestHelpers.Get(order, "Product"), product))
                .Where(order => (int)ReflectionTestHelpers.Get(order, "MaximumUnitPrice") >=
                                (int)ReflectionTestHelpers.Get(product, "Price"))
                .ToList();
            int demand = eligible.Sum(order => (int)ReflectionTestHelpers.Get(order, "Quantity"));
            int stock = (int)ReflectionTestHelpers.Get(product, "Stock");
            if (stock > 0 && stock < demand)
                sorted += eligible.Count;
        }
        return sorted;
    }

    private static void AssertUniformPrices(IList fills)
    {
        foreach (IGrouping<object, object> productFills in fills.Cast<object>()
                     .GroupBy(fill => ReflectionTestHelpers.Get(
                         ReflectionTestHelpers.Get(fill, "Order"), "Product")))
        {
            Assert.That(productFills.Select(fill =>
                    (int)ReflectionTestHelpers.Get(fill, "UnitPrice")).Distinct().Count(),
                Is.EqualTo(1));
        }
    }

    private static void AssertPlannedProductTotals(IList results, object[] products)
    {
        for (int index = 0; index < ProductCount; index++)
        {
            object result = results.Cast<object>().Single(candidate => ReferenceEquals(
                ReflectionTestHelpers.Get(candidate, "Product"), products[index]));
            int expectedDemand = OrderCount / ProductCount +
                                 (index < OrderCount % ProductCount ? 1 : 0);
            int expectedStock = 100 + index;
            Assert.That(ReflectionTestHelpers.Get(result, "RequestedDemand"),
                Is.EqualTo(expectedDemand));
            Assert.That(ReflectionTestHelpers.Get(result, "AvailableStock"),
                Is.EqualTo(expectedStock));
            Assert.That(ReflectionTestHelpers.Get(result, "SoldQuantity"),
                Is.EqualTo(expectedStock));
        }
    }

    private static void AssertAudit(object ledger, long expectedMoneySupply)
    {
        object[] audit = { 0L };
        Assert.That(ledger.GetType().GetMethod("Audit").Invoke(ledger, audit), Is.True);
        Assert.That(audit[0], Is.EqualTo(expectedMoneySupply));
    }

    private static bool RegisterInitial(object ledger, object account) =>
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", account);

    private static object Account(string id, long balance) =>
        ReflectionTestHelpers.New("MoneyAccount", id, balance);

    private static object Recipient(string id)
    {
        object proxy = typeof(DispatchProxy).GetMethod("Create").MakeGenericMethod(
            ReflectionTestHelpers.Find("IMarketOrderRecipient"),
            typeof(MarketClearingRecipientProxy)).Invoke(null, null);
        ((MarketClearingRecipientProxy)proxy).Id = id;
        return proxy;
    }
}
