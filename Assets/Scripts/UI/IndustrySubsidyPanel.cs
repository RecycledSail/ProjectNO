using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 산업별 보조금 세부 배분 팝업.
/// FinanceUI의 Indus 슬라이더로 정한 총액 안에서
/// BuildingType별로 얼마씩 줄지 플레이어가 직접 설정합니다.
/// </summary>
public class IndustrySubsidyPanel : MonoBehaviour
{
    [Header("행 생성")]
    public Transform rowContainer;  // ScrollRect > Viewport > Content
    public GameObject rowPrefab;    // IndustrySubsidyRow 컴포넌트를 가진 프리팹

    [Header("합계 표시")]
    public TMP_Text budgetText;          // "배분 가능: 50,000"
    public TMP_Text totalText;           // "현재 합계: 30,000"
    public TMP_Text overBudgetWarning;   // 예산 초과 경고

    private Nation _nation;
    private GovernmentBudget Budget => _nation?.governmentBudget;

    private readonly List<IndustrySubsidyRow> _rows = new();

    private static IndustrySubsidyPanel _instance;
    public static IndustrySubsidyPanel Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType<IndustrySubsidyPanel>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) Destroy(gameObject);

        gameObject.SetActive(false);
    }

    // ─── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 패널을 열고 해당 국가의 산업 목록을 표시합니다.
    /// FinanceUI와 무관하게 독립적으로 호출할 수 있습니다.
    /// </summary>
    public void Open(Nation nation)
    {
        if (nation == null) return;
        _nation = nation;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 행의 입력값이 바뀔 때 IndustrySubsidyRow에서 호출합니다.
    /// Policy를 즉시 갱신하고 합계 표시를 업데이트합니다.
    /// </summary>
    public void OnRowAmountChanged()
    {
        if (Budget == null) return;

        Budget.Policy.IndustrySubsidy.Clear();
        foreach (IndustrySubsidyRow row in _rows)
        {
            long amount = row.GetAmount();
            if (amount > 0)
                Budget.Policy.IndustrySubsidy[row.TypeName] = amount;
        }

        RefreshTotalDisplay();
    }

    // ─── Private ──────────────────────────────────────────────────

    private void Refresh()
    {
        RebuildRows();
        RefreshTotalDisplay();
    }

    private void RebuildRows()
    {
        foreach (IndustrySubsidyRow row in _rows)
            if (row != null) Destroy(row.gameObject);
        _rows.Clear();

        if (_nation == null || rowPrefab == null || rowContainer == null) return;

        // BuildingType별 포화도·수익 집계
        var stats = new Dictionary<string, (float totalSat, int count, long totalGain)>();
        foreach (Province province in _nation.provinces)
        {
            foreach (var kv in province.buildings)
            {
                string key  = kv.Key.name;
                Building b  = kv.Value;
                long cap    = System.Math.Max(1L, b.level * b.buildingType.workerNeeded);
                float sat   = (float)b.currentWorkers / cap;

                if (stats.TryGetValue(key, out var s))
                    stats[key] = (s.totalSat + sat, s.count + 1, s.totalGain + b.previousGain);
                else
                    stats[key] = (sat, 1, b.previousGain);
            }
        }

        // 기존 Policy 값 보존 (이전에 입력한 값 유지)
        var existing = Budget?.Policy.IndustrySubsidy ?? new Dictionary<string, long>();

        foreach (var (typeName, data) in stats)
        {
            float avgSat = data.totalSat / data.count;
            existing.TryGetValue(typeName, out long prev);

            GameObject go = Instantiate(rowPrefab, rowContainer);
            IndustrySubsidyRow row = go.GetComponent<IndustrySubsidyRow>();
            row.Setup(typeName, avgSat, data.totalGain, prev, this);
            _rows.Add(row);
        }
    }

    private void RefreshTotalDisplay()
    {
        // FinanceUI가 설정한 총 산업 예산
        long budget = Budget?.Policy.IndustryTotal() ?? 0L;

        // 행 입력 합계
        long sum = 0;
        foreach (IndustrySubsidyRow row in _rows)
            sum += row.GetAmount();

        if (budgetText != null)
            budgetText.text = $"배분 가능: {budget:N0}";

        if (totalText != null)
            totalText.text = $"현재 합계: {sum:N0}";

        bool over = sum > budget;
        if (overBudgetWarning != null)
        {
            overBudgetWarning.gameObject.SetActive(over);
            if (over) overBudgetWarning.text = $"예산 {sum - budget:N0} 초과";
        }
        if (totalText != null)
            totalText.color = over ? Color.red : Color.white;
    }
}
