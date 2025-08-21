using TMPro;
using UnityEngine;

public class RegimentButtonUI : MonoBehaviour
{
    public TMP_Text nameText; // ���� �̸� �ؽ�Ʈ
    public TMP_Text popText; // ���� �� �ؽ�Ʈ
    public TMP_Text hometownText;
    private Nation nationData; // ���� ������ �ؽ�Ʈ
    private Regiment regiment;
   
    /// <summary>
    /// Regiment �����͸� �����ϰ� UI�� ������Ʈ�մϴ�.
    /// </summary>
    public void SetRegimentData(Nation nation, Regiment regiment)
    {
        nationData = nation;
        this.regiment = regiment;
        nameText.text = this.regiment.name;
        popText.text = this.regiment.GetUnitCount().ToString();
        hometownText.text = this.regiment.location.name;
    }

    private void Update()
    {
        UpdatePopCount();
    }

    private void UpdatePopCount()
    {
        popText.text = regiment.GetUnitCount().ToString();
    }

    /// <summary>
    /// Regiment ��ư�� Ŭ���� �� ������ ��� (��: �� ���� ǥ��).
    /// </summary>
    public void OnClick()
    {
        //TODO: Building build UI
        RegimentDetailUI.Instance.OpenRegimentDetail(regiment);
    }
}