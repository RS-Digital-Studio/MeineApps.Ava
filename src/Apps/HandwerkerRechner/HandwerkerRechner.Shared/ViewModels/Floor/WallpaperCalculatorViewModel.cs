using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class WallpaperCalculatorViewModel : ViewModelBase, IDisposable, ICalculatorViewModel
{
    private readonly CraftEngine _craftEngine;
    private Timer? _debounceTimer;
    private readonly IProjectService _projectService;
    private readonly ILocalizationService _localization;
    private readonly ICalculationHistoryService _historyService;
    private readonly IUnitConverterService _unitConverter;
    private readonly IMaterialExportService _exportService;
    private readonly IFileShareService _fileShareService;
    private readonly IMaterialPriceService _priceService;
    private string? _currentProjectId;

    /// <summary>
    /// Event to request navigation (replaces Shell.Current.GoToAsync)
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event for showing alerts/messages to the user (title, message)
    /// </summary>
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;
    public event Action? CalculationPerformed;

    /// <summary>
    /// Invoke navigation request
    /// </summary>
    private void NavigateTo(string route)
    {
        NavigationRequested?.Invoke(route);
    }

    public WallpaperCalculatorViewModel(
        CraftEngine craftEngine,
        IProjectService projectService,
        ILocalizationService localization,
        ICalculationHistoryService historyService,
        IUnitConverterService unitConverter,
        IMaterialExportService exportService,
        IFileShareService fileShareService,
        IMaterialPriceService priceService)
    {
        _craftEngine = craftEngine;
        _projectService = projectService;
        _localization = localization;
        _historyService = historyService;
        _unitConverter = unitConverter;
        _exportService = exportService;
        _fileShareService = fileShareService;
        _priceService = priceService;

        _unitConverter.UnitSystemChanged += OnUnitSystemChanged;

        // Standard-Materialpreis laden
        PricePerRoll = (double)(_priceService.GetPrice("wallpaper_standard")?.EffectivePrice ?? 0);
    }

    /// <summary>
    /// Debounce: Berechnung 300ms nach letzter Eingabe-Änderung auslösen
    /// </summary>
    private void ScheduleAutoCalculate()
    {
        if (_debounceTimer == null)
            _debounceTimer = new Timer(_ => Dispatcher.UIThread.Post(() => _ = Calculate()), null, 300, Timeout.Infinite);
        else
            _debounceTimer.Change(300, Timeout.Infinite);
    }

    /// <summary>
    /// Berechnet die Gesamtfläche der Abzüge (Türen + Fenster) — Formel in der CraftEngine
    /// </summary>
    private double CalculateDeductionArea()
    {
        if (!ShowDeductions) return 0;
        return _craftEngine.CalculateOpeningsDeduction(
            DoorCount, DoorWidth, DoorHeight, WindowCount, WindowWidth, WindowHeight);
    }

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            if (WallLength <= 0 || RoomHeight <= 0 || RollLength <= 0 || RollWidth <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(_localization.GetString("InvalidInputTitle"), _localization.GetString("ValueMustBePositive"));
                return;
            }

            // Negativer Rapport ist nicht sinnvoll
            if (PatternRepeat < 0) PatternRepeat = 0;

            // Abzugsfläche berechnen
            var deduction = CalculateDeductionArea();
            var grossArea = WallLength * RoomHeight;
            var effectiveWallLength = WallLength;
            if (deduction > 0 && grossArea > 0)
            {
                var netArea = Math.Max(0.1, grossArea - deduction);
                effectiveWallLength = WallLength * (netArea / grossArea);
            }
            DeductedAreaDisplay = deduction > 0 ? $"-{deduction:F1} m²" : "";

            // WallLength = Raumumfang (gesamte Wandlänge). Engine erwartet roomLength+roomWidth
            // und berechnet perimeter = 2*(L+W). Mit L=Umfang/2, W=0 ergibt sich perimeter=Umfang.
            Result = _craftEngine.CalculateWallpaper(effectiveWallLength / 2, 0, RoomHeight, RollLength, RollWidth, PatternRepeat);
            HasResult = true;
            CalculationPerformed?.Invoke();

            await SaveToHistoryAsync();
        }
        finally
        {
            IsCalculating = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        WallLength = 14.0;
        RoomHeight = 2.5;
        RollLength = 10.05;
        RollWidth = 53;
        PatternRepeat = 0;
        PricePerRoll = 0;
        ShowDeductions = false;
        DoorCount = 0;
        DoorWidth = 0.8;
        DoorHeight = 2.0;
        WindowCount = 0;
        WindowWidth = 1.2;
        WindowHeight = 1.0;
        DeductedAreaDisplay = "";
        Result = null;
        HasResult = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    /// <summary>
    /// Cleanup wenn die VM von der View weg-navigiert. API-konsistent mit Premium-VMs.
    /// </summary>
    public void Cleanup()
    {
        _unitConverter.UnitSystemChanged -= OnUnitSystemChanged;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public void Dispose()
    {
        // Auch im Dispose abmelden (robuster Pfad, falls Dispose ohne vorheriges Cleanup läuft). -= ist idempotent.
        _unitConverter.UnitSystemChanged -= OnUnitSystemChanged;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
