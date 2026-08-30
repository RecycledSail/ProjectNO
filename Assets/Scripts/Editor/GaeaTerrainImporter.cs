using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class GaeaTerrainImporter
{
    private const string DefaultGaeaFolder = "Assets/Prefabs/GaeaTerrain";
    private const string SceneTerrainObjectName = "Gaea003Terrain";
    private const float SourceTerrainSize = 500000f;
    private const float DefaultImportScale = 0.01f;
    private const float FallbackTerrainHeight = 1000f;
    private const float TargetTerrainHeight = 40f;
    private const float MinTerrainHeight = 30f;
    private const float MaxTerrainHeight = 50f;

    [MenuItem("Tools/Gaea/Import Gaea Terrain")]
    public static void ImportScenes003Terrain()
    {
        ImportGaeaFolder(DefaultGaeaFolder, GetSceneReferenceBounds());
    }

    [MenuItem("Tools/Gaea/Import Selected Gaea Folder")]
    public static void ImportSelectedGaeaFolder()
    {
        UnityEngine.Object active = Selection.activeObject;
        string selectedPath = active == null ? string.Empty : AssetDatabase.GetAssetPath(active);

        if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
        {
            EditorUtility.DisplayDialog("Gaea Import", "Select a Gaea export folder in the Project window first.", "OK");
            return;
        }

        ImportGaeaFolder(selectedPath, GetSceneReferenceBounds());
    }

    private static void ImportGaeaFolder(string folder, Bounds? referenceBounds)
    {
        string heightPath = $"{folder}/Combine_Out.exr";
        string colorPath = $"{folder}/colour_Out.png";
        string waterMaskPath = $"{folder}/water_mask_Out.png";
        string objPath = $"{folder}/Mesher.obj";
        string outputFolder = $"{folder}/Imported";

        if (!File.Exists(ToFullPath(heightPath)))
        {
            EditorUtility.DisplayDialog("Gaea Import", $"Heightmap not found:\n{heightPath}", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            AssetDatabase.CreateFolder(folder, "Imported");
        }
        ConfigureHeightTexture(heightPath);
        ConfigureColorTexture(colorPath);
        bool hasWaterMask = File.Exists(ToFullPath(waterMaskPath));
        if (hasWaterMask)
        {
            ConfigureWaterMaskTexture(waterMaskPath);
        }
        AssetDatabase.ImportAsset(heightPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(colorPath, ImportAssetOptions.ForceUpdate);
        if (hasWaterMask)
        {
            AssetDatabase.ImportAsset(waterMaskPath, ImportAssetOptions.ForceUpdate);
        }

        Texture2D heightTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath);
        if (heightTexture == null)
        {
            EditorUtility.DisplayDialog("Gaea Import", $"Could not load heightmap:\n{heightPath}", "OK");
            return;
        }

        float squareFootprintSize = referenceBounds.HasValue
            ? Mathf.Max(referenceBounds.Value.size.x, referenceBounds.Value.size.z)
            : SourceTerrainSize * DefaultImportScale;
        float horizontalScale = squareFootprintSize / SourceTerrainSize;
        float terrainHeight = Mathf.Clamp(TargetTerrainHeight, MinTerrainHeight, MaxTerrainHeight);
        Vector3 terrainSize = new Vector3(
            squareFootprintSize,
            terrainHeight,
            squareFootprintSize);
        int heightmapResolution = Mathf.Clamp(heightTexture.width + 1, 33, 4097);

        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = heightmapResolution,
            size = terrainSize
        };

        terrainData.SetHeights(0, 0, BuildHeights(heightTexture, heightmapResolution));

        TerrainLayer layer = CreateTerrainLayer(colorPath, outputFolder, terrainSize);
        if (layer != null)
        {
            terrainData.terrainLayers = new[] { layer };
        }

        string terrainDataPath = $"{outputFolder}/Gaea003TerrainData.asset";
        AssetDatabase.DeleteAsset(terrainDataPath);
        AssetDatabase.CreateAsset(terrainData, terrainDataPath);

        GameObject terrainObject = FindSceneObjectByName(SceneTerrainObjectName);
        bool updatingSceneObject = terrainObject != null;
        if (terrainObject == null)
        {
            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = SceneTerrainObjectName;
        }
        else
        {
            ApplyTerrainDataToExistingObject(terrainObject, terrainData);
        }

        terrainObject.transform.position = referenceBounds.HasValue
            ? new Vector3(
                referenceBounds.Value.center.x - squareFootprintSize * 0.5f,
                referenceBounds.Value.min.y,
                referenceBounds.Value.center.z - squareFootprintSize * 0.5f)
            : new Vector3(-terrainSize.x * 0.5f, 0f, -terrainSize.z * 0.5f);

        if (hasWaterMask)
        {
            CreateWaterSurface(terrainObject.transform, terrainSize, heightTexture, waterMaskPath, outputFolder);
        }

        string prefabPath = $"{outputFolder}/Gaea003Terrain.prefab";
        AssetDatabase.DeleteAsset(prefabPath);
        PrefabUtility.SaveAsPrefabAsset(terrainObject, prefabPath);
        if (!updatingSceneObject)
        {
            UnityEngine.Object.DestroyImmediate(terrainObject);
        }
        else
        {
            EditorSceneManager.MarkSceneDirty(terrainObject.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath));

        Debug.Log($"Imported Gaea terrain from {folder}. Terrain size: {terrainSize}, resolution: {heightmapResolution}. Updated scene object: {updatingSceneObject}. Prefab: {prefabPath}");
    }

    private static float[,] BuildHeights(Texture2D texture, int resolution)
    {
        float[,] heights = new float[resolution, resolution];

        for (int y = 0; y < resolution; y++)
        {
            float v = y / (float)(resolution - 1);
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)(resolution - 1);
                heights[y, x] = Mathf.Clamp01(texture.GetPixelBilinear(u, v).r);
            }
        }

        return heights;
    }

    private static TerrainLayer CreateTerrainLayer(string colorPath, string outputFolder, Vector3 terrainSize)
    {
        Texture2D colorTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
        if (colorTexture == null)
        {
            return null;
        }

        TerrainLayer layer = new TerrainLayer
        {
            diffuseTexture = colorTexture,
            tileSize = new Vector2(terrainSize.x, terrainSize.z)
        };

        string layerPath = $"{outputFolder}/Gaea003Colour.terrainlayer";
        AssetDatabase.DeleteAsset(layerPath);
        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    private static void ConfigureHeightTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static void ConfigureColorTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.isReadable = false;
        importer.mipmapEnabled = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static void ConfigureWaterMaskTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static void ApplyTerrainDataToExistingObject(GameObject terrainObject, TerrainData terrainData)
    {
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = terrainObject.AddComponent<Terrain>();
        }

        TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = terrainObject.AddComponent<TerrainCollider>();
        }

        terrain.terrainData = terrainData;
        terrainCollider.terrainData = terrainData;
    }

    private static void CreateWaterSurface(Transform parent, Vector3 terrainSize, Texture2D heightTexture, string waterMaskPath, string outputFolder)
    {
        Texture2D waterMask = AssetDatabase.LoadAssetAtPath<Texture2D>(waterMaskPath);
        if (waterMask == null)
        {
            Debug.LogWarning($"Water mask not found. Skipping water plane: {waterMaskPath}");
            return;
        }

        Material waterMaterial = CreateWaterMaterial(waterMask, outputFolder);
        if (waterMaterial == null)
        {
            return;
        }

        Transform existingWater = parent.Find("Gaea_003_Water");
        if (existingWater != null)
        {
            UnityEngine.Object.DestroyImmediate(existingWater.gameObject);
        }

        GameObject waterPlane = new GameObject("Gaea_003_Water");
        waterPlane.transform.SetParent(parent, false);
        waterPlane.transform.localPosition = Vector3.zero;
        waterPlane.transform.localRotation = Quaternion.identity;
        waterPlane.transform.localScale = Vector3.one;

        MeshFilter meshFilter = waterPlane.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateWaterSurfaceMesh(heightTexture, terrainSize, outputFolder);

        MeshRenderer renderer = waterPlane.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
    }

    private static Material CreateWaterMaterial(Texture2D waterMask, string outputFolder)
    {
        Shader shader = Shader.Find("ProjectNO/Gaea Water Mask");
        if (shader == null)
        {
            Debug.LogWarning("Shader 'ProjectNO/Gaea Water Mask' was not found. Skipping water plane.");
            return null;
        }

        Material material = new Material(shader)
        {
            name = "Gaea003Water"
        };
        material.SetTexture("_MaskTex", waterMask);
        material.SetColor("_WaterColor", new Color(0.04f, 0.27f, 0.42f, 0.38f));
        material.SetColor("_ShallowColor", new Color(0.18f, 0.58f, 0.72f, 0.28f));
        material.SetColor("_FoamColor", new Color(0.72f, 0.95f, 1f, 0.35f));
        material.SetFloat("_AlphaCutoff", 0.45f);
        material.SetFloat("_EdgeSoftness", 0.12f);
        material.SetFloat("_RippleScale", 80f);
        material.SetFloat("_RippleStrength", 0.32f);
        material.SetFloat("_WaveAmplitude", 0.18f);
        material.SetFloat("_WaveScale", 22f);
        material.SetFloat("_WaveSpeed", 1.2f);
        material.SetFloat("_FresnelPower", 3.2f);

        string materialPath = $"{outputFolder}/Gaea003Water.mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static Mesh CreateWaterSurfaceMesh(Texture2D heightTexture, Vector3 terrainSize, string outputFolder)
    {
        const int gridResolution = 257;
        const float surfaceOffset = 0.18f;

        Vector3[] vertices = new Vector3[gridResolution * gridResolution];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(gridResolution - 1) * (gridResolution - 1) * 6];

        for (int z = 0; z < gridResolution; z++)
        {
            float v = z / (float)(gridResolution - 1);
            for (int x = 0; x < gridResolution; x++)
            {
                float u = x / (float)(gridResolution - 1);
                int index = z * gridResolution + x;
                float height = heightTexture.GetPixelBilinear(u, v).r * terrainSize.y;

                vertices[index] = new Vector3(u * terrainSize.x, height + surfaceOffset, v * terrainSize.z);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < gridResolution - 1; z++)
        {
            for (int x = 0; x < gridResolution - 1; x++)
            {
                int bottomLeft = z * gridResolution + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + gridResolution;
                int topRight = topLeft + 1;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "Gaea003WaterSurface",
            indexFormat = IndexFormat.UInt32,
            vertices = vertices,
            uv = uvs,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        string meshPath = $"{outputFolder}/Gaea003WaterSurface.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        return mesh;
    }

    private static float GetObjHeightRange(string objPath, float fallback)
    {
        string fullPath = ToFullPath(objPath);
        if (!File.Exists(fullPath))
        {
            return fallback;
        }

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        foreach (string line in File.ReadLines(fullPath))
        {
            if (!line.StartsWith("v ", StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }
        }

        if (float.IsInfinity(min) || float.IsNaN(min) || float.IsInfinity(max) || float.IsNaN(max) || max <= min)
        {
            return fallback;
        }

        return max - min;
    }

    private static Bounds? GetSceneReferenceBounds()
    {
        GameObject reference = FindSceneObjectByName("Terrain");

        if (reference == null)
        {
            Debug.LogWarning("No scene object named 'Terrain' found. Importing with default 1/100 scale.");
            return null;
        }

        if (!TryGetWorldBounds(reference, out Bounds bounds))
        {
            Debug.LogWarning($"Reference object '{reference.name}' has no Renderer, Collider, or Terrain bounds. Importing with default 1/100 scale.");
            return null;
        }

        Debug.Log($"Using '{reference.name}' bounds for Gaea import: center {bounds.center}, size {bounds.size}");
        return bounds;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        foreach (GameObject sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (sceneObject.name == objectName && sceneObject.scene.IsValid() && sceneObject.scene.isLoaded)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Encapsulate(renderer.bounds, ref bounds, ref hasBounds);
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            Encapsulate(collider.bounds, ref bounds, ref hasBounds);
        }

        foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
        {
            Bounds terrainBounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
            Encapsulate(terrainBounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void Encapsulate(Bounds value, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = value;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(value);
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
