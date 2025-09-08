using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ArmyUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI 패널

    //Province List 관련
    public Transform regimentListParent; // Province 목록이 들어갈 부모 객체
    public GameObject regimentItemPrefab; // Province 버튼 프리팹


    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;

    private int delayedUpdate;

    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static ArmyUI _instance;
    public static ArmyUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(ArmyUI)) as ArmyUI;

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
            //InitRegimentList();
        }
    }


    /// <summary>
    /// 특정 Nation의 UI를 열고 그에 속한 Province 목록을 표시합니다.
    /// </summary>
    /// <param name="nation">선택한 국가</param>
    public void OpenArmyUI()
    {
        currentNation = GameManager.Instance.player.nation;

        InitRegimentList();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// Province의 목록을 해당 nation의 province들로 초기화한다.
    /// </summary>
    public void InitRegimentList()
    {
        // 기존 리스트 정리
        foreach (Transform child in regimentListParent)
        {
            Destroy(child.gameObject);
        }

        // TODO: Nation에 속한 Regiment 추가
        foreach(var regiment in currentNation.regiments)
        {
            GameObject newObject = Instantiate(regimentItemPrefab, regimentListParent);
            newObject.GetComponent<RegimentButtonUI>().SetRegimentData(currentNation, regiment);
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
    public void CloseArmyUI()
    {
        uiPanel.SetActive(false);
    }
}
