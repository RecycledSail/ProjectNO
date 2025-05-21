using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class MainMenu : MonoBehaviour
{
    public void OnNewGameClicked()
    {
        GlobalVariables.LoadDefaultData();
        // "PlayScene" �ε�
        SceneManager.LoadScene("PlayScene");
    }

    public void OnLoadClicked()
    {

        if (!File.Exists(GlobalVariables.saveFileName))
        {
            SaveManager.OnLoad();
            //Debug.Log("Save file loaded:\n" + GlobalVariables.saveFileName);

            // TODO: �ҷ��� �����͸� �����ϴ� ���� �߰�
            SceneManager.LoadScene("PlayScene");
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
