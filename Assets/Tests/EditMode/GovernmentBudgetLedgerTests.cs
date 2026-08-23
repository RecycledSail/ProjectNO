using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class GovernmentBudgetLedgerTests
{
    private const string BuildingTypeName = "government-budget-ledger-test-industry";
    private const string EarlyIndustryTypeName = "government-budget-atomic-a-early";
    private const string OverflowIndustryTypeName = "government-budget-atomic-z-overflow";

    [TearDown]
    public void RemoveTestRecipe()
    {
        Recipes.Remove(BuildingTypeName);
        Recipes.Remove(EarlyIndustryTypeName);
        Recipes.Remove(OverflowIndustryTypeName);
    }

    [Test]
    public void PrintMoney_MintsOnceAndDistributesOnlyTheIssuedAmount()
    {
        PolicyContext context = CreateContext();
        object policy = ReflectionTestHelpers.Get(context.Budget, "Policy");
        ReflectionTestHelpers.Set(policy, "MilitarySalary", 100L);
        ReflectionTestHelpers.Set(policy, "RealEstateFund", 100L);
        ((IDictionary)ReflectionTestHelpers.Get(policy, "IndustrySubsidy"))[BuildingTypeName] = 100L;

        Assert.That(Call(context.Budget, "PrintMoney"), Is.EqualTo(true));

        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(400L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(100L));
        Assert.That(Balance(context.Population), Is.EqualTo(200L));
        Assert.That(Balance(context.Building), Is.EqualTo(100L));
        Assert.That(Balance(context.Treasury) + Balance(context.Population) +
            Balance(context.Building), Is.EqualTo(400L));

        List<object> transactions = Transactions(context.Ledger);
        List<object> mints = transactions.Where(record =>
            ReflectionTestHelpers.Get(record, "Kind").ToString() == "Mint").ToList();
        Assert.That(mints, Has.Count.EqualTo(1));
        Assert.That(GetLong(mints[0], "Amount"), Is.EqualTo(300L));
        Assert.That(ReflectionTestHelpers.Get(mints[0], "Reason"), Is.EqualTo("policy issuance"));
        Assert.That(transactions.Count(record =>
            ReflectionTestHelpers.Get(record, "Kind").ToString() == "Transfer"), Is.EqualTo(3));
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger, "Audit", (object)null), Is.True);
        Assert.That(GetLong(ReflectionTestHelpers.Get(context.Budget, "Policy"), "Total"), Is.Zero);
    }

    [Test]
    public void Budget_GdpChangesDoNotCreateTaxMoney()
    {
        PolicyContext context = CreateContext();
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");
        int transactionsBefore = Transactions(context.Ledger).Count;

        ReflectionTestHelpers.Set(context.Nation, "GDPAverage", long.MaxValue);

        Assert.That(context.Budget.GetType().GetMethod("CollectTaxes"), Is.Null);
        Assert.That(Balance(context.Treasury), Is.EqualTo(100L));
        Assert.That(Balance(context.Population), Is.Zero);
        Assert.That(Balance(context.Building), Is.Zero);
        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(supplyBefore));
        Assert.That(GetLong(context.Budget, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(Transactions(context.Ledger), Has.Count.EqualTo(transactionsBefore));
    }

    [Test]
    public void Budget_ViewsLedgerAndForwardsSalesTaxBasisPoints()
    {
        PolicyContext context = CreateContext();
        PropertyInfo moneySupply = context.Budget.GetType().GetProperty("MoneySupply");
        PropertyInfo weeklyTaxRevenue = context.Budget.GetType().GetProperty("WeeklyTaxRevenue");

        Assert.That(moneySupply.CanWrite, Is.False);
        Assert.That(weeklyTaxRevenue.CanWrite, Is.False);
        Assert.That(ReflectionTestHelpers.Get(context.Budget, "SalesTaxBasisPoints"), Is.EqualTo(1000));

        ReflectionTestHelpers.Set(context.Budget, "SalesTaxBasisPoints", 725);

        Assert.That(ReflectionTestHelpers.Get(context.Ledger, "SalesTaxBasisPoints"), Is.EqualTo(725));
        Assert.That(GetLong(context.Budget, "MoneySupply"),
            Is.EqualTo(GetLong(context.Ledger, "MoneySupply")));
        Assert.That(GetLong(context.Budget, "WeeklyTaxRevenue"),
            Is.EqualTo(GetLong(context.Ledger, "WeeklyTaxRevenue")));
    }

    [Test]
    public void BeginWeek_ResetsOnlyTheBudgetWeeklyTaxView()
    {
        PolicyContext context = CreateContext();
        IList entries = (IList)TestEconomyFactory.ListOf("MoneyTransferEntry",
            ReflectionTestHelpers.New("MoneyTransferEntry", context.Treasury, -10L),
            ReflectionTestHelpers.New("MoneyTransferEntry",
                ReflectionTestHelpers.Get(context.Population, "Account"), 10L));
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger,
            "TryTransferBatch", entries, "tax statistic fixture", 4L), Is.True);
        long supplyBefore = GetLong(context.Ledger, "MoneySupply");
        long treasuryBefore = Balance(context.Treasury);
        long populationBefore = Balance(context.Population);

        Call(context.Ledger, "BeginWeek");

        Assert.That(GetLong(context.Budget, "WeeklyTaxRevenue"), Is.Zero);
        Assert.That(GetLong(context.Budget, "MoneySupply"), Is.EqualTo(supplyBefore));
        Assert.That(Balance(context.Treasury), Is.EqualTo(treasuryBefore));
        Assert.That(Balance(context.Population), Is.EqualTo(populationBefore));
    }

    [Test]
    public void PrintMoney_WithOverflowingPolicyChangesNothingAndKeepsPolicyPending()
    {
        PolicyContext context = CreateContext();
        object policy = ReflectionTestHelpers.Get(context.Budget, "Policy");
        ReflectionTestHelpers.Set(policy, "ResearchFund", long.MaxValue);
        ReflectionTestHelpers.Set(policy, "MilitarySalary", long.MaxValue);
        ReflectionTestHelpers.Set(policy, "RealEstateFund", 3L);
        int transactionsBefore = Transactions(context.Ledger).Count;

        Assert.That(Call(context.Budget, "PrintMoney"), Is.EqualTo(false));

        Assert.That(GetLong(context.Ledger, "MoneySupply"), Is.EqualTo(100L));
        Assert.That(Balance(context.Treasury), Is.EqualTo(100L));
        Assert.That(ReflectionTestHelpers.Get(context.Budget, "Policy"), Is.SameAs(policy));
        Assert.That(Transactions(context.Ledger), Has.Count.EqualTo(transactionsBefore));
    }

    [Test]
    public void PrintMoney_KeepsResearchAndUnfundedRecipientChannelsInTreasury()
    {
        object nation = TestEconomyFactory.NewNation("GovernmentBudgetNoTargets", 100L);
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
            new object[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province")
            });
        object budget = ReflectionTestHelpers.Get(nation, "governmentBudget");
        object policy = ReflectionTestHelpers.Get(budget, "Policy");
        ReflectionTestHelpers.Set(policy, "ResearchFund", 40L);
        ReflectionTestHelpers.Set(policy, "MilitarySalary", 50L);
        ReflectionTestHelpers.Set(policy, "RealEstateFund", 70L);
        ((IDictionary)ReflectionTestHelpers.Get(policy, "IndustrySubsidy"))["missing"] = 60L;

        Assert.That(Call(budget, "PrintMoney"), Is.EqualTo(true));

        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        Assert.That(GetLong(ledger, "MoneySupply"), Is.EqualTo(320L));
        Assert.That(Balance(ReflectionTestHelpers.Get(nation, "Account")), Is.EqualTo(320L));
        Assert.That(GetLong(nation, "researchFund"), Is.EqualTo(40L));
        Assert.That(Transactions(ledger).Count(record =>
            ReflectionTestHelpers.Get(record, "Kind").ToString() == "Mint"), Is.EqualTo(1));
        Assert.That(Transactions(ledger).Count(record =>
            ReflectionTestHelpers.Get(record, "Kind").ToString() == "Transfer"), Is.Zero);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "Audit", (object)null), Is.True);
    }

    [Test]
    public void PrintMoney_AppliesLivingStandardOnlyToFundedRealEstateRecipients()
    {
        PolicyContext context = CreateContext();
        object unregisteredPopulation = TestEconomyFactory.AddPop(context.Province, 0L, 1.0);
        object policy = ReflectionTestHelpers.Get(context.Budget, "Policy");
        ReflectionTestHelpers.Set(policy, "RealEstateFund", 100L);

        Assert.That(Call(context.Budget, "PrintMoney"), Is.EqualTo(true));

        Assert.That(Balance(context.Population), Is.EqualTo(100L));
        Assert.That(Balance(unregisteredPopulation), Is.Zero);
        Assert.That((double)ReflectionTestHelpers.Get(context.Population, "livingStandard"),
            Is.EqualTo(10.0));
        Assert.That((double)ReflectionTestHelpers.Get(unregisteredPopulation, "livingStandard"),
            Is.EqualTo(1.0));
    }

    [Test]
    public void PrintMoney_LaterIndustryOverflowLeavesTheCompletePolicyUnchanged()
    {
        object nation = TestEconomyFactory.NewNation("GovernmentBudgetAtomicOverflow", 100L);
        object province = TestEconomyFactory.NewProvince(902, "GovernmentBudgetAtomicOverflowProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);
        ReflectionTestHelpers.Set(nation, "capital", province);
        object population = TestEconomyFactory.AddPop(province, 0L, 1.0);
        TestEconomyFactory.AddBuilding(province, EarlyIndustryTypeName, 1, 0L);
        TestEconomyFactory.AddBuilding(province, OverflowIndustryTypeName, 2, 0L);
        object earlyBuilding = BuildingByType(province, EarlyIndustryTypeName);
        object overflowBuilding = BuildingByType(province, OverflowIndustryTypeName);
        ReflectionTestHelpers.Set(ReflectionTestHelpers.Get(earlyBuilding, "buildingType"),
            "workerNeeded", 10L);
        ReflectionTestHelpers.Set(earlyBuilding, "previousGain", 1);
        ReflectionTestHelpers.Set(ReflectionTestHelpers.Get(overflowBuilding, "buildingType"),
            "workerNeeded", long.MaxValue);
        Initialize(nation, province);

        object budget = ReflectionTestHelpers.Get(nation, "governmentBudget");
        object policy = ReflectionTestHelpers.Get(budget, "Policy");
        ReflectionTestHelpers.Set(policy, "ResearchFund", 10L);
        ReflectionTestHelpers.Set(policy, "MilitarySalary", 10L);
        IDictionary industry = (IDictionary)ReflectionTestHelpers.Get(policy, "IndustrySubsidy");
        industry[EarlyIndustryTypeName] = 20L;
        industry[OverflowIndustryTypeName] = 30L;
        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        int transactionsBefore = Transactions(ledger).Count;

        Assert.That(Call(budget, "PrintMoney"), Is.EqualTo(false));

        Assert.That(GetLong(ledger, "MoneySupply"), Is.EqualTo(100L));
        Assert.That(Balance(ReflectionTestHelpers.Get(nation, "Account")), Is.EqualTo(100L));
        Assert.That(Balance(population), Is.Zero);
        Assert.That(Balance(earlyBuilding), Is.Zero);
        Assert.That(Balance(overflowBuilding), Is.Zero);
        Assert.That(GetLong(nation, "researchFund"), Is.Zero);
        Assert.That(GetLong(earlyBuilding, "currentWorkers"), Is.Zero);
        Assert.That(GetLong(overflowBuilding, "currentWorkers"), Is.Zero);
        Assert.That(Transactions(ledger), Has.Count.EqualTo(transactionsBefore));
        Assert.That(ReflectionTestHelpers.Get(budget, "Policy"), Is.SameAs(policy));
    }

    [Test]
    public void PrintMoney_DuplicatePopulationAccountIdsLeaveTheCompletePolicyUnchanged()
    {
        object nation = TestEconomyFactory.NewNation("GovernmentBudgetDuplicatePopulation", 100L);
        object province = TestEconomyFactory.NewProvince(903, "GovernmentBudgetDuplicateProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);
        ReflectionTestHelpers.Set(nation, "capital", province);
        object firstPopulation = TestEconomyFactory.AddPop(province, 0L, 1.0);
        object secondPopulation = TestEconomyFactory.AddPop(province, 0L, 1.0);
        Initialize(nation, province);

        object budget = ReflectionTestHelpers.Get(nation, "governmentBudget");
        object policy = ReflectionTestHelpers.Get(budget, "Policy");
        ReflectionTestHelpers.Set(policy, "ResearchFund", 10L);
        ReflectionTestHelpers.Set(policy, "MilitarySalary", 20L);
        object ledger = ReflectionTestHelpers.Get(nation, "Ledger");
        int transactionsBefore = Transactions(ledger).Count;

        Assert.That(Call(budget, "PrintMoney"), Is.EqualTo(false));

        Assert.That(GetLong(ledger, "MoneySupply"), Is.EqualTo(100L));
        Assert.That(Balance(ReflectionTestHelpers.Get(nation, "Account")), Is.EqualTo(100L));
        Assert.That(Balance(firstPopulation), Is.Zero);
        Assert.That(Balance(secondPopulation), Is.Zero);
        Assert.That(GetLong(nation, "researchFund"), Is.Zero);
        Assert.That(Transactions(ledger), Has.Count.EqualTo(transactionsBefore));
        Assert.That(ReflectionTestHelpers.Get(budget, "Policy"), Is.SameAs(policy));
    }

    [Test]
    public void MoneyAccount_HasNoLoadingBalanceReplacementBypass()
    {
        Type accountType = ReflectionTestHelpers.Find("MoneyAccount");
        MethodInfo replacement = accountType.GetMethod(
            "ReplaceForLoading", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(replacement, Is.Null);
        Assert.That(accountType.GetProperty("Balance").SetMethod.IsPrivate, Is.True);
        Assert.That(accountType.GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name), Is.EqualTo(new[] { "ApplyDelta" }));
    }

    private static PolicyContext CreateContext()
    {
        object nation = TestEconomyFactory.NewNation("GovernmentBudgetLedgerTestNation", 100L);
        object province = TestEconomyFactory.NewProvince(901, "GovernmentBudgetLedgerTestProvince");
        Assert.That(ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", province), Is.True);
        ReflectionTestHelpers.Set(nation, "capital", province);
        object population = TestEconomyFactory.AddPop(province, 0L, 1.0);
        TestEconomyFactory.AddBuilding(province, BuildingTypeName, 1, 0L);
        object building = TestEconomyFactory.GetOnlyBuilding(province);

        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
            new object[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });

        return new PolicyContext(nation, population, building);
    }

    private static void Initialize(object nation, object province)
    {
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(null,
            new object[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });
    }

    private static object BuildingByType(object province, string typeName)
    {
        IDictionary buildings = (IDictionary)ReflectionTestHelpers.Get(province, "buildings");
        object key = buildings.Keys.Cast<object>()
            .Single(candidate =>
                (string)ReflectionTestHelpers.Get(candidate, "name") == typeName);
        return buildings[key];
    }

    private static List<object> Transactions(object ledger) =>
        ((IEnumerable)ReflectionTestHelpers.Get(ledger, "Transactions")).Cast<object>().ToList();

    private static long Balance(object owner)
    {
        object account = owner.GetType().Name == "MoneyAccount"
            ? owner
            : ReflectionTestHelpers.Get(owner, "Account");
        return GetLong(account, "Balance");
    }

    private static long GetLong(object instance, string member) =>
        (long)ReflectionTestHelpers.Get(instance, member);

    private static object Call(object instance, string method, params object[] arguments) =>
        ReflectionTestHelpers.Call<object>(instance, method, arguments);

    private static IDictionary Recipes => (IDictionary)ReflectionTestHelpers.Find("GlobalVariables")
        .GetField("BUILDING_RECIPE", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private sealed class PolicyContext
    {
        public object Nation { get; }
        public object Province { get; }
        public object Population { get; }
        public object Building { get; }
        public object Budget => ReflectionTestHelpers.Get(Nation, "governmentBudget");
        public object Ledger => ReflectionTestHelpers.Get(Nation, "Ledger");
        public object Treasury => ReflectionTestHelpers.Get(Nation, "Account");

        public PolicyContext(object nation, object population, object building)
        {
            Nation = nation;
            Province = ReflectionTestHelpers.Get(population, "province");
            Population = population;
            Building = building;
        }
    }
}
