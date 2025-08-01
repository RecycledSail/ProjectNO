using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildUI : MonoBehaviour
{
    public GameObject uiPanel;  // Nation UI �г�

    //Build List ����
    public Transform BuildListParent; // Build ����� �� �θ� ��ü
    public GameObject BuildItemPrefab; // Build ��ư ������


    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private Nation currentNation;

    private int delayedUpdate;

    private List<BuildingType> buildings;

    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
    private static BuildUI _instance;
    public static BuildUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(BuildUI)) as BuildUI;

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
        delayedUpdate = 0;
        /*
        buildings = new();
        foreach(BuildingType buildingType in GlobalVariables.BUILDING_TYPE.Values)
        {
            buildings.Add(buildingType);
        }
        */
        currentNation = null;
        uiPanel.SetActive(false); // ó������ UI�� ����
    }

    private void Update()
    {
        delayedUpdate = (delayedUpdate + 1) % 5;
        if (delayedUpdate == 0 && currentNation != null)
        {
            //InitBuildList();
        }
    }


    /// <summary>
    /// Ư�� Nation�� UI�� ���� �׿� ���� Province ����� ǥ���մϴ�.
    /// </summary>
    /// <param name="nation">������ ����</param>
    public void OpenBuildUI()
    {
        currentNation = GameManager.Instance.player.nation;

        //InitBuildList();

        UIManager.Instance.ReplacePopUp(gameObject);
    }
    

    /// <summary>
    /// Province�� ����� �ش� nation�� province��� �ʱ�ȭ�Ѵ�.
    /// </summary>
    public void InitBuildList()
    {
        // ���� ����Ʈ ����
        foreach (Transform child in BuildListParent)
        {
            Destroy(child.gameObject);
        }

        // TODO: Nation�� ���� Build �߰�
        foreach(BuildingType buildingType in buildings)
        {
            GameObject child = Instantiate(BuildItemPrefab, BuildListParent);
            BuildingUI buildingUI = child.GetComponent<BuildingUI>();
            buildingUI.SetBuildingData(currentNation, buildingType);
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
    public void CloseBuildUI()
    {
        uiPanel.SetActive(false);
    }
}
