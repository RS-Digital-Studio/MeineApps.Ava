using ZeitManager.Models;

namespace ZeitManager.Services;

public interface IAlarmSchedulerService
{
    Task InitializeAsync();
    Task ScheduleAlarmAsync(AlarmItem alarm);
    Task CancelAlarmAsync(AlarmItem alarm);
    Task SnoozeAlarmAsync(AlarmItem alarm);
    Task DismissAlarmAsync(AlarmItem alarm);

    Task PauseAllAlarmsAsync(DateTime pauseUntil);
    Task ResumeAllAlarmsAsync();
    DateTime? PausedUntil { get; }
    bool IsAllPaused { get; }

    event EventHandler<AlarmItem>? AlarmTriggered;
    event EventHandler? AlarmPermissionMissing;
}
