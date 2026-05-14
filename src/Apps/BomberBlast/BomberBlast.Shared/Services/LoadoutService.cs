using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistente Loadout-Verwaltung pro Story-Level (v2.0.41, Plan Task 3.2).
/// JSON in IPreferencesService, Key "LoadoutData".
/// </summary>
public sealed class LoadoutService : ILoadoutService
{
    private const string PREFS_KEY = "LoadoutData";
    private const int MAX_BOOSTS_PER_LEVEL = 2;

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private LoadoutData _data;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LoadoutService(IPreferencesService preferences, ICoinService coinService, IGemService gemService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _data = Load();
    }

    public IReadOnlyList<LoadoutBoost> GetSavedLoadout(int level)
    {
        return _data.Loadouts.TryGetValue(level, out var list) ? list : [];
    }

    public void SaveLoadout(int level, IReadOnlyList<LoadoutBoost> boosts)
    {
        if (boosts.Count == 0)
        {
            _data.Loadouts.Remove(level);
        }
        else
        {
            // Max 2 Boosts erzwingen
            var limited = boosts.Take(MAX_BOOSTS_PER_LEVEL).ToList();
            _data.Loadouts[level] = limited;
        }
        Save();
    }

    public void ClearLoadout(int level)
    {
        if (_data.Loadouts.Remove(level))
            Save();
    }

    public int GetCoinCost(LoadoutBoostType type) => type switch
    {
        LoadoutBoostType.ExtraBomb => 300,
        LoadoutBoostType.ExtraFire => 300,
        LoadoutBoostType.SpeedBoost => 200,
        LoadoutBoostType.Wallpass => 800,
        LoadoutBoostType.Invincibility => 1500,
        _ => 0
    };

    public int GetGemCost(LoadoutBoostType type) => type switch
    {
        LoadoutBoostType.ExtraBomb => 2,
        LoadoutBoostType.ExtraFire => 2,
        LoadoutBoostType.SpeedBoost => 1,
        LoadoutBoostType.Wallpass => 4,
        LoadoutBoostType.Invincibility => 8,
        _ => 0
    };

    public IReadOnlyList<LoadoutBoost>? Purchase(int level, IReadOnlyList<LoadoutBoostType> boosts, bool useGems)
    {
        if (boosts.Count == 0 || boosts.Count > MAX_BOOSTS_PER_LEVEL)
            return null;

        // Gesamt-Kosten berechnen
        int totalCoins = 0;
        int totalGems = 0;
        foreach (var t in boosts)
        {
            if (useGems) totalGems += GetGemCost(t);
            else totalCoins += GetCoinCost(t);
        }

        // Pre-Check + atomare Buchung. CoinService/GemService haben CanAfford → TrySpend pattern.
        if (useGems)
        {
            if (!_gemService.CanAfford(totalGems)) return null;
            if (!_gemService.TrySpendGems(totalGems)) return null;
        }
        else
        {
            if (!_coinService.CanAfford(totalCoins)) return null;
            if (!_coinService.TrySpendCoins(totalCoins)) return null;
        }

        // Loadout speichern. Bei Save-Failure Coin/Gem-Refund
        // damit Spieler nicht waehrungsverlust hat ohne Loadout-Persistenz.
        var loadoutList = boosts.Select(t => new LoadoutBoost { Type = t, PaidWithGems = useGems }).ToList();
        if (!TrySaveLoadout(level, loadoutList))
        {
            // Refund: Coins/Gems zurueckgeben
            if (useGems) _gemService.AddGems(totalGems);
            else _coinService.AddCoins(totalCoins);
            return null;
        }
        return loadoutList;
    }

    /// <summary>
    /// Speichert das Loadout und gibt zurueck ob die Persistenz erfolgreich war.
    /// Wird von Purchase() fuer Refund-Logik genutzt.
    /// </summary>
    private bool TrySaveLoadout(int level, List<LoadoutBoost> boosts)
    {
        try
        {
            // Max 2 Boosts erzwingen + speichern (analog zu SaveLoadout, aber mit Erfolgs-Return)
            var limited = boosts.Take(MAX_BOOSTS_PER_LEVEL).ToList();
            _data.Loadouts[level] = limited;
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(PREFS_KEY, json);
            return true;
        }
        catch (Exception ex)
        {
            // Rollback In-Memory-State + Corruption melden
            _data.Loadouts.Remove(level);
            PersistenceHealth.ReportCorruption(nameof(LoadoutService), ex);
            return false;
        }
    }

    private LoadoutData Load()
    {
        var json = _preferences.Get(PREFS_KEY, "");
        if (string.IsNullOrEmpty(json))
            return new LoadoutData();

        try
        {
            return JsonSerializer.Deserialize<LoadoutData>(json, JsonOptions) ?? new LoadoutData();
        }
        catch (Exception ex)
        {
            PersistenceHealth.ReportCorruption(nameof(LoadoutService), ex);
            return new LoadoutData();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(PREFS_KEY, json);
        }
        catch
        {
            // Save-Fehler werden beim naechsten Save erneut versucht (Loadout wird wieder gesetzt).
        }
    }

    private class LoadoutData
    {
        public Dictionary<int, List<LoadoutBoost>> Loadouts { get; set; } = new();
    }
}
