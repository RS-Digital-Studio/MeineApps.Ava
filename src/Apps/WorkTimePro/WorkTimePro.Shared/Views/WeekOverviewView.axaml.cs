using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using WorkTimePro.Graphics;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class WeekOverviewView : UserControl
{
    private WeekOverviewViewModel? _vm;

    public WeekOverviewView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is WeekOverviewViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(_vm.Days):
            case nameof(_vm.WorkTimeDisplay):
            case nameof(_vm.TargetTimeDisplay):
            case nameof(_vm.IsLoading):
                WeekBarCanvas?.InvalidateSurface();
                WeekProgressCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.ProgressPercent):
                WeekProgressCanvas?.InvalidateSurface();
                break;
        }
    }

    /// <summary>
    /// Zeichnet das Wochen-Balkendiagramm mit Ist/Soll-Vergleich pro Tag.
    /// </summary>
    private void OnPaintWeekBars(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not WeekOverviewViewModel vm || vm.Days == null || vm.Days.Count != 7) return;

        // Tagesnamen, Ist-Stunden und Soll-Stunden aus den WorkDay-Objekten extrahieren
        var dayLabels = new string[7];
        var actualHours = new float[7];
        var targetHours = new float[7];
        int todayIndex = -1;

        for (int i = 0; i < 7; i++)
        {
            var day = vm.Days[i];
            dayLabels[i] = day.DayName;
            actualHours[i] = day.ActualWorkMinutes / 60f;
            targetHours[i] = day.TargetWorkMinutes / 60f;

            // Heutigen Tag erkennen
            if (day.Date.Date == DateTime.Today)
                todayIndex = i;
        }

        WeekBarVisualization.Render(canvas, bounds, dayLabels, actualHours, targetHours, todayIndex);
    }

    /// <summary>
    /// Zeichnet die Wochenfortschritts-Leiste als SkiaSharp Linear-Progress.
    /// </summary>
    private void OnPaintWeekProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not WeekOverviewViewModel vm) return;

        float progress = (float)(vm.ProgressPercent / 100.0);
        LinearProgressVisualization.Render(canvas, bounds, progress,
            SkiaThemeHelper.Primary, SkiaThemeHelper.Accent);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
    }
}
