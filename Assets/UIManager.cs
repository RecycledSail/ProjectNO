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

    public TMP_Text CurrencyText;
    public TMP_Text PopulationText;

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
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (!GameManager.Instance.paused)
        {
            UpdateCurrencyText();
            UpdatePopulation();
        }
        
    }
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
    void UpdateCurrencyText()
    {
        long currency = GameManager.Instance.player.GetCurrentBalance();
        string curText = ShortenValue(currency);
        CurrencyText.text = "$: " + curText;
    }
    void UpdatePopulation()
    {
        long population = GameManager.Instance.player.nation.GetPopulation();
        string popText = ShortenValue(population);
        PopulationText.text = "POP: " + popText;
    }
}
