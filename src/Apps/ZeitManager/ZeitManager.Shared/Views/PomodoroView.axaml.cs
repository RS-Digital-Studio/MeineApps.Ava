using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;
using System.ComponentModel;
using ZeitManager.Graphics;
using ZeitManager.Models;
using ZeitManager.ViewModels;

namespace ZeitManager.Views;

public partial class PomodoroView : UserControl
{
    private Point _dragStart;
    private bool _isDragging;
    private TranslateTransform? _sheetTransform;
    private DispatcherTimer? _springTimer;
    private double _springFrom;
    private int _springFrame;

    // SkiaSharp Animation (Pomodoro-Ring Puls)
    private DispatcherTimer? _animTimer;
    private float _animTime;

    // Balken-Einfahranimation
    private DispatcherTimer? _barAnimTimer;
    private float _barAnimFraction;
    private bool _barAnimCompleted;

    // ViewModel-Referenz für saubere Event-Abmeldung
    private PomodoroViewModel? _viewModel;

    private const double DismissThreshold = 80;
    private const int SpringFrames = 10;
    private const int SpringIntervalMs = 16;

    public PomodoroView()
    {
        InitializeComponent();
    }

    private PomodoroViewModel? ViewModel => DataContext as PomodoroViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Alten Handler abmelden
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        // Neuen Handler anmelden
        if (DataContext is PomodoroViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>Selektive Canvas-Invalidierung je nach geändertem Property.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not PomodoroViewModel vm) return;

