using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ArmyUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI �г�

    //Province List ����
    public Transform regimentListParent; // Province ����� �� �θ� ��ü
    public GameObject regimentItemPrefab; // Province ��ư ������


    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;

    private int delayedUpdate;

    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
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
            //InitRegimentList();
        }
    }


    /// <summary>
    /// Ư�� Nation�� UI�� ���� �׿� ���� Province ����� ǥ���մϴ�.
    /// </summary>
    /// <param name="nation">������ ����</param>
    public void OpenArmyUI()
    {
        currentNation = GameManager.Instance.player.nation;

        InitRegimentList();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// Province�� ����� �ش� nation�� province��� �ʱ�ȭ�Ѵ�.
    /// </summary>
    public void InitRegimentList()
    {
        // ���� ����Ʈ ����
        foreach (Transform child in regimentListParent)
        {
            Destroy(child.gameObject);
        }

        // TODO: Nation�� ���� Regiment �߰�
        foreach(var regiment in currentNation.regiments)
        {
            GameObject newObject = Instantiate(regimentItemPrefab, regimentListParent);
            newObject.GetComponent<RegimentButtonUI>().SetRegimentData(currentNation, regiment);
        }
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
    public void CloseArmyUI()
    {
        uiPanel.SetActive(false);
    }
}
