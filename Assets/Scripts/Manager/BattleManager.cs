using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BattleManager : MonoBehaviour
{
    private const string EditorRegimentPrefabPath = "Assets/Prefabs/GameObject/Cube.prefab";
    private const double BattleDamageScale = 100.0;

    private List<Regiment> regiments;
    private Dictionary<Province, Battle> battleInProvinces;
    private readonly Dictionary<Regiment, GameObject> regimentMarkers = new();
    private Transform provinceRoot;
    private GameObject regimentPrefab;
    private float markerGroundClearance = 10f;
    private float sameProvinceMarkerPadding = 10f;

    private static BattleManager _instance;
    public static BattleManager Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(BattleManager)) as BattleManager;

            if (!_instance)
            {
                GameObject battleManagerObject = new GameObject("BattleManager");
                _instance = battleManagerObject.AddComponent<BattleManager>();
            }

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
        if (GameManager.Instance != null && GameManager.Instance.dayUIEvent != null)
        {
            GameManager.Instance.dayUIEvent.AddListener(UpdateBattleEvent);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //InitiateBattle();
        //UpdateBattles();
    }

    void UpdateBattleEvent()
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
        if (regiment == null || regiments.Contains(regiment))
        {
            return;
        }

        regiments.Add(regiment);
    }

    public void AddRegiments(IEnumerable<Regiment> newRegiments)
    {
        if (newRegiments == null)
        {
            return;
        }

        foreach (Regiment regiment in newRegiments)
        {
            AddRegiment(regiment);
        }
    }

    public void RefreshRegimentMarkers()
    {
        ClearRegimentMarkers();
        ResolveMarkerDependencies();

        if (provinceRoot == null || regimentPrefab == null)
        {
            Debug.LogWarning("Regiment map markers could not initialize. Province root or regiment prefab was not found.");
            return;
        }

        Dictionary<Province, int> provinceMarkerCounts = new();
        foreach (Regiment regiment in regiments)
        {
            CreateRegimentMarker(regiment, provinceMarkerCounts);
        }
    }

    private void CreateRegimentMarker(Regiment regiment, Dictionary<Province, int> provinceMarkerCounts)
    {
        if (regiment == null || regiment.location == null)
        {
            return;
        }

        Transform provinceTransform = FindProvinceTransform(regiment.location.name);
        if (provinceTransform == null)
        {
            Debug.LogWarning("Could not find province object for regiment marker: " + regiment.location.name);
            return;
        }

        provinceMarkerCounts.TryGetValue(regiment.location, out int provinceIndex);
        provinceMarkerCounts[regiment.location] = provinceIndex + 1;

        GameObject marker = CreateRegimentMarkerObject();
        if (marker == null)
        {
            Debug.LogWarning("Could not create regiment marker object: " + regiment.name);
            return;
        }

        marker.name = regiment.name + " Marker";
        marker.SetActive(true);
        marker.transform.rotation = Quaternion.identity;
        marker.transform.position = GetRegimentMarkerPosition(provinceTransform, marker, provinceIndex);

        RegimentUI regimentUI = marker.GetComponent<RegimentUI>();
        if (regimentUI != null)
        {
            regimentUI.SetRegiment(regiment);
        }

        regimentMarkers[regiment] = marker;
    }

    private GameObject CreateRegimentMarkerObject()
    {
        if (regimentPrefab == null)
        {
            return null;
        }

        GameObject marker;
#if UNITY_EDITOR
        if (EditorUtility.IsPersistent(regimentPrefab))
        {
            marker = PrefabUtility.InstantiatePrefab(regimentPrefab) as GameObject;
        }
        else
        {
            marker = UnityEngine.Object.Instantiate(regimentPrefab);
        }
#else
        marker = UnityEngine.Object.Instantiate(regimentPrefab);
#endif

        if (marker == null)
        {
            return null;
        }

        marker.transform.SetParent(transform, true);
        return marker;
    }

    private Vector3 GetRegimentMarkerPosition(Transform provinceTransform, GameObject marker, int provinceIndex)
    {
        Bounds provinceBounds = GetWorldBounds(provinceTransform);
        Bounds markerBounds = GetWorldBounds(marker.transform);
        Vector3 position = provinceBounds.center;
        position.y = provinceBounds.max.y + markerBounds.extents.y + markerGroundClearance;

        int side = provinceIndex % 2 == 0 ? 1 : -1;
        int ring = (provinceIndex + 1) / 2;
        float markerSpacing = markerBounds.size.x + sameProvinceMarkerPadding;
        position += Vector3.right * side * ring * markerSpacing;

        return position;
    }

    private Bounds GetWorldBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        return new Bounds(target.position, Vector3.zero);
    }

    private Transform FindProvinceTransform(string provinceName)
    {
        if (provinceRoot != null && provinceRoot.name == provinceName)
        {
            return provinceRoot;
        }

        Transform match = provinceRoot == null ? null : FindChildRecursive(provinceRoot, provinceName);
        if (match != null)
        {
            return match;
        }

        GameObject provinceObject = GameObject.Find(provinceName);
        return provinceObject == null ? null : provinceObject.transform;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform match = FindChildRecursive(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void ResolveMarkerDependencies()
    {
        if (provinceRoot == null || !ProvinceRootContainsKnownProvince())
        {
            SelectProvince3D provinceSelector = FindFirstObjectByType<SelectProvince3D>();
            if (provinceSelector != null)
            {
                provinceRoot = provinceSelector.transform;
            }
        }

        if (regimentPrefab == null)
        {
            regimentPrefab = Resources.Load<GameObject>("RegimentMarker");
        }

#if UNITY_EDITOR
        if (regimentPrefab == null)
        {
            regimentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorRegimentPrefabPath);
        }
#endif

        LogResolvedMarkerDependencies();
    }

    private bool ProvinceRootContainsKnownProvince()
    {
        if (provinceRoot == null)
        {
            return false;
        }

        foreach (Province province in GlobalVariables.PROVINCES.Values)
        {
            if (FindChildRecursive(provinceRoot, province.name) != null)
            {
                return true;
            }
        }

        return false;
    }

    private void LogResolvedMarkerDependencies()
    {
#if UNITY_EDITOR
        string prefabPath = regimentPrefab == null ? "null" : AssetDatabase.GetAssetPath(regimentPrefab);
#else
        string prefabPath = regimentPrefab == null ? "null" : regimentPrefab.name;
#endif
        string rootName = provinceRoot == null ? "null" : provinceRoot.name;
        Debug.Log("Regiment marker dependencies resolved. provinceRoot=" + rootName + ", prefab=" + prefabPath);
    }

    private void ClearRegimentMarkers()
    {
        foreach (GameObject marker in regimentMarkers.Values)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        regimentMarkers.Clear();
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

        int attackUnitCount = battle.GetAttackNumber();
        int defenseUnitCount = battle.GetDefenseNumber();

        if (attackUnitCount <= 0 || defenseUnitCount <= 0)
        {
            EndBattle(province, battle);
            return;
        }

        foreach (Regiment regiment in battle.attackRegiments)
        {
            attackCapability += regiment.GetAttackPower();
            defenseCapability -= regiment.GetDefensePower();
        }

        foreach (Regiment regiment in battle.defenseRegiments)
        {
            defenseCapability += regiment.GetAttackPower();
            attackCapability -= regiment.GetDefensePower();
        }
        attackCapability = attackCapability >= 0 ? attackCapability / BattleDamageScale : 0;
        defenseCapability = defenseCapability >= 0 ? defenseCapability / BattleDamageScale : 0;

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
            EndBattle(province, battle);
        }
    }

    private void EndBattle(Province province, Battle battle)
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

    /// <summary>
    /// 전투 개시 메서드
    /// IDLE 상태인 regiment A에 대해...
    /// 1. 현재 regiment A 위치에서 전투가 일어나고 있으면 참전
    /// 2. 다른 regiment가 같은 위치에 있고, 두 regiment의 nation이 적일 때 전투 새로 생성
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
                        if (regimentA.nation != regimentB.nation && regimentA.location == regimentB.location && regimentB.state != RegimentState.BATTLE &&
                            regimentA.nation.enemies.ContainsKey(regimentB.nation))
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
