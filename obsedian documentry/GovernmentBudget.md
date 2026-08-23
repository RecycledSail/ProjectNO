# GovernmentBudget

`GovernmentBudget` is the nation-facing view of the monetary ledger and the
place where an explicit policy issuance is planned. It does not own a separate
money-supply counter and it does not derive government revenue from GDP.

## Ledger-backed money

Each nation has one `MoneyLedger`. Every treasury, population, building,
supplier, and active construction-escrow account that uses the national
currency is registered with that ledger.

`MoneySupply` is the exact sum of the balances of those registered accounts.
It is not an arbitrary starting constant. Economic initialization registers
the authored opening balances, capitalizes existing buildings by a treasury-to-
building transfer, seals the ledger, and audits the resulting supply.

Ordinary transfers conserve supply. A purchase, starting-building
capitalization, construction investment, refund, completion, subsidy, or
salary moves existing money between registered accounts; it does not create or
destroy money. `MoneyLedger.Audit` compares recorded supply with the exact sum
of registered balances and reports any divergence. No tolerance is used.

At the aggregate-economy boundary, only the ledger's authority-gated issuance
operations change combined supply:

- `TryMint` increases a registered account and `MoneySupply` by the same exact
  amount. The caller must be the ledger's issuance authority.
- `TryBurn` decreases a funded registered account and `MoneySupply` by the same
  exact amount. It requires the same authority.

For a nation, the `Nation` object is the issuance authority. Neutral-province
ledgers have no issuance authority and therefore cannot mint or burn.

Individual ledgers also change their recorded share of supply during opening
account registration and neutral absorption. Registration establishes authored
opening supply. Absorption subtracts an exact amount from the neutral ledger
and adds the same amount to the national ledger, so combined economy supply is
unchanged.

## Purchases and actual sales tax

Market stock is owned inventory. Every unit is held in a `ProductInventory`
lot keyed by a supplier `MoneyAccount`; `ProductState.Stock` is only the sum of
those lots. Province-to-nation market transfer preserves both the quantities
and their supplier accounts.

`MarketSettlement` charges the buyer the gross purchase amount, credits the
selling supplier accounts with the net proceeds, and credits the national or
local treasury with sales tax in one balanced ledger batch. The rate is stored
as integer basis points on the ledger:

```text
tax = gross amount * SalesTaxBasisPoints / 10,000
seller proceeds = gross amount - tax
```

The default rate is 1,000 basis points (10%). The batch must balance exactly,
all accounts must belong to the same ledger, and the inventory sale commits
only after the monetary batch succeeds.

`WeeklyTaxRevenue` is a statistic accumulated from successful real purchase
settlements. `BeginWeek` resets the statistic without moving money or changing
supply. GDP is also statistical: `UpdateGDPWeekly` updates GDP and its rolling
average, but GDP does not create tax income and there is no GDP-based tax
collection path.

## Policy issuance and redistribution

`PolicyAllocation` records proposed research, military-salary, industry-
subsidy, and real-estate amounts. `GovernmentBudget.PrintMoney` preflights the
complete policy before mutation. When the plan is valid, the ledger records
one issuance for `Policy.Total` into the national treasury and then applies the
funded policy channels as balanced transfers.

Issuance increases supply once. The later policy transfers only redistribute
that issued money. Research allocation and any channel without a valid
recipient stay in the treasury. The policy record and related non-monetary
effects are committed only after the atomic ledger operation succeeds.

## Industry-subsidy allocation helper

`SetIndustrySubsidyTotal(long totalAmount)` prepares the industry portion of
the pending policy; it does not transfer money by itself. It first clears the
current `Policy.IndustrySubsidy`. For a positive amount, it collects the
distinct building-type names present in the nation's provinces in ordinal
sort order, divides the amount evenly across those types, and assigns any
integer remainder to the first type. A nonpositive amount or a nation with no
building types leaves the industry allocation empty.

`BudgetUI` and `FinanceUI` use this helper when their industry slider changes.
`IndustrySubsidyPanel` can instead rebuild the same pending dictionary by
building type. The dictionary keys are `BuildingType.name` strings, so renaming
a building type also changes the policy key expected by these interfaces.

## Inflation statistics

`UpdateInflation()` is statistical and does not move money. It calls the
private `CalculatePriceIndex()`, assigns `CurrentPriceIndex`, and keeps at most
four weekly price-index samples. With at least two samples and a positive
oldest sample, it calculates:

