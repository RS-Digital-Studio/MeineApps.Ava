namespace SunSeeker.Shared.Models;

/// <summary>
/// Beschreibt ein physisches Solarpanel: Nennleistung, ob bifazial, und die verfügbaren
/// Aufstellwinkel des Standfußes (Kickstand). Ein leeres <see cref="KickstandTilts"/>
/// bedeutet ein frei verstellbares Panel (beliebiger Winkel einstellbar).
/// </summary>
public sealed record PanelProfile(
    string Name,
    double NominalPowerWatts,
    bool IsBifacial,
    IReadOnlyList<double> KickstandTilts,
    string? Notes = null)
{
    /// <summary>Anker SOLIX PS400 Bifazial — stufenlos verstellbarer Standwinkel, Front + Rückseite.</summary>
    public static readonly PanelProfile Ps400Bifacial = new(
        "Anker SOLIX PS400 (Bifazial)", 400, true, [],
        "Stufenlos verstellbarer Standwinkel. Bifazial: Rückseite erntet reflektiertes Licht.");

    /// <summary>Anker SOLIX PS400 (monofazial) — Kickstand 30/40/50/80 Grad.</summary>
    public static readonly PanelProfile Ps400 = new(
        "Anker SOLIX PS400", 400, false, [30, 40, 50, 80],
        "Vier feste Standwinkel.");

    /// <summary>Generisches, frei verstellbares Panel.</summary>
    public static readonly PanelProfile Generic = new(
        "Frei verstellbares Panel", 0, false, [],
        "Beliebiger Neigungswinkel einstellbar.");

    public static IReadOnlyList<PanelProfile> All => [Ps400Bifacial, Ps400, Generic];

    /// <summary>Hat das Panel feste, vorgegebene Aufstellwinkel?</summary>
    public bool HasFixedTilts => KickstandTilts.Count > 0;

    /// <summary>
    /// Nächster verfügbarer Kickstand-Winkel zum gewünschten Neigungswinkel. Bei einem
    /// frei verstellbaren Panel (keine festen Winkel) wird der Wunschwinkel selbst zurückgegeben.
    /// </summary>
    public double NearestKickstand(double desiredTilt)
    {
        if (KickstandTilts.Count == 0) return desiredTilt;

        var best = KickstandTilts[0];
        foreach (var tilt in KickstandTilts)
        {
            if (Math.Abs(tilt - desiredTilt) < Math.Abs(best - desiredTilt))
                best = tilt;
        }
        return best;
    }
}
