using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveManager
{
    public const int CurrentVersion = 1;
    public static string LastError { get; private set; }

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

    private static SaveDataFormat Read(string name)
    {
        string path = GetSavePath(name);
        if (!File.Exists(path)) throw new FileNotFoundException("저장 파일을 찾을 수 없습니다.", path);
        var data = JsonUtility.FromJson<SaveDataFormat>(File.ReadAllText(path));
        if (data == null || data.format != "ProjectNO")
            throw new InvalidDataException("이전 저장 형식에는 경제·건설 상태가 없어 복원할 수 없습니다. 새 게임에서 저장해 주세요.");
        if (data.version != CurrentVersion)
            throw new InvalidDataException($"지원하지 않는 저장 버전입니다: {data.version}");
        GameSaveState.ValidateHeader(data);
        return data;
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
        public int price, lastPrice, demand, supply;
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
