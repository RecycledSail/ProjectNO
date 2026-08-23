using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public enum MoneyTransactionKind
{
    Transfer,
    Mint,
    Burn,
    Migration
}

public sealed class MoneyTransferEntry
{
    public MoneyAccount Account { get; }
    public long Delta { get; }

    public MoneyTransferEntry(MoneyAccount account, long delta)
    {
        Account = account ?? throw new ArgumentNullException(nameof(account));
        Delta = delta;
    }
}

public sealed class MoneyTransactionRecord
{
    public MoneyTransactionKind Kind { get; }
    public IReadOnlyList<string> SourceIds { get; }
    public IReadOnlyList<string> DestinationIds { get; }
    public long Amount { get; }
    public string Reason { get; }

    internal MoneyTransactionRecord(
        MoneyTransactionKind kind,
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> destinationIds,
        long amount,
        string reason)
    {
        Kind = kind;
        SourceIds = new ReadOnlyCollection<string>(new List<string>(sourceIds));
        DestinationIds = new ReadOnlyCollection<string>(new List<string>(destinationIds));
        Amount = amount;
        Reason = reason;
    }
}

public sealed class MoneyLedger
{
    private readonly object _issuanceAuthority;
    private readonly MoneyAccount _treasuryAccount;
    private readonly HashSet<MoneyAccount> _registeredAccounts = new();
    private readonly List<MoneyTransactionRecord> _transactions = new();
    private readonly IReadOnlyList<MoneyTransactionRecord> _readOnlyTransactions;
    private bool _initializationSealed;

    public string CurrencyId { get; }
    public long MoneySupply { get; private set; }
    public IReadOnlyList<MoneyTransactionRecord> Transactions => _readOnlyTransactions;

    public MoneyLedger(string currencyId, object issuanceAuthority, MoneyAccount treasuryAccount)
    {
        if (string.IsNullOrWhiteSpace(currencyId))
            throw new ArgumentException(nameof(currencyId));

        CurrencyId = currencyId;
        _issuanceAuthority = issuanceAuthority;
        _treasuryAccount = treasuryAccount ?? throw new ArgumentNullException(nameof(treasuryAccount));
        _readOnlyTransactions = _transactions.AsReadOnly();
    }

