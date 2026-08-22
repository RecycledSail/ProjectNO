# Monetary Circulation Foundation Design

## Purpose

The game must begin with historically unequal wealth and must not create or destroy money during ordinary economic activity. Each nation issues and tracks its own currency. After initial data loading, taxes, consumption, production, subsidies, construction investment, and other ordinary flows move existing money between accounts. Only explicit currency issuance and destruction may change a nation's money supply.

This design establishes the monetary and market-settlement foundation required before employment and wages are implemented.

## Scope

This phase includes:

- Data-driven starting wealth for nations and province ethnic populations.
- Data-driven operating capital for each building type.
- One monetary ledger per nation and one provisional local ledger per neutral province.
- Conserving transfers, currency issuance, and currency destruction.
- Construction investment escrow and completed-building capitalization.
- Producer-owned market inventory.
- Consumer and building purchases that pay actual producers.
- A single sales tax withheld during market settlement.
- Removal of existing code paths that create, destroy, or overwrite money unintentionally.
- EditMode tests for all monetary invariants and settlement rounding.

This phase excludes:

- Employment assignment, wages, and dividends.
- Construction-company fees and construction-worker wages.
- Autonomous ethnic-pop investment decisions.
- Product quality, distance, transport cost, seller competition, and exchange rates.
- Cross-currency and international market settlement. The only cross-ledger operation in scope is the administrative 1:1 absorption of a neutral province into a nation.
- Additional tax types such as income, corporate, property, or wealth taxes.

## Approved Decisions

- Starting wealth is intentionally unequal and explicitly authored in JSON.
- A province ethnic population's `property` is the total wealth of that population record, not per-capita wealth.
- `livingStandard` is also loaded from the population record.
- Starting buildings are existing state-owned buildings. Their operating capital is paid from the owning nation's starting treasury after province ownership is assigned.
- A building has no automatically created default balance.
- Each building recipe defines an explicit `initialCapital` used as operating capital.
- Each nation owns a separate currency and `MoneyLedger`.
- A province with no nation owns a provisional local `MoneyLedger` so its populations, buildings, and local market can participate without inventing a country.
- Ordinary monetary movement uses a ledger transfer. Only national issuance changes supply upward; explicit destruction changes it downward.
- Market inventory retains producer ownership.
- Sales are allocated among suppliers in proportion to their owned stock for the sold product.
- A single sales tax is withheld from seller proceeds at settlement.
- GDP remains a statistic and no longer directly creates tax revenue.
- Current seller allocation treats equivalent products as identical. Future quality and distance rules will replace only seller selection and allocation policy, not inventory ownership or settlement.

## Starting Data

### Province ethnic populations

Every entry in `Assets/Resources/Provinces.json` gains explicit nonnegative `property` and positive `livingStandard` fields:

```json
{
  "name": "Elf",
  "population": [5333, 0, 0, 0],
  "culture": "Lisa",
  "property": 30000,
  "livingStandard": 0.7
}
```

The values are content-balancing data. They may differ by province, species, culture, national status, and historical conditions. The migration will seed every existing record with a concrete rough value; no runtime equality formula will override those values.

Neutral province records additionally define a nonnegative `initialLocalTreasury`. It funds that province's pre-existing buildings and belongs to the provisional local monetary authority:

```json
{
  "id": 10,
  "name": "Tarantsusi",
  "initialLocalTreasury": 50000
}
```

Nationally owned provinces must use zero for this field because their starting buildings are capitalized by the national treasury.

### Nations

Every entry in `Assets/Resources/Nations.json` gains a nonnegative `initialBalance`:

```json
{
  "id": 1,
  "name": "Nation1",
  "initialBalance": 100000
}
```

This is the treasury balance before capitalizing the nation's starting buildings. Authored balances must be large enough to fund every starting building owned by that nation.

### Building recipes

Every entry in `Assets/Resources/BuildingRecipes.json` gains a positive `initialCapital`:

