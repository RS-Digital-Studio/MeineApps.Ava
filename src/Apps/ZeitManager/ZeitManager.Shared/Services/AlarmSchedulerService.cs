using System.Globalization;
using System.Timers;
using Avalonia.Threading;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using ZeitManager.Models;
using Timer = System.Timers.Timer;

namespace ZeitManager.Services;

public class AlarmSchedulerService : IAlarmSchedulerService, IDisposable
{
    private readonly IDatabaseService _database;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localization;
    private readonly IPreferencesService _preferences;
    private Timer? _checkTimer;
    private readonly List<AlarmItem> _activeAlarms = [];
    private readonly object _lock = new();
    private readonly HashSet<int> _triggeredToday = [];
    private DateOnly _lastTriggerDate = DateOnly.MinValue;

    public DateTime? PausedUntil { get; private set; }
    public bool IsAllPaused => PausedUntil != null && PausedUntil > DateTime.UtcNow;

    public event EventHandler<AlarmItem>? AlarmTriggered;
    public event EventHandler? AlarmPermissionMissing;

    public AlarmSchedulerService(IDatabaseService database, INotificationService notificationService, ILocalizationService localization, IPreferencesService preferences)
    {
        _database = database;
        _notificationService = notificationService;
        _localization = localization;
        _preferences = preferences;

        // Gespeicherte Pause laden
        var pausedUntilStr = _preferences.Get<string>("alarm_paused_until", "");
        if (!string.IsNullOrEmpty(pausedUntilStr) && DateTime.TryParse(pausedUntilStr, null, DateTimeStyles.RoundtripKind, out var dt))
        {
            if (dt > DateTime.UtcNow)
                PausedUntil = dt;
            else
                _preferences.Set("alarm_paused_until", "");
        }
    }

