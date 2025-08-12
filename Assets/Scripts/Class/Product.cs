/// <summary>
/// 프로빈스 내 작물의 갯수 클래스
/// 작물 ID, 작물 이름을 정의
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
    /// 작물 생산 메서드
    /// </summary>
    /// <param name="quantity"></param>
    public void Produce(int quantity)
    {
        this.amount += quantity;
    }
}