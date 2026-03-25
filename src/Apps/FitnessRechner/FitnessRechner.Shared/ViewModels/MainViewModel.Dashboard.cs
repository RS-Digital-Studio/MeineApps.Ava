using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Services;

namespace FitnessRechner.ViewModels;

/// <summary>
/// Dashboard-Daten, Localized Labels, Gamification, Quick-Add, Heatmap, Weekly Comparison, Evening Summary.
/// Ausgelagert aus MainViewModel.cs für bessere Übersichtlichkeit.
/// </summary>
public sealed partial class MainViewModel
{
    #region Dashboard Properties

    [ObservableProperty] private string _todayWeightDisplay = "-";
    [ObservableProperty] private string _todayBmiDisplay = "-";
    [ObservableProperty] private string _todayWaterDisplay = "-";
    [ObservableProperty] private string _todayCaloriesDisplay = "-";
    [ObservableProperty] private bool _hasDashboardData;

    // Rohwerte für VitalSignsHeroRenderer
    public float RawWeight { get; private set; }
    public float RawBmi { get; private set; }
    public float RawWaterMl { get; private set; }
    public float RawWaterGoalMl { get; private set; }
    public float RawCalories { get; private set; }
    public float RawCalorieGoal { get; private set; }
    public int WeightTrend { get; private set; }
    public string BmiCategoryText { get; private set; } = "";

    [ObservableProperty] private double _calorieProgress;
    [ObservableProperty] private double _waterProgress;

    public bool HasWaterProgress => WaterProgress > 0;
    public bool HasCalorieProgress => CalorieProgress > 0;
    public double WaterProgressFraction => Math.Min(WaterProgress / 100.0, 1.0);
    public double CalorieProgressFraction => Math.Min(CalorieProgress / 100.0, 1.0);

    [ObservableProperty] private bool _hasLoggedWeightToday;

    public double DailyScoreFraction
    {
        get
        {
            var waterPart = Math.Min(WaterProgress, 100);
            var caloriePart = Math.Min(CalorieProgress, 100);
            var weightPart = HasLoggedWeightToday ? 100.0 : 0.0;
            return (waterPart + caloriePart + weightPart) / 300.0;
        }
    }

    public string DailyScoreDisplay => $"{DailyScoreFraction * 100:F0}%";

    partial void OnWaterProgressChanged(double value)
    {
        OnPropertyChanged(nameof(HasWaterProgress));
        OnPropertyChanged(nameof(WaterProgressFraction));
        OnPropertyChanged(nameof(DailyScoreFraction));
        OnPropertyChanged(nameof(DailyScoreDisplay));
    }

    partial void OnCalorieProgressChanged(double value)
    {
        OnPropertyChanged(nameof(HasCalorieProgress));
        OnPropertyChanged(nameof(CalorieProgressFraction));
        OnPropertyChanged(nameof(DailyScoreFraction));
        OnPropertyChanged(nameof(DailyScoreDisplay));
    }

    partial void OnHasLoggedWeightTodayChanged(bool value)
    {
        OnPropertyChanged(nameof(DailyScoreFraction));
        OnPropertyChanged(nameof(DailyScoreDisplay));
    }

    // Quick-Add
    [ObservableProperty] private bool _showWeightQuickAdd;
    [ObservableProperty] private bool _showWaterQuickAdd;
    [ObservableProperty] private double _quickAddWeight = 70.0;
    private bool _wasWaterGoalReachedOnDashboard;

    // Streak
    [ObservableProperty] private int _currentStreak;
    [ObservableProperty] private string _streakDisplay = "";
    [ObservableProperty] private string _streakBestDisplay = "";
    [ObservableProperty] private bool _hasStreak;

    // Heatmap
    [ObservableProperty] private Dictionary<DateTime, int> _heatmapData = new();
    [ObservableProperty] private bool _hasHeatmapData;
    [ObservableProperty] private bool _showHeatmapHint;
    public string HeatmapHintText => _localization.GetString("HeatmapHint") ?? "Keep tracking to fill your activity calendar!";

