using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.ViewModels;

/// <summary>
/// Tracking-Einträge hinzufügen/bearbeiten/löschen, Undo-Logik, Load/Refresh der Daten.
/// Enthält auch Wasser/Kalorien-Ziele, Weekly Analysis und Export.
/// </summary>
public sealed partial class ProgressViewModel
{
    #region Add / Toggle Form

    [RelayCommand]
    private void ToggleAddForm()
    {
        ShowAddForm = !ShowAddForm;
        if (ShowAddForm)
        {
            ShowFoodSearch = false;
            ResetForm();
        }
    }

    [RelayCommand]
    private async Task AddEntry()
    {
        if (NewValue <= 0)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.InvalidValueEntered);
            return;
        }

        // Bereichsvalidierung je nach Tracking-Typ
        var maxValue = SelectedTab switch
        {
            ProgressTab.Weight => 500.0,
            ProgressTab.Body => IsBmiSelected ? 100.0 : 100.0,
            ProgressTab.Water => 20000.0,
            _ => double.MaxValue
        };

        if (NewValue > maxValue)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.InvalidValueEntered);
            return;
        }

        TrackingType type = SelectedTab switch
        {
            ProgressTab.Weight => TrackingType.Weight,
            ProgressTab.Body => IsBmiSelected ? TrackingType.Bmi : TrackingType.BodyFat,
            ProgressTab.Water => TrackingType.Water,
            _ => TrackingType.Weight
        };

        var entry = new TrackingEntry
        {
            Type = type,
            Value = NewValue,
            Date = NewDate,
            Note = string.IsNullOrWhiteSpace(NewNote) ? null : NewNote.Trim()
        };

        await _trackingService.AddEntryAsync(entry);
        ShowAddForm = false;
        ResetForm();
        await LoadCurrentTabDataAsync();

        // Floating Text Feedback je nach Tracking-Typ
        var displayText = type switch
        {
            TrackingType.Weight => $"{entry.Value:F1} kg",
            TrackingType.Bmi => $"BMI {entry.Value:F1}",
            TrackingType.BodyFat => $"{entry.Value:F1}%",
            TrackingType.Water => $"+{entry.Value:F0} ml",
            _ => $"{entry.Value:F1}"
        };
        FloatingTextRequested?.Invoke(displayText, "info");
    }

    #endregion

    #region Delete / Undo

    [RelayCommand]
    private async Task DeleteEntry(TrackingEntry entry)
    {
        _undoCancellation?.Cancel();
        _undoCancellation = new CancellationTokenSource();

        _recentlyDeletedMeal = null; // Alten Meal-Undo verwerfen
        _recentlyDeletedEntry = entry;

        // Aus der passenden Collection entfernen
        switch (SelectedTab)
        {
            case ProgressTab.Weight:
                WeightEntries.Remove(entry);
                break;
            case ProgressTab.Body:
                if (IsBmiSelected)
                    BmiEntries.Remove(entry);
                else
                    BodyFatEntries.Remove(entry);
                break;
        }

        UndoMessage = string.Format(AppStrings.EntryDeletedOn, entry.Date.ToString("d", CultureInfo.CurrentCulture));
        ShowUndoBanner = true;

        try
        {
            await Task.Delay(PreferenceKeys.UndoTimeoutMs, _undoCancellation.Token);
            await _trackingService.DeleteEntryAsync(entry.Id);
            _recentlyDeletedEntry = null;
            await LoadCurrentTabDataAsync();
        }
        catch (TaskCanceledException)
        {
            // Undo ausgelöst
        }
        finally
        {
            ShowUndoBanner = false;
        }
    }

    [RelayCommand]
    private async Task DeleteMeal(FoodLogEntry entry)
    {
        _undoCancellation?.Cancel();
        _undoCancellation = new CancellationTokenSource();

        _recentlyDeletedEntry = null; // Alten Tracking-Undo verwerfen
        _recentlyDeletedMeal = entry;
        TodayMeals.Remove(entry);
        HasMeals = TodayMeals.Count > 0;

        // Summary lokal aus aktueller UI-Liste berechnen (Eintrag noch in DB)
        RecalculateCalorieDataFromMeals();

        UndoMessage = string.Format(AppStrings.ItemDeleted, entry.FoodName);
        ShowUndoBanner = true;

        try
        {
            await Task.Delay(PreferenceKeys.UndoTimeoutMs, _undoCancellation.Token);
            await _foodSearchService.DeleteFoodLogAsync(entry.Id);
            _recentlyDeletedMeal = null;
            await UpdateCalorieDataAsync();
        }
        catch (TaskCanceledException)
        {
            // Undo ausgelöst
        }
        finally
        {
            ShowUndoBanner = false;
        }
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_recentlyDeletedEntry != null)
        {
            _undoCancellation?.Cancel();

            switch (SelectedTab)
            {
                case ProgressTab.Weight:
                    var weightList = WeightEntries.ToList();
                    weightList.Add(_recentlyDeletedEntry);
                    WeightEntries = new ObservableCollection<TrackingEntry>(weightList.OrderByDescending(e => e.Date));
                    break;
                case ProgressTab.Body:
                    if (IsBmiSelected)
                    {
                        var bmiList = BmiEntries.ToList();
                        bmiList.Add(_recentlyDeletedEntry);
                        BmiEntries = new ObservableCollection<TrackingEntry>(bmiList.OrderByDescending(e => e.Date));
                    }
                    else
                    {
                        var bfList = BodyFatEntries.ToList();
                        bfList.Add(_recentlyDeletedEntry);
                        BodyFatEntries = new ObservableCollection<TrackingEntry>(bfList.OrderByDescending(e => e.Date));
                    }
                    break;
            }

            _recentlyDeletedEntry = null;
            ShowUndoBanner = false;
        }

        if (_recentlyDeletedMeal != null)
        {
            _undoCancellation?.Cancel();
            var meals = TodayMeals.ToList();
            meals.Add(_recentlyDeletedMeal);
            TodayMeals = new ObservableCollection<FoodLogEntry>(meals.OrderBy(m => m.Date));
            HasMeals = TodayMeals.Count > 0;
            _recentlyDeletedMeal = null;
            ShowUndoBanner = false;
            // Lokal berechnen statt DB-Read (Eintrag existiert dort evtl. noch/nicht mehr)
            RecalculateCalorieDataFromMeals();
        }
    }

    #endregion

    #region Quick-Add Wasser

    [RelayCommand]
    private async Task QuickAddWater(string amountStr)
    {
        if (!int.TryParse(amountStr, out var amount)) return;
        try
        {
            var today = await _trackingService.GetLatestEntryAsync(TrackingType.Water);

            if (today != null && today.Date.Date == DateTime.Today)
            {
                today.Value += amount;
                await _trackingService.UpdateEntryAsync(today);
            }
            else
            {
                var entry = new TrackingEntry
                {
                    Type = TrackingType.Water,
                    Value = amount,
                    Date = DateTime.Today
                };
                await _trackingService.AddEntryAsync(entry);
            }

            await LoadWaterDataAsync();

            // Floating Text für Wasser-Hinzufügung
            FloatingTextRequested?.Invoke($"+{amount} ml", "info");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    #endregion

    #region Ziele setzen (Wasser, Kalorien, Gewicht)

    [RelayCommand]
    private void SetWaterGoal()
    {
        if (DailyWaterGoal <= 0)
        {
            DailyWaterGoal = 2500;
        }
        HasWaterGoal = true;
        _preferences.Set(PreferenceKeys.WaterGoal, DailyWaterGoal);
        UpdateWaterStatus();
    }

    [RelayCommand]
    private void SetCalorieGoal()
    {
        if (DailyCalorieGoal <= 0)
        {
            DailyCalorieGoal = 2000;
        }
        _preferences.Set(PreferenceKeys.CalorieGoal, DailyCalorieGoal);
        UpdateCalorieStatus();
    }

    [RelayCommand]
    private void SetWeightGoalValue()
    {
        if (WeightGoal > 0 && WeightGoal <= 500)
        {
            _preferences.Set(PreferenceKeys.WeightGoal, WeightGoal);
            HasWeightGoal = true;
            UpdateWeightGoalStatus();
        }
    }

    /// <summary>
    /// Speichert das Wasser-Ziel von einem User-eingegebenen Wert.
    /// </summary>
    public void SaveWaterGoal(double goal)
    {
        if (goal > 0)
        {
            DailyWaterGoal = goal;
            HasWaterGoal = true;
            _preferences.Set(PreferenceKeys.WaterGoal, goal);
            UpdateWaterStatus();
        }
    }

    /// <summary>
    /// Speichert das Kalorien-Ziel von einem User-eingegebenen Wert.
    /// </summary>
    public void SaveCalorieGoal(double goal)
    {
        if (goal > 0)
        {
            DailyCalorieGoal = goal;
            _preferences.Set(PreferenceKeys.CalorieGoal, goal);
            UpdateCalorieStatus();
        }
    }

    #endregion

    #region Load / Refresh

    private async Task LoadCurrentTabDataAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            switch (SelectedTab)
            {
                case ProgressTab.Weight:
                    await LoadWeightDataAsync();
                    break;
                case ProgressTab.Body:
                    await LoadBodyDataAsync();
                    break;
                case ProgressTab.Water:
                    await LoadWaterDataAsync();
                    break;
                case ProgressTab.Calories:
                    await LoadCalorieDataAsync();
                    break;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadWeightDataAsync()
    {
        var entries = await _trackingService.GetEntriesAsync(TrackingType.Weight, ChartDays);
        // Pending-Delete-Eintrag filtern (verhindert Flicker während Undo-Phase)
        var pendingId = _recentlyDeletedEntry?.Id;
        WeightEntries = new ObservableCollection<TrackingEntry>(
            entries.Where(e => e.Id != pendingId).OrderByDescending(e => e.Date));
        WeightStats = await _trackingService.GetStatsAsync(TrackingType.Weight, ChartDays);

        OnPropertyChanged(nameof(WeightCurrentDisplay));
        OnPropertyChanged(nameof(WeightAverageDisplay));
        OnPropertyChanged(nameof(WeightTrendDisplay));
        OnPropertyChanged(nameof(WeightTrendIcon));
        OnPropertyChanged(nameof(WeightTrendColor));
        OnPropertyChanged(nameof(WeightTrendLabel));

        UpdateWeightGoalStatus();
        UpdateWeightChart();
        UpdateWeightMilestones();
    }

    private async Task LoadBodyDataAsync()
    {
        var pendingId = _recentlyDeletedEntry?.Id;

        var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, ChartDays);
        BmiEntries = new ObservableCollection<TrackingEntry>(
            bmiEntries.Where(e => e.Id != pendingId).OrderByDescending(e => e.Date));
        BmiStats = await _trackingService.GetStatsAsync(TrackingType.Bmi, ChartDays);

        var bodyFatEntries = await _trackingService.GetEntriesAsync(TrackingType.BodyFat, ChartDays);
        BodyFatEntries = new ObservableCollection<TrackingEntry>(
            bodyFatEntries.Where(e => e.Id != pendingId).OrderByDescending(e => e.Date));
        BodyFatStats = await _trackingService.GetStatsAsync(TrackingType.BodyFat, ChartDays);

        OnPropertyChanged(nameof(HasBmiEntries));
        OnPropertyChanged(nameof(HasBodyFatEntries));
        OnPropertyChanged(nameof(BmiCurrentDisplay));
        OnPropertyChanged(nameof(BmiAverageDisplay));
        OnPropertyChanged(nameof(BmiTrendDisplay));
        OnPropertyChanged(nameof(BmiMinDisplay));
        OnPropertyChanged(nameof(BmiMaxDisplay));
        OnPropertyChanged(nameof(BodyFatCurrentDisplay));
        OnPropertyChanged(nameof(BodyFatAverageDisplay));
        OnPropertyChanged(nameof(BodyFatTrendDisplay));
        OnPropertyChanged(nameof(BodyFatMinDisplay));
        OnPropertyChanged(nameof(BodyFatMaxDisplay));

        UpdateBodyCharts();
    }

    private async Task LoadWaterDataAsync()
    {
        var todayEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
        TodayWater = todayEntry?.Date.Date == DateTime.Today ? todayEntry.Value : 0;
        UpdateWaterStatus();
    }

    private async Task LoadCalorieDataAsync()
    {
        var meals = await _foodSearchService.GetFoodLogAsync(DateTime.Today);
        // Pending-Delete-Meal filtern (verhindert Flicker während Undo-Phase)
        var pendingMealId = _recentlyDeletedMeal?.Id;
        var filteredMeals = meals.Where(m => m.Id != pendingMealId).ToList();
        TodayMeals = new ObservableCollection<FoodLogEntry>(filteredMeals);
        HasMeals = TodayMeals.Count > 0;

        // Mahlzeiten nach Typ gruppieren
        GroupMealsByType(filteredMeals);

        await UpdateCalorieDataAsync();
        await LoadWeeklyCaloriesAsync();
    }

    private async Task UpdateCalorieDataAsync()
    {
        var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
        ConsumedCalories = summary.TotalCalories;
        ProteinConsumed = summary.TotalProtein;
        CarbsConsumed = summary.TotalCarbs;
        FatConsumed = summary.TotalFat;

        UpdateCalorieStatus();

        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(FatProgress));
        OnPropertyChanged(nameof(HasMacroGoals));
    }

    private void ResetForm()
    {
        NewValue = SelectedTab switch
        {
            ProgressTab.Weight => 70,
            ProgressTab.Body => IsBmiSelected ? 22 : 20,
            ProgressTab.Water => 250,
            _ => 0
        };
        NewDate = DateTime.Today;
        NewNote = null;
    }

    #endregion

    #region Weekly Analysis Commands

    /// <summary>
    /// Analyse anfordern. Premium: direkt zeigen. Sonst: Ad-Overlay.
    /// </summary>
    [RelayCommand]
    private async Task RequestAnalysisAsync()
    {
        if (_purchaseService.IsPremium)
        {
            await GenerateAnalysisReportAsync();
            ShowAnalysisOverlay = true;
        }
        else
        {
            ShowAnalysisAdOverlay = true;
        }
    }

    /// <summary>
    /// User bestätigt: Video schauen für Wochenreport.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAnalysisAdAsync()
    {
        ShowAnalysisAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("detail_analysis");
        if (success)
        {
            await GenerateAnalysisReportAsync();
            ShowAnalysisOverlay = true;
        }
    }

    /// <summary>
    /// Analyse-Overlay schließen.
    /// </summary>
    [RelayCommand]
    private void CloseAnalysis()
    {
        ShowAnalysisOverlay = false;
        ShowAnalysisAdOverlay = false;
    }

    /// <summary>
    /// Berechnet Durchschnittswerte der letzten 7 Tage.
    /// </summary>
    private async Task GenerateAnalysisReportAsync()
    {
        var startDate = DateTime.Today.AddDays(-6);
        var endDate = DateTime.Today;

        // Gewicht-Daten (letzte 7 Tage)
        var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, endDate);
        if (weightEntries.Count > 0)
        {
            var avgWeight = weightEntries.Average(e => e.Value);
            AvgWeightDisplay = $"{avgWeight:F1} kg";

            // Trend: Differenz erstes und letztes Gewicht
            var sorted = weightEntries.OrderBy(e => e.Date).ToList();
            if (sorted.Count >= 2)
            {
                var diff = sorted[^1].Value - sorted[0].Value;
                TrendDisplay = diff >= 0 ? $"+{diff:F1} kg" : $"{diff:F1} kg";
            }
            else
            {
                TrendDisplay = "-";
            }
        }
        else
        {
            AvgWeightDisplay = "-";
            TrendDisplay = "-";
        }

        // Kalorien-Daten (letzte 7 Tage) - parallel laden
        var summaryTasks = Enumerable.Range(0, 7)
            .Select(i => _foodSearchService.GetDailySummaryAsync(startDate.AddDays(i)))
            .ToArray();
        var summaries = await Task.WhenAll(summaryTasks);
        double totalCals = 0;
        int daysWithCals = 0;
        foreach (var summary in summaries)
        {
            if (summary.TotalCalories > 0)
            {
                totalCals += summary.TotalCalories;
                daysWithCals++;
            }
        }
        AvgCaloriesDisplay = daysWithCals > 0 ? $"{totalCals / daysWithCals:F0} kcal" : "-";

        // Kalorienziel-Erreichung
        var calorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 0.0);
        if (calorieGoal > 0 && daysWithCals > 0)
        {
            var avgCals = totalCals / daysWithCals;
            var percentage = avgCals / calorieGoal * 100;
            CalorieTargetDisplay = $"{percentage:F0}%";
        }
        else
        {
            CalorieTargetDisplay = "-";
        }

        // Wasser-Daten (letzte 7 Tage)
        var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, endDate);
        if (waterEntries.Count > 0)
        {
            var avgWater = waterEntries.Average(e => e.Value);
            AvgWaterDisplay = $"{avgWater:F0} ml";
        }
        else
        {
            AvgWaterDisplay = "-";
        }
    }

    #endregion

    #region Export Commands

    /// <summary>
    /// Tracking-Daten exportieren. Premium: direkt. Sonst: Ad-Overlay.
    /// </summary>
    [RelayCommand]
    private async Task ExportTrackingAsync()
    {
        if (_purchaseService.IsPremium)
        {
            await PerformExportAsync();
        }
        else
        {
            ShowExportAdOverlay = true;
        }
    }

    /// <summary>
    /// User bestätigt: Video schauen für Export.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmExportAdAsync()
    {
        ShowExportAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("tracking_export");
        if (success)
        {
            await PerformExportAsync();
        }
    }

    /// <summary>
    /// Export-Ad-Overlay schließen.
    /// </summary>
    [RelayCommand]
    private void CancelExport()
    {
        ShowExportAdOverlay = false;
    }

    /// <summary>
    /// Erstellt CSV-Export und teilt die Datei.
    /// </summary>
    private async Task PerformExportAsync()
    {
        try
        {
            var exportDir = _fileShareService.GetExportDirectory("FitnessRechner");
            var fileName = $"tracking_export_{DateTime.Today:yyyy-MM-dd}.csv";
            var filePath = Path.Combine(exportDir, fileName);

            var sb = new StringBuilder();
            // CSV Header
            sb.AppendLine("Date,Weight (kg),BMI,Water (ml),Calories");

            // Letzte 90 Tage exportieren
            var startDate = DateTime.Today.AddDays(-89);
            var endDate = DateTime.Today;

            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, endDate);
            var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, startDate, endDate);
            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, endDate);

            // Alle Daten nach Datum zusammenführen (letzter Eintrag pro Tag gewinnt)
            var weightByDate = weightEntries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Last().Value);
            var bmiByDate = bmiEntries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Last().Value);
            var waterByDate = waterEntries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Last().Value);

            // Kalorien parallel laden (max 5 gleichzeitig um Thread Pool Starvation zu vermeiden)
            var dates = Enumerable.Range(0, 90).Select(i => startDate.AddDays(i)).ToArray();
            var throttle = new SemaphoreSlim(5);
            var summaryTasks = dates.Select(async d =>
            {
                await throttle.WaitAsync();
                try { return await _foodSearchService.GetDailySummaryAsync(d); }
                finally { throttle.Release(); }
            }).ToArray();
            var summaries = await Task.WhenAll(summaryTasks);

            for (int i = 0; i < 90; i++)
            {
                var date = dates[i];
                var weight = weightByDate.TryGetValue(date, out var w) ? w.ToString("F1", CultureInfo.InvariantCulture) : "";
                var bmi = bmiByDate.TryGetValue(date, out var b) ? b.ToString("F1", CultureInfo.InvariantCulture) : "";
                var water = waterByDate.TryGetValue(date, out var wa) ? wa.ToString("F0", CultureInfo.InvariantCulture) : "";
                var cals = summaries[i].TotalCalories > 0 ? summaries[i].TotalCalories.ToString("F0", CultureInfo.InvariantCulture) : "";

                // Nur Zeilen mit mindestens einem Wert
                if (!string.IsNullOrEmpty(weight) || !string.IsNullOrEmpty(bmi) ||
                    !string.IsNullOrEmpty(water) || !string.IsNullOrEmpty(cals))
                {
                    sb.AppendLine($"{date:yyyy-MM-dd},{weight},{bmi},{water},{cals}");
                }
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
            await _fileShareService.ShareFileAsync(filePath, AppStrings.ExportTracking, "text/csv");
            MessageRequested?.Invoke(AppStrings.AlertSuccess, AppStrings.ExportTracking);
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    #endregion
}
