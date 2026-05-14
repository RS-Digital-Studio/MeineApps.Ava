using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Speedrun-Belohnungs-System (v2.0.36).
/// Bei jeder neuen persoenlichen Bestzeit pro Tier wird eine Goldschrauben-Belohnung
/// vergeben — abhaengig vom Tier und der erreichten Zeit. Skaliert mit Schwierigkeit:
/// schnellere Runs in hoeheren Tiers bringen mehr GS.
///
/// Spielmechanisch lockt das Late-Game-Spieler dazu, jeden Run zu optimieren —
/// ein zweiter Loop neben Multiplikator-Grind nach 3× Legende.
/// (Empfehlung: "Speedrun-Tournaments fuellen Late-Game-Vakuum".)
/// </summary>
public static class SpeedrunRewards
{
    /// <summary>
    /// Berechnet die Speedrun-Belohnung in Goldschrauben fuer eine neue Bestzeit.
    /// Skala (Tier × Time-Bracket):
    /// <list type="bullet">
    ///   <item>Bronze: 5/10/15 GS bei &lt;2h / &lt;1h / &lt;30min</item>
    ///   <item>Silver: 10/20/35 GS bei &lt;3h / &lt;2h / &lt;1h</item>
    ///   <item>Gold: 15/30/60 GS bei &lt;5h / &lt;3h / &lt;1.5h</item>
    ///   <item>Platin: 25/50/100 GS bei &lt;8h / &lt;5h / &lt;2.5h</item>
    ///   <item>Diamant: 40/80/150 GS bei &lt;12h / &lt;8h / &lt;4h</item>
    ///   <item>Meister: 60/120/250 GS bei &lt;20h / &lt;12h / &lt;6h</item>
    ///   <item>Legende: 100/200/400 GS bei &lt;30h / &lt;20h / &lt;10h</item>
    /// </list>
    /// </summary>
    public static int CalculateReward(PrestigeTier tier, TimeSpan runDuration)
    {
        if (runDuration <= TimeSpan.Zero) return 0;

        // (TimeLimitHours, RewardGS) pro Tier, in absteigender Time-Reihenfolge
        var brackets = tier switch
        {
            PrestigeTier.Bronze => new[] { (2.0, 5), (1.0, 10), (0.5, 15) },
            PrestigeTier.Silver => new[] { (3.0, 10), (2.0, 20), (1.0, 35) },
            PrestigeTier.Gold => new[] { (5.0, 15), (3.0, 30), (1.5, 60) },
            PrestigeTier.Platin => new[] { (8.0, 25), (5.0, 50), (2.5, 100) },
            PrestigeTier.Diamant => new[] { (12.0, 40), (8.0, 80), (4.0, 150) },
            PrestigeTier.Meister => new[] { (20.0, 60), (12.0, 120), (6.0, 250) },
            PrestigeTier.Legende => new[] { (30.0, 100), (20.0, 200), (10.0, 400) },
            _ => Array.Empty<(double, int)>()
        };

        // Höchste passende Belohnung finden (schneller = höher)
        int reward = 0;
        double hours = runDuration.TotalHours;
        foreach (var (limitHours, gs) in brackets)
        {
            if (hours <= limitHours) reward = gs;
        }
        return reward;
    }

    /// <summary>
    /// Liefert das Time-Limit fuer das beste Bracket eines Tiers (fuer UI-Anzeige
    /// "Schaffst du es unter X Stunden?").
    /// </summary>
    public static double GetGoldBracketHours(PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 0.5,
        PrestigeTier.Silver => 1.0,
        PrestigeTier.Gold => 1.5,
        PrestigeTier.Platin => 2.5,
        PrestigeTier.Diamant => 4.0,
        PrestigeTier.Meister => 6.0,
        PrestigeTier.Legende => 10.0,
        _ => 0.0
    };
}
