using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public GameObject loadPanel;
    [SerializeField] private SettingsPanelUI settingsPanel;
    [SerializeField] private GameObject nationSelectPanel;

    private void Awake()
    {
        if (settingsPanel != null) settingsPanel.ApplySavedSettings();
    }

    public void OnNewGameClicked()
    {
        if (GlobalVariables.NATIONS.Count == 0)
            GlobalVariables.LoadData();

        if (nationSelectPanel != null)
            nationSelectPanel.SetActive(true);
        else
            Debug.LogError("NationSelectPanel reference is missing on MainMenu.");
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
