using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BomberBlast.Graphics;

namespace BomberBlast.Controls;

/// <summary>
/// Wiederverwendbares UserControl mit animiertem Bomberman-Menü-Hintergrund.
/// Unterstützt 7 Themes (Default, Dungeon, Shop, League, BattlePass, Victory, LuckySpin).
/// Nutzt MenuBackgroundRenderer für SkiaSharp-Rendering mit ~30fps Timer-Animation.
/// Startet automatisch bei Einfügen in den Visual Tree, stoppt beim Entfernen.
/// </summary>
public class MenuBackgroundCanvas : UserControl
{
    /// <summary>
    /// StyledProperty für das Hintergrund-Theme.
    /// Setzbar per XAML: &lt;controls:MenuBackgroundCanvas Theme="Dungeon" /&gt;
    /// </summary>
    public static readonly StyledProperty<BackgroundTheme> BackgroundThemeProperty =
        AvaloniaProperty.Register<MenuBackgroundCanvas, BackgroundTheme>(nameof(BackgroundTheme), BackgroundTheme.Default);

    public BackgroundTheme BackgroundTheme
    {
        get => GetValue(BackgroundThemeProperty);
        set => SetValue(BackgroundThemeProperty, value);
    }

    private SKCanvasView? _canvas;
    private DispatcherTimer? _timer;
    private readonly Stopwatch _stopwatch = new();
    private bool _initialized;

    public MenuBackgroundCanvas()
    {
        // Keine Touch-/Klick-Interaktion (reiner Hintergrund)
        IsHitTestVisible = false;

        // Canvas füllt das gesamte Control
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _canvas = new SKCanvasView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _canvas.PaintSurface += OnPaintSurface;

        Content = _canvas;

        // Automatisch starten/stoppen bei Visual-Tree-Änderungen
        AttachedToVisualTree += (_, _) => StartIfVisible();
        DetachedFromVisualTree += (_, _) => Stop();
        // Parent-Visibility-Wechsel (PageView-Border in MainView beim Wechsel ins Spiel):
        // IsEffectivelyVisible hat keine eigene AvaloniaProperty → EffectiveViewportChanged feuert,
        // wenn der Parent (un)sichtbar wird. Dann Timer KOMPLETT stoppen/neu starten, statt ihn
        // 30fps-leerlaufen zu lassen (UI-Thread-Dauerlast waehrend des ganzen Spiels).
        EffectiveViewportChanged += (_, _) =>
        {
            if (IsEffectivelyVisible) StartIfVisible();
            else Stop();
        };
    }

    /// <summary>
    /// Reagiert auf IsVisible- und Theme-Änderungen. Den Parent-Visibility-Fall (PageView-Border
    /// in MainView wird beim Wechsel ins Spiel unsichtbar) deckt der EffectiveViewportChanged-Handler
    /// im Ctor ab — die eigene IsVisible-Property bleibt dabei unverändert. Der IsEffectivelyVisible-
    /// Guard im Tick bleibt zusätzlich als Sicherheitsnetz.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            if (change.GetNewValue<bool>())
                StartIfVisible();
            else
                Stop();
        }
        else if (change.Property == BackgroundThemeProperty)
        {
            // Theme geändert → Renderer neu initialisieren
            _initialized = false;
            if (IsEffectivelyVisible && TopLevel.GetTopLevel(this) != null)
            {
                MenuBackgroundRenderer.Initialize(42, BackgroundTheme);
                _initialized = true;
                _canvas?.InvalidateSurface();
            }
        }
    }

    /// <summary>Startet nur wenn effektiv sichtbar (inkl. Parent-Visibility) und im Visual Tree.</summary>
    private void StartIfVisible()
    {
        if (IsEffectivelyVisible && TopLevel.GetTopLevel(this) != null)
            Start();
    }

    /// <summary>
    /// Startet die Animations-Schleife (~30fps).
    /// Initialisiert den Renderer beim ersten Aufruf mit dem gesetzten Theme.
    /// </summary>
    public void Start()
    {
        if (TopLevel.GetTopLevel(this) == null || !IsEffectivelyVisible)
            return;

        // Renderer einmalig initialisieren (mit aktuellem Theme)
        if (!_initialized)
        {
            MenuBackgroundRenderer.Initialize(42, BackgroundTheme);
            _initialized = true;
        }

        // Bestehenden Timer stoppen (kein doppelter Timer)
        _timer?.Stop();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) =>
        {
            // Skippen wenn Parent-Container (PageView-Border) unsichtbar ist.
            // Sonst würden ~20 Menü-Canvases in inaktiven Tabs parallel Frames rendern
            // und Android-CPU/GPU belasten → Akku, Heat, ruckelige aktive View.
            if (IsEffectivelyVisible)
                _canvas?.InvalidateSurface();
        };
        _timer.Start();
        _stopwatch.Restart();
    }

    /// <summary>
    /// Stoppt die Animations-Schleife und den Timer.
    /// </summary>
    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _stopwatch.Stop();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear();

        var time = (float)_stopwatch.Elapsed.TotalSeconds;
        MenuBackgroundRenderer.Render(canvas, bounds.Width, bounds.Height, time, BackgroundTheme);
    }
}
