using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class BuildUI : MonoBehaviour
{
    public GameObject uiPanel;

    public Transform BuildListParent;
    public GameObject BuildItemPrefab;

    public Transform BuildQueueParent;
    public GameObject BuildQueueItemPrefab;

    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;
    private Nation currentNation;

    public TMP_Text detailedText;

    [HideInInspector]
    public ProduceUI selectedProduceUI = null;

    private readonly Dictionary<string, ProduceUI> productRows = new();

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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
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

        currentNation = null;
        uiPanel.SetActive(false);
        GameManager.Instance.dayUIEvent.AddListener(UpdateBuildUI);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateBuildUI);
    }

    public void OpenBuildUI()
    {
        currentNation = GameManager.Instance.player.nation;

        UpdateBuildUI();

        UIManager.Instance.ReplacePopUp(gameObject);
    }

    public void UpdateBuildUI()
    {
        if (currentNation == null)
            return;

        InitBuildList();
        UpdateDetailUI();
        UpdateQueue();
    }

    public void InitBuildList()
    {
        string oldProductType = selectedProduceUI == null ? "" : selectedProduceUI.productName;

        HashSet<string> currentProducts = new(GlobalVariables.PRODUCTS.Keys);
        foreach (var pair in productRows.ToList())
        {
            if (currentProducts.Contains(pair.Key))
                continue;

            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
            productRows.Remove(pair.Key);
        }

        foreach (string productType in GlobalVariables.PRODUCTS.Keys)
        {
            if (!productRows.TryGetValue(productType, out ProduceUI produceUI) || produceUI == null)
            {
                GameObject child = Instantiate(BuildItemPrefab, BuildListParent);
                produceUI = child.GetComponent<ProduceUI>();
                productRows[productType] = produceUI;
            }

            produceUI.SetProduceData(currentNation, productType);
            if (productType == oldProductType)
                selectedProduceUI = produceUI;
        }
    }

    public void ChangeSubUI(int index)
    {
        currentOpenSubUI.SetActive(false);
        subUIs[index].SetActive(true);
        currentOpenSubUI = subUIs[index];
    }

    public void CloseBuildUI()
    {
        uiPanel.SetActive(false);
        selectedProduceUI = null;
    }

    public void OnManualButtonClick()
    {
        if (selectedProduceUI == null)
            return;

        if (GlobalVariables.PRODUCT_TO_BUILDING.TryGetValue(selectedProduceUI.productName, out string buildingTypeName) &&
            GlobalVariables.BUILDING_TYPE.TryGetValue(buildingTypeName, out BuildingType buildingType))
        {
            BuildProvinceUI.Instance.OpenBuildProvinceUI(buildingType);
        }
    }

    public void UpdateDetailUI()
    {
        if (selectedProduceUI == null)
        {
            detailedText.text = "Select product\nto see details";
            return;
        }

        string text = selectedProduceUI.productName + "\n";
        text += "Supply: " + selectedProduceUI.productSupplyCount + " Demand: " + selectedProduceUI.productDemandCount + "\n";
        detailedText.text = text;
    }

    public void UpdateQueue()
    {
        if (currentNation == null)
            return;

        List<Building> queuedBuildings = currentNation.buildingsInProgress.ToList();
        for (int i = 0; i < queuedBuildings.Count; i++)
        {
            if (i < BuildQueueParent.childCount)
            {
                BuildQueueParent.GetChild(i).GetComponent<BuildQueueItem>().SetBuildingData(queuedBuildings[i]);
            }
            else
            {
                GameObject child = Instantiate(BuildQueueItemPrefab, BuildQueueParent);
                BuildQueueItem bqi = child.GetComponent<BuildQueueItem>();
                bqi.SetBuildingData(queuedBuildings[i]);
            }
        }

        for (int i = BuildQueueParent.childCount - 1; i >= queuedBuildings.Count; i--)
        {
            Destroy(BuildQueueParent.GetChild(i).gameObject);
        }
    }
}
