using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NationSelectUI : MonoBehaviour
{
    [SerializeField] private Transform content;
    [SerializeField] private NationSelectButtonUI buttonPrefab;
    [SerializeField] private TMP_Text emptyMessage;

    private void OnEnable()
    {
        RefreshNationButtons();
    }

    private void RefreshNationButtons()
    {
        if (content == null || buttonPrefab == null)
        {
            Debug.LogError("NationSelectUI requires Content and Button Prefab references.", this);
            return;
        }

        for (int index = content.childCount - 1; index >= 0; index--)
        {
            GameObject oldButton = content.GetChild(index).gameObject;
            oldButton.SetActive(false);
            Destroy(oldButton);
        }

        if (GlobalVariables.NATIONS.Count == 0)
            GlobalVariables.LoadData();

        foreach (Nation nation in GlobalVariables.NATIONS.Values.OrderBy(nation => nation.id))
        {
            NationSelectButtonUI newButton = Instantiate(buttonPrefab, content);
            newButton.SetNationData(nation, this);
        }

        if (emptyMessage != null)
            emptyMessage.gameObject.SetActive(GlobalVariables.NATIONS.Count == 0);
    }

    public void SelectNation(string nationCode)
    {
        if (!GlobalVariables.NATIONS.ContainsKey(nationCode))
        {
            Debug.LogError($"Cannot start a new game: nation '{nationCode}' does not exist.");
            return;
        }

        GlobalVariables.saveFileName = null;
        GlobalVariables.newGameNationCode = nationCode;
        SceneManager.LoadScene("PlayScene");
    }

    public void OnBack()
    {
        gameObject.SetActive(false);
    }
}
