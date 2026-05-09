namespace BomberBlast.Services;

/// <summary>
/// Saisonale Story-Events mit hardcoded Datum-Spannen (v2.0.41, Plan Task 3.4).
/// Pure Date-Logic — keine Persistenz, kein Firebase, deterministisch.
/// </summary>
public sealed class EventService : IEventService
{
    // Reihenfolge ist wichtig: GetEventForDate matcht den ersten passenden Event.
    // Spezifischere/kuerzere Events MUESSEN vor breiteren stehen, damit z.B. NewYear (31.12.-01.01.)
    // am 31.12. nicht von Christmas (22.12.-02.01.) gefressen wird.
    private static readonly SeasonalEvent[] _events =
    [
        // NewYear vor Christmas: 31.12./01.01. faellt in beide, NewYear hat Vorrang.
        new SeasonalEvent
        {
            Type = SeasonalEventType.NewYear,
            NameKey = "EventNewYear",
            DescriptionKey = "EventNewYearDesc",
            GreetingKey = "EventNewYearGreeting",
            AccentColor = "#FFD700",  // Feuerwerks-Gold
            Start = (12, 31),
            End = (1, 1),
        },
        new SeasonalEvent
        {
            Type = SeasonalEventType.Christmas,
            NameKey = "EventChristmas",
            DescriptionKey = "EventChristmasDesc",
            GreetingKey = "EventChristmasGreeting",
            AccentColor = "#C62828",  // Weihnachts-Rot
            Start = (12, 22),
            End = (1, 2),  // ueberspannt Jahres-Wechsel
        },
        new SeasonalEvent
        {
            Type = SeasonalEventType.Halloween,
            NameKey = "EventHalloween",
            DescriptionKey = "EventHalloweenDesc",
            GreetingKey = "EventHalloweenGreeting",
            AccentColor = "#FF6F00",  // Kuerbis-Orange
            Start = (10, 25),
            End = (11, 2),
        },
        new SeasonalEvent
        {
            Type = SeasonalEventType.Summer,
            NameKey = "EventSummer",
            DescriptionKey = "EventSummerDesc",
            GreetingKey = "EventSummerGreeting",
            AccentColor = "#0288D1",  // Tropen-Blau
            Start = (7, 15),
            End = (8, 15),
        },
    ];

    public SeasonalEvent? CurrentEvent => GetEventForDate(DateTime.UtcNow);
    public bool IsEventActive => CurrentEvent != null;
    public IReadOnlyList<SeasonalEvent> AllEvents => _events;

    public SeasonalEvent? GetEventForDate(DateTime utcDate)
    {
        // Order matters: NewYear (kuerzeste Spanne) hat Vorrang ueber Christmas (auch am 31.12./01.01. aktiv).
        // Wir pruefen NewYear zuerst, dann Christmas, dann Halloween/Summer (disjunkte Bereiche).
        foreach (var ev in _events)
        {
            if (IsDateInEventRange(utcDate, ev))
                return ev;
        }
        return null;
    }

    public int DaysUntilNextEvent
    {
        get
        {
            var next = NextEvent;
            if (next == null) return 0;
            var startThisYear = ResolveStartDate(next, DateTime.UtcNow.Year);
            // Wenn Start dieses Jahr schon vergangen → naechstes Jahr
            if (startThisYear < DateTime.UtcNow.Date)
                startThisYear = ResolveStartDate(next, DateTime.UtcNow.Year + 1);
            return Math.Max(0, (startThisYear - DateTime.UtcNow.Date).Days);
        }
    }

    public SeasonalEvent? NextEvent
    {
        get
        {
            var now = DateTime.UtcNow.Date;
            SeasonalEvent? bestCandidate = null;
            DateTime bestStart = DateTime.MaxValue;

            foreach (var ev in _events)
            {
                var startThisYear = ResolveStartDate(ev, now.Year);
                var startNextYear = ResolveStartDate(ev, now.Year + 1);
                // Wenn dieses Jahr noch in der Zukunft → kandidat
                var candidate = startThisYear >= now ? startThisYear : startNextYear;
                if (candidate < bestStart)
                {
                    bestStart = candidate;
                    bestCandidate = ev;
                }
            }
            return bestCandidate;
        }
    }

    /// <summary>
    /// Prueft ob das gegebene Datum innerhalb der Event-Spanne liegt.
    /// Beruecksichtigt Jahres-Wechsel-Spannen (z.B. Christmas 22.12. - 02.01.).
    /// </summary>
    private static bool IsDateInEventRange(DateTime utcDate, SeasonalEvent ev)
    {
        var date = utcDate.Date;
        int year = date.Year;

        // Standard-Fall: Start <= End im selben Jahr
        if (ev.End.month > ev.Start.month ||
            (ev.End.month == ev.Start.month && ev.End.day >= ev.Start.day))
        {
            var start = new DateTime(year, ev.Start.month, ev.Start.day);
            var end = new DateTime(year, ev.End.month, ev.End.day);
            return date >= start && date <= end;
        }

        // Spannt Jahres-Wechsel: Start z.B. 22.12., End 02.01.
        // → Gilt wenn (date >= Start dieses Jahres) ODER (date <= End dieses Jahres mit Jahr-1-Pruefung)
        var startThisYear = new DateTime(year, ev.Start.month, ev.Start.day);
        var endThisYear = new DateTime(year, ev.End.month, ev.End.day);
        if (date >= startThisYear) return true;
        if (date <= endThisYear)
        {
            // Pruefen ob Vorjahres-Spanne gestartet hat
            var startLastYear = new DateTime(year - 1, ev.Start.month, ev.Start.day);
            return date >= startLastYear;
        }
        return false;
    }

    private static DateTime ResolveStartDate(SeasonalEvent ev, int year)
        => new DateTime(year, ev.Start.month, ev.Start.day);
}
