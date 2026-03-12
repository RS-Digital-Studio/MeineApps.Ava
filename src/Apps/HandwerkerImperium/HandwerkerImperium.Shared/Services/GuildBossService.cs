using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet kooperative Gilden-Bosse (wöchentlich, 6 rotierende Typen).
/// Mitglieder fügen durch Spielaktionen Schaden zu. Race-Condition-frei:
/// Jeder Spieler schreibt nur seinen eigenen Damage-Eintrag.
/// HP wird client-seitig aggregiert: currentHp = bossHp - SUM(allDamage).
/// Firebase-Pfade: guild_bosses/{guildId}/, guild_boss_damage/{guildId}/{uid}/
/// </summary>
public sealed class GuildBossService : IGuildBossService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly IPreferencesService _preferences;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Cache
    private FirebaseGuildBoss? _cachedBoss;
    private GuildBossDefinition? _cachedDefinition;
    private DateTime _lastBossCheck = DateTime.MinValue;

    // Belohnungen
    private const int MvpRewardGs = 30;
    private const int Top3RewardGs = 20;
    private const int ParticipantRewardGs = 10;

    // Preferences-Keys
    private const string PrefKeyLastBossRewardWeek = "guild_boss_reward_week";

    public GuildBossService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        IPreferencesService preferences)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _preferences = preferences;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AKTIVEN BOSS LADEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lädt den aktuell aktiven Boss mit Leaderboard und eigenem Beitrag.
    /// Berechnet HP durch Aggregation aller Member-Damage-Einträge.
    /// </summary>
    public async Task<GuildBossDisplayData?> GetActiveBossAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return null;

        await _lock.WaitAsync();
        try
        {
            var guildId = membership.GuildId;
            var boss = await _firebase.GetAsync<FirebaseGuildBoss>($"guild_bosses/{guildId}");

            if (boss == null || boss.Status != "active")
                return null;

            _cachedBoss = boss;

            // Boss-Definition laden
            if (!Enum.TryParse<GuildBossType>(boss.BossId, true, out var bossType))
                return null;

            var definition = GuildBossDefinition.GetAll().FirstOrDefault(d => d.BossType == bossType);
            if (definition == null) return null;
            _cachedDefinition = definition;

            // Gesamtschaden aggregieren (Race-Condition-frei)
            var totalDamage = await CalculateTotalDamageAsync(guildId);
            var currentHp = Math.Max(0, boss.BossHp - totalDamage);

            // Eigenen Beitrag laden
            var uid = _firebase.PlayerId;
            GuildBossDamage? ownDamage = null;
            if (!string.IsNullOrEmpty(uid))
                ownDamage = await _firebase.GetAsync<GuildBossDamage>(
                    $"guild_boss_damage/{guildId}/{uid}");

            // Leaderboard laden
            var leaderboard = await GetLeaderboardAsync();

            // Eigenen Rang berechnen (Anzahl Spieler mit mehr Schaden + 1)
            var ownRank = 0;
            if (ownDamage != null && ownDamage.TotalDamage > 0)
                ownRank = leaderboard.Count(e => e.Damage > ownDamage.TotalDamage) + 1;

            // ExpiresAt parsen
            DateTime.TryParse(boss.ExpiresAt, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expiresAt);

            return new GuildBossDisplayData
            {
                BossType = bossType,
                BossName = definition.NameKey,
                MaxHp = boss.BossHp,
                CurrentHp = currentHp,
                ExpiresAt = boss.ExpiresAt,
                Status = BossStatus.Active,
                OwnDamage = ownDamage?.TotalDamage ?? 0,
                OwnHits = ownDamage?.Hits ?? 0,
                OwnRank = ownRank,
                Leaderboard = leaderboard
            };
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

    // ═══════════════════════════════════════════════════════════════════════
    // SCHADEN ZUFÜGEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fügt Schaden am aktiven Boss hinzu. Berechnet Multiplikatoren basierend
    /// auf Boss-Typ (z.B. Eisentitan: Crafting 2x) und Hall-Boni.
    /// Schreibt NUR den eigenen Damage-Eintrag (Race-Condition-frei).
    /// </summary>
    public async Task DealDamageAsync(long damage, string source)
    {
        if (damage <= 0) return;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        var uid = _firebase.PlayerId;
        if (string.IsNullOrEmpty(uid))
            return;

        await _lock.WaitAsync();
        try
        {
            var guildId = membership.GuildId;

            // Boss aus Firebase laden falls nicht gecacht (z.B. nach App-Neustart)
            if (_cachedBoss == null || _cachedBoss.Status != "active")
            {
                _cachedBoss = await _firebase.GetAsync<FirebaseGuildBoss>($"guild_bosses/{guildId}");
                if (_cachedBoss == null || _cachedBoss.Status != "active") return;

                if (Enum.TryParse<GuildBossType>(_cachedBoss.BossId, true, out var bt))
                    _cachedDefinition = GuildBossDefinition.GetAll().FirstOrDefault(d => d.BossType == bt);
            }

            // Boss-spezifische Multiplikatoren anwenden
            var effectiveDamage = damage;
            if (_cachedDefinition != null)
            {
                var multiplier = source.ToLowerInvariant() switch
                {
                    "crafting" => _cachedDefinition.CraftingDamageMultiplier,
                    "order" or "orders" => _cachedDefinition.OrderDamageMultiplier,
                    "minigame" or "minigames" => _cachedDefinition.MiniGameDamageMultiplier,
                    "donation" or "donations" => _cachedDefinition.MoneyDonationDamageMultiplier,
                    _ => 1m
                };

                effectiveDamage = (long)(damage * multiplier);
            }

            // Eigenen Damage-Eintrag laden und aktualisieren
            var damagePath = $"guild_boss_damage/{guildId}/{uid}";
            var ownDamage = await _firebase.GetAsync<GuildBossDamage>(damagePath)
                            ?? new GuildBossDamage();

            ownDamage.TotalDamage += effectiveDamage;
            ownDamage.Hits++;
            ownDamage.LastHitAt = DateTime.UtcNow.ToString("O");

            // Nur eigenen Eintrag schreiben (Race-Condition-frei)
            await _firebase.SetAsync(damagePath, ownDamage);
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS-STATUS PRÜFEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob der Boss besiegt (HP <= 0) oder abgelaufen (expiresAt überschritten) ist.
    /// Verteilt bei Sieg Belohnungen: MVP 30 GS, Top 3 20 GS, Teilnahme 10 GS.
    /// </summary>
    public async Task CheckBossStatusAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        // Nicht zu oft prüfen (max alle 30 Sekunden)
        if (DateTime.UtcNow - _lastBossCheck < TimeSpan.FromSeconds(30))
            return;
        _lastBossCheck = DateTime.UtcNow;

        await _lock.WaitAsync();
        try
        {
            var guildId = membership.GuildId;
            var boss = await _firebase.GetAsync<FirebaseGuildBoss>($"guild_bosses/{guildId}");

            if (boss == null || boss.Status != "active")
                return;

            _cachedBoss = boss;
            var now = DateTime.UtcNow;

            // Ablauf prüfen
            if (DateTime.TryParse(boss.ExpiresAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var expiresAt) && now >= expiresAt)
            {
                // Boss abgelaufen
                await _firebase.UpdateAsync($"guild_bosses/{guildId}",
                    new Dictionary<string, object> { ["status"] = "expired" });
                _cachedBoss = null;
                return;
            }

            // HP prüfen
            var totalDamage = await CalculateTotalDamageAsync(guildId);
            if (totalDamage >= boss.BossHp)
            {
                // Boss besiegt!
                await _firebase.UpdateAsync($"guild_bosses/{guildId}",
                    new Dictionary<string, object>
                    {
                        ["status"] = "defeated",
                        ["currentHp"] = 0
                    });

                // Belohnungen verteilen
                await DistributeBossRewardsAsync(guildId);
                _cachedBoss = null;
            }
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS SPAWNEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawnt einen neuen Boss falls keiner aktiv ist.
    /// Boss-Typ rotiert wöchentlich (weekNumber % 6).
    /// HP skaliert mit Gildenlevel: HpPerLevel × GuildLevel.
    /// </summary>
    public async Task<bool> SpawnBossIfNeededAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return false;

        await _lock.WaitAsync();
        try
        {
            var guildId = membership.GuildId;

            // Prüfe ob bereits ein aktiver Boss existiert
            var existing = await _firebase.GetAsync<FirebaseGuildBoss>($"guild_bosses/{guildId}");
            if (existing != null && existing.Status == "active")
                return false;

            // Boss-Typ basierend auf Kalenderwoche bestimmen
            var cal = CultureInfo.InvariantCulture.Calendar;
            var weekNumber = cal.GetWeekOfYear(DateTime.UtcNow,
                CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var allBosses = GuildBossDefinition.GetAll();
            var bossIndex = weekNumber % allBosses.Count;
            var definition = allBosses[bossIndex];

            // HP berechnen
            var guildLevel = Math.Max(1, membership.GuildLevel);
            var bossHp = definition.CalculateHp(guildLevel);

            var now = DateTime.UtcNow;
            var newBoss = new FirebaseGuildBoss
            {
                BossId = definition.BossType.ToString(),
                BossHp = bossHp,
                CurrentHp = bossHp,
                BossLevel = guildLevel,
                StartedAt = now.ToString("O"),
                ExpiresAt = now.AddHours(definition.DurationHours).ToString("O"),
                Status = "active"
            };

            await _firebase.SetAsync($"guild_bosses/{guildId}", newBoss);

            // Alte Damage-Einträge löschen
            await _firebase.DeleteAsync($"guild_boss_damage/{guildId}");

            _cachedBoss = newBoss;
            _cachedDefinition = definition;

            return true;
        }
        catch
        {
            // Netzwerkfehler still behandelt
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LEADERBOARD
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lädt das Schadens-Leaderboard aus allen Member-Damage-Einträgen.
    /// Sortiert nach Gesamtschaden absteigend.
    /// </summary>
    public async Task<List<BossDamageEntry>> GetLeaderboardAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return [];

        try
        {
            var guildId = membership.GuildId;
            var json = await _firebase.QueryAsync($"guild_boss_damage/{guildId}", "");

            if (string.IsNullOrEmpty(json))
                return [];

            var damages = JsonSerializer.Deserialize<Dictionary<string, GuildBossDamage>>(json);
            if (damages == null || damages.Count == 0)
                return [];

            // Alle Mitglied-Namen in einem Request laden (statt N+1)
            var membersJson = await _firebase.QueryAsync($"guild_members/{guildId}", "");
            var memberNames = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(membersJson))
            {
                var members = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(membersJson);
                if (members != null)
                {
                    foreach (var (uid, member) in members)
                    {
                        if (member.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                            memberNames[uid] = nameEl.GetString() ?? uid;
                    }
                }
            }

            var leaderboard = new List<BossDamageEntry>();

            foreach (var (uid, dmg) in damages)
            {
                if (dmg.TotalDamage <= 0) continue;

                leaderboard.Add(new BossDamageEntry
                {
                    PlayerName = memberNames.GetValueOrDefault(uid, uid),
                    Damage = dmg.TotalDamage,
                    Hits = dmg.Hits
                });
            }

            return leaderboard.OrderByDescending(e => e.Damage).ToList();
        }
        catch
        {
            // Netzwerkfehler still behandelt
            return [];
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: SCHADEN AGGREGIEREN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet den Gesamtschaden aller Gildenmitglieder.
    /// Race-Condition-frei: Jeder Spieler hat seinen eigenen Eintrag.
    /// </summary>
    private async Task<long> CalculateTotalDamageAsync(string guildId)
    {
        try
        {
            var json = await _firebase.QueryAsync($"guild_boss_damage/{guildId}", "");

            if (string.IsNullOrEmpty(json))
                return 0;

            var damages = JsonSerializer.Deserialize<Dictionary<string, GuildBossDamage>>(json);
            return damages?.Values.Sum(d => d.TotalDamage) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: BELOHNUNGEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verteilt Boss-Belohnungen: MVP 30 GS + Cosmetic, Top 3 20 GS, Teilnahme 10 GS.
    /// Duplikat-Schutz über Preferences (Wochen-Key).
    /// </summary>
    private async Task DistributeBossRewardsAsync(string guildId)
    {
        var uid = _firebase.PlayerId;
        if (string.IsNullOrEmpty(uid)) return;

        // Duplikat-Schutz: Nur einmal pro Woche belohnen
        var cal = CultureInfo.InvariantCulture.Calendar;
        var weekNum = cal.GetWeekOfYear(DateTime.UtcNow,
            CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var weekKey = $"{DateTime.UtcNow.Year}-{weekNum:D2}";
        var rewardKey = $"{PrefKeyLastBossRewardWeek}_{weekKey}";
        if (_preferences.Get(rewardKey, false)) return;

        try
        {
            var leaderboard = await GetLeaderboardAsync();
            if (leaderboard.Count == 0) return;

            // Eigenen Schaden aus dem bereits geladenen Leaderboard extrahieren (kein extra Firebase-Call)
            var playerName = _gameStateService.State.PlayerName ?? uid;
            var ownEntry = leaderboard.FirstOrDefault(e =>
                e.PlayerName == playerName || e.PlayerName == uid);
            if (ownEntry == null || ownEntry.Damage <= 0) return;

            var ownRank = leaderboard.Count(e => e.Damage > ownEntry.Damage) + 1;

            var gsReward = ownRank switch
            {
                1 => MvpRewardGs,      // MVP: 30 GS + Cosmetic
                <= 3 => Top3RewardGs,   // Top 3: 20 GS
                _ => ParticipantRewardGs // Teilnahme: 10 GS
            };

            _gameStateService.AddGoldenScrews(gsReward);
            _preferences.Set(rewardKey, true);
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }
    }
}
