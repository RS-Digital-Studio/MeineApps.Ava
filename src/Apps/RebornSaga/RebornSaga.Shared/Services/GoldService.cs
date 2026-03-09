namespace RebornSaga.Services;

using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using RebornSaga.Models;
using System;
using System.Globalization;
using System.Threading.Tasks;

/// <summary>
/// Verwaltet Gold-Economy: Quellen (Kampf, Video-Ads), Senken (Shop, Kapitel-Freischaltung).
/// Rewarded-Video-Cooldown: max. 3 pro Tag, je 500 Gold.
/// </summary>
public class GoldService
{
    private const int GoldPerVideo = 500;
    private const int MaxDailyVideos = 3;
    private const string PrefKeyDailyWatches = "gold_daily_watches";
    private const string PrefKeyLastWatchDate = "gold_last_watch_date";

    private readonly IPreferencesService _preferences;
    private readonly IRewardedAdService _adService;

    /// <summary>
    /// Event wenn sich das Gold ändert (alter Wert, neuer Wert).
    /// </summary>
    public event Action<int, int>? GoldChanged;

    /// <summary>
    /// Anzahl der heute noch verfügbaren Video-Belohnungen.
    /// </summary>
    public int DailyVideoWatchesRemaining { get; private set; } = MaxDailyVideos;

    public GoldService(IPreferencesService preferences, IRewardedAdService adService)
    {
        _preferences = preferences;
        _adService = adService;
        ResetDailyCountersIfNeeded();
    }

    /// <summary>
    /// Maximales Gold-Limit (verhindert Overflow und absurde Werte).
    /// </summary>
    public const int MaxGold = 9_999_999;

    /// <summary>
    /// Fügt Gold hinzu (Kampf-Belohnung, Verkauf etc.).
    /// Clamp auf [0, MaxGold] verhindert Overflow.
    /// </summary>
    public void AddGold(Player player, int amount)
    {
        if (amount <= 0) return;
        var old = player.Gold;
        player.Gold = Math.Min(player.Gold + amount, MaxGold);
        GoldChanged?.Invoke(old, player.Gold);
    }

    /// <summary>
    /// Gibt Gold aus. Gibt false zurück wenn nicht genug vorhanden.
    /// Clamp auf 0 als Sicherheitsnetz.
    /// </summary>
    public bool SpendGold(Player player, int amount)
    {
        if (amount <= 0 || player.Gold < amount) return false;
        var old = player.Gold;
        player.Gold = Math.Max(player.Gold - amount, 0);
        GoldChanged?.Invoke(old, player.Gold);
        return true;
    }

    /// <summary>
    /// Zeigt ein Rewarded Video und gibt 500 Gold bei Erfolg.
    /// Gibt true zurück wenn die Belohnung vergeben wurde.
    /// </summary>
    public async Task<bool> WatchVideoForGoldAsync(Player player)
    {
        ResetDailyCountersIfNeeded();

        if (DailyVideoWatchesRemaining <= 0)
            return false;

        var result = await _adService.ShowAdAsync("gold_bonus");
        if (!result) return false;

        // Belohnung vergeben
        DailyVideoWatchesRemaining--;
        SaveDailyWatches();
        AddGold(player, GoldPerVideo);

        return true;
    }

    /// <summary>
    /// Prüft ob Video-Belohnungen verfügbar sind.
    /// </summary>
    public bool CanWatchVideo()
    {
        ResetDailyCountersIfNeeded();
        return DailyVideoWatchesRemaining > 0;
    }

    /// <summary>
    /// Gold-Betrag pro Video.
    /// </summary>
    public static int VideoReward => GoldPerVideo;

    /// <summary>
    /// Setzt die täglichen Zähler zurück wenn ein neuer Tag begonnen hat.
    /// </summary>
    private void ResetDailyCountersIfNeeded()
    {
        var lastDateStr = _preferences.Get(PrefKeyLastWatchDate, "");
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (lastDateStr != today)
        {
            DailyVideoWatchesRemaining = MaxDailyVideos;
            _preferences.Set(PrefKeyLastWatchDate, today);
            _preferences.Set(PrefKeyDailyWatches, 0);
        }
        else
        {
            var watched = _preferences.Get(PrefKeyDailyWatches, 0);
            DailyVideoWatchesRemaining = Math.Max(0, MaxDailyVideos - watched);
        }
    }

    private void SaveDailyWatches()
    {
        var watched = MaxDailyVideos - DailyVideoWatchesRemaining;
        _preferences.Set(PrefKeyDailyWatches, watched);
    }
}
