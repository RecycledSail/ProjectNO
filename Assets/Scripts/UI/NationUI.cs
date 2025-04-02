using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NationUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI 패널
    public TMP_Text nationNameText; // Nation 이름 표시 텍스트
    public Transform provinceListParent; // Province 목록이 들어갈 부모 객체
    public GameObject provinceItemPrefab; // Province 버튼 프리팹

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
        uiPanel.SetActive(false); // 처음에는 UI를 숨김
    }

    private void Update()
    {
    }
    /// <summary>
    /// 특정 Nation의 UI를 열고 그에 속한 Province 목록을 표시합니다.
    /// </summary>
    public void OpenNationUI(Nation nation)
    {

        // Nation 이름 표시
        nationNameText.text = nation.name;

        // 기존 리스트 정리
        foreach (Transform child in provinceListParent)
        {
            Destroy(child.gameObject);
        }

        // Nation에 속한 Province 추가
        if (GlobalVariables.INITIAL_PROVINCES.TryGetValue(nation.name, out List<string> provinces))
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

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// Nation UI를 닫습니다.
    /// </summary>
    public void CloseNationUI()
    {
        uiPanel.SetActive(false);
    }
}
