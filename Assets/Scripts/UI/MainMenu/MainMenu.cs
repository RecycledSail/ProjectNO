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
        SceneManager.LoadScene("PlayScene");
    }

    public void OnLoadClicked()
    {
        loadPanel.SetActive(true);
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
