using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // 이름 텍스트
   
    /// <summary>
    /// Save name을 설정
    /// </summary>
    public void SetProvinceData(string name)
    {
        nameText.text = name;
    }

    /// <summary>
    /// 버튼이 클릭되면 saveFileName을 바꾸고 씬을 로드
    /// </summary>
    public void OnClick()
    {
        GlobalVariables.saveFileName = nameText.text;
        //SaveManager.OnLoad();
        SceneManager.LoadScene("PlayScene");
    }
}