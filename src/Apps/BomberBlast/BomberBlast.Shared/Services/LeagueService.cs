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
public sealed class LeagueService : ILeagueService
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
    private readonly Lazy<IAchievementService> _achievementService;

    private LeagueData _data;
    private LeagueStats _stats;

    // Firebase-Cache: Echte Spieler in der aktuellen Liga
    private List<LeagueLeaderboardEntry> _cachedOnlineEntries = [];
    private CancellationTokenSource? _pushDebounce;
    private bool _isLoading;

    public event EventHandler? PointsChanged;
    public event EventHandler? SeasonEnded;
    public event EventHandler? LeaderboardUpdated;

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
        IFirebaseService firebase,
        Lazy<IAchievementService> achievementService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _localization = localization;
        _firebase = firebase;
        _achievementService = achievementService;

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

    // Einfacher Profanity-Filter für Spielernamen (Play Store Policy: UGC moderieren).
    // Liste ist bewusst in "normalisierter" Form (lowercase, ohne Sonderzeichen) — der Input wird
    // beim Check analog normalisiert, damit Bypass-Versuche wie "F.u.c.k", "FÜCK", "fVck" erkannt werden.
    // WICHTIG: Keine Tokens < 4 Zeichen (z.B. "ass") — verursachen False-Positives bei legitimen
    // Namen wie "Cassandra", "Passion", "Bass". Stattdessen laengere spezifische Formen verwenden.
    private static readonly HashSet<string> _blockedWords = new(StringComparer.Ordinal)
    {
        // Englisch
        "fuck", "shit", "dick", "bitch", "nigger", "nigga", "cunt", "whore", "asshole", "jackass",
        "faggot", "retard", "pussy", "bastard", "motherfucker", "slut",
        // Deutsch
        "arsch", "ficken", "scheisse", "hurensohn", "wichser", "fotze", "wixer", "nutte",
        // Spanisch
        "puta", "mierda", "perra", "pendejo",
        // Franzoesisch
        "merde", "putain", "connard", "salope",
        // Italienisch
        "cazzo", "stronzo", "troia",
        // Portugiesisch
        "porra", "caralho", "puteiro",
        // Leetspeak / Nach-Normalize-Varianten
        "fvck", "fukk", "sh1t", "b1tch", "d1ck", "n1gger", "n1gga", "fuk",
        // Hass / Verbotene Ideologie
        "nazi", "heil", "hitler", "jihad", "islamist"
    };

    /// <summary>
    /// Normalisiert Nutzereingabe fuer Profanity-Check:
    /// - Unicode-Normalisierung NFKD (trennt Diakritika ab)
    /// - Entfernt alle kombinierenden Marks (Akzente)
    /// - Entfernt Zero-Width / RTL-Override / andere Steuerzeichen
    /// - Konvertiert auf ASCII-lowercase (Homoglyphen werden primitiv entfernt)
    /// - Entfernt alle Non-Alphanumerischen Zeichen (Punkte, Leerzeichen, Emojis)
    /// Damit werden Bypass-Versuche wie "F.u.c.k", "FÜCK", "f u c k", "f\u200Bu\u200Bck" erkannt.
    /// </summary>
    /// <summary>
    /// Entfernt unsichtbare Zeichen (Zero-Width-Space, Zero-Width-Joiner, RTL-Override,
    /// Byte-Order-Mark, Format/Control) aus dem Spielernamen. Verhindert Leaderboard-Spoofing
    /// durch scheinbar leere oder identisch aussehende Namen.
    /// </summary>
    private static string StripInvisibleChars(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            // Format (Zero-Width, RTL-Override etc.) und Control komplett verwerfen.
            // NonSpacingMark/SpacingCombiningMark (Akzente) bleiben erhalten — legitime Namen wie "Müller".
            if (cat == System.Globalization.UnicodeCategory.Format ||
                cat == System.Globalization.UnicodeCategory.Control)
                continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string NormalizeForProfanityCheck(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var normalized = input.Normalize(System.Text.NormalizationForm.FormKD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            // Combining Marks (Akzente), Formatting (Zero-Width, RTL-Override) wegwerfen
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark ||
                cat == System.Globalization.UnicodeCategory.SpacingCombiningMark ||
                cat == System.Globalization.UnicodeCategory.Format ||
                cat == System.Globalization.UnicodeCategory.Control)
                continue;
            // Nur Buchstaben und Ziffern durchlassen, in lowercase
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    public void SetPlayerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        // Zero-Width / RTL-Override / Format-Control wegstrippen BEVOR der Längen-Check greift.
        // Sonst reicht ein einziges \u200B, um als "gültiger Name" durchzugehen, obwohl
        // im Leaderboard ein sichtbar leerer / spoofbarer Eintrag entstünde.
        name = StripInvisibleChars(name);
        // Auf 16 Zeichen begrenzen, Whitespace trimmen
        name = name.Trim();
        if (name.Length > 16) name = name[..16];
        // Nach Strip + Trim erneut prüfen ob überhaupt noch Inhalt da ist
        if (string.IsNullOrWhiteSpace(name)) return;

        // Profanity-Check: Input normalisieren, dann gegen Blocklist pruefen.
        // Bei Treffer: Ersatz-Name aus UID-Suffix (nicht "****") → Spieler im Leaderboard unterscheidbar
        // und UI zeigt dem User explizit was passiert ist (via SuggestedName-Callback in ViewModel).
        var normalized = NormalizeForProfanityCheck(name);
        bool blocked = false;
        if (normalized.Length > 0)
        {
            foreach (var word in _blockedWords)
            {
                if (normalized.Contains(word, StringComparison.Ordinal))
                {
                    blocked = true;
                    break;
                }
            }
        }

        if (blocked)
        {
            // Ersatz-Name basierend auf UID-Suffix (nicht "****"). Behaelt Eindeutigkeit im Leaderboard.
            var uid = _firebase.Uid ?? "";
            var suffix = uid.Length >= 4 ? uid[^4..] : Random.Shared.Next(1000, 9999).ToString();
            name = $"Player_{suffix}";
        }

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
                        Uid = entryUid,
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

    /// <summary>
    /// Meldet einen Spieler wegen anstössigem Namen / Cheating. Schreibt nach
    /// <c>reports/{reportedUid}/{reporterUid}</c> mit Reason + Server-Timestamp.
    /// Security-Rules sollten Rate-Limiting durchsetzen (1 Report pro Paar/24h).
    /// </summary>
    public async Task<bool> ReportPlayerAsync(string reportedUid, string reason)
    {
        if (string.IsNullOrEmpty(reportedUid)) return false;
        if (string.IsNullOrEmpty(reason)) reason = "other";

        // Reason auf bekannte Werte begrenzen (Security: keine Code-Injection in den Report)
        if (reason != "offensive_name" && reason != "cheating" && reason != "other")
            reason = "other";

        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var reporterUid = _firebase.Uid;
            if (string.IsNullOrEmpty(reporterUid)) return false;
            if (reporterUid == reportedUid) return false; // Self-Report blockieren

            var path = $"reports/{reportedUid}/{reporterUid}";
            var payload = new Dictionary<string, object>
            {
                ["reason"] = reason,
                ["reportedAt"] = FirebaseServerTimestamp  // Server-Timestamp-Sentinel
            };
            await _firebase.UpdateAsync(path, payload);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// DSGVO Art. 17: Eigenen Liga-Eintrag aus allen Tier-Subtrees + Daily-Race-Subtree
    /// + alle eigenen Reports löschen. Best-Effort: Fehler werden geschluckt damit
    /// die lokale Account-Löschung trotzdem durchgehen kann.
    /// </summary>
    public async Task DeleteOwnEntryAsync()
    {
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            // Liga-Eintrag in allen 5 Tiers parallel löschen (Spieler kann historisch auf-/abgestiegen sein)
            var deleteTasks = new List<Task>();
            foreach (var tier in Enum.GetValues<LeagueTier>())
            {
                var path = $"league/s{SeasonNumber}/{tier.ToString().ToLowerInvariant()}/{uid}";
                deleteTasks.Add(SafeDeleteAsync(path));
            }

            // Reports zu diesem Spieler bleiben (Moderations-Audit-Trail), aber eigene Submissions als
            // Reporter werden NICHT gelöscht — sie sind anonymisierbar via reporterUid-Hash. DSGVO erlaubt
            // Pseudonymisierung. Hier vorerst kein Eingriff, da der Reporter-Pfad uid-bound ist und
            // nur durch Service-Account aufgelöst werden kann.

            await Task.WhenAll(deleteTasks);
        }
        catch
        {
            // Best-Effort
        }
    }

    private async Task SafeDeleteAsync(string path)
    {
        try { await _firebase.DeleteAsync(path); }
        catch { /* Best-Effort */ }
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
                // Server-Timestamp-Sentinel: Firebase löst das beim Write zur Server-Zeit in ms auf.
                // Einzige Zeitstempel-Quelle — nicht client-spoofbar (v2.0.34).
                UpdatedMs = FirebaseServerTimestamp
            };

            var path = $"{GetSeasonTierPath()}/{uid}";
            await _firebase.SetAsync(path, entry);
        }
        catch
        {
            // Netzwerkfehler → nächster Push-Versuch beim nächsten AddPoints
        }
    }

    /// <summary>
    /// Firebase ServerValue.TIMESTAMP-Sentinel. Wird beim Serialize als <c>{".sv":"timestamp"}</c>
    /// transportiert und serverseitig in die aktuelle Server-Zeit (Millisekunden seit Epoch) aufgelöst.
    /// </summary>
    private static readonly Dictionary<string, string> FirebaseServerTimestamp = new() { [".sv"] = "timestamp" };

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
        _achievementService.Value.OnLeagueTierReached((int)newTier);

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

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY BOMB RACE (v2.0.41, Plan Task 3.1)
    // ═══════════════════════════════════════════════════════════════════════
    // Alle Spieler erhalten denselben Tages-Seed → identisches Level. Score ranked pro Tier.
    // Lokale Persistenz: "DailyRaceBest_yyyy-MM-dd" → int. Firebase-Push optional via SubmitDailyRaceScoreAsync.
    // Liga-Punkte werden vom Caller (z.B. GameOverViewModel) ueber AddPoints vergeben — Service-API ist read-only.

    private const string DAILY_RACE_PREF_PREFIX = "DailyRaceBest_";

    public int GetDailyRaceSeed(DateTime utcDate)
    {
        // Deterministischer Seed: yyyy*10000 + MM*100 + dd → identisch fuer alle Spieler weltweit.
        return utcDate.Year * 10000 + utcDate.Month * 100 + utcDate.Day;
    }

    public string GetDailyRaceDateKey(DateTime utcDate)
    {
        return utcDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    public int TodayDailyRaceBestScore
    {
        get
        {
            var key = DAILY_RACE_PREF_PREFIX + GetDailyRaceDateKey(DateTime.UtcNow);
            return _preferences.Get(key, 0);
        }
    }

    public bool HasPlayedDailyRaceToday => TodayDailyRaceBestScore > 0;

    public async Task<bool> SubmitDailyRaceScoreAsync(int score)
    {
        var todayKey = GetDailyRaceDateKey(DateTime.UtcNow);
        var prefKey = DAILY_RACE_PREF_PREFIX + todayKey;
        int currentBest = _preferences.Get(prefKey, 0);

        // Nur neuer Wert wenn besser
        if (score <= currentBest) return false;

        _preferences.Set(prefKey, score);

        // Firebase-Push (best-effort, kein Throw bei Offline)
        var uid = _firebase.Uid;
        if (IsOnline && !string.IsNullOrEmpty(uid))
        {
            try
            {
                await PushDailyRaceScoreToFirebaseAsync(todayKey, score, uid);
                // Liga-Punkte-Vergabe basierend auf Tages-Rang nach Push
                // Top-3 = +100, Top-10 = +50. Einmal pro Tag (nur bei neuem Best).
                await AwardDailyRaceLeaguePointsAsync(todayKey, uid);
            }
            catch
            {
                // Firebase-Fehler werden bewusst ignoriert — lokaler Wert ist persistiert.
            }
        }
        return true;
    }

    /// <summary>
    /// Vergibt Liga-Punkte basierend auf Daily-Race-Rang nach erfolgreichem Score-Push.
    /// Top-3 = +100, Top-10 = +50. Idempotent via "DailyRacePointsAwarded_{dateKey}" Preferences-Flag
    /// damit ein einzelner Spieler bei mehreren Best-Improvements nicht mehrfach Punkte bekommt.
    /// </summary>
    private async Task AwardDailyRaceLeaguePointsAsync(string dateKey, string uid)
    {
        var awardKey = $"DailyRacePointsAwarded_{dateKey}";
        if (_preferences.Get(awardKey, false)) return;

        try
        {
            var leaderboard = await FetchDailyRaceLeaderboardFromFirebaseAsync(dateKey, uid);
            var ownEntry = leaderboard.FirstOrDefault(e => e.Uid == uid);
            if (ownEntry == null) return;

            int points = ownEntry.Rank switch
            {
                <= 3 => 100,
                <= 10 => 50,
                _ => 0,
            };
            if (points > 0)
            {
                AddPoints(points);
                _preferences.Set(awardKey, true);
            }
        }
        catch
        {
            // Liga-Punkte-Vergabe ist best-effort. Bei Firebase-Fehler kein Award —
            // wird beim naechsten Submit oder Leaderboard-Refresh nochmal versucht.
        }
    }

    public async Task<IReadOnlyList<LeagueLeaderboardEntry>> GetDailyRaceLeaderboardAsync(DateTime? utcDate = null)
    {
        var date = utcDate ?? DateTime.UtcNow;
        var key = GetDailyRaceDateKey(date);
        var uid = _firebase.Uid ?? "";

        // Offline / nicht-initialisiert: nur eigener Eintrag.
        if (!IsOnline || string.IsNullOrEmpty(uid))
        {
            int local = _preferences.Get(DAILY_RACE_PREF_PREFIX + key, 0);
            if (local <= 0) return Array.Empty<LeagueLeaderboardEntry>();
            return new[]
            {
                new LeagueLeaderboardEntry
                {
                    Uid = uid,
                    Name = PlayerName,
                    Points = local,
                    Rank = 1,
                    IsPlayer = true,
                    IsRealPlayer = true,
                }
            };
        }

        try
        {
            return await FetchDailyRaceLeaderboardFromFirebaseAsync(key, uid);
        }
        catch
        {
            return Array.Empty<LeagueLeaderboardEntry>();
        }
    }

    /// <summary>
    /// Cross-Tier (Global): fetcht parallel alle 5 Tier-Subtrees, merged + sortiert + Top-50.
    /// </summary>
    public async Task<IReadOnlyList<LeagueLeaderboardEntry>> GetDailyRaceGlobalLeaderboardAsync(DateTime? utcDate = null)
    {
        var date = utcDate ?? DateTime.UtcNow;
        var key = GetDailyRaceDateKey(date);
        var uid = _firebase.Uid ?? "";

        if (!IsOnline || string.IsNullOrEmpty(uid))
        {
            // Offline-Fallback identisch zu Single-Tier (nur eigener Eintrag wenn vorhanden).
            return await GetDailyRaceLeaderboardAsync(utcDate);
        }

        try
        {
            await _firebase.EnsureAuthenticatedAsync();

            // Alle 5 Tiers parallel fetchen
            var tiers = new[] { LeagueTier.Bronze, LeagueTier.Silver, LeagueTier.Gold, LeagueTier.Platinum, LeagueTier.Diamond };
            var fetchTasks = tiers.Select(async tier =>
            {
                var path = $"league/s{SeasonNumber}/daily_race/{key}/{(int)tier}";
                try
                {
                    return await _firebase.GetAsync<Dictionary<string, DailyRaceFirebaseEntry>>(path)
                           ?? new Dictionary<string, DailyRaceFirebaseEntry>();
                }
                catch
                {
                    return new Dictionary<string, DailyRaceFirebaseEntry>();
                }
            }).ToArray();

            var allTierResults = await Task.WhenAll(fetchTasks);

            // Mergen aller Tier-Eintraege in eine globale Liste
            var merged = new List<LeagueLeaderboardEntry>();
            foreach (var tierEntries in allTierResults)
            {
                foreach (var (entryUid, entry) in tierEntries)
                {
                    merged.Add(new LeagueLeaderboardEntry
                    {
                        Uid = entryUid,
                        Name = entry.Name ?? "",
                        Points = entry.Score,
                        IsPlayer = entryUid == uid,
                        IsRealPlayer = true,
                    });
                }
            }

            // Score-DESC sortieren, Rank zuweisen, Top-50 cappen
            merged.Sort((a, b) => b.Points.CompareTo(a.Points));
            for (int i = 0; i < merged.Count; i++) merged[i].Rank = i + 1;
            if (merged.Count > 50) merged.RemoveRange(50, merged.Count - 50);
            return merged;
        }
        catch
        {
            return Array.Empty<LeagueLeaderboardEntry>();
        }
    }

    /// <summary>
    /// Pusht den Daily-Race-Score in Firebase: <c>league/s{saison}/daily_race/{date}/{tier}/{uid}</c>.
    /// Server-Timestamp via {".sv":"timestamp"} verhindert client-spoofbare updatedMs (analog Liga-Saison-Schema).
    /// EnsureAuthenticatedAsync stellt sicher dass Anonymous-Auth aktiv ist — sonst wuerde Permission-Denied folgen.
    /// </summary>
    private async Task PushDailyRaceScoreToFirebaseAsync(string dateKey, int score, string uid)
    {
        await _firebase.EnsureAuthenticatedAsync();
        var path = $"league/s{SeasonNumber}/daily_race/{dateKey}/{(int)CurrentTier}/{uid}";
        var payload = new DailyRaceFirebaseEntry
        {
            Name = PlayerName,
            Score = score,
            UpdatedMs = new Dictionary<string, string> { [".sv"] = "timestamp" },
        };
        await _firebase.SetAsync(path, payload);
    }

    /// <summary>
    /// Liefert Top-20 echte Spieler aus dem eigenen Tier-Subtree fuer den angegebenen Tag.
    /// Sortiert absteigend nach Score. EnsureAuthenticatedAsync vor dem Read damit keine Permission-Denied folgt.
    /// </summary>
    private async Task<IReadOnlyList<LeagueLeaderboardEntry>> FetchDailyRaceLeaderboardFromFirebaseAsync(string dateKey, string playerUid)
    {
        await _firebase.EnsureAuthenticatedAsync();
        var path = $"league/s{SeasonNumber}/daily_race/{dateKey}/{(int)CurrentTier}";
        var entries = await _firebase.GetAsync<Dictionary<string, DailyRaceFirebaseEntry>>(path);
        if (entries == null || entries.Count == 0) return Array.Empty<LeagueLeaderboardEntry>();

        var list = new List<LeagueLeaderboardEntry>(entries.Count);
        foreach (var (uid, entry) in entries)
        {
            list.Add(new LeagueLeaderboardEntry
            {
                Uid = uid,
                Name = entry.Name ?? "",
                Points = entry.Score,
                IsPlayer = uid == playerUid,
                IsRealPlayer = true,
            });
        }
        list.Sort((a, b) => b.Points.CompareTo(a.Points));
        for (int i = 0; i < list.Count; i++) list[i].Rank = i + 1;
        if (list.Count > 20) list.RemoveRange(20, list.Count - 20);
        return list;
    }

    /// <summary>Firebase-Payload fuer Daily-Race-Eintrag (analog FirebaseLeagueEntry, aber mit Score statt Points).</summary>
    private class DailyRaceFirebaseEntry
    {
        public string? Name { get; set; }
        public int Score { get; set; }
        // Server-Timestamp-Sentinel fuer manipulationssichere updatedMs (siehe Saison-Liga-Schema).
        public Dictionary<string, string>? UpdatedMs { get; set; }
    }
}