    // Empty-State
    [ObservableProperty] private bool _showEmptyState = true;

    // Abend-Zusammenfassung
    [ObservableProperty] private bool _showEveningSummary;
    [ObservableProperty] private string _eveningSummaryText = "";
    [ObservableProperty] private string _eveningSummaryRating = "";
    [ObservableProperty] private string _eveningSummaryRatingColor = "#EAB308";

    // Fasten-Status für Dashboard
    public bool IsFastingActive => FastingViewModel.IsActive;
    public string FastingTimeDisplay => FastingViewModel.TimeDisplay;
    public double FastingProgress => FastingViewModel.ProgressFraction;

    #endregion

    #region Gamification Properties

    [ObservableProperty] private int _currentGamificationLevel;
    [ObservableProperty] private double _levelProgress;
    [ObservableProperty] private string _xpDisplay = "";
    [ObservableProperty] private bool _isChallengeCompleted;
    [ObservableProperty] private bool _showAchievements;

    // Weekly Comparison
    [ObservableProperty] private string _weeklyCaloriesChange = "";
    [ObservableProperty] private string _weeklyWaterChange = "";
    [ObservableProperty] private string _weeklyWeightChange = "";
    [ObservableProperty] private string _weeklyLogDays = "";
    [ObservableProperty] private string _weeklyCaloriesColor = "#EAB308";
    [ObservableProperty] private string _weeklyWaterColor = "#EAB308";
    [ObservableProperty] private string _weeklyWeightColor = "#EAB308";
    [ObservableProperty] private bool _hasWeeklyComparison;

    public DailyChallenge TodayChallenge => _challengeService.TodayChallenge;
    public string ChallengeTitleText => _localization.GetString(TodayChallenge.TitleKey) ?? TodayChallenge.TitleKey;
    public string ChallengeDescText => _localization.GetString(TodayChallenge.DescriptionKey) ?? TodayChallenge.DescriptionKey;
    public double ChallengeProgressValue => TodayChallenge.Progress;
    public string ChallengeProgressText => $"{TodayChallenge.CurrentValue}/{TodayChallenge.TargetValue}";
    public string ChallengeXpText => $"+{TodayChallenge.XpReward} XP";
    public IReadOnlyList<FitnessAchievement> RecentAchievements => _achievementService.RecentUnlocked;
    public IReadOnlyList<FitnessAchievement> AllAchievements => _achievementService.Achievements;
    public string AchievementCountDisplay => $"{_achievementService.UnlockedCount}/{_achievementService.Achievements.Count}";
    public bool HasRecentAchievements => _achievementService.RecentUnlocked.Count > 0;
    public string LevelLabel => string.Format(_localization.GetString("Level") ?? "Level {0}", CurrentGamificationLevel);

    #endregion

    #region Localized Labels

    public string AppDescription => _localization.GetString("AppDescription");
    public string CalcBmiLabel => _localization.GetString("CalcBmi");
    public string CalcCaloriesLabel => _localization.GetString("CalcCalories");
    public string CalcWaterLabel => _localization.GetString("CalcWater");
    public string CalcIdealWeightLabel => _localization.GetString("CalcIdealWeight");
    public string CalcBodyFatLabel => _localization.GetString("CalcBodyFat");
    public string CalculatorsLabel => _localization.GetString("Calculators");
    public string MyProgressLabel => _localization.GetString("MyProgress");
    public string RemoveAdsText => _localization.GetString("RemoveAds") ?? "Go Ad-Free";
    public string PremiumPriceText => _localization.GetString("PremiumPrice") ?? "From 3.99 €";
    public string SectionCalculatorsText => _localization.GetString("SectionCalculators") ?? "Calculators";
    public string StreakTitleText => _localization.GetString("StreakTitle") ?? "Logging Streak";
    public string QuickAddWeightLabel => _localization.GetString("QuickAddWeight") ?? "Enter weight";

