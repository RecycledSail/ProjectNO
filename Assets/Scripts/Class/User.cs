/// <summary>
/// 유저 클래스
/// 유저 ID, 국가를 정의
/// </summary>
public class User
{
    public int id { get; }
    public Nation nation { get; set; }

    /// <summary>
    /// 유저 생성자
    /// </summary>
    /// <param name="id">유저의 ID</param>
    /// <param name="nation">유저가 속한 국가</param>
    public User(int id, Nation nation)
    {
        this.id = id;
        this.nation = nation;
    }

    /// <summary>
    /// 유저가 속한 국가의 재산 반환
    /// </summary>
    /// <returns>유저가 속한 국가의 재산</returns>
    public long GetCurrentBalance()
    {
        return this.nation.balance;
    }
}