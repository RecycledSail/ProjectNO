using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// SelectProvince Ŭ������ ���콺 Ŭ���� ���� Ư�� ����(Province)�� �����ϰ�, 
/// �ش� ������ ������ ������ ���� �����ϴ� ����� �����մϴ�.
/// </summary>
public class SelectProvince3D : MonoBehaviour
{
    public Camera cam; // ȭ���� ���ߴ� ī�޶�
    private List<GameObject> children; // ���� ������Ʈ�� Children
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

        OutlineProvince(child); // ���κ� ��ĥ �Լ� ȣ��
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
        // ���콺 Ŭ�� ��ġ�� Raycast�� ���� �浹�� �ִ��� Ȯ��
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
        {
            return null;
        }

        GameObject obj = hit.transform.gameObject;
        MeshCollider meshCollider = hit.collider as MeshCollider;

        // �浹�� ������Ʈ�� ��ȿ�� �ؽ�ó�� �ִ��� Ȯ��
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
            if (cur.nation != null)
                NationUI.Instance.OpenNationUI(cur.nation);
            else
                ProvinceDetailUI.Instance.OpenProvinceDetailUI(cur);
        }
    }

    /// <summary>
    /// Ŭ���� ��ġ�� ���κ󽺸� �����ϰ� ��ĥ�ϴ� �Լ�
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
    //    // ������ ĥ�ߴ� ������ ������� �ǵ���
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
    ///// ������ ���κ󽺿� ������ ���κ󽺵��� ��ĥ�ϴ� �Լ�
    ///// </summary>
    ///// <param name = "c" > ������ ����</param>
    ///// <param name = "tex_0" > ������ �ؽ�ó</param>
    //void ColorNewProvinces(Color32 c)
    //{
    //    List<Vector2> list;
    //    paintedColors.Push(c); // ���� ������ ���ÿ� ����

    //    // ���� ���� �ش��ϴ� ��� �ȼ��� �Ķ������� ����
    //    if (colorToVec2.TryGetValue(c, out list))
    //    {
    //        foreach (Vector2 v in list)
    //        {
    //            tex_0.SetPixel((int)v.x, (int)v.y, Color.blue);
    //        }
    //    }

    //    // ���� ���κ󽺸� ������
    //    Province cur;
    //    if (GlobalVariables.COLORTOPROVINCE.TryGetValue(c, out cur))
    //    {
    //        List<Province> provinces;
    //        // ���� ���κ󽺿� ������ ���κ� ����� ������
    //        if (GlobalVariables.ADJACENT_PROVINCES.TryGetValue(cur.name, out provinces))
    //        {
    //            foreach (Province province in provinces)
    //            {
    //                Color32 provColor = province.color;
    //                List<Vector2> provPixelVec;

    //                // ������ ���κ��� �ȼ��� �ϴû����� ����
    //                if (colorToVec2.TryGetValue(provColor, out provPixelVec))
    //                {
    //                    foreach (Vector2 v in provPixelVec)
    //                    {
    //                        tex_0.SetPixel((int)v.x, (int)v.y, Color.cyan);
    //                    }
    //                    paintedColors.Push(provColor); // ������ ������ ���ÿ� ����
    //                }
    //            }
    //        }
    //    }
    //}
}
