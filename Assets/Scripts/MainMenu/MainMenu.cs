using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class MainMenu : MonoBehaviour
{
    public void OnNewGameClicked()
    {
        // "PlayScene" 로딩
        SceneManager.LoadScene("PlayScene");
    }

    public void OnLoadClicked()
    {
        string saveFilePath = Path.Combine(Application.persistentDataPath, "savefile.sav");

        if (File.Exists(saveFilePath))
        {
            string saveData = File.ReadAllText(saveFilePath);
            Debug.Log("Save file loaded:\n" + saveData);

            // TODO: 불러온 데이터를 적용하는 로직 추가
        }
        else
        {
            Debug.LogWarning("Save file not found at: " + saveFilePath);
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
