using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public enum Topography
{
    Plane,
    Mountain,
    Sea
}
class Province
{
    public int id;
    public String name;
    public long population;
    public Topography topo;
    public Province(int id, String name, long population, Topography topo)
    {
        // this -> instance의 id
        this.id = id;
        this.name = name;
        this.population = population;
        this.topo = topo;
    }
}
public class SelectProvince : MonoBehaviour
{
    public Camera cam;
    public Texture2D initTex;
    private Renderer hereRend;
    private bool isWorking;
    Dictionary<Color32, Province> mapLookUpTable;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //cam = GetComponent<Camera>();
        hereRend = transform.GetComponent<Renderer>();
        isWorking = false;
        Texture2D clone = Instantiate(initTex);
        hereRend.materials[0].mainTexture = clone;
        clone.Apply();
        Color32 c = new Color(0.220f, 0.980f, 0.980f, 1.000f);
        Province seaProvince = new Province(0, "Sea", 530, Topography.Sea);
        //Color32 -> Province
        mapLookUpTable = new Dictionary<Color32, Province>
        {
            {c, seaProvince}
        };
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
            // 지금 클릭한 텍스쳐의 픽셀컬러값
            Color32 c = tex_1.GetPixel((int)pixelUV.x, (int)pixelUV.y);

            Province a = null;
            if (mapLookUpTable.TryGetValue(c, out a))
            {
                Debug.Log("Province found! id="+a.id + "\nname=" + a.name + "\npopulation=" + a.population);
                switch (a.topo)
                {
                    case Topography.Mountain:
                        Debug.Log("This is Mountain");
                        break;
                    case Topography.Plane:
                        Debug.Log("This is Plane");
                        break;
                    case Topography.Sea:
                        Debug.Log("This is Sea");
                        break;
                }
            }
            else
            {
                Debug.Log("not in table");
            }

            Debug.Log(c);
            Queue<Vector2> q = new Queue<Vector2>();
            q.Enqueue(pixelUV);

            Vector2[] moves = new Vector2[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) };
            tex_0.SetPixel((int)pixelUV.x, (int)pixelUV.y, Color.black);
            for (var i = 0; i < tex_1.width; i++)
            {
                for (var j = 0; j < tex_1.width; j++)
                {
                    if (c.Equals((Color32)tex_1.GetPixel(i, j)))
                        tex_0.SetPixel(i, j, Color.black);
                }
            }
            tex_0.Apply();
            isWorking = false;
        }
    }
}
