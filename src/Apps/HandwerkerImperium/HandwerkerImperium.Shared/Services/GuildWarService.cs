using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet wöchentliche Gilden-Kriege via Firebase.
/// Gilden werden automatisch gematcht (ähnliches Level).
/// Gesamtpunkte aller Mitglieder entscheiden über Sieg/Niederlage.
/// </summary>
public sealed class GuildWarService : IGuildWarService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly IPreferencesService _preferences;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string PrefKeyWarRewardPrefix = "gw_reward_";

    // Punkte-Quellen
    public const long PointsForOrder = 100;
    public const long PointsForMiniGame = 75;
    public const long PointsForCrafting = 75;
    public const long PointsForUpgrade = 25;

    // Belohnungen
    private const int WinnerReward = 20;  // Goldschrauben
    private const int LoserReward = 5;

    // Cache
    private string? _activeWarId;
    private GuildWar? _cachedWar;
    private DateTime _lastWarCheck = DateTime.MinValue;
    private static readonly TimeSpan WarCheckCooldown = TimeSpan.FromMinutes(2);

    public GuildWarService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        IPreferencesService preferences)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _preferences = preferences;
    }

    public async Task<GuildWar?> GetOrCreateActiveWarAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return null;

        await _lock.WaitAsync();
        try
        {
            // Cache prüfen
            if (_cachedWar != null && DateTime.UtcNow - _lastWarCheck < WarCheckCooldown)
                return _cachedWar;

            // Aktuelle Woche als War-ID
            var warId = GetCurrentWarId();
            _activeWarId = warId;

            // War laden
            var war = await _firebase.GetAsync<GuildWar>($"guild_wars/{warId}");
            if (war != null)
            {
                _cachedWar = war;
                _lastWarCheck = DateTime.UtcNow;
                return war;
            }

            // Kein aktiver War → neuen erstellen (Matching)
            war = await TryCreateWarAsync(warId, membership.GuildId, membership.GuildName);
            _cachedWar = war;
            _lastWarCheck = DateTime.UtcNow;
            return war;
        }
        catch
        {
            // Netzwerkfehler still behandelt
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ContributeScoreAsync(long points)
    {
        if (points <= 0) return;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (string.IsNullOrEmpty(_activeWarId))
        {
            _activeWarId = GetCurrentWarId();
        }

        try
        {
            var uid = _firebase.PlayerId;
            if (string.IsNullOrEmpty(uid)) return;

            // Eigenen Score aktualisieren (Race-Condition-frei: jeder Spieler hat eigenen Eintrag)
            var ownScore = await _firebase.GetAsync<GuildWarScore>(
                $"guild_war_scores/{_activeWarId}/{membership.GuildId}/{uid}");

            var newScore = (ownScore?.Score ?? 0) + points;
            await _firebase.SetAsync($"guild_war_scores/{_activeWarId}/{membership.GuildId}/{uid}",
                new GuildWarScore
                {
                    Score = newScore,
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            // Cache aktualisieren (lokaler Wert, kein Firebase-Write auf Gesamt-Score)
            if (_cachedWar != null)
            {
                var isGuildA = _cachedWar.GuildAId == membership.GuildId;
                if (isGuildA)
                    _cachedWar.ScoreA += points;
                else
                    _cachedWar.ScoreB += points;
            }
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
    }

    public async Task<GuildWarDisplayData?> GetWarStatusAsync()
    {
        var war = await GetOrCreateActiveWarAsync();
        if (war == null) return null;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return null;

        var isGuildA = war.GuildAId == membership.GuildId;

        // Scores aus per-Player-Daten aggregieren (Race-Condition-frei)
        long ownScore = 0;
        long opponentScore = 0;
        long ownContribution = 0;
        try
        {
            if (!string.IsNullOrEmpty(_activeWarId))
            {
                var ownGuildId = membership.GuildId;
                var opponentGuildId = isGuildA ? war.GuildBId : war.GuildAId;

                (ownScore, ownContribution) = await AggregateGuildScoresAsync(
                    _activeWarId, ownGuildId, _firebase.PlayerId);
                (opponentScore, _) = await AggregateGuildScoresAsync(
                    _activeWarId, opponentGuildId, null);
            }
        }
        catch
        {
            // Netzwerkfehler → Fallback auf War-Objekt-Werte
            ownScore = isGuildA ? war.ScoreA : war.ScoreB;
            opponentScore = isGuildA ? war.ScoreB : war.ScoreA;
        }

        var endDate = DateTime.TryParse(war.EndDate, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.UtcNow.AddDays(7);

        return new GuildWarDisplayData
        {
            OwnGuildName = isGuildA ? war.GuildAName : war.GuildBName,
            OpponentGuildName = isGuildA ? war.GuildBName : war.GuildAName,
            OwnScore = ownScore,
            OpponentScore = opponentScore,
            OwnContribution = ownContribution,
            EndDate = endDate,
            IsActive = war.Status == "active" && endDate > DateTime.UtcNow,
            DidWin = war.Status == "completed" &&
                     (isGuildA ? ownScore > opponentScore : opponentScore < ownScore)
        };
    }

    public async Task CheckAndFinalizeWarAsync()
    {
        // War-ID bestimmen (nach Restart kann _activeWarId null sein)
        if (string.IsNullOrEmpty(_activeWarId))
            _activeWarId = GetCurrentWarId();

        // War aus Firebase laden falls Cache leer (z.B. nach App-Neustart)
        if (_cachedWar == null)
        {
            _cachedWar = await _firebase.GetAsync<GuildWar>($"guild_wars/{_activeWarId}");
        }

        if (_cachedWar == null || _cachedWar.Status != "active")
            return;

        var endDate = DateTime.TryParse(_cachedWar.EndDate, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.MaxValue;

        if (DateTime.UtcNow < endDate) return;

        // Duplikat-Schutz: Pro War nur einmal belohnen
        var rewardKey = $"{PrefKeyWarRewardPrefix}{_activeWarId}";
        if (_preferences.Get(rewardKey, false)) return;

        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return;

            // Scores aggregieren (Race-Condition-frei)
            var isGuildA = _cachedWar.GuildAId == membership.GuildId;
            var ownGuildId = membership.GuildId;
            var opponentGuildId = isGuildA ? _cachedWar.GuildBId : _cachedWar.GuildAId;

            var (ownTotal, _) = await AggregateGuildScoresAsync(_activeWarId, ownGuildId, null);
            var (opponentTotal, _) = await AggregateGuildScoresAsync(_activeWarId, opponentGuildId, null);

            // War als beendet markieren
            await _firebase.UpdateAsync($"guild_wars/{_activeWarId}",
                new Dictionary<string, object> { ["status"] = "completed" });
            _cachedWar.Status = "completed";

            // Belohnungen verteilen
            var won = ownTotal > opponentTotal;
            var reward = won ? WinnerReward : LoserReward;
            _gameStateService.AddGoldenScrews(reward);
            _preferences.Set(rewardKey, true);
            await _saveGameService.SaveAsync();
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
    }

    /// <summary>
    /// Aggregiert alle Spieler-Scores einer Gilde in einem War (Race-Condition-frei).
    /// Gibt (GildenGesamt, eigener Beitrag) zurück. Wenn playerIdToFind null ist, wird eigener Beitrag nicht gesucht.
    /// </summary>
    private async Task<(long Total, long OwnContribution)> AggregateGuildScoresAsync(
        string warId, string guildId, string? playerIdToFind)
    {
        try
        {
            var json = await _firebase.QueryAsync($"guild_war_scores/{warId}/{guildId}", "");
            if (string.IsNullOrEmpty(json) || json == "null")
                return (0, 0);

            var scores = JsonSerializer.Deserialize<Dictionary<string, GuildWarScore>>(json);
            if (scores == null || scores.Count == 0)
                return (0, 0);

            long total = 0;
            long own = 0;

            foreach (var (playerId, score) in scores)
            {
                total += score.Score;
                if (playerIdToFind != null && playerId == playerIdToFind)
                    own = score.Score;
            }

            return (total, own);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Erstellt einen neuen Gilden-Krieg mit Matching.
    /// </summary>
    private async Task<GuildWar?> TryCreateWarAsync(string warId, string ownGuildId, string ownGuildName)
    {
        try
        {
            // Einfaches Matching: Gilde "Herausforderer" gegen zufällige andere
            // In der Praxis: Erste Gilde die prüft erstellt, zweite wird gematcht
            var now = DateTime.UtcNow;
            var endDate = now.AddDays(7); // 1 Woche

            var war = new GuildWar
            {
                GuildAId = ownGuildId,
                GuildAName = ownGuildName,
                GuildBId = "waiting",
                GuildBName = "Warte auf Gegner...",
                ScoreA = 0,
                ScoreB = 0,
                StartDate = now.ToString("O"),
                EndDate = endDate.ToString("O"),
                Status = "active"
            };

            // Prüfe ob schon ein War mit "waiting" existiert, dem wir beitreten können
            var existingJson = await _firebase.QueryAsync("guild_wars",
                $"orderBy=\"status\"&equalTo=\"active\"&limitToFirst=5");

            if (!string.IsNullOrEmpty(existingJson))
            {
                var existingWars = JsonSerializer.Deserialize<Dictionary<string, GuildWar>>(existingJson);
                if (existingWars != null)
                {
                    foreach (var (existingWarId, existingWar) in existingWars)
                    {
                        // Beitreten wenn "waiting" und nicht unsere Gilde
                        if (existingWar.GuildBId == "waiting" && existingWar.GuildAId != ownGuildId)
                        {
                            await _firebase.UpdateAsync($"guild_wars/{existingWarId}",
                                new Dictionary<string, object>
                                {
                                    ["guildBId"] = ownGuildId,
                                    ["guildBName"] = ownGuildName
                                });
                            existingWar.GuildBId = ownGuildId;
                            existingWar.GuildBName = ownGuildName;
                            _activeWarId = existingWarId;
                            return existingWar;
                        }
                    }
                }
            }

            // Kein offener War gefunden → neuen erstellen
            await _firebase.SetAsync($"guild_wars/{warId}", war);
            return war;
        }
        catch
        {
            // Netzwerkfehler still behandelt
            return null;
        }
    }

    /// <summary>
    /// Generiert eine War-ID basierend auf der aktuellen Kalenderwoche.
    /// </summary>
    private static string GetCurrentWarId()
    {
        var now = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"w_{now.Year}_{week:D2}";
    }
}
