using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RegimentUI : MonoBehaviour
{
    //���� ���� ���̴� �̹���/�ؽ�Ʈ
    public TMP_Text unitPop;
    public Image statusImage;

    private Regiment regiment { get; set; }

    public int testPop;
    public string testName;

    public int testAttackPerUnit;
    public int testDefensePerUnit;

    private UnitType testUnitType;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(regiment == null)
        {
            CreateTestRegiment();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (CheckAliveRegiment())
            UpdateRegimentUI();
        else
            Destroy(gameObject);
    }

    private bool CheckAliveRegiment()
    {
        return regiment != null && regiment.GetUnitCount() > 0;
    }

    /// <summary>
    /// �׽�Ʈ�� Regiment ����
    /// �����δ� regiment�� �ν��Ͻ����� ���Թޱ� ������ �̰� �� ���� ������ �� ��
    /// </summary>
    private void CreateTestRegiment()
    {
        Nation nation = new(-1, testName, new());


        Province province;
        if (GlobalVariables.PROVINCES.ContainsKey("testProvince"))
        {
            province = GlobalVariables.PROVINCES["testProvince"];
        }
        else
        {
            province = new(-1, "testProvince", 10000, Topography.Mountain);
            GlobalVariables.PROVINCES["testProvince"] = province;
        }

        regiment = new(nation, -1, testName, province);
        testUnitType = new UnitType(-1, "TestUnit-" + testName, testAttackPerUnit, testDefensePerUnit, 1);
        regiment.units.Add(testUnitType, testPop);
        Debug.Log(BattleManager.Instance);
        BattleManager.Instance.AddRegiment(regiment);
    }

    private void UpdateRegimentUI()
    {
        int pop = 0;
        foreach(var unitType in regiment.units.Keys)
        {
            pop += regiment.units[unitType];
        }
        unitPop.text = pop.ToString();
    }
}
