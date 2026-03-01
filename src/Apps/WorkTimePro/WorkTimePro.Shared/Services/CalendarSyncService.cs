using System.Text.Json;
using MeineApps.Core.Ava.Services;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the Google Calendar Sync Service
/// </summary>
public class CalendarSyncService : ICalendarSyncService
{
    private readonly IDatabaseService _database;
    private readonly IPreferencesService _preferences;
    private CalendarSyncOptions _options = new();

    private const string PREFERENCES_CONNECTED = "calendar_connected";
    private const string PREFERENCES_EMAIL = "calendar_email";
    private const string PREFERENCES_CALENDAR_ID = "calendar_id";
    private const string PREFERENCES_CALENDAR_NAME = "calendar_name";
    private const string PREFERENCES_LAST_SYNC = "calendar_last_sync";
    private const string PREFERENCES_OPTIONS = "calendar_options";

    public CalendarSyncService(IDatabaseService database, IPreferencesService preferences)
    {
        _database = database;
        _preferences = preferences;
        LoadSettings();
    }

    // === Properties ===

    public bool IsConnected { get; private set; }
    public string? ConnectedEmail { get; private set; }
    public string? CalendarName { get; private set; }
    public DateTime? LastSyncDate { get; private set; }

    private string? _calendarId;

    public event EventHandler<bool>? ConnectionChanged;

    // === Initialization ===

    private void LoadSettings()
    {
        IsConnected = _preferences.Get(PREFERENCES_CONNECTED, false);
        ConnectedEmail = _preferences.Get(PREFERENCES_EMAIL, string.Empty);
        _calendarId = _preferences.Get(PREFERENCES_CALENDAR_ID, string.Empty);
        CalendarName = _preferences.Get(PREFERENCES_CALENDAR_NAME, string.Empty);

        var lastSyncStr = _preferences.Get(PREFERENCES_LAST_SYNC, string.Empty);
        if (!string.IsNullOrEmpty(lastSyncStr) && DateTime.TryParse(lastSyncStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastSync))
        {
            LastSyncDate = lastSync;
        }

        var optionsJson = _preferences.Get(PREFERENCES_OPTIONS, string.Empty);
        if (!string.IsNullOrEmpty(optionsJson))
        {
            try
            {
                _options = JsonSerializer.Deserialize<CalendarSyncOptions>(optionsJson) ?? new();
            }
            catch
            {
                _options = new CalendarSyncOptions();
            }
        }
    }

    private void SaveSettings()
    {
        _preferences.Set(PREFERENCES_CONNECTED, IsConnected);
        _preferences.Set(PREFERENCES_EMAIL, ConnectedEmail ?? string.Empty);
        _preferences.Set(PREFERENCES_CALENDAR_ID, _calendarId ?? string.Empty);
        _preferences.Set(PREFERENCES_CALENDAR_NAME, CalendarName ?? string.Empty);

        if (LastSyncDate.HasValue)
        {
            _preferences.Set(PREFERENCES_LAST_SYNC, LastSyncDate.Value.ToString("O"));
        }

        var optionsJson = JsonSerializer.Serialize(_options);
        _preferences.Set(PREFERENCES_OPTIONS, optionsJson);
    }

    // === Connection ===

    public async Task<bool> ConnectAsync()
    {
        try
        {
            // Benötigt Google OAuth mit Google.Apis.Auth (noch nicht integriert)
            // Platzhalter für UI-Tests
            await Task.Delay(1000);

            IsConnected = true;
            ConnectedEmail = "user@gmail.com"; // Placeholder

            SaveSettings();
            ConnectionChanged?.Invoke(this, true);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            IsConnected = false;
            ConnectedEmail = null;
            _calendarId = null;
            CalendarName = null;

            SaveSettings();
            ConnectionChanged?.Invoke(this, false);

            await Task.CompletedTask;
        }
        catch (Exception)
        {
        }
    }

    public async Task<List<CalendarInfo>> GetAvailableCalendarsAsync()
    {
        var calendars = new List<CalendarInfo>();

        try
        {
            if (!IsConnected)
                return calendars;

            // Benötigt Google Calendar API v3 für echte Kalender-Abfrage

            // Placeholder
            calendars.Add(new CalendarInfo
            {
                Id = "primary",
                Name = "Main Calendar",
                IsPrimary = true,
                BackgroundColor = "#1565C0"
            });

            calendars.Add(new CalendarInfo
            {
                Id = "work",
                Name = "Work",
                IsPrimary = false,
                BackgroundColor = "#4CAF50"
            });

            await Task.CompletedTask;
        }
        catch (Exception)
        {
        }

        return calendars;
    }

    public async Task SetTargetCalendarAsync(string calendarId)
    {
        _calendarId = calendarId;

        var calendars = await GetAvailableCalendarsAsync();
        var calendar = calendars.FirstOrDefault(c => c.Id == calendarId);
        CalendarName = calendar?.Name;

        SaveSettings();
    }

    // === Synchronisation ===

