using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Darstellungs-Modell für eine Tier-Option im Prestige-Bestätigungsdialog.
/// </summary>
public sealed class PrestigeTierOption
{
    public required PrestigeTier Tier { get; init; }

    /// <summary>Enum-Name als String für CommandParameter (z.B. "Bronze").</summary>
    public string TierKey => Tier.ToString();

    /// <summary>Lokalisierter Anzeige-Name.</summary>
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string Color { get; init; }
    public required int Points { get; init; }
    public required string PointsText { get; init; }
    public required string BonusText { get; init; }
    public required string PreservationText { get; init; }
    public required bool IsSelected { get; set; }
    public required bool IsRecommended { get; init; }
}
