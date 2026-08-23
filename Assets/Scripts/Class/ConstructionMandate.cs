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

    public IBuildingInvestor Investor { get; }
    public BuildingType BuildingType { get; }
    public Province TargetProvince { get; }
    public MoneyAccount EscrowAccount { get; }
    public long InvestedCapital { get; }
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

        EscrowAccount = escrowAccount;
        InvestedCapital = investedCapital;
        RequiredManhours = Math.Max(1d, requiredManhours);
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
        if (availableManhours <= 0d ||
            (Status != ConstructionMandateStatus.Assigned &&
             Status != ConstructionMandateStatus.InProgress))
            return 0d;

        Status = ConstructionMandateStatus.InProgress;
        double spentManhours = Math.Min(availableManhours, RemainingManhours);
        RemainingManhours -= spentManhours;

        if (RemainingManhours <= 0d)
            Complete();

        return spentManhours;
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

    private bool Complete()
    {
        if (Status == ConstructionMandateStatus.Completed)
            return true;

        bool createdBuilding = false;
        if (!TargetProvince.buildings.TryGetValue(BuildingType, out Building building))
        {
            if (EscrowAccount == null)
            {
                building = BuildingFactory.Create(BuildingType, TargetProvince);
                TargetProvince.buildings[BuildingType] = building;
            }
            else if (!BuildingFactory.TryCreateAndRegister(BuildingType, TargetProvince, out building))
            {
                return false;
            }
            createdBuilding = true;
        }

        if (EscrowAccount != null)
        {
            if (TargetProvince.ActiveLedger == null ||
                building.Account.Ledger != TargetProvince.ActiveLedger)
            {
                RollBackCreatedBuilding(createdBuilding, building);
                return false;
            }

            if (!TryTransferEscrowTo(building.Account, "Construction mandate completion capitalization"))
            {
                RollBackCreatedBuilding(createdBuilding, building);
                return false;
            }

            if (!TryUnregisterTerminalEscrow())
                return false;
        }

        building.level++;
        RemainingManhours = 0d;
        Status = ConstructionMandateStatus.Completed;
        UntrackActiveMandate(this);
        return true;
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

    private void RollBackCreatedBuilding(bool createdBuilding, Building building)
    {
        if (!createdBuilding)
            return;

        TargetProvince.buildings.Remove(BuildingType);
        TargetProvince.ActiveLedger?.UnregisterEmptyAccount(building.Account);
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
