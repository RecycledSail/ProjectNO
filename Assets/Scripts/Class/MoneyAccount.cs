using System;

public sealed class MoneyAccount
{
    public string Id { get; }
    public long Balance { get; private set; }
    public MoneyLedger Ledger { get; internal set; }

    public MoneyAccount(string id, long openingBalance = 0)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
        if (openingBalance < 0) throw new ArgumentOutOfRangeException(nameof(openingBalance));
        Id = id;
        Balance = openingBalance;
    }

    internal void ApplyDelta(long delta) => Balance = checked(Balance + delta);

    internal void ReplaceForLoading(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Balance = value;
    }
}
