using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static GlobalVariables;
using static SaveManager;

/// <summary>A detached, validated world. References in the JSON are stable names/account IDs.</summary>
public sealed class GameSaveState
{
    private SaveDataFormat data;
    private readonly Dictionary<string, Nation> nations = new();
    private readonly Dictionary<string, Province> provinces = new();
    private readonly Dictionary<string, MoneyAccount> accounts = new();
    private readonly Dictionary<string, IBuildingInvestor> investors = new();
    private readonly Dictionary<string, Building> buildings = new();
    private readonly Dictionary<string, MoneyLedger> ledgers = new();
    private readonly Dictionary<string, ConstructionMandate> mandates = new();
    private readonly Dictionary<int, Regiment> regiments = new();
    private readonly Dictionary<Province, Battle> battles = new();
    private readonly Dictionary<string, List<Province>> adjacency = new();
    private List<User> users;
    private User player;
    private List<Regiment> activeRegiments;

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    internal static List<AmountData> Amounts(IEnumerable<KeyValuePair<string, long>> values) =>
        values.Select(p => new AmountData { key = p.Key, value = p.Value }).ToList();
    internal static Dictionary<string, long> AmountMap(List<AmountData> values) => values.ToDictionary(p => p.key, p => p.value);

    public static void ValidateHeader(SaveDataFormat data)
    {
        Require(data != null && data.format == "ProjectNO" && data.version == CurrentVersion, "Unsupported save format.");
        _ = new DateTime(data.year, data.month, data.day);
        Require(data.dayOfWeek >= 0 && data.dayOfWeek < 7 && data.employmentWeek >= 0 &&
            (data.speed == 1 || data.speed == 2 || data.speed == 4 || data.speed == 8), "Invalid simulation clock.");
        Require(data.nations != null && data.nations.Count > 0 && data.provinces != null && data.provinces.Count > 0 &&
            data.accounts != null && data.ledgers != null && data.users != null && data.users.Count > 0 &&
            data.diplomacies != null && data.battles != null && data.activeRegiments != null, "Incomplete save.");
    }

