using System.Collections.Generic;
using UnityEngine;

public enum DiplomacyType
{
    ALLY,
    ENEMY,
}

public class Diplomacy
{
    public HashSet<Nation> leftNations { get; private set; }
    public HashSet<Nation> rightNations { get; private set; }
    public DiplomacyType type { get; private set; }

    // �ܱ� ���谡 Ȱ��ȭ�Ǿ� �ִ��� Ȯ��
    public bool isActive { get; private set; }

    public Diplomacy(HashSet<Nation> leftNations, HashSet<Nation> rightNations, DiplomacyType type)
    {
        this.leftNations = new HashSet<Nation>(leftNations);
        this.rightNations = new HashSet<Nation>(rightNations);
        this.type = type;
        this.isActive = false;

        SetNationDiplomacy();
    }

    /// <summary>
    /// �ܱ� ���踦 Ȱ��ȭ�ϴ� �޼���
    /// Nation���� allies �Ǵ� enemies �ȿ� ��뱹�� key��, �� �ܱ����踦 value�� ����ִ´�
    /// Diplomacy ���� �ÿ� ����ȴ�
    /// </summary>
    public void SetNationDiplomacy()
    {
        if (isActive)
        {
            Debug.LogWarning("�ܱ� ���谡 �̹� Ȱ��ȭ�Ǿ� �ֽ��ϴ�.");
            return;
        }

        foreach (var lnation in leftNations)
        {
            if (lnation == null) continue;  // null üũ

            foreach (var rnation in rightNations)
            {
                if (rnation == null || lnation == rnation) continue;  // null üũ �� �ڱ� �ڽ� ����

                switch (type)
                {
                    case DiplomacyType.ALLY:
                        lnation.allies.Add(rnation, this);
                        rnation.allies.Add(lnation, this);

                        // ���� ���� ���谡 �ִٸ� ����
                        lnation.enemies.Remove(rnation);
                        rnation.enemies.Remove(lnation);
                        break;

                    case DiplomacyType.ENEMY:
                        lnation.enemies.Add(rnation, this);
                        rnation.enemies.Add(lnation, this);

                        // ���� ���� ���谡 �ִٸ� ����
                        lnation.allies.Remove(rnation);
                        rnation.allies.Remove(lnation);
                        break;
                }
            }
        }

        isActive = true;
        Debug.Log($"�ܱ� ���� ���� �Ϸ�: {type}");
    }

    /// <summary>
    /// �ܱ� ���踦 �����ϴ� �޼���
    /// </summary>
    public void EndNationDiplomacy()
    {
        if (!isActive)
        {
            Debug.LogWarning("�ܱ� ���谡 �̹� ��Ȱ��ȭ�Ǿ� �ֽ��ϴ�.");
            return;
        }

        foreach (var lnation in leftNations)
        {
            if (lnation == null) continue;

            foreach (var rnation in rightNations)
            {
                if (rnation == null || lnation == rnation) continue;

                // �����ߴ� ���� ����
                switch (type)
                {
                    case DiplomacyType.ALLY:
                        lnation.allies.Remove(rnation);
                        rnation.allies.Remove(lnation);
                        break;

                    case DiplomacyType.ENEMY:
                        lnation.enemies.Remove(rnation);
                        rnation.enemies.Remove(lnation);
                        break;
                }
            }
        }

        isActive = false;
        Debug.Log($"�ܱ� ���� ���� �Ϸ�: {type}");
    }

    /// <summary>
    /// Ư�� ���� �� ���� �� �ܱ� ���迡 ���ԵǴ��� Ȯ��
    /// </summary>
    /// <param name="nation1">Ȯ���� ���� 1��</param>
    /// <param name="nation2">Ȯ���� ���� 2��</param>
    /// <returns>���ԵǸ� true, ���Ե��� ������ false</returns>
    public bool ContainsNations(Nation nation1, Nation nation2)
    {
        return (leftNations.Contains(nation1) && rightNations.Contains(nation2)) ||
               (leftNations.Contains(nation2) && rightNations.Contains(nation1));
    }

    /// <summary>
    /// ����׿� �ܱ����� ���� ���
    /// </summary>
    public void PrintDiplomacyInfo()
    {
        Debug.Log($"�ܱ� ����: {type}, Ȱ��ȭ: {isActive}");
        Debug.Log($"���� ����: {string.Join(", ", leftNations)}");
        Debug.Log($"������ ����: {string.Join(", ", rightNations)}");
    }
}