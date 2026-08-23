using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

public class NeutralProvinceLedgerTests
{
    [Test]
    public void AbsorbNeutralProvince_MigratesCombinedSupplyWithoutChangingActorBalances()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 700L);
        object population = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);
        Initialize(nation, province);

        object nationalLedger = ReflectionTestHelpers.Get(nation, "Ledger");
        object localLedger = ReflectionTestHelpers.Get(province, "LocalLedger");
        object nationalTreasury = ReflectionTestHelpers.Get(nation, "Account");
        object localTreasury = ReflectionTestHelpers.Get(province, "LocalTreasuryAccount");
        object building = TestEconomyFactory.GetOnlyBuilding(province);
        object populationAccount = ReflectionTestHelpers.Get(population, "Account");
        object buildingAccount = ReflectionTestHelpers.Get(building, "Account");
        object inventoryProduct = AddMarketLot(province, populationAccount, 7);

        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Balance"), Is.EqualTo(500L));
        Assert.That(TryAbsorb(province, nation, out string error), Is.True, error);

        Assert.That(ReflectionTestHelpers.Get(localLedger, "MoneySupply"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(nationalLedger, "MoneySupply"), Is.EqualTo(2000L));
        Assert.That(ReflectionTestHelpers.Get(populationAccount, "Ledger"), Is.SameAs(nationalLedger));
        Assert.That(ReflectionTestHelpers.Get(buildingAccount, "Ledger"), Is.SameAs(nationalLedger));
        Assert.That(LotQuantity(inventoryProduct, populationAccount), Is.EqualTo(7));
        Assert.That(ReflectionTestHelpers.Get(inventoryProduct, "Stock"), Is.EqualTo(7));
        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Balance"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(nationalTreasury, "Balance"), Is.EqualTo(1500L));
        Assert.That(ReflectionTestHelpers.Get(population, "property"), Is.EqualTo(300L));
        Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(200L));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalLedger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "LocalTreasuryAccount"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(province, "ActiveLedger"), Is.SameAs(nationalLedger));
        Assert.That(ReflectionTestHelpers.Get(province, "nation"), Is.SameAs(nation));
        Assert.That((bool)ReflectionTestHelpers.Call<bool>(nation, "HasProvinces", province), Is.True);
    }

    [Test]
    public void AbsorbNeutralProvince_ForeignMarketLotOwnerLeavesAllStateUnchanged()
    {
        NeutralInventoryContext context = NeutralInventoryContext.Create();
        object foreignTreasury = ReflectionTestHelpers.New("MoneyAccount", "foreign:treasury", 0L);
        object foreignSupplier = ReflectionTestHelpers.New("MoneyAccount", "foreign:supplier", 0L);
        object foreignLedger = ReflectionTestHelpers.New(
            "MoneyLedger", "foreign", new object(), foreignTreasury);
        Assert.That(ReflectionTestHelpers.Call<bool>(
            foreignLedger, "RegisterInitialAccount", foreignTreasury), Is.True);
        Assert.That(ReflectionTestHelpers.Call<bool>(
            foreignLedger, "RegisterInitialAccount", foreignSupplier), Is.True);
        ReflectionTestHelpers.Call<object>(foreignLedger, "SealInitialization");
        object product = AddMarketLot(context.Province, foreignSupplier, 9);

        AssertAbsorptionRejectedWithoutMutation(
            context, product, foreignSupplier, foreignLedger, "supplier");
        Assert.That(ReflectionTestHelpers.Get(foreignLedger, "MoneySupply"), Is.Zero);
    }

    [Test]
    public void AbsorbNeutralProvince_LocalTreasuryMarketLotOwnerLeavesAllStateUnchanged()
    {
        NeutralInventoryContext context = NeutralInventoryContext.Create();
        object product = AddMarketLot(context.Province, context.LocalTreasury, 9);

        AssertAbsorptionRejectedWithoutMutation(
            context, product, context.LocalTreasury, context.LocalLedger, "supplier");
    }

    [Test]
    public void AbsorbNeutralProvince_UnregisteredMarketLotOwnerLeavesAllStateUnchanged()
    {
        NeutralInventoryContext context = NeutralInventoryContext.Create();
        object unregisteredSupplier = ReflectionTestHelpers.New(
            "MoneyAccount", "unregistered:supplier", 0L);
        object product = AddMarketLot(context.Province, unregisteredSupplier, 9);

        AssertAbsorptionRejectedWithoutMutation(
            context, product, unregisteredSupplier, null, "supplier");
    }

    [Test]
    public void AddProvinces_InitializedNeutralProvinceRequiresAbsorption()
    {
        NeutralInventoryContext context = NeutralInventoryContext.Create();

        Assert.That(ReflectionTestHelpers.Call<bool>(
            context.Nation, "AddProvinces", context.Province), Is.False);
        Assert.That(ReflectionTestHelpers.Get(context.Province, "nation"), Is.Null);
        Assert.That(ReflectionTestHelpers.Call<bool>(
            context.Nation, "HasProvinces", context.Province), Is.False);
        Assert.That(ReflectionTestHelpers.Get(context.Province, "LocalLedger"),
            Is.SameAs(context.LocalLedger));
        Assert.That(ReflectionTestHelpers.Get(context.Province, "ActiveLedger"),
            Is.SameAs(context.LocalLedger));
    }

    [Test]
    public void AbsorbNeutralProvince_ForeignActorAccountLeavesAllStateUnchanged()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 700L);
        object population = TestEconomyFactory.AddPop(province, 300L, 1.0);
        TestEconomyFactory.AddBuilding(province, "WheatField", 2, 100L);
        Initialize(nation, province);

        object foreignPopulation = TestEconomyFactory.AddPop(province, 0L, 1.0);
        object foreignAccount = ReflectionTestHelpers.Get(foreignPopulation, "Account");
        object foreignTreasury = ReflectionTestHelpers.New("MoneyAccount", "foreign:treasury", 0L);
        object foreignLedger = ReflectionTestHelpers.New("MoneyLedger", "foreign", null, foreignTreasury);
        ReflectionTestHelpers.Call<bool>(foreignLedger, "RegisterInitialAccount", foreignTreasury);
        ReflectionTestHelpers.Call<bool>(foreignLedger, "RegisterInitialAccount", foreignAccount);
        ReflectionTestHelpers.Call<object>(foreignLedger, "SealInitialization");

        object nationalLedger = ReflectionTestHelpers.Get(nation, "Ledger");
        object localLedger = ReflectionTestHelpers.Get(province, "LocalLedger");
        object localTreasury = ReflectionTestHelpers.Get(province, "LocalTreasuryAccount");
        object building = TestEconomyFactory.GetOnlyBuilding(province);
        object populationAccount = ReflectionTestHelpers.Get(population, "Account");
        object buildingAccount = ReflectionTestHelpers.Get(building, "Account");

        Assert.That(TryAbsorb(province, nation, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
        Assert.That(ReflectionTestHelpers.Get(nationalLedger, "MoneySupply"), Is.EqualTo(1000L));
        Assert.That(ReflectionTestHelpers.Get(localLedger, "MoneySupply"), Is.EqualTo(1000L));
        Assert.That(ReflectionTestHelpers.Get(foreignLedger, "MoneySupply"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(localTreasury, "Balance"), Is.EqualTo(500L));
        Assert.That(ReflectionTestHelpers.Get(population, "property"), Is.EqualTo(300L));
        Assert.That(ReflectionTestHelpers.Get(building, "balance"), Is.EqualTo(200L));
        Assert.That(ReflectionTestHelpers.Get(populationAccount, "Ledger"), Is.SameAs(localLedger));
        Assert.That(ReflectionTestHelpers.Get(buildingAccount, "Ledger"), Is.SameAs(localLedger));
        Assert.That(ReflectionTestHelpers.Get(foreignAccount, "Ledger"), Is.SameAs(foreignLedger));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalLedger"), Is.SameAs(localLedger));
        Assert.That(ReflectionTestHelpers.Get(province, "LocalTreasuryAccount"), Is.SameAs(localTreasury));
        Assert.That(ReflectionTestHelpers.Get(province, "nation"), Is.Null);
        Assert.That((bool)ReflectionTestHelpers.Call<bool>(nation, "HasProvinces", province), Is.False);
    }

    [Test]
    public void AbsorbNeutralProvince_MigratesFundedActiveMandateEscrowWithoutChangingSupply()
    {
        object nation = TestEconomyFactory.NewNation("N1", 1000L);
        object province = TestEconomyFactory.NewProvince(1, "Prano");
        ReflectionTestHelpers.Set(province, "initialLocalTreasury", 1000L);
        object population = TestEconomyFactory.AddPop(province, 200L, 1.0);
        Initialize(nation, province);

        object localLedger = ReflectionTestHelpers.Get(province, "LocalLedger");
        object populationAccount = ReflectionTestHelpers.Get(population, "Account");
        object escrow = ReflectionTestHelpers.New("MoneyAccount", "mandate:Prano:WheatField", 0L);
        Assert.That(ReflectionTestHelpers.Call<bool>(localLedger, "RegisterEmptyAccount", escrow), Is.True);
        Assert.That(ReflectionTestHelpers.Call<bool>(localLedger, "TryTransfer",
            populationAccount, escrow, 100L, "Fund neutral mandate"), Is.True);

        object buildingType = ReflectionTestHelpers.New("BuildingType", "WheatField");
        object mandate = ReflectionTestHelpers.New("ConstructionMandate", population,
            buildingType, province, 10d, escrow, 100L);
        object nationalLedger = ReflectionTestHelpers.Get(nation, "Ledger");

        Assert.That(ReflectionTestHelpers.Get(escrow, "Balance"), Is.EqualTo(100L));
        Assert.That(TryAbsorb(province, nation, out string error), Is.True, error);

        Assert.That(ReflectionTestHelpers.Get(escrow, "Ledger"), Is.SameAs(nationalLedger));
        Assert.That(ReflectionTestHelpers.Get(escrow, "Balance"), Is.EqualTo(100L));
        Assert.That(ReflectionTestHelpers.Get(nationalLedger, "MoneySupply"), Is.EqualTo(2200L));
        Assert.That(ReflectionTestHelpers.Get(localLedger, "MoneySupply"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(mandate, "Status").ToString(), Is.EqualTo("Requested"));
    }

    [Test]
    public void AbsorbNeutralProvince_AfterMandateCancellationHasNoTerminalEscrowAccount()
    {
        NeutralFundedMandate context = NeutralFundedMandate.Create();

        Assert.That(ReflectionTestHelpers.Call<bool>(context.Mandate, "Cancel"), Is.True);
        Assert.That(ReflectionTestHelpers.Get(context.Escrow, "Balance"), Is.EqualTo(0L));
        Assert.That(TryAbsorb(context.Province, context.Nation, out string error), Is.True, error);
        Assert.That(ReflectionTestHelpers.Get(context.Escrow, "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(context.LocalLedger, "MoneySupply"), Is.EqualTo(0L));
    }

    [Test]
    public void AbsorbNeutralProvince_AfterMandateCompletionHasNoTerminalEscrowAccount()
    {
        NeutralFundedMandate context = NeutralFundedMandate.Create();
        object companyType = ReflectionTestHelpers.New("BuildingType", "construcntionCompany");
        object company = ReflectionTestHelpers.New("ConstructionCompanyBuilding",
            companyType, context.Province, 1);

        Assert.That(ReflectionTestHelpers.Call<bool>(company, "TryAssign", context.Mandate), Is.True);
        ReflectionTestHelpers.Call<object>(company, "ProgressWeekly", 10d);

        Assert.That(ReflectionTestHelpers.Get(context.Mandate, "Status").ToString(),
            Is.EqualTo("Completed"));
        Assert.That(ReflectionTestHelpers.Get(context.Escrow, "Balance"), Is.EqualTo(0L));
        Assert.That(TryAbsorb(context.Province, context.Nation, out string error), Is.True, error);
        Assert.That(ReflectionTestHelpers.Get(context.Escrow, "Ledger"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(context.LocalLedger, "MoneySupply"), Is.EqualTo(0L));
    }

    private static void Initialize(object nation, object province)
    {
        ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize")
            .Invoke(null, new object[]
            {
                TestEconomyFactory.ListOf("Nation", nation),
                TestEconomyFactory.ListOf("Province", province)
            });
    }

    private static bool TryAbsorb(object province, object nation, out string error)
    {
        MethodInfo method = ReflectionTestHelpers.Find("ProvinceCurrencyMigration").GetMethod(
            "TryAbsorbNeutralProvince", BindingFlags.Public | BindingFlags.Static);
        object[] arguments = { province, nation, null };
        bool result = (bool)method.Invoke(null, arguments);
        error = (string)arguments[2];
        return result;
    }

    private static object AddMarketLot(object province, object supplier, int quantity)
    {
        object market = ReflectionTestHelpers.Get(province, "market");
        if (market == null)
        {
            market = ReflectionTestHelpers.New(
                "ProvinceMarket", ReflectionTestHelpers.Get(province, "name"));
            ReflectionTestHelpers.Set(province, "market", market);
        }

        ReflectionTestHelpers.Call<object>(market, "AddProduct", "MigrationProduct", 10);
        object product = ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))[
            "MigrationProduct"];
        ReflectionTestHelpers.Call<object>(product, "AddSupply", supplier, quantity);
        return product;
    }

    private static int LotQuantity(object product, object supplier)
    {
        object inventory = ReflectionTestHelpers.Get(product, "Inventory");
        foreach (object lot in (IEnumerable)ReflectionTestHelpers.Get(inventory, "Lots"))
        {
            if (ReferenceEquals(ReflectionTestHelpers.Get(lot, "Key"), supplier))
                return (int)ReflectionTestHelpers.Get(lot, "Value");
        }

        return 0;
    }

    private static int TransactionCount(object ledger) =>
        ((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count;

    private static void AssertAbsorptionRejectedWithoutMutation(
        NeutralInventoryContext context,
        object product,
        object supplier,
        object expectedSupplierLedger,
        string expectedError)
    {
        long nationalSupply = (long)ReflectionTestHelpers.Get(context.NationalLedger, "MoneySupply");
        long localSupply = (long)ReflectionTestHelpers.Get(context.LocalLedger, "MoneySupply");
        long nationalBalance = (long)ReflectionTestHelpers.Get(context.NationalTreasury, "Balance");
        long localBalance = (long)ReflectionTestHelpers.Get(context.LocalTreasury, "Balance");
        long populationBalance = (long)ReflectionTestHelpers.Get(context.Population, "property");
        int nationalTransactions = TransactionCount(context.NationalLedger);
        int localTransactions = TransactionCount(context.LocalLedger);

        Assert.That(TryAbsorb(context.Province, context.Nation, out string error), Is.False);

        Assert.That(error, Does.Contain(expectedError));
        Assert.That(ReflectionTestHelpers.Get(context.NationalLedger, "MoneySupply"),
            Is.EqualTo(nationalSupply));
        Assert.That(ReflectionTestHelpers.Get(context.LocalLedger, "MoneySupply"),
            Is.EqualTo(localSupply));
        Assert.That(ReflectionTestHelpers.Get(context.NationalTreasury, "Balance"),
            Is.EqualTo(nationalBalance));
        Assert.That(ReflectionTestHelpers.Get(context.LocalTreasury, "Balance"),
            Is.EqualTo(localBalance));
        Assert.That(ReflectionTestHelpers.Get(context.Population, "property"),
            Is.EqualTo(populationBalance));
        Assert.That(ReflectionTestHelpers.Get(context.PopulationAccount, "Ledger"),
            Is.SameAs(context.LocalLedger));
        Assert.That(ReflectionTestHelpers.Get(supplier, "Ledger"),
            expectedSupplierLedger == null ? Is.Null : Is.SameAs(expectedSupplierLedger));
        Assert.That(ReflectionTestHelpers.Get(context.Province, "nation"), Is.Null);
        Assert.That(ReflectionTestHelpers.Get(context.Province, "LocalLedger"),
            Is.SameAs(context.LocalLedger));
        Assert.That(ReflectionTestHelpers.Get(context.Province, "LocalTreasuryAccount"),
            Is.SameAs(context.LocalTreasury));
        Assert.That(ReflectionTestHelpers.Get(context.Province, "ActiveLedger"),
            Is.SameAs(context.LocalLedger));
        Assert.That(ReflectionTestHelpers.Call<bool>(
            context.Nation, "HasProvinces", context.Province), Is.False);
        Assert.That(LotQuantity(product, supplier), Is.EqualTo(9));
        Assert.That(ReflectionTestHelpers.Get(product, "Stock"), Is.EqualTo(9));
        Assert.That(ReflectionTestHelpers.Get(product, "LastSupply"), Is.EqualTo(9));
        Assert.That(ReflectionTestHelpers.Get(product, "LastDemand"), Is.Zero);
        Assert.That(TransactionCount(context.NationalLedger), Is.EqualTo(nationalTransactions));
        Assert.That(TransactionCount(context.LocalLedger), Is.EqualTo(localTransactions));
    }

    private sealed class NeutralInventoryContext
    {
        public object Nation { get; }
        public object Province { get; }
        public object Population { get; }
        public object PopulationAccount { get; }
        public object NationalLedger { get; }
        public object LocalLedger { get; }
        public object NationalTreasury { get; }
        public object LocalTreasury { get; }

        private NeutralInventoryContext(
            object nation,
            object province,
            object population,
            object nationalLedger,
            object localLedger,
            object nationalTreasury,
            object localTreasury)
        {
            Nation = nation;
            Province = province;
            Population = population;
            PopulationAccount = ReflectionTestHelpers.Get(population, "Account");
            NationalLedger = nationalLedger;
            LocalLedger = localLedger;
            NationalTreasury = nationalTreasury;
            LocalTreasury = localTreasury;
        }

        public static NeutralInventoryContext Create()
        {
            object nation = TestEconomyFactory.NewNation("MigrationNation", 1000L);
            object province = TestEconomyFactory.NewProvince(1, "MigrationNeutral");
            ReflectionTestHelpers.Set(province, "initialLocalTreasury", 500L);
            object population = TestEconomyFactory.AddPop(province, 200L, 1.0);
            Initialize(nation, province);
            return new NeutralInventoryContext(
                nation,
                province,
                population,
                ReflectionTestHelpers.Get(nation, "Ledger"),
                ReflectionTestHelpers.Get(province, "LocalLedger"),
                ReflectionTestHelpers.Get(nation, "Account"),
                ReflectionTestHelpers.Get(province, "LocalTreasuryAccount"));
        }
    }

    private sealed class NeutralFundedMandate
    {
        public object Nation { get; }
        public object Province { get; }
        public object LocalLedger { get; }
        public object Escrow { get; }
        public object Mandate { get; }

        private NeutralFundedMandate(
            object nation,
            object province,
            object localLedger,
            object escrow,
            object mandate)
        {
            Nation = nation;
            Province = province;
            LocalLedger = localLedger;
            Escrow = escrow;
            Mandate = mandate;
        }

        public static NeutralFundedMandate Create()
        {
            object nation = TestEconomyFactory.NewNation("N1", 1000L);
            object province = TestEconomyFactory.NewProvince(1, "Prano");
            ReflectionTestHelpers.Set(province, "initialLocalTreasury", 1000L);
            object population = TestEconomyFactory.AddPop(province, 200L, 1.0);
            Initialize(nation, province);

            object localLedger = ReflectionTestHelpers.Get(province, "LocalLedger");
            object escrow = ReflectionTestHelpers.New("MoneyAccount", "mandate:Prano:WheatField", 0L);
            object populationAccount = ReflectionTestHelpers.Get(population, "Account");
            Assert.That(ReflectionTestHelpers.Call<bool>(localLedger, "RegisterEmptyAccount", escrow), Is.True);
            Assert.That(ReflectionTestHelpers.Call<bool>(localLedger, "TryTransfer",
                populationAccount, escrow, 100L, "Fund neutral mandate"), Is.True);

            object buildingType = ReflectionTestHelpers.New("BuildingType", "WheatField");
            object mandate = ReflectionTestHelpers.New("ConstructionMandate", population,
                buildingType, province, 10d, escrow, 100L);
            return new NeutralFundedMandate(nation, province, localLedger, escrow, mandate);
        }
    }
}
