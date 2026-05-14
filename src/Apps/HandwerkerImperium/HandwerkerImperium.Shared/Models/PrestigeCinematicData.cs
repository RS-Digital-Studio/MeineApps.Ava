using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Eingabe-Daten für die Prestige-Cinematic (P0.3 — 2026-05-08).
/// Wird beim Prestige-Reset vom <see cref="Services.IPrestigeService"/> erzeugt
/// und an die Cinematic-View übergeben.
///
/// Phasen-Sequenz (siehe <c>PrestigeCinematicRenderer</c>):
/// 1. Money-Reverse-Counter (3s): <see cref="MoneyAtPrestige"/> rollt rückwärts
/// 2. Tier-Badge-Reveal (3s): Glow-Pulse + Tier-Name
/// 3. Multiplier-Stagger (5s): Tier-Bonus → Diminishing → Bonus-PP → Final-Score
/// 4. Reward-Card (3s): "Tap to Continue" mit zusammenfassenden Belohnungen
/// </summary>
public sealed class PrestigeCinematicData
{
    /// <summary>Geld zum Zeitpunkt des Prestiges (für Reverse-Counter ).</summary>
    public decimal MoneyAtPrestige { get; init; }

    /// <summary>Erreichte Tier (Bronze/Silver/...).</summary>
    public PrestigeTier Tier { get; init; }

    /// <summary>Prestige-Punkte aus dieser Run (Basis ohne Bonus).</summary>
    public int BasePrestigePoints { get; init; }

    /// <summary>Bonus-PP aus Achievements (Perfect-Ratings, Research-Branch, Gebäude-Lv5).</summary>
    public int BonusPrestigePoints { get; init; }

    /// <summary>Effektiver Tier-Multiplier nach Diminishing Returns (z.B. 1.35 bei Silver mit DR 0.83).</summary>
    public double TierMultiplierEffective { get; init; }

    /// <summary>Roher Tier-Bonus (z.B. 0.35 = +35% bei Silver) ohne Diminishing.</summary>
    public double TierMultiplierRaw { get; init; }

    /// <summary>Diminishing-Returns-Faktor (1.0 = 1. Prestige des Tiers, sinkt mit jedem weiteren).</summary>
    public double DiminishingReturnsFactor { get; init; }

    /// <summary>Anzahl bereits durchgeführter Prestiges in diesem Tier (für Diminishing).</summary>
    public int TierCount { get; init; }

    /// <summary>Run-Dauer als Sekunden (für Speedrun-Anzeige in ).</summary>
    public double RunDurationSeconds { get; init; }

    /// <summary>Anzahl aktive Challenges (für Challenge-Bonus-Anzeige in ).</summary>
    public int ActiveChallengeCount { get; init; }

    /// <summary>Lokalisierter Tier-Name (z.B. "Silber"). Wird vom Service via RESX gesetzt.</summary>
    public string TierDisplayName { get; init; } = string.Empty;
}
