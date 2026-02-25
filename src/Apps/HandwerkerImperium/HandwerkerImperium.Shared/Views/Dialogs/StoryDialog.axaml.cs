using Avalonia.Controls;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views.Dialogs;

/// <summary>
/// Story-Dialog mit Meister Hans SkiaSharp-Portrait.
/// Die Hans-Animation (Blinzeln + Idle-Bobbing) wird vom
/// MainView Render-Timer über UpdateHansAnimation() und
/// InvalidateHansCanvas() gesteuert.
/// </summary>
public partial class StoryDialog : UserControl
{
    // Meister Hans Animation-State
    private float _hansElapsed;
    private bool _hansBlinking;
    private float _nextBlinkTime = 3.5f;

    public StoryDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wird vom MainView Render-Timer aufgerufen (20fps).
    /// Aktualisiert Blinzel-Animation und invalidiert Canvas.
    /// </summary>
    public void UpdateHansAnimation()
    {
        _hansElapsed += 0.05f; // 50ms pro Tick

        // Blinzel-Logik: Alle 3-4.5s für ~150ms blinzeln
        if (_hansBlinking)
        {
            if (_hansElapsed > _nextBlinkTime + 0.15f)
            {
                _hansBlinking = false;
                _nextBlinkTime = _hansElapsed + 3f + Random.Shared.NextSingle() * 1.5f;
            }
        }
        else if (_hansElapsed >= _nextBlinkTime)
        {
            _hansBlinking = true;
        }

        MeisterHansCanvas?.InvalidateSurface();
    }

    private void OnMeisterHansPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        var vm = DataContext as MainViewModel;
        var mood = vm?.StoryMood ?? "happy";

        MeisterHansRenderer.Render(canvas, bounds, mood, _hansElapsed, _hansBlinking);
    }
}
