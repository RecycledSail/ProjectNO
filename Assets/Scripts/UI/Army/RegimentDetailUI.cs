using TMPro;
using UnityEngine;

public class RegimentDetailUI : MonoBehaviour
{
    public GameObject uiPanel;
    public TMP_Text nameText; // ���� �̸� �ؽ�Ʈ
    public TMP_Text popText; // ���� �� �ؽ�Ʈ
    public TMP_Text hometownText;
    private Nation nationData; // ���� ������ �ؽ�Ʈ
    private Regiment regiment;
    public GameObject squadPrefab;
    public Transform squadParent;

    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
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
    /// ���� ���� �� �ʱ�ȭ �޼��� (�̱��� �ߺ����� ó��)
    /// </summary>
    private void Awake()
    {
        // �̱��� �ߺ� ���� ����
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // �ߺ� �� ����
        }
    }

    private void Start()
    {
        uiPanel.SetActive(false); // ó������ UI�� ����
    }

    

    private void Update()
    {
    }


    /// <summary>
    /// Regiment ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI

    }

    /// <summary>
    /// Regiment �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
    /// </summary>
    public void OpenRegimentDetail(Regiment regiment)
    {
        nameText.text = regiment.name;
        popText.text = regiment.GetUnitCount().ToString();
        // ���� ����Ʈ ����
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