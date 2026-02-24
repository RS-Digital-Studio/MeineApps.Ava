using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet wöchentliche Gilden-Kriege via Firebase.
/// Gilden werden automatisch gematcht (ähnliches Level).
/// Gesamtpunkte aller Mitglieder entscheiden über Sieg/Niederlage.
/// </summary>
public class GuildWarService : IGuildWarService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly SemaphoreSlim _lock = new(1, 1);

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
        ISaveGameService saveGameService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
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
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            // Eigenen Score aktualisieren
            var ownScore = await _firebase.GetAsync<GuildWarScore>(
                $"guild_war_scores/{_activeWarId}/{membership.GuildId}/{uid}");

            var newScore = (ownScore?.Score ?? 0) + points;
            await _firebase.SetAsync($"guild_war_scores/{_activeWarId}/{membership.GuildId}/{uid}",
                new GuildWarScore
                {
                    Score = newScore,
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            // Gilden-Gesamtscore aktualisieren
            if (_cachedWar != null)
            {
                var isGuildA = _cachedWar.GuildAId == membership.GuildId;
                var scoreField = isGuildA ? "scoreA" : "scoreB";
                var currentTotal = isGuildA ? _cachedWar.ScoreA : _cachedWar.ScoreB;

                await _firebase.UpdateAsync($"guild_wars/{_activeWarId}",
                    new Dictionary<string, object> { [scoreField] = currentTotal + points });

                // Cache aktualisieren
                if (isGuildA)
                    _cachedWar.ScoreA += points;
                else
                    _cachedWar.ScoreB += points;
            }
        }
        catch
        {
            // Fire-and-forget, Fehler ignorieren
        }
    }

    public async Task<GuildWarDisplayData?> GetWarStatusAsync()
    {
        var war = await GetOrCreateActiveWarAsync();
        if (war == null) return null;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return null;

        var isGuildA = war.GuildAId == membership.GuildId;

        // Eigenen Beitrag laden
        long ownContribution = 0;
        try
        {
            var uid = _firebase.Uid;
            if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(_activeWarId))
            {
                var score = await _firebase.GetAsync<GuildWarScore>(
                    $"guild_war_scores/{_activeWarId}/{membership.GuildId}/{uid}");
                ownContribution = score?.Score ?? 0;
            }
        }
        catch { /* Ignorieren */ }

        var endDate = DateTime.TryParse(war.EndDate, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.UtcNow.AddDays(7);

        return new GuildWarDisplayData
        {
            OwnGuildName = isGuildA ? war.GuildAName : war.GuildBName,
            OpponentGuildName = isGuildA ? war.GuildBName : war.GuildAName,
            OwnScore = isGuildA ? war.ScoreA : war.ScoreB,
            OpponentScore = isGuildA ? war.ScoreB : war.ScoreA,
            OwnContribution = ownContribution,
            EndDate = endDate,
            IsActive = war.Status == "active" && endDate > DateTime.UtcNow,
            DidWin = war.Status == "completed" &&
                     (isGuildA ? war.ScoreA > war.ScoreB : war.ScoreB > war.ScoreA)
        };
    }

    public async Task CheckAndFinalizeWarAsync()
    {
        if (_cachedWar == null || _cachedWar.Status != "active")
            return;

        var endDate = DateTime.TryParse(_cachedWar.EndDate, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.MaxValue;

        if (DateTime.UtcNow < endDate) return;

        try
        {
            // War als beendet markieren
            await _firebase.UpdateAsync($"guild_wars/{_activeWarId}",
                new Dictionary<string, object> { ["status"] = "completed" });
            _cachedWar.Status = "completed";

            // Belohnungen verteilen
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return;

            var isGuildA = _cachedWar.GuildAId == membership.GuildId;
            var won = isGuildA
                ? _cachedWar.ScoreA > _cachedWar.ScoreB
                : _cachedWar.ScoreB > _cachedWar.ScoreA;

            var reward = won ? WinnerReward : LoserReward;
            _gameStateService.AddGoldenScrews(reward);
            await _saveGameService.SaveAsync();
        }
        catch
        {
            // Fehler ignorieren
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
