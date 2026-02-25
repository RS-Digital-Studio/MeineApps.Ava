using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public class ResearchService : IResearchService
{
    private readonly IGameStateService _gameState;
    private ResearchEffect? _cachedEffects;
    private bool _effectsDirty = true;

    public event EventHandler<Research>? ResearchCompleted;

    public ResearchService(IGameStateService gameState)
    {
        _gameState = gameState;
    }

    public bool StartResearch(string researchId)
    {
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
        state.ActiveResearchId = researchId;
        return true;
    }

    public bool CancelResearch()
    {
        var state = _gameState.State;
        if (state.ActiveResearchId == null) return false;

        var research = state.Researches.FirstOrDefault(r => r.Id == state.ActiveResearchId);
        if (research != null)
        {
            research.IsActive = false;
            research.StartedAt = null;
            // Refund 50% of cost
            _gameState.AddMoney(research.Cost * 0.5m);
        }

        state.ActiveResearchId = null;
        _effectsDirty = true;
        return true;
    }

    public Research? GetActiveResearch()
    {
        var state = _gameState.State;
        if (state.ActiveResearchId == null) return null;
        return state.Researches.FirstOrDefault(r => r.Id == state.ActiveResearchId);
    }

    public List<Research> GetResearchTree() => _gameState.State.Researches;

    public List<Research> GetBranch(ResearchBranch branch) =>
        _gameState.State.Researches.Where(r => r.Branch == branch).OrderBy(r => r.Level).ToList();

    public bool IsResearched(string id) =>
        _gameState.State.Researches.Any(r => r.Id == id && r.IsResearched);

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
        _effectsDirty = true;

        ResearchCompleted?.Invoke(this, active);
        return true;
    }

    public void UpdateTimer(double deltaSeconds)
    {
        var active = GetActiveResearch();
        if (active == null) return;

        // Gilden-Forschung: Beschleunigung (+20%)
        var effectiveDuration = active.Duration;
        var guildSpeedBonus = _gameState.State.GuildMembership?.ResearchSpeedBonus ?? 0m;
        if (guildSpeedBonus > 0)
            effectiveDuration = TimeSpan.FromSeconds(effectiveDuration.TotalSeconds / (double)(1m + guildSpeedBonus));

        // Check if completed
        if (active.StartedAt != null && DateTime.UtcNow >= active.StartedAt.Value + effectiveDuration)
        {
            active.IsResearched = true;
            active.IsActive = false;
            active.CompletedAt = DateTime.UtcNow;
            _gameState.State.ActiveResearchId = null;
            _effectsDirty = true;

            ResearchCompleted?.Invoke(this, active);
        }
    }
}
