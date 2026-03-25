using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet 30 Gilden-Achievements (10 Typen × 3 Tiers).
/// Fortschritt wird in Firebase gespeichert und bei relevanten Aktionen aktualisiert.
/// Race-Condition-frei: Fortschritt wird nur client-seitig geprüft und bei Abschluss geschrieben.
/// Firebase-Pfad: guild_achievements/{guildId}/{achievementId}/
/// </summary>
public sealed class GuildAchievementService : IGuildAchievementService, IDisposable
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Cache
    private Dictionary<string, GuildAchievementState>? _cachedStates;
    // Definitionen-Lookup (einmalig erstellt, vermeidet 15x GetAll().FirstOrDefault() pro Check-Cycle)
    private static readonly Dictionary<string, GuildAchievementDefinition> _definitionLookup =
        GuildAchievementDefinition.GetAll().ToDictionary(d => d.Id);
    // Gebaeude-Definitionen Lookup (einmalig, vermeidet GetAll().ToDictionary() alle 300s)
    private static readonly Dictionary<string, GuildBuildingDefinition> s_buildingDefLookup =
        GuildBuildingDefinition.GetAll().ToDictionary(d => d.BuildingId.ToString());

    /// <summary>Feuert wenn ein Achievement abgeschlossen wurde (für UI-Celebration).</summary>
    public event Action<GuildAchievementDisplay>? AchievementCompleted;

    public GuildAchievementService(
        IFirebaseService firebase,
        IGameStateService gameStateService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENTS LADEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lädt alle 30 Gilden-Achievements mit aktuellem Fortschritt.
    /// Merged statische Definitionen mit Firebase-Zuständen.
    /// </summary>
    public async Task<List<GuildAchievementDisplay>> GetAchievementsAsync()
    {
        var result = new List<GuildAchievementDisplay>();

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return result;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return result; // Timeout: Lock nicht erhalten
        try
        {
            var guildId = membership.GuildId;

            // Firebase-Zustände laden
            _cachedStates = await _firebase.GetAsync<Dictionary<string, GuildAchievementState>>(
                $"guild_achievements/{guildId}").ConfigureAwait(false)
                ?? new Dictionary<string, GuildAchievementState>();

            var definitions = GuildAchievementDefinition.GetAll();

            foreach (var def in definitions)
            {
                _cachedStates.TryGetValue(def.Id, out var state);

                result.Add(new GuildAchievementDisplay
                {
                    Id = def.Id,
                    Name = def.NameKey,
                    Description = def.DescKey,
                    Icon = def.Icon,
                    Category = def.Category,
                    Tier = def.Tier,
                    Target = def.Target,
                    Progress = state?.Progress ?? 0,
                    IsCompleted = state?.Completed ?? false,
                    GoldenScrewReward = def.GoldenScrewReward,
                    CosmeticReward = def.CosmeticReward
                });
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

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITT AKTUALISIEREN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert den Fortschritt eines bestimmten Achievements.
    /// Prüft auf Abschluss und verteilt Belohnungen.
    /// </summary>
    public async Task UpdateProgressAsync(string achievementId, long progress)
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            await UpdateProgressCoreAsync(achievementId, progress, membership.GuildId).ConfigureAwait(false);
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

    /// <summary>
    /// Interne Update-Logik OHNE Lock-Erwerb. Wird von UpdateProgressAsync (mit Lock)
    /// und CheckAllAchievementsAsync (hält bereits den Lock) aufgerufen.
    /// </summary>
    private async Task UpdateProgressCoreAsync(string achievementId, long progress, string guildId)
    {
        var path = $"guild_achievements/{guildId}/{achievementId}";

        // Aktuellen Zustand laden
        _cachedStates ??= new Dictionary<string, GuildAchievementState>();
        _cachedStates.TryGetValue(achievementId, out var state);
        state ??= new GuildAchievementState();

        // Bereits abgeschlossen? Überspringen
        if (state.Completed) return;

        // Fortschritt nur erhöhen, nie verringern
        if (progress <= state.Progress) return;
        state.Progress = progress;

        // Definition aus gecachtem Dictionary suchen (kein LINQ)
        if (!_definitionLookup.TryGetValue(achievementId, out var definition)) return;

        // Abschluss prüfen
        var justCompleted = false;
        if (state.Progress >= definition.Target)
        {
            state.Completed = true;
            state.CompletedAt = DateTime.UtcNow.ToString("O");
            justCompleted = true;
        }

        // Cache aktualisieren + Firebase schreiben
        var previousState = _cachedStates.GetValueOrDefault(achievementId);
        _cachedStates[achievementId] = state;

        try
        {
            await _firebase.SetAsync(path, state).ConfigureAwait(false);
        }
        catch
        {
            // Firebase-Fehler: Cache zurücksetzen damit Belohnung beim nächsten Check vergeben wird
            if (previousState != null)
                _cachedStates[achievementId] = previousState;
            else
                _cachedStates.Remove(achievementId);
            return;
        }

        // Belohnung NUR nach erfolgreichem Firebase-Write verteilen
        if (justCompleted)
        {
            _gameStateService.AddGoldenScrews(definition.GoldenScrewReward);

            AchievementCompleted?.Invoke(new GuildAchievementDisplay
            {
                Id = definition.Id,
                Name = definition.NameKey,
                Description = definition.DescKey,
                Icon = definition.Icon,
                Category = definition.Category,
                Tier = definition.Tier,
                Target = definition.Target,
                Progress = state.Progress,
                IsCompleted = true,
                GoldenScrewReward = definition.GoldenScrewReward,
                CosmeticReward = definition.CosmeticReward
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ALLE ACHIEVEMENTS PRÜFEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft alle Achievements auf Abschluss. Berechnet aktuellen Fortschritt
    /// aus verschiedenen Quellen (Gildenlevel, Forschungen, Mitglieder, etc.).
    /// Wird periodisch aufgerufen (z.B. beim Öffnen des Achievement-Tabs).
    /// </summary>
    public async Task CheckAllAchievementsAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten, überspringen
        try
        {
            var guildId = membership.GuildId;

            // 3 Firebase-Requests parallel statt sequentiell
            var guildDataTask = _firebase.QueryAsync($"guilds/{guildId}", "");
            var researchTask = _firebase.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{guildId}");
            var buildingTask = _firebase.GetAsync<Dictionary<string, GuildBuildingState>>(
                $"guild_hall/{guildId}/buildings");
            await Task.WhenAll(guildDataTask, researchTask, buildingTask).ConfigureAwait(false);

            // Gildendaten parsen
            long totalContrib = 0;
            int memberCount = 0;
            int hallLevel = 1;
            var guildDataJson = guildDataTask.Result;
            if (!string.IsNullOrEmpty(guildDataJson))
            {
                var guildData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(guildDataJson);
                if (guildData != null)
                {
                    if (guildData.TryGetValue("weeklyProgress", out var wp))
                        totalContrib = wp.TryGetInt64(out var tc) ? tc : 0;
                    if (guildData.TryGetValue("memberCount", out var mc))
                        memberCount = mc.TryGetInt32(out var m) ? m : 0;
                    if (guildData.TryGetValue("hallLevel", out var hlEl))
                        hallLevel = hlEl.TryGetInt32(out var h) ? h : 1;
                }
            }

            // Abgeschlossene Forschungen zählen
            var researchStates = researchTask.Result;
            var completedResearch = researchStates?.Values.Count(s => s.Completed) ?? 0;

            // Gebäude auf Max-Level zählen
            var buildingStates = buildingTask.Result;
            var maxLevelBuildings = 0;
            if (buildingStates != null)
            {
                // Statisches Lookup-Dictionary verwenden (kein GetAll().ToDictionary() alle 300s)
                foreach (var (key, state) in buildingStates)
                {
                    if (s_buildingDefLookup.TryGetValue(key, out var def) && state.Level >= def.MaxLevel)
                        maxLevelBuildings++;
                }
            }

            // ── Gemeinsam stark ── (nutzt UpdateProgressCoreAsync statt UpdateProgressAsync um Deadlock zu vermeiden)
            await UpdateProgressCoreAsync("guild_ach_money_bronze", totalContrib, guildId);
            await UpdateProgressCoreAsync("guild_ach_money_silver", totalContrib, guildId);
            await UpdateProgressCoreAsync("guild_ach_money_gold", totalContrib, guildId);

            await UpdateProgressCoreAsync("guild_ach_research_bronze", completedResearch, guildId);
            await UpdateProgressCoreAsync("guild_ach_research_silver", completedResearch, guildId);
            await UpdateProgressCoreAsync("guild_ach_research_gold", completedResearch, guildId);

            await UpdateProgressCoreAsync("guild_ach_members_bronze", memberCount, guildId);
            await UpdateProgressCoreAsync("guild_ach_members_silver", memberCount, guildId);
            await UpdateProgressCoreAsync("guild_ach_members_gold", memberCount, guildId);

            // ── Baumeister ──
            await UpdateProgressCoreAsync("guild_ach_maxbuilding_bronze", maxLevelBuildings, guildId);
            await UpdateProgressCoreAsync("guild_ach_maxbuilding_silver", maxLevelBuildings, guildId);
            await UpdateProgressCoreAsync("guild_ach_maxbuilding_gold", maxLevelBuildings, guildId);

            await UpdateProgressCoreAsync("guild_ach_hall_bronze", hallLevel, guildId);
            await UpdateProgressCoreAsync("guild_ach_hall_silver", hallLevel, guildId);
            await UpdateProgressCoreAsync("guild_ach_hall_gold", hallLevel, guildId);

            // Hinweis: Kriegs- und Boss-Achievements werden direkt bei den
            // entsprechenden Aktionen aktualisiert (GuildWarSeasonService/GuildBossService)
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

    public void Dispose()
    {
        _lock.Dispose();
    }
}
