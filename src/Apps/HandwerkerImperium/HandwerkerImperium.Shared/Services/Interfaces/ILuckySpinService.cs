using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet das Glücksrad mit täglichem Gratis-Spin und kostenpflichtigen Spins.
/// </summary>
public interface ILuckySpinService
{
    /// <summary>
    /// Ob ein kostenloser Spin verfügbar ist (einmal täglich).
    /// </summary>
    bool HasFreeSpin { get; }

    /// <summary>BAL-AD-6: Ob ein Video-Spin verfügbar ist (1x/Tag, nach Gratis-Spin).</summary>
    bool HasAdSpin { get; }

    /// <summary>Kosten pro Spin in Goldschrauben (wenn kein Gratis-/Ad-Spin verfügbar).</summary>
    int SpinCost { get; }

    /// <summary>Führt einen Spin durch (Gratis oder kostenpflichtig).</summary>
    LuckySpinPrizeType Spin();

    /// <summary>BAL-AD-6: Bestimmt Gewinn für Ad-Spin (ohne Kosten). MarkAdSpinUsed() danach aufrufen.</summary>
    LuckySpinPrizeType SpinForAd();

    /// <summary>BAL-AD-6: Markiert den Ad-Spin als heute verbraucht.</summary>
    void MarkAdSpinUsed();

    /// <summary>Wendet den Gewinn auf den GameState an.</summary>
    void ApplyPrize(LuckySpinPrizeType prizeType);
}
