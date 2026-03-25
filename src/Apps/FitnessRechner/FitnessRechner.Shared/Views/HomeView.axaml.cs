using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels;
using SkiaSharp;

namespace FitnessRechner.Views;

public partial class HomeView : UserControl
{
    // VitalSignsHero Renderer (animierter kreisförmiger Monitor)
    private readonly VitalSignsHeroRenderer _heroRenderer = new();
    private SKRect _lastHeroBounds;

    // Render-Zeit für Quick-Action Button Puls-Animation
    private float _renderTime;

    // Pressed-State für Quick-Action Buttons
    private bool _weightPressed, _waterPressed, _caloriesPressed;

    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        try
        {
            if (DataContext is MainViewModel vm)
                await vm.OnAppearingAsync();
        }
        catch (Exception)
        {
            // async void darf keine Exception werfen → App-Crash verhindern
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _heroRenderer.Dispose();
    }

    // =====================================================================
    // VitalSignsHero Paint + Touch
    // =====================================================================

    /// <summary>
    /// Zeichnet den VitalSigns Hero Monitor (EKG-Ring, 4 Quadranten, Center-Score).
    /// </summary>
    private void OnVitalSignsPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _lastHeroBounds = bounds;

        if (DataContext is not MainViewModel vm) return;

        var state = new VitalSignsState
        {
            Weight = vm.RawWeight,
            Bmi = vm.RawBmi,
            WaterMl = vm.RawWaterMl,
            WaterGoalMl = vm.RawWaterGoalMl,
            Calories = vm.RawCalories,
            CalorieGoal = vm.RawCalorieGoal,
            DailyScore = (int)(vm.DailyScoreFraction * 100),
            WeightTrend = vm.WeightTrend,
            BmiCategory = vm.BmiCategoryText ?? "",
            Time = 0f, // Wird vom Render-Loop via Update() gesteuert
            HasData = vm.HasDashboardData
        };