    public static SaveDataFormat Capture(GameManager game, BattleManager battleManager)
    {
        var result = new SaveDataFormat
        {
            format = "ProjectNO", version = CurrentVersion,
            year = game.year, month = game.month, day = game.day, dayOfWeek = game.dayoftheWeek,
            employmentWeek = game.EmploymentWeek, speed = game.SavedSpeed, paused = game.paused,
            playerId = game.player.id, nextRegimentId = Regiment.global_id,
            users = game.users.Select(u => new UserData { id = u.id, nation = u.nation.name }).ToList(),
            activeRegiments = battleManager.SaveRegiments.Select(r => r.id).ToList(),
            battles = battleManager.SaveBattles.Select(b => new BattleData
            {
                province = b.battleArea.name, attackers = b.attackRegiments.Select(r => r.id).ToList(),
                defenders = b.defenseRegiments.Select(r => r.id).ToList(), winProbability = b.winProbability
            }).ToList()
        };
        var savedAccounts = new Dictionary<string, MoneyAccount>();
        var savedLedgers = new HashSet<MoneyLedger>();
        void Account(MoneyAccount account)
        {
            if (account == null) return;
            if (savedAccounts.TryGetValue(account.Id, out var previous))
            {
                Require(ReferenceEquals(previous, account), "Duplicate account ID: " + account.Id);
                return;
            }
            savedAccounts.Add(account.Id, account);
            result.accounts.Add(new AccountData { id = account.Id, balance = account.Balance, ledger = account.Ledger?.CurrencyId });
        }
        void Ledger(MoneyLedger ledger, string nation, string province)
        {
            if (ledger == null || !savedLedgers.Add(ledger)) return;
            Require(ledger.Audit(out _), "Cannot save an unbalanced ledger: " + ledger.CurrencyId);
            result.ledgers.Add(new LedgerData
            {
                id = ledger.CurrencyId, nation = nation, province = province, treasury = ledger.TreasuryAccount.Id,
                supply = ledger.MoneySupply, weeklyTax = ledger.WeeklyTaxRevenue, taxRate = ledger.SalesTaxBasisPoints,
                transactions = ledger.Transactions.Select(t => new TransactionData
                {
                    kind = t.Kind, amount = t.Amount, reason = t.Reason,
                    sources = t.SourceIds.ToList(), destinations = t.DestinationIds.ToList()
                }).ToList()
            });
            foreach (var account in ledger.SaveAccounts) Account(account);
            Account(ledger.TreasuryAccount);
        }
        foreach (var nation in game.nations.Values)
        {
            Require(nation.Ledger != null, "Nation economy is not initialized.");
            Ledger(nation.Ledger, nation.name, null);
            Account(nation.Account);
            var n = new NationData
            {
                id = nation.id, name = nation.name, capital = nation.capital?.name, color = nation.color,
                provinces = nation.provinces.Select(p => p.name).ToList(),
                research = nation.doneResearches.Select(r => r.name).ToList(),
                buffs = nation.buffs.Select(p => new BuffData { kind = p.Key, value = p.Value }).ToList(),
                gdp = nation.GDP, gdpAverage = nation.GDPAverage, researchFund = nation.researchFund,
                gdpHistory = nation.GDPHistory.ToList(), budget = nation.governmentBudget.CaptureBudget(),
                market = CaptureMarket(nation.market.Products),
                regiments = nation.regiments.Select(r => new RegimentData
                {
                    id = r.id, name = r.name, location = r.location.name, state = r.state,
                    squads = r.units.Values.Select(s => new SquadData
                    { type = s.unitType.name, capacity = s.capacity, population = s.population }).ToList()
                }).ToList(),
                mandates = nation.ConstructionMandates.Select(m => m.CaptureMandate()).ToList()
            };
            foreach (var mandate in nation.ConstructionMandates) Account(mandate.EscrowAccount);
            result.nations.Add(n);
        }
        foreach (var province in game.provinces.Values)
        {
            Require(province.Employment != null && province.ActiveLedger != null, "Province economy is not initialized.");
            Ledger(province.LocalLedger, null, province.name);
            Account(province.LocalTreasuryAccount);
            foreach (var pop in province.provinceEthnicPops) Account(pop.Account);
            foreach (var building in province.buildings.Values) Account(building.Account);
            result.provinces.Add(new ProvinceData
            {
                id = province.id, name = province.name, nation = province.nation?.name, topography = province.topo,
                ledger = province.ActiveLedger.CurrencyId, localLedger = province.LocalLedger?.CurrencyId,
                localTreasury = province.LocalTreasuryAccount?.Id,
                road = province.road, desolation = province.desolation, connected = province.isConnectedToCapital,
                pops = province.provinceEthnicPops.Select(p => new PopData
                {
                    species = p.ethnicGroup.species.name, culture = p.ethnicGroup.culture.name,
                    dividend = p.dividend, livingStandard = p.livingStandard,
                    ages = Enum.GetValues(typeof(AgeGroupType)).Cast<AgeGroupType>()
                        .Select(type => p.ageGroups.Single(a => a.type == type).agepopulation).ToList()
                }).ToList(),
                buildings = province.buildings.Values.Select(b => new BuildingData
                {
                    type = b.buildingType.name, owner = b.Owner?.InvestmentAccount.Id,
                    level = b.level, previousGain = b.previousGain, manhoursLeft = b.manhoursLeft,
                    slots = b is ConstructionCompanyBuilding company ? company.CaptureSlots() : new List<CompanySlotData>()
                }).ToList(),
                specialBuildings = province.specialBuildings.Values.Select(b => new SpecialBuildingData
                { type = b.buildingType.name, level = b.level, workers = b.currentWorkers, manhoursLeft = b.manhoursLeft }).ToList(),
                market = CaptureMarket(province.market.Products), lastPaidWeek = province.Employment.LastPaidWeek,
                employmentError = province.Employment.LastError, employment = province.Employment.CaptureEmployment(),
                neighbors = ADJACENT_PROVINCES.TryGetValue(province.name, out var adjacent)
                    ? adjacent.Select(p => p.name).ToList() : new List<string>()
            });
        }
        result.diplomacies = game.nations.Values.SelectMany(n => n.allies.Values.Concat(n.enemies.Values))
            .Distinct().Where(d => d.isActive).Select(d => new DiplomacyData
            { type = d.type, left = d.leftNations.Select(n => n.name).ToList(), right = d.rightNations.Select(n => n.name).ToList() }).ToList();
        ValidateHeader(result);
        return result;
    }

    private static List<ProductData> CaptureMarket(Dictionary<string, ProductState> products) => products.Values.Select(p =>
        new ProductData
        {
            name = p.ProductName, price = p.Price, lastPrice = p.LastPrice, demand = p.LastDemand,
            supply = p.LastSupply, elasticity = p.Elasticity,
            lots = p.Inventory.Lots.Select(l => new AmountData { key = l.Key.Id, value = l.Value }).ToList()
        }).ToList();

