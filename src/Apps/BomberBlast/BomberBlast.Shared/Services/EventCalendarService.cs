using System.Globalization;

namespace BomberBlast.Services;

/// <summary>
/// Implementierung von <see cref="IEventCalendarService"/> (Phase 20 — L2).
///
/// <para>Deterministisch via ISO-Wochen-Seed: Alle Spieler weltweit sehen dasselbe Wochen-Event.
/// Pool von 8 Event-Typen rotiert sequenziell durch (gleicher Spread, kein Häufungs-Bias bei zufälliger
/// Auswahl). Die Reihenfolge ist deterministic-shuffled mit ISO-Year als Salt damit identische Wochen
/// in verschiedenen Jahren nicht stupide gleich sind.</para>
///
/// <para>Server-Override-Hook (<see cref="GetWeekEventOverride"/>) ist Skeleton — Phase 20b
/// implementiert Firebase-Pull für Live-Ops-Override (z.B. "Boss Week" verlängern).</para>
/// </summary>
public sealed class EventCalendarService : IEventCalendarService
{
    private static readonly WeeklyEventType[] Pool =
    {
        WeeklyEventType.DoubleXp,
        WeeklyEventType.DoubleCoins,
        WeeklyEventType.CardRain,
        WeeklyEventType.BossWeek,
        WeeklyEventType.DungeonRush,
        WeeklyEventType.LeagueRumble,
        WeeklyEventType.MissionMadness,
        WeeklyEventType.LuckyWeek,
    };

    public WeeklyCalendarEvent CurrentEvent => GetEventForDate(DateTime.UtcNow);

    public WeeklyCalendarEvent GetEventForDate(DateTime utcDate)
    {
        var (year, week) = GetIsoYearWeek(utcDate);
        return GetEventForWeek(year, week);
    }

    public WeeklyCalendarEvent GetEventForWeek(int isoYear, int isoWeek)
    {
        // Server-Override hat Vorrang
        var overrideEvent = GetWeekEventOverride(isoYear, isoWeek);
        if (overrideEvent != null) return overrideEvent;

        // Deterministische Auswahl: Year-Salt verhindert dass identische Wochen-Nummer
        // jedes Jahr dasselbe Event ergeben. Pool-Index = (isoYear * 7 + isoWeek) % poolSize.
        var idx = ((isoYear * 7) + isoWeek) % Pool.Length;
        if (idx < 0) idx += Pool.Length;
        var type = Pool[idx];

        var (start, end) = GetIsoWeekRange(isoYear, isoWeek);
        return new WeeklyCalendarEvent
        {
            Type = type,
            NameKey = $"WeeklyEvent_{type}",
            DescriptionKey = $"WeeklyEvent_{type}_Desc",
            Multiplier = GetDefaultMultiplier(type),
            AccentColorHex = GetDefaultAccentHex(type),
            WeekStartUtc = start,
            WeekEndUtc = end,
            IsoYear = isoYear,
            IsoWeek = isoWeek,
        };
    }

    public IReadOnlyList<WeeklyCalendarEvent> GetUpcomingEvents(int weeksAhead = 12)
    {
        var result = new List<WeeklyCalendarEvent>(weeksAhead);
        var now = DateTime.UtcNow;
        for (int offset = 0; offset < weeksAhead; offset++)
        {
            var date = now.AddDays(7 * offset);
            var (year, week) = GetIsoYearWeek(date);
            result.Add(GetEventForWeek(year, week));
        }
        return result;
    }

    public WeeklyCalendarEvent? GetWeekEventOverride(int isoYear, int isoWeek)
    {
        // Phase 20b: Firebase-Pull-Stub. Aktuell kein Override.
        return null;
    }

    /// <summary>
    /// ISO-Year/Week-Berechnung (Montag-basiert, ISO 8601). Standard-.NET-Klasse
    /// <see cref="ISOWeek"/> wird verwendet für Korrektheit an Jahres-Übergängen.
    /// </summary>
    public static (int year, int week) GetIsoYearWeek(DateTime utc)
    {
        return (ISOWeek.GetYear(utc), ISOWeek.GetWeekOfYear(utc));
    }

    /// <summary>
    /// Liefert (Montag 00:00 UTC, Sonntag 23:59:59 UTC) für ein gegebenes ISO-Year/Week.
    /// </summary>
    public static (DateTime start, DateTime end) GetIsoWeekRange(int isoYear, int isoWeek)
    {
        var monday = ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday);
        var startUtc = DateTime.SpecifyKind(monday, DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(7).AddTicks(-1);
        return (startUtc, endUtc);
    }

    private static float GetDefaultMultiplier(WeeklyEventType type) => type switch
    {
        WeeklyEventType.DoubleXp => 2.0f,
        WeeklyEventType.DoubleCoins => 2.0f,
        WeeklyEventType.CardRain => 1.5f,
        WeeklyEventType.BossWeek => 2.0f,
        WeeklyEventType.DungeonRush => 1.5f,
        WeeklyEventType.LeagueRumble => 1.5f,
        WeeklyEventType.MissionMadness => 1.5f,
        WeeklyEventType.LuckyWeek => 2.0f,
        _ => 1.0f,
    };

    private static string GetDefaultAccentHex(WeeklyEventType type) => type switch
    {
        WeeklyEventType.DoubleXp => "#3B82F6",      // Blau
        WeeklyEventType.DoubleCoins => "#FBBF24",   // Gold
        WeeklyEventType.CardRain => "#A855F7",      // Lila
        WeeklyEventType.BossWeek => "#DC2626",      // Rot
        WeeklyEventType.DungeonRush => "#7C3AED",   // Dungeon-Lila
        WeeklyEventType.LeagueRumble => "#06B6D4",  // Cyan
        WeeklyEventType.MissionMadness => "#F97316",// Orange
        WeeklyEventType.LuckyWeek => "#10B981",     // Grün
        _ => "#FFFFFF",
    };
}
