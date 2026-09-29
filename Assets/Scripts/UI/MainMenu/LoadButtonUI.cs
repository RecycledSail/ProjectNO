using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadButtonUI : MonoBehaviour
{
    public TMP_Text nameText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button deleteButton;

    private string saveName;
    private LoadSelectUI owner;

    private void Awake()
    {
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClick);
    }

    public void SetSaveData(string name, LoadSelectUI loadSelectUI)
    {
        saveName = name;
        owner = loadSelectUI;
        nameText.text = name;
        UpdateSummary();
    }

    public void SetProvinceData(string name) => SetSaveData(name, null);

    public void OnClick()
    {
        if (!SaveManager.CanLoad(saveName))
            return;

        GlobalVariables.saveFileName = saveName;
        SceneManager.LoadScene("PlayScene");
    }

    private void OnDeleteClick()
    {
        string targetSaveName = saveName;
        SaveDeleteConfirmationUI.Show(this, targetSaveName, () =>
        {
            if (SaveManager.TryDelete(targetSaveName))
                owner?.RefreshSaveList();
        });
    }

    private void UpdateSummary()
    {
        if (summaryText == null)
            return;

        summaryText.text = SaveManager.TryGetSummary(saveName, out SaveManager.SaveSummary summary)
            ? $"{summary.NationName}  ·  GDP {UIManager.ShortenValue(summary.GDP)}  ·  {summary.Year}"
            : "Unavailable save";
    }
}
