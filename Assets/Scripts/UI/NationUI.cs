using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NationUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI 패널
    public TMP_Text nationNameText; // Nation 이름 표시 텍스트

    //Province List 관련
    public Transform provinceListParent; // Province 목록이 들어갈 부모 객체
    public GameObject provinceItemPrefab; // Province 버튼 프리팹

    //Nation stats 관련
    public Transform nationStatsParent;
    public TMP_Text nationStatsText;

    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;

    private int delayedUpdate;

    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static NationUI _instance;
    public static NationUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(NationUI)) as NationUI;

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
        for(int i=0; i<subUIs.Count; i++)
        {
            if(i != 0)
            {
                subUIs[i].SetActive(false);
            }
            else
            {
                subUIs[i].SetActive(true);
                currentOpenSubUI = subUIs[i];
            }
        }
        uiPanel.SetActive(false); // 처음에는 UI를 숨김
        currentNation = null;
        delayedUpdate = 0;
    }

    private void Update()
    {
        delayedUpdate = (delayedUpdate + 1) % 5;
        if(delayedUpdate == 0 && currentNation != null)
        {
            InitProvinceList();
            InitNationStats();
        }
    }


    /// <summary>
    /// 특정 Nation의 UI를 열고 그에 속한 Province 목록을 표시합니다.
    /// </summary>
    /// <param name="nation">선택한 국가</param>
    public void OpenNationUI(Nation nation)
    {

        // Nation 이름 표시
        nationNameText.text = nation.name;
        currentNation = nation;

        InitProvinceList();
        InitNationStats();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// Province의 목록을 해당 nation의 province들로 초기화한다.
    /// </summary>
    public void InitProvinceList()
    {
        // 기존 리스트 정리
        foreach (Transform child in provinceListParent)
        {
            Destroy(child.gameObject);
        }

        // Nation에 속한 Province 추가
        if (GlobalVariables.INITIAL_PROVINCES.TryGetValue(currentNation.name, out List<string> provinces))
        {
            foreach (string provinceName in provinces)
            {
                Province province = GlobalVariables.PROVINCES[provinceName]; // Assume this method exists

                GameObject newProvince = Instantiate(provinceItemPrefab, provinceListParent);
                ProvinceUI provinceUI = newProvince.GetComponent<ProvinceUI>();
                if (provinceUI != null && province != null)
                {
                    provinceUI.SetProvinceData(province);
                }
            }
        }
    }

    /// <summary>
    /// Province의 목록을 해당 nation의 province들로 초기화한다.
    /// </summary>
    public void InitNationStats()
    {
        string text = "";
        text += "Population: " + currentNation.GetPopulation() + "\n";
        text += "Currency: " + currentNation.balance + "\n";

        nationStatsText.text = text;
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
    public void CloseNationUI()
    {
        uiPanel.SetActive(false);
    }
}