    public async Task InitializeAsync()
    {
        var alarms = await _database.GetAlarmsAsync();
        lock (_lock)
        {
            _activeAlarms.Clear();
            _activeAlarms.AddRange(alarms.Where(a => a.IsEnabled && !a.IsShiftAlarm));
        }
        EnsureCheckTimer();

        // Alarm-Permission pruefen und warnen
        if (!_notificationService.CanScheduleExactAlarms())
        {
            AlarmPermissionMissing?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Alle aktiven Alarme beim Android AlarmManager registrieren
        List<AlarmItem> snapshot;
        lock (_lock) snapshot = _activeAlarms.ToList();
        foreach (var alarm in snapshot)
        {
            await ScheduleSystemNotificationAsync(alarm);
        }
    }

    public async Task ScheduleAlarmAsync(AlarmItem alarm)
    {
        lock (_lock)
        {
            _activeAlarms.RemoveAll(a => a.Id == alarm.Id);
            if (alarm.IsEnabled)
            {
                alarm.CurrentSnoozeCount = 0;
                _activeAlarms.Add(alarm);
            }
        }
        await _database.SaveAlarmAsync(alarm);
        EnsureCheckTimer();

        // Android AlarmManager: planen oder canceln
        if (alarm.IsEnabled)
        {
            if (!_notificationService.CanScheduleExactAlarms())
            {
                AlarmPermissionMissing?.Invoke(this, EventArgs.Empty);
                return;
            }
            await ScheduleSystemNotificationAsync(alarm);
        }
        else
        {
            await _notificationService.CancelNotificationAsync(alarm.Id.ToString());
        }
    }

    public async Task CancelAlarmAsync(AlarmItem alarm)
    {
        lock (_lock) _activeAlarms.RemoveAll(a => a.Id == alarm.Id);
        alarm.CurrentSnoozeCount = 0;
        await _database.SaveAlarmAsync(alarm);
        CheckStopTimer();
        await _notificationService.CancelNotificationAsync(alarm.Id.ToString());
    }

    public async Task SnoozeAlarmAsync(AlarmItem alarm)
    {
        if (alarm.CurrentSnoozeCount >= alarm.MaxSnoozeCount) return;

        alarm.CurrentSnoozeCount++;
        alarm.NextTriggerDateTime = DateTime.Now.AddMinutes(alarm.SnoozeDurationMinutes);
        await _database.SaveAlarmAsync(alarm);

        lock (_lock)
        {
            if (!_activeAlarms.Any(a => a.Id == alarm.Id))
                _activeAlarms.Add(alarm);
        }
        EnsureCheckTimer();

        // Snooze-Notification beim System planen
        var alarmName = string.IsNullOrEmpty(alarm.Name) ? _localization.GetString("Alarm") : alarm.Name;
        await _notificationService.ScheduleNotificationAsync(
            alarm.Id.ToString(),
            alarmName,
            alarm.TimeFormatted,
            alarm.NextTriggerDateTime.Value);
    }

    public async Task DismissAlarmAsync(AlarmItem alarm)
    {
        alarm.CurrentSnoozeCount = 0;
        alarm.NextTriggerDateTime = null;

        if (!alarm.IsRepeating)
        {
            alarm.IsEnabled = false;
            lock (_lock) _activeAlarms.RemoveAll(a => a.Id == alarm.Id);
        }

        await _database.SaveAlarmAsync(alarm);
        await _notificationService.CancelNotificationAsync(alarm.Id.ToString());

        // Wiederholenden Alarm fuer den naechsten Auslösezeitpunkt neu planen
        if (alarm.IsRepeating && alarm.IsEnabled)
            await ScheduleSystemNotificationAsync(alarm);
    }

    public async Task PauseAllAlarmsAsync(DateTime pauseUntil)
    {
        PausedUntil = pauseUntil;
        _preferences.Set("alarm_paused_until", pauseUntil.ToString("O"));

        // Alle aktiven Alarm-Notifications canceln
        List<AlarmItem> snapshot;
        lock (_lock) snapshot = _activeAlarms.ToList();
        foreach (var alarm in snapshot)
            await _notificationService.CancelNotificationAsync(alarm.Id.ToString());
    }

    public async Task ResumeAllAlarmsAsync()
    {
        PausedUntil = null;
        _preferences.Set("alarm_paused_until", "");

        // Alle aktiven Alarme neu planen
        List<AlarmItem> snapshot;
        lock (_lock) snapshot = _activeAlarms.ToList();
        foreach (var alarm in snapshot)
            await ScheduleSystemNotificationAsync(alarm);
    }

    /// <summary>
    /// Plant eine System-Notification (Android AlarmManager / Desktop Task.Delay)
    /// fuer den naechsten Auslösezeitpunkt des Alarms.
    /// </summary>
    private async Task ScheduleSystemNotificationAsync(AlarmItem alarm)
    {
        var nextTrigger = CalculateNextTriggerTime(alarm);
        if (nextTrigger == null) return;

        var alarmName = string.IsNullOrEmpty(alarm.Name) ? _localization.GetString("Alarm") : alarm.Name;
        await _notificationService.ScheduleNotificationAsync(
            alarm.Id.ToString(),
            alarmName,
            alarm.TimeFormatted,
            nextTrigger.Value);
    }

    /// <summary>
    /// Berechnet den naechsten Auslösezeitpunkt fuer einen Alarm.
    /// </summary>
    private static DateTime? CalculateNextTriggerTime(AlarmItem alarm)
    {
        // Snoozed: NextTriggerDateTime verwenden
        if (alarm.NextTriggerDateTime != null)
            return alarm.NextTriggerDateTime.Value;

        var now = DateTime.Now;
        var alarmToday = now.Date + alarm.Time.ToTimeSpan();

        if (!alarm.IsRepeating)
        {
            // Einmalig: heute wenn in der Zukunft, sonst morgen
            return alarmToday > now ? alarmToday : alarmToday.AddDays(1);
        }

        // Wiederholend: naechsten passenden Wochentag finden
        for (int i = 0; i < 7; i++)
        {
            var candidate = alarmToday.AddDays(i);
            if (alarm.RepeatDaysEnum.HasFlag(ToWeekDays(candidate.DayOfWeek)) && candidate > now)
                return candidate;
        }

        // Fallback
        return alarmToday.AddDays(1);
    }

    /// <summary>
    /// Konvertiert System.DayOfWeek zu WeekDays-Flag.
    /// Eliminiert Switch-Duplikate in CalculateNextTriggerTime und ShouldTrigger.
    /// </summary>
    private static WeekDays ToWeekDays(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        DayOfWeek.Sunday => WeekDays.Sunday,
        _ => WeekDays.None
    };

    private void EnsureCheckTimer()
    {
        if (_checkTimer != null) return;
        _checkTimer = new Timer(10_000); // Check every 10 seconds
        _checkTimer.Elapsed += OnCheckTimerTick;
        _checkTimer.Start();
    }

    private void CheckStopTimer()
    {
        int count;
        lock (_lock) count = _activeAlarms.Count;
        if (count == 0 && _checkTimer != null)
        {
            _checkTimer.Stop();
            _checkTimer.Dispose();
            _checkTimer = null;
        }
    }

    private void OnCheckTimerTick(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            // Reset triggered set on new day
            if (today != _lastTriggerDate)
            {
                _triggeredToday.Clear();
                _lastTriggerDate = today;
            }

            // Abgelaufene Pause aufheben
            if (PausedUntil != null && DateTime.UtcNow >= PausedUntil)
            {
                _ = ResumeAllAlarmsAsync();
            }

            List<AlarmItem> alarms;
            lock (_lock) alarms = _activeAlarms.ToList();
            foreach (var alarm in alarms)
            {
                if (ShouldTrigger(alarm, now))
                {
                    // Double-Trigger-Schutz
                    var triggerKey = alarm.NextTriggerDateTime != null
                        ? alarm.Id * 10000 + now.Hour * 100 + now.Minute + alarm.CurrentSnoozeCount
                        : alarm.Id * 10000 + now.Hour * 100 + now.Minute;

                    if (_triggeredToday.Contains(triggerKey)) continue;
                    _triggeredToday.Add(triggerKey);

                    // Snooze nach Trigger zuruecksetzen
                    alarm.NextTriggerDateTime = null;

                    AlarmTriggered?.Invoke(this, alarm);
                }
            }
        });
    }

    private bool ShouldTrigger(AlarmItem alarm, DateTime now)
    {
        // Urlaubsmodus aktiv → keine Alarme auslösen
        if (IsAllPaused) return false;

        // Snoozed Alarm pruefen
        if (alarm.NextTriggerDateTime != null)
        {
            return now >= alarm.NextTriggerDateTime.Value;
        }

        // Regulaere Alarmzeit pruefen
        var alarmTime = alarm.Time;
        if (now.Hour != alarmTime.Hour || now.Minute != alarmTime.Minute)
            return false;

        // Nur in den ersten 30 Sekunden der Minute auslösen
        if (now.Second > 30)
            return false;

        // Wochentag pruefen
        if (alarm.IsRepeating)
        {
            return alarm.RepeatDaysEnum.HasFlag(ToWeekDays(now.DayOfWeek));
        }

        return true;
    }

    public void Dispose()
    {
        _checkTimer?.Stop();
        _checkTimer?.Dispose();
        _checkTimer = null;
    }
}
