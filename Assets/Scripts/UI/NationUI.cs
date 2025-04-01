using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NationUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI �г�
    public TMP_Text nationNameText; // Nation �̸� ǥ�� �ؽ�Ʈ
    public Transform provinceListParent; // Province ����� �� �θ� ��ü
    public GameObject provinceItemPrefab; // Province ��ư ������

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
            DontDestroyOnLoad(gameObject);  // �� ���� �� ����
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
    /// Ư�� Nation�� UI�� ���� �׿� ���� Province ����� ǥ���մϴ�.
    /// </summary>
    public void OpenNationUI(Nation nation)
    {

        // Nation �̸� ǥ��
        nationNameText.text = nation.name;

        // ���� ����Ʈ ����
        foreach (Transform child in provinceListParent)
        {
            Destroy(child.gameObject);
        }

        // Nation�� ���� Province �߰�
        if (GlobalVariables.INITIAL_PROVINCES.TryGetValue(nation.name, out List<string> provinces))
        {
            foreach (string province in provinces)
            {
                GameObject newProvince = Instantiate(provinceItemPrefab, provinceListParent);
                newProvince.GetComponentInChildren<TMP_Text>().text = province;
            }
        }

        // UI Ȱ��ȭ
        uiPanel.SetActive(true);
    }

    /// <summary>
    /// Nation UI�� �ݽ��ϴ�.
    /// </summary>
    public void CloseNationUI()
    {
        uiPanel.SetActive(false);
    }
}
