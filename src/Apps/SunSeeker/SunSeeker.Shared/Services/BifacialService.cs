using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Bifazial-Logik. Der Mehrertrag (Bifacial Gain) wird konservativ als Bereich aus der Albedo
/// geschaetzt — dominanter Hebel laut Literatur: "doppelte Albedo ~ doppelter Mehrertrag".
/// Die Werte sind fuer ein bodennahes Einzelpanel bewusst vorsichtig gewaehlt (grosse
/// Ground-Mount-Studien liegen hoeher). Bifaziale Panels profitieren bei hoher Albedo von
/// STEILERER Neigung (Rueckseite sieht mehr Boden) — daher der Tilt-Zuschlag (+0 bis +11 Grad).
/// </summary>
public sealed class BifacialService : IBifacialService
{
    private const double MaxGain = 0.30;        // Deckel fuer ein mobiles Einzelpanel
    private const double TiltBonusMax = 11.0;   // max. Steilwinkel-Zuschlag bei hoechster Albedo
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

    private static IReadOnlyList<string> BuildTips(GroundType ground, double albedo, PanelProfile panel)
    {
        var tips = new List<string>();

        if (!panel.IsBifacial)
        {
            tips.Add("Dieses Panel ist nicht bifazial — der Untergrund beeinflusst den Ertrag kaum.");
            return tips;
        }

        // Untergrund-Bewertung
        if (albedo >= 0.55)
            tips.Add("Sehr heller Untergrund — ideal fuer die Rueckseite. Hier holst du den maximalen Mehrertrag.");
        else if (albedo >= 0.30)
            tips.Add("Mittelheller Untergrund — solider Rueckseiten-Ertrag.");
        else
            tips.Add("Dunkler Untergrund — eine helle Plane oder hellen Kies unterlegen kann den Mehrertrag nahezu verdoppeln.");

        // Konkrete Hebel (aus der Recherche)
        tips.Add("Rueckseite frei halten: keinen Rucksack, dunkle Wand oder das Fahrzeug direkt dahinter stellen.");
        tips.Add("Etwas hoeher aufstellen hilft der Rueckseite (mehr Boden im Blickfeld, weniger Selbstverschattung).");

        if (panel.HasFixedTilts && panel.KickstandTilts.Count > 1)
            tips.Add("Bei sehr hellem Untergrund (Schnee/weisse Flaeche) den steileren Standwinkel waehlen.");

        return tips;
    }
}
