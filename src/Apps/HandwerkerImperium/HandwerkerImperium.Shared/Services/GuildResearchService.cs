using System.Globalization;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet Gilden-Forschung (18 Technologien in 6 Kategorien).
/// Extrahiert aus GuildService für bessere Trennung.
/// Firebase-Pfad: guild_research/{guildId}/{researchId}
/// </summary>
public sealed class GuildResearchService : IGuildResearchService
{
    private readonly IGameStateService _gameStateService;
    private readonly IFirebaseService _firebaseService;
    private GuildResearchEffects _cachedEffects = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public GuildResearchService(
        IGameStateService gameStateService,
        IFirebaseService firebaseService)
    {
        _gameStateService = gameStateService;
        _firebaseService = firebaseService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORSCHUNGEN LADEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lädt alle 18 Gilden-Forschungen mit aktuellem Fortschritt von Firebase.
    /// Merged statische Definitionen mit Firebase-Zuständen.
    /// Schließt dabei automatisch abgelaufene Timer ab.
    /// </summary>
    public async Task<List<GuildResearchDisplay>> GetGuildResearchAsync()
    {
        var result = new List<GuildResearchDisplay>();

        await _semaphore.WaitAsync();
        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return result;

            await _firebaseService.EnsureAuthenticatedAsync();

            // Alle Forschungs-Zustände laden
            var statesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{membership.GuildId}");
            var states = statesRaw ?? new Dictionary<string, GuildResearchState>();

            // Definitionen einmal laden und als Dictionary cachen
            var definitions = GuildResearchDefinition.GetAll();
            var defLookup = definitions.ToDictionary(d => d.Id);

            // Timer-Check: Abgelaufene Forschungen automatisch abschließen
            var now = DateTime.UtcNow;
            foreach (var (id, state) in states)
            {
                if (state.Completed || string.IsNullOrEmpty(state.ResearchStartedAt)) continue;
                if (!DateTime.TryParse(state.ResearchStartedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var startedAt))
                    continue;
                if (!defLookup.TryGetValue(id, out var def2)) continue;

                var durH = GuildResearchDefinition.GetResearchDurationHours(def2.Cost);
                // Schnellforschung-Bonus anwenden (guild_mastery_1)
                if (_cachedEffects.ResearchSpeedBonus > 0)
                    durH *= (double)(1m - _cachedEffects.ResearchSpeedBonus);

                if (now >= startedAt.AddHours(durH))
                {
                    state.Completed = true;
                    state.CompletedAt = now.ToString("O");
                    await _firebaseService.SetAsync($"guild_research/{membership.GuildId}/{id}", state);
                }
            }

            // Abgeschlossene IDs sammeln für Effekt-Berechnung
            var completedIds = new HashSet<string>();
            foreach (var (id, state) in states)
            {
                if (state.Completed) completedIds.Add(id);
            }

            // Effekte berechnen und cachen
            _cachedEffects = GuildResearchEffects.Calculate(completedIds);
            membership.ApplyResearchEffects(_cachedEffects);
            _gameStateService.MarkDirty();

            // Definitionen mit Zuständen mergen, pro Kategorie aktive bestimmen
            var categoryFirstIncomplete = new Dictionary<GuildResearchCategory, bool>();

            foreach (var def in definitions.OrderBy(d => d.Category).ThenBy(d => d.Order))
            {
                states.TryGetValue(def.Id, out var researchState);
                var isCompleted = researchState?.Completed ?? false;

                // IsResearching prüfen
                var isResearching = !isCompleted && !string.IsNullOrEmpty(researchState?.ResearchStartedAt);

                // Erste nicht-abgeschlossene pro Kategorie = aktiv (aber nur wenn nicht im Timer)
                var isActive = false;
                if (!isCompleted && !categoryFirstIncomplete.ContainsKey(def.Category))
                {
                    if (!isResearching)
                        isActive = true;
                    categoryFirstIncomplete[def.Category] = true;
                }

                result.Add(new GuildResearchDisplay
                {
                    Id = def.Id,
                    Name = def.NameKey, // Wird im ViewModel durch lokalisierten Text ersetzt
                    Description = def.DescKey,
                    Icon = def.Icon,
                    Cost = def.Cost,
                    Progress = researchState?.Progress ?? 0,
                    Category = def.Category,
                    EffectType = def.EffectType,
                    EffectValue = def.EffectValue,
                    CategoryColor = GuildResearchDefinition.GetCategoryColor(def.Category),
                    IsCompleted = isCompleted,
                    IsActive = isActive,
                    IsResearching = isResearching,
                    ResearchStartedAt = researchState?.ResearchStartedAt,
                    DurationHours = GuildResearchDefinition.GetResearchDurationHours(def.Cost),
                });
            }
        }
        catch
        {
            // Bei Fehler leere Liste zurückgeben
        }
        finally
        {
            _semaphore.Release();
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BEITRÄGE LEISTEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Leistet einen Geldbeitrag zu einer bestimmten Forschung.
    /// Zieht Geld ab, erhöht Fortschritt, startet Timer bei 100%.
    /// Firebase wird ZUERST aktualisiert - Geld erst bei Erfolg abgezogen.
    /// </summary>
    public async Task<bool> ContributeToResearchAsync(string researchId, long amount)
    {
        await _semaphore.WaitAsync();
        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null || amount <= 0) return false;

            var state = _gameStateService.State;
            if (state.Money < amount) return false;

            await _firebaseService.EnsureAuthenticatedAsync();

            var guildId = membership.GuildId;
            var path = $"guild_research/{guildId}/{researchId}";

            // Aktuellen Zustand laden
            var researchState = await _firebaseService.GetAsync<GuildResearchState>(path);
            researchState ??= new GuildResearchState();

            if (researchState.Completed) return false;

            // Kosten der Forschung ermitteln
            var definition = GuildResearchDefinition.GetAll().FirstOrDefault(d => d.Id == researchId);
            if (definition == null) return false;

            // Beitrag berechnen (nicht mehr als nötig)
            var remaining = definition.Cost - researchState.Progress;
            var actualAmount = Math.Min(amount, remaining);
            if (actualAmount <= 0) return false;

            // Fortschritt erhöhen (in-memory)
            researchState.Progress += actualAmount;

            // Abschluss prüfen → Timer starten statt sofort abschließen
            if (researchState.Progress >= definition.Cost && string.IsNullOrEmpty(researchState.ResearchStartedAt))
            {
                researchState.ResearchStartedAt = DateTime.UtcNow.ToString("O");
                // completed wird NICHT gesetzt - erst wenn Timer abläuft
            }

            // Firebase ZUERST aktualisieren - Geld erst bei Erfolg abziehen
            await _firebaseService.SetAsync(path, researchState);

            // Firebase-Write erfolgreich → Geld lokal abziehen
            _gameStateService.AddMoney(-actualAmount);

            // Effekte neu berechnen
            await RefreshEffectsCoreAsync(guildId);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIMER-COMPLETION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob laufende Forschungen abgeschlossen sind (Timer abgelaufen).
    /// Wird periodisch vom GameLoop/ViewModel aufgerufen.
    /// </summary>
    public async Task<bool> CheckResearchCompletionAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return false;

            await _firebaseService.EnsureAuthenticatedAsync();

            var guildId = membership.GuildId;
            var statesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{guildId}");
            if (statesRaw == null) return false;

            var now = DateTime.UtcNow;
            var anyCompleted = false;
            var defLookup = GuildResearchDefinition.GetAll().ToDictionary(d => d.Id);

            foreach (var (id, state) in statesRaw)
            {
                if (state.Completed || string.IsNullOrEmpty(state.ResearchStartedAt)) continue;

                // Timer-Start parsen
                if (!DateTime.TryParse(state.ResearchStartedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var startedAt))
                    continue;

                // Forschungsdauer ermitteln
                if (!defLookup.TryGetValue(id, out var definition)) continue;

                var durationHours = GuildResearchDefinition.GetResearchDurationHours(definition.Cost);

                // Schnellforschung-Bonus (guild_mastery_1: +20% Speed = -20% Dauer)
                if (_cachedEffects.ResearchSpeedBonus > 0)
                    durationHours *= (double)(1m - _cachedEffects.ResearchSpeedBonus);

                var endTime = startedAt.AddHours(durationHours);
                if (now >= endTime)
                {
                    // Timer abgelaufen → Forschung abschließen
                    state.Completed = true;
                    state.CompletedAt = now.ToString("O");
                    await _firebaseService.SetAsync($"guild_research/{guildId}/{id}", state);
                    anyCompleted = true;
                }
            }

            if (anyCompleted)
            {
                await RefreshEffectsCoreAsync(guildId);
            }

            return anyCompleted;
        }
        catch
        {
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EFFEKT-CACHE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die gecachten Forschungs-Effekte zurück (kein Firebase-Request).
    /// Wird vom GameLoop und anderen Services für schnellen Zugriff genutzt.
    /// </summary>
    public GuildResearchEffects GetCachedEffects() => _cachedEffects;

    /// <summary>
    /// Aktualisiert den Forschungs-Effekt-Cache von Firebase.
    /// Berechnet Gesamteffekte aus abgeschlossenen Forschungen und cached sie auf GuildMembership.
    /// </summary>
    public async Task RefreshResearchCacheAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var membership = _gameStateService.State.GuildMembership;
            if (membership == null) return;

            await _firebaseService.EnsureAuthenticatedAsync();
            await RefreshEffectsCoreAsync(membership.GuildId);
        }
        catch
        {
            // Bei Fehler alten Cache behalten
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INTERNE HELPER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Interne Methode zum Aktualisieren des Effekt-Caches.
    /// ACHTUNG: Muss innerhalb des Semaphores aufgerufen werden!
    /// </summary>
    private async Task RefreshEffectsCoreAsync(string guildId)
    {
        try
        {
            var statesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildResearchState>>(
                $"guild_research/{guildId}");

            var completedIds = new HashSet<string>();
            if (statesRaw != null)
            {
                foreach (var (id, rs) in statesRaw)
                {
                    if (rs.Completed) completedIds.Add(id);
                }
            }

            _cachedEffects = GuildResearchEffects.Calculate(completedIds);

            // Cache in GuildMembership aktualisieren
            var membership = _gameStateService.State.GuildMembership;
            if (membership != null)
            {
                membership.ApplyResearchEffects(_cachedEffects);
                _gameStateService.MarkDirty();
            }
        }
        catch
        {
            // Bei Fehler alten Cache behalten
        }
    }
}
