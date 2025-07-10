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
        GlobalVariables.LoadDefaultData();
        // "PlayScene" 로딩
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
        // 에디터에서 Quit이 동작하지 않기 때문에
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
