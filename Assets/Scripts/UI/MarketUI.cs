using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MarketUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI 패널

    //Build List 관련
    public Transform MarketListParent; // Build 목록이 들어갈 부모 객체
    public GameObject MarketItemPrefab; // Build 버튼 프리팹


    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;


    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static MarketUI _instance;
    public static MarketUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(MarketUI)) as MarketUI;

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
        uiPanel.SetActive(false); // 처음에는 UI를 숨김
        currentNation = null;
        GameManager.Instance.dayEvent.AddListener(UpdateMarketUI);
    }

    private void Update()
    {
        //delayedUpdate = (delayedUpdate + 1) % 5;
        //if (delayedUpdate == 0 && currentNation != null)
        //{
        //    InitMarketList();
        //}
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayEvent.RemoveListener(UpdateMarketUI);
    }

    private void UpdateMarketUI()
    {
        InitMarketList();
    }

    /// <summary>
    /// 특정 Nation의 UI를 열고 그에 속한 Market 목록을 표시합니다.
    /// </summary>
    public void OpenMarketUI()
    {
        currentNation = GameManager.Instance.player.nation;

        InitMarketList();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// Market의 목록을 해당 nation의 market로 초기화한다.
    /// </summary>
    public void InitMarketList()
    {
        // 기존 리스트 정리
        foreach (Transform child in MarketListParent)
        {
            Destroy(child.gameObject);
        }

        // TODO: Nation에 속한 Market 추가
        

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
    public void CloseMarketUI()
    {
        uiPanel.SetActive(false);
    }
}
