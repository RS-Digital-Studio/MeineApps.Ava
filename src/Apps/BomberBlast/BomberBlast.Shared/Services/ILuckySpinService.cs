namespace BomberBlast.Services;

/// <summary>
/// Tägliches Glücksrad: 1x gratis pro Tag, extra Spins per Rewarded Ad
/// </summary>
public interface ILuckySpinService
{
    /// <summary>Ob ein kostenloser Spin heute verfügbar ist</summary>
    bool IsFreeSpinAvailable { get; }

    /// <summary>Gesamtanzahl bisheriger Spins</summary>
    int TotalSpins { get; }

    // === Phase 23 — Lootbox-Compliance (Pity-Counter) ========================

    /// <summary>
    /// Phase 23 — Spins seit dem letzten Jackpot. Wird in der UI als
    /// "Pity-Counter" angezeigt: bei <see cref="JackpotPityThreshold"/> garantiert das System
    /// den nächsten Jackpot. Compliance-Anforderung (UK/China — Drop-Rate-Transparenz).
    /// </summary>
    int SpinsSinceLastJackpot { get; }

    /// <summary>Anzahl Spins bis zum garantierten Jackpot (Konstante 50).</summary>
    int JackpotPityThreshold { get; }

    /// <summary>Drop-Rate-Tabelle in Prozent fuer UI-Disclosure (Compliance).</summary>
    IReadOnlyList<(int RewardIndex, float ProbabilityPercent)> GetDropRates();

    /// <summary>Verfügbare Rad-Segmente</summary>
    IReadOnlyList<SpinReward> GetRewards();

    /// <summary>Zufälliges Segment wählen (gewichtet). Gibt Index zurück</summary>
    int Spin();

    /// <summary>Kostenlosen Tages-Spin als verbraucht markieren</summary>
    void ClaimFreeSpin();
}

/// <summary>
/// Ein Segment auf dem Glücksrad
/// </summary>
public class SpinReward
{
    public int Index { get; init; }
    public string NameKey { get; init; } = "";
    public int Coins { get; init; }
    public int Gems { get; init; }
    public bool IsJackpot { get; init; }

    /// <summary>Gewichtung (höher = wahrscheinlicher)</summary>
    public int Weight { get; init; } = 1;
}
