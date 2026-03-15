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

    /// <summary>
    /// Reduziert die verbleibende Forschungszeit um den angegebenen Prozentsatz (0.0-1.0).
    /// BAL-4: Für Rewarded-Ad-Speedup (50% statt Sofortfertigstellung).
    /// </summary>
    bool ReduceResearchTime(double percentage);

    /// <summary>
    /// Invalidiert alle internen Caches (Effekte + aktive Forschung).
    /// Muss nach SaveGame-Load/Import/Reset aufgerufen werden,
    /// damit der Cache zum neuen GameState passt.
    /// </summary>
    void InvalidateCaches();

    event EventHandler<Research>? ResearchCompleted;
}
