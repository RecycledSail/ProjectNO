using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject loadPanel;
    [SerializeField] private SettingsPanelUI settingsPanel;

    private void Awake()
    {
        if (settingsPanel != null) settingsPanel.ApplySavedSettings();
    }

    public void OnNewGameClicked()
    {
        GlobalVariables.saveFileName = null;
        GlobalVariables.LoadData();
        SceneManager.LoadScene("PlayScene");
    }

    public void OnLoadClicked()
    {
        if (!File.Exists(GlobalVariables.saveFileName)) loadPanel.SetActive(true);
        else Debug.LogWarning("Save file not found at: " + GlobalVariables.saveFileName);
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.Open();
        else Debug.LogError("SettingsPanelUI reference is missing on MainMenu.");
    }

    public void OnExitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
