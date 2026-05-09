namespace BomberBlast.Services;

/// <summary>
/// Saisonale Story-Events (v2.0.41, Plan Task 3.4).
///
/// Vier vordefinierte Saison-Events mit fester Datum-Spanne. Bei aktivem Event:
/// <list type="bullet">
///   <item>Welt-Skin-Override (alle Welten bekommen Event-Look)</item>
///   <item>Daily-Hub zeigt zusaetzliche Event-Card</item>
///   <item>Spezielle Floating-Texte / Begruessung beim Start</item>
/// </list>
///
/// Pure Date-Logic (keine Persistenz, deterministisch). Kein Firebase. Keine Push-Notification —
/// das waere eine eigene Iteration mit FCM-Setup.
/// </summary>
public interface IEventService
{
    /// <summary>Aktuell aktives Event (null = kein Event laeuft).</summary>
    SeasonalEvent? CurrentEvent { get; }

    /// <summary>True wenn aktuell ein Event laeuft.</summary>
    bool IsEventActive { get; }

    /// <summary>Tage bis zum naechsten Event (0 wenn aktuell aktiv).</summary>
    int DaysUntilNextEvent { get; }

    /// <summary>Naechstes anstehendes Event (auch wenn schon eines laeuft, das danach folgt).</summary>
    SeasonalEvent? NextEvent { get; }

    /// <summary>Liefert das fuer das angegebene UTC-Datum aktive Event (oder null).</summary>
    SeasonalEvent? GetEventForDate(DateTime utcDate);

    /// <summary>Alle vordefinierten Events (fuer UI-Vorschau-Liste / Settings).</summary>
    IReadOnlyList<SeasonalEvent> AllEvents { get; }
}

/// <summary>
/// Vordefinierte Event-Typen. Datums-Spannen sind hardcoded (siehe EventService).
/// </summary>
public enum SeasonalEventType
{
    /// <summary>Halloween: 25.10. - 02.11. (8 Tage). Welt-Skin: dunkel + Spuk.</summary>
    Halloween,

    /// <summary>Weihnachten: 22.12. - 02.01. (12 Tage). Welt-Skin: schneeweiss + festlich.</summary>
    Christmas,

    /// <summary>Neujahr: 31.12. - 01.01. (Feuerwerks-Burst-Effekte, ueberlappt Christmas).</summary>
    NewYear,

    /// <summary>Sommer: 15.07. - 15.08. (~30 Tage). Welt-Skin: hell + tropisch.</summary>
    Summer,
}

/// <summary>
/// Definiert ein Saison-Event mit Datum, Skin-Override und Begruessungs-Text.
/// </summary>
public class SeasonalEvent
{
    public required SeasonalEventType Type { get; init; }
    public required string NameKey { get; init; }
    public required string DescriptionKey { get; init; }

    /// <summary>RESX-Key fuer Begruessungs-Floating-Text (z.B. "EventHalloweenGreeting").</summary>
    public required string GreetingKey { get; init; }

    /// <summary>Hex-Farbe fuer UI-Akzent waehrend dem Event (z.B. "#FF6F00" fuer Halloween).</summary>
    public required string AccentColor { get; init; }

    /// <summary>Start-Datum im Jahr (Monat + Tag, Jahr ist variabel).</summary>
    public required (int month, int day) Start { get; init; }

    /// <summary>End-Datum im Jahr. Wenn End < Start ⇒ Event ueberspannt Jahres-Wechsel (z.B. Christmas).</summary>
    public required (int month, int day) End { get; init; }
}
