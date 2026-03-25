using System.Globalization;
using System.Text.Json;
using FitnessRechner.Models;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.Services;

/// <summary>
/// Intervallfasten-Service mit Persistenz über PreferencesService.
/// Verwaltet Fasten-Pläne (16:8, 18:6, 20:4, Custom), Timer und History.
/// </summary>
public sealed class FastingService : IFastingService
{
    private readonly IPreferencesService _preferences;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = false };
    private const int MaxHistoryEntries = 30;

    // Gecachte History
    private List<FastingRecord>? _historyCache;

    public event Action? FastingStarted;
    public event Action? FastingCompleted;

    public FastingService(IPreferencesService preferences)
    {
        _preferences = preferences;

        // Gespeicherten Plan laden
        var planStr = _preferences.Get(PreferenceKeys.FastingPlan, "Plan16_8");
        SelectedPlan = Enum.TryParse<FastingPlan>(planStr, out var p) ? p : FastingPlan.Plan16_8;

        // Custom-Stunden aus Preferences laden, Fallback auf Plan-Standard
        FastingHours = GetFastingHoursForPlan(SelectedPlan);
    }

    public FastingPlan SelectedPlan { get; set; }

    public int FastingHours { get; set; }

    public int EatingHours => 24 - FastingHours;

    public bool IsActive
    {
        get
        {
            var active = _preferences.Get(PreferenceKeys.FastingIsActive, false);
            if (!active) return false;

            // Nur prüfen ob abgelaufen, KEIN Seiteneffekt
            if (EndTime.HasValue && DateTime.UtcNow >= EndTime.Value)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Prüft ob die Fasten-Periode abgelaufen ist und schließt sie ggf. ab.
    /// Soll im Timer-Tick aufgerufen werden, nicht im Property-Getter.
    /// </summary>
    public void CheckAndCompleteIfDone()
    {
        var active = _preferences.Get(PreferenceKeys.FastingIsActive, false);
        if (!active) return;

        if (EndTime.HasValue && DateTime.UtcNow >= EndTime.Value)
            CompleteFasting();
    }

    public DateTime? StartTime
    {
        get
        {
            var str = _preferences.Get(PreferenceKeys.FastingStartTime, "");
            if (string.IsNullOrEmpty(str)) return null;
            return DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }

    public DateTime? EndTime
    {
        get
        {
            if (!StartTime.HasValue) return null;
            return StartTime.Value.AddHours(FastingHours);
        }
    }

    public TimeSpan ElapsedTime
    {
        get
        {
            if (!StartTime.HasValue || !IsActive) return TimeSpan.Zero;
            var elapsed = DateTime.UtcNow - StartTime.Value;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }
    }

    public TimeSpan RemainingTime
    {
        get
        {
            if (!EndTime.HasValue || !IsActive) return TimeSpan.Zero;
            var remaining = EndTime.Value - DateTime.UtcNow;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public double Progress
    {
        get
        {
            if (!StartTime.HasValue || FastingHours <= 0) return 0;
            var totalSeconds = FastingHours * 3600.0;
            var elapsedSeconds = ElapsedTime.TotalSeconds;
            return Math.Clamp(elapsedSeconds / totalSeconds, 0.0, 1.0);
        }
    }

    public void StartFasting()
    {
        var now = DateTime.UtcNow;
        _preferences.Set(PreferenceKeys.FastingStartTime, now.ToString("O"));
        _preferences.Set(PreferenceKeys.FastingIsActive, true);
        _preferences.Set(PreferenceKeys.FastingPlan, SelectedPlan.ToString());

        // Custom-Stunden persistieren damit sie beim nächsten Start verfügbar sind
        if (SelectedPlan == FastingPlan.Custom)
            _preferences.Set(PreferenceKeys.FastingCustomHours, FastingHours);

        FastingStarted?.Invoke();
    }

    public void StopFasting()
    {
        if (!IsActive) return;

        // Vorzeitig beendet → nicht erfolgreich
        var record = new FastingRecord
        {
            StartTime = StartTime ?? DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            Plan = SelectedPlan,
            FastingHours = FastingHours,
            IsCompleted = false
        };
        AddToHistory(record);

        _preferences.Set(PreferenceKeys.FastingIsActive, false);
        _preferences.Set(PreferenceKeys.FastingStartTime, "");
    }

    public List<FastingRecord> GetHistory()
    {
        if (_historyCache != null) return _historyCache;

        var json = _preferences.Get(PreferenceKeys.FastingHistory, "");
        if (string.IsNullOrEmpty(json))
        {
            _historyCache = [];
            return _historyCache;
        }

        try
        {
            _historyCache = JsonSerializer.Deserialize<List<FastingRecord>>(json, s_jsonOptions) ?? [];
        }
        catch
        {
            _historyCache = [];
        }
        return _historyCache;
    }

    /// <summary>
    /// Fasten-Periode erfolgreich abschließen (Zeit abgelaufen).
    /// </summary>
    private void CompleteFasting()
    {
        var record = new FastingRecord
        {
            StartTime = StartTime ?? DateTime.UtcNow,
            EndTime = EndTime ?? DateTime.UtcNow,
            Plan = SelectedPlan,
            FastingHours = FastingHours,
            IsCompleted = true
        };
        AddToHistory(record);

        _preferences.Set(PreferenceKeys.FastingIsActive, false);
        _preferences.Set(PreferenceKeys.FastingStartTime, "");

        FastingCompleted?.Invoke();
    }

    /// <summary>
    /// Eintrag zur History hinzufügen (max. 30 Einträge behalten).
    /// </summary>
    private void AddToHistory(FastingRecord record)
    {
        var history = GetHistory();
        history.Insert(0, record);

        // Maximal 30 Einträge behalten
        if (history.Count > MaxHistoryEntries)
            history.RemoveRange(MaxHistoryEntries, history.Count - MaxHistoryEntries);

        _historyCache = history;
        var json = JsonSerializer.Serialize(history, s_jsonOptions);
        _preferences.Set(PreferenceKeys.FastingHistory, json);
    }

    /// <summary>
    /// Gibt die Fastenstunden für einen Plan zurück.
    /// Bei Custom werden die gespeicherten Stunden aus Preferences geladen.
    /// </summary>
    private int GetFastingHoursForPlan(FastingPlan plan) => plan switch
    {
        FastingPlan.Plan16_8 => 16,
        FastingPlan.Plan18_6 => 18,
        FastingPlan.Plan20_4 => 20,
        FastingPlan.Custom => _preferences.Get(PreferenceKeys.FastingCustomHours, 16),
        _ => 16
    };
}
