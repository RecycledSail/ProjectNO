using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LoadSelectUI : MonoBehaviour
{
    public Transform content;
    public GameObject buttonPrefab;
    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
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
