namespace BomberBlast.Services;

/// <summary>
/// Wöchentlicher Event-Calendar (Phase 20 — L2).
///
/// <para>Im Gegensatz zu <see cref="IEventService"/> (4 fixe Saison-Events) liefert dieser Service
/// für JEDE ISO-Woche ein deterministisches Event aus einem rotierenden Pool. Vorbild: Brawl Stars
/// "Brawl-Cup" / "Power-Match" Wochen-Events. Spieler haben jede Woche ein neues Limited-Time-Goal.</para>
///
/// <para>Deterministisch: Seed = ISO-Year × 100 + ISO-Week. Kein Firebase nötig — alle Spieler
/// weltweit sehen dasselbe Wochen-Event. Server-Override (Phase 20b) kann später per
/// <see cref="GetWeekEventOverride"/> eingehakt werden.</para>
/// </summary>
public interface IEventCalendarService
{
    /// <summary>Aktuelles Wochen-Event (basierend auf <see cref="DateTime.UtcNow"/>).</summary>
    WeeklyCalendarEvent CurrentEvent { get; }

    /// <summary>Liefert das Event für eine spezifische ISO-Wochen-ID (Year × 100 + Week).</summary>
    WeeklyCalendarEvent GetEventForWeek(int isoYear, int isoWeek);

    /// <summary>Liefert das Event für ein spezifisches UTC-Datum.</summary>
    WeeklyCalendarEvent GetEventForDate(DateTime utcDate);

    /// <summary>Liefert die nächsten N Wochen-Events ab heute (für Vorschau-UI).</summary>
    IReadOnlyList<WeeklyCalendarEvent> GetUpcomingEvents(int weeksAhead = 12);

    /// <summary>
    /// Optional: Server-Override für eine konkrete Woche. Wird in Phase 20b implementiert
    /// (Firebase-Pull). Default-Implementation gibt null zurück.
    /// </summary>
    WeeklyCalendarEvent? GetWeekEventOverride(int isoYear, int isoWeek);
}

/// <summary>
/// Wochen-Event-Typen. Pool-Rotation deterministisch via ISO-Wochen-Seed.
/// </summary>
public enum WeeklyEventType
{
    /// <summary>2× XP für alle Aktivitäten.</summary>
    DoubleXp = 0,
    /// <summary>2× Coins von Story-Levels.</summary>
    DoubleCoins = 1,
    /// <summary>Erhöhte Karten-Drop-Rate (×1.5 + bevorzugt Rare+).</summary>
    CardRain = 2,
    /// <summary>Boss-Rush gibt 2× Wochen-Score-Punkte.</summary>
    BossWeek = 3,
    /// <summary>Dungeon-Loot-Multiplier ×1.5.</summary>
    DungeonRush = 4,
    /// <summary>Liga-Punkte ×1.5 für die Woche.</summary>
    LeagueRumble = 5,
    /// <summary>Daily-Mission-Belohnung +50%.</summary>
    MissionMadness = 6,
    /// <summary>Lucky-Spin: 2 Gratis-Spins/Tag statt 1.</summary>
    LuckyWeek = 7,
}

/// <summary>
/// Ein Wochen-Event mit Typ, Datums-Bereich und Reward-Multiplier.
/// </summary>
public sealed class WeeklyCalendarEvent
{
    public required WeeklyEventType Type { get; init; }

    /// <summary>RESX-Key für lokalisierten Event-Namen (z.B. "WeeklyEvent_DoubleXp").</summary>
    public required string NameKey { get; init; }

    /// <summary>RESX-Key für Beschreibung (z.B. "WeeklyEvent_DoubleXp_Desc").</summary>
    public required string DescriptionKey { get; init; }

    /// <summary>Reward-Multiplier (für DoubleXp/DoubleCoins = 2.0, CardRain = 1.5, ...).</summary>
    public required float Multiplier { get; init; }

    /// <summary>Hex-Akzentfarbe für UI-Banner.</summary>
    public required string AccentColorHex { get; init; }

    /// <summary>Wochen-Start (UTC, Montag 00:00).</summary>
    public required DateTime WeekStartUtc { get; init; }

    /// <summary>Wochen-Ende (UTC, Sonntag 23:59:59).</summary>
    public required DateTime WeekEndUtc { get; init; }

    /// <summary>ISO-Year (z.B. 2026).</summary>
    public required int IsoYear { get; init; }

    /// <summary>ISO-Week-Number (1-53).</summary>
    public required int IsoWeek { get; init; }

    /// <summary>Verbleibende Stunden bis zum Wochen-Ende (für Countdown).</summary>
    public double HoursRemaining(DateTime nowUtc)
        => Math.Max(0, (WeekEndUtc - nowUtc).TotalHours);
}
