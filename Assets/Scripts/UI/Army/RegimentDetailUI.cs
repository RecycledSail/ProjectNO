using TMPro;
using UnityEngine;

public class RegimentDetailUI : MonoBehaviour
{
    public GameObject uiPanel;
    public TMP_Text nameText; // 연대 이름 텍스트
    public TMP_Text popText; // 연대 수 텍스트
    public TMP_Text hometownText;
    private Nation nationData; // 연대 데이터 텍스트
    private Regiment regiment;
    public GameObject squadPrefab;
    public Transform squadParent;

    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static RegimentDetailUI _instance;
    public static RegimentDetailUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(RegimentDetailUI)) as RegimentDetailUI;

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
    /// Regiment 버튼이 클릭될 때 실행할 기능 (예: 상세 정보 표시).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }

    /// <summary>
    /// Regiment 데이터를 설정하고 UI를 업데이트합니다.
    /// </summary>
    public void OpenRegimentDetail(Regiment regiment)
    {
        nameText.text = regiment.name;
        popText.text = regiment.GetUnitCount().ToString();
        // 기존 리스트 정리
        foreach (Transform child in squadParent)
        {
            Destroy(child.gameObject);
        }
        foreach (Squad squad in regiment.units.Values)
        {
            GameObject newSquadUI = Instantiate(squadPrefab, squadParent);
            newSquadUI.GetComponent<SquadUI>().SetSquadData(squad);
        }
        UIManager.Instance.ReplacePopUp(gameObject);
    }
}