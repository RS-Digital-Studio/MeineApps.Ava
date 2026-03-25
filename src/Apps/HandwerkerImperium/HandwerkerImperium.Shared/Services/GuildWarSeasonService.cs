using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet das Saison-Ligen-System für Gildenkriege.
/// Jede Saison dauert 4 Wochen mit wöchentlichen Kriegen (Mo-So).
/// Phasen: Angriff (Mo-Mi) → Verteidigung (Do-Fr) → Auswertung (Sa-So).
/// Race-Condition-frei: Nur eigene Spieler-Scores schreiben, Gesamtscore wird bei Abfrage berechnet.
/// </summary>
public sealed class GuildWarSeasonService : IGuildWarSeasonService, IDisposable
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly IPreferencesService _preferences;
    private readonly ILogService _log;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Cache
    private GuildWar? _cachedWar;
    private string? _activeWarId;
    private GuildLeague _currentLeague = GuildLeague.Bronze;
    private WarPhase _lastKnownPhase = WarPhase.Attack;
    private long _cachedOwnTotalScore;
    private long _cachedOpponentTotalScore;

    // Belohnungen
    private const int WinRewardGs = 20;
    private const int DrawRewardGs = 10;
    private const int LossRewardGs = 5;
    private const int MvpBonusGs = 5;
    private const int AllBonusMissionGs = 3;
    private const int WinLeaguePoints = 3;
    private const int DrawLeaguePoints = 1;

    // Matching-Toleranzen
    private const int LevelMatchTolerance = 3;
    private const int LevelMatchToleranceExtended = 5;

    // Preferences-Keys
    private const string PrefKeyLastPhase = "gws_last_phase";
    private const string PrefKeyBonusMissionPrefix = "gws_bonusmission_";
    private const string PrefKeyWarRewardPrefix = "gws_war_reward_";
    private const string PrefKeySeasonRewardPrefix = "gws_season_reward_";

    public GuildWarSeasonService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        IPreferencesService preferences,
        ILogService log)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _preferences = preferences;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    public async Task InitializeAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            // Liga aus Firebase laden
            var leagueId = await _firebase.GetAsync<string>(
                $"guilds/{membership.GuildId}/leagueId");
            _currentLeague = ParseLeague(leagueId ?? membership.LeagueId);

            // Saison laden oder erstellen
            var seasonId = GetCurrentSeasonId();
            await GetOrCreateSeasonAsync(seasonId);

            // War der aktuellen Woche laden oder erstellen
            var weekNr = GetCurrentWeekInSeason();
            var warId = await FindOrCreateWarAsync(seasonId, weekNr);

            if (!string.IsNullOrEmpty(warId))
            {
                _activeWarId = warId;
                _cachedWar = await _firebase.GetAsync<GuildWar>($"guild_wars/{warId}");
            }

            // Letzte bekannte Phase laden
            var savedPhase = _preferences.Get(PrefKeyLastPhase, "attack");
            _lastKnownPhase = ParseWarPhase(savedPhase);
        }
        catch (Exception ex)
        {
            _log.Error("Gildenkrieg-Initialisierung fehlgeschlagen", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUNKTE BEITRAGEN
    // ═══════════════════════════════════════════════════════════════════════

    public async Task ContributeScoreAsync(long points, string source)
    {
        if (points <= 0) return;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        var uid = _firebase.PlayerId;
        if (string.IsNullOrEmpty(uid))
            return;

        var phase = GetCurrentPhase();
        if (phase == WarPhase.Evaluation || phase == WarPhase.Completed)
            return; // Keine Punkte während Auswertung

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            // _activeWarId kann sich zwischen Pre-Check und Lock-Erwerb ändern
            if (string.IsNullOrEmpty(_activeWarId))
                return;

            var guildId = membership.GuildId;
            var scorePath = $"guild_war_scores/{_activeWarId}/{guildId}/{uid}";

            // Eigenen Score laden
            var playerScore = await _firebase.GetAsync<GuildWarPlayerScore>(scorePath)
                              ?? new GuildWarPlayerScore();

            // Aktuelle Scores VOR dem Multiplikator-Check laden (statt stale Cache)
            var opponentGuildId = _cachedWar != null
                ? (_cachedWar.GuildAId == guildId ? _cachedWar.GuildBId : _cachedWar.GuildAId)
                : null;
            var freshOwnScore = await CalculateGuildTotalScoreAsync(_activeWarId, guildId);
            var freshOpponentScore = 0L;
            if (!string.IsNullOrEmpty(opponentGuildId) && opponentGuildId != "waiting")
                freshOpponentScore = await CalculateGuildTotalScoreAsync(_activeWarId, opponentGuildId);
            _cachedOwnTotalScore = freshOwnScore;
            _cachedOpponentTotalScore = freshOpponentScore;

            // Punkte berechnen mit Phasen-Multiplikator
            var effectivePoints = points;

            // Verteidigungsphase: Punkte halbiert
            if (phase == WarPhase.Defense)
                effectivePoints = (long)(points * 0.5);

            // Aufhol-Multiplikator prüfen (1.5x wenn zurückliegend)
            if (freshOpponentScore > freshOwnScore)
                effectivePoints = (long)(effectivePoints * 1.5);

            // Hall-Kriegspunkte-Bonus anwenden
            if (membership.HallWarPointsBonus > 0)
                effectivePoints = (long)(effectivePoints * (1m + membership.HallWarPointsBonus));

            // Score in richtiger Phase zuweisen
            if (phase == WarPhase.Attack)
                playerScore.AttackScore += effectivePoints;
            else
                playerScore.DefenseScore += effectivePoints;

            playerScore.UpdatedAt = DateTime.UtcNow.ToString("O");

            // Nur eigenen Score schreiben (Race-Condition-frei)
            await _firebase.SetAsync(scorePath, playerScore);

            // War-Log-Eintrag pushen
            var logEntry = new GuildWarLogEntry
            {
                Type = "score",
                GuildId = guildId,
                PlayerName = GetPlayerName(),
                Points = effectivePoints,
                Message = source,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
            await _firebase.PushAsync($"guild_war_log/{_activeWarId}", logEntry);
        }
        catch (Exception ex)
        {
            _log.Error("Kriegspunkte beitragen fehlgeschlagen", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AKTUELLE KRIEGS-DATEN LADEN
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<WarSeasonDisplayData?> GetCurrentWarDataAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return null;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return null; // Timeout: Lock nicht erhalten
        try
        {
            var seasonId = GetCurrentSeasonId();
            var weekNr = GetCurrentWeekInSeason();
            var phase = GetCurrentPhase();

            // Saison-Daten laden
            var season = await _firebase.GetAsync<GuildWarSeasonData>(
                $"guild_war_seasons/{seasonId}");

            // Bye-Week wenn kein aktiver War
            if (string.IsNullOrEmpty(_activeWarId) || _cachedWar == null)
            {
                return new WarSeasonDisplayData
                {
                    SeasonId = seasonId,
                    SeasonNumber = GetSeasonNumber(),
                    WeekNumber = weekNr,
                    OwnLeague = _currentLeague,
                    CurrentPhase = phase,
                    PhaseEndsAt = GetPhaseEndTime().ToString("O")
                };
            }

            // War-Daten aktualisieren
            _cachedWar = await _firebase.GetAsync<GuildWar>($"guild_wars/{_activeWarId}")
                         ?? _cachedWar;

            var guildId = membership.GuildId;
            var isGuildA = _cachedWar.GuildAId == guildId;
            var opponentGuildId = isGuildA ? _cachedWar.GuildBId : _cachedWar.GuildAId;

            // Gesamt-Scores aus Member-Scores berechnen (Race-Condition-frei)
            var ownScore = await CalculateGuildTotalScoreAsync(_activeWarId, guildId);
            var opponentScore = 0L;
            if (!string.IsNullOrEmpty(opponentGuildId) && opponentGuildId != "waiting")
                opponentScore = await CalculateGuildTotalScoreAsync(_activeWarId, opponentGuildId);

            // Live-Scores cachen für Aufhol-Multiplikator in ContributeScoreAsync
            _cachedOwnTotalScore = ownScore;
            _cachedOpponentTotalScore = opponentScore;

            // MVP berechnen
            var (mvpName, mvpScore) = await GetMvpAsync(_activeWarId, guildId);

            // Bonus-Missionen laden
            var bonusMissions = await GetBonusMissionsAsync();

            // Liga-Eintrag laden
            var leagueEntry = await _firebase.GetAsync<GuildLeagueEntry>(
                $"guild_war_seasons/{seasonId}/leagues/{_currentLeague.ToString().ToLowerInvariant()}/{guildId}");

            return new WarSeasonDisplayData
            {
                SeasonId = seasonId,
                SeasonNumber = GetSeasonNumber(),
                WeekNumber = weekNr,
                OwnLeague = _currentLeague,
                WarId = _activeWarId,
                OpponentName = isGuildA ? _cachedWar.GuildBName : _cachedWar.GuildAName,
                OpponentLevel = isGuildA ? _cachedWar.GuildBLevel : _cachedWar.GuildALevel,
                OwnScore = ownScore,
                OpponentScore = opponentScore,
                CurrentPhase = phase,
                PhaseEndsAt = GetPhaseEndTime().ToString("O"),
                BonusMissions = bonusMissions,
                MvpName = mvpName,
                MvpScore = mvpScore
            };
        }
        catch (Exception ex)
        {
            _log.Error("Kriegsdaten laden fehlgeschlagen", ex);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KRIEGS-LOG
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<GuildWarLogEntry>> GetWarLogAsync(int limit = 50)
    {
        if (string.IsNullOrEmpty(_activeWarId))
            return [];

        try
        {
            var json = await _firebase.QueryAsync(
                $"guild_war_log/{_activeWarId}",
                $"orderBy=\"timestamp\"&limitToLast={limit}");

            if (string.IsNullOrEmpty(json))
                return [];

            var entries = JsonSerializer.Deserialize<Dictionary<string, GuildWarLogEntry>>(json);
            if (entries == null)
                return [];

            // Nach Timestamp absteigend sortieren
            return entries.Values
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }
        catch (Exception ex)
        {
            _log.Error("Kriegs-Log laden fehlgeschlagen", ex);
            return [];
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BONUS-MISSIONEN
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<WarBonusMission>> GetBonusMissionsAsync()
    {
        await Task.CompletedTask; // Bonus-Missionen sind lokal

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var prefix = $"{PrefKeyBonusMissionPrefix}{today}";

        // 3 tägliche Missionen mit lokalem Fortschritts-Tracking
        var missions = new List<WarBonusMission>
        {
            new()
            {
                Id = "orders_5",
                NameKey = "GuildWarBonusOrders",
                DescKey = "GuildWarBonusOrdersDesc",
                Target = 5,
                Progress = _preferences.Get($"{prefix}_orders", 0),
                BonusPoints = 200
            },
            new()
            {
                Id = "minigames_3",
                NameKey = "GuildWarBonusMiniGames",
                DescKey = "GuildWarBonusMiniGamesDesc",
                Target = 3,
                Progress = _preferences.Get($"{prefix}_minigames", 0),
                BonusPoints = 150
            },
            new()
            {
                Id = "deposit_50k",
                NameKey = "GuildWarBonusDeposit",
                DescKey = "GuildWarBonusDepositDesc",
                Target = 50_000,
                Progress = _preferences.Get($"{prefix}_deposit", 0),
                BonusPoints = 100
            }
        };

        return missions;
    }

    /// <summary>
    /// Aktualisiert den Fortschritt einer Bonus-Mission (lokal in Preferences).
    /// Vergibt automatisch Bonuspunkte bei Abschluss.
    /// </summary>
    public async Task UpdateBonusMissionProgressAsync(string missionType, int amount = 1)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var key = $"{PrefKeyBonusMissionPrefix}{today}_{missionType}";
        var claimedKey = $"{key}_claimed";

        // Bereits abgeschlossen und Bonus kassiert?
        if (_preferences.Get(claimedKey, false))
            return;

        var current = _preferences.Get(key, 0);
        var newVal = current + amount;
        _preferences.Set(key, newVal);

        // Prüfe ob Mission abgeschlossen
        var target = missionType switch
        {
            "orders" => 5,
            "minigames" => 3,
            "deposit" => 50_000,
            _ => int.MaxValue
        };

        if (newVal >= target)
        {
            var bonusPoints = missionType switch
            {
                "orders" => 200,
                "minigames" => 150,
                "deposit" => 100,
                _ => 0
            };

            if (bonusPoints > 0)
            {
                _preferences.Set(claimedKey, true);
                await ContributeScoreAsync(bonusPoints, $"bonus_{missionType}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PHASENWECHSEL PRÜFEN
    // ═══════════════════════════════════════════════════════════════════════

    public async Task CheckPhaseTransitionAsync()
    {
        var currentPhase = GetCurrentPhase();
        if (currentPhase == _lastKnownPhase)
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            // Double-Check nach Lock-Erwerb (anderer Thread könnte schneller gewesen sein)
            currentPhase = GetCurrentPhase();
            if (currentPhase == _lastKnownPhase)
                return;

            var previousPhase = _lastKnownPhase;
            _lastKnownPhase = currentPhase;
            _preferences.Set(PrefKeyLastPhase, currentPhase.ToString().ToLowerInvariant());

            // Cache invalidieren bei Phasenwechsel
            _cachedWar = null;

            if (string.IsNullOrEmpty(_activeWarId))
                return;

            // Phase in Firebase updaten
            await _firebase.UpdateAsync($"guild_wars/{_activeWarId}",
                new Dictionary<string, object>
                {
                    ["phase"] = currentPhase.ToString().ToLowerInvariant(),
                    ["phaseEndsAt"] = GetPhaseEndTime().ToString("O")
                });

            // Log-Eintrag pushen
            var logEntry = new GuildWarLogEntry
            {
                Type = "phase_change",
                GuildId = _gameStateService.State.GuildMembership?.GuildId ?? "",
                PlayerName = "",
                Points = 0,
                Message = $"{previousPhase} -> {currentPhase}",
                Timestamp = DateTime.UtcNow.ToString("O")
            };
            await _firebase.PushAsync($"guild_war_log/{_activeWarId}", logEntry);
        }
        catch (Exception ex)
        {
            _log.Error("Phasenwechsel prüfen fehlgeschlagen", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SAISON-ENDE PRÜFEN
    // ═══════════════════════════════════════════════════════════════════════

    public async Task CheckSeasonEndAsync()
    {
        var weekNr = GetCurrentWeekInSeason();
        var dow = DateTime.UtcNow.DayOfWeek;

        // Saison-Ende: Sonntag der 4. Woche
        if (weekNr != 4 || dow != DayOfWeek.Sunday)
            return;

        var seasonId = GetCurrentSeasonId();
        var season = await _firebase.GetAsync<GuildWarSeasonData>(
            $"guild_war_seasons/{seasonId}");

        if (season == null || season.Status == "completed")
            return;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            // Letzten War-Belohnungen verteilen
            if (_cachedWar != null && _cachedWar.Status != "completed")
                await DistributeWarRewardsAsync(_cachedWar);

            // Liga-Auf/Abstieg berechnen
            await ProcessLeaguePromotionAsync(seasonId, membership.GuildId);

            // Saison als beendet markieren
            await _firebase.UpdateAsync($"guild_war_seasons/{seasonId}",
                new Dictionary<string, object> { ["status"] = "completed" });

            // Saison-Belohnungen verteilen
            DistributeSeasonRewards();
        }
        catch (Exception ex)
        {
            _log.Error("Saison-Ende prüfen fehlgeschlagen", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public GuildLeague GetCurrentLeague() => _currentLeague;

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: SAISON-VERWALTUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lädt oder erstellt die Saison-Daten in Firebase.
    /// </summary>
    private async Task<GuildWarSeasonData> GetOrCreateSeasonAsync(string seasonId)
    {
        var season = await _firebase.GetAsync<GuildWarSeasonData>(
            $"guild_war_seasons/{seasonId}");

        if (season != null)
            return season;

        // Neue Saison erstellen
        var now = DateTime.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
        if (now.DayOfWeek == DayOfWeek.Sunday)
            startOfWeek = startOfWeek.AddDays(-7);
        var weekInSeason = GetCurrentWeekInSeason();
        var seasonStart = startOfWeek.AddDays(-7 * (weekInSeason - 1));

        season = new GuildWarSeasonData
        {
            StartDate = seasonStart.Date.ToString("O"),
            EndDate = seasonStart.AddDays(28).ToString("O"),
            Status = "active",
            Week = weekInSeason
        };

        await _firebase.SetAsync($"guild_war_seasons/{seasonId}", season);

        // Eigene Gilde in der Liga registrieren
        var membership = _gameStateService.State.GuildMembership;
        if (membership != null && !string.IsNullOrEmpty(membership.GuildId))
        {
            var leagueKey = _currentLeague.ToString().ToLowerInvariant();
            var entry = new GuildLeagueEntry
            {
                Points = 0,
                Wins = 0,
                Losses = 0,
                Rank = 0
            };
            await _firebase.SetAsync(
                $"guild_war_seasons/{seasonId}/leagues/{leagueKey}/{membership.GuildId}", entry);
        }

        return season;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: MATCHING-ALGORITHMUS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht einen offenen War zum Beitreten oder erstellt einen neuen.
    /// Level-Matching: Erst +-3, dann +-5, sonst Bye-Woche.
    /// </summary>
    private async Task<string?> FindOrCreateWarAsync(string seasonId, int weekNr)
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return null;

        var warPrefix = $"{seasonId}_w{weekNr}";
        var ownGuildId = membership.GuildId;
        var ownGuildName = membership.GuildName;
        var ownLevel = membership.GuildLevel;

        try
        {
            // Prüfe ob wir bereits in einem War dieser Woche sind
            var existingWar = await _firebase.GetAsync<GuildWar>($"guild_wars/{warPrefix}_{ownGuildId}");
            if (existingWar != null)
                return $"{warPrefix}_{ownGuildId}";

            // Suche nach offenen Wars mit "waiting" als GuildB
            var json = await _firebase.QueryAsync("guild_wars",
                $"orderBy=\"status\"&equalTo=\"active\"&limitToFirst=20");

            if (!string.IsNullOrEmpty(json))
            {
                var wars = JsonSerializer.Deserialize<Dictionary<string, GuildWar>>(json);
                if (wars != null)
                {
                    // Erste Runde: Enge Toleranz (+-3 Level)
                    foreach (var (warId, war) in wars)
                    {
                        if (!warId.StartsWith(warPrefix)) continue;
                        if (war.GuildBId != "waiting") continue;
                        if (war.GuildAId == ownGuildId) continue;

                        if (Math.Abs(war.GuildALevel - ownLevel) <= LevelMatchTolerance)
                        {
                            // Beitreten
                            await _firebase.UpdateAsync($"guild_wars/{warId}",
                                new Dictionary<string, object>
                                {
                                    ["guildBId"] = ownGuildId,
                                    ["guildBName"] = ownGuildName,
                                    ["guildBLevel"] = ownLevel
                                });
                            return warId;
                        }
                    }

                    // Zweite Runde: Erweiterte Toleranz (+-5 Level)
                    foreach (var (warId, war) in wars)
                    {
                        if (!warId.StartsWith(warPrefix)) continue;
                        if (war.GuildBId != "waiting") continue;
                        if (war.GuildAId == ownGuildId) continue;

                        if (Math.Abs(war.GuildALevel - ownLevel) <= LevelMatchToleranceExtended)
                        {
                            await _firebase.UpdateAsync($"guild_wars/{warId}",
                                new Dictionary<string, object>
                                {
                                    ["guildBId"] = ownGuildId,
                                    ["guildBName"] = ownGuildName,
                                    ["guildBLevel"] = ownLevel
                                });
                            return warId;
                        }
                    }
                }
            }

            // Kein passender War gefunden → neuen erstellen
            var newWarId = $"{warPrefix}_{ownGuildId}";
            var now = DateTime.UtcNow;
            var phase = GetCurrentPhase();

            var newWar = new GuildWar
            {
                GuildAId = ownGuildId,
                GuildAName = ownGuildName,
                GuildALevel = ownLevel,
                GuildBId = "waiting",
                GuildBName = "",
                GuildBLevel = 0,
                ScoreA = 0,
                ScoreB = 0,
                StartDate = GetWeekStartTime().ToString("O"),
                EndDate = GetWeekEndTime().ToString("O"),
                Status = "active",
                Phase = phase.ToString().ToLowerInvariant(),
                PhaseEndsAt = GetPhaseEndTime().ToString("O")
            };

            await _firebase.SetAsync($"guild_wars/{newWarId}", newWar);
            return newWarId;
        }
        catch (Exception ex)
        {
            _log.Error("Krieg suchen/erstellen fehlgeschlagen", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: SCORE-BERECHNUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet den Gesamt-Score einer Gilde durch Summierung aller Member-Scores.
    /// Race-Condition-frei: Jeder Spieler schreibt nur seinen eigenen Score.
    /// </summary>
    private async Task<long> CalculateGuildTotalScoreAsync(string warId, string guildId)
    {
        try
        {
            var json = await _firebase.QueryAsync(
                $"guild_war_scores/{warId}/{guildId}", "");

            if (string.IsNullOrEmpty(json))
                return 0;

            var scores = JsonSerializer.Deserialize<Dictionary<string, GuildWarPlayerScore>>(json);
            if (scores == null)
                return 0;

            return scores.Values.Sum(s => s.TotalScore);
        }
        catch (Exception ex)
        {
            _log.Error("Gilden-Score berechnen fehlgeschlagen", ex);
            return 0;
        }
    }

    /// <summary>
    /// Ermittelt den MVP (Spieler mit höchstem TotalScore) einer Gilde.
    /// </summary>
    private async Task<(string Name, long Score)> GetMvpAsync(string warId, string guildId)
    {
        try
        {
            var json = await _firebase.QueryAsync(
                $"guild_war_scores/{warId}/{guildId}", "");

            if (string.IsNullOrEmpty(json))
                return ("", 0);

            var scores = JsonSerializer.Deserialize<Dictionary<string, GuildWarPlayerScore>>(json);
            if (scores == null || scores.Count == 0)
                return ("", 0);

            // Höchsten Score finden
            var mvpEntry = scores.MaxBy(s => s.Value.TotalScore);
            if (mvpEntry.Value.TotalScore <= 0)
                return ("", 0);

            // Spielername aus guild_members laden (dort liegt der Name)
            var membership = _gameStateService.State.GuildMembership;
            var mvpName = mvpEntry.Key;
            if (membership != null && !string.IsNullOrEmpty(membership.GuildId))
            {
                var memberJson = await _firebase.QueryAsync(
                    $"guild_members/{membership.GuildId}/{mvpEntry.Key}", "");
                if (!string.IsNullOrEmpty(memberJson) && memberJson != "null")
                {
                    using var doc = JsonDocument.Parse(memberJson);
                    if (doc.RootElement.TryGetProperty("name", out var nameEl)
                        && nameEl.ValueKind == JsonValueKind.String)
                    {
                        mvpName = nameEl.GetString() ?? mvpEntry.Key;
                    }
                }
            }

            // Fallback: Spielername aus Preferences wenn eigener Spieler
            if (mvpName == mvpEntry.Key && mvpEntry.Key == _firebase.PlayerId)
                mvpName = GetPlayerName();

            return (mvpName, mvpEntry.Value.TotalScore);
        }
        catch (Exception ex)
        {
            _log.Error("MVP ermitteln fehlgeschlagen", ex);
            return ("", 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: BELOHNUNGEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verteilt Belohnungen am Ende eines Kriegs.
    /// Sieg: 20 GS + 3 Liga-Punkte, Unentschieden: 10 GS + 1 LP, Niederlage: 5 GS.
    /// MVP-Bonus: +5 GS. Alle 3 Bonus-Missionen: +3 GS.
    /// </summary>
    private async Task DistributeWarRewardsAsync(GuildWar war)
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        // Duplikat-Schutz: Pro War nur einmal belohnen
        var warRewardKey = $"{PrefKeyWarRewardPrefix}{_activeWarId}";
        if (_preferences.Get(warRewardKey, false)) return;

        var guildId = membership.GuildId;
        var isGuildA = war.GuildAId == guildId;

        // Gesamt-Scores berechnen
        var ownScore = await CalculateGuildTotalScoreAsync(_activeWarId ?? "", guildId);
        var opponentGuildId = isGuildA ? war.GuildBId : war.GuildAId;
        var opponentScore = 0L;
        if (!string.IsNullOrEmpty(opponentGuildId) && opponentGuildId != "waiting")
            opponentScore = await CalculateGuildTotalScoreAsync(_activeWarId ?? "", opponentGuildId);

        int gsReward;
        int leaguePoints;

        if (ownScore > opponentScore)
        {
            gsReward = WinRewardGs;
            leaguePoints = WinLeaguePoints;
        }
        else if (ownScore == opponentScore)
        {
            gsReward = DrawRewardGs;
            leaguePoints = DrawLeaguePoints;
        }
        else
        {
            gsReward = LossRewardGs;
            leaguePoints = 0;
        }

        // MVP-Bonus prüfen
        var uid = _firebase.PlayerId;
        if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(_activeWarId))
        {
            var (_, mvpScore) = await GetMvpAsync(_activeWarId, guildId);
            var ownPlayerScore = await _firebase.GetAsync<GuildWarPlayerScore>(
                $"guild_war_scores/{_activeWarId}/{guildId}/{uid}");
            if (ownPlayerScore != null && ownPlayerScore.TotalScore > 0 &&
                ownPlayerScore.TotalScore >= mvpScore)
            {
                gsReward += MvpBonusGs;
            }
        }

        // Alle-3-Bonus-Missionen abgeschlossen?
        var bonusMissions = await GetBonusMissionsAsync();
        if (bonusMissions.Count > 0 && bonusMissions.All(m => m.IsCompleted))
            gsReward += AllBonusMissionGs;

        // Belohnungen vergeben
        _gameStateService.AddGoldenScrews(gsReward);
        _preferences.Set(warRewardKey, true);

        // Liga-Eintrag aktualisieren (Punkte + Wins/Losses)
        {
            var seasonId = GetCurrentSeasonId();
            var leagueKey = _currentLeague.ToString().ToLowerInvariant();
            var entryPath = $"guild_war_seasons/{seasonId}/leagues/{leagueKey}/{guildId}";

            var entry = await _firebase.GetAsync<GuildLeagueEntry>(entryPath)
                        ?? new GuildLeagueEntry();

            entry.Points += leaguePoints;
            if (ownScore > opponentScore)
                entry.Wins++;
            else if (ownScore < opponentScore)
                entry.Losses++;

            await _firebase.SetAsync(entryPath, entry);
        }

        // War als beendet markieren
        if (!string.IsNullOrEmpty(_activeWarId))
        {
            await _firebase.UpdateAsync($"guild_wars/{_activeWarId}",
                new Dictionary<string, object>
                {
                    ["status"] = "completed",
                    ["phase"] = "completed"
                });
        }

        // Ergebnis-Log
        var resultLog = new GuildWarLogEntry
        {
            Type = "result",
            GuildId = guildId,
            PlayerName = "",
            Points = ownScore,
            Message = ownScore > opponentScore ? "win" :
                      ownScore == opponentScore ? "draw" : "loss",
            Timestamp = DateTime.UtcNow.ToString("O")
        };
        if (!string.IsNullOrEmpty(_activeWarId))
            await _firebase.PushAsync($"guild_war_log/{_activeWarId}", resultLog);
    }

    /// <summary>
    /// Verteilt Saison-End-Belohnungen basierend auf Liga.
    /// </summary>
    private void DistributeSeasonRewards()
    {
        // Duplikat-Schutz: Pro Saison nur einmal belohnen
        var seasonId = GetCurrentSeasonId();
        var seasonRewardKey = $"{PrefKeySeasonRewardPrefix}{seasonId}";
        if (_preferences.Get(seasonRewardKey, false)) return;

        var gsReward = _currentLeague switch
        {
            GuildLeague.Diamond => 100,
            GuildLeague.Gold => 50,
            GuildLeague.Silver => 25,
            GuildLeague.Bronze => 10,
            _ => 10
        };
        _gameStateService.AddGoldenScrews(gsReward);
        _preferences.Set(seasonRewardKey, true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: LIGA-AUF/ABSTIEG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet Liga-Auf/Abstieg basierend auf Liga-Punkten.
    /// Bronze Top30% → Silber, Silber Top25% → Gold, Silber Bottom25% → Bronze,
    /// Gold Top20% → Diamond, Gold Bottom30% → Silber, Diamond Bottom30% → Gold.
    /// </summary>
    private async Task ProcessLeaguePromotionAsync(string seasonId, string guildId)
    {
        var leagueKey = _currentLeague.ToString().ToLowerInvariant();
        var leaguePath = $"guild_war_seasons/{seasonId}/leagues/{leagueKey}";

        try
        {
            // Alle Gilden in der Liga laden
            var json = await _firebase.QueryAsync(leaguePath, "orderBy=\"points\"");
            if (string.IsNullOrEmpty(json))
                return;

            var entries = JsonSerializer.Deserialize<Dictionary<string, GuildLeagueEntry>>(json);
            if (entries == null || entries.Count <= 1)
                return;

            // Nach Punkten absteigend sortieren
            var sorted = entries.OrderByDescending(e => e.Value.Points).ToList();
            var totalGuilds = sorted.Count;
            var ownIndex = sorted.FindIndex(e => e.Key == guildId);
            if (ownIndex < 0) return;

            var ownPercentile = (double)(ownIndex + 1) / totalGuilds;
            var newLeague = _currentLeague;

            switch (_currentLeague)
            {
                case GuildLeague.Bronze:
                    // Top 30% steigt auf zu Silber
                    if (ownPercentile <= 0.30)
                        newLeague = GuildLeague.Silver;
                    break;

                case GuildLeague.Silver:
                    // Top 25% steigt auf zu Gold
                    if (ownPercentile <= 0.25)
                        newLeague = GuildLeague.Gold;
                    // Bottom 25% steigt ab zu Bronze
                    else if (ownPercentile > 0.75)
                        newLeague = GuildLeague.Bronze;
                    break;

                case GuildLeague.Gold:
                    // Top 20% steigt auf zu Diamond
                    if (ownPercentile <= 0.20)
                        newLeague = GuildLeague.Diamond;
                    // Bottom 30% steigt ab zu Silber
                    else if (ownPercentile > 0.70)
                        newLeague = GuildLeague.Silver;
                    break;

                case GuildLeague.Diamond:
                    // Bottom 30% steigt ab zu Gold
                    if (ownPercentile > 0.70)
                        newLeague = GuildLeague.Gold;
                    break;
            }

            if (newLeague != _currentLeague)
            {
                _currentLeague = newLeague;
                var newLeagueKey = newLeague.ToString().ToLowerInvariant();

                // Liga in Firebase updaten
                await _firebase.SetAsync($"guilds/{guildId}/leagueId", newLeagueKey);

                // Lokalen Cache updaten
                var membership = _gameStateService.State.GuildMembership;
                if (membership != null)
                    membership.LeagueId = newLeagueKey;
            }
        }
        catch (Exception ex)
        {
            _log.Error("Liga-Auf/Abstieg berechnen fehlgeschlagen", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: ZEIT-BERECHNUNGEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generiert die Saison-ID basierend auf ISO-Jahr und 4-Wochen-Block.
    /// Format: s_{year}_{number:D2}
    /// Verwendet ISOWeek statt GetWeekOfYear um Jahreswechsel-Probleme zu vermeiden
    /// (z.B. 29.12. kann ISO-Woche 1 des Folgejahres sein).
    /// </summary>
    private static string GetCurrentSeasonId()
    {
        var now = DateTime.UtcNow;
        var isoYear = ISOWeek.GetYear(now);
        var isoWeek = ISOWeek.GetWeekOfYear(now);
        var seasonNumber = (isoWeek - 1) / 4 + 1;
        return $"s_{isoYear}_{seasonNumber:D2}";
    }

    /// <summary>
    /// Gibt die aktuelle Woche innerhalb der Saison zurück (1-4).
    /// </summary>
    private static int GetCurrentWeekInSeason()
    {
        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now);
        return ((isoWeek - 1) % 4) + 1;
    }

    /// <summary>
    /// Gibt die fortlaufende Saison-Nummer zurück (für Anzeige).
    /// </summary>
    private static int GetSeasonNumber()
    {
        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now);
        return (isoWeek - 1) / 4 + 1;
    }

    /// <summary>
    /// Bestimmt die aktuelle Kriegsphase anhand des Wochentags (UTC).
    /// Mo-Mi: Angriff, Do-Fr: Verteidigung, Sa-So: Auswertung.
    /// </summary>
    private static WarPhase GetCurrentPhase()
    {
        var dow = DateTime.UtcNow.DayOfWeek;
        return dow switch
        {
            DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday => WarPhase.Attack,
            DayOfWeek.Thursday or DayOfWeek.Friday => WarPhase.Defense,
            _ => WarPhase.Evaluation
        };
    }

    /// <summary>
    /// Berechnet den Endzeitpunkt der aktuellen Phase.
    /// </summary>
    private static DateTime GetPhaseEndTime()
    {
        var now = DateTime.UtcNow;
        var dow = now.DayOfWeek;

        // Tage bis zum nächsten Phasenwechsel berechnen
        var daysUntilEnd = dow switch
        {
            // Angriff endet Donnerstag 00:00
            DayOfWeek.Monday => 3,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 1,
            // Verteidigung endet Samstag 00:00
            DayOfWeek.Thursday => 2,
            DayOfWeek.Friday => 1,
            // Auswertung endet Montag 00:00 (nächste Woche)
            DayOfWeek.Saturday => 2,
            DayOfWeek.Sunday => 1,
            _ => 1
        };

        return now.Date.AddDays(daysUntilEnd);
    }

    /// <summary>
    /// Gibt den Montag 00:00 UTC der aktuellen Woche zurück.
    /// </summary>
    private static DateTime GetWeekStartTime()
    {
        var now = DateTime.UtcNow;
        var daysToSubtract = now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)now.DayOfWeek - 1;
        return now.Date.AddDays(-daysToSubtract);
    }

    /// <summary>
    /// Gibt den Sonntag 23:59 UTC der aktuellen Woche zurück.
    /// </summary>
    private static DateTime GetWeekEndTime()
    {
        var start = GetWeekStartTime();
        return start.AddDays(7).AddSeconds(-1);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt den Spielernamen zurück (aus Preferences).
    /// </summary>
    private string GetPlayerName()
    {
        return _preferences.Get<string?>("guild_player_name", null) ?? "Unbekannt";
    }

    /// <summary>
    /// Parst einen Liga-String zu GuildLeague Enum.
    /// </summary>
    private static GuildLeague ParseLeague(string leagueId)
    {
        return leagueId.ToLowerInvariant() switch
        {
            "silver" => GuildLeague.Silver,
            "gold" => GuildLeague.Gold,
            "diamond" => GuildLeague.Diamond,
            _ => GuildLeague.Bronze
        };
    }

    /// <summary>
    /// Parst einen Phase-String zu WarPhase Enum.
    /// </summary>
    private static WarPhase ParseWarPhase(string phase)
    {
        return phase.ToLowerInvariant() switch
        {
            "defense" => WarPhase.Defense,
            "evaluation" => WarPhase.Evaluation,
            "completed" => WarPhase.Completed,
            _ => WarPhase.Attack
        };
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
