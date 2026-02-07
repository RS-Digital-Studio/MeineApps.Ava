using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public class ResearchService : IResearchService
{
    private readonly IGameStateService _gameState;

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
        var total = new ResearchEffect();
        foreach (var research in _gameState.State.Researches.Where(r => r.IsResearched))
        {
            total = ResearchEffect.Combine(total, research.Effect);
        }
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

        ResearchCompleted?.Invoke(this, active);
        return true;
    }

    public void UpdateTimer(double deltaSeconds)
    {
        var active = GetActiveResearch();
        if (active == null) return;

        // Check if completed
        if (active.StartedAt != null && DateTime.UtcNow >= active.StartedAt.Value + active.Duration)
        {
            active.IsResearched = true;
            active.IsActive = false;
            active.CompletedAt = DateTime.UtcNow;
            _gameState.State.ActiveResearchId = null;

            ResearchCompleted?.Invoke(this, active);
        }
    }
}
