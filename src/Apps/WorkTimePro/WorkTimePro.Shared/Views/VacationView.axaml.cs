using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using WorkTimePro.Graphics;
using WorkTimePro.Resources.Strings;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class VacationView : UserControl
{
    private VacationViewModel? _vm;

    public VacationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is VacationViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        QuotaGaugeCanvas?.InvalidateSurface();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Bei neuen Quota-Daten den Ring-Gauge neu zeichnen.
        if (e.PropertyName == nameof(VacationViewModel.Statistics))
            QuotaGaugeCanvas?.InvalidateSurface();
    }

    private void OnPaintQuotaGauge(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.Statistics is not { } s) return;

        VacationQuotaGaugeVisualization.Render(canvas, canvas.LocalClipBounds,
            totalDays: s.AvailableDays, usedDays: s.TakenDays,
            plannedDays: s.PlannedDays, remainingDays: s.RemainingDays,
            usedLabel: AppStrings.Taken, plannedLabel: AppStrings.Planned,
            remainLabel: AppStrings.Remaining, daysLabel: AppStrings.Days);
    }
}