```json
{
  "name": "WheatField",
  "buildRequirements": [
    { "item": "Wood", "amount": 800 }
  ],
  "TimeToBuild": 40,
  "initialCapital": 5000
}
```

`initialCapital` is operating money available after completion. It is separate from construction materials, future construction-company fees, and future wages. Constructing or preloading multiple levels capitalizes `initialCapital * level`.

### Validation

Initialization rejects invalid economic data with a clear error identifying the record and field:

- Negative monetary values.
- Missing or nonpositive `livingStandard`.
- Missing or nonpositive `initialCapital`.
- A national treasury unable to capitalize all of its starting buildings.
- A neutral local treasury unable to capitalize all buildings in its province.
- A population or building that has neither a national ledger nor a neutral province ledger after authority assignment.

The economic ledger is initialized only after these validations pass.

## Monetary Accounts and Ledger

### Account contract

`Nation`, `ProvinceEthnicPop`, `Building`, and construction mandate escrow participate as monetary accounts. Account balances use `long` consistently. Public gameplay code may read balances but may not assign them directly.

Each account belongs to exactly one national or neutral local currency ledger during this phase. Transfers between different ledgers are rejected because exchange rates and international settlement are out of scope. Neutral-province absorption is a separate administrative migration operation, not a market transfer.

### MoneyLedger responsibilities

Each `Nation` and each neutral `Province` owns one `MoneyLedger`. A neutral ledger has a local treasury account but no authority to call `Mint` or `Burn`. A ledger provides these monetary operations:

- `Transfer(from, to, amount, reason)`: debits and credits the same positive amount atomically. It does not change money supply.
- `TransferBatch(entries, reason)`: validates a balanced set of debits and credits, then applies all entries atomically. It is used when one purchase pays tax and multiple suppliers.
- `Mint(to, amount, reason)`: credits newly issued money and increases money supply by the same amount. Only the owning nation's government budget may request issuance.
- `Burn(from, amount, reason)`: debits money and decreases money supply by the same amount. Only the owning nation's government budget may request destruction.

Operations reject zero or negative amounts, unbalanced batches, insufficient funds, accounts belonging to another currency, unregistered accounts, and arithmetic overflow. A rejected operation changes no balance or statistic.

The ledger records enough transaction metadata for diagnostics: operation type, source, destination, amount, and reason. A full player-facing bank statement is not required in this phase.

### Money supply invariant

After startup capitalization completes, national supply is:

```text
MoneySupply =
    national treasury
  + all domestic province-pop balances
  + all domestic building balances
  + all active construction escrow balances
```

The initialized `MoneySupply` equals the sum of registered accounts. Ordinary transfers preserve it exactly. `Mint` and `Burn` change both the account sum and recorded supply by the same amount.

For a neutral province, the same invariant substitutes its local treasury for the national treasury and includes only that province's populations, buildings, and construction escrows.

`GovernmentBudget.MoneySupply` becomes a view of the nation's ledger rather than an independently initialized value.

## Initialization Order

Economic initialization occurs after nations, provinces, province ownership, populations, recipes, and starting buildings are loaded:

1. Load authored population properties and living standards.
2. Load authored national starting balances.
3. Create starting buildings with zero balance.
4. Assign configured provinces and their buildings to nations; leave unassigned provinces neutral.
5. Create each nation's ledger and register its treasury and domestic population accounts.
6. Create one local ledger and local treasury for every neutral province and register its population accounts.
7. Register every starting building with its national or neutral ledger and transfer `initialCapital * level` from the applicable treasury into the building.
8. Sum registered balances and seal that sum as each ledger's initial money supply.

Startup capitalization is a transfer, not issuance. It therefore does not change the total amount loaded from JSON.

## Neutral Province Absorption

When a neutral province later joins a nation, its local currency area is absorbed 1:1 without market exchange:

