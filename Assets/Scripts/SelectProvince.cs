using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// SelectProvince 클래스는 마우스 클릭을 통해 특정 지역(Province)을 선택하고, 
/// 해당 지역과 인접한 지역을 색상 변경하는 기능을 수행합니다.
/// </summary>
public class SelectProvince : MonoBehaviour
{
    public Camera cam; // 화면을 비추는 카메라
    public Texture2D initTex; // 초기 텍스처


    private Renderer hereRend; // 현재 오브젝트의 Renderer
    private Color32 prevColor; // 이전에 선택한 색상
    private Stack<Color32> paintedColors; // 색칠한 색상들을 저장하는 스택

    // 각 색상에 해당하는 픽셀 좌표 리스트를 저장하는 딕셔너리
    private Dictionary<Color32, List<Vector2>> colorToVec2;

    // 클릭한 오브젝트의 텍스처 가져오기
    private Texture2D tex_0; // 변경될 텍스처
    private Texture2D tex_1; // 기준이 되는 텍스처

    void Start()
    {
        // Renderer 컴포넌트 가져오기
        hereRend = transform.GetComponent<Renderer>();

        // 초기 텍스처를 복제하여 사용 (원본을 변경하지 않기 위해)
        Texture2D clone = Instantiate(initTex);
        hereRend.materials[0].mainTexture = clone;
        clone.Apply();

        colorToVec2 = new Dictionary<Color32, List<Vector2>>();
        paintedColors = new Stack<Color32>();

        // 2번째 머티리얼의 텍스처를 가져옴 (각 지역을 구분하는 텍스처)
        Texture2D lookUp = hereRend.materials[1].mainTexture as Texture2D;

        // 텍스처의 모든 픽셀을 순회하며, 각 색상의 좌표를 저장
        for (int i = 0; i < initTex.width; i++)
        {
            for (int j = 0; j < initTex.height; j++)
            {
                Color32 c = lookUp.GetPixel(i, j);
                List<Vector2> list;
                if (colorToVec2.TryGetValue(c, out list))
                {
                    list.Add(new Vector2(i, j));
                }
                else
                {
                    list = new List<Vector2> { new Vector2(i, j) };
                    colorToVec2.Add(c, list);
                }
            }
        }

        prevColor = new Color32(0, 0, 0, 0); // 이전 색상 초기화

        tex_0 = hereRend.materials[0].mainTexture as Texture2D; // 변경될 텍스처
        tex_1 = hereRend.materials[1].mainTexture as Texture2D; // 기준이 되는 텍스처
    }

    void Update()
    {
        // 마우스 클릭 여부를 확인하는 부분이 주석 처리되어 있음
        // if (!Input.GetMouseButton(0))
        //    return;
        if (Input.GetMouseButton(0))
        {
            OpenNationUI();
        }

        ColorProvince(); // 프로빈스 색칠 함수 호출
    }

    RaycastHit? HitRenderer()
    {
        RaycastHit hit;
        // 마우스 클릭 위치에 Raycast를 쏴서 충돌이 있는지 확인
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
            return null;


        Renderer rend = hit.transform.GetComponent<Renderer>();
        MeshCollider meshCollider = hit.collider as MeshCollider;

        // 충돌한 오브젝트에 유효한 텍스처가 있는지 확인
        if (rend != hereRend)
            return null;

        if (EventSystem.current.IsPointerOverGameObject())
            return null;

        return hit;
    }

    void OpenNationUI()
    {
        RaycastHit? hit = HitRenderer();
        if (!hit.HasValue) {
            return;
        }

        // 클릭한 위치의 UV 좌표를 가져와 픽셀 좌표로 변환
        Vector2 pixelUV = hit.Value.textureCoord;
        pixelUV.x *= tex_1.width;
        pixelUV.y *= tex_1.height;

        // 클릭한 픽셀의 색상을 가져옴
        Color32 c = tex_1.GetPixel((int)pixelUV.x, (int)pixelUV.y);

        Province cur;
        Debug.Log(c);
        if (GlobalVariables.COLORTOPROVINCE.TryGetValue(c, out cur))
        {
            if (cur.nation != null)
                NationUI.Instance.OpenNationUI(cur.nation);
            else
                ProvinceDetailUI.Instance.OpenProvinceDetailUI(cur);
        }
    }
    
    /// <summary>
    /// 클릭한 위치의 프로빈스를 감지하고 색칠하는 함수
    /// </summary>
    void ColorProvince()
    {
        RaycastHit? hit = HitRenderer();
        if (!hit.HasValue)
        {
            RemoveColors();
            prevColor = new Color32(0, 0, 0, 0); // 이전 색상 초기화
            tex_0.Apply(); // 텍스처 변경 적용
            return;
        }

        // 클릭한 위치의 UV 좌표를 가져와 픽셀 좌표로 변환
        Vector2 pixelUV = hit.Value.textureCoord;
        pixelUV.x *= tex_1.width;
        pixelUV.y *= tex_1.height;

        // 클릭한 픽셀의 색상을 가져옴
        Color32 c = tex_1.GetPixel((int)pixelUV.x, (int)pixelUV.y);

        // 이전에 클릭한 색상과 다르면 색칠 작업 수행
        if (!prevColor.Equals(c))
        {
            RemoveColors();
            // 선택한 색상이 유효한 프로빈스인지 확인 후 색칠
            if (GlobalVariables.COLORTOPROVINCE.ContainsKey(c))
            {
                //ColorNewProvinces(c);
            }

            prevColor = c; // 이전 색상 갱신
        }

        tex_0.Apply(); // 텍스처 변경 적용
    }

    void RemoveColors()
    {
        List<Vector2> list;
        // 이전에 칠했던 색상을 원래대로 되돌림
        while (paintedColors.Count != 0)
        {
            Color32 painted = paintedColors.Pop();
            if (colorToVec2.TryGetValue(painted, out list))
            {
                foreach (Vector2 v in list)
                {
                    tex_0.SetPixel((int)v.x, (int)v.y, painted);
                }
            }
        }
    }

    /// <summary>
    /// 선택한 프로빈스와 인접한 프로빈스들을 색칠하는 함수
    /// </summary>
    /// <param name="c">선택한 색상</param>
    /// <param name="tex_0">변경할 텍스처</param>
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
