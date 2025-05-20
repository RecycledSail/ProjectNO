using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class MainMenu : MonoBehaviour
{
    public void OnNewGameClicked()
    {
        // "PlayScene" �ε�
        SceneManager.LoadScene("PlayScene");
    }

    public void OnLoadClicked()
    {
        string saveFilePath = Path.Combine(Application.persistentDataPath, "savefile.sav");

        if (File.Exists(saveFilePath))
        {
            string saveData = File.ReadAllText(saveFilePath);
            Debug.Log("Save file loaded:\n" + saveData);

            // TODO: �ҷ��� �����͸� �����ϴ� ���� �߰�
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
        // �����Ϳ��� Quit�� �������� �ʱ� ������
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
