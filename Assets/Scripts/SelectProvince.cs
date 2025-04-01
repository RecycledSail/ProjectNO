using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// SelectProvince Ŭ������ ���콺 Ŭ���� ���� Ư�� ����(Province)�� �����ϰ�, 
/// �ش� ������ ������ ������ ���� �����ϴ� ����� �����մϴ�.
/// </summary>
public class SelectProvince : MonoBehaviour
{
    public Camera cam; // ȭ���� ���ߴ� ī�޶�
    public Texture2D initTex; // �ʱ� �ؽ�ó
    private Renderer hereRend; // ���� ������Ʈ�� Renderer
    private bool isWorking; // ��ĥ �۾��� ���� ������ ����
    private Color32 prevColor; // ������ ������ ����
    private Stack<Color32> paintedColors; // ��ĥ�� ������� �����ϴ� ����

    // �� ���� �ش��ϴ� �ȼ� ��ǥ ����Ʈ�� �����ϴ� ��ųʸ�
    Dictionary<Color32, List<Vector2>> colorToVec2;

    void Start()
    {
        // Renderer ������Ʈ ��������
        hereRend = transform.GetComponent<Renderer>();
        isWorking = false;

        // �ʱ� �ؽ�ó�� �����Ͽ� ��� (������ �������� �ʱ� ����)
        Texture2D clone = Instantiate(initTex);
        hereRend.materials[0].mainTexture = clone;
        clone.Apply();

        colorToVec2 = new Dictionary<Color32, List<Vector2>>();
        paintedColors = new Stack<Color32>();

        // 2��° ��Ƽ������ �ؽ�ó�� ������ (�� ������ �����ϴ� �ؽ�ó)
        Texture2D lookUp = hereRend.materials[1].mainTexture as Texture2D;

        // �ؽ�ó�� ��� �ȼ��� ��ȸ�ϸ�, �� ������ ��ǥ�� ����
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

        prevColor = new Color32(0, 0, 0, 0); // ���� ���� �ʱ�ȭ
    }

    void Update()
    {
        // ���콺 Ŭ�� ���θ� Ȯ���ϴ� �κ��� �ּ� ó���Ǿ� ����
        // if (!Input.GetMouseButton(0))
        //    return;

        ColorProvince(); // ���κ� ��ĥ �Լ� ȣ��
    }

    /// <summary>
    /// Ŭ���� ��ġ�� ���κ󽺸� �����ϰ� ��ĥ�ϴ� �Լ�
    /// </summary>
    void ColorProvince()
    {
        RaycastHit hit;
        // ���콺 Ŭ�� ��ġ�� Raycast�� ���� �浹�� �ִ��� Ȯ��
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
            return;

        Renderer rend = hit.transform.GetComponent<Renderer>();
        MeshCollider meshCollider = hit.collider as MeshCollider;

        // �浹�� ������Ʈ�� ��ȿ�� �ؽ�ó�� �ִ��� Ȯ��
        if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
            return;

        isWorking = true;

        // Ŭ���� ������Ʈ�� �ؽ�ó ��������
        Texture2D tex_0 = rend.materials[0].mainTexture as Texture2D; // ����� �ؽ�ó
        Texture2D tex_1 = rend.materials[1].mainTexture as Texture2D; // ������ �Ǵ� �ؽ�ó

        // Ŭ���� ��ġ�� UV ��ǥ�� ������ �ȼ� ��ǥ�� ��ȯ
        Vector2 pixelUV = hit.textureCoord;
        pixelUV.x *= tex_1.width;
        pixelUV.y *= tex_1.height;

        // Ŭ���� �ȼ��� ������ ������
        Color32 c = tex_1.GetPixel((int)pixelUV.x, (int)pixelUV.y);

        // ������ Ŭ���� ����� �ٸ��� ��ĥ �۾� ����
        if (!prevColor.Equals(c))
        {
            List<Vector2> list;

            // ������ ĥ�ߴ� ������ ������� �ǵ���
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

            // ������ ������ ��ȿ�� ���κ����� Ȯ�� �� ��ĥ
            if (GlobalVariables.COLORTOPROVINCE.ContainsKey(c))
            {
                ColorNewProvinces(c, tex_0);
            }

            prevColor = c; // ���� ���� ����
        }

        tex_0.Apply(); // �ؽ�ó ���� ����
        isWorking = false;
    }

    /// <summary>
    /// ������ ���κ󽺿� ������ ���κ󽺵��� ��ĥ�ϴ� �Լ�
    /// </summary>
    /// <param name="c">������ ����</param>
    /// <param name="tex_0">������ �ؽ�ó</param>
    void ColorNewProvinces(Color32 c, Texture2D tex_0)
    {
        List<Vector2> list;
        paintedColors.Push(c); // ���� ������ ���ÿ� ����

        // ���� ���� �ش��ϴ� ��� �ȼ��� �Ķ������� ����
        if (colorToVec2.TryGetValue(c, out list))
        {
            foreach (Vector2 v in list)
            {
                tex_0.SetPixel((int)v.x, (int)v.y, Color.blue);
            }
        }

        // ���� ���κ󽺸� ������
        Province cur;
        if (GlobalVariables.COLORTOPROVINCE.TryGetValue(c, out cur))
        {
            List<Province> provinces;
            // ���� ���κ󽺿� ������ ���κ� ����� ������
            if (GlobalVariables.ADJACENT_PROVINCES.TryGetValue(cur.name, out provinces))
            {
                foreach (Province province in provinces)
                {
                    Color32 provColor = province.color;
                    List<Vector2> provPixelVec;

                    // ������ ���κ��� �ȼ��� �ϴû����� ����
                    if (colorToVec2.TryGetValue(provColor, out provPixelVec))
                    {
                        foreach (Vector2 v in provPixelVec)
                        {
                            tex_0.SetPixel((int)v.x, (int)v.y, Color.cyan);
                        }
                        paintedColors.Push(provColor); // ������ ������ ���ÿ� ����
                    }
                }
            }
        }
    }
}