        switch (args.PropertyName)
        {
            // Ring-relevante Properties → nur PomodoroRingCanvas
            case nameof(vm.ProgressFraction):
            case nameof(vm.IsRunning):
            case nameof(vm.CurrentPhase):
            case nameof(vm.CurrentCycle):
            case nameof(vm.RemainingFormatted):
            case nameof(vm.TodaySessions):
            case nameof(vm.DailyGoal):
                PomodoroRingCanvas?.InvalidateSurface();
                UpdateAnimation(vm.IsRunning);
                break;

            // Wochen-Balken → nur WeeklyBarsCanvas
            case nameof(vm.WeekDays):
                WeeklyBarsCanvas?.InvalidateSurface();
                break;

            // Heatmap → nur HeatmapCanvas
            case nameof(vm.HeatmapDays):
                HeatmapCanvas?.InvalidateSurface();
                break;

            // Sichtbarkeitswechsel → alle Canvas (wegen Anzeige-Toggle)
            case nameof(vm.IsStatisticsView):
                PomodoroRingCanvas?.InvalidateSurface();
                WeeklyBarsCanvas?.InvalidateSurface();
                HeatmapCanvas?.InvalidateSurface();
                UpdateAnimation(vm.IsRunning);
                if (vm.IsStatisticsView)
                    StartBarAnimation();
                break;
        }
    }

    private void UpdateAnimation(bool isRunning)
    {
        if (isRunning && _animTimer == null)
        {
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animTimer.Tick += (_, _) =>
            {
                _animTime += 0.033f;
                PomodoroRingCanvas?.InvalidateSurface();
            };
            _animTimer.Start();
        }
        else if (!isRunning && _animTimer != null)
        {
            _animTimer.Stop();
            _animTimer = null;
        }
    }

    /// <summary>Rendert den Pomodoro-Fortschrittsring mit SkiaSharp.</summary>
    private void OnPaintPomodoroRing(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not PomodoroViewModel vm) return;

        int phase = vm.CurrentPhase switch
        {
            PomodoroPhase.Work => 0,
            PomodoroPhase.ShortBreak => 1,
            PomodoroPhase.LongBreak => 2,
            _ => 0
        };

        PomodoroVisualization.RenderRing(canvas, bounds,
            (float)vm.ProgressFraction, phase,
            vm.CurrentCycle, vm.CyclesBeforeLongBreak,
            vm.IsRunning, vm.RemainingFormatted ?? "25:00",
            vm.PhaseText ?? "", _animTime,
            vm.TodaySessions, vm.DailyGoal);
    }

    /// <summary>Startet die Balken-Einfahranimation (CubicEaseOut, ~500ms).</summary>
    private void StartBarAnimation()
    {
        StopBarAnimation();
        _barAnimFraction = 0f;
        _barAnimCompleted = false;

        _barAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _barAnimTimer.Tick += (_, _) =>
        {
            // ~500ms Dauer bei 33ms Intervall = ~15 Frames
            _barAnimFraction += 0.066f; // ~1.0 nach 15 Frames
            if (_barAnimFraction >= 1f)
            {
                _barAnimFraction = 1f;
                _barAnimCompleted = true;
                StopBarAnimation();
            }
            WeeklyBarsCanvas?.InvalidateSurface();
        };
        _barAnimTimer.Start();
    }

    /// <summary>Stoppt die Balken-Animation.</summary>
    private void StopBarAnimation()
    {
        _barAnimTimer?.Stop();
        _barAnimTimer = null;
    }

    /// <summary>Rendert das Wochen-Balkendiagramm mit SkiaSharp.</summary>
    private void OnPaintWeeklyBars(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not PomodoroViewModel vm) return;
        if (vm.WeekDays == null || vm.WeekDays.Count != 7) return;

        var dayNames = new string[7];
        var sessions = new int[7];
        int todayIndex = -1;

        for (int i = 0; i < 7; i++)
        {
            dayNames[i] = vm.WeekDays[i].DayName;
            sessions[i] = vm.WeekDays[i].Sessions;
            if (vm.WeekDays[i].IsToday)
                todayIndex = i;
        }

        // CubicEaseOut auf animFraction anwenden
        float t = Math.Clamp(_barAnimFraction, 0f, 1f);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        // Wenn Animation abgeschlossen oder nie gestartet: volle Hoehe
        float anim = _barAnimCompleted ? 1f : eased;

        PomodoroVisualization.RenderWeeklyBars(canvas, bounds,
            dayNames, sessions, todayIndex, anim);
    }

    /// <summary>Rendert die Monats-Heatmap mit SkiaSharp.</summary>
    private void OnPaintHeatmap(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not PomodoroViewModel vm) return;
        if (vm.HeatmapDays.Length == 0) return;

        PomodoroStatisticsVisualization.Render(canvas, bounds,
            vm.HeatmapDays, vm.HeatmapWeekDayLabels, vm.HeatmapTitle);
    }

    /// <summary>Backdrop-Tap schließt Config-Overlay.</summary>
    private void OnConfigBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewModel?.CancelConfigCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Drag-Zone: Pointer-Down startet Swipe-Tracking.</summary>
    private void OnDragZonePressed(object? sender, PointerPressedEventArgs e)
    {
        EnsureSheetTransform();
        _dragStart = e.GetPosition(this);
        _isDragging = true;
        StopSpring();
        if (sender is Control control)
            e.Pointer.Capture(control);
        e.Handled = true;
    }

    /// <summary>Drag-Zone: Pointer-Move verschiebt Sheet nach unten.</summary>
    private void OnDragZoneMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _sheetTransform == null) return;

        var current = e.GetPosition(this);
        var deltaY = current.Y - _dragStart.Y;

        // Nur nach unten verschieben (nicht nach oben)
        _sheetTransform.Y = Math.Max(0, deltaY);
        e.Handled = true;
    }

    /// <summary>Drag-Zone: Pointer-Up → Dismiss oder Zurückfedern.</summary>
    private void OnDragZoneReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        e.Pointer.Capture(null);

        if (_sheetTransform == null) return;

        if (_sheetTransform.Y >= DismissThreshold)
        {
            // Schwellwert erreicht → Sheet schließen
            _sheetTransform.Y = 0;
            ViewModel?.CancelConfigCommand.Execute(null);
        }
        else
        {
            // Zurückfedern
            SpringBack();
        }

        e.Handled = true;
    }

    /// <summary>Pointer-Capture verloren → Zurückfedern.</summary>
    private void OnDragZoneCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        if (_sheetTransform != null)
            SpringBack();
    }

    private void EnsureSheetTransform()
    {
        if (_sheetTransform != null) return;

        _sheetTransform = ConfigSheet.RenderTransform as TranslateTransform;
        if (_sheetTransform == null)
        {
            _sheetTransform = new TranslateTransform();
            ConfigSheet.RenderTransform = _sheetTransform;
        }
    }

    private void SpringBack()
    {
        if (_sheetTransform == null) return;

        StopSpring();
        _springFrom = _sheetTransform.Y;
        _springFrame = 0;

        if (_springFrom < 1)
        {
            _sheetTransform.Y = 0;
            return;
        }

        _springTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SpringIntervalMs) };
        _springTimer.Tick += OnSpringTick;
        _springTimer.Start();
    }

    private void OnSpringTick(object? sender, EventArgs e)
    {
        if (_sheetTransform == null) { StopSpring(); return; }

        _springFrame++;

        if (_springFrame >= SpringFrames)
        {
            _sheetTransform.Y = 0;
            StopSpring();
            return;
        }

        // CubicEaseOut
        var t = (double)_springFrame / SpringFrames;
        var eased = 1 - Math.Pow(1 - t, 3);
        _sheetTransform.Y = _springFrom * (1 - eased);
    }

    private void StopSpring()
    {
        if (_springTimer == null) return;
        _springTimer.Stop();
        _springTimer.Tick -= OnSpringTick;
        _springTimer = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Event-Handler abmelden (Memory-Leak verhindern)
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        _animTimer?.Stop();
        _animTimer = null;
        StopBarAnimation();
    }
}
