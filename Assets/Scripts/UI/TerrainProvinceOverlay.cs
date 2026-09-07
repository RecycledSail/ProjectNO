using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class TerrainProvinceOverlay : MonoBehaviour
{
    private const int GridResolution = 513;

    private Terrain terrain;
    private Texture2D provinceMap;
    private bool flipY;
    private Dictionary<Color32, string> colorToProvinceName;
    private Mesh overlayMesh;
    private MeshRenderer meshRenderer;
    private Material material;
    private Texture2D tintMap;
    private int tintStateHash = int.MinValue;

    public void Configure(
        Terrain targetTerrain,
        Texture2D targetProvinceMap,
        bool shouldFlipY,
        Dictionary<Color32, string> bindings)
    {
        terrain = targetTerrain;
        provinceMap = targetProvinceMap;
        flipY = shouldFlipY;
        colorToProvinceName = bindings;

        if (terrain == null || provinceMap == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        EnsureRenderer();
        if (material == null)
        {
            gameObject.SetActive(false);
            return;
        }

        BuildMesh();
        material.SetTexture("_ProvinceMap", provinceMap);
        material.SetFloat("_FlipY", flipY ? 1f : 0f);
        tintStateHash = int.MinValue;
    }

    public void RefreshTint(SelectProvince.ProvinceVisualMode visualMode, Nation focusedNation, Province focusedProvince)
    {
        if (terrain == null || provinceMap == null || colorToProvinceName == null || material == null)
            return;

        int stateHash = GetTintStateHash(visualMode, focusedNation, focusedProvince);
        if (stateHash == tintStateHash)
            return;

        EnsureTintMap();
        Color32[] sourcePixels = provinceMap.GetPixels32();
        Color32[] tintPixels = new Color32[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color32 sourceColor = sourcePixels[i];
            tintPixels[i] = GetProvinceTint(sourceColor, visualMode, focusedNation, focusedProvince);
        }

        tintMap.SetPixels32(tintPixels);
        tintMap.Apply(false, false);
        material.SetTexture("_TintMap", tintMap);
        tintStateHash = stateHash;
    }

    public void SetHoveredColor(Color32 color, bool hasHoveredProvince)
    {
        if (material == null)
            return;

        material.SetColor("_HoverColor", color);
        material.SetFloat("_HasHover", hasHoveredProvince ? 1f : 0f);
    }

    private void EnsureRenderer()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (material == null)
        {
            Shader shader = Shader.Find("ProjectNO/Province Terrain Overlay");
            if (shader == null)
            {
                Debug.LogError("Province terrain overlay shader was not found.", this);
                return;
            }

            material = new Material(shader)
            {
                name = "Runtime Province Terrain Overlay",
                renderQueue = 3100,
            };
        }

        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
    }

    private void BuildMesh()
    {
        if (overlayMesh != null || terrain.terrainData == null)
            return;

        int vertexCount = GridResolution * GridResolution;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[(GridResolution - 1) * (GridResolution - 1) * 6];
        Vector3 size = terrain.terrainData.size;
        float surfaceOffset = Mathf.Max(0.35f, size.y * 0.03f);

        for (int z = 0; z < GridResolution; z++)
        {
            float v = z / (float)(GridResolution - 1);
            for (int x = 0; x < GridResolution; x++)
            {
                float u = x / (float)(GridResolution - 1);
                int index = z * GridResolution + x;
                vertices[index] = new Vector3(
                    u * size.x,
                    terrain.terrainData.GetInterpolatedHeight(u, v) + surfaceOffset,
                    v * size.z);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < GridResolution - 1; z++)
        {
            for (int x = 0; x < GridResolution - 1; x++)
            {
                int bottomLeft = z * GridResolution + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + GridResolution;
                int topRight = topLeft + 1;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        overlayMesh = new Mesh
        {
            name = "Runtime Province Terrain Overlay",
            indexFormat = IndexFormat.UInt32,
            vertices = vertices,
            uv = uvs,
            triangles = triangles,
        };
        overlayMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = overlayMesh;
    }

    private void EnsureTintMap()
    {
        if (tintMap != null && tintMap.width == provinceMap.width && tintMap.height == provinceMap.height)
            return;

        if (tintMap != null)
            Destroy(tintMap);

        tintMap = new Texture2D(provinceMap.width, provinceMap.height, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Province Tint Map",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
    }

    private Color32 GetProvinceTint(
        Color32 sourceColor,
        SelectProvince.ProvinceVisualMode visualMode,
        Nation focusedNation,
        Province focusedProvince)
    {
        if (!colorToProvinceName.TryGetValue(sourceColor, out string provinceName) ||
            !GlobalVariables.PROVINCES.TryGetValue(provinceName, out Province province))
        {
            return new Color32(0, 0, 0, 0);
        }

        if (visualMode == SelectProvince.ProvinceVisualMode.Normal)
            return province.nation != null ? province.nation.color : new Color32(110, 110, 110, 120);

        bool isFocused = visualMode == SelectProvince.ProvinceVisualMode.BuildRoad
            ? GameManager.Instance != null && GameManager.Instance.player != null && province.nation == GameManager.Instance.player.nation
            : visualMode == SelectProvince.ProvinceVisualMode.Nation
                ? province.nation == focusedNation
                : province == focusedProvince;

        if (!isFocused)
            return new Color32(35, 35, 40, 95);

        bool isCapital = province.nation != null && province == province.nation.capital;
        if (isCapital)
            return province.road == 1 ? new Color32(100, 100, 255, 220) : new Color32(200, 100, 200, 220);

        return province.road == 1 ? new Color32(100, 255, 100, 220) : new Color32(255, 100, 100, 220);
    }

    private int GetTintStateHash(
        SelectProvince.ProvinceVisualMode visualMode,
        Nation focusedNation,
        Province focusedProvince)
    {
        int hash = (int)visualMode;
        hash = hash * 31 + (focusedNation?.id ?? 0);
        hash = hash * 31 + (focusedProvince?.id ?? 0);
        foreach (KeyValuePair<Color32, string> pair in colorToProvinceName)
        {
            if (!GlobalVariables.PROVINCES.TryGetValue(pair.Value, out Province province))
                continue;

            hash = hash * 31 + province.road;
            hash = hash * 31 + (province.nation?.id ?? 0);
        }

        return hash;
    }

    private void OnDestroy()
    {
        if (overlayMesh != null)
            Destroy(overlayMesh);
        if (material != null)
            Destroy(material);
        if (tintMap != null)
            Destroy(tintMap);
    }
}
