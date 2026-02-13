namespace FitnessRechner.Services;

/// <summary>
/// Interface für Erinnerungs-Notifications.
/// Android: AlarmManager + NotificationChannel.
/// Desktop: Kein Push - nur In-App Check.
/// </summary>
public interface IReminderService
{
    /// <summary>Wasser-Erinnerung aktiviert.</summary>
    bool IsWaterReminderEnabled { get; set; }

    /// <summary>Wasser-Erinnerung Intervall in Stunden (Standard: 2).</summary>
    int WaterReminderIntervalHours { get; set; }

    /// <summary>Gewicht-Erinnerung aktiviert.</summary>
    bool IsWeightReminderEnabled { get; set; }

    /// <summary>Gewicht-Erinnerung Uhrzeit (Standard: 08:00).</summary>
    TimeSpan WeightReminderTime { get; set; }

    /// <summary>Abend-Zusammenfassung aktiviert.</summary>
    bool IsEveningSummaryEnabled { get; set; }

    /// <summary>Abend-Zusammenfassung Uhrzeit (Standard: 21:00).</summary>
    TimeSpan EveningSummaryTime { get; set; }

    /// <summary>Aktualisiert alle geplanten Erinnerungen basierend auf aktuellen Einstellungen.</summary>
    void UpdateSchedule();
}

/// <summary>
/// Basis-Implementierung mit Preferences-Persistenz.
/// Desktop: Nur Settings speichern, keine echten Notifications.
/// </summary>
public class ReminderService : IReminderService
{
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _preferences;

    public ReminderService(MeineApps.Core.Ava.Services.IPreferencesService preferences)
    {
        _preferences = preferences;
        IsWaterReminderEnabled = _preferences.Get("reminder_water_enabled", false);
        WaterReminderIntervalHours = _preferences.Get("reminder_water_hours", 2);
        IsWeightReminderEnabled = _preferences.Get("reminder_weight_enabled", false);
        WeightReminderTime = TimeSpan.FromHours(_preferences.Get("reminder_weight_hour", 8));
        IsEveningSummaryEnabled = _preferences.Get("reminder_evening_enabled", false);
        EveningSummaryTime = TimeSpan.FromHours(_preferences.Get("reminder_evening_hour", 21));
    }

    private bool _isWaterReminderEnabled;
    public bool IsWaterReminderEnabled
    {
        get => _isWaterReminderEnabled;
        set
        {
            _isWaterReminderEnabled = value;
            _preferences.Set("reminder_water_enabled", value);
        }
    }

    private int _waterReminderIntervalHours = 2;
    public int WaterReminderIntervalHours
    {
        get => _waterReminderIntervalHours;
        set
        {
            _waterReminderIntervalHours = Math.Clamp(value, 1, 6);
            _preferences.Set("reminder_water_hours", _waterReminderIntervalHours);
        }
    }

    private bool _isWeightReminderEnabled;
    public bool IsWeightReminderEnabled
    {
        get => _isWeightReminderEnabled;
        set
        {
            _isWeightReminderEnabled = value;
            _preferences.Set("reminder_weight_enabled", value);
        }
    }

    private TimeSpan _weightReminderTime = TimeSpan.FromHours(8);
    public TimeSpan WeightReminderTime
    {
        get => _weightReminderTime;
        set
        {
            _weightReminderTime = value;
            _preferences.Set("reminder_weight_hour", (int)value.TotalHours);
        }
    }

    private bool _isEveningSummaryEnabled;
    public bool IsEveningSummaryEnabled
    {
        get => _isEveningSummaryEnabled;
        set
        {
            _isEveningSummaryEnabled = value;
            _preferences.Set("reminder_evening_enabled", value);
        }
    }

    private TimeSpan _eveningSummaryTime = TimeSpan.FromHours(21);
    public TimeSpan EveningSummaryTime
    {
        get => _eveningSummaryTime;
        set
        {
            _eveningSummaryTime = value;
            _preferences.Set("reminder_evening_hour", (int)value.TotalHours);
        }
    }

    /// <summary>
    /// Desktop: Keine echten Notifications - nur Settings werden gespeichert.
    /// Android überschreibt diese Methode mit AlarmManager-Logik.
    /// </summary>
    public virtual void UpdateSchedule()
    {
        // Desktop: Nur Preferences werden aktualisiert (schon in den Properties)
    }
}
