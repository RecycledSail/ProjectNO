using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenIndustrySubsidyButton : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OpenPanel);
    }

    private void OpenPanel()
    {
        Nation nation = FinanceUI.Instance?.CurrentNation;
        if (nation != null)
            IndustrySubsidyPanel.Instance.Open(nation);
        else
            Debug.LogWarning("[IndustrySubsidy] FinanceUI에 nation이 설정되지 않았습니다.");
    }
}
