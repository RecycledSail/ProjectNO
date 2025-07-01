using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MarketUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI �г�

    //Build List ����
    public Transform MarketListParent; // Build ����� �� �θ� ��ü
    public GameObject MarketItemPrefab; // Build ��ư ������


    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;

    private int delayedUpdate;

    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
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
        uiPanel.SetActive(false); // ó������ UI�� ����
        currentNation = null;
        delayedUpdate = 0;
    }

    private void Update()
    {
        delayedUpdate = (delayedUpdate + 1) % 5;
        if (delayedUpdate == 0 && currentNation != null)
        {
            InitMarketList();
        }
    }


    /// <summary>
    /// Ư�� Nation�� UI�� ���� �׿� ���� Market ����� ǥ���մϴ�.
    /// </summary>
    public void OpenMarketUI()
    {
        currentNation = GameManager.Instance.player.nation;

        InitMarketList();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// Market�� ����� �ش� nation�� market�� �ʱ�ȭ�Ѵ�.
    /// </summary>
    public void InitMarketList()
    {
        // ���� ����Ʈ ����
        foreach (Transform child in MarketListParent)
        {
            Destroy(child.gameObject);
        }

        // TODO: Nation�� ���� Market �߰�

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
    public void CloseMarketUI()
    {
        uiPanel.SetActive(false);
    }
}
