using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet das interaktive Gilden-Hauptquartier mit 10 Gebäuden.
/// Jedes Gebäude hat Level (1-5) und gibt permanente Boni.
/// Upgrade-Timer (1h/4h/12h), Kosten: Goldschrauben + Gildengeld.
/// Firebase-Pfad: guild_hall/{guildId}/buildings/{buildingId}/
/// Effekte werden lokal auf GuildMembership gecacht.
/// </summary>
public sealed class GuildHallService : IGuildHallService, IDisposable
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Cache
    private GuildHallEffects _cachedEffects = new();
    private int _hallLevel = 1;
    private Dictionary<string, GuildBuildingState>? _cachedStates;

    // Upgrade-Timer-Dauern pro Kosten-Tier (Stunden)
    private static readonly Dictionary<int, double> UpgradeDurations = new()
    {
        { 1, 1.0 },  // Tier 1: 1 Stunde
        { 2, 2.0 },  // Tier 2: 2 Stunden
        { 3, 4.0 },  // Tier 3: 4 Stunden
        { 4, 8.0 },  // Tier 4: 8 Stunden
        { 5, 12.0 }, // Tier 5: 12 Stunden
    };

    public GuildHallService(
        IFirebaseService firebase,
        IGameStateService gameStateService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GEBÄUDE LADEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lädt alle 10 Gebäude mit aktuellem Level, Upgrade-Status und Kosten.
    /// Merged statische Definitionen mit Firebase-Zuständen.
    /// </summary>
    public async Task<List<GuildBuildingDisplay>> GetBuildingsAsync()
    {
        var result = new List<GuildBuildingDisplay>();

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return result;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return result; // Timeout: Lock nicht erhalten
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var guildId = membership.GuildId;

            // Hallen-Level laden
            var hallLevelRaw = await _firebase.GetAsync<string>($"guilds/{guildId}/hallLevel");
            _hallLevel = int.TryParse(hallLevelRaw, out var hl) ? Math.Max(1, hl) : 1;

            // Gebäude-Zustände laden
            var statesRaw = await _firebase.GetAsync<Dictionary<string, GuildBuildingState>>(
                $"guild_hall/{guildId}/buildings");
            _cachedStates = statesRaw ?? new Dictionary<string, GuildBuildingState>();

            var definitions = GuildBuildingDefinition.GetAll();

            foreach (var def in definitions)
            {
                var stateKey = def.BuildingId.ToString();
                _cachedStates.TryGetValue(stateKey, out var state);

                var currentLevel = state?.Level ?? 0;
                var isUnlocked = _hallLevel >= def.UnlockHallLevel;

                // Upgrade-Timer prüfen
                var isUpgrading = false;
                DateTime? upgradeCompleteAt = null;
                if (state != null && !string.IsNullOrEmpty(state.UpgradingUntil))
                {
                    if (DateTime.TryParse(state.UpgradingUntil, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var until))
                    {
                        if (until > DateTime.UtcNow)
                        {
                            isUpgrading = true;
                            upgradeCompleteAt = until;
                        }
                    }
                }

                // Nächste Upgrade-Kosten
                GuildBuildingCost? nextCost = null;
                if (currentLevel < def.MaxLevel)
                    nextCost = def.GetUpgradeCost(currentLevel + 1);

                result.Add(new GuildBuildingDisplay
                {
                    BuildingId = def.BuildingId,
                    Name = def.NameKey,
                    Description = def.DescKey,
                    EffectDescription = def.EffectKey,
                    Icon = def.Icon,
                    Color = def.Color,
                    CurrentLevel = currentLevel,
                    MaxLevel = def.MaxLevel,
                    UnlockHallLevel = def.UnlockHallLevel,
                    IsUnlocked = isUnlocked,
                    IsUpgrading = isUpgrading,
                    UpgradeCompleteAt = upgradeCompleteAt?.ToString("O") ?? "",
                    NextUpgradeCost = nextCost
                });
            }

            // Effekte berechnen und cachen
            RecalculateEffects(definitions);
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
    // GEBÄUDE UPGRADEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Startet ein Gebäude-Upgrade. Prüft Kosten (GS + Gildengeld),
    /// Hallen-Level-Voraussetzung und ob bereits ein Upgrade läuft.
    /// Setzt upgradingUntil-Timer in Firebase.
    /// </summary>
    public async Task<bool> UpgradeBuildingAsync(GuildBuildingId buildingId)
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return false;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return false; // Timeout: Lock nicht erhalten
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var guildId = membership.GuildId;
            var stateKey = buildingId.ToString();

            // Definition suchen
            var definition = GuildBuildingDefinition.GetAll()
                .FirstOrDefault(d => d.BuildingId == buildingId);
            if (definition == null) return false;

            // Hallen-Level-Voraussetzung
            if (_hallLevel < definition.UnlockHallLevel) return false;

            // Aktuellen Zustand laden
            _cachedStates ??= new Dictionary<string, GuildBuildingState>();
            _cachedStates.TryGetValue(stateKey, out var state);
            state ??= new GuildBuildingState();

            // Max-Level erreicht?
            if (state.Level >= definition.MaxLevel) return false;

            // Bereits im Upgrade?
            if (state.IsUpgrading) return false;

            // Kosten berechnen
            var cost = definition.GetUpgradeCost(state.Level + 1);

            // Goldschrauben prüfen und abziehen
            if (!_gameStateService.CanAffordGoldenScrews(cost.GoldenScrews))
                return false;

            // Gildengeld prüfen (aus Gilden-Kasse)
            // Für MVP: Gildengeld wird aus dem Wochenziel-Beitrag genommen
            // Vereinfacht: Gildengeld wird als lokales Geld abgezogen
            if (!_gameStateService.CanAfford(cost.GuildMoney))
                return false;

            // Upgrade-Timer berechnen
            var tier = Math.Min(state.Level + 1, 5);
            var durationHours = UpgradeDurations.GetValueOrDefault(tier, 1.0);
            var upgradeEnd = DateTime.UtcNow.AddHours(durationHours);

            // Kosten VOR Firebase-Write abziehen (bei Firebase-Fehler zurückgeben)
            if (!_gameStateService.TrySpendGoldenScrews(cost.GoldenScrews))
                return false;
            if (!_gameStateService.TrySpendMoney(cost.GuildMoney))
            {
                // Rollback: Goldschrauben zurückgeben
                _gameStateService.AddGoldenScrews(cost.GoldenScrews);
                return false;
            }

            // Firebase aktualisieren
            state.UpgradingUntil = upgradeEnd.ToString("O");
            if (string.IsNullOrEmpty(state.UnlockedAt))
                state.UnlockedAt = DateTime.UtcNow.ToString("O");

            try
            {
                await _firebase.SetAsync($"guild_hall/{guildId}/buildings/{stateKey}", state);
            }
            catch
            {
                // Firebase-Fehler: Kosten zurückgeben
                _gameStateService.AddGoldenScrews(cost.GoldenScrews);
                _gameStateService.AddMoney(cost.GuildMoney);
                return false;
            }

            // Cache aktualisieren
            _cachedStates[stateKey] = state;

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
    // UPGRADE-COMPLETION PRÜFEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob laufende Gebäude-Upgrades abgeschlossen sind (Timer abgelaufen).
    /// Erhöht Level und löscht upgradingUntil. Aktualisiert Effekt-Cache.
    /// </summary>
    public async Task CheckUpgradeCompletionAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var guildId = membership.GuildId;
            var statesRaw = await _firebase.GetAsync<Dictionary<string, GuildBuildingState>>(
                $"guild_hall/{guildId}/buildings");

            if (statesRaw == null) return;
            _cachedStates = statesRaw;

            var now = DateTime.UtcNow;
            var anyCompleted = false;

            foreach (var (key, state) in statesRaw)
            {
                if (string.IsNullOrEmpty(state.UpgradingUntil)) continue;
                if (!DateTime.TryParse(state.UpgradingUntil, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var until))
                    continue;

                if (now >= until)
                {
                    // Upgrade abgeschlossen
                    state.Level++;
                    state.UpgradingUntil = "";
                    await _firebase.SetAsync($"guild_hall/{guildId}/buildings/{key}", state);
                    anyCompleted = true;
                }
            }

            if (anyCompleted)
            {
                RecalculateEffects(GuildBuildingDefinition.GetAll());
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
    // EFFEKT-CACHE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die gecachten Gebäude-Effekte zurück (kein Firebase-Request).
    /// </summary>
    public GuildHallEffects GetCachedEffects() => _cachedEffects;

    /// <summary>
    /// Aktualisiert den Gebäude-Effekt-Cache von Firebase.
    /// Berechnet Gesamteffekte und cached sie auf GuildMembership.
    /// </summary>
    public async Task RefreshHallCacheAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null || string.IsNullOrEmpty(membership.GuildId))
            return;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return; // Timeout: Lock nicht erhalten
        try
        {
            await _firebase.EnsureAuthenticatedAsync();
            var guildId = membership.GuildId;

            // Hallen-Level laden
            var hallLevelRaw = await _firebase.GetAsync<string>($"guilds/{guildId}/hallLevel");
            _hallLevel = int.TryParse(hallLevelRaw, out var hl) ? Math.Max(1, hl) : 1;

            // Gebäude-Zustände laden
            _cachedStates = await _firebase.GetAsync<Dictionary<string, GuildBuildingState>>(
                $"guild_hall/{guildId}/buildings")
                ?? new Dictionary<string, GuildBuildingState>();

            RecalculateEffects(GuildBuildingDefinition.GetAll());
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
    /// Gibt das aktuelle Hallen-Level zurück.
    /// </summary>
    public int GetHallLevel() => _hallLevel;

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE: EFFEKTE BERECHNEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet die Gesamteffekte aller Gebäude via GuildHallEffects.Calculate().
    /// Cached Ergebnis und wendet es auf GuildMembership an.
    /// </summary>
    private void RecalculateEffects(List<GuildBuildingDefinition> definitions)
    {
        if (_cachedStates == null) return;

        // Cache-States in BuildingId→Level Dictionary umwandeln
        var buildingLevels = new Dictionary<GuildBuildingId, int>();
        foreach (var def in definitions)
        {
            var stateKey = def.BuildingId.ToString();
            if (_cachedStates.TryGetValue(stateKey, out var state) && state.Level > 0)
                buildingLevels[def.BuildingId] = state.Level;
        }

        _cachedEffects = GuildHallEffects.Calculate(buildingLevels);

        // Auf GuildMembership cachen (für Offline-Nutzung)
        var membership = _gameStateService.State.GuildMembership;
        if (membership != null)
        {
            membership.ApplyHallEffects(_cachedEffects);
            membership.GuildHallLevel = _hallLevel;
            _gameStateService.MarkDirty();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