    public bool RegisterInitialAccount(MoneyAccount account)
    {
        if (_initializationSealed || !CanRegister(account))
            return false;

        try
        {
            long newSupply = checked(MoneySupply + account.Balance);
            Register(account);
            MoneySupply = newSupply;
            AppendRecord(MoneyTransactionKind.Migration, Array.Empty<string>(),
                new[] { account.Id }, account.Balance, "Initial account registration");
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public bool RegisterEmptyAccount(MoneyAccount account)
    {
        if (!_initializationSealed || !CanRegister(account) || account.Balance != 0)
            return false;

        Register(account);
        AppendRecord(MoneyTransactionKind.Migration, Array.Empty<string>(),
            new[] { account.Id }, 0, "Empty account registration");
        return true;
    }

    public bool UnregisterEmptyAccount(MoneyAccount account)
    {
        if (account == null || account.Balance != 0 || !_registeredAccounts.Remove(account))
            return false;

        account.Ledger = null;
        AppendRecord(MoneyTransactionKind.Migration, new[] { account.Id },
            Array.Empty<string>(), 0, "Empty account unregistration");
        return true;
    }

    public void SealInitialization()
    {
        _initializationSealed = true;
    }

    public bool TryTransfer(MoneyAccount from, MoneyAccount to, long amount, string reason)
    {
        if (from == null || to == null || ReferenceEquals(from, to) || amount <= 0)
            return false;

        return TryTransferBatch(new[]
        {
            new MoneyTransferEntry(from, -amount),
            new MoneyTransferEntry(to, amount)
        }, reason);
    }

    public bool TryTransferBatch(IReadOnlyList<MoneyTransferEntry> entries, string reason)
    {
        if (!TryValidateBatch(entries, out Dictionary<MoneyAccount, long> deltas))
            return false;

        if (!TryCreateTransferRecord(entries, reason, out MoneyTransactionRecord record))
            return false;

        foreach (KeyValuePair<MoneyAccount, long> pair in deltas)
            pair.Key.ApplyDelta(pair.Value);

        _transactions.Add(record);
        return true;
    }

    public bool TryMint(object authority, MoneyAccount target, long amount, string reason)
    {
        if (!HasIssuanceAuthority(authority) || !IsRegistered(target) || amount <= 0)
            return false;

        try
        {
            _ = checked(target.Balance + amount);
            long newSupply = checked(MoneySupply + amount);
            target.ApplyDelta(amount);
            MoneySupply = newSupply;
            AppendRecord(MoneyTransactionKind.Mint, Array.Empty<string>(),
                new[] { target.Id }, amount, reason);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public bool TryBurn(object authority, MoneyAccount source, long amount, string reason)
    {
        if (!HasIssuanceAuthority(authority) || !IsRegistered(source) || amount <= 0 ||
            source.Balance < amount || MoneySupply < amount)
            return false;

        source.ApplyDelta(-amount);
        MoneySupply -= amount;
        AppendRecord(MoneyTransactionKind.Burn, new[] { source.Id },
            Array.Empty<string>(), amount, reason);
        return true;
    }

    public bool Audit(out long registeredBalance)
    {
        try
        {
            long total = 0;
            foreach (MoneyAccount account in _registeredAccounts)
                total = checked(total + account.Balance);

            registeredBalance = total;
            return total == MoneySupply;
        }
        catch (OverflowException)
        {
            registeredBalance = 0;
            return false;
        }
    }

    private bool TryValidateBatch(
        IReadOnlyList<MoneyTransferEntry> entries,
        out Dictionary<MoneyAccount, long> deltas)
    {
        deltas = null;
        if (entries == null || entries.Count == 0)
            return false;

        try
        {
            long batchTotal = 0;
            Dictionary<MoneyAccount, long> candidateDeltas = new();
            foreach (MoneyTransferEntry entry in entries)
            {
                if (entry == null || !IsRegistered(entry.Account))
                    return false;

                batchTotal = checked(batchTotal + entry.Delta);
                candidateDeltas.TryGetValue(entry.Account, out long existingDelta);
                candidateDeltas[entry.Account] = checked(existingDelta + entry.Delta);
            }

            if (batchTotal != 0)
                return false;

            foreach (KeyValuePair<MoneyAccount, long> pair in candidateDeltas)
            {
                if (checked(pair.Key.Balance + pair.Value) < 0)
                    return false;
            }

            deltas = candidateDeltas;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private bool CanRegister(MoneyAccount account) =>
        account != null && account.Ledger == null && !_registeredAccounts.Contains(account);

    private bool IsRegistered(MoneyAccount account) =>
        account != null && account.Ledger == this && _registeredAccounts.Contains(account);

    private bool HasIssuanceAuthority(object authority) =>
        _issuanceAuthority != null && ReferenceEquals(_issuanceAuthority, authority);

    private void Register(MoneyAccount account)
    {
        _registeredAccounts.Add(account);
        account.Ledger = this;
    }

    private bool TryCreateTransferRecord(
        IReadOnlyList<MoneyTransferEntry> entries,
        string reason,
        out MoneyTransactionRecord record)
    {
        try
        {
            List<string> sources = new();
            List<string> destinations = new();
            long amount = 0;
            foreach (MoneyTransferEntry entry in entries)
            {
                if (entry.Delta < 0)
                {
                    sources.Add(entry.Account.Id);
                    amount = checked(amount - entry.Delta);
                }
                else if (entry.Delta > 0)
                {
                    destinations.Add(entry.Account.Id);
                }
            }

            record = new MoneyTransactionRecord(
                MoneyTransactionKind.Transfer, sources, destinations, amount, reason);
            return true;
        }
        catch (OverflowException)
        {
            record = null;
            return false;
        }
    }

    private void AppendRecord(
        MoneyTransactionKind kind,
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> destinationIds,
        long amount,
        string reason)
    {
        _transactions.Add(new MoneyTransactionRecord(
            kind, sourceIds, destinationIds, amount, reason));
    }
}
