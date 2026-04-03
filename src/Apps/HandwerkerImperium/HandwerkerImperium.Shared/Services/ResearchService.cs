using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public sealed class ResearchService : IResearchService
{
    private readonly IGameStateService _gameState;
    private readonly IAscensionService _ascensionService;
    private readonly IChallengeConstraintService _challengeConstraints;
    private readonly IPrestigeService _prestigeService;
    private ResearchEffect? _cachedEffects;
    private bool _effectsDirty = true;
    // Cache fuer aktive Forschung (vermeidet FirstOrDefault ueber 45 Eintraege pro Tick)
    private Research? _activeResearchCache;
    private bool _activeResearchDirty = true;
    // Gecachte Branches (vermeidet Where+OrderBy+ToList bei jedem GetBranch()-Aufruf)
    private const int BranchCount = 3; // Tools=0, Management=1, Marketing=2
    private List<Research>[]? _cachedBranches;
    // Gecachte erledigte Research-IDs (vermeidet Any() ueber 45 Eintraege bei IsResearched())
    private HashSet<string>? _researchedIds;

    public event EventHandler<Research>? ResearchCompleted;

    public ResearchService(
        IGameStateService gameState,
        IAscensionService ascensionService,
        IChallengeConstraintService challengeConstraints,
        IPrestigeService prestigeService)
    {
        _gameState = gameState;
        _ascensionService = ascensionService;
        _challengeConstraints = challengeConstraints;
        _prestigeService = prestigeService;
        // Bei State-Wechsel (Load/Import/Reset) Caches invalidieren
        _gameState.StateLoaded += (_, _) => InvalidateCaches();
    }

    public bool StartResearch(string researchId)
    {
        // Challenge: OhneForschung blockiert alle Forschung
        if (_challengeConstraints.IsResearchBlocked()) return false;

        var state = _gameState.State;
        if (state.ActiveResearchId != null) return false;

        var research = state.Researches.FirstOrDefault(r => r.Id == researchId);
        if (research == null || research.IsResearched || research.IsActive) return false;

        // Check prerequisites
        foreach (var prereqId in research.Prerequisites)
        {
            var prereq = state.Researches.FirstOrDefault(r => r.Id == prereqId);
            if (prereq is not { IsResearched: true }) return false;
        }

        // Check cost
        if (!_gameState.CanAfford(research.Cost)) return false;

        _gameState.TrySpendMoney(research.Cost);
        research.IsActive = true;
        research.StartedAt = DateTime.UtcNow;
        research.BonusSeconds = 0; // BonusSeconds zurücksetzen bei neuem Research-Start
        research.EffectiveDuration = CalculateEffectiveDuration(research);
        state.ActiveResearchId = researchId;
        // Active-Research-Cache aktualisieren
        _activeResearchCache = research;
        _activeResearchDirty = false;
        return true;
    }

    public bool CancelResearch()
    {
        var state = _gameState.State;
        if (state.ActiveResearchId == null) return false;

        var research = GetActiveResearch();
        if (research != null)
        {
            research.IsActive = false;
            research.StartedAt = null;
            // 50% Kosten erstatten
            _gameState.AddMoney(research.Cost * 0.5m);
        }

        state.ActiveResearchId = null;
        MarkEffectsDirty();
        // Active-Research-Cache invalidieren
        _activeResearchCache = null;
        _activeResearchDirty = false;
        return true;
    }

    public Research? GetActiveResearch()
    {
        var state = _gameState.State;
        if (state.ActiveResearchId == null)
        {
            _activeResearchCache = null;
            _activeResearchDirty = false;
            return null;
        }
        // Gecachtes Ergebnis zurückgeben wenn noch gültig
        if (!_activeResearchDirty && _activeResearchCache != null
            && _activeResearchCache.Id == state.ActiveResearchId)
            return _activeResearchCache;

        // Cache neu aufbauen (FirstOrDefault nur bei Invalidierung)
        _activeResearchCache = state.Researches.FirstOrDefault(r => r.Id == state.ActiveResearchId);
        _activeResearchDirty = false;
        return _activeResearchCache;
    }

    public List<Research> GetResearchTree() => _gameState.State.Researches;

    public List<Research> GetBranch(ResearchBranch branch)
    {
        if (_cachedBranches == null)
            RebuildCaches();
        return _cachedBranches![(int)branch];
    }

    public bool IsResearched(string id)
    {
        if (_researchedIds == null)
            RebuildCaches();
        return _researchedIds!.Contains(id);
    }

    /// <summary>
    /// Baut Branch-Cache und ResearchedIds-HashSet in einem Durchlauf auf.
    /// Wird bei _effectsDirty oder null-Caches aufgerufen.
    /// </summary>
    private void RebuildCaches()
    {
        var researches = _gameState.State.Researches;
        _cachedBranches = new List<Research>[BranchCount];
        for (int b = 0; b < BranchCount; b++)
            _cachedBranches[b] = new List<Research>();
        _researchedIds = new HashSet<string>();

        for (int i = 0; i < researches.Count; i++)
        {
            var r = researches[i];
            int branchIdx = (int)r.Branch;
            if (branchIdx >= 0 && branchIdx < BranchCount)
                _cachedBranches[branchIdx].Add(r);
            if (r.IsResearched)
                _researchedIds.Add(r.Id);
        }

        // Branches nach Level sortieren
        for (int b = 0; b < BranchCount; b++)
            _cachedBranches[b].Sort((a, c) => a.Level.CompareTo(c.Level));
    }

    /// <inheritdoc/>
    public void InvalidateCaches()
    {
        MarkEffectsDirty();
        _activeResearchDirty = true;
        _activeResearchCache = null;
    }

    /// <summary>
    /// Markiert Effekte, Branch-Cache und ResearchedIds als veraltet.
    /// Wird bei jeder Research-Statusaenderung aufgerufen.
    /// </summary>
    private void MarkEffectsDirty()
    {
        _effectsDirty = true;
        _cachedEffects = null;
        _cachedBranches = null;
        _researchedIds = null;
    }

    public ResearchEffect GetTotalEffects()
    {
        if (!_effectsDirty && _cachedEffects != null)
            return _cachedEffects;

        var total = new ResearchEffect();
        foreach (var research in _gameState.State.Researches)
        {
            if (research.IsResearched)
                total = ResearchEffect.Combine(total, research.Effect);
        }
        _cachedEffects = total;
        _effectsDirty = false;
        return total;
    }

    public bool InstantFinishResearch()
    {
        var active = GetActiveResearch();
        if (active == null || !active.CanInstantFinish) return false;

        var cost = active.InstantFinishScrewCost;
        if (!_gameState.CanAffordGoldenScrews(cost)) return false;

        _gameState.TrySpendGoldenScrews(cost);

        active.IsResearched = true;
        active.IsActive = false;
        active.CompletedAt = DateTime.UtcNow;
        _gameState.State.ActiveResearchId = null;
        MarkEffectsDirty();
        // Active-Research-Cache invalidieren
        _activeResearchCache = null;
        _activeResearchDirty = false;

        ResearchCompleted?.Invoke(this, active);
        return true;
    }

    /// <summary>
    /// Berechnet die zeitbasierten GS-Kosten für Sofortfertigstellung.
    /// 5 GS pro verbleibende Stunde (aufgerundet), min 5, max 50.
    /// </summary>
    public int GetInstantCompleteGSCost(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return 0;
        int hours = (int)Math.Ceiling(remaining.TotalHours);
        int cost = hours * 5;
        return Math.Clamp(cost, 5, 50);
    }

    /// <summary>
    /// Sofortfertigstellung der aktiven Forschung mit zeitbasierten GS-Kosten.
    /// </summary>
    public bool InstantCompleteResearch()
    {
        var active = GetActiveResearch();
        if (active == null || !active.IsActive) return false;

        // Effektive Restzeit berechnen (inkl. Gilden-Bonus)
        var effectiveDuration = CalculateEffectiveDuration(active);

        var elapsed = active.StartedAt.HasValue
            ? DateTime.UtcNow - active.StartedAt.Value + TimeSpan.FromSeconds(active.BonusSeconds)
            : TimeSpan.Zero;
        var remaining = effectiveDuration - elapsed;
        if (remaining <= TimeSpan.Zero) return false;

        var cost = GetInstantCompleteGSCost(remaining);
        if (!_gameState.CanAffordGoldenScrews(cost)) return false;

        _gameState.TrySpendGoldenScrews(cost);

        active.IsResearched = true;
        active.IsActive = false;
        active.CompletedAt = DateTime.UtcNow;
        _gameState.State.ActiveResearchId = null;
        MarkEffectsDirty();
        _activeResearchCache = null;
        _activeResearchDirty = false;

        ResearchCompleted?.Invoke(this, active);
        return true;
    }

    /// <summary>
    /// BAL-4: Reduziert die verbleibende Forschungszeit um den angegebenen Prozentsatz.
    /// Verschiebt StartedAt in die Vergangenheit, sodass die Restzeit sinkt.
    /// Berücksichtigt Guild-Speed-Bonus für korrekte effektive Restzeit.
    /// </summary>
    public bool ReduceResearchTime(double percentage)
    {
        var active = GetActiveResearch();
        if (active?.StartedAt == null) return false;

        percentage = Math.Clamp(percentage, 0.0, 1.0);

        // Effektive Dauer inkl. Gilden-Forschungs-Bonus
        var effectiveDuration = CalculateEffectiveDuration(active);

        var elapsed = DateTime.UtcNow - active.StartedAt.Value + TimeSpan.FromSeconds(active.BonusSeconds);
        var effectiveRemaining = effectiveDuration - elapsed;
        if (effectiveRemaining <= TimeSpan.Zero) return false;

        // BonusSeconds erhöhen statt StartedAt manipulieren
        var reductionSeconds = effectiveRemaining.TotalSeconds * percentage;
        active.BonusSeconds += reductionSeconds;

        return true;
    }

    public void UpdateTimer(double deltaSeconds)
    {
        var active = GetActiveResearch();
        if (active == null) return;

        // EffectiveDuration aktualisieren (Gilden-Bonus kann sich zur Laufzeit ändern)
        var effectiveDuration = CalculateEffectiveDuration(active);
        active.EffectiveDuration = effectiveDuration;

        // Check if completed (berücksichtigt BonusSeconds aus InnovationLab)
        if (active.StartedAt != null &&
            DateTime.UtcNow >= active.StartedAt.Value + effectiveDuration - TimeSpan.FromSeconds(active.BonusSeconds))
        {
            active.IsResearched = true;
            active.IsActive = false;
            active.CompletedAt = DateTime.UtcNow;
            _gameState.State.ActiveResearchId = null;
            MarkEffectsDirty();
            // Active-Research-Cache invalidieren
            _activeResearchCache = null;
            _activeResearchDirty = false;

            ResearchCompleted?.Invoke(this, active);
        }
    }

    /// <summary>
    /// Berechnet die effektive Forschungsdauer inkl. Gilden- und Ascension-Bonus.
    /// Wird auf Research.EffectiveDuration gesetzt damit RemainingTime/Progress korrekt sind.
    /// </summary>
    private TimeSpan CalculateEffectiveDuration(Research research)
    {
        var duration = research.Duration;

        // Gilden-Forschungs-Bonus (additiv auf Geschwindigkeit, z.B. 0.2 = +20% schneller)
        var guildSpeedBonus = _gameState.State.GuildMembership?.ResearchSpeedBonus ?? 0m;
        if (guildSpeedBonus > 0)
            duration = TimeSpan.FromSeconds(duration.TotalSeconds / (double)(1m + guildSpeedBonus));

        // Ascension Timeless-Research-Perk (reduziert Dauer, z.B. 0.1 = -10% Dauer)
        var ascensionBonus = _ascensionService.GetResearchSpeedBonus();
        if (ascensionBonus > 0)
            duration = TimeSpan.FromSeconds(duration.TotalSeconds * (double)(1m - ascensionBonus));

        // Prestige-Shop Forschungs-Turbo (z.B. 0.25 = -25% Dauer)
        var shopResearchBonus = _prestigeService.GetResearchSpeedBonus();
        if (shopResearchBonus > 0)
            duration = TimeSpan.FromSeconds(duration.TotalSeconds * (double)(1m - shopResearchBonus));

        // Minimaldauer 60 Sekunden (verhindert Sofort-Abschluss durch gestackte Boni)
        return TimeSpan.FromSeconds(Math.Max(duration.TotalSeconds, 60));
    }
}
