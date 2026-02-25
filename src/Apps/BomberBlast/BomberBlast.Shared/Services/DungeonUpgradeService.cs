using System.Text.Json;
using BomberBlast.Models.Dungeon;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet permanente Dungeon-Upgrades und DungeonCoin-Währung.
/// Persistenz via IPreferencesService (JSON).
/// </summary>
public class DungeonUpgradeService : IDungeonUpgradeService
{
    private const string DATA_KEY = "DungeonUpgradeData";

    private readonly IPreferencesService _preferences;
    private DungeonUpgradeData _data;

    private static readonly JsonSerializerOptions JsonOptions = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public int DungeonCoinBalance => _data.DungeonCoins;
    public event Action? BalanceChanged;

    public DungeonUpgradeService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
    }

    public int GetUpgradeLevel(string upgradeId)
    {
        foreach (var state in _data.Upgrades)
            if (state.Id == upgradeId) return state.Level;
        return 0;
    }

    public bool CanBuyUpgrade(string upgradeId)
    {
        var def = DungeonUpgradeCatalog.Find(upgradeId);
        if (def == null) return false;

        int currentLevel = GetUpgradeLevel(upgradeId);
        if (currentLevel >= def.MaxLevel) return false;

        int cost = def.CostsPerLevel[currentLevel];
        return _data.DungeonCoins >= cost;
    }

    public bool TryBuyUpgrade(string upgradeId)
    {
        var def = DungeonUpgradeCatalog.Find(upgradeId);
        if (def == null) return false;

        int currentLevel = GetUpgradeLevel(upgradeId);
        if (currentLevel >= def.MaxLevel) return false;

        int cost = def.CostsPerLevel[currentLevel];
        if (_data.DungeonCoins < cost) return false;

        _data.DungeonCoins -= cost;

        // Level aktualisieren oder neuen Eintrag erstellen
        var existing = _data.Upgrades.Find(u => u.Id == upgradeId);
        if (existing != null)
        {
            existing.Level = currentLevel + 1;
        }
        else
        {
            _data.Upgrades.Add(new DungeonUpgradeState { Id = upgradeId, Level = 1 });
        }

        Save();
        BalanceChanged?.Invoke();
        return true;
    }

    public void AddDungeonCoins(int amount)
    {
        if (amount <= 0) return;
        _data.DungeonCoins += amount;
        Save();
        BalanceChanged?.Invoke();
    }

    public int GetNextLevelCost(string upgradeId)
    {
        var def = DungeonUpgradeCatalog.Find(upgradeId);
        if (def == null) return 0;

        int currentLevel = GetUpgradeLevel(upgradeId);
        if (currentLevel >= def.MaxLevel) return 0;

        return def.CostsPerLevel[currentLevel];
    }

    public List<(DungeonUpgradeDefinition Definition, int CurrentLevel)> GetAllUpgrades()
    {
        var result = new List<(DungeonUpgradeDefinition, int)>(DungeonUpgradeCatalog.All.Length);
        foreach (var def in DungeonUpgradeCatalog.All)
            result.Add((def, GetUpgradeLevel(def.Id)));
        return result;
    }

    // === Persistenz ===

    private DungeonUpgradeData Load()
    {
        var json = _preferences.Get(DATA_KEY, "");
        if (string.IsNullOrEmpty(json)) return new DungeonUpgradeData();

        try
        {
            return JsonSerializer.Deserialize<DungeonUpgradeData>(json, JsonOptions) ?? new DungeonUpgradeData();
        }
        catch
        {
            return new DungeonUpgradeData();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        _preferences.Set(DATA_KEY, json);
    }
}

/// <summary>
/// Persistierte Daten für DungeonCoins + Upgrade-Zustände
/// </summary>
public class DungeonUpgradeData
{
    public int DungeonCoins { get; set; }
    public List<DungeonUpgradeState> Upgrades { get; set; } = [];
}
