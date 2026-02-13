using System.Diagnostics;
using System.Globalization;
using MeineApps.Core.Ava.Localization;
using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Orchestriert alle 5 Reminder-Typen:
/// 1. Morgen-Erinnerung (Zeit einzustempeln)
/// 2. Abend-Erinnerung (Noch am Arbeiten?)
/// 3. Pausen-Erinnerung (Nach X Stunden ohne Pause)
/// 4. Überstunden-Warnung (Über MaxDailyHours)
/// 5. Wochenzusammenfassung (Montag Morgen)
/// </summary>
public class ReminderService : IReminderService
{
    // Notification-IDs
    private const string MorningReminderId = "reminder_morning";
    private const string EveningReminderId = "reminder_evening";
    private const string PauseReminderId = "reminder_pause";
    private const string OvertimeReminderId = "reminder_overtime";
    private const string WeeklyReminderId = "reminder_weekly";

    private readonly INotificationService _notification;
    private readonly ITimeTrackingService _tracking;
    private readonly IDatabaseService _database;
    private readonly ILocalizationService _localization;

    private CancellationTokenSource? _pauseTimerCts;
    private CancellationTokenSource? _overtimeTimerCts;
    private bool _disposed;

    public ReminderService(
        INotificationService notification,
        ITimeTrackingService tracking,
        IDatabaseService database,
        ILocalizationService localization)
    {
        _notification = notification;
        _tracking = tracking;
        _database = database;
        _localization = localization;

        // StatusChanged abonnieren
        _tracking.StatusChanged += OnStatusChanged;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _database.GetSettingsAsync();
            var status = _tracking.CurrentStatus;

            // Morgen/Abend-Reminder planen
            await ScheduleMorningReminderAsync(settings);
            await ScheduleEveningReminderAsync(settings);

            // Falls bereits eingecheckt: Timer starten
            if (status == TrackingStatus.Working)
            {
                StartPauseTimer(settings);
                StartOvertimeTimer(settings);
            }

            // Wochenzusammenfassung planen
            await ScheduleWeeklyReminderAsync(settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ReminderService.InitializeAsync Fehler: {ex.Message}");
        }
    }

    public async Task RescheduleAsync()
    {
        try
        {
            // Alle bestehenden Notifications canceln
            await _notification.CancelNotificationAsync(MorningReminderId);
            await _notification.CancelNotificationAsync(EveningReminderId);
            await _notification.CancelNotificationAsync(PauseReminderId);
            await _notification.CancelNotificationAsync(OvertimeReminderId);
            await _notification.CancelNotificationAsync(WeeklyReminderId);

            StopPauseTimer();
            StopOvertimeTimer();

            // Alles neu initialisieren
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ReminderService.RescheduleAsync Fehler: {ex.Message}");
        }
    }

