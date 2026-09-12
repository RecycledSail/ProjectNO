using System;
using System.Collections.Generic;
using System.Linq;

public enum ConstructionMandateStatus
{
    Requested,
    Assigned,
    InProgress,
    Completed,
    Cancelled
}

public interface IBuildingInvestor
{
    MoneyAccount InvestmentAccount { get; }
}

public sealed class ConstructionMandate
{
    private static readonly Dictionary<Province, List<ConstructionMandate>> ActiveByProvince = new();
    private readonly ConstructionMaterials _materials;
    private readonly decimal _requiredManhours;
    private decimal _completedManhours;

    public string Id { get; }
    public IBuildingInvestor Investor { get; }
    public BuildingType BuildingType { get; }
    public Province TargetProvince { get; }
    public MoneyAccount EscrowAccount { get; }
    public long InvestedCapital { get; }
    public long ConstructionFee { get; }
    public long PaidConstructionFee { get; private set; }
    public long MaterialSpending => _materials.Spending;
    public decimal MaterialProgressLimit => _materials.ProgressLimit;
    public bool CanStart => IsActive && (Status == ConstructionMandateStatus.InProgress || _materials.CanStart);
    public IReadOnlyDictionary<string, long> RequiredMaterials => _materials.Required;
    public IReadOnlyDictionary<string, long> AcquiredMaterials => _materials.Acquired;
    public IReadOnlyDictionary<string, long> ConsumedMaterials => _materials.Consumed;
    public ConstructionCompanyBuilding AssignedCompany { get; private set; }
    public double RequiredManhours { get; }
    public double RemainingManhours { get; private set; }
    public ConstructionMandateStatus Status { get; private set; }

    public bool IsActive => Status != ConstructionMandateStatus.Completed &&
                            Status != ConstructionMandateStatus.Cancelled;

    public ConstructionMandate(
        IBuildingInvestor investor,
        BuildingType buildingType,
        Province targetProvince,
        double requiredManhours)
        : this(investor, buildingType, targetProvince, requiredManhours, null, 0L)
    {
    }

    public ConstructionMandate(
        IBuildingInvestor investor,
        BuildingType buildingType,
        Province targetProvince,
        double requiredManhours,
        MoneyAccount escrowAccount,
        long investedCapital)
    {
        Investor = investor;
        BuildingType = buildingType ?? throw new ArgumentNullException(nameof(buildingType));
        TargetProvince = targetProvince ?? throw new ArgumentNullException(nameof(targetProvince));
        if (investedCapital < 0)
            throw new ArgumentOutOfRangeException(nameof(investedCapital));
        if (investedCapital > 0 && escrowAccount == null)
            throw new ArgumentNullException(nameof(escrowAccount));

        if (double.IsNaN(requiredManhours) || double.IsInfinity(requiredManhours) ||
            requiredManhours > (double)decimal.MaxValue ||
            (escrowAccount != null && requiredManhours <= 0d))
            throw new ArgumentOutOfRangeException(nameof(requiredManhours));

        GlobalVariables.BUILDING_RECIPE.TryGetValue(buildingType.name, out BuildingRecipe recipe);
        if (escrowAccount != null)
        {
            if (recipe == null)
                throw new InvalidOperationException("A funded construction contract requires a recipe.");
            recipe.ValidateConstructionContract();
            if (recipe.InitialCapital < 0)
                throw new InvalidOperationException("Operating capital must be nonnegative.");
            _ = checked(investedCapital + recipe.ConstructionFee);
        }

        Id = escrowAccount?.Id ?? $"mandate:{Guid.NewGuid():N}";
        EscrowAccount = escrowAccount;
        InvestedCapital = investedCapital;
        // Legacy unbacked mandates retain their free construction behavior.
        ConstructionFee = escrowAccount == null ? 0L : recipe.ConstructionFee;
        _materials = new ConstructionMaterials(recipe);
        RequiredManhours = Math.Max(1d, requiredManhours);
        _requiredManhours = (decimal)RequiredManhours;
        RemainingManhours = RequiredManhours;
        Status = ConstructionMandateStatus.Requested;
        TrackActiveMandate(this);
    }

    public bool TryAssign(ConstructionCompanyBuilding company)
    {
        if (company == null || Status != ConstructionMandateStatus.Requested)
            return false;

        AssignedCompany = company;
        Status = ConstructionMandateStatus.Assigned;
        return true;
    }

