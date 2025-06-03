using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // �̸� �ؽ�Ʈ

    /// <summary>
    /// Save name�� ����
    /// </summary>
    public void SetName(string name)
    {
        nameText.text = name;
    }

    /// <summary>
    /// ��ư�� Ŭ���Ǹ� saveFileName�� �ٲٰ� ���� �ε�
    /// </summary>
    public void OnClick()
    {
        GlobalVariables.saveFileName = nameText.text;
        SaveManager.OnSave();
        SceneManager.LoadScene("MainMenuScene");
    }
}