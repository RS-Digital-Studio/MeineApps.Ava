using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using WorkTimePro.Graphics;
using WorkTimePro.Models;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class TodayView : UserControl
{
    // Earnings CountUp Animation
    private double _displayedEarnings;
    private double _targetEarnings;
    private DispatcherTimer? _countUpTimer;
    private int _countUpFrame;
    private const int CountUpTotalFrames = 24; // ~800ms bei 30fps
    private double _countUpStartValue;

    // ViewModel-Referenz fuer sauberes Event-Handling
    private MainViewModel? _viewModel;

    // Gecachte TimeBlocks (vermeidet List + LINQ + ToArray bei jedem PaintSurface im 1s-Takt).
    // Wir cachen NUR die abgeschlossenen Blöcke (vollständige CheckIn/CheckOut-Paare und
    // beendete Pausen). Die noch offenen Segmente werden pro Frame aus den gespeicherten
    // Start-Timestamps + DateTime.Now zusammengesetzt — kein BuildTimeBlocks mehr pro Sekunde.
    private DayTimelineVisualization.TimeBlock[]? _cachedClosedBlocks;
    private DateTime? _openCheckInTimestamp;
    private DateTime? _openPauseStartTimestamp;
    private bool _timeBlocksDirty = true;

    public TodayView()
    {
        InitializeComponent();
    }

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
        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>
    /// Benannter Handler fuer ViewModel-PropertyChanged (kein anonymes Lambda).
    /// Ermoeglicht saubere Abmeldung in OnDetachedFromVisualTree.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_viewModel == null) return;

        // Cache-Rebuild nur bei strukturellen Änderungen (Entries/Pauses/Status).
        // CurrentWorkTime triggert nur einen Repaint — der laufende Block wird in
        // GetCurrentBlocks() aus dem gespeicherten Start-Timestamp + jetzigem DateTime.Now
        // dynamisch ergänzt, ohne den ganzen Cache neu zu bauen.
        if (args.PropertyName is nameof(MainViewModel.TodayEntries)
            or nameof(MainViewModel.TodayPauses)
            or nameof(MainViewModel.CurrentStatus))
        {
            _timeBlocksDirty = true;
            TimelineCanvas?.InvalidateSurface();
        }
        else if (args.PropertyName == nameof(MainViewModel.CurrentWorkTime))
        {
            TimelineCanvas?.InvalidateSurface();
        }

        // Earnings CountUp Animation starten (lauscht auf den double-Wert,
        // nicht den formatierten String — kein Reparsing pro Sekunde mehr nötig)
        if (args.PropertyName == nameof(MainViewModel.TodayEarningsValue))
        {
            StartEarningsCountUp(_viewModel.TodayEarningsValue);
        }

        // Balance-Puls bei negativer Balance
        if (args.PropertyName == nameof(MainViewModel.IsBalanceNegative))
        {
            UpdateBalancePulse(_viewModel.IsBalanceNegative);
        }
    }

    /// <summary>
    /// Startet die CountUp-Animation fuer den Earnings-Wert.
    /// Interpoliert vom alten zum neuen Wert ueber ~800ms mit EaseOut.
    /// Throttle: Bei kleinen Differenzen (&lt; 0.10 EUR) wird nicht animiert,
    /// damit der 1s-Timer nicht jede Sekunde 30fps-Loops startet (GC-Druck).
    /// </summary>
    private void StartEarningsCountUp(double newValue)
    {
        var diff = Math.Abs(newValue - _displayedEarnings);

        // Bei sehr kleinen Differenzen: nichts tun — das Binding aktualisiert
        // den Anzeigetext bereits in der nächsten Sekunde.
        if (diff < 0.10)
        {
            // Animation läuft? Dann beenden und Zielwert direkt darstellen.
            if (_countUpTimer is { IsEnabled: true })
            {
                _countUpTimer.Stop();
                _displayedEarnings = newValue;
                UpdateEarningsDisplay(newValue);
            }
            else
            {
                _displayedEarnings = newValue;
            }
            return;
        }

        _countUpStartValue = _displayedEarnings;
        _targetEarnings = newValue;
        _countUpFrame = 0;

        // Timer starten (30fps = 33ms pro Frame)
        if (_countUpTimer == null)
        {
            _countUpTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _countUpTimer.Tick += OnCountUpTick;
        }

        _countUpTimer.Start();
    }

    private void OnCountUpTick(object? sender, EventArgs e)
    {
        _countUpFrame++;

        if (_countUpFrame >= CountUpTotalFrames)
        {
            // Animation abgeschlossen
            _countUpTimer?.Stop();
            _displayedEarnings = _targetEarnings;
            UpdateEarningsDisplay(_targetEarnings);
            return;
        }

        // EaseOut-Interpolation: t = 1 - (1 - progress)^3
        var progress = (double)_countUpFrame / CountUpTotalFrames;
        var eased = 1.0 - Math.Pow(1.0 - progress, 3);

        var currentValue = _countUpStartValue + (_targetEarnings - _countUpStartValue) * eased;
        _displayedEarnings = currentValue;
        UpdateEarningsDisplay(currentValue);
    }

    private void UpdateEarningsDisplay(double value)
    {
        if (EarningsTextBlock != null)
        {
            // Explizit aktuelle Kultur verwenden (konsistent mit App-Spracheinstellung)
            EarningsTextBlock.Text = value.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    /// Setzt oder entfernt die pulsierende Animation auf dem Balance-TextBlock.
    /// </summary>
    private void UpdateBalancePulse(bool isNegative)
    {
        if (BalanceTextBlock == null) return;

        if (isNegative)
        {
            if (!BalanceTextBlock.Classes.Contains("BalancePulse"))
                BalanceTextBlock.Classes.Add("BalancePulse");
        }
        else
        {
            BalanceTextBlock.Classes.Remove("BalancePulse");
            BalanceTextBlock.Opacity = 1.0; // Reset auf volle Sichtbarkeit
        }
    }

    /// <summary>
    /// Zeichnet die Tages-Timeline (Arbeitsbloecke + Pausen als farbige Segmente).
    /// </summary>
    private void OnPaintTimeline(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm) return;

        // Cache nur bei strukturellen Datenänderungen neu bauen (Entries/Pauses/Status).
        // Live-Segmente werden pro Frame angefügt — kein BuildTimeBlocks-LINQ pro Sekunde.
        if (_timeBlocksDirty || _cachedClosedBlocks == null)
        {
            RebuildClosedBlockCache(vm);
            _timeBlocksDirty = false;
        }

        var blocks = ComposeBlocksWithLiveSegments();
        float currentHour = DateTime.Now.Hour + DateTime.Now.Minute / 60f;

        DayTimelineVisualization.Render(canvas, bounds, blocks, currentHour);
    }

    /// <summary>
    /// Baut das Cache-Array aus abgeschlossenen Segmenten (CheckIn/CheckOut-Paare und beendete Pausen).
    /// Speichert die offenen Segment-Starts (offener CheckIn, aktive Pause) separat als Timestamps.
    /// </summary>
    private void RebuildClosedBlockCache(MainViewModel vm)
    {
        // Vorher zählen, dann ohne Zwischenliste in ein passend großes Array schreiben
        var entries = vm.TodayEntries;
        var pauses = vm.TodayPauses;

        int closedCount = 0;
        DateTime? lastCheckIn = null;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Type == EntryType.CheckIn) lastCheckIn = entry.Timestamp;
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null) { closedCount++; lastCheckIn = null; }
        }
        _openCheckInTimestamp = (lastCheckIn != null && vm.CurrentStatus != TrackingStatus.Idle)
            ? lastCheckIn : null;

        DateTime? activePauseStart = null;
        for (var i = 0; i < pauses.Count; i++)
        {
            var p = pauses[i];
            if (p.EndTime.HasValue) closedCount++;
            else activePauseStart = p.StartTime;
        }
        _openPauseStartTimestamp = activePauseStart;

        var arr = new DayTimelineVisualization.TimeBlock[closedCount];
        int idx = 0;

        lastCheckIn = null;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Type == EntryType.CheckIn)
            {
                lastCheckIn = entry.Timestamp;
            }
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                arr[idx++] = new DayTimelineVisualization.TimeBlock(
                    lastCheckIn.Value.Hour + lastCheckIn.Value.Minute / 60f,
                    entry.Timestamp.Hour + entry.Timestamp.Minute / 60f,
                    false);
                lastCheckIn = null;
            }
        }
        for (var i = 0; i < pauses.Count; i++)
        {
            var p = pauses[i];
            if (!p.EndTime.HasValue) continue;
            arr[idx++] = new DayTimelineVisualization.TimeBlock(
                p.StartTime.Hour + p.StartTime.Minute / 60f,
                p.EndTime.Value.Hour + p.EndTime.Value.Minute / 60f,
                true);
        }

        _cachedClosedBlocks = arr;
    }

    /// <summary>
    /// Komponiert das finale TimeBlock-Array für den Renderer. Wenn keine offenen
    /// Segmente vorhanden sind, wird der Cache direkt zurückgegeben (0 Allokationen).
    /// Sonst wird ein Array mit den Live-Segmenten am Ende neu erstellt.
    /// </summary>
    private DayTimelineVisualization.TimeBlock[] ComposeBlocksWithLiveSegments()
    {
        var closed = _cachedClosedBlocks ?? Array.Empty<DayTimelineVisualization.TimeBlock>();

        var liveCount = 0;
        if (_openCheckInTimestamp != null) liveCount++;
        if (_openPauseStartTimestamp != null) liveCount++;
        if (liveCount == 0) return closed;

        var result = new DayTimelineVisualization.TimeBlock[closed.Length + liveCount];
        Array.Copy(closed, result, closed.Length);

        var now = DateTime.Now;
        var nowH = now.Hour + now.Minute / 60f;
        var idx = closed.Length;

        if (_openCheckInTimestamp != null)
        {
            var s = _openCheckInTimestamp.Value;
            result[idx++] = new DayTimelineVisualization.TimeBlock(
                s.Hour + s.Minute / 60f, nowH, false);
        }
        if (_openPauseStartTimestamp != null)
        {
            var s = _openPauseStartTimestamp.Value;
            result[idx++] = new DayTimelineVisualization.TimeBlock(
                s.Hour + s.Minute / 60f, nowH, true);
        }

        return result;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _countUpTimer?.Stop();

        // PropertyChanged-Handler sauber abmelden
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }
}
