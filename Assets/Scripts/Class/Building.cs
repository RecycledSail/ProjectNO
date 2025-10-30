using System.Collections.Generic;
using Unity.Mathematics;



/// <summary>
/// 건물의 종류 클래스
/// </summary>
public class BuildingType
{
    public string name;
    public Dictionary<string, int> requireItems;
    public Dictionary<string, int> produceItems;
    public long workerNeeded;

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
    public long currentWorkers; // 일꾼의 비율 (1.0 -> BuildingType의 workerNeeded의 1배율)
    public int level = 0;
    public int balance = 0;
    public int previousGain = 0;

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
    public long GetWorkers()
    {
        return currentWorkers;
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
            // 일단 한번에 50명씩 고용
            long hirePeople = math.min(level * buildingType.workerNeeded - currentWorkers, 50);
            currentWorkers += hirePeople;
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
        if (balance <= 0 || previousGain <= 0 || ((double)currentWorkers / buildingType.workerNeeded) >= level || province.population <= province.hiredPopulation) return false;
        else return true;
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
        //하루에 얼만큼 수확할건지?
        double produceScale = 1;
        if (currentWorkers <= 0) return 0.0;
        else
        {
            double minCropsScale = (double)currentWorkers / (double)buildingType.workerNeeded;
            return minCropsScale * produceScale;
        }
    }
}







public class BuildingRecipe
{
    public string name;
    public Dictionary<string, int> requireItems = new();

    public BuildingRecipe(string name)
    {
        this.name = name;
    }
}

