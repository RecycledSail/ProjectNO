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
        return nation != null && buildingType != null &&
               nation.CanPlaceConstructionMandate(buildingType, provinceData, out _);
    }

    private string GetBuildTooltipMessage()
    {
        Nation nation = provinceData?.nation;
        if (nation == null || buildingType == null)
            return "Cannot build here.";

        return nation.CanPlaceConstructionMandate(buildingType, provinceData, out string error)
            ? ""
            : error;
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

        ConstructionMandate mandate = nation.PlaceConstructionMandate(
            buildingType,
            provinceData);
        if (mandate == null)
        {
            Debug.LogWarning(
                $"[ConstructionMandate] Could not issue {buildingType.name} in {provinceData.name}.");
            UpdateBuildButtonState();
            return;
        }

        BuildUI.Instance.UpdateQueue();
        UpdateBuildButtonState();
    }
}
