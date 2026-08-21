using System;

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
}

public sealed class ConstructionMandate
{
    public IBuildingInvestor Investor { get; }
    public BuildingType BuildingType { get; }
    public Province TargetProvince { get; }
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
    {
        Investor = investor;
        BuildingType = buildingType ?? throw new ArgumentNullException(nameof(buildingType));
        TargetProvince = targetProvince ?? throw new ArgumentNullException(nameof(targetProvince));
        RequiredManhours = Math.Max(1d, requiredManhours);
        RemainingManhours = RequiredManhours;
        Status = ConstructionMandateStatus.Requested;
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

        Status = ConstructionMandateStatus.Cancelled;
        return true;
    }

    private void Complete()
    {
        if (Status == ConstructionMandateStatus.Completed)
            return;

        if (!TargetProvince.buildings.TryGetValue(BuildingType, out Building building))
        {
            building = BuildingFactory.Create(BuildingType, TargetProvince);
            TargetProvince.buildings[BuildingType] = building;
        }

        building.level++;
        RemainingManhours = 0d;
        Status = ConstructionMandateStatus.Completed;
    }
}