    public static GameSaveState Restore(SaveDataFormat data)
    {
        ValidateHeader(data);
        var state = new GameSaveState { data = data };
        state.BuildActors();
        state.BuildEconomy();
        state.BuildConstruction();
        state.BuildMilitary();
        state.users = data.users.Select(u => new User(u.id, state.nations[u.nation])).ToList();
        Require(state.users.Select(u => u.id).Distinct().Count() == state.users.Count, "Duplicate user ID.");
        state.player = state.users.Single(u => u.id == data.playerId);
        return state;
    }

    private void BuildActors()
    {
        var accountData = data.accounts.ToDictionary(a => a.id);
        void Bind(MoneyAccount account)
        {
            var saved = accountData[account.Id];
            Require(saved.balance >= 0, "Negative account balance.");
            account.ApplyDelta(checked(saved.balance - account.Balance));
            accounts.Add(account.Id, account);
        }
        foreach (var n in data.nations)
        {
            Require(NATIONS.ContainsKey(n.name), "Unknown nation: " + n.name);
            var nation = new Nation(n.id, n.name, new List<ResearchNode>())
            {
                color = n.color, doneResearches = n.research.Select(name => RESEARCH_NODE[name]).ToList(),
                buffs = n.buffs.ToDictionary(b => b.kind, b => b.value),
                GDP = n.gdp, GDPAverage = n.gdpAverage, researchFund = n.researchFund
            };
            Require(n.gdp >= 0 && n.gdpAverage >= 0 && n.researchFund >= 0 && n.gdpHistory.Count <= 4 &&
                n.gdpHistory.All(v => v >= 0) && n.buffs.All(b => Finite(b.value)), "Invalid national statistics.");
            foreach (long value in n.gdpHistory) nation.GDPHistory.Enqueue(value);
            nations.Add(n.name, nation);
            Bind(nation.Account);
            investors.Add(nation.Account.Id, nation);
        }
        foreach (var p in data.provinces)
        {
            Require(PROVINCES.ContainsKey(p.name) && Enum.IsDefined(typeof(Topography), p.topography) &&
                (p.road == 0 || p.road == 1) && p.desolation >= 0 && p.desolation <= 100, "Invalid province: " + p.name);
            var province = new Province(p.id, p.name, p.topography)
            { road = p.road, isConnectedToCapital = p.connected, market = new ProvinceMarket(p.name) };
            province.SetDesolation(p.desolation);
            provinces.Add(p.name, province);
            if (!string.IsNullOrEmpty(p.nation)) Require(nations[p.nation].AddProvinces(province), "Duplicate province ownership.");
            foreach (var pop in p.pops)
            {
                Require(pop.ages.Count == 4 && pop.ages.All(a => a >= 0) && Finite(pop.livingStandard) && pop.livingStandard > 0,
                    "Invalid population.");
                var key = (SPECIES_SPEC[pop.species], CULTURE[pop.culture]);
                EthnicGroup group;
                if (province.nation != null)
                {
                    if (!province.nation.ethnicGroups.TryGetValue(key, out group))
                        province.nation.ethnicGroups.Add(key, group = new EthnicGroup(key.Item1, key.Item2));
                }
                else group = new EthnicGroup(key.Item1, key.Item2);
                var population = new ProvinceEthnicPop(province, group, new List<int> { 0, 0, 0, 0 }, 0, pop.livingStandard)
                {
                    dividend = pop.dividend,
                    ageGroups = pop.ages.Select((count, index) => new ProvinceEthnicPop.AgeGroup((AgeGroupType)index, count)).ToList(),
                    population = pop.ages.Sum()
                };
                province.provinceEthnicPops.Add(population);
                group.provincePops.Add(population);
                Bind(population.Account);
                investors.Add(population.Account.Id, population);
            }
            province.InitializePopulation();
            foreach (var b in p.buildings)
            {
                Require(b.level >= 0 && Finite(b.manhoursLeft) && b.manhoursLeft >= 0, "Invalid building.");
                var type = BUILDING_TYPE[b.type];
                var building = BuildingFactory.Create(type, province, b.level);
                building.previousGain = b.previousGain;
                building.manhoursLeft = b.manhoursLeft;
                province.buildings.Add(type, building);
                buildings.Add(building.Account.Id, building);
                Bind(building.Account);
            }
            foreach (var b in p.specialBuildings)
            {
                Require(b.level >= 0 && b.workers >= 0 && Finite(b.manhoursLeft) && b.manhoursLeft >= 0, "Invalid special building.");
                var type = SPECIAL_BUILDING_TYPE[b.type];
                province.specialBuildings.Add(type, new SpecialBuilding(type, province)
                { level = b.level, currentWorkers = b.workers, manhoursLeft = b.manhoursLeft });
            }
        }
        Require(provinces.Count == PROVINCES.Count && nations.Count == NATIONS.Count, "Save world does not match the current scenario.");
        foreach (var a in data.accounts)
        {
            Require(!string.IsNullOrWhiteSpace(a.id) && a.balance >= 0, "Invalid account.");
            if (!accounts.ContainsKey(a.id)) accounts.Add(a.id, new MoneyAccount(a.id, a.balance));
        }
        foreach (var n in data.nations)
        {
            var nation = nations[n.name];
            var owned = n.provinces.Select(name => provinces[name]).ToList();
            Require(owned.Count == nation.provinces.Count && owned.Distinct().Count() == owned.Count &&
                owned.All(p => p.nation == nation), "National province list does not match ownership.");
            nation.provinces = owned;
            nation.capital = string.IsNullOrEmpty(n.capital) ? null : provinces[n.capital];
            Require(nation.capital == null || nation.capital.nation == nation, "Capital is not owned by its nation.");
        }
        foreach (var p in data.provinces)
        {
            var province = provinces[p.name];
            adjacency.Add(p.name, p.neighbors.Select(name => provinces[name]).ToList());
            foreach (var b in p.buildings)
                province.buildings[BUILDING_TYPE[b.type]].Owner = string.IsNullOrEmpty(b.owner) ? null : investors[b.owner];
        }
    }

