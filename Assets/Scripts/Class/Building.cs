using System.Collections.Generic;
using Unity.VisualScripting;



/// <summary>
/// �ǹ��� ���� Ŭ����
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
/// ���� ���κ� ���� �������� ���� Ŭ����
/// </summary>
public class Building
{
    public BuildingType buildingType;
    public Province province;
    public double workerScale; // �ϲ��� ���� (1.0 -> BuildingType�� workerNeeded�� 1����)
    public int level = 0;

    public Building(BuildingType buildingType, Province province)
    {
        this.buildingType = buildingType;
        this.province = province;
    }

    /// <summary>
    /// jobType�� �ش��ϴ� ������ ���� �ٹ��ڰ� ��� �ִ��� ��ȯ
    /// </summary>
    /// <param name="jobType">������ (JobTypes)</param>
    /// <returns>�ٹ��� ��</returns>
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
    /// ������ �����Ϸ��� �õ��Ѵ�.
    /// ������ ������ ��� �����ϰ� workers�� �ø���.
    /// </summary>
    /// <returns>�����ϸ� true, �Ұ����ϸ� false</returns>
    public bool HireWorkers()
    {
        if (IsNewWorkerAvailable())
        {
            //TODO: ���� �߰�
            return true;
        }
        else return false;
    }

    /// <summary>
    /// ���� �� �ٷ��ڸ� ���� �������� Ȯ���Ѵ�.
    /// </summary>
    /// <returns>���� �����ϸ� O, �Ұ����ϸ� X</returns>
    public bool IsNewWorkerAvailable()
    {
        //TODO: ���κ󽺿� ����/�Ǿ� ���� �� �ٽ� ����
        //TODO: BuildingType�� ���� �ο�, ���� ���� �� ������ �ٽ� ����
        return false;
    }

    /// <summary>
    /// Item�� ������ �� �ִ� �ּ� ������ŭ �����ϰ� �� ������ ��ȯ�Ѵ�.
    /// 
    ///  * (����)
    /// Require => Rice: 100, Tomato: 200, Onion: 50
    /// Produce => ProducedFood: 10
    /// Have => Rice: 200, Tomato: 600, Onion: 25
    ///
    /// �� ����: 0.5����(Onion�� Have: 25 / Require: 50)
    /// Market => Rice: 150, Tomato: 500, Onion: 0, ProducedFood: 5
    /// 
    /// </summary>
    /// <returns>������ ����</returns>
    public double ProduceItem()
    {
        //�Ϸ翡 ��ŭ ��Ȯ�Ұ���?
        double produceScale = 1;
        //TODO: Province�� Market�� products�� Items�� ����ȭ��Ų��
        //TODO: BuildingType�� requiredItems�� ��ȸ�ϸ鼭 Market�� ������ �ִ� �ּڰ��� ã��, �� ���� Market���� �� ���� produceItem���� ���� ������ ���ؼ� Market�� �����Ѵ�
        //��ŭ Produce�ߴ��� ������ return�Ѵ�.
        if (workerScale <= 0.0) return 0.0;
        else
        {
            double minCropsScale = workerScale;
            // 1��: minCropsScale�� ã�´�
            // foreach(string cropName in buildingType.requireItems.Keys)
            // {
            //     int cropNeed = buildingType.requireItems[cropName];
            //     Crop crop = province.market.GetCrop(cropName);
            //     if(crop.amount <= cropNeed * minCropsScale * produceScale)
            //     {
            //         minCropsScale = crop.amount / cropNeed;
            //     }
            // }

            // // 2��: ã�� minCropsScale��ŭ, �׸��� produceScale��ŭ �Ϸ縶�� market.crops���� �����Ѵ�
            // foreach (string cropName in buildingType.requireItems.Keys)
            // {
            //     int cropNeed = buildingType.requireItems[cropName];
            //     Crop crop = province.market.GetCrop(cropName);
            //     crop.amount -= (int)(cropNeed * minCropsScale * produceScale);
            // }

            // ��ȯ
            return minCropsScale * produceScale;
        }
    }
}

