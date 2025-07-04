using UnityEngine;
using UnityEngine.UI;

public class PieChart : MonoBehaviour
{
    public GameObject pieChartElement;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector2 widthHeight = gameObject.GetComponent<RectTransform>().sizeDelta;
        GameObject newObject = Instantiate(pieChartElement, transform);
        var rect = newObject.GetComponent<RectTransform>();
        rect.sizeDelta = widthHeight;
        Image image = newObject.GetComponent<Image>();
        image.fillAmount = 0.56f;
        image.color = Color.yellow;

        newObject = Instantiate(pieChartElement, transform);
        rect = newObject.GetComponent<RectTransform>();
        rect.sizeDelta = widthHeight;
        image = newObject.GetComponent<Image>();
        image.fillAmount = 0.47f;
        image.color = Color.black;

        newObject = Instantiate(pieChartElement, transform);
        rect = newObject.GetComponent<RectTransform>();
        rect.sizeDelta = widthHeight;
        image = newObject.GetComponent<Image>();
        image.fillAmount = 0.15f;
        image.color = Color.red;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