    private void BuildEconomy()
    {
        foreach (var l in data.ledgers)
        {
            Require(string.IsNullOrEmpty(l.nation) != string.IsNullOrEmpty(l.province), "Invalid currency authority.");
            Nation nation = string.IsNullOrEmpty(l.nation) ? null : nations[l.nation];
            var ledger = new MoneyLedger(l.id, nation, accounts[l.treasury]);
            ledgers.Add(l.id, ledger);
            if (nation != null)
            {
                Require(nation.Ledger == null && nation.Account == accounts[l.treasury], "Invalid national treasury.");
                nation.Ledger = ledger;
            }
            else
            {
                var province = provinces[l.province];
                Require(province.LocalLedger == null, "Duplicate local currency.");
                province.LocalLedger = ledger;
                province.LocalTreasuryAccount = accounts[l.treasury];
            }
        }
        foreach (var a in data.accounts)
            if (!string.IsNullOrEmpty(a.ledger))
                Require(ledgers[a.ledger].RegisterInitialAccount(accounts[a.id]), "Cannot register account: " + a.id);
        foreach (var l in data.ledgers) ledgers[l.id].RestoreStatistics(l);
        foreach (var p in data.provinces)
        {
            var province = provinces[p.name];
            province.ActiveLedger = ledgers[p.ledger];
            if (!string.IsNullOrEmpty(p.localTreasury)) province.LocalTreasuryAccount = accounts[p.localTreasury];
            Require(province.LocalLedger == (string.IsNullOrEmpty(p.localLedger) ? null : ledgers[p.localLedger]), "Local ledger mismatch.");
            Require(province.ActiveLedger == (province.nation?.Ledger ?? province.LocalLedger), "Province currency mismatch.");
            foreach (var account in province.provinceEthnicPops.Select(pop => pop.Account).Concat(province.buildings.Values.Select(b => b.Account)))
                Require(province.ActiveLedger.OwnsAccount(account), "Actor account belongs to the wrong currency.");
            Require(province.nation != null || province.ActiveLedger.OwnsAccount(province.LocalTreasuryAccount), "Missing neutral treasury.");
            province.market.Products = RestoreMarket(p.market, province.ActiveLedger);
            ProvinceEmployment.RestoreEmployment(province, p, data.employmentWeek);
        }
        foreach (var n in data.nations)
        {
            var nation = nations[n.name];
            Require(nation.Ledger != null && nation.Ledger.OwnsAccount(nation.Account), "Missing nation ledger.");
            nation.market.Products = RestoreMarket(n.market, nation.Ledger);
            nation.governmentBudget.RestoreBudget(n.budget);
        }
    }

