using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UI;

public class PieChart : MonoBehaviour
{
    public GameObject pieChartElement;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<Color> colorPool = new()
    {
        Color.red,
        Color.yellow,
        Color.blue,
        Color.cyan
    };
    private List<GameObject> pieElements = new();
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Pie Chart�� ������Ʈ, ProvinceDetailUI���� ȣ��
    /// </summary>
    /// <param name="pops">Province�� pops</param>
    public void UpdatePieChart(List<Species> pops)
    {
        foreach(GameObject element in pieElements)
        {
            Destroy(element);
        }
        pieElements.Clear();

        List<Species> newList = new();
        float totalpop = 0;
        foreach(var species in pops)
        {
            newList.Add(species);
            totalpop += species.population;
        }
        newList.Sort((o1, o2) => o2.population - o1.population);

        float curpop = totalpop;
        int count = 0;
        foreach(var species in newList)
        {
            //Debug.Log("fillAmount = " + (curpop / totalpop) + ", PopKind: " + species.name + ", colorPool: " + count);
            InstantiatePieElement(curpop / totalpop, (count++) % colorPool.Count);
            curpop -= species.population;
        }
    }

    /// <summary>
    /// Pie Element�� Instantiate��Ų��. �� ��Ҵ� Species�� �α� ����
    /// </summary>
    /// <param name="fillAmount">������ ����, UpdatePieChart���� �����ϸ� 1���� �ٿ������� ������� ��ġ��</param>
    /// <param name="index">colorPool���� � ���� ������� �ε���</param>
    private void InstantiatePieElement(float fillAmount, int index)
    {
        Vector2 widthHeight = gameObject.GetComponent<RectTransform>().sizeDelta;
        GameObject newObject = Instantiate(pieChartElement, transform);
        var rect = newObject.GetComponent<RectTransform>();
        rect.sizeDelta = widthHeight;
        Image image = newObject.GetComponent<Image>();
        image.fillAmount = fillAmount;
        image.color = colorPool[index];
        pieElements.Add(newObject);
    }
}
