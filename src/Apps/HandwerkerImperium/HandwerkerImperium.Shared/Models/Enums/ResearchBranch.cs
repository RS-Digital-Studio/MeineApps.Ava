namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Research branches in the skill tree.
/// Each branch has 15 levels of research.
/// </summary>
public enum ResearchBranch
{
    /// <summary>Improves tools, efficiency, and mini-game bonuses</summary>
    Tools = 0,

    /// <summary>Improves worker management, hiring, and training</summary>
    Management = 1,

    /// <summary>Improves marketing, reputation, and order rewards</summary>
    Marketing = 2
}

public static class ResearchBranchExtensions
{
    /// <summary>
    /// Icon for this research branch.
    /// </summary>
    public static string GetIcon(this ResearchBranch branch) => branch switch
    {
        ResearchBranch.Tools => "\ud83d\udd27",       // Wrench
        ResearchBranch.Management => "\ud83d\udcbc",   // Briefcase
        ResearchBranch.Marketing => "\ud83d\udce3",    // Megaphone
        _ => "\ud83d\udd27"
    };

    /// <summary>
    /// Localization key for branch name.
    /// </summary>
    public static string GetLocalizationKey(this ResearchBranch branch) => $"Branch{branch}";

    /// <summary>
    /// Localization key for branch description.
    /// </summary>
    public static string GetDescriptionKey(this ResearchBranch branch) => $"Branch{branch}Desc";

    /// <summary>
    /// Color key for UI display.
    /// </summary>
    public static string GetColorKey(this ResearchBranch branch) => branch switch
    {
        ResearchBranch.Tools => "#FF9800",       // Orange
        ResearchBranch.Management => "#2196F3",  // Blue
        ResearchBranch.Marketing => "#4CAF50",   // Green
        _ => "#FF9800"
    };
}
