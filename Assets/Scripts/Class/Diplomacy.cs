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

    // 외교 관계가 활성화되어 있는지 확인
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
    /// 외교 관계를 활성화하는 메서드
    /// Nation들의 allies 또는 enemies 안에 상대국을 key로, 이 외교관계를 value로 집어넣는다
    /// Diplomacy 시작 시에 수행된다
    /// </summary>
    public void SetNationDiplomacy()
    {
        if (isActive)
        {
            Debug.LogWarning("외교 관계가 이미 활성화되어 있습니다.");
            return;
        }

        foreach (var lnation in leftNations)
        {
            if (lnation == null) continue;  // null 체크

            foreach (var rnation in rightNations)
            {
                if (rnation == null || lnation == rnation) continue;  // null 체크 및 자기 자신 제외

                switch (type)
                {
                    case DiplomacyType.ALLY:
                        lnation.allies.Add(rnation, this);
                        rnation.allies.Add(lnation, this);

                        // 기존 적대 관계가 있다면 제거
                        lnation.enemies.Remove(rnation);
                        rnation.enemies.Remove(lnation);
                        break;

                    case DiplomacyType.ENEMY:
                        lnation.enemies.Add(rnation, this);
                        rnation.enemies.Add(lnation, this);

                        // 기존 동맹 관계가 있다면 제거
                        lnation.allies.Remove(rnation);
                        rnation.allies.Remove(lnation);
                        break;
                }
            }
        }

        isActive = true;
        Debug.Log($"외교 관계 설정 완료: {type}");
    }

    /// <summary>
    /// 외교 관계를 종료하는 메서드
    /// </summary>
    public void EndNationDiplomacy()
    {
        if (!isActive)
        {
            Debug.LogWarning("외교 관계가 이미 비활성화되어 있습니다.");
            return;
        }

        foreach (var lnation in leftNations)
        {
            if (lnation == null) continue;

            foreach (var rnation in rightNations)
            {
                if (rnation == null || lnation == rnation) continue;

                // 설정했던 관계 제거
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
        Debug.Log($"외교 관계 해제 완료: {type}");
    }

    /// <summary>
    /// 특정 국가 두 개가 이 외교 관계에 포함되는지 확인
    /// </summary>
    /// <param name="nation1">확인할 국가 1번</param>
    /// <param name="nation2">확인할 국가 2번</param>
    /// <returns>포함되면 true, 포함되지 않으면 false</returns>
    public bool ContainsNations(Nation nation1, Nation nation2)
    {
        return (leftNations.Contains(nation1) && rightNations.Contains(nation2)) ||
               (leftNations.Contains(nation2) && rightNations.Contains(nation1));
    }

    /// <summary>
    /// 디버그용 외교관계 정보 출력
    /// </summary>
    public void PrintDiplomacyInfo()
    {
        Debug.Log($"외교 관계: {type}, 활성화: {isActive}");
        Debug.Log($"왼쪽 진영: {string.Join(", ", leftNations)}");
        Debug.Log($"오른쪽 진영: {string.Join(", ", rightNations)}");
    }
}