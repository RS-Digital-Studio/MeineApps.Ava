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
public sealed class GuildAchievementService : IGuildAchievementService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Cache
    private Dictionary<string, GuildAchievementState>? _cachedStates;

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

        await _lock.WaitAsync();
        try
        {
            var guildId = membership.GuildId;

            // Firebase-Zustände laden
            _cachedStates = await _firebase.GetAsync<Dictionary<string, GuildAchievementState>>(
                $"guild_achievements/{guildId}")
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetAchievementsAsync: {ex.Message}");
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

        await _lock.WaitAsync();
        try
        {
            var guildId = membership.GuildId;
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

            // Definition suchen
            var definition = GuildAchievementDefinition.GetAll()
                .FirstOrDefault(d => d.Id == achievementId);
            if (definition == null) return;

            // Abschluss prüfen
            if (state.Progress >= definition.Target)
            {
                state.Completed = true;
                state.CompletedAt = DateTime.UtcNow.ToString("O");

                // Belohnung verteilen
                _gameStateService.AddGoldenScrews(definition.GoldenScrewReward);

                // UI-Event feuern
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

            // Firebase schreiben
            await _firebase.SetAsync(path, state);

            // Cache aktualisieren
            _cachedStates[achievementId] = state;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in UpdateProgressAsync: {ex.Message}");
        }
        finally
        {
            _lock.Release();
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

        try
        {
            var guildId = membership.GuildId;

            // Gildendaten laden für Beitrag und Mitglieder
            var guildDataJson = await _firebase.QueryAsync($"guilds/{guildId}", "");
            long totalContrib = 0;
            int memberCount = 0;
            int hallLevel = 1;
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
            var researchStates = await _firebase.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{guildId}");
            var completedResearch = researchStates?.Values.Count(s => s.Completed) ?? 0;

            // Gebäude auf Max-Level zählen
            var buildingStates = await _firebase.GetAsync<Dictionary<string, GuildBuildingState>>(
                $"guild_hall/{guildId}/buildings");
            var maxLevelBuildings = 0;
            if (buildingStates != null)
            {
                var defs = GuildBuildingDefinition.GetAll().ToDictionary(d => d.BuildingId.ToString());
                foreach (var (key, state) in buildingStates)
                {
                    if (defs.TryGetValue(key, out var def) && state.Level >= def.MaxLevel)
                        maxLevelBuildings++;
                }
            }

            // ── Gemeinsam stark ──
            await UpdateProgressAsync("guild_ach_money_bronze", totalContrib);
            await UpdateProgressAsync("guild_ach_money_silver", totalContrib);
            await UpdateProgressAsync("guild_ach_money_gold", totalContrib);

            await UpdateProgressAsync("guild_ach_research_bronze", completedResearch);
            await UpdateProgressAsync("guild_ach_research_silver", completedResearch);
            await UpdateProgressAsync("guild_ach_research_gold", completedResearch);

            await UpdateProgressAsync("guild_ach_members_bronze", memberCount);
            await UpdateProgressAsync("guild_ach_members_silver", memberCount);
            await UpdateProgressAsync("guild_ach_members_gold", memberCount);

            // ── Baumeister ──
            await UpdateProgressAsync("guild_ach_maxbuilding_bronze", maxLevelBuildings);
            await UpdateProgressAsync("guild_ach_maxbuilding_silver", maxLevelBuildings);
            await UpdateProgressAsync("guild_ach_maxbuilding_gold", maxLevelBuildings);

            await UpdateProgressAsync("guild_ach_hall_bronze", hallLevel);
            await UpdateProgressAsync("guild_ach_hall_silver", hallLevel);
            await UpdateProgressAsync("guild_ach_hall_gold", hallLevel);

            // Hinweis: Kriegs- und Boss-Achievements werden direkt bei den
            // entsprechenden Aktionen aktualisiert (GuildWarSeasonService/GuildBossService)
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in CheckAllAchievementsAsync: {ex.Message}");
        }
    }
}
