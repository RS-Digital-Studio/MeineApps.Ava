using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Bifazial-Logik. Der Mehrertrag (Bifacial Gain) wird konservativ als Bereich aus der Albedo
/// geschätzt — dominanter Hebel laut Literatur: "doppelte Albedo ~ doppelter Mehrertrag".
/// Die Werte sind für ein bodennahes Einzelpanel bewusst vorsichtig gewählt (große
/// Ground-Mount-Studien liegen höher). Bifaziale Panels profitieren bei hoher Albedo von
/// STEILERER Neigung (Rückseite sieht mehr Boden) — daher der Tilt-Zuschlag (+0 bis +11 Grad).
/// </summary>
public sealed class BifacialService : IBifacialService
{
    private const double MaxGain = 0.30;        // Deckel für ein mobiles Einzelpanel
    private const double TiltBonusMax = 11.0;   // max. Steilwinkel-Zuschlag bei höchster Albedo
    private const double AlbedoLow = 0.20;      // ab hier beginnt ein nennenswerter Zuschlag
    private const double AlbedoHigh = 0.85;     // Schnee

    public BifacialAdvice GetAdvice(GroundType ground, PanelProfile panel)
    {
        var albedo = ground.Albedo();

        double gainLow, gainHigh, tiltBonus;
        if (panel.IsBifacial)
        {
            gainLow = Math.Min(albedo * 0.20, MaxGain);
            gainHigh = Math.Min(albedo * 0.45, MaxGain);
            tiltBonus = Math.Clamp((albedo - AlbedoLow) / (AlbedoHigh - AlbedoLow) * TiltBonusMax, 0.0, TiltBonusMax);
        }
        else
        {
            gainLow = 0.0;
            gainHigh = 0.0;
            tiltBonus = 0.0;
        }

        var tips = BuildTips(ground, albedo, panel);
        return new BifacialAdvice(ground, albedo, gainLow, gainHigh, tiltBonus, tips);
    }

    /// <summary>
    /// Liefert Lokalisierungs-KEYS (keine fertigen Texte) — der UI-Layer löst sie über GetString
    /// auf, damit der Service sprachneutral + testbar bleibt.
    /// </summary>
    private static IReadOnlyList<string> BuildTips(GroundType ground, double albedo, PanelProfile panel)
    {
        if (!panel.IsBifacial)
            return ["BifacialTipNotBifacial"];

        var tips = new List<string>();

        // Untergrund-Bewertung
        if (albedo >= 0.55)
            tips.Add("BifacialTipBrightGround");
        else if (albedo >= 0.30)
            tips.Add("BifacialTipMediumGround");
        else
            tips.Add("BifacialTipDarkGround");

        // Konkrete Hebel (aus der Recherche)
        tips.Add("BifacialTipBackFree");
        tips.Add("BifacialTipRaise");

        if (panel.HasFixedTilts && panel.KickstandTilts.Count > 1)
            tips.Add("BifacialTipSteepSnow");

        return tips;
    }
}