1. Pause settlement for that province for the duration of the ownership change.
2. Verify every local population, building, mandate escrow, and supplier-owned local-market lot belongs to the neutral ledger.
3. Remove those accounts and their unchanged balances from the neutral ledger's registered supply.
4. Register the population, building, and escrow accounts with the destination nation's ledger and add their unchanged balances to national supply.
5. Move the remaining local-treasury balance into the destination national treasury as part of the same migration, decreasing neutral supply and increasing national supply by the identical amount without `Mint`.
6. Keep local-market inventory in the province market with its existing supplier ownership; subsequent settlement uses the destination national ledger.
7. Close the now-empty neutral ledger and resume settlement.

The combined supply of the source and destination ledgers is identical before and after absorption. Failure during validation changes neither ownership nor monetary state. Nation-to-nation conquest, secession, exchange rates, and conversion ratios other than 1:1 remain outside this phase.

## Construction Investment

`ConstructionMandate` gains a monetary escrow balance and records the recipe's required operating capital when placed.

### Placement

1. Verify the investor and target belong to the same currency ledger.
2. Verify the investor can pay the full `initialCapital`.
3. Verify the complete construction-material reservation can succeed.
4. Create and register the mandate escrow account.
5. Commit the material reservation and transfer the full capital from investor to escrow as one placement operation with rollback on failure.
6. Add and assign the mandate through the existing construction-company flow.

If any validation or transfer fails, no mandate is created and no money moves.

### Completion

The building is created or its level is increased exactly once. The completed building is registered with the ledger, then the entire escrow balance is transferred into the building. For an upgrade, the capital is added to the existing building balance.

### Cancellation

Cancellation returns the entire escrow balance to the original investor before the mandate becomes terminal. Repeated cancellation or completion cannot move money twice.

Construction materials continue to use the existing reservation behavior in this phase. Construction-company fees and labor compensation are deferred to the employment phase and are not taken from `initialCapital`.

## Producer-Owned Inventory

### Ownership model

`ProductState.Stock`, `LastSupply`, `LastDemand`, and price behavior remain the aggregate market view used by existing UI and price calculations. Product inventory additionally records nonnegative quantities owned by each supplying monetary account.

The following invariant always holds:

```text
ProductState.Stock = sum of all producer-owned quantities for that product
```

Every addition to stock must identify a producer. Every removal caused by a purchase must remove the same amount from producer-owned quantities.

### Producers

- Building production registers the producing `Building` as supplier.
- Basic province production registers each `ProvinceEthnicPop` as supplier. The existing fixed basic-food output is divided among province populations in proportion to population, using deterministic remainder allocation.
- Basic production no longer assigns or overwrites `ProvinceEthnicPop.property`.

Market infrastructure is not a seller and never receives sale proceeds.

### Supplier allocation

When multiple suppliers own an equivalent product, sold quantity is allocated in proportion to current owned stock. Integer remainders are assigned deterministically by largest fractional remainder, followed by a stable supplier key. Seller net proceeds use the same sold-quantity weights and deterministic remainder rule.

This policy is isolated behind seller-allocation behavior so later quality, distance, and transport rules can replace it without changing inventory ownership or ledger settlement.

## Market Purchase and Settlement

Purchases are processed per buyer and product. The market never pools one population's money to subsidize another population's purchase.

For each purchase:

1. Clamp requested quantity by available aggregate stock.
2. Clamp affordable quantity by the buyer's available balance and the product's positive price.
3. Allocate the purchased quantity among suppliers.
4. Calculate `gross = purchased quantity * unit price` using checked `long` arithmetic.
5. Calculate `tax = floor(gross * salesTaxBasisPoints / 10000)`, clamped between zero and gross.
6. Build one balanced transfer batch that debits the buyer by gross, credits the treasury by tax, and credits suppliers with the remainder according to sold quantity.
7. Commit the transfer batch atomically.
8. Reduce supplier-owned quantities and aggregate stock by the purchased quantity.
9. Increase `LastDemand` by the purchased quantity.

All monetary transfers and inventory mutations form one settlement. Validation and allocation occur first. If any monetary operation cannot complete, neither money nor inventory changes.

The total settlement invariant is:

```text
buyer debit = supplier credits + tax credit
```

### Consumers

