using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// SelectProvince 클래스는 마우스 클릭을 통해 특정 지역(Province)을 선택하고, 
/// 해당 지역과 인접한 지역을 색상 변경하는 기능을 수행합니다.
/// </summary>
public class SelectProvince3D : MonoBehaviour
{
    public Camera cam; // 화면을 비추는 카메라
    private List<GameObject> children; // 현재 오브젝트의 Children
    private List<GameObject> outlined;
    private Dictionary<GameObject, Color32> originalColors; // 각 child의 원래 색상 저장
    private bool isBuildSubActive = false;
    [SerializeField] private Color hoverBorderColor = new Color(1f, 0.32f, 0f, 1f);
    [SerializeField] private float hoverBorderWidth = 0.05f;
    [SerializeField] private float hoverBorderHeightOffset = 0.08f;
    [SerializeField] private float hoverBorderSimplifyTolerance = 0.08f;
    [SerializeField, UnityEngine.Range(0, 3)] private int hoverBorderSmoothIterations = 1;
    [SerializeField] private bool prewarmHoverBorderCache = true;
    private int provinceColorMode = 0; // 0: 기본, 1: 길 건설 모드

    private Nation previousNation = null;
    private Province previousProvince = null;
    private readonly Dictionary<Mesh, List<Vector3[]>> hoverBorderCache = new();
    private readonly List<LineRenderer> hoverBorderLinePool = new();
    private int activeHoverBorderLineCount = 0;
    private Material hoverBorderMaterial;

    void Start()
    {
        children = new();
        outlined = new();
        originalColors = new();
        for(int i = 0, count = this.transform.childCount; i < count; i++)
        {
            GameObject child = this.transform.GetChild(i).gameObject;
            children.Add(child);

            // 저장해둘 원래 색상
            var renderer = child.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                originalColors[child] = renderer.material.color;
            }
            else
            {
                originalColors[child] = new Color32(255,255,255,255);
            }
        }
        
        // 초기화 시 각 Province를 Nation의 색상으로 칠함
        ColorOnNormalMode();

