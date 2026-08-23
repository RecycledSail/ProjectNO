using System.Collections.Generic;

public static class ProvinceCurrencyMigration
{
    public static bool TryAbsorbNeutralProvince(
        Province province,
        Nation destination,
        out string error)
    {
        error = null;
        if (province == null || destination == null)
        {
            error = "The province and destination nation are required.";
            return false;
        }

        if (province.nation != null || !destination.CanAddProvince(province))
        {
            error = "Only an unowned province can be absorbed by the destination nation.";
            return false;
        }

        MoneyLedger sourceLedger = province.LocalLedger;
        MoneyAccount sourceTreasury = province.LocalTreasuryAccount;
        MoneyLedger destinationLedger = destination.Ledger;
        if (sourceLedger == null || sourceTreasury == null || destinationLedger == null)
        {
            error = "Both initialized ledgers and the local treasury are required.";
            return false;
        }

        if (province.ActiveLedger != sourceLedger)
        {
            error = "The province active ledger does not match its local ledger.";
            return false;
        }

        if (!province.TryCollectMigrationAccounts(out List<MoneyAccount> accounts, out error))
            return false;

        foreach (MoneyAccount escrow in ConstructionMandate.GetActiveEscrowAccountsFor(province))
        {
            if (escrow == null || accounts.Contains(escrow))
            {
                error = "The province has an invalid construction mandate escrow account.";
                return false;
            }

            accounts.Add(escrow);
        }

        accounts.Add(sourceTreasury);
        if (!sourceLedger.TryMigrateEntireLedgerTo(
            destinationLedger,
            accounts,
            sourceTreasury,
            destination.Account,
            $"Absorb neutral province {province.name}",
            out error))
            return false;

        province.LocalLedger = null;
        province.LocalTreasuryAccount = null;
        province.ActiveLedger = destinationLedger;

        // CanAddProvince was checked before migration, so this cannot fail without
        // an external state change between the two operations.
        if (!destination.AddProvinces(province))
            throw new System.InvalidOperationException("Province ownership changed during absorption.");

        return true;
    }
}