    private async void OnStatusChanged(object? sender, TrackingStatus status)
    {
        try
        {
            var settings = await _database.GetSettingsAsync();

            switch (status)
            {
                case TrackingStatus.Working:
                    // Eingecheckt → Morgen-Reminder canceln, Timer starten
                    await _notification.CancelNotificationAsync(MorningReminderId);
                    StartPauseTimer(settings);
                    StartOvertimeTimer(settings);
                    break;

                case TrackingStatus.OnBreak:
                    // In Pause → Pause-Timer stoppen (Overtime läuft weiter)
                    StopPauseTimer();
                    break;

                case TrackingStatus.Idle:
                    // Ausgecheckt → Abend-Reminder canceln, alle Timer stoppen
                    await _notification.CancelNotificationAsync(EveningReminderId);
                    StopPauseTimer();
                    StopOvertimeTimer();
                    // Morgen/Abend für nächsten Tag planen
                    await ScheduleMorningReminderAsync(settings);
                    await ScheduleEveningReminderAsync(settings);
                    // Wochenzusammenfassung planen
                    await ScheduleWeeklyReminderAsync(settings);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ReminderService.OnStatusChanged Fehler: {ex.Message}");
        }
    }

    // === Morgen-Erinnerung ===

    private async Task ScheduleMorningReminderAsync(WorkSettings settings)
    {
        if (!settings.MorningReminderEnabled) return;

        var nextWorkDay = GetNextWorkDay(settings);
        if (nextWorkDay == null) return;

        var triggerAt = nextWorkDay.Value.Date
            .Add(settings.MorningReminderTime.ToTimeSpan());

        // Nur planen wenn in der Zukunft
        if (triggerAt <= DateTime.Now) return;

        var title = _localization.GetString("ReminderMorningTitle");
        var body = _localization.GetString("ReminderMorningBody");

        await _notification.ScheduleNotificationAsync(MorningReminderId, title, body, triggerAt);
    }

    // === Abend-Erinnerung ===

    private async Task ScheduleEveningReminderAsync(WorkSettings settings)
    {
        if (!settings.EveningReminderEnabled) return;

        // Heute abend wenn eingecheckt, sonst nächster Arbeitstag
        DateTime triggerDate;
        if (_tracking.CurrentStatus != TrackingStatus.Idle && settings.IsWorkDay(DateTime.Today.DayOfWeek))
        {
            triggerDate = DateTime.Today;
        }
        else
        {
            var nextWorkDay = GetNextWorkDay(settings);
            if (nextWorkDay == null) return;
            triggerDate = nextWorkDay.Value.Date;
        }

        var triggerAt = triggerDate.Add(settings.EveningReminderTime.ToTimeSpan());

        if (triggerAt <= DateTime.Now) return;

        var title = _localization.GetString("ReminderEveningTitle");
        var body = _localization.GetString("ReminderEveningBody");

        await _notification.ScheduleNotificationAsync(EveningReminderId, title, body, triggerAt);
    }

    // === Pausen-Erinnerung (Timer-basiert) ===

    private void StartPauseTimer(WorkSettings settings)
    {
        if (!settings.PauseReminderEnabled) return;

        StopPauseTimer();

        var delayHours = settings.PauseReminderAfterHours;
        if (delayHours <= 0) return;

        // Bisherige Arbeitszeit berücksichtigen
        var currentWorkTime = _tracking.GetCurrentSessionDuration();
        var remainingDelay = TimeSpan.FromHours(delayHours) - currentWorkTime;

        if (remainingDelay <= TimeSpan.Zero)
        {
            // Bereits überschritten → sofort benachrichtigen
            _ = ShowPauseReminderAsync(delayHours);
            return;
        }

        _pauseTimerCts = new CancellationTokenSource();
        var token = _pauseTimerCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(remainingDelay, token);
                if (!token.IsCancellationRequested)
                {
                    await ShowPauseReminderAsync(delayHours);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReminderService.PauseTimer Fehler: {ex.Message}");
            }
        }, token);
    }

    private async Task ShowPauseReminderAsync(double hours)
    {
        var title = _localization.GetString("ReminderPauseTitle");
        var bodyTemplate = _localization.GetString("ReminderPauseBody");
        var body = string.Format(bodyTemplate, hours.ToString("0.#", CultureInfo.CurrentCulture));

        await _notification.ShowNotificationAsync(title, body, PauseReminderId);
    }

    private void StopPauseTimer()
    {
        if (_pauseTimerCts != null)
        {
            _pauseTimerCts.Cancel();
            _pauseTimerCts.Dispose();
            _pauseTimerCts = null;
        }
    }

    // === Überstunden-Warnung (Timer-basiert) ===

    private void StartOvertimeTimer(WorkSettings settings)
    {
        if (!settings.OvertimeWarningEnabled) return;

        StopOvertimeTimer();

        var maxHours = settings.MaxDailyHours;
        if (maxHours <= 0) return;

        // Bisherige Arbeitszeit berücksichtigen
        var currentWorkTime = _tracking.GetCurrentSessionDuration();
        var remainingDelay = TimeSpan.FromHours(maxHours) - currentWorkTime;

        if (remainingDelay <= TimeSpan.Zero)
        {
            // Bereits überschritten → sofort benachrichtigen
            _ = ShowOvertimeReminderAsync(maxHours);
            return;
        }

        _overtimeTimerCts = new CancellationTokenSource();
        var token = _overtimeTimerCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(remainingDelay, token);
                if (!token.IsCancellationRequested)
                {
                    await ShowOvertimeReminderAsync(maxHours);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReminderService.OvertimeTimer Fehler: {ex.Message}");
            }
        }, token);
    }

    private async Task ShowOvertimeReminderAsync(int maxHours)
    {
        var title = _localization.GetString("ReminderOvertimeTitle");
        var bodyTemplate = _localization.GetString("ReminderOvertimeBody");
        var body = string.Format(bodyTemplate, maxHours);

        await _notification.ShowNotificationAsync(title, body, OvertimeReminderId);
    }

    private void StopOvertimeTimer()
    {
        if (_overtimeTimerCts != null)
        {
            _overtimeTimerCts.Cancel();
            _overtimeTimerCts.Dispose();
            _overtimeTimerCts = null;
        }
    }

    // === Wochenzusammenfassung ===

    private async Task ScheduleWeeklyReminderAsync(WorkSettings settings)
    {
        // Nächsten Montag berechnen
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7; // Immer nächsten Montag

        var nextMonday = today.AddDays(daysUntilMonday);
        var triggerAt = nextMonday.Add(settings.MorningReminderTime.ToTimeSpan());

        // Wochendaten berechnen (Montag bis Sonntag der aktuellen Woche)
        var mondayThisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        // KW berechnen basierend auf dem Montag der Woche (nicht heute)
        var cal = CultureInfo.CurrentCulture.Calendar;
        var weekNum = cal.GetWeekOfYear(mondayThisWeek, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var sundayThisWeek = mondayThisWeek.AddDays(6);

        var totalWorked = TimeSpan.Zero;
        var targetMinutes = 0.0;

        for (var day = mondayThisWeek; day <= sundayThisWeek; day = day.AddDays(1))
        {
            var workDay = await _database.GetWorkDayAsync(day);
            if (workDay != null)
            {
                totalWorked += TimeSpan.FromMinutes(workDay.ActualWorkMinutes);
            }

            if (settings.IsWorkDay(day.DayOfWeek))
            {
                targetMinutes += settings.GetDailyMinutesForDay(day.DayOfWeek);
            }
        }

        var targetTime = TimeSpan.FromMinutes(targetMinutes);
        var balance = totalWorked - targetTime;
        var balanceSign = balance >= TimeSpan.Zero ? "+" : "-";
        var balanceAbs = balance.Duration();

        var title = _localization.GetString("ReminderWeeklyTitle");
        var bodyTemplate = _localization.GetString("ReminderWeeklyBody");
        var body = string.Format(bodyTemplate,
            weekNum,
            $"{totalWorked.TotalHours:0.0}h",
            $"{targetTime.TotalHours:0.0}h",
            $"{balanceSign}{balanceAbs.TotalHours:0.0}h");

        await _notification.ScheduleNotificationAsync(WeeklyReminderId, title, body, triggerAt);
    }

    // === Hilfsmethoden ===

    /// <summary>
    /// Findet den nächsten Arbeitstag (heute oder in der Zukunft)
    /// </summary>
    private static DateTime? GetNextWorkDay(WorkSettings settings)
    {
        var date = DateTime.Today;

        // Maximal 7 Tage vorausschauen
        for (int i = 0; i < 8; i++)
        {
            var checkDate = date.AddDays(i);
            if (settings.IsWorkDay(checkDate.DayOfWeek))
            {
                return checkDate;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tracking.StatusChanged -= OnStatusChanged;
        StopPauseTimer();
        StopOvertimeTimer();

        GC.SuppressFinalize(this);
    }
}
