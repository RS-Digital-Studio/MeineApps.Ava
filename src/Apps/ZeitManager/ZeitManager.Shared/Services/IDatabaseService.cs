using ZeitManager.Models;

namespace ZeitManager.Services;

public interface IDatabaseService
{
    Task InitializeAsync();

    // Timers
    Task<List<TimerItem>> GetTimersAsync();
    Task<TimerItem?> GetTimerAsync(int id);
    Task<int> SaveTimerAsync(TimerItem timer);
    Task<int> DeleteTimerAsync(TimerItem timer);

    // Alarms
    Task<List<AlarmItem>> GetAlarmsAsync();
    Task<AlarmItem?> GetAlarmAsync(int id);
    Task<int> SaveAlarmAsync(AlarmItem alarm);
    Task<int> DeleteAlarmAsync(AlarmItem alarm);

    // Shift Schedules
    Task<List<ShiftSchedule>> GetShiftSchedulesAsync();
    Task<ShiftSchedule?> GetActiveScheduleAsync();
    Task<int> SaveShiftScheduleAsync(ShiftSchedule schedule);
    Task<int> DeleteShiftScheduleAsync(ShiftSchedule schedule);

    // Shift Exceptions
    Task<List<ShiftException>> GetShiftExceptionsAsync(int scheduleId);
    Task<int> SaveShiftExceptionAsync(ShiftException exception);
    Task<int> DeleteShiftExceptionAsync(ShiftException exception);

    // Timer Presets
    Task<List<TimerPreset>> GetTimerPresetsAsync();
    Task<int> SaveTimerPresetAsync(TimerPreset preset);
    Task<int> DeleteTimerPresetAsync(TimerPreset preset);

    // Focus Sessions (Pomodoro)
    Task<List<FocusSession>> GetFocusSessionsAsync(DateTime from, DateTime to);
    Task<int> SaveFocusSessionAsync(FocusSession session);
}