Existing food and category consumption continues choosing products with the current product preference and price behavior. Instead of directly subtracting `property` and stock, it calls the market purchase operation for each population account.

### Building inputs

A building may consume an input only by purchasing it through the same market settlement. Its own balance limits affordable production scale. Input sellers receive payment and the state receives sales tax. If required input stock or building funds are insufficient, production scales down before any input is removed.

## Taxation and Government Policy

The existing GDP-based `CollectTaxes()` money creation is removed. GDP and average GDP remain reporting statistics.

The only tax in this phase is the nation's sales tax applied during successful domestic market settlement. It is stored as integer `salesTaxBasisPoints` from 0 through 10,000, where 1,000 means 10%. Any existing float-facing UI may convert this value for display, but settlement uses integer arithmetic. `WeeklyTaxRevenue` is reset at the start of the week and accumulates actual tax transfers received during that week.

Government expenditures such as military salary, industry subsidy, research allocation, and real-estate support use ledger transfers from the treasury. If a policy is explicitly funded by new issuance, the government first mints the approved amount into the treasury and then transfers it to recipients. This prevents the current double credit in which issued money remains in the treasury while also appearing in recipient accounts.

Policies cannot spend more existing treasury money than is available unless the same operation explicitly authorizes issuance.

## Failure Handling and Diagnostics

- Invalid startup data stops economic initialization with a record-specific error.
- Insufficient purchase funds reduce affordable quantity; zero affordable quantity produces no transaction.
- Insufficient construction investment rejects mandate placement without partial state.
- Failed atomic settlement leaves balances, inventory ownership, aggregate stock, demand, and tax statistics unchanged.
- Ledger audits compare account sums with recorded supply after initialization and at deterministic simulation checkpoints in development builds and tests.
- Any aggregate inventory mismatch identifies the market and product in its diagnostic message.
- Checked arithmetic prevents silent overflow in price, quantity, balance, and money-supply calculations.

## Compatibility and Migration

- Existing UI may continue reading `Nation.balance`, `ProvinceEthnicPop.property`, `Building.balance`, and `GovernmentBudget.MoneySupply`, but these become read-only views backed by controlled account balances.
- `ProductState.Stock` remains available for existing UI and price logic.
- Existing market price calculation remains unchanged.
- Existing construction mandate status, assignment, manhours, and completion behavior remains unchanged except for capital escrow requirements.
- Save data must include all monetary account balances, mandate escrow balances, money supply, and producer-owned inventory before save/load can safely preserve this system. If the current save format remains inactive or incomplete, implementation must not claim economic save compatibility.

## Testing Strategy

EditMode tests cover:

- Initial supply equals the exact sum of loaded treasury, population, building, and escrow accounts.
- Unequal JSON population properties and living standards load without runtime replacement.
- Starting-building capitalization debits treasury and credits buildings without changing supply.
- Transfer preserves supply and rejects invalid or unaffordable operations atomically.
- Mint and Burn change account sum and recorded supply by exactly the requested amount.
- Cross-ledger transfers are rejected.
- Neutral province absorption moves account registration and supply 1:1, empties the neutral ledger, and preserves combined supply.
- Construction placement, cancellation, completion, and upgrade move capital exactly once.
- Basic and building production create owned inventory but no money.
- Aggregate stock always equals supplier-owned stock.
- Proportional supplier allocation is deterministic and conserves integer quantity.
- Purchase settlement exactly balances buyer debit, seller credits, and tax credit.
- Consumption cannot spend more than an individual population owns.
- Building input purchases pay suppliers and limit production by funds and stock.
- GDP updates do not alter balances or money supply.
- Government issuance plus policy distribution does not double credit money.
- A representative weekly simulation without issuance or destruction ends with unchanged national money supply.

## Success Criteria

The phase is complete when all existing domestic monetary mutations use the ledger, every sold market item has an accountable producer, construction operating capital comes from the investor, sales fund producers and the treasury, and automated tests demonstrate exact national money conservation in every non-issuance flow.
