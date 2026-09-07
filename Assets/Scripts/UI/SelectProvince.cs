using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Selects a province on a Unity Terrain by sampling a province color map.
/// The color map is an ID map: each province must use one unique, flat color.
/// </summary>
[RequireComponent(typeof(TerrainCollider))]
public class SelectProvince : MonoBehaviour
{
    [Serializable]
    public struct ProvinceColorBinding
    {
        public string provinceName;
        public Color32 color;
    }

    public enum ProvinceVisualMode
    {
        Normal,
        BuildRoad,
        Nation,
        Province,
    }

    [Header("Terrain")]
    [SerializeField] private Camera cam;
    [SerializeField] private Terrain terrain;

    [Header("Province Color Map")]
    [Tooltip("Flat-color province ID map. Leave empty until the map is ready.")]
    [SerializeField] private Texture2D colorMap;
    [SerializeField] private List<ProvinceColorBinding> provinceColors = new();
    [SerializeField] private bool flipColorMapY;
    [SerializeField, Range(0f, 1f)] private float minimumColorAlpha = 0.01f;

    public Province HoveredProvince { get; private set; }
    public Province SelectedProvince { get; private set; }
    public ProvinceVisualMode VisualMode { get; private set; }

    public event Action<Province> HoveredProvinceChanged;
    public event Action<Province> ProvinceSelected;
    public event Action<ProvinceVisualMode> VisualModeChanged;

    private TerrainCollider terrainCollider;
    private readonly Dictionary<Color32, string> colorToProvinceName = new();
    private bool isBuildRoadMode;
    private Nation previousNation;
    private Province previousProvince;
    private bool warnedAboutMissingColorMap;

    private void Awake()
    {
        terrain = terrain != null ? terrain : GetComponent<Terrain>();
        terrainCollider = GetComponent<TerrainCollider>();
        cam = cam != null ? cam : Camera.main;
        BuildColorLookup();
    }

    private void OnValidate()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        terrainCollider = GetComponent<TerrainCollider>();
        BuildColorLookup();
    }

    private void Update()
    {
        UpdateVisualMode();
        HandleHoverAndSelection();
    }

    private void BuildColorLookup()
    {
        colorToProvinceName.Clear();
        foreach (ProvinceColorBinding binding in provinceColors)
        {
            if (string.IsNullOrWhiteSpace(binding.provinceName))
                continue;

            if (!colorToProvinceName.TryAdd(binding.color, binding.provinceName))
                Debug.LogWarning($"SelectProvince: duplicate ColorMap color {binding.color}.", this);
        }
    }

    private void UpdateVisualMode()
    {
        bool buildRoadMode = BuildUI.Instance != null &&
            BuildUI.Instance.subUIs != null &&
            BuildUI.Instance.subUIs.Count > 2 &&
            BuildUI.Instance.subUIs[2].activeInHierarchy;

        bool nationMode = NationUI.Instance != null &&
            NationUI.Instance.gameObject.activeInHierarchy &&
            NationUI.Instance.CurrentNation != null;

        bool provinceMode = ProvinceDetailUI.Instance != null &&
            ProvinceDetailUI.Instance.gameObject.activeInHierarchy &&
            ProvinceDetailUI.Instance.CurrentProvince != null;

        isBuildRoadMode = buildRoadMode;
        ProvinceVisualMode nextMode = buildRoadMode ? ProvinceVisualMode.BuildRoad :
            nationMode ? ProvinceVisualMode.Nation :
            provinceMode ? ProvinceVisualMode.Province : ProvinceVisualMode.Normal;

        Nation currentNation = nationMode ? NationUI.Instance.CurrentNation : null;
        Province currentProvince = provinceMode ? ProvinceDetailUI.Instance.CurrentProvince : null;
        if (VisualMode == nextMode && previousNation == currentNation && previousProvince == currentProvince)
            return;

        VisualMode = nextMode;
        previousNation = currentNation;
        previousProvince = currentProvince;
        VisualModeChanged?.Invoke(VisualMode);
    }

    private void HandleHoverAndSelection()
    {
        Province province = GetProvinceUnderPointer();
        if (HoveredProvince != province)
        {
            HoveredProvince = province;
            HoveredProvinceChanged?.Invoke(HoveredProvince);
        }

        if (province == null || !Input.GetMouseButtonDown(0))
            return;

        if (isBuildRoadMode)
        {
            ToggleRoad(province);
            return;
        }

        SelectedProvince = province;
        ProvinceSelected?.Invoke(SelectedProvince);
        if (ProvinceDetailUI.Instance != null)
            ProvinceDetailUI.Instance.OpenProvinceDetailUI(SelectedProvince);
    }

    private Province GetProvinceUnderPointer()
    {
        if (IsPointerOverUIObject() || cam == null || terrain == null || terrainCollider == null)
            return null;

        if (colorMap == null)
        {
            if (!warnedAboutMissingColorMap)
            {
                Debug.LogWarning("SelectProvince: assign a ColorMap before selecting provinces.", this);
                warnedAboutMissingColorMap = true;
            }

            return null;
        }

        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
            return null;

        if (hit.collider != terrainCollider)
            return null;

        if (!TryGetColorMapColor(hit.point, out Color32 color))
            return null;

        return colorToProvinceName.TryGetValue(color, out string provinceName) &&
            GlobalVariables.PROVINCES.TryGetValue(provinceName, out Province province)
            ? province
            : null;
    }

    private bool TryGetColorMapColor(Vector3 worldPosition, out Color32 color)
    {
        color = default;
        if (terrain.terrainData == null || !colorMap.isReadable)
            return false;

        Vector3 localPoint = terrain.transform.InverseTransformPoint(worldPosition);
        Vector3 size = terrain.terrainData.size;
        if (size.x <= 0f || size.z <= 0f)
            return false;

        float u = localPoint.x / size.x;
        float v = localPoint.z / size.z;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        int x = Mathf.Clamp(Mathf.FloorToInt(u * colorMap.width), 0, colorMap.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(v * colorMap.height), 0, colorMap.height - 1);
        if (flipColorMapY)
            y = colorMap.height - 1 - y;

        color = colorMap.GetPixel(x, y);
        return color.a / 255f >= minimumColorAlpha;
    }

    private void ToggleRoad(Province province)
    {
        if (GameManager.Instance == null || GameManager.Instance.player == null)
            return;

        Nation playerNation = GameManager.Instance.player.nation;
        if (province.nation != playerNation)
            return;

        if (province.road == 0)
            province.BuildRoad();
        else
            province.RemoveRoad();

        SelectedProvince = province;
        ProvinceSelected?.Invoke(SelectedProvince);
    }

    private static bool IsPointerOverUIObject()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new(EventSystem.current)
        {
            position = Input.mousePosition,
        };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
