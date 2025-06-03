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
    // �̱��� �ν��Ͻ� (�ٸ� ��ũ��Ʈ���� ���� ���� ����)
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
