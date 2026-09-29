using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SaveManager
{
    public const int CurrentVersion = 2;
    public static string LastError { get; private set; }

    public readonly struct SaveSummary
    {
        public readonly string NationName;
        public readonly long GDP;
        public readonly int Year;

        public SaveSummary(string nationName, long gdp, int year)
        {
            NationName = nationName;
            GDP = gdp;
            Year = year;
        }
    }

    public static string GetSavePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains("/") || name.Contains("\\") || name.EndsWith(".") ||
            name == "." || name == "..")
            throw new ArgumentException("저장 이름에 사용할 수 없는 문자가 있습니다.");
        return Path.Combine(Application.persistentDataPath, name + ".json");
    }

    // Preserve the void entry points for existing UnityEvent/Inspector bindings.
    public static void OnSave() => TrySave(GlobalVariables.saveFileName);
    public static void OnLoad()
    {
        if (!TryLoad(GlobalVariables.saveFileName)) return;
        BattleManager.Instance.RefreshRegimentMarkers();
        GameManager.Instance.dayUIEvent.Invoke();
    }

    public static bool TrySave(string name)
    {
        string temporaryPath = null;
        try
        {
            string path = GetSavePath(name);
            var manager = GameManager.Instance;
            if (manager == null || manager.player == null)
                throw new InvalidOperationException("저장할 게임이 없습니다.");
            SaveDataFormat data = GameSaveState.Capture(manager, BattleManager.Instance);
            string json = JsonUtility.ToJson(data, true);
            // Never replace a usable checkpoint with a file we cannot reconstruct.
            GameSaveState.Restore(JsonUtility.FromJson<SaveDataFormat>(json));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, json);
            // Only replace the last good save once the entire new file is written.
            if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
            else File.Move(temporaryPath, path);
            GlobalVariables.saveFileName = name;
            LastError = null;
            return true;
        }
        catch (Exception exception) { return Fail("저장 실패", exception); }
        finally
        {
            if (temporaryPath != null)
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static bool TryLoad(string name)
    {
        try
        {
            var data = Read(name);
            var manager = GameManager.Instance;
            if (manager == null) throw new InvalidOperationException("게임 관리자가 없습니다.");
            // Build and validate a separate world before replacing live references.
            GameSaveState restored = GameSaveState.Restore(data);
            restored.Apply(manager, BattleManager.Instance);
            GlobalVariables.saveFileName = name;
            LastError = null;
            return true;
        }
        catch (Exception exception) { return Fail("불러오기 실패", exception); }
    }

    public static bool CanLoad(string name)
    {
        try { Read(name); LastError = null; return true; }
        catch (Exception exception) { return Fail("불러오기 실패", exception); }
    }

    public static bool TryGetSummary(string name, out SaveSummary summary)
    {
        summary = default;
        try
        {
            SaveDataFormat data = Read(name);
            UserData player = data.users.FirstOrDefault(user => user.id == data.playerId);
            NationData nation = player == null
                ? null
                : data.nations.FirstOrDefault(item => item.name == player.nation);
            if (player == null || nation == null)
                throw new InvalidDataException("The save does not contain the player's nation.");

            summary = new SaveSummary(nation.name, nation.gdp, data.year);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryDelete(string name)
    {
        try
        {
            string path = GetSavePath(name);
            if (!File.Exists(path))
                throw new FileNotFoundException("Save file was not found.", path);

            File.Delete(path);
            DeleteIfPresent(path + ".bak");
            DeleteIfPresent(path + ".tmp");
            if (GlobalVariables.saveFileName == name)
                GlobalVariables.saveFileName = null;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            return Fail("Delete failed", exception);
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static SaveDataFormat Read(string name)
    {
        string path = GetSavePath(name);
        if (!File.Exists(path)) throw new FileNotFoundException("저장 파일을 찾을 수 없습니다.", path);
        var data = JsonUtility.FromJson<SaveDataFormat>(File.ReadAllText(path));
        if (data == null || data.format != "ProjectNO")
            throw new InvalidDataException("이전 저장 형식에는 경제·건설 상태가 없어 복원할 수 없습니다. 새 게임에서 저장해 주세요.");
        if (data.version == 1) MigrateVersion1To2(data);
        if (data.version != CurrentVersion)
            throw new InvalidDataException($"지원하지 않는 저장 버전입니다: {data.version}");
        GameSaveState.ValidateHeader(data);
        return data;
    }

    private static void MigrateVersion1To2(SaveDataFormat data)
    {
        void MigrateMarket(List<ProductData> market)
        {
            GameSaveState.Require(market != null, "Missing required legacy market.");
            foreach (ProductData product in market)
            {
                GameSaveState.Require(product != null, "Invalid legacy market product.");
                product.requestedDemand = product.demand;
                product.unmetDemand = 0;
                product.lastClearingPrice = product.price;
            }
        }

        GameSaveState.Require(data.nations != null && data.provinces != null,
            "Missing required legacy world collection.");
        foreach (NationData nation in data.nations)
        {
            GameSaveState.Require(nation != null, "Invalid legacy nation.");
            MigrateMarket(nation.market);
        }
        foreach (ProvinceData province in data.provinces)
        {
            GameSaveState.Require(province != null && province.buildings != null, "Invalid legacy province.");
            MigrateMarket(province.market);
            foreach (BuildingData building in province.buildings)
            {
                GameSaveState.Require(building != null, "Invalid legacy building.");
                building.inputInventory ??= new List<AmountData>();
            }
        }
        data.version = CurrentVersion;
    }

    private static bool Fail(string operation, Exception exception)
    {
        LastError = operation + ": " + exception.Message;
        Debug.LogError(LastError);
        SaveErrorDialog.Show(LastError);
        return false;
    }

    [Serializable]
    public sealed class SaveDataFormat
    {
        public string format;
        public int version;
        public int year, month, day, dayOfWeek, speed, nextRegimentId;
        public long employmentWeek;
        public bool paused;
        public int playerId;
        public List<UserData> users = new();
        public List<NationData> nations = new();
        public List<ProvinceData> provinces = new();
        public List<AccountData> accounts = new();
        public List<LedgerData> ledgers = new();
        public List<DiplomacyData> diplomacies = new();
        public List<BattleData> battles = new();
        public List<int> activeRegiments = new();
    }
    [Serializable] public sealed class UserData { public int id; public string nation; }
    [Serializable] public sealed class AmountData { public string key; public long value; }
    [Serializable] public sealed class AccountData { public string id, ledger; public long balance; }
    [Serializable] public sealed class TransactionData
    {
        public MoneyTransactionKind kind;
        public List<string> sources, destinations;
        public long amount;
        public string reason;
    }
    [Serializable] public sealed class LedgerData
    {
        public string id, nation, province, treasury;
        public long supply, weeklyTax;
        public int taxRate;
        public List<TransactionData> transactions = new();
    }
    [Serializable] public sealed class NationData
    {
        public int id;
        public string name, capital;
        public Color32 color;
        public List<string> provinces = new();
        public List<string> research = new();
        public List<BuffData> buffs = new();
        public long gdp, gdpAverage, researchFund;
        public List<long> gdpHistory = new();
        public BudgetData budget;
        public List<ProductData> market;
        public List<RegimentData> regiments = new();
        public List<MandateData> mandates = new();
    }
    [Serializable] public sealed class BuffData { public BuffKind kind; public double value; }
    [Serializable] public sealed class BudgetData
    {
        public long research, military, realEstate;
        public List<AmountData> subsidies = new();
        public float inflation, priceIndex;
        public List<float> priceHistory = new();
    }
    [Serializable] public sealed class ProvinceData
    {
        public int id, road, desolation;
        public string name, nation, ledger, localLedger, localTreasury;
        public Topography topography;
        public bool connected;
        public List<PopData> pops = new();
        public List<BuildingData> buildings = new();
        public List<SpecialBuildingData> specialBuildings = new();
        public List<ProductData> market;
        public long lastPaidWeek;
        public string employmentError;
        public List<EmploymentData> employment = new();
        public List<string> neighbors = new();
    }
    [Serializable] public sealed class PopData
    {
        public string species, culture;
        public long dividend;
        public double livingStandard;
        public List<long> ages = new();
    }
    [Serializable] public sealed class BuildingData
    {
        public string type, owner;
        public int level, previousGain;
        public double manhoursLeft;
        public List<AmountData> inputInventory = new();
        // Free slot positions affect the priority of subsequent projects.
        public List<CompanySlotData> slots = new();
    }
    [Serializable] public sealed class CompanySlotData { public int slot; public string mandate; }
    [Serializable] public sealed class SpecialBuildingData
    {
        public string type;
        public int level;
        public long workers;
        public double manhoursLeft;
    }
    [Serializable] public sealed class EmploymentData { public string building, pop; public long workers; }
    [Serializable] public sealed class ProductData
    {
        public string name;
        public int price, lastPrice, demand, supply, requestedDemand, unmetDemand, lastClearingPrice;
        public float elasticity;
        public List<AmountData> lots = new();
    }
    [Serializable] public sealed class MandateData
    {
        public string id, investor, type, province, escrow, company;
        public long capital, fee, paidFee, materialSpending;
        public int startBasisPoints;
        public double requiredManhours;
        public string completedManhours;
        public ConstructionMandateStatus status;
        public bool procurementFailed;
        public List<AmountData> required, acquired, consumed;
    }
    [Serializable] public sealed class RegimentData
    {
        public int id;
        public string name, location;
        public RegimentState state;
        public List<SquadData> squads = new();
    }
    [Serializable] public sealed class SquadData { public string type; public int capacity, population; }
    [Serializable] public sealed class DiplomacyData { public DiplomacyType type; public List<string> left, right; }
    [Serializable] public sealed class BattleData
    {
        public string province;
        public List<int> attackers, defenders;
        public double winProbability;
    }
}