    public string MotivationalQuote
    {
        get
        {
            var index = DateTime.Today.DayOfYear % 10;
            return _localization.GetString($"MotivQuote{index + 1}") ?? "Every step counts!";
        }
    }

    public string DailyProgressLabel => _localization.GetString("DailyProgress") ?? "Daily Progress";
    public string DailyChallengeLabel => _localization.GetString("DailyChallenge") ?? "Daily Challenge";
    public string ChallengeCompletedLabel => _localization.GetString("ChallengeCompleted") ?? "Completed!";
    public string AchievementsTitleLabel => _localization.GetString("AchievementsTitle") ?? "Achievements";
    public string BadgesLabel => _localization.GetString("Badges") ?? "Badges";
    public string ShowAllLabel => _localization.GetString("ShowAll") ?? "Show All";
    public string WeeklyComparisonLabel => _localization.GetString("WeeklyComparison") ?? "Weekly Comparison";
    public string ThisWeekLabel => _localization.GetString("ThisWeek") ?? "This Week";
    public string LastWeekLabel => _localization.GetString("LastWeek") ?? "Last Week";
    public string EveningSummaryLabel => _localization.GetString("EveningSummary") ?? "Today's Summary";
    public string EmptyStateTitle => _localization.GetString("EmptyStateTitle") ?? "Start your fitness journey!";
    public string EmptyStateSubtitle => _localization.GetString("EmptyStateSubtitle") ?? "Track your weight, water and calories to unlock all dashboard features.";
    public string EmptyStateHint => _localization.GetString("EmptyStateHint") ?? "Use the buttons above to get started";

