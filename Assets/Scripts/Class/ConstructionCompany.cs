using System;
using System.Collections.Generic;
using System.Linq;

public class ConstructionCompanyBuilding : Building
{
    private readonly Dictionary<int, ConstructionMandate> _active = new();

    // Building.level을 "동시 공사 가능 슬롯 수"로 그대로 사용
    public bool HasFreeSlot => _active.Count < level;

    public IReadOnlyList<ConstructionMandate> ActiveProjects => _active
        .OrderBy(pair => pair.Key)
        .Select(pair => pair.Value)
        .ToList();


    // 회사 이름(원하면 별도로)


    public ConstructionCompanyBuilding(

        BuildingType buildingType,
        Province province,
        int companyLevel
    ) : base(buildingType, province)
    {
        this.level = companyLevel;
    }

    public bool TryAssign(ConstructionMandate mandate)
    {
        if (mandate == null || !HasFreeSlot || _active.Values.Contains(mandate))
            return false;

        int slot = Enumerable.Range(0, level)
            .First(index => !_active.ContainsKey(index));

        if (!mandate.TryAssign(this))
            return false;

        _active[slot] = mandate;
        return true;
    }

    public void ProgressWeekly(double weeklyManhoursPerLevel)
    {
        double remainingManhours = Math.Max(0d, weeklyManhoursPerLevel) * level;

        foreach (int slot in _active.Keys.OrderBy(index => index).ToList())
        {
            ConstructionMandate mandate = _active[slot];
            if (!mandate.IsActive)
            {
                _active.Remove(slot);
                continue;
            }

            remainingManhours -= mandate.ApplyManhours(remainingManhours);

            if (!mandate.IsActive)
                _active.Remove(slot);

            if (remainingManhours <= 0d)
                break;
        }
    }
}

