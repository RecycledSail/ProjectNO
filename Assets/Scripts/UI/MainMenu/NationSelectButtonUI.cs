using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NationSelectButtonUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image nationColor;

    private string nationCode;
    private NationSelectUI owner;

    private void Awake()
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClick);
        else
            Debug.LogError("NationSelectButtonUI requires a Button component.", this);
    }

    public void SetNationData(Nation nation, NationSelectUI selectUI)
    {
        nationCode = nation.name;
        owner = selectUI;
        nameText.text = nation.name;

        if (nationColor != null)
            nationColor.color = nation.color;
    }

    private void OnClick()
    {
        owner?.SelectNation(nationCode);
    }
}
