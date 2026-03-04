using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels;
using MeineApps.UI.SkiaSharp;
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
            vm.QuickAddWaterCommand.Execute("250"); // XAML CommandParameter ist immer string
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
    // SkiaSharp Progress-Bars
    // =====================================================================

    /// <summary>
    /// XP-Level-Fortschritt zeichnen (grün → accent).
    /// </summary>
    private void OnPaintLevelProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.LevelProgress,
            new SKColor(0x22, 0xC5, 0x5E), // Grün Start
            new SKColor(0x16, 0xA3, 0x4A), // Grün End
            showText: false, glowEnabled: true);
    }

    /// <summary>
    /// Challenge-Fortschritt zeichnen (indigo → lila).
    /// </summary>
    private void OnPaintChallengeProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.ChallengeProgressValue,
            new SKColor(0x63, 0x66, 0xF1), // Indigo Start
            new SKColor(0x8B, 0x5C, 0xF6), // Lila End
            showText: false, glowEnabled: false);
    }
}
