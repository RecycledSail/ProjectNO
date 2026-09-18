using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class ConstructionInvestmentTests
{
    [SetUp]
    public void ClearConstructionGlobals()
    {
        Recipes.Clear();
        Adjacencies.Clear();
    }

    [Test]
    public void PlaceAndProgress_PaysConstructionFeeAndCapitalizesCompletedBuilding()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 1000L, initialCapital: 300L, constructionFee: 200L,
            timeToBuild: 10);
        object mandate = context.Place();

        Assert.That(ReflectionTestHelpers.Get(mandate, "Id"), Is.EqualTo(
            ReflectionTestHelpers.Get(ReflectionTestHelpers.Get(mandate, "EscrowAccount"), "Id")));
        Assert.That(context.InvestorBalance, Is.EqualTo(500L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(500L));
        Assert.That(context.MoneySupply, Is.EqualTo(1000L));

        context.Progress(5d);
        Assert.That(context.CompanyBalance, Is.EqualTo(100L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(400L));

        int transactionsBeforeCompletion = context.TransactionCount;
        context.Progress(5d);
        Assert.That(context.CompanyBalance, Is.EqualTo(200L));
        Assert.That(context.CompletedBuildingBalance, Is.EqualTo(300L));
        Assert.That(context.CompletedBuildingOwner, Is.SameAs(context.Nation));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(0L));
        Assert.That(context.TransactionCount, Is.EqualTo(transactionsBeforeCompletion + 3),
            "Completion should register the building, settle fee and capital in one transfer, then release escrow.");
        Assert.That(context.MoneySupply, Is.EqualTo(1000L));
        context.Progress(10d);
        Assert.That(context.CompanyBalance, Is.EqualTo(200L));
        Assert.That(context.CompletedBuildingBalance, Is.EqualTo(300L));
    }

    [Test]
    public void Materials_GateStartLimitProgressAndAreConsumedExactlyAtCompletion()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 500L, initialCapital: 0L, constructionFee: 100L,
            timeToBuild: 10,
            requiredMaterials: new Dictionary<string, int>
            {
                ["Iron"] = 100,
                ["Wood"] = 10
            });
        object mandate = context.Place();

        context.Acquire(mandate, 30L, ("Iron", 30L), ("Wood", 2L));
        Assert.That(ReflectionTestHelpers.Get(mandate, "CanStart"), Is.False);
        Assert.That(context.Apply(mandate, 10d), Is.EqualTo(0d));

        context.Acquire(mandate, 5L, ("Wood", 1L));
        Assert.That(ReflectionTestHelpers.Get(mandate, "CanStart"), Is.True);
        Assert.That(ReflectionTestHelpers.Get(mandate, "MaterialProgressLimit"), Is.EqualTo(0.3m));
        Assert.That(context.Apply(mandate, 10d), Is.EqualTo(3d).Within(0.000001d));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Iron"), Is.EqualTo(30L));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Wood"), Is.EqualTo(3L));

        context.Acquire(mandate, 70L, ("Iron", 70L), ("Wood", 7L));
        Assert.That(context.Apply(mandate, 10d), Is.EqualTo(7d).Within(0.000001d));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Iron"), Is.EqualTo(100L));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Wood"), Is.EqualTo(10L));
        Assert.That(ReflectionTestHelpers.Get(mandate, "MaterialSpending"), Is.EqualTo(105L));
        Assert.That(ReflectionTestHelpers.Get(mandate, "Status").ToString(), Is.EqualTo("Completed"));
    }

    [Test]
    public void RepeatingMaterialRatio_NeverConsumesMoreThanWasAcquired()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 10L, initialCapital: 0L, constructionFee: 0L,
            timeToBuild: 10, startMaterialBasisPoints: 3333,
            requiredMaterials: new Dictionary<string, int> { ["Iron"] = 3 });
        object mandate = context.Place();
        context.Acquire(mandate, 1L, ("Iron", 1L));

        Assert.That(context.Apply(mandate, 10d), Is.EqualTo(10d / 3d).Within(0.000001d));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Iron"), Is.EqualTo(1L));
        Assert.That(context.MaterialAmount(mandate, "AcquiredMaterials", "Iron"), Is.EqualTo(1L));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    [TestCase(-1d)]
    public void InvalidManhours_DoNotChangeProgressPaymentOrConsumption(double manhours)
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 100L, initialCapital: 0L, constructionFee: 100L,
            timeToBuild: 10);
        object mandate = context.Place();

        Assert.That(context.Apply(mandate, manhours), Is.EqualTo(0d));
        Assert.That(ReflectionTestHelpers.Get(mandate, "RemainingManhours"), Is.EqualTo(10d));
        Assert.That(ReflectionTestHelpers.Get(mandate, "PaidConstructionFee"), Is.EqualTo(0L));
        Assert.That(context.CompanyBalance, Is.EqualTo(0L));
    }

    [Test]
    public void FailedProgressPayment_PreservesProgressMaterialsAndBuildingState()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 200L, initialCapital: 100L, constructionFee: 100L,
            timeToBuild: 10,
            requiredMaterials: new Dictionary<string, int> { ["Iron"] = 10 });
        object mandate = context.Place();
        context.Acquire(mandate, 0L, ("Iron", 10L));
        context.DrainEscrow(mandate, 150L);
        int transactionsBeforeProgress = context.TransactionCount;

        Assert.That(context.Apply(mandate, 10d), Is.EqualTo(0d));
        Assert.That(ReflectionTestHelpers.Get(mandate, "RemainingManhours"), Is.EqualTo(10d));
        Assert.That(ReflectionTestHelpers.Get(mandate, "PaidConstructionFee"), Is.EqualTo(0L));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Iron"), Is.EqualTo(0L));
        Assert.That(context.HasCompletedBuilding, Is.False);
        Assert.That(context.TransactionCount, Is.EqualTo(transactionsBeforeProgress + 2),
            "A failed completion may only register and release its empty candidate account.");
    }

    [Test]
    public void ContractSnapshot_IsUnaffectedByLaterRecipeMutation()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 500L, initialCapital: 100L, constructionFee: 200L,
            timeToBuild: 10,
            requiredMaterials: new Dictionary<string, int> { ["Iron"] = 100 });
        object mandate = context.Place();

        context.MutateRecipe(timeToBuild: 100, initialCapital: 400L,
            constructionFee: 900L, startMaterialBasisPoints: 10000,
            requiredMaterials: new Dictionary<string, int> { ["Iron"] = 1000 });
        context.Acquire(mandate, 30L, ("Iron", 30L));

        Assert.That(ReflectionTestHelpers.Get(mandate, "ConstructionFee"), Is.EqualTo(200L));
        Assert.That(context.MaterialAmount(mandate, "RequiredMaterials", "Iron"), Is.EqualTo(100L));
        Assert.That(context.Apply(mandate, 10d), Is.EqualTo(3d).Within(0.000001d));
        Assert.That(ReflectionTestHelpers.Get(mandate, "PaidConstructionFee"), Is.EqualTo(60L));
        Assert.That(context.CompanyBalance, Is.EqualTo(60L));
    }

    [Test]
    public void FractionalProgress_PaysCumulativeFloorAndFinalRemainder()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 100L, initialCapital: 0L, constructionFee: 100L,
            timeToBuild: 3);
        object mandate = context.Place();

        context.Apply(mandate, 1d);
        Assert.That(context.CompanyBalance, Is.EqualTo(33L));
        context.Apply(mandate, 1d);
        Assert.That(context.CompanyBalance, Is.EqualTo(66L));
        context.Apply(mandate, 1d);
        Assert.That(context.CompanyBalance, Is.EqualTo(100L));
        Assert.That(context.EscrowBalance(mandate), Is.Zero);
        Assert.That(context.MoneySupply, Is.EqualTo(100L));
    }

    [Test]
    public void FailedIntermediatePayment_DoesNotStartOrConsumeMaterials()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 100L, initialCapital: 0L, constructionFee: 100L,
            timeToBuild: 10,
            requiredMaterials: new Dictionary<string, int> { ["Iron"] = 10 });
        object mandate = context.Place();
        context.Acquire(mandate, 0L, ("Iron", 10L));
        context.DrainEscrow(mandate, 90L);
        int transactions = context.TransactionCount;

        Assert.That(context.Apply(mandate, 5d), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(mandate, "Status").ToString(), Is.EqualTo("Assigned"));
        Assert.That(ReflectionTestHelpers.Get(mandate, "RemainingManhours"), Is.EqualTo(10d));
        Assert.That(context.MaterialAmount(mandate, "ConsumedMaterials", "Iron"), Is.Zero);
        Assert.That(context.CompanyBalance, Is.Zero);
        Assert.That(context.TransactionCount, Is.EqualTo(transactions));
    }

    [Test]
    public void OwnerChangesBeforeCompletion_PreservesFeeCapitalAndLevel()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 500L, initialCapital: 100L, constructionFee: 100L,
            timeToBuild: 10, existingLevel: 1);
        object mandate = context.Place();
        Assert.That(context.Apply(mandate, 5d), Is.EqualTo(5d));
        context.SetExistingBuildingOwner(TestEconomyFactory.NewNation("Other", 0L));
        int transactions = context.TransactionCount;

        Assert.That(context.Apply(mandate, 5d), Is.Zero);
        Assert.That(ReflectionTestHelpers.Get(mandate, "RemainingManhours"), Is.EqualTo(5d));
        Assert.That(context.CompanyBalance, Is.EqualTo(50L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(150L));
        Assert.That(context.CompletedBuildingBalance, Is.EqualTo(100L));
        Assert.That(context.CompletedBuildingLevel, Is.EqualTo(1));
        Assert.That(context.TransactionCount, Is.EqualTo(transactions));
    }

    [Test]
    public void UnregisteredCompletionAccount_PreventsFinalFeeAsWellAsCapitalTransfer()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 100L, initialCapital: 0L, constructionFee: 100L,
            timeToBuild: 10, existingLevel: 1);
        object mandate = context.Place();
        object account = ReflectionTestHelpers.Get(context.CompletedBuilding, "Account");
        Assert.That(ReflectionTestHelpers.Call<bool>(context.Ledger,
            "UnregisterEmptyAccount", account), Is.True);
        int transactions = context.TransactionCount;

        Assert.That(context.Apply(mandate, 10d), Is.Zero);
        Assert.That(context.CompanyBalance, Is.Zero);
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(100L));
        Assert.That(context.CompletedBuildingLevel, Is.EqualTo(1));
        Assert.That(context.TransactionCount, Is.EqualTo(transactions));
    }

    [Test]
    public void AcquisitionPreparation_RejectsRequirementOverflowWithoutMutatingLedger()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 20L, initialCapital: 0L, constructionFee: 0L,
            timeToBuild: 10,
            requiredMaterials: new Dictionary<string, int> { ["Iron"] = 2 });
        object mandate = context.Place();

        Assert.That(context.TryAcquire(mandate, 10L, ("Iron", 3L)), Is.False);
        Assert.That(context.MaterialAmount(mandate, "AcquiredMaterials", "Iron"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(mandate, "MaterialSpending"), Is.EqualTo(0L));

        IDictionary required = (IDictionary)ReflectionTestHelpers.Get(mandate, "RequiredMaterials");
        Assert.Throws<NotSupportedException>(() => required["Iron"] = 99L);
    }

    [Test]
    public void DifferentOwnerExistingBuilding_RejectsUpgradeBeforeEscrowCreation()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 100L, initialCapital: 0L, constructionFee: 0L,
            timeToBuild: 10, existingLevel: 1);
        context.SetExistingBuildingOwner(TestEconomyFactory.NewNation("Other", 0L));
        int transactionsBeforePlacement = context.TransactionCount;

        Assert.That(context.Place(), Is.Null);
        Assert.That(context.CanPlaceError, Does.Contain("owner").IgnoreCase);
        Assert.That(context.TransactionCount, Is.EqualTo(transactionsBeforePlacement));
        Assert.That(context.CompletedBuildingLevel, Is.EqualTo(1));
    }

    [Test]
    public void CombinedEscrowOverflow_IsRejectedWithoutCreatingAMandate()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: long.MaxValue, initialCapital: long.MaxValue,
            constructionFee: 1L, timeToBuild: 10, withCompany: false);
        int transactionsBeforePlacement = context.TransactionCount;

        Assert.That(context.Place(), Is.Null);
        Assert.That(context.CanPlaceError, Does.Contain("Int64"));
        Assert.That(context.MandateCount, Is.EqualTo(0));
        Assert.That(context.TransactionCount, Is.EqualTo(transactionsBeforePlacement));
    }

    [Test]
    public void NationPlacement_RejectsRecipeMadeInvalidAfterInitialization()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 100L, initialCapital: 0L, constructionFee: 0L,
            timeToBuild: 10, withCompany: false);
        context.MutateRecipe(timeToBuild: 0, initialCapital: 0L,
            constructionFee: 0L, startMaterialBasisPoints: 3000,
            requiredMaterials: new Dictionary<string, int>());

        Assert.That(context.Place(), Is.Null);
        Assert.That(context.CanPlaceError, Does.Contain("TimeToBuild"));
        Assert.That(context.MandateCount, Is.EqualTo(0));
    }

    [Test]
    public void RequestedMandate_CancellationRefundsItsEntireEscrow()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 1000L, initialCapital: 300L, timeToBuild: 10,
            withCompany: false);
        object mandate = context.Place();

        Assert.That(ReflectionTestHelpers.Get(mandate, "Status").ToString(),
            Is.EqualTo("Requested"));
        Assert.That(ReflectionTestHelpers.Call<bool>(mandate, "Cancel"), Is.True);
        Assert.That(context.InvestorBalance, Is.EqualTo(1000L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(0L));
        Assert.That(context.MoneySupply, Is.EqualTo(1000L));
    }

    [Test]
    public void ZeroCapitalPlacement_RegistersAnEmptyEscrowWithoutTransferringMoney()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 0L, initialCapital: 0L, timeToBuild: 10,
            withCompany: false);

        object mandate = context.Place();

        Assert.That(mandate, Is.Not.Null);
        Assert.That(context.InvestorBalance, Is.EqualTo(0L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(mandate, "EscrowAccount"), "Ledger"),
            Is.SameAs(context.Ledger));
        Assert.That(context.MoneySupply, Is.EqualTo(0L));
    }

    [Test]
    public void InsufficientCapital_CreatesNoMandateOrMaterialReservation()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 299L, initialCapital: 300L, timeToBuild: 10,
            requiredMaterial: "Steel", materialStock: 2);
        int transactionsBeforePlacement = context.TransactionCount;

        Assert.That(context.Place(), Is.Null);
        Assert.That(context.MandateCount, Is.EqualTo(0));
        Assert.That(context.MaterialStock, Is.EqualTo(2));
        Assert.That(context.MoneySupply, Is.EqualTo(299L));
        Assert.That(context.TransactionCount, Is.EqualTo(transactionsBeforePlacement));
        Assert.That(context.CanPlaceError, Does.Contain("Need operating capital"));
    }

    [Test]
    public void Upgrade_TransfersOneRecipeCapitalToTheExistingBuilding()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 1000L, initialCapital: 300L, timeToBuild: 10,
            existingLevel: 1);
        object existingBuilding = context.CompletedBuilding;

        object mandate = context.Place();
        context.Progress(10d);

        Assert.That(ReferenceEquals(context.CompletedBuilding, existingBuilding), Is.True);
        Assert.That(context.CompletedBuildingLevel, Is.EqualTo(2));
        Assert.That(context.CompletedBuildingBalance, Is.EqualTo(600L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(0L));
        Assert.That(context.MoneySupply, Is.EqualTo(1000L));
    }

    private static IDictionary Recipes => StaticDictionary("BUILDING_RECIPE");
    private static IDictionary Adjacencies => StaticDictionary("ADJACENT_PROVINCES");

    private static IDictionary StaticDictionary(string fieldName) =>
        (IDictionary)ReflectionTestHelpers.Find("GlobalVariables").GetField(
            fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private sealed class FundedConstruction
    {
        private readonly object _nation;
        private readonly object _target;
        private readonly object _buildingType;
        private readonly object _company;
        private readonly object _ledger;
        private readonly string _requiredMaterial;

        private FundedConstruction(
            object nation,
            object target,
            object buildingType,
            object company,
            object ledger,
            string requiredMaterial)
        {
            _nation = nation;
            _target = target;
            _buildingType = buildingType;
            _company = company;
            _ledger = ledger;
            _requiredMaterial = requiredMaterial;
        }

        public long InvestorBalance => (long)ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(_nation, "Account"), "Balance");
        public long MoneySupply => (long)ReflectionTestHelpers.Get(_ledger, "MoneySupply");
        public object Ledger => _ledger;
        public object Nation => _nation;
        public int TransactionCount => ((ICollection)ReflectionTestHelpers.Get(
            _ledger, "Transactions")).Count;
        public int MandateCount => ((ICollection)ReflectionTestHelpers.Get(
            _nation, "ConstructionMandates")).Count;
        public long CompanyBalance => (long)ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(_company, "Account"), "Balance");
        public int MaterialStock => _requiredMaterial == null ? 0 : (int)ReflectionTestHelpers.Get(
            ((IDictionary)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(_target, "market"), "Products"))[_requiredMaterial], "Stock");
        public object CompletedBuilding => ((IDictionary)ReflectionTestHelpers.Get(
            _target, "buildings"))[_buildingType];
        public bool HasCompletedBuilding => ((IDictionary)ReflectionTestHelpers.Get(
            _target, "buildings")).Contains(_buildingType);
        public int CompletedBuildingLevel => (int)ReflectionTestHelpers.Get(
            CompletedBuilding, "level");
        public long CompletedBuildingBalance => (long)ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(CompletedBuilding, "Account"), "Balance");
        public object CompletedBuildingOwner => ReflectionTestHelpers.Get(
            CompletedBuilding, "Owner");

        public string CanPlaceError
        {
            get
            {
                object[] arguments = { _buildingType, _target, null };
                bool canPlace = (bool)ReflectionTestHelpers.Find("Nation").GetMethod(
                    "CanPlaceConstructionMandate").Invoke(_nation, arguments);
                Assert.That(canPlace, Is.False);
                return (string)arguments[2];
            }
        }

        public static FundedConstruction Create(
            long investorBalance,
            long initialCapital,
            int timeToBuild,
            long constructionFee = 0L,
            bool withCompany = true,
            int existingLevel = 0,
            string requiredMaterial = null,
            int materialStock = 0,
            int startMaterialBasisPoints = 3000,
            Dictionary<string, int> requiredMaterials = null)
        {
            object nation = TestEconomyFactory.NewNation("Builder", investorBalance);
            object target = TestEconomyFactory.NewProvince(1, "Target");
            ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", target);
            object type = ReflectionTestHelpers.New("BuildingType", "WheatField");
            object recipe = ReflectionTestHelpers.New("BuildingRecipe", "WheatField");
            ReflectionTestHelpers.Set(recipe, "TimeToBuild", timeToBuild);
            ReflectionTestHelpers.Set(recipe, "InitialCapital", initialCapital);
            ReflectionTestHelpers.Set(recipe, "ConstructionFee", constructionFee);
            ReflectionTestHelpers.Set(recipe, "StartMaterialBasisPoints", startMaterialBasisPoints);
            if (requiredMaterial != null)
                ((IDictionary)ReflectionTestHelpers.Get(recipe, "requireItems"))[requiredMaterial] = 1;
            if (requiredMaterials != null)
            {
                foreach (KeyValuePair<string, int> requirement in requiredMaterials)
                    ((IDictionary)ReflectionTestHelpers.Get(recipe, "requireItems"))[requirement.Key] = requirement.Value;
            }
            Recipes["WheatField"] = recipe;

            if (requiredMaterial != null)
            {
                object market = ReflectionTestHelpers.New("ProvinceMarket", "Target");
                ReflectionTestHelpers.Call<object>(market, "AddProduct", requiredMaterial, 1);
                object material = ((IDictionary)ReflectionTestHelpers.Get(
                    market, "Products"))[requiredMaterial];
                object supplier = ReflectionTestHelpers.New("MoneyAccount",
                    $"test:material:{requiredMaterial}", 0L);
                ReflectionTestHelpers.Call<object>(material, "AddSupply", supplier, materialStock);
                ReflectionTestHelpers.Set(target, "market", market);
            }

            if (existingLevel > 0)
            {
                object building = ReflectionTestHelpers.Find("BuildingFactory").GetMethod("Create")
                    .Invoke(null, new[] { type, target, (object)existingLevel, 0L });
                ((IDictionary)ReflectionTestHelpers.Get(target, "buildings"))[type] = building;
            }

            object company = null;
            IList provinces = (IList)TestEconomyFactory.ListOf("Province", target);
            if (withCompany)
            {
                object builderProvince = TestEconomyFactory.NewProvince(2, "BuilderProvince");
                ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", builderProvince);
                object companyType = ReflectionTestHelpers.New("BuildingType", "construcntionCompany");
                object companyRecipe = ReflectionTestHelpers.New("BuildingRecipe", "construcntionCompany");
                ReflectionTestHelpers.Set(companyRecipe, "InitialCapital", 0L);
                Recipes["construcntionCompany"] = companyRecipe;
                company = ReflectionTestHelpers.New("ConstructionCompanyBuilding", companyType, builderProvince, 1);
                ((IDictionary)ReflectionTestHelpers.Get(builderProvince, "buildings"))[companyType] = company;
                provinces.Add(builderProvince);
                IList adjacent = (IList)TestEconomyFactory.ListOf("Province", builderProvince);
                Adjacencies["Target"] = adjacent;
            }

            ReflectionTestHelpers.Find("EconomicInitializer").GetMethod("Initialize").Invoke(
                null, new[] { TestEconomyFactory.ListOf("Nation", nation), provinces });
            return new FundedConstruction(nation, target, type, company,
                ReflectionTestHelpers.Get(nation, "Ledger"), requiredMaterial);
        }

        public object Place() => ReflectionTestHelpers.Call<object>(
            _nation, "PlaceConstructionMandate", _buildingType, _target);

        public long EscrowBalance(object mandate) => (long)ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(mandate, "EscrowAccount"), "Balance");

        public void Progress(double manhours) => ReflectionTestHelpers.Call<object>(
            _company, "ProgressWeekly", manhours);

        public double Apply(object mandate, double manhours) =>
            ReflectionTestHelpers.Call<double>(mandate, "ApplyManhours", manhours);

        public bool TryAcquire(
            object mandate,
            long spending,
            params (string Name, long Amount)[] materials)
        {
            Dictionary<string, long> quantities = materials.ToDictionary(
                material => material.Name, material => material.Amount, StringComparer.Ordinal);
            MethodInfo prepare = mandate.GetType().GetMethod(
                "TryPrepareMaterialAcquisition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(prepare, Is.Not.Null);
            object[] arguments = { quantities, spending, null };
            bool prepared = (bool)prepare.Invoke(mandate, arguments);
            if (!prepared)
                return false;

            MethodInfo commit = mandate.GetType().GetMethod(
                "CommitMaterialAcquisition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(commit, Is.Not.Null);
            commit.Invoke(mandate, new[] { arguments[2] });
            return true;
        }

        public void Acquire(
            object mandate,
            long spending,
            params (string Name, long Amount)[] materials) =>
            Assert.That(TryAcquire(mandate, spending, materials), Is.True);

        public long MaterialAmount(object mandate, string ledgerName, string material) =>
            (long)((IDictionary)ReflectionTestHelpers.Get(mandate, ledgerName))[material];

        public void DrainEscrow(object mandate, long amount)
        {
            object escrow = ReflectionTestHelpers.Get(mandate, "EscrowAccount");
            object account = ReflectionTestHelpers.Get(_nation, "Account");
            Assert.That(ReflectionTestHelpers.Call<bool>(
                _ledger, "TryTransfer", escrow, account, amount, "Test escrow drain"), Is.True);
        }

        public void MutateRecipe(
            int timeToBuild,
            long initialCapital,
            long constructionFee,
            int startMaterialBasisPoints,
            Dictionary<string, int> requiredMaterials)
        {
            object recipe = Recipes["WheatField"];
            ReflectionTestHelpers.Set(recipe, "TimeToBuild", timeToBuild);
            ReflectionTestHelpers.Set(recipe, "InitialCapital", initialCapital);
            ReflectionTestHelpers.Set(recipe, "ConstructionFee", constructionFee);
            ReflectionTestHelpers.Set(recipe, "StartMaterialBasisPoints", startMaterialBasisPoints);
            IDictionary items = (IDictionary)ReflectionTestHelpers.Get(recipe, "requireItems");
            items.Clear();
            foreach (KeyValuePair<string, int> requirement in requiredMaterials)
                items[requirement.Key] = requirement.Value;
        }

        public void SetExistingBuildingOwner(object owner) =>
            ReflectionTestHelpers.Set(CompletedBuilding, "Owner", owner);
    }
}
