using System.Collections.Generic;



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
    private BuildingType buildingType;
    private Province province;
    private int workers;

    public Building(BuildingType buildingType, Province province)
    {
        this.buildingType = buildingType;
        this.province = province;
    }

    public int GetWorkers()
    {
        return workers;
    }

    /// <summary>
    /// ������ ����Ϸ��� �õ��Ѵ�.
    /// ����� ������ ��� ����ϰ� workers�� �ø���.
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
    /// ���� �� �ٷ��ڸ� ��� �������� Ȯ���Ѵ�.
    /// </summary>
    /// <returns>��� �����ϸ� O, �Ұ����ϸ� X</returns>
    public bool IsNewWorkerAvailable()
    {
        //TODO: ���κ󽺿� ���/�Ǿ� ���� �� �ٽ� ����
        //TODO: BuildingType�� ��� �ο�, ��� ���� �� ������ �ٽ� ����
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
        //TODO: Province�� Market�� Crops�� Items�� ����ȭ��Ų��
        //TODO: BuildingType�� requiredItems�� ��ȸ�ϸ鼭 Market�� ������ �ִ� �ּڰ��� ã��, �� ���� Market���� �� ���� produceItem���� ���� ������ ���ؼ� Market�� �����Ѵ�
        //��ŭ Produce�ߴ��� ������ return�Ѵ�.
        
        return 0.0;
    }
}