    public async Task<CalendarSyncResult> SyncWorkDaysAsync(DateTime start, DateTime end)
    {
        var result = new CalendarSyncResult { Timestamp = DateTime.UtcNow };

        try
        {
            if (!IsConnected || string.IsNullOrEmpty(_calendarId))
            {
                result.Errors.Add("Not connected or no calendar selected");
                return result;
            }

            var workDays = await _database.GetWorkDaysAsync(start, end);

            foreach (var workDay in workDays)
            {
                if (_options.OnlyDaysWithEntries && workDay.ActualWorkMinutes == 0)
                {
                    result.SkippedEvents++;
                    continue;
                }

                var success = await ExportWorkDayAsync(workDay);
                if (success)
                {
                    result.CreatedEvents++;
                }
                else
                {
                    result.Errors.Add($"Error at {workDay.Date:dd.MM.yyyy}");
                }
            }

            // Sync vacation
            if (_options.SyncVacation)
            {
                var vacations = await _database.GetVacationEntriesAsync(start, end);
                foreach (var vacation in vacations)
                {
                    if (await ExportVacationAsync(vacation))
                    {
                        result.CreatedEvents++;
                    }
                }
            }

            result.Success = result.Errors.Count == 0;
            LastSyncDate = DateTime.UtcNow;
            SaveSettings();
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<bool> ExportWorkDayAsync(WorkDay workDay)
    {
        try
        {
            if (!IsConnected || string.IsNullOrEmpty(_calendarId))
                return false;

            var title = FormatEventTitle(workDay);
            var description = FormatEventDescription(workDay);

            var entries = await _database.GetTimeEntriesAsync(workDay.Date);
            var checkIns = entries.Where(e => e.Type == EntryType.CheckIn).OrderBy(e => e.Timestamp).ToList();
            var checkOuts = entries.Where(e => e.Type == EntryType.CheckOut).OrderByDescending(e => e.Timestamp).ToList();

            DateTime eventStart, eventEnd;

            if (checkIns.Any() && checkOuts.Any())
            {
                eventStart = checkIns.First().Timestamp;
                eventEnd = checkOuts.First().Timestamp;
            }
            else
            {
                eventStart = workDay.Date;
                eventEnd = workDay.Date.AddDays(1);
            }

            // Benötigt Google Calendar API v3: events.insert(calendarId, event)

            await Task.CompletedTask;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> ExportVacationAsync(VacationEntry vacation)
    {
        try
        {
            if (!IsConnected || string.IsNullOrEmpty(_calendarId))
                return false;

            var title = AppStrings.Vacation;
            if (!string.IsNullOrEmpty(vacation.Note))
            {
                title += $": {vacation.Note}";
            }

            // Benötigt Google Calendar API v3: events.insert() mit date statt dateTime

            await Task.CompletedTask;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> DeleteEventAsync(string eventId)
    {
        try
        {
            if (!IsConnected || string.IsNullOrEmpty(_calendarId))
                return false;

            // Benötigt Google Calendar API v3: events.delete(calendarId, eventId)
            await Task.CompletedTask;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // === Settings ===

    public async Task SetSyncOptionsAsync(CalendarSyncOptions options)
    {
        _options = options;
        SaveSettings();
        await Task.CompletedTask;
    }

    public CalendarSyncOptions GetSyncOptions()
    {
        return _options;
    }

    // === Helper methods ===

    private string FormatEventTitle(WorkDay workDay)
    {
        var hours = workDay.ActualWorkMinutes / 60;
        var mins = workDay.ActualWorkMinutes % 60;
        var title = $"{AppStrings.WorkTime}: {hours}:{mins:D2}";

        if (_options.ShowOvertimeInTitle && workDay.BalanceMinutes != 0)
        {
            var sign = workDay.BalanceMinutes >= 0 ? "+" : "-";
            var balanceHours = Math.Abs(workDay.BalanceMinutes) / 60;
            var balanceMins = Math.Abs(workDay.BalanceMinutes) % 60;
            title += $" ({sign}{balanceHours}:{balanceMins:D2})";
        }

        return title;
    }

    private string FormatEventDescription(WorkDay workDay)
    {
        var lines = new List<string>
        {
            $"{AppStrings.WorkTime}: {workDay.ActualWorkMinutes / 60}:{workDay.ActualWorkMinutes % 60:D2}",
            $"{AppStrings.Target}: {workDay.TargetWorkMinutes / 60}:{workDay.TargetWorkMinutes % 60:D2}"
        };

        var totalPause = workDay.ManualPauseMinutes + workDay.AutoPauseMinutes;
        if (totalPause > 0)
        {
            lines.Add($"{AppStrings.Break}: {totalPause / 60}:{totalPause % 60:D2}");
            if (workDay.AutoPauseMinutes > 0)
            {
                lines.Add($"  {string.Format(AppStrings.CalendarAutoBreak, workDay.AutoPauseMinutes)}");
            }
        }

        var balanceSign = workDay.BalanceMinutes >= 0 ? "+" : "";
        lines.Add($"{AppStrings.Balance}: {balanceSign}{workDay.BalanceMinutes / 60}:{Math.Abs(workDay.BalanceMinutes) % 60:D2}");

        if (workDay.Status != DayStatus.WorkDay)
        {
            lines.Add($"Status: {TimeFormatter.GetStatusName(workDay.Status)}");
        }

        lines.Add("");
        lines.Add(AppStrings.CalendarCreatedBy);

        return string.Join("\n", lines);
    }

}
