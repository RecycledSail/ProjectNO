using TMPro;
using UnityEngine;

public class ProvinceDetailUI : MonoBehaviour
{
    public GameObject uiPanel;
    public TMP_Text provinceNameText;
    public TMP_Text provinceDescriptionText;
    public TMP_Text provincePopulationText;
    private Province province;


    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
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
    /// ���� ���� �� �ʱ�ȭ �޼��� (�̱��� �ߺ����� ó��)
    /// </summary>
    private void Awake()
    {
        // �̱��� �ߺ� ���� ����
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);  // �ߺ� �� ����
        }
    }

    private void Start()
    {
        uiPanel.SetActive(false); // ó������ UI�� ����
    }

    private void Update()
    {
    }

    /// <summary>
    /// Province Detail UI�� �����ϰ� ���� �޼���
    /// </summary>
    /// <param name="province">������ ���κ�</param>
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
        provincePopulationText.text = "Pop: " + UIManager.ShortenValue(province.population);

        UIManager.Instance.ReplacePopUp(gameObject);
    }
}
