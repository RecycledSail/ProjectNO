using System.Collections.Generic;
using System.Linq;

public class ConstructionCompanyBuilding : Building
{
    // slotIndex -> BuildingInProgress
    private readonly Dictionary<int, BuildingInProgress> _active = new();

    // Building.level을 "동시 공사 가능 슬롯 수"로 그대로 사용
    public bool HasFreeSlot => _active.Count < level;

    public IReadOnlyList<BuildingInProgress> ActiveProjects => _active.Values.ToList();

    // 이 건설회사의 "본거지"는 곧 이 빌딩이 있는 프로빈스
    public Province BaseProvince => province;

    // 회사 이름(원하면 별도로)
    public string companyName;

    public ConstructionCompanyBuilding(
        string companyName,
        BuildingType buildingType,
        Province province,
        int companyLevel
    ) : base(buildingType, province)
    {
        this.companyName = companyName;
        this.level = companyLevel;
    }

    public bool TryAssign(BuildingReservation reservation, out BuildingInProgress bip)
    {
        bip = null;
        if (!HasFreeSlot) return false;

        // 빈 슬롯 찾기: 0..level-1
        int slot = -1;
        for (int i = 0; i < level; i++)
        {
            if (!_active.ContainsKey(i))
            {
                slot = i;
                break;
            }
        }
        if (slot < 0) return false;

        bip = new BuildingInProgress(
            slotIndex: slot,
            ConstructionConfirmLocation: this.BaseProvince,
            buildingrequest: reservation
        );

        _active[slot] = bip;
        return true;
    }

    public bool TryFinish(int slotIndex)
    {
        return _active.Remove(slotIndex);
    }
}

