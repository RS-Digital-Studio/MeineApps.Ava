using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace FitnessRechner.ViewModels;

/// <summary>
/// ViewModel für Aktivitäts-/Sport-Tracking.
/// Zeigt heutige Aktivitäten, verbrannte Kalorien und ein Formular zum Hinzufügen.
/// </summary>
public sealed partial class ActivityViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly IActivityService _activityService;
    private readonly ITrackingService _trackingService;
    private readonly IPreferencesService _preferences;
    private readonly ILocalizationService _localization;
    private readonly IHapticService _hapticService;

    /// <summary>Navigation anfordern (z.B. ".." für zurück).</summary>
    public event Action<string>? NavigationRequested;

    /// <summary>Nachricht anzeigen (Titel, Text).</summary>
    public event Action<string, string>? MessageRequested;

    /// <summary>Floating Text anzeigen (text, category).</summary>
    public event Action<string, string>? FloatingTextRequested;

    // =====================================================================
    // Tages-Übersicht
    // =====================================================================

    /// <summary>Alle Aktivitäten von heute.</summary>
    public ObservableCollection<ActivityEntry> TodayActivities { get; } = new();

    /// <summary>Heute verbrannte Kalorien (Summe aller Aktivitäten).</summary>
    [ObservableProperty]
    private double _todayBurnedCalories;

    /// <summary>Formatierte Anzeige der heute verbrannten Kalorien.</summary>
    public string TodayBurnedCaloriesDisplay => $"{TodayBurnedCalories:F0} kcal";

    partial void OnTodayBurnedCaloriesChanged(double value)
    {
        OnPropertyChanged(nameof(TodayBurnedCaloriesDisplay));
    }

    /// <summary>True wenn heute mindestens eine Aktivität geloggt wurde.</summary>
    [ObservableProperty]
    private bool _hasActivities;

    // =====================================================================
    // Add-Formular
    // =====================================================================

    /// <summary>True wenn das Hinzufügen-Formular angezeigt wird.</summary>
    [ObservableProperty]
    private bool _showAddForm;

    /// <summary>Suchtext zum Filtern der Aktivitäts-Liste.</summary>
    [ObservableProperty]
    private string _activitySearchQuery = "";

    /// <summary>Die aktuell ausgewählte Aktivität aus der Datenbank.</summary>
    [ObservableProperty]
    private ActivityDefinition? _selectedActivity;

    /// <summary>Dauer der Aktivität in Minuten.</summary>
    [ObservableProperty]
    private int _durationMinutes = 30;

    /// <summary>Berechnete verbrannte Kalorien (Live-Update).</summary>
    [ObservableProperty]
    private double _calculatedCalories;

    /// <summary>Formatierte Anzeige der berechneten Kalorien.</summary>
    public string CalculatedCaloriesDisplay => $"{CalculatedCalories:F0} kcal";

    /// <summary>Optionale Notiz zur Aktivität.</summary>
    [ObservableProperty]
    private string _activityNote = "";

    /// <summary>Gefilterte Aktivitäten basierend auf der Suchabfrage.</summary>
    public ObservableCollection<ActivityDefinition> AvailableActivities { get; } = new();

    // =====================================================================
    // Live-Berechnung bei Änderung von Aktivität oder Dauer
    // =====================================================================

    partial void OnSelectedActivityChanged(ActivityDefinition? value)
    {
        _ = RecalculateCaloriesAsync();
    }

    partial void OnDurationMinutesChanged(int value)
    {
        _ = RecalculateCaloriesAsync();
    }

    partial void OnCalculatedCaloriesChanged(double value)
    {
        OnPropertyChanged(nameof(CalculatedCaloriesDisplay));
    }

    partial void OnActivitySearchQueryChanged(string value)
    {
        FilterActivities();
    }

    // =====================================================================
    // Lokalisierte Labels
    // =====================================================================

    public string ActivitiesTitle => _localization.GetString("ActivitiesTitle") ?? "Activities";
    public string AddActivityLabel => _localization.GetString("AddActivity") ?? "Add Activity";
    public string ActivityDurationLabel => _localization.GetString("ActivityDuration") ?? "Duration (min)";
    public string CaloriesBurnedLabel => _localization.GetString("CaloriesBurned") ?? "Calories Burned";
    public string TodayBurnedLabel => _localization.GetString("TodayBurned") ?? "Burned Today";
    public string NoActivitiesLabel => _localization.GetString("NoActivities") ?? "No activities today";
    public string SelectActivityLabel => _localization.GetString("SelectActivity") ?? "Select Activity";
    public string ActivitySearchPlaceholder => _localization.GetString("ActivitySearchPlaceholder") ?? "Search activities...";

    // =====================================================================
    // Konstruktor
    // =====================================================================

    public ActivityViewModel(
        IActivityService activityService,
        ITrackingService trackingService,
        IPreferencesService preferences,
        ILocalizationService localization,
        IHapticService hapticService)
    {
        _activityService = activityService;
        _trackingService = trackingService;
        _preferences = preferences;
        _localization = localization;
        _hapticService = hapticService;

        // Initiale Aktivitäten-Liste laden
        FilterActivities();
    }

    // =====================================================================
    // Lifecycle
    // =====================================================================

    /// <summary>
    /// Wird aufgerufen wenn die View sichtbar wird.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        await LoadTodayActivitiesAsync();
    }

    /// <summary>
    /// Aktualisiert lokalisierte Texte nach Sprachwechsel.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(ActivitiesTitle));
        OnPropertyChanged(nameof(AddActivityLabel));
        OnPropertyChanged(nameof(ActivityDurationLabel));
        OnPropertyChanged(nameof(CaloriesBurnedLabel));
        OnPropertyChanged(nameof(TodayBurnedLabel));
        OnPropertyChanged(nameof(NoActivitiesLabel));
        OnPropertyChanged(nameof(SelectActivityLabel));
        OnPropertyChanged(nameof(ActivitySearchPlaceholder));

        // Aktivitäten-Liste neu filtern (lokalisierte Namen könnten sich geändert haben)
        FilterActivities();
    }

    // =====================================================================
    // Commands
    // =====================================================================

    [RelayCommand]
    private void OpenAddForm()
    {
        SelectedActivity = null;
        DurationMinutes = 30;
        CalculatedCalories = 0;
        ActivityNote = "";
        ActivitySearchQuery = "";
        FilterActivities();
        ShowAddForm = true;
    }

    [RelayCommand]
    private void CloseAddForm()
    {
        ShowAddForm = false;
    }

    [RelayCommand]
    private async Task AddActivity()
    {
        if (SelectedActivity == null || DurationMinutes <= 0) return;

        try
        {
            // Aktuelles Gewicht für Berechnung holen
            var weightKg = await GetCurrentWeightAsync();
            var calories = _activityService.CalculateCalories(SelectedActivity.Met, weightKg, DurationMinutes);

            // Lokalisierten Namen für die Aktivität holen
            var activityName = _localization.GetString(SelectedActivity.NameKey) ?? SelectedActivity.NameKey;

            var entry = new ActivityEntry
            {
                Date = DateTime.UtcNow,
                ActivityName = activityName,
                DurationMinutes = DurationMinutes,
                CaloriesBurned = calories,
                MetValue = SelectedActivity.Met,
                Note = string.IsNullOrWhiteSpace(ActivityNote) ? null : ActivityNote.Trim()
            };

            await _activityService.AddActivityAsync(entry);

            ShowAddForm = false;
            _hapticService.Click();

            var text = string.Format(
                _localization.GetString("ActivitySaved") ?? "-{0} kcal",
                calories.ToString("F0"));
            FloatingTextRequested?.Invoke(text, "activity");

            await LoadTodayActivitiesAsync();
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Error saving data");
        }
    }

    [RelayCommand]
    private async Task DeleteActivity(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        try
        {
            await _activityService.DeleteActivityAsync(id);
            _hapticService.Tick();
            await LoadTodayActivitiesAsync();
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorDeletingData") ?? "Error deleting data");
        }
    }

    [RelayCommand]
    private void SelectActivityItem(ActivityDefinition? activity)
    {
        SelectedActivity = activity;
        _hapticService.Tick();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // =====================================================================
    // Private Methoden
    // =====================================================================

    /// <summary>
    /// Lädt alle Aktivitäten von heute und aktualisiert die Summe.
    /// </summary>
    private async Task LoadTodayActivitiesAsync()
    {
        try
        {
            var activities = await _activityService.GetActivitiesAsync(DateTime.Today);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TodayActivities.Clear();
                foreach (var activity in activities)
                    TodayActivities.Add(activity);

                TodayBurnedCalories = activities.Sum(a => a.CaloriesBurned);
                HasActivities = TodayActivities.Count > 0;
            });
        }
        catch (Exception)
        {
            // Fehler beim Laden
        }
    }

    /// <summary>
    /// Filtert die Aktivitäts-Liste basierend auf der Suchabfrage.
    /// Durchsucht lokalisierte Namen.
    /// </summary>
    private void FilterActivities()
    {
        AvailableActivities.Clear();

        var query = ActivitySearchQuery?.Trim() ?? "";
        var allActivities = ActivityDatabase.All;

        foreach (var activity in allActivities)
        {
            if (string.IsNullOrEmpty(query))
            {
                AvailableActivities.Add(activity);
                continue;
            }

            // Lokalisierter Name prüfen
            var localizedName = _localization.GetString(activity.NameKey) ?? activity.NameKey;
            if (localizedName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                AvailableActivities.Add(activity);
            }
        }
    }

    /// <summary>
    /// Berechnet die Kalorien basierend auf ausgewählter Aktivität, Dauer und Gewicht.
    /// </summary>
    private async Task RecalculateCaloriesAsync()
    {
        if (SelectedActivity == null || DurationMinutes <= 0)
        {
            CalculatedCalories = 0;
            return;
        }

        try
        {
            var weightKg = await GetCurrentWeightAsync();
            CalculatedCalories = _activityService.CalculateCalories(
                SelectedActivity.Met, weightKg, DurationMinutes);
        }
        catch
        {
            CalculatedCalories = 0;
        }
    }

    /// <summary>
    /// Holt das aktuelle Gewicht aus dem TrackingService.
    /// Fallback: Preferences oder 70 kg.
    /// </summary>
    private async Task<double> GetCurrentWeightAsync()
    {
        try
        {
            var weightEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            if (weightEntry != null)
                return weightEntry.Value;
        }
        catch
        {
            // Fallback bei Fehler
        }

        // Kein Gewicht geloggt → 70 kg als Standard
        return 70.0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
