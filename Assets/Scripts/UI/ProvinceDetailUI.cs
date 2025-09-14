using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


public class ProvinceDetailUI : MonoBehaviour
{
    public GameObject uiPanel;
    public TMP_Text provinceNameText;
    public TMP_Text provinceDescriptionText;
    public TMP_Text provincePopulationText;

    //Stats panel
    public TMP_Text provinceCropsText;
    public PieChart racesPieChart;
    private Province province;

    //Market panel
    public Transform marketPanel;
    public GameObject marketChild; 

    // Panels to change
    public List<GameObject> subUIs;

    private GameObject currentOpenSubUI;

    private float timer = 0.0f;

    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static ProvinceDetailUI _instance;
    public static ProvinceDetailUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(ProvinceDetailUI)) as ProvinceDetailUI;

            return _instance;
        }
    }

    /// <summary>
    /// 게임 시작 전 초기화 메서드 (싱글톤 중복방지 처리)
    /// </summary>
    private void Awake()
    {
        // 싱글톤 중복 방지 로직
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // 중복 시 제거
        }
    }

    private void Start()
    {
        for (int i = 0; i < subUIs.Count; i++)
        {
            if (i != 0)
            {
                subUIs[i].SetActive(false);
            }
            else
            {
                subUIs[i].SetActive(true);
                currentOpenSubUI = subUIs[i];
            }
        }
        uiPanel.SetActive(false); // 처음에는 UI를 숨김
        GameManager.Instance.dayEvent.AddListener(UpdateProvinceUI);
    }

    private void Update()
    {
        
        //if (province != null)
        //{
        //    // 1.0초마다 갱신
        //    timer += Time.deltaTime;
        //    if(timer >= 1.0f)
        //    {
        //        UpdateUI();
        //        timer -= 1.0f;
        //    }
        //}
    }

    private void UpdateProvinceUI()
    {
        if (province != null)
        {
            UpdateUI();
        }
    }

    /// <summary>
    /// Province Detail UI를 설정하고 여는 메서드
    /// </summary>
    /// <param name="province">보여줄 프로빈스</param>
    public void OpenProvinceDetailUI(Province province)
    {
        this.province = province;
        provinceNameText.text = province.name;
        string topoString = "Default Topology";
        switch (province.topo)
        {
            case Topography.Plane:
                topoString = "Plane";
                break;
            case Topography.Mountain:
                topoString = "Mountain";
                break;
            case Topography.Sea:
                topoString = "Sea";
                break;
            default:
                break;
        }
        provinceDescriptionText.text = "Topology: " + topoString;
        timer = 0.0f;
        InitMarketPanel();
        UpdateUI();
        UIManager.Instance.ReplacePopUp(gameObject);
    }

    /// <summary>
    /// UI 업데이트
    /// </summary>
    private void UpdateUI()
    {
        UpdatePopulationText();
        UpdateCropsText();
        UpdatePieChart();
    }

    /// <summary>
    /// 인구수 텍스트를 업데이트
    /// </summary>
    private void UpdatePopulationText()
    {
        provincePopulationText.text = "";
        bool isTop = true;
        foreach (Species species in province.pops)
        {
            if (!isTop)
                provincePopulationText.text += "\n";
            provincePopulationText.text += "PopKind: " + species.name + " Pop: " + UIManager.ShortenValue(species.population);
            isTop = false;
        }
    }

    /// <summary>
    /// 작물 수 텍스트를 업데이트
    /// </summary>
    private void UpdateCropsText()
    {
        //provinceCropsText.text = "Crops: " + string.Join(", ",
        //    province.market.crops.Select(crop => $"{crop.name}: {UIManager.ShortenValue(crop.amount)}")
        //);
    }

    /// <summary>
    /// 파이 차트를 업데이트
    /// </summary>
    private void UpdatePieChart()
    {
        racesPieChart.UpdatePieChart(province.pops);
    }

    private void InitMarketPanel()
    {
        // 기존 리스트 정리
        foreach (Transform child in marketPanel)
        {
            Destroy(child.gameObject);
        }

        ProvinceMarket pm = province.market;
        foreach(var kv in GlobalVariables.PRODUCTS)
        {
            GameObject child = Instantiate(marketChild, marketPanel);
            ProductButtonUI productButtonUI = child.GetComponent<ProductButtonUI>();
            string product = kv.Key;
            ProductState ps;
            if(pm.Products.TryGetValue(product, out ps))
            {
                productButtonUI.SetProductOrigin(ps);
            }
        }
    }

    /// <summary>
    /// 현재 활성화된 SubUI를 변경한다.
    /// </summary>
    /// <param name="index">변경할 SubUI의 index</param>
    public void ChangeSubUI(int index)
    {
        currentOpenSubUI.SetActive(false);
        subUIs[index].SetActive(true);
        currentOpenSubUI = subUIs[index];
    }
}
