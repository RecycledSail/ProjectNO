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
    /// Pie Chart를 업데이트, ProvinceDetailUI에서 호출
    /// </summary>
    /// <param name="pops">Province의 pops</param>
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
    /// Pie Element를 Instantiate시킨다. 각 요소는 Species별 인구 비율
    /// </summary>
    /// <param name="fillAmount">파이의 비율, UpdatePieChart에서 관리하며 1부터 줄여나가는 방법으로 겹치기</param>
    /// <param name="index">colorPool에서 어떤 색을 사용할지 인덱스</param>
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
