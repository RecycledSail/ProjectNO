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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //cam = GetComponent<Camera>();
        hereRend = transform.GetComponent<Renderer>();
        isWorking = false;
        Texture2D clone = Instantiate(initTex);
        hereRend.materials[0].mainTexture = clone;
        clone.Apply();
        Debug.Log("done");
    }

    // Update is called once per frame
    void Update()
    {
        if (!isWorking)
        {
            
            if (!Input.GetMouseButton(0))
                return;

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
            Color c = tex_1.GetPixel((int)pixelUV.x, (int)pixelUV.y);
            Debug.Log(c);
            Queue<Vector2> q = new Queue<Vector2>();
            q.Enqueue(pixelUV);

            Vector2[] moves = new Vector2[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) };
            tex_0.SetPixel((int)pixelUV.x, (int)pixelUV.y, Color.black);
            for (var i = 0; i < tex_1.width; i++)
            {
                for (var j = 0; j < tex_1.width; j++)
                {
                    if (tex_1.GetPixel(i, j) == c)
                        tex_0.SetPixel(i, j, Color.black);
                }
            }
            tex_0.Apply();
            isWorking = false;
        }
    }
}
