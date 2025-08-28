using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class MainMenu : MonoBehaviour
{
    public GameObject loadPanel;
    public void OnNewGameClicked()
    {
        GlobalVariables.saveFileName = null;
        GlobalVariables.LoadData();
        // "PlayScene" �ε�
        SceneManager.LoadScene("PlayScene");
    }

    public void OnLoadClicked()
    {

        if (!File.Exists(GlobalVariables.saveFileName))
        {
            loadPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Save file not found at: " + GlobalVariables.saveFileName);
        }
    }

    public void OnExitClicked()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();

#if UNITY_EDITOR
        // �����Ϳ��� Quit�� �������� �ʱ� ������
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
