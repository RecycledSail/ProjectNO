using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using static ReflectionTestHelpers;

public class EmploymentTests
{
    private object province;
    private object a;
    private object b;
    private object shop;

    [SetUp]
    public void Setup()
    {
        province = TestEconomyFactory.NewProvince(1, "EmploymentProvince");
        a = AddPopulation("A", 100);
        b = AddPopulation("B", 300);
        shop = AddBuilding("Shop", 200, 100);
    }

    [Test]
    public void InitialWorkers_AreAssignedToActualPopulationsProportionally()
    {
        Initialize();
        Assert.That(Workers(shop, a), Is.EqualTo(25));
        Assert.That(Workers(shop, b), Is.EqualTo(75));
        Assert.That(Get(a, "EmployedPopulation"), Is.EqualTo(25L));
        Assert.That(Get(a, "UnemployedPopulation"), Is.EqualTo(75L));
        Assert.That(Get(province, "hiredPopulation"), Is.EqualTo(100L));
    }

    [Test]
    public void MultipleBuildings_CannotEmployMoreThanEachGroupsLaborSupply()
    {
        Set(shop, "currentWorkers", 200L);
        object second = AddBuilding("Second", 400, 400);
        Initialize();
        Assert.That(Get(province, "hiredPopulation"), Is.EqualTo(400L));
        Assert.That(Workers(shop, a) + Workers(second, a), Is.EqualTo(100));
        Assert.That(Workers(shop, b) + Workers(second, b), Is.EqualTo(300));
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(133L));
        Assert.That(Get(second, "currentWorkers"), Is.EqualTo(267L));
    }

    [Test]
    public void DemographicDecline_TrimsEmploymentAndRemovedBuildingsReleaseWorkers()
    {
        Initialize();
        Set(((IList)Get(a, "ageGroups"))[1], "agepopulation", 10L);
        Reconcile();
        Assert.That(Get(a, "EmployedPopulation"), Is.EqualTo(10L));
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(85L));
        ((IDictionary)Get(province, "buildings")).Clear();
        Reconcile();
        Assert.That(Get(province, "hiredPopulation"), Is.EqualTo(0L));
        Assert.That(Get(b, "EmployedPopulation"), Is.EqualTo(0L));
    }

    [Test]
    public void WeeklyVacancies_GrowByAtMostFiftyAndPreserveExistingWorkers()
    {
        Fund(1000);
        Initialize();
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(150L));
        Assert.That(Workers(shop, a), Is.EqualTo(38));
        Assert.That(Workers(shop, b), Is.EqualTo(112));
        Reconcile();
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(150L));
    }

    [Test]
    public void ManagedWorkers_CannotBeAssignedDirectly()
    {
        Initialize();
        Assert.Throws<TargetInvocationException>(() => Set(shop, "currentWorkers", 900L));
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(100L));
    }

    [Test]
    public void InvalidForeignPopulation_RejectsBeforeAttachingEmployment()
    {
        Set(b, "province", TestEconomyFactory.NewProvince(2, "Foreign"));
        Assert.Throws<TargetInvocationException>(() => Initialize());
        Assert.That(Get(province, "Employment"), Is.Null);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(100L));
    }

    [Test]
    public void CapacityReductionAndNoLabor_LeaveNoPhantomEmployment()
    {
        Initialize();
        Set(shop, "level", 0);
        Reconcile();
        Assert.That(Get(province, "hiredPopulation"), Is.EqualTo(0L));
        Assert.That(Get(a, "UnemployedPopulation"), Is.EqualTo(100L));
        Set(shop, "level", 1);
        foreach (object pop in new[] { a, b })
            Set(((IList)Get(pop, "ageGroups"))[1], "agepopulation", 0L);
        Reconcile();
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(0L));
    }

    [Test]
    public void Wages_TransferExactlyOncePerWeekAndPreserveSupply()
    {
        object ledger = Fund(1000);
        Initialize();
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(150L));
        Assert.That(Get(a, "property"), Is.EqualTo(38L));
        Assert.That(Get(b, "property"), Is.EqualTo(112L));
        Assert.That(Get(shop, "balance"), Is.EqualTo(850L));
        Assert.That(Get(ledger, "MoneySupply"), Is.EqualTo(1000L));
        Assert.That(Get(ledger, "WeeklyTaxRevenue"), Is.EqualTo(0L));
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "balance"), Is.EqualTo(850L));
        Assert.That(ProcessWeek(0), Is.False);
        Assert.That(ProcessWeek(2), Is.True);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(200L));
        Assert.That(Get(shop, "balance"), Is.EqualTo(650L));
        Assert.That(Call<bool>(ledger, "Audit", (object)null), Is.True);
    }

    [Test]
    public void WageBudget_ClampsWorkersWithoutDebtAndKeepsMoneyForInputs()
    {
        Fund(80);
        Set(Get(shop, "buildingType"), "weeklyWage", 2L);
        Set(Get(shop, "buildingType"), "requireItems", new Dictionary<string, int> { ["Iron"] = 1 });
        Initialize();
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(20L));
        Assert.That(Get(shop, "balance"), Is.EqualTo(40L));
        Assert.That(Get(a, "property"), Is.EqualTo(10L));
        Assert.That(Get(b, "property"), Is.EqualTo(30L));
    }

    [Test]
    public void EmptyEmployer_CannotWorkWithoutPay()
    {
        Fund(0); Initialize();
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(0L));
        Assert.That(Get(province, "hiredPopulation"), Is.EqualTo(0L));
    }

    [Test]
    public void ForeignLedger_RejectsWholePayrollWithoutChangingContractsOrMoney()
    {
        object ledger = Fund(1000); Initialize();
        Set(province, "ActiveLedger", New("MoneyLedger", "foreign", null, New("MoneyAccount", "foreign-treasury", 0L)));
        Assert.That(ProcessWeek(1), Is.False);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(100L));
        Assert.That(Get(shop, "balance"), Is.EqualTo(1000L));
        Assert.That(Get(a, "property"), Is.EqualTo(0L));
        Set(province, "ActiveLedger", ledger);
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "balance"), Is.EqualTo(850L));
    }

    [Test]
    public void InvalidWage_RejectsBeforeAnyPaymentAndCanRetry()
    {
        Fund(1000); Initialize();
        Set(Get(shop, "buildingType"), "weeklyWage", 0L);
        Assert.That(ProcessWeek(1), Is.False);
        Assert.That(Get(shop, "balance"), Is.EqualTo(1000L));
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(100L));
        Set(Get(shop, "buildingType"), "weeklyWage", 1L);
        Assert.That(ProcessWeek(1), Is.True);
    }

    [Test]
    public void PaidWages_CanBuyEmployerOutputWithoutCreatingMoney()
    {
        object ledger = Fund(1000); Initialize();
        Assert.That(ProcessWeek(1), Is.True);
        object product = New("ProductState", "Food", 10);
        Call<object>(product, "AddSupply", Get(shop, "Account"), 10);
        object purchase = Find("MarketSettlement").GetMethod("TryPurchase").Invoke(null,
            new[] { product, Get(a, "Account"), (object)3, ledger });
        Assert.That(Get(purchase, "Success"), Is.True);
        Assert.That(Get(a, "property"), Is.EqualTo(8L));
        Assert.That(Get(shop, "balance"), Is.EqualTo(877L));
        Assert.That(Get(ledger, "WeeklyTaxRevenue"), Is.EqualTo(3L));
        Assert.That(Get(product, "Stock"), Is.EqualTo(7));
        Assert.That(Get(ledger, "MoneySupply"), Is.EqualTo(1000L));
        Assert.That(Call<bool>(ledger, "Audit", (object)null), Is.True);
    }

    [Test]
    public void ConstructionProgress_UsesActualEmploymentFraction()
    {
        object type = Get(shop, "buildingType");
        shop = New("ConstructionCompanyBuilding", type, province, 1);
        ((IDictionary)Get(province, "buildings"))[type] = shop;
        Set(shop, "currentWorkers", 50L);
        Initialize();
        object mandate = New("ConstructionMandate", a, New("BuildingType", "Target"), province, 100d);
        try
        {
            Assert.That(Call<bool>(shop, "TryAssign", mandate), Is.True);
            Call<object>(shop, "ProgressWeekly", 20d);
            Assert.That(Get(mandate, "RemainingManhours"), Is.EqualTo(95d));
            foreach (object pop in new[] { a, b }) Set(((IList)Get(pop, "ageGroups"))[1], "agepopulation", 0L);
            Reconcile();
            Call<object>(shop, "ProgressWeekly", 20d);
            Assert.That(Get(mandate, "RemainingManhours"), Is.EqualTo(95d));
        }
        finally { Call<bool>(mandate, "Cancel"); }
    }

    [Test]
    public void AnnualAgeTransition_ImmediatelyRemovesExcessEmployment()
    {
        Set(Get(shop, "buildingType"), "workerNeeded", 400L);
        Set(shop, "currentWorkers", 400L);
        Initialize();
        Call<object>(province, "AdvanceAgeGroupsOneYear");
        Assert.That(Get(a, "EmployedPopulation"), Is.EqualTo(99L));
        Assert.That(Get(b, "EmployedPopulation"), Is.EqualTo(298L));
        Assert.That(Get(province, "hiredPopulation"), Is.EqualTo(397L));
    }

    [Test]
    public void ManagedIndustrySubsidy_FundsEmployerWithoutCreatingWorkers()
    {
        object nation = TestEconomyFactory.NewNation("EmployerNation", 1000L);
        Call<bool>(nation, "AddProvinces", province);
        Set(province, "nation", nation);
        object treasury = Get(nation, "Account");
        object ledger = New("MoneyLedger", "employer-currency", nation, treasury);
        foreach (object account in new[] { treasury, Get(a, "Account"), Get(b, "Account"), Get(shop, "Account") })
            Assert.That(Call<bool>(ledger, "RegisterInitialAccount", account), Is.True);
        Call<object>(ledger, "SealInitialization");
        Set(nation, "Ledger", ledger); Set(province, "ActiveLedger", ledger);
        Set(shop, "previousGain", 1);
        Initialize();
        object budget = Get(nation, "governmentBudget");
        ((IDictionary)Get(Get(budget, "Policy"), "IndustrySubsidy"))["Shop"] = 100L;
        Assert.That(Call<bool>(budget, "PrintMoney"), Is.True);
        Assert.That(Get(shop, "balance"), Is.EqualTo(100L));
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(100L));
        Assert.That(Get(a, "EmployedPopulation"), Is.EqualTo(25L));
        Assert.That(Call<bool>(shop, "HireWorkers"), Is.False);
        Assert.That(Get(ledger, "MoneySupply"), Is.EqualTo(1100L));
    }

    [Test]
    public void OverflowingCapacity_RejectsPayrollBeforeMutatingAnyAccount()
    {
        Fund(1000); Initialize();
        Set(shop, "level", 2); Set(Get(shop, "buildingType"), "workerNeeded", long.MaxValue);
        Assert.That(ProcessWeek(1), Is.False);
        Assert.That(Get(shop, "balance"), Is.EqualTo(1000L));
        Assert.That(Get(a, "property"), Is.EqualTo(0L));
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(100L));
    }

    [Test]
    public void ReconciliationAfterPayroll_CannotCreateUnpaidWorkers()
    {
        Fund(0); Initialize();
        Assert.That(ProcessWeek(1), Is.True);
        Reconcile();
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(0L));
        Assert.That(ProcessWeek(1), Is.True);
        Assert.That(Get(shop, "currentWorkers"), Is.EqualTo(0L));
        Assert.That(Get(a, "property"), Is.EqualTo(0L));
    }

    private object Fund(long amount)
    {
        object treasury = New("MoneyAccount", "treasury", amount);
        object ledger = New("MoneyLedger", "currency", null, treasury);
        foreach (var account in new[] { treasury, Get(a, "Account"), Get(b, "Account"), Get(shop, "Account") })
            Assert.That(Call<bool>(ledger, "RegisterInitialAccount", account), Is.True);
        Call<object>(ledger, "SealInitialization");
        if (amount > 0) Assert.That(Call<bool>(ledger, "TryTransfer", treasury, Get(shop, "Account"), amount, "seed"), Is.True);
        Set(province, "ActiveLedger", ledger);
        return ledger;
    }

    private bool ProcessWeek(long week) => Call<bool>(Get(province, "Employment"), "TryProcessWeek", week);

    private object AddPopulation(string name, int count)
    {
        object species = New("SpeciesSpec"); Set(species, "name", name);
        object group = New("EthnicGroup", species, New("Culture", "Culture"));
        object pop = New("ProvinceEthnicPop", province, group, new List<int> { 0, count, 0, 0 }, 0L, 1.0);
        ((IList)Get(province, "provinceEthnicPops")).Add(pop);
        Call<object>(province, "InitializePopulation");
        return pop;
    }

    private object AddBuilding(string name, long capacity, long workers)
    {
        object type = New("BuildingType", name); Set(type, "workerNeeded", capacity);
        object building = New("Building", type, province);
        Set(building, "level", 1); Set(building, "currentWorkers", workers);
        ((IDictionary)Get(province, "buildings"))[type] = building;
        return building;
    }

    private void Initialize() => Find("ProvinceEmployment").GetMethod("Initialize").Invoke(null, new[] { province });
    private void Reconcile() => Call<object>(Get(province, "Employment"), "Reconcile");
    private long Workers(object building, object pop)
    {
        object records = Call<object>(Get(province, "Employment"), "GetWorkers", building);
        return ((IDictionary)records).Contains(pop) ? (long)((IDictionary)records)[pop] : 0;
    }
}