    private Dictionary<string, ProductState> RestoreMarket(List<ProductData> products, MoneyLedger ledger)
    {
        var result = new Dictionary<string, ProductState>();
        foreach (var p in products)
        {
            Require(PRODUCTS.ContainsKey(p.name) && p.price > 0 && p.lastPrice >= 0 && p.demand >= 0 && p.supply >= 0 &&
                Finite(p.elasticity), "Invalid market product: " + p.name);
            var product = new ProductState(p.name, p.price)
            { LastPrice = p.lastPrice, LastDemand = p.demand, LastSupply = p.supply, Elasticity = p.elasticity };
            foreach (var lot in AmountMap(p.lots))
            {
                var supplier = accounts[lot.Key];
                Require(ledger.OwnsAccount(supplier), "Inventory supplier belongs to the wrong currency.");
                product.Inventory.Add(supplier, checked((int)lot.Value));
            }
            result.Add(p.name, product);
        }
        return result;
    }

    private void BuildConstruction()
    {
        foreach (var n in data.nations)
        {
            foreach (var m in n.mandates)
            {
                var company = string.IsNullOrEmpty(m.company) ? null : buildings[m.company] as ConstructionCompanyBuilding;
                var mandate = new ConstructionMandate(m, investors[m.investor], BUILDING_TYPE[m.type], provinces[m.province],
                    string.IsNullOrEmpty(m.escrow) ? null : accounts[m.escrow], company);
                mandates.Add(m.id, mandate);
                nations[n.name].RestoreMandate(mandate);
            }
        }
        foreach (var p in data.provinces)
            foreach (var b in p.buildings)
            {
                var building = provinces[p.name].buildings[BUILDING_TYPE[b.type]];
                if (building is ConstructionCompanyBuilding company) company.RestoreSlots(b.slots, mandates);
                else Require(b.slots.Count == 0, "A non-construction building has project slots.");
            }
        foreach (var m in mandates.Values.Where(m => m.IsActive && m.AssignedCompany != null))
            Require(m.AssignedCompany.ActiveProjects.Contains(m), "Active construction is missing from its company's slots.");
    }

    private void BuildMilitary()
    {
        foreach (var d in data.diplomacies)
        {
            Require(Enum.IsDefined(typeof(DiplomacyType), d.type), "Invalid diplomacy type.");
            _ = new Diplomacy(d.left.Select(n => nations[n]).ToHashSet(), d.right.Select(n => nations[n]).ToHashSet(), d.type);
        }
        foreach (var n in data.nations)
            foreach (var r in n.regiments)
            {
                Require(r.id >= 0 && Enum.IsDefined(typeof(RegimentState), r.state), "Invalid regiment.");
                var regiment = new Regiment(nations[n.name], r, provinces[r.location]);
                regiments.Add(r.id, regiment);
                nations[n.name].regiments.Add(regiment);
            }
        Require(data.nextRegimentId >= 0 && regiments.Keys.All(id => id < data.nextRegimentId), "Invalid next regiment ID.");
        activeRegiments = data.activeRegiments.Select(id => regiments[id]).ToList();
        Require(activeRegiments.Distinct().Count() == activeRegiments.Count, "Duplicate active regiment.");
        var fighting = new HashSet<Regiment>();
        foreach (var b in data.battles)
        {
            var province = provinces[b.province];
            var attackers = b.attackers.Select(id => regiments[id]).ToList();
            var defenders = b.defenders.Select(id => regiments[id]).ToList();
            Require(attackers.Count > 0 && defenders.Count > 0 && Finite(b.winProbability), "Invalid battle.");
            foreach (var regiment in attackers.Concat(defenders))
                Require(activeRegiments.Contains(regiment) && regiment.location == province && regiment.state == RegimentState.BATTLE &&
                    fighting.Add(regiment), "Invalid battle participant.");
            battles.Add(province, new Battle(attackers, defenders, province) { winProbability = b.winProbability });
        }
        Require(regiments.Values.All(r => r.state != RegimentState.BATTLE || fighting.Contains(r)), "Regiment is missing its battle.");
    }

    public void Apply(GameManager game, BattleManager battleManager)
    {
        var colors = game.colorToProvince.Where(p => provinces.ContainsKey(p.Value.name))
            .ToDictionary(p => p.Key, p => provinces[p.Value.name]);
        // All fallible reconstruction has completed. Publish the same instances to
        // simulation, map selection, adjacency, and battle systems in one phase.
        game.nations = nations;
        game.provinces = provinces;
        game.users = users;
        game.player = player;
        NATIONS = nations;
        PROVINCES = provinces;
        ADJACENT_PROVINCES = adjacency;
        game.colorToProvince = colors;
        Regiment.global_id = data.nextRegimentId;
        ConstructionMandate.ResetTracking(mandates.Values);
        game.RestoreClock(data);
        battleManager.RestoreBattles(activeRegiments, battles);
    }
}
