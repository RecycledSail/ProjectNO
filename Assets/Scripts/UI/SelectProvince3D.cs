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
    private int provinceColorMode = 0; // 0: 기본, 1: 길 건설 모드

    private Nation previousNation = null;
    private Province previousProvince = null;

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
    }

    void Update()
    {
        HandleProvinceColoring();
        HandleHoverAndSelection();
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
        Material mat = child.GetComponent<Renderer>().material;
        mat.color = color;
    }

    
    void RemoveOutline()
    {
        for(int i=outlined.Count-1; i>=0; i--)
        {
            GameObject province = outlined[i];
            var outline = province.GetComponent<Outline>();
            outline.enabled = false;
            outlined.RemoveAt(i);
        }
    }

    void AddOutline(GameObject province)
    {
        var outline = province.GetComponent<Outline>();
        outline.enabled = true;
        outlined.Add(province);
    }
    
}
