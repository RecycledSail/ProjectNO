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
    private bool childrenColoredByBuildUI = false; // subUIs[2]에 의해 색칠된 상태인지
    private bool isBuildSubActive = false;

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
    }

    void Update()
    {
        HandleBuildUIColoring();
        HandleHoverAndSelection();
    }

    private void HandleBuildUIColoring()
    {
        bool buildSubActive = false;
        if (BuildUI.Instance != null && BuildUI.Instance.subUIs != null && BuildUI.Instance.subUIs.Count > 2)
        {
            buildSubActive = BuildUI.Instance.subUIs[2].activeSelf;
        }

        if (buildSubActive)
        {
            if (!childrenColoredByBuildUI)
            {
                foreach (var c in children)
                {
                    // child의 이름인 provinced의 road가 1이면 초록색, 0이면 빨간색
                    
                    if (GlobalVariables.PROVINCES.TryGetValue(c.name, out Province province))
                    {
                        if (province.road == 1)
                            RecolorProvince(c, new Color32(100, 255, 100, 255));
                        else
                            RecolorProvince(c, new Color32(255, 100, 100, 255));
                    }
                }
                childrenColoredByBuildUI = true;
            }
        }
        else
        {
            if (childrenColoredByBuildUI)
            {
                foreach (var c in children)
                {
                    if (originalColors.TryGetValue(c, out Color32 col))
                        RecolorProvince(c, col);
                }
                childrenColoredByBuildUI = false;
            }
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
                    if (province.road == 0)
                    {
                        province.road = 1;
                        // 좀 연한 초록색으로 변경
                        RecolorProvince(child, new Color32(100, 255, 100, 255));
                    }
                    else
                    {
                        province.road = 0;
                        // 좀 연한 빨강색으로 변경
                        RecolorProvince(child, new Color32(255, 100, 100, 255));
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
