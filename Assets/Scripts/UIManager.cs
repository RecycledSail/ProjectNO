using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    // Singleton instance
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindFirstObjectByType(typeof(UIManager)) as UIManager;
            }
            return _instance;
        }
    }

    /// <summary>
    /// ������ ���� ������ ���(ȭ��)�� ǥ���ϴ� �ؽ�Ʈ UI
    /// </summary>
    public TMP_Text CurrencyText;

    /// <summary>
    /// ������ ���� ������ �α��� ǥ���ϴ� �ؽ�Ʈ UI
    /// </summary>
    public TMP_Text PopulationText;

    /// <summary>
    /// ���� �� ��¥ �� �ð� �ӵ��� ǥ���ϴ� �ؽ�Ʈ UI
    /// </summary>
    public TMP_Text dateText;

    /// <summary>
    /// �̱��� ������ �����Ͽ� UIManager�� �ߺ� ������ ����
    /// </summary>
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Start�� ù ������ ������Ʈ ���� �� �� ȣ���
    /// </summary>
    void Start()
    {
    }

    /// <summary>
    /// �� �����Ӹ��� ȣ��Ǵ� Update �޼���
    /// ���� UI�� �ֽ� ���·� ������
    /// </summary>
    void Update()
    {
        UpdateCurrencyText();  // ���(ȭ��) UI ������Ʈ
        UpdatePopulation();    // �α� UI ������Ʈ
        UpdateUIDate();        // ��¥ �� �ð� �ӵ� UI ������Ʈ
    }

    /// <summary>
    /// ���ڸ� ���� ����(M, K ����)�� ��ȯ�ϴ� �޼���
    /// ��: 1,500 -> "1.5K", 2,000,000 -> "2.0M"
    /// </summary>
    /// <param name="val">��ȯ�� ����</param>
    /// <returns>���� ���ڿ� ��</returns>
    string ShortenValue(long val)
    {
        string returnText;
        if (val >= 1000000)
        {
            returnText = (val / 1000000) + "." + ((val % 1000000) / 100000) + "M";
        }
        else if (val >= 1000)
        {
            returnText = (val / 1000) + "." + ((val % 1000) / 100) + "K";
        }
        else
        {
            returnText = val.ToString();
        }
        return returnText;
    }

    /// <summary>
    /// ������ ������ ���(ȭ��) ������ UI�� ������Ʈ�ϴ� �޼���
    /// </summary>
    void UpdateCurrencyText()
    {
        long currency = GameManager.Instance.player.GetCurrentBalance();
        string curText = ShortenValue(currency);
        CurrencyText.text = "$: " + curText;
    }

    /// <summary>
    /// ������ ���� ������ �� �α� ������ UI�� ������Ʈ�ϴ� �޼���
    /// </summary>
    void UpdatePopulation()
    {
        long population = GameManager.Instance.player.nation.GetPopulation();
        string popText = ShortenValue(population);
        PopulationText.text = "POP: " + popText;
    }

    /// <summary>
    /// ������ ���� ��¥�� �ð� �ӵ��� UI�� ������Ʈ�ϴ� �޼���
    /// </summary>
    private void UpdateUIDate()
    {
        if (dateText != null)
        {
            dateText.text = GameManager.Instance.GetTimeSpeed() + " " + GameManager.Instance.GetCurrentDate();
        }
    }
}
