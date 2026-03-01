using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Alle Benutzer-Einstellungen für WorkTime Pro
/// </summary>
[Table("WorkSettings")]
public class WorkSettings
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // === Basis-Einstellungen ===

    /// <summary>
    /// Tägliche Soll-Arbeitszeit in Stunden (Standard: 8.0)
    /// </summary>
    public double DailyHours { get; set; } = 8.0;

    /// <summary>
    /// Wöchentliche Soll-Arbeitszeit in Stunden (Standard: 40.0)
    /// </summary>
    public double WeeklyHours { get; set; } = 40.0;

    /// <summary>
    /// Arbeitstage als Komma-getrennte Wochentage (1=Mo, 7=So)
    /// Standard: "1,2,3,4,5" = Mo-Fr
    /// </summary>
    public string WorkDays { get; set; } = "1,2,3,4,5";

    /// <summary>
    /// Individuelle Stunden pro Tag (JSON: {"1":8.0,"2":8.0,...})
    /// Leer = DailyHours für alle Tage
    /// </summary>
    public string DailyHoursPerDay { get; set; } = "";

    // === Urlaub ===

    /// <summary>
    /// Urlaubstage pro Jahr (Standard: 30)
    /// </summary>
    public int VacationDaysPerYear { get; set; } = 30;

    // === Auto-Pause ===

    /// <summary>
    /// Auto-Pause aktiviert?
    /// </summary>
    public bool AutoPauseEnabled { get; set; } = true;

    /// <summary>
    /// Nach wie vielen Stunden greift Auto-Pause?
    /// </summary>
    public double AutoPauseAfterHours { get; set; } = 6.0;

    /// <summary>
    /// Wie viele Minuten Pause sind gesetzlich vorgeschrieben?
    /// </summary>
    public int AutoPauseMinutes { get; set; } = 30;

    /// <summary>
    /// Nach 9 Stunden: zusätzliche Pausenzeit (in Minuten)
    /// </summary>
    public int AutoPauseMinutesOver9Hours { get; set; } = 45;

    // === Erinnerungen ===

    /// <summary>
    /// Morgen-Erinnerung aktiviert?
    /// </summary>
    public bool MorningReminderEnabled { get; set; } = true;

    /// <summary>
    /// Uhrzeit der Morgen-Erinnerung (als Ticks)
    /// </summary>
    public long MorningReminderTimeTicks { get; set; } = new TimeOnly(8, 0).Ticks;

    /// <summary>
    /// Feierabend-Erinnerung aktiviert?
    /// </summary>
    public bool EveningReminderEnabled { get; set; } = true;

    /// <summary>
    /// Uhrzeit der Feierabend-Erinnerung (als Ticks)
    /// </summary>
    public long EveningReminderTimeTicks { get; set; } = new TimeOnly(18, 0).Ticks;

    /// <summary>
    /// Pausen-Erinnerung aktiviert?
    /// </summary>
    public bool PauseReminderEnabled { get; set; } = true;

    /// <summary>
    /// Nach wie vielen Stunden soll an Pause erinnert werden?
    /// </summary>
    public double PauseReminderAfterHours { get; set; } = 4.0;

    // === Überstunden ===

    /// <summary>
    /// Überstunden-Warnung aktiviert?
    /// </summary>
    public bool OvertimeWarningEnabled { get; set; } = true;

    /// <summary>
    /// Ab wie vielen Überstunden pro Woche warnen?
    /// </summary>
    public double OvertimeWarningHours { get; set; } = 10.0;

    // === Feiertage ===

    /// <summary>
    /// Feiertags-Region (z.B. "DE-BY" für Bayern)
    /// </summary>
    public string HolidayRegion { get; set; } = "DE-BY";

    // === Cloud & Backup ===

    /// <summary>
    /// Cloud-Backup aktiviert?
    /// </summary>
    public bool CloudBackupEnabled { get; set; } = false;

    /// <summary>
    /// Cloud-Provider (GoogleDrive, OneDrive)
    /// </summary>
    public CloudProvider CloudProvider { get; set; } = CloudProvider.None;

    /// <summary>
    /// Automatische Backup-Frequenz in Tagen (0 = nur manuell)
    /// </summary>
    public int AutoBackupDays { get; set; } = 7;

    /// <summary>
    /// Letztes Backup-Datum
    /// </summary>
    public DateTime? LastBackupDate { get; set; }

    // === Arbeitszeitgesetz ===

    /// <summary>
    /// Arbeitszeitgesetz-Prüfung aktiviert?
    /// </summary>
    public bool LegalComplianceEnabled { get; set; } = true;

    /// <summary>
    /// Maximale tägliche Arbeitszeit (Standard: 10h nach ArbZG)
    /// </summary>
    public int MaxDailyHours { get; set; } = 10;

    /// <summary>
    /// Minimale Ruhezeit zwischen Schichten (Standard: 11h nach ArbZG)
    /// </summary>
    public int MinRestHours { get; set; } = 11;

    // === Zeitrundung ===

    /// <summary>
    /// Rundung der Arbeitszeit in Minuten (0 = keine Rundung, 5/10/15/30)
    /// </summary>
    public int RoundingMinutes { get; set; } = 0;

    // === Stundenlohn ===

    /// <summary>
    /// Stundenlohn für Verdienst-Berechnung (0 = deaktiviert)
    /// </summary>
    public double HourlyRate { get; set; } = 0;

    // === UI-Einstellungen ===

    /// <summary>
    /// Standard-Ansicht nach App-Start (0=Tag, 1=Woche, 2=Monat)
    /// </summary>
    public int DefaultView { get; set; } = 0;

    /// <summary>
    /// Wochenstart (1=Montag, 7=Sonntag)
    /// </summary>
    public int WeekStartDay { get; set; } = 1;

    /// <summary>
    /// 24-Stunden-Format verwenden?
    /// </summary>
    public bool Use24HourFormat { get; set; } = true;

    /// <summary>
    /// Widget-Farbe (Hex-Code)
    /// </summary>
    public string WidgetColor { get; set; } = "#1565C0";

    // === Timestamps ===

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // === Helper Properties ===

    /// <summary>
    /// Morgen-Erinnerung als TimeOnly
    /// </summary>
    [Ignore]
    public TimeOnly MorningReminderTime
    {
        get => new(MorningReminderTimeTicks);
        set => MorningReminderTimeTicks = value.Ticks;
    }

    /// <summary>
    /// Feierabend-Erinnerung als TimeOnly
    /// </summary>
    [Ignore]
    public TimeOnly EveningReminderTime
    {
        get => new(EveningReminderTimeTicks);
        set => EveningReminderTimeTicks = value.Ticks;
    }

    /// <summary>
    /// Morgen-Erinnerung Stunde
    /// </summary>
    [Ignore]
    public int MorningReminderHour => MorningReminderTime.Hour;

    /// <summary>
    /// Morgen-Erinnerung Minute
    /// </summary>
    [Ignore]
    public int MorningReminderMinute => MorningReminderTime.Minute;

    /// <summary>
    /// Abend-Erinnerung Stunde
    /// </summary>
    [Ignore]
    public int EveningReminderHour => EveningReminderTime.Hour;

    /// <summary>
    /// Abend-Erinnerung Minute
    /// </summary>
    [Ignore]
    public int EveningReminderMinute => EveningReminderTime.Minute;

    /// <summary>
    /// Tägliche Soll-Zeit in Minuten
    /// </summary>
    [Ignore]
    public int DailyMinutes => (int)(DailyHours * 60);

    /// <summary>
    /// Wöchentliche Soll-Zeit in Minuten
    /// </summary>
    [Ignore]
    public int WeeklyMinutes => (int)(WeeklyHours * 60);

    // Cache für WorkDaysArray (wird bei jedem set/get von WorkDays invalidiert)
    private int[]? _cachedWorkDaysArray;
    private string? _cachedWorkDaysString;

    // Cache für DailyHoursPerDay JSON (wird nur bei Änderung neu deserialisiert)
    private Dictionary<string, double>? _dailyHoursCache;
    private string? _cachedDailyHoursJson;

    /// <summary>
    /// Arbeitstage als int-Array (gecacht, da häufig aufgerufen)
    /// </summary>
    [Ignore]
    public int[] WorkDaysArray
    {
        get
        {
            if (string.IsNullOrWhiteSpace(WorkDays))
                return Array.Empty<int>();

            // Cache invalidieren wenn sich der String geändert hat
            if (_cachedWorkDaysArray == null || _cachedWorkDaysString != WorkDays)
            {
                _cachedWorkDaysString = WorkDays;
                _cachedWorkDaysArray = WorkDays.Split(',')
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => int.TryParse(s.Trim(), out var v) ? v : -1)
                    .Where(v => v >= 0)
                    .ToArray();
            }
            return _cachedWorkDaysArray;
        }
        set
        {
            WorkDays = string.Join(",", value);
            _cachedWorkDaysArray = value;
            _cachedWorkDaysString = WorkDays;
        }
    }

    /// <summary>
    /// Prüft ob ein Wochentag ein Arbeitstag ist
    /// </summary>
    public bool IsWorkDay(DayOfWeek dayOfWeek)
    {
        // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        // Unser Format: Monday=1, ..., Sunday=7
        var ourDay = dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
        return WorkDaysArray.Contains(ourDay);
    }

    /// <summary>
    /// Gibt die Soll-Minuten für einen bestimmten Wochentag zurück.
    /// Berücksichtigt individuelle Tagesstunden wenn konfiguriert.
    /// </summary>
    public int GetDailyMinutesForDay(DayOfWeek dayOfWeek)
    {
        if (string.IsNullOrEmpty(DailyHoursPerDay))
            return DailyMinutes;

        var ourDay = dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
        return (int)(GetHoursForDay(ourDay) * 60);
    }

    /// <summary>
    /// Holt die Soll-Stunden für einen bestimmten Wochentag.
    /// Gecacht: JSON wird nur bei Änderung von DailyHoursPerDay neu deserialisiert.
    /// </summary>
    public double GetHoursForDay(int dayOfWeek)
    {
        if (string.IsNullOrEmpty(DailyHoursPerDay))
            return DailyHours;

        try
        {
            // Cache invalidieren wenn sich der JSON-String geändert hat
            if (_dailyHoursCache == null || _cachedDailyHoursJson != DailyHoursPerDay)
            {
                _cachedDailyHoursJson = DailyHoursPerDay;
                _dailyHoursCache = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(DailyHoursPerDay);
            }

            if (_dailyHoursCache != null && _dailyHoursCache.TryGetValue(dayOfWeek.ToString(), out var hours))
                return hours;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DailyHoursPerDay JSON-Fehler: {ex.Message}");
        }

        return DailyHours;
    }

    /// <summary>
    /// Setzt die Soll-Stunden für einen bestimmten Wochentag
    /// </summary>
    public void SetHoursForDay(int dayOfWeek, double hours)
    {
        Dictionary<string, double> dict;
        try
        {
            dict = string.IsNullOrEmpty(DailyHoursPerDay)
                ? new Dictionary<string, double>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(DailyHoursPerDay)
                  ?? new Dictionary<string, double>();
        }
        catch
        {
            dict = new Dictionary<string, double>();
        }

        dict[dayOfWeek.ToString()] = hours;
        DailyHoursPerDay = System.Text.Json.JsonSerializer.Serialize(dict);

        // Cache aktualisieren
        _dailyHoursCache = dict;
        _cachedDailyHoursJson = DailyHoursPerDay;
    }

    /// <summary>
    /// Berechnet gesetzliche Pausenzeit für gegebene Arbeitszeit
    /// </summary>
    public int GetRequiredPauseMinutes(int workMinutes)
    {
        if (!AutoPauseEnabled) return 0;

        var workHours = workMinutes / 60.0;

        // Über 9 Stunden: 45 Minuten
        if (workHours > 9)
            return AutoPauseMinutesOver9Hours;

        // Über 6 Stunden: 30 Minuten
        if (workHours > AutoPauseAfterHours)
            return AutoPauseMinutes;

        return 0;
    }
}
