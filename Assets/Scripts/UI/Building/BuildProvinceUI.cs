using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static NUnit.Framework.Internal.OSPlatform;

public class BuildProvinceUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI 패널
    public TMP_Text buildingTypeText;


    //Build List 관련
    public Transform BuildListParent; // Build 목록이 들어갈 부모 객체
    public GameObject BuildItemPrefab; // Build 버튼 프리팹


    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;
    private BuildingType currentBuildingType;
    private Nation currentNation;

    private List<BuildingType> buildings;

    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static BuildProvinceUI _instance;
    public static BuildProvinceUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(BuildProvinceUI)) as BuildProvinceUI;

            return _instance;
        }
    }

    /// <summary>
    /// 게임 시작 전 초기화 메서드 (싱글톤 중복방지 처리)
    /// </summary>
    private void Awake()
    {
        // 싱글톤 중복 방지 로직
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // 중복 시 제거
        }
    }

    private void Start()
    {
        for (int i = 0; i < subUIs.Count; i++)
        {
            if (i != 0)
            {
                subUIs[i].SetActive(false);
            }
            else
            {
                subUIs[i].SetActive(true);
                currentOpenSubUI = subUIs[i];
            }
        }
        
        /*
        buildings = new();
        foreach(BuildingType buildingType in GlobalVariables.BUILDING_TYPE.Values)
        {
            buildings.Add(buildingType);
        }
        */
        currentBuildingType = null;
        currentNation = null;
        uiPanel.SetActive(false); // 처음에는 UI를 숨김
        GameManager.Instance.dayUIEvent.AddListener(UpdateBuildProvinceUI);
    }

    private void Update()
    {
    }

    private void OnDestroy()
    {
        if(GameManager.Instance != null)
        {
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateBuildProvinceUI);
        }
    }


    /// <summary>
    /// 특정 Nation의 UI를 열고 그에 속한 Province 목록을 표시합니다.
    /// </summary>
    /// <param name="nation">선택한 국가</param>
    public void OpenBuildProvinceUI(BuildingType buildingType)
    {
        currentNation = GameManager.Instance.player.nation;
        currentBuildingType = buildingType;
        buildingTypeText.text = buildingType.name;

        UpdateBuildProvinceUI();

        UIManager.Instance.ReplacePopUp(gameObject);
    }
    

    /// <summary>
    /// BuildProvinceUI를 업데이트한다.
    /// </summary>
    public void UpdateBuildProvinceUI()
    {
        if (currentNation != null)
        {
            UpdateBuildProvinceList();
        }
    }


    /// <summary>
    /// Province의 목록을 해당 nation의 province들로 초기화한다.
    /// </summary>
    public void UpdateBuildProvinceList()
    {
        // 1. 이전에 만들어뒀던 province-child 반환
        Dictionary<Province, Transform> prevChilds = new();
        foreach (Transform child in BuildListParent)
        {
            Province province = child.gameObject.GetComponent<BuildProvinceButtonUI>().Province;
            if (province != null)
                prevChilds.Add(province, child);
        }

        // 2. 현재 Province 반환
        foreach(Province province in currentNation.provinces)
        {
            if(prevChilds.ContainsKey(province))
                prevChilds.Remove(province);
            else
            {
                GameObject child = Instantiate(BuildItemPrefab, BuildListParent);
                BuildProvinceButtonUI bpbUI = child.GetComponent<BuildProvinceButtonUI>();
                bpbUI.SetBuildingData(province, currentBuildingType);
            }
        }

        // 3. 남은 prevChilds 제거
        foreach(Province province in prevChilds.Keys)
        {
            Destroy(prevChilds[province]);
        }
    }

    /// <summary>
    /// 현재 활성화된 SubUI를 변경한다.
    /// </summary>
    /// <param name="index">변경할 SubUI의 index</param>
    public void ChangeSubUI(int index)
    {
        currentOpenSubUI.SetActive(false);
        subUIs[index].SetActive(true);
        currentOpenSubUI = subUIs[index];
    }

    /// <summary>
    /// Nation UI를 닫습니다.
    /// </summary>
    public void CloseBuildUI()
    {
        uiPanel.SetActive(false);
    }
}
