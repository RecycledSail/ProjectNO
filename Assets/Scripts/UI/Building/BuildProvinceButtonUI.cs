using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildProvinceButtonUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text countText;
    public TMP_Text unemployedText;
    public Button buildButton;

    private BuildRequirementTooltip buildTooltip;
    private Province provinceData;
    public Province Province { get { return provinceData; } }

    private BuildingType buildingType;

    private void Awake()
    {
        if (buildButton == null)
            buildButton = transform.Find("BuildButton")?.GetComponent<Button>();

        if (buildButton != null)
            buildTooltip = buildButton.GetComponent<BuildRequirementTooltip>() ?? buildButton.gameObject.AddComponent<BuildRequirementTooltip>();

        DisableNonBuildButtons();
    }

    private void DisableNonBuildButtons()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button == buildButton)
                continue;

            button.interactable = false;
            button.enabled = false;
        }
    }

    public void SetBuildingData(Province province, BuildingType buildingType)
    {
        provinceData = province;
        this.buildingType = buildingType;

        if (nameText != null)
            nameText.text = province.name;

        UpdateCount();
        UpdateBuildButtonState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateCount);
            GameManager.Instance.dayUIEvent.RemoveListener(UpdateBuildButtonState);
            GameManager.Instance.dayUIEvent.AddListener(UpdateCount);
            GameManager.Instance.dayUIEvent.AddListener(UpdateBuildButtonState);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.dayUIEvent.RemoveListener(UpdateCount);
        GameManager.Instance.dayUIEvent.RemoveListener(UpdateBuildButtonState);
    }

    private void UpdateCount()
    {
        if (provinceData == null || buildingType == null)
            return;

        int count = 0;
        if (provinceData.buildings.TryGetValue(buildingType, out Building building))
            count += building.level;

        if (countText != null)
            countText.text = count.ToString() + " || " + "100";

        if (unemployedText != null)
            unemployedText.text = (provinceData.population - provinceData.hiredPopulation).ToString();
    }

    private void UpdateBuildButtonState()
    {
        if (buildButton != null)
            buildButton.interactable = CanStartConstruction();

        if (buildTooltip != null)
            buildTooltip.SetMessage(GetBuildTooltipMessage());
    }

    private bool CanStartConstruction()
    {
        Nation nation = provinceData?.nation;
        if (nation == null || buildingType == null)
            return false;

        if (nation.IsInBuildQueue(buildingType, provinceData))
            return false;

        if (!GlobalVariables.BUILDING_RECIPE.TryGetValue(buildingType.name, out BuildingRecipe recipe))
            return false;

        Dictionary<string, ProductState> products = GetAccessibleProducts(provinceData);
        if (products == null)
            return false;

        foreach (var requirement in recipe.requireItems)
        {
            int reserved = GetReservedAmount(nation, products, requirement.Key);
            if (!products.TryGetValue(requirement.Key, out ProductState product) ||
                product.Stock - reserved < requirement.Value)
                return false;
        }

        return true;
    }

    private string GetBuildTooltipMessage()
    {
        Nation nation = provinceData?.nation;
        if (nation == null || buildingType == null)
            return "Cannot build here.";

        if (nation.IsInBuildQueue(buildingType, provinceData))
            return "Already queued.";

        if (!GlobalVariables.BUILDING_RECIPE.TryGetValue(buildingType.name, out BuildingRecipe recipe))
            return $"No recipe for {buildingType.name}.";

        Dictionary<string, ProductState> products = GetAccessibleProducts(provinceData);
        if (products == null)
            return "No accessible market.";

        List<string> missing = new();
        foreach (var requirement in recipe.requireItems)
        {
            int reserved = GetReservedAmount(nation, products, requirement.Key);
            int stock = products.TryGetValue(requirement.Key, out ProductState product) ? product.Stock : 0;
            int available = Mathf.Max(0, stock - reserved);
            if (available < requirement.Value)
                missing.Add($"{requirement.Key}: have {available:N0}, need {requirement.Value:N0}");
        }

        if (missing.Count == 0)
            return "";

        return "Need more materials\n" + string.Join("\n", missing);
    }

    private Dictionary<string, ProductState> GetAccessibleProducts(Province province)
    {
        if (province == null)
            return null;

        if (province.isConnectedToCapital && province.nation?.market != null)
            return province.nation.market.Products;

        return province.market?.Products;
    }

    private int GetReservedAmount(Nation nation, Dictionary<string, ProductState> products, string productName)
    {
        return nation.constructionRequest.buildingReservations
            .Where(reservation => GetAccessibleProducts(reservation.targetProvince) == products)
            .Where(reservation => GlobalVariables.BUILDING_RECIPE.ContainsKey(reservation.buildingType.name))
            .Sum(reservation =>
                GlobalVariables.BUILDING_RECIPE[reservation.buildingType.name]
                    .requireItems.TryGetValue(productName, out int amount)
                        ? amount
                        : 0);
    }

    public void OnClick()
    {
        Nation nation = provinceData?.nation;
        if (nation == null || buildingType == null)
            return;

        if (!CanStartConstruction())
        {
            Debug.LogWarning($"[BuildQueue] Requirements are not met for {buildingType.name} in {provinceData.name}.");
            UpdateBuildButtonState();
            return;
        }

        if (!provinceData.buildings.TryGetValue(buildingType, out Building building))
        {
            building = new Building(buildingType, provinceData);
            provinceData.buildings[buildingType] = building;
        }

        nation.constructionRequest.AddBuildingReservation(buildingType, provinceData, nation);
        nation.AddToBuildQueue(building);

        AssignAdjacentConstructionCompany(nation);

        BuildUI.Instance.UpdateQueue();
        UpdateBuildButtonState();
    }

    private void AssignAdjacentConstructionCompany(Nation nation)
    {
        List<Province> adjacentProvinces = GlobalVariables.ADJACENT_PROVINCES.TryGetValue(provinceData.name, out var adjProvs)
            ? adjProvs
            : new List<Province>();

        foreach (Province adjProvince in adjacentProvinces)
        {
            foreach (var bld in adjProvince.buildings.Values)
            {
                if (bld is not ConstructionCompanyBuilding constructionCompany)
                    continue;

                BuildingReservation reservation = new BuildingReservation(buildingType, provinceData, nation);
                if (constructionCompany.TryAssign(reservation, out BuildingInProgress bip))
                {
                    Debug.Log($"Assigned building project for {buildingType.name} in {provinceData.name} to construction company in {adjProvince.name}");
                    return;
                }
            }
        }
    }
}
