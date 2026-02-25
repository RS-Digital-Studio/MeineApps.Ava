using System.Globalization;
using System.Text.Json;
using BomberBlast.Models.Firebase;
using BomberBlast.Models.League;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Liga-System: Local-First mit Firebase-Sync.
/// - Lokaler State (Preferences) für schnelle Reads
/// - Firebase Realtime Database für echte Online-Rangliste
/// - Deterministische 14-Tage-Saisons (Epoche + Berechnung, kein Firebase nötig)
/// - NPC-Backfill wenn weniger als 20 echte Spieler in der Liga
/// - Debounced Firebase-Push nach Punkteänderungen (3s)
/// </summary>
public class LeagueService : ILeagueService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string DataKey = "LeagueData";
    private const string StatsKey = "LeagueStatsData";
    private const string PlayerNameKey = "LeaguePlayerName";

    // Deterministische Saisons: Alle Clients berechnen die gleiche Saison-Nr
    // Epoche: Montag, 24. Februar 2026 00:00 UTC
    private static readonly DateTime SeasonEpoch = new(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc);
    private const int SeasonDurationDays = 14;
    private const int TargetLeaderboardSize = 20;
    private const int PushDebounceMs = 3000;

    // NPC-Namenpool
    private static readonly string[] NpcNames =
    [
        "BomberMax", "BlastKing42", "ExplosionQueen", "TNT_Master", "DynamiteDan",
        "BombSquad99", "FireStarter", "ChainReaction", "MegaBlast", "BoomBoy",
        "DetonatorX", "PowerBomber", "FlameRunner", "BlastWave", "BombHero",
        "KaBoomKid", "NitroPro", "ShockWave77", "FuseLight", "BlazeStorm",
        "BombRanger", "ExplodeX", "BlastPhoenix", "NovaStrike", "ThunderBomb"
    ];

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localization;
    private readonly IFirebaseService _firebase;
    private IAchievementService? _achievementService;

    private LeagueData _data;
    private LeagueStats _stats;

    // Firebase-Cache: Echte Spieler in der aktuellen Liga
    private List<LeagueLeaderboardEntry> _cachedOnlineEntries = [];
    private CancellationTokenSource? _pushDebounce;
    private bool _isLoading;

    public event EventHandler? PointsChanged;
    public event EventHandler? SeasonEnded;
    public event EventHandler? LeaderboardUpdated;

    /// <summary>Lazy-Injection um zirkuläre DI-Abhängigkeit zu vermeiden.</summary>
    public void SetAchievementService(IAchievementService achievementService) => _achievementService = achievementService;

    public LeagueTier CurrentTier => _data.CurrentTier;
    public int CurrentPoints => _data.Points;
    public int SeasonNumber => GetDeterministicSeasonNumber();
    public bool IsSeasonRewardClaimed => _data.SeasonRewardClaimed;
    public bool IsOnline => _firebase.IsOnline;
    public bool IsLoading => _isLoading;

    public string PlayerName
    {
        get
        {
            var name = _preferences.Get<string>(PlayerNameKey, "");
            if (string.IsNullOrEmpty(name))
            {
                // Standard-Name: "Bomber" + letzte 4 Zeichen der Firebase-UID
                var uid = _firebase.Uid ?? "";
                var suffix = uid.Length >= 4 ? uid[^4..] : "????";
                name = $"Bomber_{suffix}";
            }
            return name;
        }
    }

    public LeagueService(
        IPreferencesService preferences,
        ICoinService coinService,
        IGemService gemService,
        ILocalizationService localization,
        IFirebaseService firebase)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _localization = localization;
        _firebase = firebase;

        _data = LoadData();
        _stats = LoadStats();

        // Saison-Migration: Von alter lokaler Saison-Verwaltung auf deterministisch
        SyncSeasonNumber();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DETERMINISTISCHE SAISONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet die aktuelle Saison-Nummer deterministisch aus der Epoche.
    /// Alle Clients bekommen die gleiche Nummer → gleicher Firebase-Pfad.
    /// </summary>
    private static int GetDeterministicSeasonNumber()
    {
        var now = DateTime.UtcNow;
        if (now < SeasonEpoch) return 1;
        var daysSinceEpoch = (now - SeasonEpoch).TotalDays;
        return (int)(daysSinceEpoch / SeasonDurationDays) + 1;
    }

    /// <summary>Start-Datum der aktuellen Saison.</summary>
    private static DateTime GetCurrentSeasonStart()
    {
        var seasonNumber = GetDeterministicSeasonNumber();
        return SeasonEpoch.AddDays((seasonNumber - 1) * SeasonDurationDays);
    }

    /// <summary>Synct lokale Saison-Nummer mit deterministischer Berechnung.</summary>
    private void SyncSeasonNumber()
    {
        var currentSeason = GetDeterministicSeasonNumber();
        if (_data.SeasonNumber != currentSeason)
        {
            // Saison hat sich geändert → Saisonende verarbeiten
            if (_data.SeasonNumber > 0 && _data.SeasonNumber < currentSeason)
            {
                ProcessSeasonEnd();
            }
            _data.SeasonNumber = currentSeason;
            SaveData();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIELERNAME
    // ═══════════════════════════════════════════════════════════════════════

    public void SetPlayerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        // Auf 16 Zeichen begrenzen, Whitespace trimmen
        name = name.Trim();
        if (name.Length > 16) name = name[..16];
        _preferences.Set(PlayerNameKey, name);

        // Firebase-Eintrag aktualisieren
        ScheduleFirebasePush();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUNKTE
    // ═══════════════════════════════════════════════════════════════════════

    public void AddPoints(int amount)
    {
        if (amount <= 0) return;

        _data.Points += amount;
        _stats.TotalPointsEarned += amount;

        if (_data.Points > _stats.BestSeasonPoints)
            _stats.BestSeasonPoints = _data.Points;

        SaveData();
        SaveStats();
        PointsChanged?.Invoke(this, EventArgs.Empty);

        // Firebase-Push mit Debounce (3s)
        ScheduleFirebasePush();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RANGLISTE
    // ═══════════════════════════════════════════════════════════════════════

    public IReadOnlyList<LeagueLeaderboardEntry> GetLeaderboard()
    {
        var entries = new List<LeagueLeaderboardEntry>();

        // 1. Echte Spieler aus Firebase-Cache (ohne den eigenen Eintrag)
        var uid = _firebase.Uid ?? "";
        foreach (var entry in _cachedOnlineEntries)
        {
            // Eigenen Eintrag aus dem Cache überspringen (wird separat hinzugefügt)
            if (entry.IsPlayer) continue;
            entries.Add(entry);
        }

        // 2. Spieler selbst hinzufügen
        entries.Add(new LeagueLeaderboardEntry
        {
            Name = PlayerName,
            Points = _data.Points,
            IsPlayer = true,
            IsRealPlayer = true
        });

        // 3. NPC-Backfill bis 20 Einträge
        int realCount = entries.Count;
        if (realCount < TargetLeaderboardSize)
        {
            var npcs = GenerateNpcs(TargetLeaderboardSize - realCount);
            entries.AddRange(npcs);
        }

        // 4. Sortieren (absteigend) und Ränge vergeben
        entries = entries.OrderByDescending(e => e.Points).ThenBy(e => e.IsPlayer ? 0 : 1).ToList();
        for (int i = 0; i < entries.Count; i++)
            entries[i].Rank = i + 1;

        return entries;
    }

    public int GetPlayerRank()
    {
        var leaderboard = GetLeaderboard();
        var player = leaderboard.FirstOrDefault(e => e.IsPlayer);
        return player?.Rank ?? leaderboard.Count;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FIREBASE SYNC
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Initialer Firebase-Sync: Auth + eigene Daten pushen + Rangliste laden.</summary>
    public async Task InitializeOnlineAsync()
    {
        try
        {
            _isLoading = true;
            await _firebase.EnsureAuthenticatedAsync();

            // Eigenen Score sofort pushen
            await PushScoreToFirebaseAsync();

            // Rangliste laden
            await RefreshLeaderboardAsync();
        }
        catch
        {
            // Offline → kein Problem, lokale Daten funktionieren
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Rangliste von Firebase aktualisieren.</summary>
    public async Task RefreshLeaderboardAsync()
    {
        try
        {
            _isLoading = true;
            var seasonPath = GetSeasonTierPath();
            var entries = await _firebase.GetAsync<Dictionary<string, FirebaseLeagueEntry>>(seasonPath);

            if (entries != null)
            {
                var uid = _firebase.Uid ?? "";
                _cachedOnlineEntries.Clear();

                foreach (var (entryUid, entry) in entries)
                {
                    bool isPlayer = entryUid == uid;
                    _cachedOnlineEntries.Add(new LeagueLeaderboardEntry
                    {
                        Name = entry.Name,
                        Points = entry.Points,
                        IsPlayer = isPlayer,
                        IsRealPlayer = true
                    });
                }
            }

            LeaderboardUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Offline → Cache behalten
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Eigenen Score nach Firebase pushen.</summary>
    private async Task PushScoreToFirebaseAsync()
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            var entry = new FirebaseLeagueEntry
            {
                Name = PlayerName,
                Points = _data.Points,
                UpdatedUtc = DateTime.UtcNow.ToString("O")
            };

            var path = $"{GetSeasonTierPath()}/{uid}";
            await _firebase.SetAsync(path, entry);
        }
        catch
        {
            // Netzwerkfehler → nächster Push-Versuch beim nächsten AddPoints
        }
    }

    /// <summary>Debounced Firebase-Push: Wartet 3s nach letztem AddPoints, dann pusht.</summary>
    private void ScheduleFirebasePush()
    {
        _pushDebounce?.Cancel();
        _pushDebounce?.Dispose();
        _pushDebounce = new CancellationTokenSource();
        var token = _pushDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(PushDebounceMs, token);
                if (!token.IsCancellationRequested)
                {
                    await PushScoreToFirebaseAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // Debounce abgebrochen → neuer Push kommt
            }
        });
    }

    /// <summary>Firebase-Pfad für die aktuelle Tier+Saison.</summary>
    private string GetSeasonTierPath()
    {
        var season = GetDeterministicSeasonNumber();
        var tierName = _data.CurrentTier.ToString().ToLowerInvariant();
        return $"league/s{season}/{tierName}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SAISON
    // ═══════════════════════════════════════════════════════════════════════

    public TimeSpan GetSeasonTimeRemaining()
    {
        var seasonStart = GetCurrentSeasonStart();
        var end = seasonStart.AddDays(SeasonDurationDays);
        var remaining = end - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public bool CheckAndProcessSeasonEnd()
    {
        var currentSeason = GetDeterministicSeasonNumber();
        if (_data.SeasonNumber == currentSeason)
            return false;

        // Saison ist abgelaufen → verarbeiten
        ProcessSeasonEnd();
        _data.SeasonNumber = currentSeason;
        _data.Points = 0;
        _data.SeasonRewardClaimed = false;
        SaveData();

        SeasonEnded?.Invoke(this, EventArgs.Empty);

        // Neuen Score (0) nach Firebase pushen
        ScheduleFirebasePush();

        return true;
    }

    public bool ClaimSeasonReward()
    {
        if (_data.SeasonRewardClaimed)
            return false;

        var (coins, gems) = _data.CurrentTier.GetSeasonReward();

        if (coins > 0) _coinService.AddCoins(coins);
        if (gems > 0) _gemService.AddGems(gems);

        _data.SeasonRewardClaimed = true;
        SaveData();
        return true;
    }

    public LeagueStats GetStats() => _stats;

    // ═══════════════════════════════════════════════════════════════════════
    // SAISON-VERARBEITUNG
    // ═══════════════════════════════════════════════════════════════════════

    private void ProcessSeasonEnd()
    {
        // Rang bestimmen (aus gecachtem Leaderboard)
        int rank = GetPlayerRank();
        var leaderboard = GetLeaderboard();
        int totalPlayers = leaderboard.Count;

        float percentile = totalPlayers > 0 ? (float)rank / totalPlayers : 1f;

        // Auf-/Abstieg bestimmen
        var promotionPercent = _data.CurrentTier.GetPromotionPercent();
        var relegationPercent = _data.CurrentTier.GetRelegationPercent();

        LeagueTier newTier = _data.CurrentTier;

        // Top 30% → Aufstieg
        if (promotionPercent > 0 && percentile <= promotionPercent && _data.CurrentTier < LeagueTier.Diamond)
        {
            newTier = _data.CurrentTier + 1;
            _stats.TotalPromotions++;
        }
        // Bottom 20% → Abstieg
        else if (relegationPercent > 0 && percentile > (1f - relegationPercent))
        {
            newTier = _data.CurrentTier - 1;
        }

        // Höchster Rang tracken
        if ((int)newTier > _stats.HighestTier)
            _stats.HighestTier = (int)newTier;

        // Achievement: Liga-Tier prüfen
        _achievementService?.OnLeagueTierReached((int)newTier);

        _stats.TotalSeasons++;
        SaveStats();

        _data.CurrentTier = newTier;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NPC-GENERIERUNG (Backfill für leere Ligen)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generiert NPCs um die Rangliste auf die Zielgröße aufzufüllen.
    /// Seeded Random für konsistente NPCs pro Saison+Tier.
    /// </summary>
    private List<LeagueLeaderboardEntry> GenerateNpcs(int count)
    {
        if (count <= 0) return [];

        var season = GetDeterministicSeasonNumber();
        var rng = new Random(season * 1000 + (int)_data.CurrentTier * 100);
        var npcs = new List<LeagueLeaderboardEntry>();

        int daysPassed = Math.Max(0, (int)(DateTime.UtcNow - GetCurrentSeasonStart()).TotalDays);

        // Liga-basierte Score-Bereiche
        int tierBase = _data.CurrentTier.GetPromotionThreshold();
        int minGrowth = 5 + (int)_data.CurrentTier * 3;
        int maxGrowth = 25 + (int)_data.CurrentTier * 8;

        for (int i = 0; i < count && i < NpcNames.Length; i++)
        {
            double factor = 0.1 + (i / (double)count) * 0.9;
            int baseScore = (int)(tierBase * 0.3 * factor) + rng.Next(0, 50);
            int dailyGrowth = rng.Next(minGrowth, maxGrowth + 1);
            int npcScore = Math.Max(0, baseScore + dailyGrowth * daysPassed);

            string name = NpcNames[i % NpcNames.Length];
            if (i >= NpcNames.Length)
                name += rng.Next(10, 99).ToString();

            npcs.Add(new LeagueLeaderboardEntry
            {
                Name = name,
                Points = npcScore,
                IsPlayer = false,
                IsRealPlayer = false
            });
        }

        return npcs;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PERSISTENZ
    // ═══════════════════════════════════════════════════════════════════════

    private LeagueData LoadData()
    {
        var json = _preferences.Get<string>(DataKey, "");
        if (string.IsNullOrEmpty(json)) return new LeagueData();
        try { return JsonSerializer.Deserialize<LeagueData>(json, JsonOptions) ?? new LeagueData(); }
        catch { return new LeagueData(); }
    }

    private void SaveData()
    {
        _preferences.Set(DataKey, JsonSerializer.Serialize(_data, JsonOptions));
    }

    private LeagueStats LoadStats()
    {
        var json = _preferences.Get<string>(StatsKey, "");
        if (string.IsNullOrEmpty(json)) return new LeagueStats();
        try { return JsonSerializer.Deserialize<LeagueStats>(json, JsonOptions) ?? new LeagueStats(); }
        catch { return new LeagueStats(); }
    }

    private void SaveStats()
    {
        _preferences.Set(StatsKey, JsonSerializer.Serialize(_stats, JsonOptions));
    }

    public void Dispose()
    {
        _pushDebounce?.Cancel();
        _pushDebounce?.Dispose();
        _pushDebounce = null;
    }
}
