using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RegimentUI : MonoBehaviour
{
    //눈에 직접 보이는 이미지/텍스트
    public TMP_Text unitPop;
    public Image statusImage;

    private Regiment regiment { get; set; }

    public int testPop;
    public string testName;

    public int testAttackPerUnit;
    public int testDefensePerUnit;

    private UnitType testUnitType;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if(regiment == null)
        {
            CreateTestRegiment();
        }
        else
        {
            GameManager.Instance.dayEvent.AddListener(CheckUpdateRegimentUI);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        //if (CheckAliveRegiment())
        //    UpdateRegimentUI();
        //else
        //    Destroy(gameObject);
    }

    private void CheckUpdateRegimentUI()
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
    /// 테스트용 Regiment 생성
    /// 실제로는 regiment를 인스턴스마다 주입받기 때문에 이걸 볼 일이 있으면 안 됨
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
            province = new(-1, "testProvince", Topography.Mountain);
            GlobalVariables.PROVINCES["testProvince"] = province;
        }

        regiment = new(nation, testName, province);
        testUnitType = new UnitType(-1, "TestUnit-" + testName, testAttackPerUnit, testDefensePerUnit, 1);
        regiment.units.Add(testUnitType, new Squad(testUnitType));
        Debug.Log(BattleManager.Instance);
        BattleManager.Instance.AddRegiment(regiment);
    }

    private void UpdateRegimentUI()
    {
        int pop = 0;
        foreach(var unitType in regiment.units.Keys)
        {
            pop += regiment.units[unitType].population;
        }
        unitPop.text = pop.ToString();
    }
}
