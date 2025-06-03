using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LoadSelectUI : MonoBehaviour
{
    public Transform content;
    public GameObject buttonPrefab;
    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
    private static LoadSelectUI _instance;
    public static LoadSelectUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(LoadSelectUI)) as LoadSelectUI;

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
        List<string> names = GlobalVariables.GetAllJsonFileNames();
        foreach(string name in names)
        {
            GameObject newButton = Instantiate(buttonPrefab, content);
            newButton.GetComponent<LoadButtonUI>().SetProvinceData(name);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    

    public void OnBack()
    {
        gameObject.SetActive(false);
    }
}
