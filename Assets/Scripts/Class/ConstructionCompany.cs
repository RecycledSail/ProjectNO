using System.Collections.Generic;
using System.Linq;

public class ConstructionCompany
{
    public string name;
    public Nation owner;
    public Province baseProvince;

    // 동시 공사 가능 개수(=회사 레벨)
    public int level;

    // slotIndex -> BuildingInProgress
    private readonly Dictionary<int, BuildingInProgress> _active = new Dictionary<int, BuildingInProgress>();

    public ConstructionCompany(string name, Nation owner, Province baseProvince, int level)
    {
        this.name = name;
        this.owner = owner;
        this.baseProvince = baseProvince;
        this.level = level;
    }

    public bool HasFreeSlot => _active.Count < level;

    public IReadOnlyList<BuildingInProgress> ActiveProjects => _active.Values.ToList();

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

        // BuildingInProgress의 confirmindex가 float이라 그대로 넣어줌
        bip = new BuildingInProgress(
            confirmindex: slot,
            ConstructionConfirmLocation: this.baseProvince,
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
