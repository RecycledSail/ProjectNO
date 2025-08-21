using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    private List<Regiment> regiments;
    private Dictionary<Province, Battle> battleInProvinces;

    private static BattleManager _instance;
    public static BattleManager Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(BattleManager)) as BattleManager;

            return _instance;
        }
    }
    private void Awake()
    {
        // 싱글톤 중복 방지 로직
        if (_instance == null)
        {
            _instance = this;
            //DontDestroyOnLoad(gameObject);  // 씬 변경 시 유지
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // 중복 시 제거
        }
        regiments = new();
        battleInProvinces = new();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        InitiateBattle();
        UpdateBattles();
    }

    /// <summary>
    /// BattleManager가 관리하는 Regiment 리스트에 삽입
    /// </summary>
    /// <param name="regiment">삽입할 Regiment</param>
    public void AddRegiment(Regiment regiment)
    {
        regiments.Add(regiment);
    }

    /// <summary>
    /// BattleManager가 관리하는 Regiment 리스트에서 삭제
    /// </summary>
    /// <param name="regiment">삭제할 Regiment</param>
    /// <returns></returns>
    public bool RemoveRegiment(Regiment regiment)
    {
        return regiments.Remove(regiment);
    }

    /// <summary>
    /// 배틀 업데이트
    /// TODO: GameManager가 하루 지날때마다 이걸 실행하게 함
    /// </summary>
    private void UpdateBattles()
    {
        List<Province> provincesOnBattle = new List<Province>(battleInProvinces.Keys);
        foreach(Province province in provincesOnBattle)
        {
            Battle battle = battleInProvinces[province];
            CalculateBattlePerDay(province, battle);
        }
    }


    /// <summary>
    /// 매일의 Battle을 계산
    /// </summary>
    /// <param name="province">전투가 일어나는 프로빈스</param>
    /// <param name="battle">전투 그 자체</param>
    private void CalculateBattlePerDay(Province province, Battle battle)
    {
        double attackCapability = 0;
        double defenseCapability = 0;

        int attackUnitCount = 0;
        int defenseUnitCount = 0;
        foreach (Regiment regiment in battle.attackRegiments)
        {
            attackCapability += regiment.GetAttackPower();
            defenseCapability -= regiment.GetDefensePower();
            attackUnitCount += regiment.GetUnitCount();
        }

        foreach (Regiment regiment in battle.defenseRegiments)
        {
            defenseCapability += regiment.GetAttackPower();
            attackCapability -= regiment.GetDefensePower();
            defenseUnitCount += regiment.GetUnitCount();
        }
        attackCapability = attackCapability >= 0 ? attackCapability : 0;
        defenseCapability = defenseCapability >= 0 ? defenseCapability : 0;

        int attackCasulties = 0;
        int defenseCasulties = 0;
        foreach (Regiment regiment in battle.attackRegiments)
        {
            List<UnitType> unitTypes = new List<UnitType>(regiment.units.Keys);
            foreach (UnitType unitType in unitTypes)
            {
                double curCount = regiment.units[unitType].population;
                int remainingUnits = (int)curCount - (int)((curCount / attackUnitCount) * defenseCapability);
                if (remainingUnits <= 0)
                {
                    attackCasulties += (int)curCount;
                    regiment.units.Remove(unitType);
                }
                else
                {
                    attackCasulties += (int)curCount - remainingUnits;
                    regiment.units[unitType].SetPopulation(remainingUnits);
                }
            }
        }

        foreach (Regiment regiment in battle.defenseRegiments)
        {
            List<UnitType> unitTypes = new List<UnitType>(regiment.units.Keys);
            foreach (UnitType unitType in unitTypes)
            {
                double curCount = regiment.units[unitType].population;
                int remainingUnits = (int)curCount - (int)((curCount / defenseUnitCount) * attackCapability);
                if (remainingUnits <= 0)
                {
                    defenseCasulties += (int)curCount;
                    regiment.units.Remove(unitType);
                }
                else
                {
                    defenseCasulties += (int)curCount - remainingUnits;
                    regiment.units[unitType].SetPopulation(remainingUnits);
                }
            }
        }

        attackUnitCount -= attackCasulties;
        defenseUnitCount -= defenseCasulties;
        if(attackUnitCount == 0 || defenseUnitCount == 0)
        {
            Debug.Log("Battle on province " + province.name + " has ended");
            foreach (Regiment regiment in battle.attackRegiments)
            {
                regiment.state = RegimentState.IDLE;
                if (regiment.GetUnitCount() == 0)
                    regiments.Remove(regiment);
            }
            foreach (Regiment regiment in battle.defenseRegiments)
            {
                regiment.state = RegimentState.IDLE;
                if (regiment.GetUnitCount() == 0)
                    regiments.Remove(regiment);
            }
            battleInProvinces.Remove(province);
        }
    }

    /// <summary>
    /// 전투 개시 메서드
    /// IDLE 상태인 regiment A에 대해...
    /// 1. 현재 regiment A 위치에서 전투가 일어나고 있으면 참전
    /// 2. 다른 regiment가 같은 위치에 있으면 전투 새로 생성
    /// </summary>
    private void InitiateBattle()
    {
        foreach(Regiment regimentA in regiments)
        {
            if(regimentA.state == RegimentState.IDLE)
            {
                if (battleInProvinces.ContainsKey(regimentA.location))
                {
                    //TODO: 아군/적군 체크
                    Battle battle = battleInProvinces[regimentA.location];
                    battle.AddAttackRegiment(regimentA);
                    Debug.Log(regimentA.name + " has joined the battle on " + battle.battleArea.name);
                    regimentA.state = RegimentState.BATTLE;
                }
                else
                {
                    foreach (Regiment regimentB in regiments)
                    {
                        if (regimentA.nation != regimentB.nation && regimentA.location == regimentB.location && regimentB.state != RegimentState.BATTLE)
                        {
                            Battle newBattle = new(new() { regimentA }, new() { regimentB }, regimentA.location);
                            battleInProvinces[regimentA.location] = newBattle;
                            Debug.Log("The battle on " + newBattle.battleArea.name + " has started\nAttacker: " + regimentA.name + ", Defender: " + regimentB.name);
                            regimentA.state = RegimentState.BATTLE;
                            regimentB.state = RegimentState.BATTLE;
                        }
                    }
                }
            }
        }
    }
}
