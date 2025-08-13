using System.Collections.Generic;
using Unity.VisualScripting;



/// <summary>
/// 건물의 종류 클래스
/// </summary>
public class BuildingType
{
    public string name;
    public Dictionary<string, int> requireItems;
    public Dictionary<string, int> produceItems;
    public Dictionary<string, int> workerNeeded;

    public BuildingType(string name)
    {
        this.name = name;
    }
}

/// <summary>
/// 실제 프로빈스 위에 얹혀지는 빌딩 클래스
/// </summary>
public class Building
{
    public BuildingType buildingType;
    public Province province;
    public double workerScale; // 일꾼의 비율 (1.0 -> BuildingType의 workerNeeded의 1배율)
    public int level = 0;

    public Building(BuildingType buildingType, Province province)
    {
        this.buildingType = buildingType;
        this.province = province;
    }

    /// <summary>
    /// jobType에 해당하는 직업을 가진 근무자가 몇명 있는지 반환
    /// </summary>
    /// <param name="jobType">직업명 (JobTypes)</param>
    /// <returns>근무자 수</returns>
    public double GetWorkers(string jobType)
    {
        int workerNeededForType;
        if (buildingType.workerNeeded.TryGetValue(jobType, out workerNeededForType))
        {
            return workerScale * workerNeededForType;
        }
        else return -1;
    }

    /// <summary>
    /// 직원을 고용하려고 시도한다.
    /// 고용이 가능할 경우 고용하고 workers를 늘린다.
    /// </summary>
    /// <returns>가능하면 true, 불가능하면 false</returns>
    public bool HireWorkers()
    {
        if (IsNewWorkerAvailable())
        {
            //TODO: 로직 추가
            return true;
        }
        else return false;
    }

    /// <summary>
    /// 현재 새 근로자를 고용 가능한지 확인한다.
    /// </summary>
    /// <returns>고용 가능하면 O, 불가능하면 X</returns>
    public bool IsNewWorkerAvailable()
    {
        //TODO: 프로빈스에 고용/실업 나눈 뒤 다시 결정
        //TODO: BuildingType에 고용 인원, 고용 종족 등 나눈뒤 다시 결정
        return false;
    }

    /// <summary>
    /// Item을 생산할 수 있는 최소 배율만큼 생산하고 그 배율을 반환한다.
    /// 
    ///  * (예시)
    /// Require => Rice: 100, Tomato: 200, Onion: 50
    /// Produce => ProducedFood: 10
    /// Have => Rice: 200, Tomato: 600, Onion: 25
    ///
    /// 총 생산: 0.5배율(Onion의 Have: 25 / Require: 50)
    /// Market => Rice: 150, Tomato: 500, Onion: 0, ProducedFood: 5
    /// 
    /// </summary>
    /// <returns>생산한 배율</returns>
    public double ProduceItem()
    {
        return 0.0;
    }
}

