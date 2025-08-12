/// <summary>
/// ���� Ŭ����
/// ���� ID, ������ ����
/// </summary>
public class User
{
    public int id { get; }
    public Nation nation { get; set; }

    /// <summary>
    /// ���� ������
    /// </summary>
    /// <param name="id">������ ID</param>
    /// <param name="nation">������ ���� ����</param>
    public User(int id, Nation nation)
    {
        this.id = id;
        this.nation = nation;
    }

    /// <summary>
    /// ������ ���� ������ ��� ��ȯ
    /// </summary>
    /// <returns>������ ���� ������ ���</returns>
    public long GetCurrentBalance()
    {
        return this.nation.balance;
    }
}