        if (prewarmHoverBorderCache)
            PrewarmHoverBorderCache();
    }

    void Update()
    {
        HandleProvinceColoring();
        HandleHoverAndSelection();
    }

    private void OnDestroy()
    {
        ClearHoverBorder();
        for (int i = hoverBorderLinePool.Count - 1; i >= 0; i--)
        {
            if (hoverBorderLinePool[i] != null)
                Destroy(hoverBorderLinePool[i].gameObject);
        }

        if (hoverBorderMaterial != null)
            Destroy(hoverBorderMaterial);
    }

    private void ColorOnBuildSubEnabled()
    {
        Nation currentNation = GameManager.Instance.player.nation;
        foreach (var c in children)
        {
            if (GlobalVariables.PROVINCES.TryGetValue(c.name, out Province province))
            {
                if (province.nation != currentNation)
                {
                    // 다른 국가의 프로빈스는 원래 색상으로 복원
                    if (originalColors.TryGetValue(c, out Color32 col))
                        RecolorProvince(c, col);
                    continue;
                }
                // 수도인 경우 도로가 없으면 보라색, 있으면 파랑색
                if (province == currentNation.capital)
                {
                    if (province.road == 1)
                        RecolorProvince(c, new Color32(100, 100, 255, 255));
                    else
                        RecolorProvince(c, new Color32(200, 100, 200, 255));
                    continue;
                }
                // 도로 색에 따라 색칠
                if (province.road == 1)
                    RecolorProvince(c, new Color32(100, 255, 100, 255));
                else
                    RecolorProvince(c, new Color32(255, 100, 100, 255));
            }
        }
        provinceColorMode = 1;
    }

    private void ColorOnNationUIEnabled(bool nationSubActive)
    {
        // 현재 NationUI의 선택된 국가에 따라 색칠
        if (nationSubActive && NationUI.Instance.CurrentNation != null)
        {
            Nation currentNation = NationUI.Instance.CurrentNation;
            foreach (var c in children)
            {
                if (GlobalVariables.PROVINCES.TryGetValue(c.name, out Province province))
                {
                    if (province.nation == currentNation)
                    {
                        // 수도인 경우 도로가 없으면 보라색, 있으면 파랑색
                        if (province == currentNation.capital)
                        {
                            if (province.road == 1)
                                RecolorProvince(c, new Color32(100, 100, 255, 255));
                            else
                                RecolorProvince(c, new Color32(200, 100, 200, 255));
                        }
                        else
                        {
                            // 도로 색에 따라 색칠
                            if (province.road == 1)
                                RecolorProvince(c, new Color32(100, 255, 100, 255));
                            else
                                RecolorProvince(c, new Color32(255, 100, 100, 255));
                        }
                    }
                    else
                    {
                        // 다른 국가의 프로빈스는 원래 색상으로 복원
                        if (originalColors.TryGetValue(c, out Color32 col))
                            RecolorProvince(c, col);
                    }
                }
            }
        }
        provinceColorMode = 2;
    }


    private void ColorOnProvinceUIEnabled(bool provinceSubActive)
    {
        // 현재 ProvinceDetailUI의 선택된 프로빈스가 속한 국가에 따라 색칠
        if (provinceSubActive && ProvinceDetailUI.Instance.CurrentProvince != null)
        {
            Province selectedProvince = ProvinceDetailUI.Instance.CurrentProvince;
            
            foreach (var c in children)
            {
                if (GlobalVariables.PROVINCES.TryGetValue(c.name, out Province province))
                {
                    if (province == selectedProvince)
                    {
                        // 선택된 국가의 프로빈스 색칠
                        if (province.nation != null && province == province.nation.capital)
                        {
                            // 수도: 도로 상태에 따라 파랑 또는 보라
                            if (province.road == 1)
                                RecolorProvince(c, new Color32(100, 100, 255, 255));
                            else
                                RecolorProvince(c, new Color32(200, 100, 200, 255));
                        }
                        else
                        {
                            // 일반 프로빈스: 도로 상태에 따라 초록 또는 빨강
                            if (province.road == 1)
                                RecolorProvince(c, new Color32(100, 255, 100, 255));
                            else
                                RecolorProvince(c, new Color32(255, 100, 100, 255));
                        }
                    }
                    else
                    {
                        // 다른 국가의 프로빈스는 원래 색상으로 복원
                        if (originalColors.TryGetValue(c, out Color32 col))
                            RecolorProvince(c, col);
                    }
                }
            }
        }
        provinceColorMode = 3;
    }

    private void ColorOnNormalMode()
    {
        // 모든 프로빈스를 해당 국가의 색상으로 색칠
        foreach (var c in children)
        {
            if (GlobalVariables.PROVINCES.TryGetValue(c.name, out Province province))
            {
                if (province.nation != null)
                {
                    RecolorProvince(c, province.nation.color);
                }
                else
                {
                    // 국가가 없는 경우 원래 색상으로 복원
                    if (originalColors.TryGetValue(c, out Color32 col))
                        RecolorProvince(c, col);
                }
            }
        }
        provinceColorMode = 0;
    }

    private void HandleProvinceColoring()
    {
        bool buildSubActive = false;
        if (BuildUI.Instance != null && BuildUI.Instance.subUIs != null && BuildUI.Instance.subUIs.Count > 2)
        {
            buildSubActive = BuildUI.Instance.subUIs[2].activeInHierarchy;
        }

        bool nationSubActive = false;
        if (NationUI.Instance != null && NationUI.Instance.subUIs != null && NationUI.Instance.subUIs.Count > 0)
        {
            nationSubActive = NationUI.Instance.gameObject.activeInHierarchy;
        }

        bool provinceSubActive = false;
        if (ProvinceDetailUI.Instance != null && ProvinceDetailUI.Instance.subUIs != null && ProvinceDetailUI.Instance.subUIs.Count > 0)
        {
            //ProvinceDetailUI의 GameObject가 활성화되어 있는지 확인
            provinceSubActive = ProvinceDetailUI.Instance.gameObject.activeInHierarchy;
            
        }

        if (buildSubActive && provinceColorMode != 1)
        // 길 건설 모드 활성화
        {
            ColorOnBuildSubEnabled();
        }
        else if (nationSubActive && (provinceColorMode != 2 || previousNation != NationUI.Instance.CurrentNation ))
        // 국가 모드
        {
            previousNation = NationUI.Instance.CurrentNation;
            ColorOnNationUIEnabled(nationSubActive);
        }
        else if (provinceSubActive && (provinceColorMode != 3 || previousProvince != ProvinceDetailUI.Instance.CurrentProvince ))
        // 프로빈스 모드
        {
            previousProvince = ProvinceDetailUI.Instance.CurrentProvince;
            ColorOnProvinceUIEnabled(provinceSubActive);
        }
        else if (!buildSubActive && !nationSubActive && !provinceSubActive && provinceColorMode != 0)
        // 길 건설 모드 비활성화
        {
            ColorOnNormalMode();
        }

        isBuildSubActive = buildSubActive;
    }

    private void HandleHoverAndSelection()
    {
        GameObject child = HitChild();

        // Normal behavior when build sub is not active
        if (!child)
        {
            RemoveOutline();
            return;
        }


        // child 있고, 마우스 왼버튼 클릭 시
        if (child && Input.GetMouseButtonDown(0))
        {
            // 길 건설 모드가 활성화되어 있을때, child의 이름을 가진 Province의 road가 0이면 1로
            if (isBuildSubActive)
            {
                if (GlobalVariables.PROVINCES.TryGetValue(child.name, out Province province))
                {
                    // 내 국가의 영토인지 확인
                    Nation currentNation = GameManager.Instance.player.nation;
                    if (province.nation != currentNation)
                    {
                        // 다른 국가의 영토이면 아무 동작도 하지 않음
                        return;
                    }
                    
                    // 도로 건설/제거 및 수도 반영해서 색칠
                    if (province.road == 0)
                    {
                        province.BuildRoad();
                        // 수도인지 확인
                        if (province == currentNation.capital)
                        {
                            RecolorProvince(child, new Color32(100, 100, 255, 255)); // 파랑
                        }
                        else
                        {
                            RecolorProvince(child, new Color32(100, 255, 100, 255)); // 초록
                        }
                    }
                    else
                    {
                        province.RemoveRoad();
                        // 수도인지 확인
                        if (province == currentNation.capital)
                        {
                            RecolorProvince(child, new Color32(200, 100, 200, 255)); // 보라
                        }
                        else
                        {
                            RecolorProvince(child, new Color32(255, 100, 100, 255)); // 빨강
                        }
                    }
                }
            }
            else
            {
                OpenNationUI(child);
            }
        }

        OutlineProvince(child);

    }

    private bool IsPointerOverUIObject()
    {
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
        return results.Count > 0;
    }

    GameObject HitChild()
    {
        RaycastHit hit;
        // 마우스 클릭 위치에 Raycast를 쏴서 충돌이 있는지 확인
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
        {
            return null;
        }

        GameObject obj = hit.transform.gameObject;
        MeshCollider meshCollider = hit.collider as MeshCollider;

        // 충돌한 오브젝트에 유효한 텍스처가 있는지 확인
        if (!children.Contains(obj))
        {
            return null;
        }

        if (IsPointerOverUIObject())
        {
            return null;
        }

        //if (EventSystem.current.IsPointerOverGameObject())
        //    return null;

        return obj;
    }

    void OpenNationUI(GameObject child)
    {
        //Province cur;
        string name = child.name;
        Debug.Log(name);
        Province cur;
        if (GlobalVariables.PROVINCES.TryGetValue(name, out cur))
        {
            //if (cur.nation != null)
            //    NationUI.Instance.OpenNationUI(cur.nation);
            //else
            //    ProvinceDetailUI.Instance.OpenProvinceDetailUI(cur);
            ProvinceDetailUI.Instance.OpenProvinceDetailUI(cur);
        }
        
    }

    /// <summary>
    /// 클릭한 위치의 프로빈스를 감지하고 색칠하는 함수
    /// </summary>
    void OutlineProvince(GameObject child)
    {
        if (!outlined.Contains(child))
        {
            RemoveOutline();
            AddOutline(child);
        }
    }

    /// <summary>
    /// Province GameObject의 material을 주어진 색상으로 변경합니다.
    /// </summary>
    void RecolorProvince(GameObject child, Color32 color)
    {
        if (!TryGetProvinceMaterial(child, out Material mat))
            return;

        mat.color = color;
    }

    
    void RemoveOutline()
    {
        for(int i=outlined.Count-1; i>=0; i--)
        {
            GameObject province = outlined[i];
            var outline = province.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            outlined.RemoveAt(i);
        }
        ClearHoverBorder();
    }

    void AddOutline(GameObject province)
    {
        var outline = province.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        CreateHoverBorder(province);
        outlined.Add(province);
    }

    private void ClearHoverBorder()
    {
        for (int i = 0; i < activeHoverBorderLineCount; i++)
        {
            if (hoverBorderLinePool[i] != null)
                hoverBorderLinePool[i].gameObject.SetActive(false);
        }

        activeHoverBorderLineCount = 0;
    }

    private void CreateHoverBorder(GameObject province)
    {
        MeshFilter meshFilter = province.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        foreach (Vector3[] loop in GetCachedHoverBorderLoops(meshFilter.sharedMesh))
        {
            if (loop.Length < 2)
                continue;

            LineRenderer lineRenderer = GetHoverBorderLineRenderer(province.name);
            lineRenderer.startColor = hoverBorderColor;
            lineRenderer.endColor = hoverBorderColor;
            lineRenderer.widthMultiplier = hoverBorderWidth;

            Vector3[] points = new Vector3[loop.Length + 1];
            for (int i = 0; i < loop.Length; i++)
                points[i] = province.transform.TransformPoint(loop[i]) + Vector3.up * hoverBorderHeightOffset;

            points[^1] = points[0];
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
        }
    }

    private List<Vector3[]> GetCachedHoverBorderLoops(Mesh mesh)
    {
        if (hoverBorderCache.TryGetValue(mesh, out List<Vector3[]> cachedLoops))
            return cachedLoops;

        List<Vector3[]> loops = new();
        foreach (List<Vector3> loop in FindBoundaryLoops(mesh))
        {
            List<Vector3> simplifiedLoop = SimplifyClosedLoop(loop, hoverBorderSimplifyTolerance);
            List<Vector3> smoothedLoop = SmoothClosedLoop(simplifiedLoop, hoverBorderSmoothIterations);
            if (smoothedLoop.Count > 2)
                loops.Add(smoothedLoop.ToArray());
        }

        hoverBorderCache[mesh] = loops;
        return loops;
    }

    private void PrewarmHoverBorderCache()
    {
        foreach (GameObject child in children)
        {
            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
                GetCachedHoverBorderLoops(meshFilter.sharedMesh);
        }
    }

    private LineRenderer GetHoverBorderLineRenderer(string provinceName)
    {
        LineRenderer lineRenderer;
        if (activeHoverBorderLineCount < hoverBorderLinePool.Count)
        {
            lineRenderer = hoverBorderLinePool[activeHoverBorderLineCount];
        }
        else
        {
            GameObject borderLine = new GameObject("HoverBorderLine");
            borderLine.transform.SetParent(transform, false);
            lineRenderer = borderLine.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.material = GetHoverBorderMaterial();
            lineRenderer.numCornerVertices = 6;
            lineRenderer.numCapVertices = 6;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            hoverBorderLinePool.Add(lineRenderer);
        }

        activeHoverBorderLineCount++;
        lineRenderer.gameObject.name = $"{provinceName}_HoverBorder";
        lineRenderer.gameObject.SetActive(true);
        return lineRenderer;
    }

    private List<Vector3> SimplifyClosedLoop(List<Vector3> loop, float tolerance)
    {
        if (loop.Count <= 3 || tolerance <= 0f)
            return new List<Vector3>(loop);

        List<Vector3> simplified = new() { loop[0] };
        float toleranceSqr = tolerance * tolerance;
        int anchor = 0;

        for (int i = 2; i < loop.Count; i++)
        {
            float distanceSqr = DistancePointToSegmentSqr(loop[i - 1], loop[anchor], loop[i]);
            if (distanceSqr > toleranceSqr)
            {
                simplified.Add(loop[i - 1]);
                anchor = i - 1;
            }
        }

        if (!ApproximatelySamePoint(simplified[^1], loop[^1]))
            simplified.Add(loop[^1]);

        return simplified.Count > 2 ? simplified : new List<Vector3>(loop);
    }

    private List<Vector3> SmoothClosedLoop(List<Vector3> loop, int iterations)
    {
        if (loop.Count <= 3 || iterations <= 0)
            return loop;

        List<Vector3> smoothed = new(loop);
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            List<Vector3> next = new(smoothed.Count * 2);
            for (int i = 0; i < smoothed.Count; i++)
            {
                Vector3 current = smoothed[i];
                Vector3 following = smoothed[(i + 1) % smoothed.Count];
                next.Add(Vector3.Lerp(current, following, 0.25f));
                next.Add(Vector3.Lerp(current, following, 0.75f));
            }

            smoothed = next;
        }

        return smoothed;
    }

    private float DistancePointToSegmentSqr(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= Mathf.Epsilon)
            return (point - start).sqrMagnitude;

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSqr);
        Vector3 projection = start + segment * t;
        return (point - projection).sqrMagnitude;
    }

    private bool ApproximatelySamePoint(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude < 0.0000001f;
    }

    private List<List<Vector3>> FindBoundaryLoops(Mesh mesh)
    {
        Dictionary<EdgeKey, int> edgeCounts = new();
        Dictionary<VertexKey, Vector3> vertexPositions = new();
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            VertexKey a = new VertexKey(vertices[triangles[i]]);
            VertexKey b = new VertexKey(vertices[triangles[i + 1]]);
            VertexKey c = new VertexKey(vertices[triangles[i + 2]]);

            vertexPositions.TryAdd(a, vertices[triangles[i]]);
            vertexPositions.TryAdd(b, vertices[triangles[i + 1]]);
            vertexPositions.TryAdd(c, vertices[triangles[i + 2]]);

            CountEdge(a, b, edgeCounts);
            CountEdge(b, c, edgeCounts);
            CountEdge(c, a, edgeCounts);
        }

        Dictionary<VertexKey, List<VertexKey>> adjacency = new();
        foreach (var pair in edgeCounts)
        {
            if (pair.Value != 1)
                continue;

            AddAdjacentVertex(pair.Key.A, pair.Key.B, adjacency);
            AddAdjacentVertex(pair.Key.B, pair.Key.A, adjacency);
        }

        List<List<Vector3>> loops = new();
        HashSet<EdgeKey> visitedEdges = new();
        foreach (var pair in edgeCounts)
        {
            if (pair.Value != 1 || visitedEdges.Contains(pair.Key))
                continue;

            List<VertexKey> loop = WalkBoundaryLoop(pair.Key.A, pair.Key.B, adjacency, visitedEdges, vertexPositions.Count);
            if (loop.Count > 2 && loop[0].Equals(loop[^1]))
                loops.Add(ConvertLoopToPositions(loop, vertexPositions));
        }

        return loops;
    }

    private List<VertexKey> WalkBoundaryLoop(VertexKey start, VertexKey next, Dictionary<VertexKey, List<VertexKey>> adjacency, HashSet<EdgeKey> visitedEdges, int vertexLimit)
    {
        List<VertexKey> loop = new() { start };
        VertexKey previous = default;
        VertexKey current = start;

        for (int guard = 0; guard < vertexLimit + 2; guard++)
        {
            EdgeKey currentEdge = new EdgeKey(current, next);
            if (!visitedEdges.Add(currentEdge))
                break;

            loop.Add(next);
            previous = current;
            current = next;

            if (current.Equals(start))
                break;

            if (!adjacency.TryGetValue(current, out List<VertexKey> neighbors))
                break;

            bool foundCandidate = false;
            VertexKey candidate = default;
            foreach (VertexKey neighbor in neighbors)
            {
                EdgeKey edge = new EdgeKey(current, neighbor);
                if (!neighbor.Equals(previous) && !visitedEdges.Contains(edge))
                {
                    candidate = neighbor;
                    foundCandidate = true;
                    break;
                }
            }

            if (!foundCandidate)
                break;

            next = candidate;
        }

        return loop;
    }

    private List<Vector3> ConvertLoopToPositions(List<VertexKey> loop, Dictionary<VertexKey, Vector3> vertexPositions)
    {
        List<Vector3> positions = new();
        int count = loop[^1].Equals(loop[0]) ? loop.Count - 1 : loop.Count;
        for (int i = 0; i < count; i++)
        {
            VertexKey key = loop[i];
            if (vertexPositions.TryGetValue(key, out Vector3 position))
                positions.Add(position);
        }

        return positions;
    }

    private void CountEdge(VertexKey a, VertexKey b, Dictionary<EdgeKey, int> edgeCounts)
    {
        EdgeKey edge = new EdgeKey(a, b);
        edgeCounts.TryGetValue(edge, out int count);
        edgeCounts[edge] = count + 1;
    }

    private void AddAdjacentVertex(VertexKey from, VertexKey to, Dictionary<VertexKey, List<VertexKey>> adjacency)
    {
        if (!adjacency.TryGetValue(from, out List<VertexKey> neighbors))
        {
            neighbors = new List<VertexKey>();
            adjacency[from] = neighbors;
        }

        neighbors.Add(to);
    }

    private Material GetHoverBorderMaterial()
    {
        if (hoverBorderMaterial != null)
            return hoverBorderMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        hoverBorderMaterial = new Material(shader);
        hoverBorderMaterial.color = hoverBorderColor;
        return hoverBorderMaterial;
    }

    private bool TryGetProvinceMaterial(GameObject province, out Material mat)
    {
        mat = null;
        if (province == null)
            return false;

        Renderer renderer = province.GetComponent<Renderer>();
        if (renderer == null)
            return false;

        mat = renderer.material;
        return mat != null;
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly VertexKey A;
        public readonly VertexKey B;

        public EdgeKey(VertexKey a, VertexKey b)
        {
            if (a.CompareTo(b) <= 0)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return A.Equals(other.A) && B.Equals(other.B);
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(A, B);
        }
    }

    private readonly struct VertexKey : IEquatable<VertexKey>, IComparable<VertexKey>
    {
        private const float Precision = 10000f;

        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public VertexKey(Vector3 position)
        {
            X = Mathf.RoundToInt(position.x * Precision);
            Y = Mathf.RoundToInt(position.y * Precision);
            Z = Mathf.RoundToInt(position.z * Precision);
        }

        public bool Equals(VertexKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public int CompareTo(VertexKey other)
        {
            int xCompare = X.CompareTo(other.X);
            if (xCompare != 0)
                return xCompare;

            int yCompare = Y.CompareTo(other.Y);
            if (yCompare != 0)
                return yCompare;

            return Z.CompareTo(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is VertexKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
    
}
