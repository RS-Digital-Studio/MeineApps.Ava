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
