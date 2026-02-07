using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Manages the research skill tree.
/// </summary>
public interface IResearchService
{
    bool StartResearch(string researchId);
    bool CancelResearch();
    Research? GetActiveResearch();
    List<Research> GetResearchTree();
    List<Research> GetBranch(ResearchBranch branch);
    bool IsResearched(string id);
    ResearchEffect GetTotalEffects();

    /// <summary>
    /// Updates active research timer. Called each game tick.
    /// Also handles offline catch-up.
    /// </summary>
    void UpdateTimer(double deltaSeconds);

    /// <summary>
    /// Sofortfertigstellung der aktiven Forschung gegen Goldschrauben (ab Level 8).
    /// </summary>
    bool InstantFinishResearch();

    event EventHandler<Research>? ResearchCompleted;
}