    public string GreetingText
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var key = hour switch { < 12 => "GreetingMorning", < 18 => "GreetingAfternoon", _ => "GreetingEvening" };
            return _localization.GetString(key) ?? "Hello!";
        }
    }

    #endregion

    #region Dashboard Lifecycle

    public async Task OnAppearingAsync()
    {
        IsPremium = _purchaseService.IsPremium;
        UpdateStreakDisplay();

        try
        {
            await LoadDashboardDataAsync();
            await LoadHeatmapDataAsync();
            await CheckGamificationProgressAsync();
            await LoadWeeklyComparisonAsync();
            await CheckEveningSummaryAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                $"Dashboard: {ex.Message}");
        }
        finally
        {
            ShowEmptyState = !HasDashboardData && !HasHeatmapData && !HasWeeklyComparison && !HasRecentAchievements;
        }
    }

    public void RecordStreakActivity()
    {
        var wasLoggedToday = _streakService.IsLoggedToday;
        var isMilestone = _streakService.RecordActivity();
        UpdateStreakDisplay();

        if (isMilestone)
        {
            var milestone = _streakService.CurrentStreak;
            var text = string.Format(_localization.GetString("StreakMilestone") ?? "{0} day streak!", milestone);
            FloatingTextRequested?.Invoke(text, "streak");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
        }
        else if (!wasLoggedToday && _streakService.IsLoggedToday)
        {
            var streak = _streakService.CurrentStreak;
            var text = string.Format(_localization.GetString("StreakIncreased") ?? "+1! {0} day streak", streak);
            FloatingTextRequested?.Invoke(text, "streak");
        }

        _levelService.AddXp(5);
        var meals = _preferences.Get(PreferenceKeys.TotalMealsLogged, 0);
        _preferences.Set(PreferenceKeys.TotalMealsLogged, meals + 1);
        _ = CheckGamificationProgressAsync();
    }

    private void UpdateStreakDisplay()
    {
        CurrentStreak = _streakService.CurrentStreak;
        HasStreak = CurrentStreak > 0;

        StreakDisplay = CurrentStreak switch
        {
            0 => _localization.GetString("StreakStart") ?? "Start your streak!",
            1 => _localization.GetString("StreakDay") ?? "1 day",
            _ => string.Format(_localization.GetString("StreakDays") ?? "{0} days", CurrentStreak)
        };

        StreakBestDisplay = string.Format(_localization.GetString("StreakBest") ?? "Best: {0}", _streakService.BestStreak);
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            HasDashboardData = false;
            RawWeight = 0f; RawBmi = 0f; RawWaterMl = 0f; RawCalories = 0f;
            WeightTrend = 0; BmiCategoryText = "";

            // Weight
            var weightEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            HasLoggedWeightToday = weightEntry != null && weightEntry.Date.Date == DateTime.Today;
            if (weightEntry != null && weightEntry.Date.Date >= DateTime.Today.AddDays(-7))
            {
                TodayWeightDisplay = $"{weightEntry.Value:F1}";
                RawWeight = (float)weightEntry.Value;
                HasDashboardData = true;

                var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight,
                    DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
                if (weightEntries.Count >= 2)
                {
                    var sorted = weightEntries.OrderByDescending(e => e.Date).ToList();
                    var diff = sorted[0].Value - sorted[1].Value;
                    WeightTrend = diff > 0.2 ? 1 : diff < -0.2 ? -1 : 0;
                }
            }
            else { TodayWeightDisplay = "-"; }

            // BMI
            var bmiEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Bmi);
            if (bmiEntry != null && bmiEntry.Date.Date >= DateTime.Today.AddDays(-7))
            {
                TodayBmiDisplay = $"{bmiEntry.Value:F1}";
                RawBmi = (float)bmiEntry.Value;
                HasDashboardData = true;
                BmiCategoryText = bmiEntry.Value switch
                {
                    < 16 => _localization.GetString("BmiSevereUnderweight") ?? "Untergewicht",
                    < 17 => _localization.GetString("BmiModerateUnderweight") ?? "Untergewicht",
                    < 18.5 => _localization.GetString("BmiMildUnderweight") ?? "Untergewicht",
                    < 25 => _localization.GetString("BmiNormal") ?? "Normal",
                    < 30 => _localization.GetString("BmiOverweight") ?? "Übergewicht",
                    < 35 => _localization.GetString("BmiObeseClass1") ?? "Adipositas I",
                    < 40 => _localization.GetString("BmiObeseClass2") ?? "Adipositas II",
                    _ => _localization.GetString("BmiObeseClass3") ?? "Adipositas III"
                };
            }
            else { TodayBmiDisplay = "-"; }

            // Water
            var waterEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
            var waterGoal = _preferences.Get(PreferenceKeys.WaterGoal, 2500.0);
            RawWaterGoalMl = (float)waterGoal;
            if (waterEntry != null && waterEntry.Date.Date == DateTime.Today)
            {
                var progress = waterGoal > 0 ? (waterEntry.Value / waterGoal) * 100 : 0;
                TodayWaterDisplay = $"{progress:F0}%";
                WaterProgress = Math.Min(progress, 100);
                RawWaterMl = (float)waterEntry.Value;
                HasDashboardData = true;
            }
            else { TodayWaterDisplay = "0%"; WaterProgress = 0; }

            // Calories
            var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var calorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 2000.0);
            RawCalorieGoal = (float)calorieGoal;
            if (summary.TotalCalories > 0)
            {
                TodayCaloriesDisplay = $"{summary.TotalCalories:F0}";
                CalorieProgress = calorieGoal > 0 ? Math.Min((summary.TotalCalories / calorieGoal) * 100, 100) : 0;
                RawCalories = (float)summary.TotalCalories;
                HasDashboardData = true;
            }
            else { TodayCaloriesDisplay = "-"; CalorieProgress = 0; }
        }
        catch (Exception) { /* Dashboard zeigt Default-Werte */ }
    }

    #endregion

    #region Dashboard Quick-Add

    [RelayCommand]
    private void OpenWeightQuickAdd()
    {
        _ = LoadLastWeightAsync();
        ShowWeightQuickAdd = true;
    }

    private async Task LoadLastWeightAsync()
    {
        try
        {
            var lastWeight = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            if (lastWeight != null) QuickAddWeight = lastWeight.Value;
        }
        catch { /* Standardwert beibehalten */ }
    }

    [RelayCommand]
    private async Task SaveWeightQuickAdd()
    {
        try
        {
            if (QuickAddWeight < 20 || QuickAddWeight > 500) return;
            var entry = new TrackingEntry { Type = TrackingType.Weight, Value = QuickAddWeight, Date = DateTime.Today };
            await _trackingService.AddEntryAsync(entry);

            ShowWeightQuickAdd = false;
            FloatingTextRequested?.Invoke($"+{QuickAddWeight:F1} kg", "info");
            _hapticService.Click();
            _levelService.AddXp(10);
            await LoadDashboardDataAsync();
            _ = CheckGamificationProgressAsync();
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Error saving data");
        }
    }

    [RelayCommand]
    private void CancelWeightQuickAdd() => ShowWeightQuickAdd = false;

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
                var entry = new TrackingEntry { Type = TrackingType.Water, Value = amount, Date = DateTime.Today };
                await _trackingService.AddEntryAsync(entry);
            }

            ShowWaterQuickAdd = false;
            FloatingTextRequested?.Invoke($"+{amount} ml", "info");
            _hapticService.Tick();
            _levelService.AddXp(3);
            await LoadDashboardDataAsync();
            _ = CheckGamificationProgressAsync();

            if (!_wasWaterGoalReachedOnDashboard && WaterProgress >= 100)
            {
                _wasWaterGoalReachedOnDashboard = true;
                FloatingTextRequested?.Invoke(_localization.GetString("GoalReached") ?? "Goal reached!", "success");
                CelebrationRequested?.Invoke();
                _hapticService.HeavyClick();
                _soundService.PlaySuccess();
            }
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Error saving data");
        }
    }

    [RelayCommand]
    private void OpenWaterQuickAdd() => ShowWaterQuickAdd = true;

    [RelayCommand]
    private void CloseWaterQuickAdd() => ShowWaterQuickAdd = false;

    [RelayCommand]
    private void OpenFoodQuickAdd()
    {
        CurrentPage = null;
        SelectedTab = 2;
        FoodSearchViewModel.ShowQuickAddPanel = true;
    }

    #endregion

    #region Heatmap

    private async Task LoadHeatmapDataAsync()
    {
        try
        {
            var startDate = DateTime.Today.AddMonths(-3);
            var endDate = DateTime.Today;

            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, endDate);
            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, endDate);
            var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, startDate, endDate);
            var bodyFatEntries = await _trackingService.GetEntriesAsync(TrackingType.BodyFat, startDate, endDate);
            var foodLogs = await _foodSearchService.GetFoodLogsInRangeAsync(startDate, endDate);
            var foodLogDates = foodLogs.Keys.ToHashSet();

            var data = new Dictionary<DateTime, int>();
            var totalDays = (endDate - startDate).Days + 1;

            for (var i = 0; i < totalDays; i++)
            {
                var date = startDate.AddDays(i).Date;
                var score = (weightEntries.Any(e => e.Date.Date == date) ? 1 : 0)
                          + (waterEntries.Any(e => e.Date.Date == date) ? 1 : 0)
                          + (foodLogDates.Contains(date) ? 1 : 0)
                          + (bmiEntries.Any(e => e.Date.Date == date) || bodyFatEntries.Any(e => e.Date.Date == date) ? 1 : 0);

                if (score > 0) data[date] = Math.Min(score, 4);
            }

            HeatmapData = data;
            HasHeatmapData = data.Count > 0;
            ShowHeatmapHint = data.Count > 0 && data.Count < 7;
        }
        catch { /* Heatmap ist optional */ }
    }

    #endregion

    #region Gamification Methods

    private void OnAchievementUnlocked(string titleKey, int xpReward)
    {
        _levelService.AddXp(xpReward);
        var title = _localization.GetString(titleKey) ?? titleKey;
        var text = $"{_localization.GetString("AchievementUnlockedText") ?? "Achievement!"} {title}";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FloatingTextRequested?.Invoke(text, "achievement");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            UpdateGamificationDisplay();
        });
    }

    private void OnLevelUp(int newLevel)
    {
        var text = $"{_localization.GetString("LevelUpText") ?? "Level Up!"} → Level {newLevel}";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FloatingTextRequested?.Invoke(text, "levelup");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            UpdateGamificationDisplay();
        });
    }

    private void OnChallengeCompleted(int xpReward)
    {
        _levelService.AddXp(xpReward);
        var text = $"{ChallengeCompletedLabel} +{xpReward} XP";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FloatingTextRequested?.Invoke(text, "challenge");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            IsChallengeCompleted = true;
            UpdateGamificationDisplay();
        });
    }

    private void UpdateGamificationDisplay()
    {
        CurrentGamificationLevel = _levelService.CurrentLevel;
        LevelProgress = _levelService.LevelProgress;
        XpDisplay = _levelService.XpDisplay;
        IsChallengeCompleted = TodayChallenge.IsCompleted;

        OnPropertyChanged(nameof(LevelLabel));
        OnPropertyChanged(nameof(ChallengeTitleText));
        OnPropertyChanged(nameof(ChallengeDescText));
        OnPropertyChanged(nameof(ChallengeProgressValue));
        OnPropertyChanged(nameof(ChallengeProgressText));
        OnPropertyChanged(nameof(ChallengeXpText));
        OnPropertyChanged(nameof(RecentAchievements));
        OnPropertyChanged(nameof(AllAchievements));
        OnPropertyChanged(nameof(AchievementCountDisplay));
        OnPropertyChanged(nameof(HasRecentAchievements));
    }

    private async Task CheckGamificationProgressAsync()
    {
        try
        {
            var startDate = DateTime.Today.AddYears(-1);
            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, DateTime.Today);
            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, DateTime.Today);

            var recipes = await _foodSearchService.GetRecipesAsync();
            var weightGoal = _preferences.Get(PreferenceKeys.WeightGoal, 0.0);
            var latestWeight = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            var hasReachedGoal = weightGoal > 0 && latestWeight != null && Math.Abs(latestWeight.Value - weightGoal) < 0.5;

            var calcMask = _preferences.Get(PreferenceKeys.CalculatorsUsedMask, 0);
            var calcsUsed = 0;
            for (int i = 0; i < 5; i++) if ((calcMask & (1 << i)) != 0) calcsUsed++;

            var calorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 2000.0);
            var waterGoal = _preferences.Get(PreferenceKeys.WaterGoal, 2500.0);
            var calorieDaysInRow = 0;
            var waterDaysInRow = 0;

            for (int i = 1; i <= 14; i++)
            {
                if (calorieGoal <= 0) break;
                var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today.AddDays(-i));
                if (summary.TotalCalories > 0 && summary.TotalCalories <= calorieGoal) calorieDaysInRow++;
                else break;
            }

            for (int i = 1; i <= 14; i++)
            {
                var dayWater = waterEntries.Where(e => e.Date.Date == DateTime.Today.AddDays(-i)).Sum(e => e.Value);
                if (waterGoal > 0 && dayWater >= waterGoal) waterDaysInRow++;
                else break;
            }

            _achievementService.CheckProgress(new AchievementCheckContext
            {
                CurrentStreak = _streakService.CurrentStreak,
                TotalWeightEntries = weightEntries.Count,
                TotalWaterMl = waterEntries.Sum(e => e.Value),
                TotalMealsLogged = _preferences.Get(PreferenceKeys.TotalMealsLogged, 0),
                TotalBarcodesScanned = _preferences.Get(PreferenceKeys.TotalBarcodesScanned, 0),
                TotalRecipesCreated = recipes.Count,
                DistinctFoodsLogged = _preferences.Get(PreferenceKeys.DistinctFoodsTracked, 0),
                CalculatorsUsed = calcsUsed,
                HasReachedWeightGoal = hasReachedGoal,
                IsPremium = _purchaseService.IsPremium,
                CurrentHour = DateTime.Now.Hour,
                CalorieGoalDaysInRow = calorieDaysInRow,
                WaterGoalDaysInRow = waterDaysInRow
            });

            var todaySummary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var todayFoodLog = await _foodSearchService.GetFoodLogAsync(DateTime.Today);
            var todayWater = waterEntries.Where(e => e.Date.Date == DateTime.Today).Sum(e => e.Value);
            var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, DateTime.Today, DateTime.Today.AddDays(1));

            _challengeService.CheckProgress(new ChallengeCheckContext
            {
                TodayWaterMl = todayWater,
                TodayMealsCount = todayFoodLog.Count,
                HasWeightEntry = weightEntries.Any(e => e.Date.Date == DateTime.Today),
                TodayCalories = todaySummary.TotalCalories,
                CalorieGoal = calorieGoal,
                TodayFoodsTracked = todayFoodLog.Count,
                TodayProtein = todaySummary.TotalProtein,
                HasUsedBmi = bmiEntries.Count > 0,
                HasScannedBarcode = _preferences.Get(PreferenceKeys.TotalBarcodesScanned, 0) > 0,
                HasBreakfast = todayFoodLog.Any(f => f.Meal == MealType.Breakfast),
                HasLunch = todayFoodLog.Any(f => f.Meal == MealType.Lunch),
                HasDinner = todayFoodLog.Any(f => f.Meal == MealType.Dinner)
            });

            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateGamificationDisplay);
        }
        catch { /* Gamification ist optional */ }
    }

    private async Task CheckEveningSummaryAsync()
    {
        try
        {
            if (DateTime.Now.Hour < 20) { ShowEveningSummary = false; return; }

            var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var waterEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
            var todayWater = (waterEntry != null && waterEntry.Date.Date == DateTime.Today) ? waterEntry.Value : 0;
            var weightEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            var todayWeight = (weightEntry != null && weightEntry.Date.Date == DateTime.Today) ? weightEntry.Value : 0;

            if (summary.TotalCalories <= 0 && todayWater <= 0 && todayWeight <= 0) { ShowEveningSummary = false; return; }

            var parts = new List<string>();
            if (summary.TotalCalories > 0) parts.Add($"{summary.TotalCalories:F0} kcal");
            if (todayWater > 0) parts.Add($"{todayWater:F0} ml");
            if (todayWeight > 0) parts.Add($"{todayWeight:F1} kg");
            EveningSummaryText = string.Join(" | ", parts);

            var score = DailyScoreFraction * 100;
            (EveningSummaryRating, EveningSummaryRatingColor) = score switch
            {
                >= 90 => (_localization.GetString("RatingGreatDay") ?? "Great day!", "#22C55E"),
                >= 50 => (_localization.GetString("RatingGoodDay") ?? "Good day!", "#3B82F6"),
                _ => (_localization.GetString("RatingTomorrowBetter") ?? "Tomorrow will be better!", "#EAB308")
            };
            ShowEveningSummary = true;
        }
        catch { ShowEveningSummary = false; }
    }

    private async Task LoadWeeklyComparisonAsync()
    {
        try
        {
            var today = DateTime.Today;
            var thisWeekStart = today.AddDays(-6);
            var lastWeekStart = today.AddDays(-13);
            var lastWeekEnd = today.AddDays(-7);

            var summaries = await _foodSearchService.GetDailySummariesInRangeAsync(lastWeekStart, today);
            double thisWeekCal = 0, lastWeekCal = 0;
            int thisWeekDays = 0, lastWeekDays = 0;
            for (int i = 0; i < 7; i++)
            {
                if (summaries.TryGetValue(thisWeekStart.AddDays(i), out var s1) && s1.TotalCalories > 0) { thisWeekCal += s1.TotalCalories; thisWeekDays++; }
                if (summaries.TryGetValue(lastWeekStart.AddDays(i), out var s2) && s2.TotalCalories > 0) { lastWeekCal += s2.TotalCalories; lastWeekDays++; }
            }

            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, lastWeekStart, today.AddDays(1));
            var thisWeekWater = waterEntries.Where(e => e.Date.Date >= thisWeekStart).Sum(e => e.Value);
            var lastWeekWater = waterEntries.Where(e => e.Date.Date >= lastWeekStart && e.Date.Date <= lastWeekEnd).Sum(e => e.Value);
            var thisWeekWaterDays = waterEntries.Where(e => e.Date.Date >= thisWeekStart).Select(e => e.Date.Date).Distinct().Count();
            var lastWeekWaterDays = waterEntries.Where(e => e.Date.Date >= lastWeekStart && e.Date.Date <= lastWeekEnd).Select(e => e.Date.Date).Distinct().Count();

            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, lastWeekStart, today.AddDays(1));
            var thisWeekWeight = weightEntries.Where(e => e.Date.Date >= thisWeekStart).OrderByDescending(e => e.Date).FirstOrDefault();
            var lastWeekWeight = weightEntries.Where(e => e.Date.Date >= lastWeekStart && e.Date.Date <= lastWeekEnd).OrderByDescending(e => e.Date).FirstOrDefault();

            var allLogDates = weightEntries.Select(e => e.Date.Date).Concat(waterEntries.Select(e => e.Date.Date)).Distinct().ToList();
            var thisWeekLogCount = allLogDates.Count(d => d >= thisWeekStart);
            var lastWeekLogCount = allLogDates.Count(d => d >= lastWeekStart && d <= lastWeekEnd);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (thisWeekDays > 0 && lastWeekDays > 0)
                {
                    var avgThis = thisWeekCal / thisWeekDays; var avgLast = lastWeekCal / lastWeekDays;
                    WeeklyCaloriesChange = $"{avgThis:F0} vs {avgLast:F0} kcal";
                    WeeklyCaloriesColor = (avgThis - avgLast) > 50 ? "#EF4444" : (avgThis - avgLast) < -50 ? "#22C55E" : "#EAB308";
                }
                else { WeeklyCaloriesChange = "-"; }

                if (thisWeekWaterDays > 0 && lastWeekWaterDays > 0)
                {
                    var avgThis = thisWeekWater / thisWeekWaterDays / 1000.0; var avgLast = lastWeekWater / lastWeekWaterDays / 1000.0;
                    WeeklyWaterChange = $"{avgThis:F1} vs {avgLast:F1} L";
                    WeeklyWaterColor = (avgThis - avgLast) > 0.2 ? "#22C55E" : (avgThis - avgLast) < -0.2 ? "#EF4444" : "#EAB308";
                }
                else { WeeklyWaterChange = "-"; }

                if (thisWeekWeight != null && lastWeekWeight != null)
                {
                    var diff = thisWeekWeight.Value - lastWeekWeight.Value;
                    WeeklyWeightChange = $"{(diff > 0 ? "↑" : diff < 0 ? "↓" : "→")} {Math.Abs(diff):F1} kg";
                    WeeklyWeightColor = diff < -0.1 ? "#22C55E" : diff > 0.1 ? "#EF4444" : "#EAB308";
                }
                else { WeeklyWeightChange = "-"; }

                WeeklyLogDays = $"{thisWeekLogCount}/7 vs {lastWeekLogCount}/7";
                HasWeeklyComparison = thisWeekDays > 0 || thisWeekWaterDays > 0 || thisWeekWeight != null;
            });
        }
        catch { /* Weekly Comparison ist optional */ }
    }

    #endregion
}