```text
InflationRate = (current index - oldest retained index)
                / oldest retained index * 100
```

Otherwise the rate is `0`.

`CalculatePriceIndex()` computes a national-market weighted average. Each
product's weight is `LastSupply + LastDemand`; products with nonpositive weight
are skipped. If the total weight is zero, the index is `1`. Money supply is not
an input to this price-index formula.

`GetInflationStatus()` maps the current percentage to UI text:

| Condition | Status |
| --- | --- |
| `> 10%` | `Hyperinflation` |
| `> 5%` | `High Inflation` |
| `> 2%` | `Inflation` |
| `> 0%` | `Mild Rise` |
| `< -5%` | `Deflation` |
| `< -2%` | `Mild Fall` |
| otherwise | `Stable` |

## Budget and finance UI behavior

`BudgetUI` uses 50% of the nation's rolling `GDPAverage` as its maximum policy
proposal, with a minimum slider ceiling of `1`. On confirmation it reads the
research, military, industry, and real-estate sliders. If their sum exceeds
the ceiling, it scales all four down proportionally, creates a new pending
`PolicyAllocation`, and calls `SetIndustrySubsidyTotal` for the industry share.
GDP is only a UI issuance limit here; it is not tax revenue and the confirmation
does not move money.

`FinanceUI` displays the ledger-backed `MoneySupply`, `WeeklyTaxRevenue`, and
the current inflation rate. Its sliders are percentages of actual weekly sales-
tax revenue and convert a percentage to a proposed amount with
`(long)(pct / 100f * revenue)`. It writes those proposed values into the same
pending `PolicyAllocation` and highlights a combined percentage over 100%; it
does not settle purchases, collect tax, or apply policy itself. The next
successful `PrintMoney()` remains an explicit issuance followed by conserved
policy transfers, not expenditure of the weekly-tax statistic.

## Neutral provinces and absorption

A neutral province receives a provisional local ledger, a local treasury, and
registered population and building accounts during economic initialization.
Its local ledger is sealed and audited in the same way as a national ledger.

When a nation absorbs a neutral province, the complete local account set,
including any active construction escrows, migrates to the national ledger.
The local treasury balance is credited 1:1 to the national treasury, actor
balances are preserved, local recorded supply falls to zero, and national
recorded supply rises by exactly the absorbed amount. Combined supply across
the two ledgers is unchanged. The migration is a currency-ledger move, not
issuance.

## Construction capital

Existing buildings receive their recipe-defined operating capital during
initialization through conserved treasury transfers.

Placing a construction mandate registers a zero-balance escrow and transfers
the full recipe `InitialCapital` from the national investor into it. Cancelling
refunds the escrow. Completing construction transfers the full escrow to the
new or upgraded building exactly once, then unregisters the empty escrow.
Placement, cancellation, completion, and upgrades therefore conserve supply.
Construction material requirements are reserved by active mandates; this
phase does not turn that reservation into a paid material purchase.

## Current omissions and save-format status

The completed circulation phase does not yet model:

- employment contracts or ledger-backed wages;
- construction-company fees or paid construction materials;
- product quality, transport distance, or exchange between currencies;
- full save/load compatibility for the new economic state.

`Assets/Scripts/Manager/SaveManager.cs` currently serializes only the older
high-level province, nation, research, user, and date structures. It does not
serialize monetary accounts or balances, ledger registration and transaction
state, active construction escrows, or supplier-owned market inventory. Its
load path likewise does not reconstruct those objects.

Existing saves must not be claimed compatible with the ledger-backed economy.
The first requirement of a future save-format migration is to serialize and
atomically reconstruct accounts, ledgers, escrows, and supplier inventory
together, then audit every reconstructed ledger. This phase intentionally does
not partially migrate the save format.

## Related runtime types

- `MoneyAccount`, `MoneyLedger`: balances, supply, authority, transfers, and
  audits.
- `MarketSettlement`, `ProductInventory`, `ProductState`: paid settlement and
  supplier-owned stock.
- `EconomicInitializer`: opening account registration and starting capital.
- `ConstructionMandate`, `ConstructionCompanyBuilding`: escrowed construction
  investment and completion.
- `GovernmentBudget`, `PolicyAllocation`: ledger views and explicit policy
  issuance.
- `GameManager`, `EconomicEngine`: weekly reset, production, consumption, GDP,
  inflation, and development/editor audits.
