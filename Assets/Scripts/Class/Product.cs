/// <summary>
/// ���κ� �� �۹��� ���� Ŭ����
/// �۹� ID, �۹� �̸��� ����
/// </summary>
public class Product
{
    public string name { get; }
    public int amount { get; set; }

    public Product(string name, int initialAmount)
    {
        this.name = name;
        this.amount = initialAmount;
    }

    /// <summary>
    /// �۹� ���� �޼���
    /// </summary>
    /// <param name="quantity"></param>
    public void Produce(int quantity)
    {
        this.amount += quantity;
    }
}