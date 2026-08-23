using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

public class MoneyLedgerTests
{
    [Test]
    public void TransferBatch_IsAtomicAndPreservesSupply()
    {
        object authority = new object();
        object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 1000L);
        object seller = ReflectionTestHelpers.New("MoneyAccount", "seller", 0L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, treasury);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", seller);
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryTransfer",
            treasury, seller, 250L, "test"), Is.True);
        Assert.That(ReflectionTestHelpers.Get(treasury, "Balance"), Is.EqualTo(750L));
        Assert.That(ReflectionTestHelpers.Get(seller, "Balance"), Is.EqualTo(250L));
        Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(1000L));

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryTransfer",
            treasury, seller, 751L, "reject"), Is.False);
        Assert.That(ReflectionTestHelpers.Get(treasury, "Balance"), Is.EqualTo(750L));
        Assert.That(ReflectionTestHelpers.Get(seller, "Balance"), Is.EqualTo(250L));

        long registeredBalance;
        object[] auditArguments = { null };
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "Audit", auditArguments), Is.True);
        registeredBalance = (long)auditArguments[0];
        Assert.That(registeredBalance, Is.EqualTo(1000L));
        Assert.That(((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count,
            Is.EqualTo(3));
    }

    [Test]
    public void MintAndBurn_RequireAuthorityAndChangeSupplyExactly()
    {
        object authority = new object();
        object stranger = new object();
        object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 100L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, treasury);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury);
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryMint",
            stranger, treasury, 50L, "unauthorized"), Is.False);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryMint",
            authority, treasury, 50L, "authorized"), Is.True);
        Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(150L));
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryBurn",
            authority, treasury, 20L, "burn"), Is.True);
        Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(130L));
        Assert.That(((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count,
            Is.EqualTo(3));
    }

    [Test]
    public void NeutralLedger_RejectsMintAndBurn()
    {
        object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 100L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", null, treasury);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury);
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryMint",
            new object(), treasury, 10L, "mint"), Is.False);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryBurn",
            new object(), treasury, 10L, "burn"), Is.False);
        Assert.That(ReflectionTestHelpers.Get(ledger, "MoneySupply"), Is.EqualTo(100L));
    }

    [Test]
    public void InvalidBatchSum_ChangesNoAccounts()
    {
        object authority = new object();
        object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 100L);
        object seller = ReflectionTestHelpers.New("MoneyAccount", "seller", 0L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, treasury);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", seller);
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

        Type entryType = ReflectionTestHelpers.Find("MoneyTransferEntry");
        IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
        entries.Add(ReflectionTestHelpers.New("MoneyTransferEntry", treasury, -20L));
        entries.Add(ReflectionTestHelpers.New("MoneyTransferEntry", seller, 10L));

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "TryTransferBatch",
            entries, "unbalanced"), Is.False);
        Assert.That(ReflectionTestHelpers.Get(treasury, "Balance"), Is.EqualTo(100L));
        Assert.That(ReflectionTestHelpers.Get(seller, "Balance"), Is.EqualTo(0L));
    }

    [Test]
    public void TransferBatch_RejectsUnrecordableAmountWithoutMutation()
    {
        object authority = new object();
        object source = ReflectionTestHelpers.New("MoneyAccount", "source", long.MaxValue);
        object intermediary = ReflectionTestHelpers.New("MoneyAccount", "intermediary", 0L);
        object destination = ReflectionTestHelpers.New("MoneyAccount", "destination", 0L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, source);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", source);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", intermediary);
        ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", destination);
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");

        Type entryType = ReflectionTestHelpers.Find("MoneyTransferEntry");
        IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
        entries.Add(ReflectionTestHelpers.New("MoneyTransferEntry", source, -long.MaxValue));
        entries.Add(ReflectionTestHelpers.New("MoneyTransferEntry", intermediary, long.MaxValue));
        entries.Add(ReflectionTestHelpers.New("MoneyTransferEntry", intermediary, -long.MaxValue));
        entries.Add(ReflectionTestHelpers.New("MoneyTransferEntry", destination, long.MaxValue));
        int transactionCount = ((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count;

        bool succeeded = true;
        Exception exception = null;
        try
        {
            succeeded = ReflectionTestHelpers.Call<bool>(ledger,
                "TryTransferBatch", entries, "unrecordable");
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Null);
        Assert.That(succeeded, Is.False);
        Assert.That(ReflectionTestHelpers.Get(source, "Balance"), Is.EqualTo(long.MaxValue));
        Assert.That(ReflectionTestHelpers.Get(intermediary, "Balance"), Is.EqualTo(0L));
        Assert.That(ReflectionTestHelpers.Get(destination, "Balance"), Is.EqualTo(0L));
        Assert.That(((ICollection)ReflectionTestHelpers.Get(ledger, "Transactions")).Count,
            Is.EqualTo(transactionCount));
    }

    [Test]
    public void Registration_OnlyAllowsEmptyAccountsAfterInitialization()
    {
        object authority = new object();
        object treasury = ReflectionTestHelpers.New("MoneyAccount", "treasury", 100L);
        object empty = ReflectionTestHelpers.New("MoneyAccount", "empty", 0L);
        object funded = ReflectionTestHelpers.New("MoneyAccount", "funded", 1L);
        object ledger = ReflectionTestHelpers.New("MoneyLedger", "N1", authority, treasury);

        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterInitialAccount", treasury), Is.True);
        ReflectionTestHelpers.Call<object>(ledger, "SealInitialization");
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterEmptyAccount", empty), Is.True);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "RegisterEmptyAccount", funded), Is.False);
        Assert.That(ReflectionTestHelpers.Call<bool>(ledger, "UnregisterEmptyAccount", empty), Is.True);
    }
}
