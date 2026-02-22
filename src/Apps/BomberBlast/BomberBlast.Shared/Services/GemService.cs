using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistente Gem-Verwaltung via IPreferencesService.
/// Pattern analog zu CoinService.
/// </summary>
public class GemService : IGemService
{
    private const string GEM_DATA_KEY = "GemData";
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private GemData _data;

    // Lazy-Injection für Mission-Tracking (vermeidet zirkuläre DI)
    private IWeeklyChallengeService? _weeklyService;
    private IDailyMissionService? _dailyMissionService;

    public int Balance => _data.Balance;
    public int TotalEarned => _data.TotalEarned;

    public event EventHandler? BalanceChanged;

    public GemService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
    }

    /// <summary>Lazy-Injection für Mission-Tracking (Phase 9.4)</summary>
    public void SetMissionServices(IWeeklyChallengeService weeklyService, IDailyMissionService dailyMissionService)
    {
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;
    }

    public void AddGems(int amount)
    {
        if (amount <= 0) return;

        _data.Balance += amount;
        _data.TotalEarned += amount;
        Save();
        BalanceChanged?.Invoke(this, EventArgs.Empty);

        // Mission-Tracking: Gems verdient
        _weeklyService?.TrackProgress(WeeklyMissionType.EarnGems, amount);
        _dailyMissionService?.TrackProgress(WeeklyMissionType.EarnGems, amount);
    }

    public bool TrySpendGems(int amount)
    {
        if (amount <= 0 || _data.Balance < amount)
            return false;

        _data.Balance -= amount;
        Save();
        BalanceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool CanAfford(int amount)
    {
        return _data.Balance >= amount;
    }

    private GemData Load()
    {
        try
        {
            string json = _preferences.Get<string>(GEM_DATA_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<GemData>(json, JsonOptions) ?? new GemData();
            }
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new GemData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(GEM_DATA_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class GemData
    {
        public int Balance { get; set; }
        public int TotalEarned { get; set; }
    }
}
