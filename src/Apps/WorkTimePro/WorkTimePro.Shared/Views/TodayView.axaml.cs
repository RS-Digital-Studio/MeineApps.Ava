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

        if (args.PropertyName is nameof(MainViewModel.TodayEntries)
            or nameof(MainViewModel.TodayPauses)
            or nameof(MainViewModel.CurrentStatus)
            or nameof(MainViewModel.CurrentWorkTime))
        {
            TimelineCanvas?.InvalidateSurface();
        }

        // Earnings CountUp Animation starten
        if (args.PropertyName == nameof(MainViewModel.TodayEarnings))
        {
            StartEarningsCountUp(_viewModel.TodayEarnings);
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
    /// </summary>
    private void StartEarningsCountUp(string newEarningsText)
    {
        // Neuen Zielwert parsen (Format: "12,34 EUR" oder "$12.34")
        if (!TryParseEarnings(newEarningsText, out var newValue)) return;

        // Wenn die Differenz zu klein ist, keine Animation noetig
        if (Math.Abs(newValue - _displayedEarnings) < 0.01)
        {
            _displayedEarnings = newValue;
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
            EarningsTextBlock.Text = value.ToString("C2");
        }
    }

    /// <summary>
    /// Versucht einen Waehrungsstring zu parsen (unabhaengig von Kultur).
    /// </summary>
    private static bool TryParseEarnings(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Alle nicht-numerischen Zeichen entfernen (ausser Komma, Punkt, Minus)
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == ',' || c == '.' || c == '-').ToArray());

        // Komma durch Punkt ersetzen fuer Double.TryParse
        // Europaeisches Format: 12.345,67 -> 12345.67
        // US-Format: 12,345.67 -> 12345.67
        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            // Beide vorhanden: Das letzte Trennzeichen ist der Dezimaltrenner
            var lastComma = cleaned.LastIndexOf(',');
            var lastDot = cleaned.LastIndexOf('.');
            if (lastComma > lastDot)
            {
                // Europaeisch: Punkt als Tausender, Komma als Dezimal
                cleaned = cleaned.Replace(".", "").Replace(",", ".");
            }
            else
            {
                // US: Komma als Tausender, Punkt als Dezimal
                cleaned = cleaned.Replace(",", "");
            }
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(",", ".");
        }

        return double.TryParse(cleaned, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
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

        // TimeEntries in TimeBlocks konvertieren (CheckIn/CheckOut-Paare -> Arbeitsbloecke)
        var blocks = BuildTimeBlocks(vm);
        float currentHour = DateTime.Now.Hour + DateTime.Now.Minute / 60f;

        DayTimelineVisualization.Render(canvas, bounds, blocks, currentHour);
    }

    /// <summary>
    /// Konvertiert TodayEntries (CheckIn/CheckOut-Paare) + TodayPauses in TimeBlocks.
    /// </summary>
    private static DayTimelineVisualization.TimeBlock[] BuildTimeBlocks(MainViewModel vm)
    {
        var blocks = new List<DayTimelineVisualization.TimeBlock>();

        // 1. Arbeitsbloecke aus TimeEntry-Paaren (CheckIn -> CheckOut)
        var entries = vm.TodayEntries.OrderBy(e => e.Timestamp).ToList();
        DateTime? lastCheckIn = null;

        foreach (var entry in entries)
        {
            if (entry.Type == EntryType.CheckIn)
            {
                lastCheckIn = entry.Timestamp;
            }
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                float startH = lastCheckIn.Value.Hour + lastCheckIn.Value.Minute / 60f;
                float endH = entry.Timestamp.Hour + entry.Timestamp.Minute / 60f;
                blocks.Add(new DayTimelineVisualization.TimeBlock(startH, endH, false));
                lastCheckIn = null;
            }
        }

        // Offener CheckIn -> bis jetzt zeichnen
        if (lastCheckIn != null && vm.CurrentStatus != TrackingStatus.Idle)
        {
            float startH = lastCheckIn.Value.Hour + lastCheckIn.Value.Minute / 60f;
            float endH = DateTime.Now.Hour + DateTime.Now.Minute / 60f;
            blocks.Add(new DayTimelineVisualization.TimeBlock(startH, endH, false));
        }

        // 2. Pausen als separate Bloecke ueberlagern
        foreach (var pause in vm.TodayPauses)
        {
            float startH = pause.StartTime.Hour + pause.StartTime.Minute / 60f;
            float endH = pause.EndTime.HasValue
                ? pause.EndTime.Value.Hour + pause.EndTime.Value.Minute / 60f
                : DateTime.Now.Hour + DateTime.Now.Minute / 60f;
            blocks.Add(new DayTimelineVisualization.TimeBlock(startH, endH, true));
        }

        return blocks.ToArray();
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