    public double ApplyManhours(double availableManhours)
    {
        if (double.IsNaN(availableManhours) || double.IsInfinity(availableManhours) ||
            availableManhours <= 0d || !CanStart ||
            (Status != ConstructionMandateStatus.Assigned &&
             Status != ConstructionMandateStatus.InProgress))
            return 0d;

        decimal remaining = _requiredManhours - _completedManhours;
        decimal offered = availableManhours >= (double)remaining ? remaining : (decimal)availableManhours;
        decimal completed = Math.Min(_completedManhours + offered,
            _requiredManhours * MaterialProgressLimit);
        if (completed <= _completedManhours)
            return 0d;

        decimal progress = Math.Min(1m, completed / _requiredManhours);
        bool completing = completed == _requiredManhours;
        long cumulativePayment = completing ? ConstructionFee :
            decimal.ToInt64(decimal.Floor(ConstructionFee * progress));
        long payment = checked(cumulativePayment - PaidConstructionFee);
        Dictionary<string, long> consumption = _materials.PrepareConsumption(progress);

        if (TargetProvince.buildings.TryGetValue(BuildingType, out Building building) &&
            (!ReferenceEquals(building.Owner, Investor) || (completing && building.level == int.MaxValue)))
            return 0d;

        bool createdBuilding = false;
        if (completing && building == null)
        {
            building = BuildingFactory.Create(BuildingType, TargetProvince);
            if (EscrowAccount != null &&
                (TargetProvince.ActiveLedger == null ||
                 !TargetProvince.ActiveLedger.RegisterEmptyAccount(building.Account)))
                return 0d;
            createdBuilding = true;
        }

        if (!TrySettleProgress(payment, completing, building))
        {
            if (createdBuilding && EscrowAccount != null)
                TargetProvince.ActiveLedger.UnregisterEmptyAccount(building.Account);
            return 0d;
        }

        double spentManhours = (double)(completed - _completedManhours);
        _completedManhours = completed;
        RemainingManhours = (double)(_requiredManhours - completed);
        PaidConstructionFee = cumulativePayment;
        _materials.CommitConsumption(consumption);
        Status = completing ? ConstructionMandateStatus.Completed : ConstructionMandateStatus.InProgress;
        if (completing)
        {
            building.Owner = Investor;
            building.level++;
            if (createdBuilding)
                TargetProvince.buildings.Add(BuildingType, building);
            // Settlement checked registration and the exact terminal debit.
            TryUnregisterTerminalEscrow();
            UntrackActiveMandate(this);
        }
        return spentManhours;
    }

    internal bool TryPrepareMaterialAcquisition(IReadOnlyDictionary<string, long> quantities,
        long spending, out ConstructionMaterials.Acquisition acquisition)
    {
        acquisition = null;
        return IsActive && _materials.TryPrepareAcquisition(quantities, spending, out acquisition);
    }

    internal void CommitMaterialAcquisition(ConstructionMaterials.Acquisition acquisition)
    {
        if (!IsActive)
            throw new InvalidOperationException("A terminal construction contract cannot acquire materials.");
        _materials.CommitAcquisition(acquisition);
    }

    public bool Cancel()
    {
        if (!IsActive)
            return false;

        if (!TryTransferEscrowTo(Investor?.InvestmentAccount, "Construction mandate cancellation refund"))
            return false;

        if (!TryUnregisterTerminalEscrow())
            return false;

        Status = ConstructionMandateStatus.Cancelled;
        UntrackActiveMandate(this);
        return true;
    }

    internal static IEnumerable<MoneyAccount> GetActiveEscrowAccountsFor(Province province)
    {
        if (province == null || !ActiveByProvince.TryGetValue(province, out List<ConstructionMandate> mandates))
            return Enumerable.Empty<MoneyAccount>();

        return mandates
            .Where(mandate => mandate.IsActive && mandate.EscrowAccount != null)
            .Select(mandate => mandate.EscrowAccount)
            .ToList();
    }

    private bool TrySettleProgress(long payment, bool completing, Building building)
    {
        if (EscrowAccount == null)
            return true;

        MoneyLedger ledger = TargetProvince.ActiveLedger;
        if (ledger == null || !ledger.OwnsAccount(EscrowAccount) ||
            !ledger.OwnsAccount(Investor?.InvestmentAccount) ||
            !ledger.OwnsAccount(AssignedCompany?.Account) ||
            (completing && !ledger.OwnsAccount(building?.Account)))
            return false;

        long capital = completing ? InvestedCapital : 0L;
        long debit = checked(payment + capital);
        if (completing && EscrowAccount.Balance != debit)
            return false;
        if (debit == 0)
            return true;

        List<MoneyTransferEntry> entries = new() { new MoneyTransferEntry(EscrowAccount, -debit) };
        if (payment > 0)
            entries.Add(new MoneyTransferEntry(AssignedCompany.Account, payment));
        if (capital > 0)
            entries.Add(new MoneyTransferEntry(building.Account, capital));
        return ledger.TryTransferBatch(entries, "Construction progress fee and completion capital");
    }

    private bool TryTransferEscrowTo(MoneyAccount destination, string reason)
    {
        if (EscrowAccount == null || EscrowAccount.Balance == 0)
            return true;

        MoneyLedger ledger = TargetProvince.ActiveLedger;
        return destination != null && ledger != null && ledger.TryTransfer(
            EscrowAccount, destination, EscrowAccount.Balance, reason);
    }

    private bool TryUnregisterTerminalEscrow()
    {
        if (EscrowAccount == null)
            return true;

        MoneyLedger ledger = EscrowAccount.Ledger;
        return EscrowAccount.Balance == 0 &&
               (ledger == null || ledger.UnregisterEmptyAccount(EscrowAccount));
    }

    private static void TrackActiveMandate(ConstructionMandate mandate)
    {
        if (!ActiveByProvince.TryGetValue(mandate.TargetProvince, out List<ConstructionMandate> mandates))
        {
            mandates = new List<ConstructionMandate>();
            ActiveByProvince.Add(mandate.TargetProvince, mandates);
        }

        mandates.Add(mandate);
    }

    private static void UntrackActiveMandate(ConstructionMandate mandate)
    {
        if (!ActiveByProvince.TryGetValue(mandate.TargetProvince, out List<ConstructionMandate> mandates))
            return;

        mandates.Remove(mandate);
        if (mandates.Count == 0)
            ActiveByProvince.Remove(mandate.TargetProvince);
    }
}
