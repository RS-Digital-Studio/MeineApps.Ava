namespace BomberBlast.Models;

/// <summary>
/// Ein einzelner Tagesbonus im 7-Tage-Zyklus
/// </summary>
public class DailyReward
{
    /// <summary>Tag im Zyklus (1-7)</summary>
    public int Day { get; init; }

    /// <summary>Coin-Belohnung</summary>
    public int Coins { get; init; }

    /// <summary>Bonus-Extra-Leben (nur Tag 5)</summary>
    public int ExtraLives { get; init; }

    /// <summary>Gem-Belohnung (nur Tag 7)</summary>
    public int Gems { get; init; }

    /// <summary>Ob der Bonus bereits abgeholt wurde</summary>
    public bool IsClaimed { get; set; }

    /// <summary>Ob dieser Tag der aktuelle ist</summary>
    public bool IsCurrentDay { get; set; }

    /// <summary>Ob der Tag vergangen (und eingesammelt) ist</summary>
    public bool IsPast { get; set; }
}
