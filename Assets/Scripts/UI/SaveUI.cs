using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveUI : MonoBehaviour
{
    public Transform content;
    public GameObject buttonPrefab;
    public Button exitButton;

    public TMP_InputField input;
    // 싱글톤 인스턴스 (다른 스크립트에서 쉽게 접근 가능)
    private static SaveUI _instance;
    public static SaveUI Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType(typeof(SaveUI)) as SaveUI;

            return _instance;
        }
    }

    private void Start()
    {
        gameObject.SetActive(false);
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
    private void OnEnable()
    {
        List<string> names = GlobalVariables.GetAllJsonFileNames();
        foreach (string name in names)
        {
            GameObject newButton = Instantiate(buttonPrefab, content);
            newButton.GetComponent<SaveButtonUI>().SetName(name);
        }
    }

    private void OnDisable()
    {
        for (int i = content.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(content.transform.GetChild(i).gameObject);
        }
    }

    public void OnExit()
    {
        gameObject.SetActive(false);
    }

    public void OnSaveButtonClick()
    {
        GlobalVariables.saveFileName = input.text;
        SaveManager.OnSave();
        SceneManager.LoadScene("MainMenuScene");
    }
}