        _heroRenderer.Render(canvas, bounds, state);
    }

    /// <summary>
    /// Touch auf den VitalSigns Monitor: Quadrant bestimmen und zum passenden Rechner navigieren.
    /// </summary>
    private void OnVitalSignsPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not SKCanvasView canvas) return;

        var pos = e.GetPosition(canvas);

        // Avalonia-Koordinaten in SkiaSharp-Koordinaten umrechnen (DPI-Skalierung)
        float scaleX = _lastHeroBounds.Width / (float)canvas.Bounds.Width;
        float scaleY = _lastHeroBounds.Height / (float)canvas.Bounds.Height;
        float skiaX = (float)pos.X * scaleX;
        float skiaY = (float)pos.Y * scaleY;

        var quadrant = _heroRenderer.HitTest(_lastHeroBounds, skiaX, skiaY);

        switch (quadrant)
        {
            case VitalQuadrant.Weight:
                vm.OpenIdealWeightCommand.Execute(null);
                break;
            case VitalQuadrant.Bmi:
                vm.OpenBmiCommand.Execute(null);
                break;
            case VitalQuadrant.Water:
                vm.OpenWaterCommand.Execute(null);
                break;
            case VitalQuadrant.Calories:
                vm.OpenCaloriesCommand.Execute(null);
                break;
            case VitalQuadrant.Center:
                // Center-Tap: Achievements öffnen
                vm.OpenAchievementsCommand.Execute(null);
                break;
        }

        e.Handled = true;
    }

    // =====================================================================
    // Render-Loop Anbindung
    // =====================================================================

    /// <summary>
    /// Wird von MainView's Render-Loop aufgerufen um animierte Canvases zu aktualisieren.
    /// </summary>
    public void OnRenderTick(float renderTime)
    {
        _renderTime += 0.05f;
        _heroRenderer.Update(0.05f);
        VitalSignsCanvas?.InvalidateSurface();

        // Quick-Action Buttons für Puls-Animation
        BtnQuickWeight?.InvalidateSurface();
        BtnQuickWater?.InvalidateSurface();
        BtnQuickCalories?.InvalidateSurface();

        // Dashboard Cards (animierte Scan-Lines, Puls, EKG)
        LevelCanvas?.InvalidateSurface();
        ChallengeCanvas?.InvalidateSurface();
        StreakCanvas?.InvalidateSurface();
    }

    // =====================================================================
    // Quick-Action Buttons Paint + Touch
    // =====================================================================

    private void OnQuickWeightPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        QuickActionButtonRenderer.Render(canvas, bounds, "+kg", "weight",
            MedicalColors.WeightPurple, _renderTime, _weightPressed);
    }

    private void OnQuickWaterPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        QuickActionButtonRenderer.Render(canvas, bounds, "+250 ml", "water",
            MedicalColors.WaterGreen, _renderTime, _waterPressed);
    }

    private void OnQuickCaloriesPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        QuickActionButtonRenderer.Render(canvas, bounds, "+kcal", "fire",
            MedicalColors.CalorieAmber, _renderTime, _caloriesPressed);
    }

    private void OnQuickWeightPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _weightPressed = true;
        BtnQuickWeight?.InvalidateSurface();
        if (DataContext is MainViewModel vm)
            vm.OpenWeightQuickAddCommand.Execute(null);
        e.Handled = true;
    }

    private void OnQuickWaterPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _waterPressed = true;
        BtnQuickWater?.InvalidateSurface();
        if (DataContext is MainViewModel vm)
            vm.OpenWaterQuickAddCommand.Execute(null);
        e.Handled = true;
    }

    private void OnQuickCaloriesPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _caloriesPressed = true;
        BtnQuickCalories?.InvalidateSurface();
        if (DataContext is MainViewModel vm)
            vm.OpenFoodQuickAddCommand.Execute(null);
        e.Handled = true;
    }

    private void OnQuickButtonReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _weightPressed = false;
        _waterPressed = false;
        _caloriesPressed = false;
        BtnQuickWeight?.InvalidateSurface();
        BtnQuickWater?.InvalidateSurface();
        BtnQuickCalories?.InvalidateSurface();
    }

    // =====================================================================
    // Dashboard Card Paint-Handler
    // =====================================================================

    /// <summary>
    /// Zeichnet die XP/Level-Bar im Medical-Design.
    /// </summary>
    private void OnPaintLevel(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        if (DataContext is not MainViewModel vm) return;

        // Level aus LevelLabel parsen (Format: "Lv. X")
        int level = 0;
        if (vm.LevelLabel is { } label)
        {
            var numStr = label.Replace("Lv.", "").Replace("Lv", "").Trim();
            int.TryParse(numStr, out level);
        }

        LevelProgressRenderer.Render(canvas, bounds,
            level, (float)vm.LevelProgress, vm.XpDisplay ?? "", _renderTime);
    }

    /// <summary>
    /// Zeichnet die Challenge-Card im Medical-Design.
    /// </summary>
    private void OnPaintChallenge(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        if (DataContext is not MainViewModel vm) return;

        ChallengeCardRenderer.Render(canvas, bounds,
            vm.ChallengeTitleText ?? "",
            (float)vm.ChallengeProgressValue,
            ParseXpReward(vm.ChallengeXpText),
            vm.IsChallengeCompleted,
            _renderTime);
    }

    /// <summary>
    /// Zeichnet die Streak-Card im Medical-Design.
    /// </summary>
    private void OnPaintStreak(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        if (DataContext is not MainViewModel vm) return;

        int currentStreak = ParseIntFromDisplay(vm.StreakDisplay);
        int bestStreak = ParseIntFromDisplay(vm.StreakBestDisplay);

        StreakCardRenderer.Render(canvas, bounds,
            currentStreak, bestStreak, vm.HasStreak, _renderTime);
    }

    /// <summary>
    /// Parst die XP-Belohnung aus dem ChallengeXpText (Format: "+25 XP").
    /// </summary>
    private static int ParseXpReward(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var numStr = new string(text.Where(c => char.IsDigit(c)).ToArray());
        return int.TryParse(numStr, out int val) ? val : 0;
    }

    /// <summary>
    /// Parst eine Ganzzahl aus einem Display-Text (z.B. "7 Tage" → 7).
    /// </summary>
    private static int ParseIntFromDisplay(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var numStr = new string(text.Where(c => char.IsDigit(c)).ToArray());
        return int.TryParse(numStr, out int val) ? val : 0;
    }
}
