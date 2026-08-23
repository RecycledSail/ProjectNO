using System;
using System.Collections;
using System.Collections.Generic;
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
    public void PlaceAndComplete_MovesCapitalThroughEscrowExactlyOnce()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 1000L, initialCapital: 300L, timeToBuild: 10);
        object mandate = context.Place();

        Assert.That(context.InvestorBalance, Is.EqualTo(700L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(300L));
        Assert.That(context.MoneySupply, Is.EqualTo(1000L));

        context.Progress(10d);
        Assert.That(context.CompletedBuildingBalance, Is.EqualTo(300L));
        Assert.That(context.EscrowBalance(mandate), Is.EqualTo(0L));
        Assert.That(context.MoneySupply, Is.EqualTo(1000L));
        context.Progress(10d);
        Assert.That(context.CompletedBuildingBalance, Is.EqualTo(300L));
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
    public void InsufficientCapital_CreatesNoMandateOrMaterialReservation()
    {
        FundedConstruction context = FundedConstruction.Create(
            investorBalance: 299L, initialCapital: 300L, timeToBuild: 10,
            requiredMaterial: "Steel", materialStock: 2);

        Assert.That(context.Place(), Is.Null);
        Assert.That(context.MandateCount, Is.EqualTo(0));
        Assert.That(context.MaterialStock, Is.EqualTo(2));
        Assert.That(context.MoneySupply, Is.EqualTo(299L));
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
        public int MandateCount => ((ICollection)ReflectionTestHelpers.Get(
            _nation, "ConstructionMandates")).Count;
        public int MaterialStock => _requiredMaterial == null ? 0 : (int)ReflectionTestHelpers.Get(
            ((IDictionary)ReflectionTestHelpers.Get(
                ReflectionTestHelpers.Get(_target, "market"), "Products"))[_requiredMaterial], "Stock");
        public object CompletedBuilding => ((IDictionary)ReflectionTestHelpers.Get(
            _target, "buildings"))[_buildingType];
        public int CompletedBuildingLevel => (int)ReflectionTestHelpers.Get(
            CompletedBuilding, "level");
        public long CompletedBuildingBalance => (long)ReflectionTestHelpers.Get(
            ReflectionTestHelpers.Get(CompletedBuilding, "Account"), "Balance");

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
            bool withCompany = true,
            int existingLevel = 0,
            string requiredMaterial = null,
            int materialStock = 0)
        {
            object nation = TestEconomyFactory.NewNation("Builder", investorBalance);
            object target = TestEconomyFactory.NewProvince(1, "Target");
            ReflectionTestHelpers.Call<bool>(nation, "AddProvinces", target);
            object type = ReflectionTestHelpers.New("BuildingType", "WheatField");
            object recipe = ReflectionTestHelpers.New("BuildingRecipe", "WheatField");
            ReflectionTestHelpers.Set(recipe, "TimeToBuild", timeToBuild);
            ReflectionTestHelpers.Set(recipe, "InitialCapital", initialCapital);
            if (requiredMaterial != null)
                ((IDictionary)ReflectionTestHelpers.Get(recipe, "requireItems"))[requiredMaterial] = 1;
            Recipes["WheatField"] = recipe;

            if (requiredMaterial != null)
            {
                object market = ReflectionTestHelpers.New("ProvinceMarket", "Target");
                ReflectionTestHelpers.Call<object>(market, "AddProduct", requiredMaterial, 1);
                ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))[requiredMaterial]
                    .GetType().GetField("Stock").SetValue(
                        ((IDictionary)ReflectionTestHelpers.Get(market, "Products"))[requiredMaterial], materialStock);
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
    }
}
