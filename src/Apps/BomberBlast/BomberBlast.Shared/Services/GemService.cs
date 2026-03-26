using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistente Gem-Verwaltung via IPreferencesService.
/// Pattern analog zu CoinService.
/// </summary>
public sealed class GemService : IGemService
{
    private const string GEM_DATA_KEY = "GemData";
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private readonly Lazy<IWeeklyChallengeService> _weeklyService;
    private readonly Lazy<IDailyMissionService> _dailyMissionService;
    private GemData _data;

    public int Balance => _data.Balance;
    public int TotalEarned => _data.TotalEarned;

    public event EventHandler? BalanceChanged;

    public GemService(
        IPreferencesService preferences,
        Lazy<IWeeklyChallengeService> weeklyService,
        Lazy<IDailyMissionService> dailyMissionService)
    {
        _preferences = preferences;
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;
        _data = Load();
    }

    public void AddGems(int amount)
    {
        if (amount <= 0) return;

        _data.Balance += amount;
        _data.TotalEarned += amount;
        Save();
        BalanceChanged?.Invoke(this, EventArgs.Empty);

        // Mission-Tracking: Gems verdient
        _weeklyService.Value.TrackProgress(WeeklyMissionType.EarnGems, amount);
        _dailyMissionService.Value.TrackProgress(WeeklyMissionType.EarnGems, amount);
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
