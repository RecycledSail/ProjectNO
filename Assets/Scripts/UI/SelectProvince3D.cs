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

    void Start()
    {
        children = new();
        outlined = new();
        for(int i = 0, count = this.transform.childCount; i < count; i++)
        {
            GameObject child = this.transform.GetChild(i).gameObject;
            children.Add(child);
        }
    }

    void Update()
    {
        GameObject child = HitChild();
        if (!child)
        {
            RemoveOutline();
            return;
        }

        if (child && Input.GetMouseButton(0))
        {
            OpenNationUI(child);
        }

        OutlineProvince(child); // 프로빈스 색칠 함수 호출
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
    //void RemoveColors()
    //{
    //    List<Vector2> list;
    //    // 이전에 칠했던 색상을 원래대로 되돌림
    //    while (paintedColors.Count != 0)
    //    {
    //        Color32 painted = paintedColors.Pop();
    //        if (colorToVec2.TryGetValue(painted, out list))
    //        {
    //            foreach (Vector2 v in list)
    //            {
    //                tex_0.SetPixel((int)v.x, (int)v.y, painted);
    //            }
    //        }
    //    }
    //}

    ///// <summary>
    ///// 선택한 프로빈스와 인접한 프로빈스들을 색칠하는 함수
    ///// </summary>
    ///// <param name = "c" > 선택한 색상</param>
    ///// <param name = "tex_0" > 변경할 텍스처</param>
    //void ColorNewProvinces(Color32 c)
    //{
    //    List<Vector2> list;
    //    paintedColors.Push(c); // 현재 색상을 스택에 저장

    //    // 현재 색상에 해당하는 모든 픽셀을 파란색으로 변경
    //    if (colorToVec2.TryGetValue(c, out list))
    //    {
    //        foreach (Vector2 v in list)
    //        {
    //            tex_0.SetPixel((int)v.x, (int)v.y, Color.blue);
    //        }
    //    }

    //    // 현재 프로빈스를 가져옴
    //    Province cur;
    //    if (GlobalVariables.COLORTOPROVINCE.TryGetValue(c, out cur))
    //    {
    //        List<Province> provinces;
    //        // 현재 프로빈스와 인접한 프로빈스 목록을 가져옴
    //        if (GlobalVariables.ADJACENT_PROVINCES.TryGetValue(cur.name, out provinces))
    //        {
    //            foreach (Province province in provinces)
    //            {
    //                Color32 provColor = province.color;
    //                List<Vector2> provPixelVec;

    //                // 인접한 프로빈스의 픽셀을 하늘색으로 변경
    //                if (colorToVec2.TryGetValue(provColor, out provPixelVec))
    //                {
    //                    foreach (Vector2 v in provPixelVec)
    //                    {
    //                        tex_0.SetPixel((int)v.x, (int)v.y, Color.cyan);
    //                    }
    //                    paintedColors.Push(provColor); // 변경한 색상을 스택에 저장
    //                }
    //            }
    //        }
    //    }
    //}
}
