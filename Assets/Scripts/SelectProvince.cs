using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public class SelectProvince : MonoBehaviour
{
    public Camera cam;
    public Texture2D initTex;
    private Renderer hereRend;
    private bool isWorking;
    private Color32 prevColor;
    private Stack<Color32> paintedColors;

    Dictionary<Color32, List<Vector2>> colorToVec2;

    void Start()
    {
        //cam = GetComponent<Camera>();
        hereRend = transform.GetComponent<Renderer>();
        isWorking = false;
        Texture2D clone = Instantiate(initTex);
        hereRend.materials[0].mainTexture = clone;
        clone.Apply();

        colorToVec2 = new Dictionary<Color32, List<Vector2>>();
        paintedColors = new Stack<Color32>();
        Texture2D lookUp = hereRend.materials[1].mainTexture as Texture2D;
        for (int i=0; i<initTex.width; i++)
        {
            for(int j=0; j<initTex.height; j++)
            {
                Color32 c = lookUp.GetPixel(i, j);
                List<Vector2> list;
                if (colorToVec2.TryGetValue(c, out list))
                {
                    list.Add(new Vector2(i, j));
                }
                else
                {
                    list = new List<Vector2>();
                    list.Add(new Vector2(i, j));
                    colorToVec2.Add(c, list);
                }
            }
        }

        prevColor = new Color32(0, 0, 0, 0);
    }

    // Update is called once per frame
    void Update()
    {
        // if (!Input.GetMouseButton(0))
        //    return;
        ColorProvince();
        
    }

    void ColorProvince()
    {
        RaycastHit hit;
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
            return;

        Renderer rend = hit.transform.GetComponent<Renderer>();
        MeshCollider meshCollider = hit.collider as MeshCollider;

        if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
            return;

        isWorking = true;
        Texture2D tex_0 = rend.materials[0].mainTexture as Texture2D;
        Texture2D tex_1 = rend.materials[1].mainTexture as Texture2D;
        Vector2 pixelUV = hit.textureCoord;
        pixelUV.x *= tex_1.width;
        pixelUV.y *= tex_1.height;
        // 지금 클릭한 텍스쳐의 픽셀컬러값
        Color32 c = tex_1.GetPixel((int)pixelUV.x, (int)pixelUV.y);


        if (!prevColor.Equals(c))
        {
            List<Vector2> list;

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

            if (GlobalVariables.COLORTOPROVINCE.ContainsKey(c))
            {
                ColorNewProvinces(c, tex_0);
            }

            prevColor = c;
        }

        tex_0.Apply();
        isWorking = false;
    }

    void ColorNewProvinces(Color32 c, Texture2D tex_0)
    {
        List<Vector2> list;
        paintedColors.Push(c);
        if (colorToVec2.TryGetValue(c, out list))
        {
            foreach (Vector2 v in list)
            {
                tex_0.SetPixel((int)v.x, (int)v.y, Color.blue);
            }
        }

        Province cur;
        if (GlobalVariables.COLORTOPROVINCE.TryGetValue(c, out cur))
        {
            List<Province> provinces;
            if (GlobalVariables.ADJACENT_PROVINCES.TryGetValue(cur.name, out provinces))
            {
                foreach (Province province in provinces)
                {
                    Color32 provColor = province.color;
                    List<Vector2> provPixelVec;
                    if (colorToVec2.TryGetValue(provColor, out provPixelVec))
                    {
                        foreach (Vector2 v in provPixelVec)
                        {
                            tex_0.SetPixel((int)v.x, (int)v.y, Color.cyan);
                        }
                        paintedColors.Push(provColor);
                    }
                }
            }
        }
    }
}
