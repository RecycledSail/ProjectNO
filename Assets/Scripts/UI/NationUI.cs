using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NationUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI �г�
    public TMP_Text nationNameText; // Nation �̸� ǥ�� �ؽ�Ʈ

    //Province List ����
    public Transform provinceListParent; // Province ����� �� �θ� ��ü
    public GameObject provinceItemPrefab; // Province ��ư ������

    //Diplomacy List ����
    public Transform allyListParent; // ���ͱ��� �� �θ� ��ü
    public Transform enemyListParent; // ������ �� �θ� ��ü
    public GameObject nationItemPrefab; // Nation ��ư ������

    //Nation stats ����
    public Transform nationStatsParent;
    public TMP_Text nationStatsText;

    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;

    private int delayedUpdate;

    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
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
        uiPanel.SetActive(false); // ó������ UI�� ����
        currentNation = null;
        delayedUpdate = 0;
    }

    private void Update()
    {
        delayedUpdate = (delayedUpdate + 1) % 5;
        if(delayedUpdate == 0 && currentNation != null)
        {
            InitNationStats();
        }
    }


    /// <summary>
    /// Ư�� Nation�� UI�� ���� �׿� ���� Province ����� ǥ���մϴ�.
    /// </summary>
    /// <param name="nation">������ ����</param>
    public void OpenNationUI(Nation nation)
    {

        // Nation �̸� ǥ��
        nationNameText.text = nation.name;
        currentNation = nation;

        InitProvinceList();
        InitDiplomacyStats();
        InitNationStats();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// �÷��̾��� Nation�� UI�� ���� �׿� ���� Province ����� ǥ���մϴ�.
    /// </summary>
    public void OpenNationUI()
    {
        Nation nation = GameManager.Instance.player.nation;
        OpenNationUI(nation);
    }

    /// <summary>
    /// Province�� ����� �ش� nation�� province��� �ʱ�ȭ�Ѵ�.
    /// </summary>
    public void InitProvinceList()
    {
        // ���� ����Ʈ ����
        foreach (Transform child in provinceListParent)
        {
            Destroy(child.gameObject);
        }

        // Nation�� ���� Province �߰�
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
    /// �� Ally ĭ�� Enemy ĭ�� ���ͱ�/�������� �ʱ�ȭ�Ѵ�.
    /// </summary>
    public void InitDiplomacyStats()
    {

        // ���� ����Ʈ ����
        for (int i = 1; i < allyListParent.childCount; i++)
        {
            Transform child = allyListParent.GetChild(1);
            Destroy(child.gameObject);
        }
        foreach (var allyNation in currentNation.allies)
        {
            GameObject newButton = Instantiate(nationItemPrefab, allyListParent);
            newButton.GetComponent<NationButtonUI>().SetProvinceData(allyNation.Key);
        }

        // ���� ����Ʈ ����
        for (int i = 1; i < enemyListParent.childCount; i++)
        {
            Transform child = enemyListParent.GetChild(1);
            Destroy(child.gameObject);
        }
        foreach (var enemyNation in currentNation.enemies)
        {
            GameObject newButton = Instantiate(nationItemPrefab, enemyListParent);
            newButton.GetComponent<NationButtonUI>().SetProvinceData(enemyNation.Key);
        }
    }

    /// <summary>
    /// Province�� ����� �ش� nation�� province��� �ʱ�ȭ�Ѵ�.
    /// </summary>
    public void InitNationStats()
    {
        string text = "";
        text += "Population: " + currentNation.GetPopulation() + "\n";
        text += "Currency: " + currentNation.balance + "\n";

        nationStatsText.text = text;
    }

    /// <summary>
    /// ���� Ȱ��ȭ�� SubUI�� �����Ѵ�.
    /// </summary>
    /// <param name="index">������ SubUI�� index</param>
    public void ChangeSubUI(int index)
    {
        currentOpenSubUI.SetActive(false);
        subUIs[index].SetActive(true);
        currentOpenSubUI = subUIs[index];
    }

    /// <summary>
    /// Nation UI�� �ݽ��ϴ�.
    /// </summary>
    public void CloseNationUI()
    {
        uiPanel.SetActive(false);
    }
}
