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

public sealed partial class ConcreteCalculatorViewModel : ViewModelBase, IDisposable, ICalculatorViewModel
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
    /// Event fuer Navigation (ersetzt Shell.Current.GoToAsync)
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event fuer Alerts/Nachrichten (Titel, Nachricht)
    /// </summary>
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;
    public event Action? CalculationPerformed;

    /// <summary>
    /// Navigation auslösen
    /// </summary>
    private void NavigateTo(string route)
    {
        NavigationRequested?.Invoke(route);
    }

    public ConcreteCalculatorViewModel(
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

        // Einheitensystem-Änderungen abonnieren
        _unitConverter.UnitSystemChanged += OnUnitSystemChanged;

        // Standard-Materialpreis laden
        PricePerBag = (double)(_priceService.GetPrice("concrete_sack_25kg")?.EffectivePrice ?? 0);
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

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            // Validierung je nach ausgewähltem Sub-Rechner
            switch (SelectedCalculator)
            {
                case 0: // Platte
                    if (SlabLength <= 0 || SlabWidth <= 0 || SlabHeight <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    Result = _craftEngine.CalculateConcrete(0, SlabLength, SlabWidth, SlabHeight, BagWeight);
                    break;

                case 1: // Streifenfundament
                    if (StripLength <= 0 || StripWidth <= 0 || StripDepth <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    Result = _craftEngine.CalculateConcrete(1, StripLength, StripWidth, StripDepth, BagWeight);
                    break;

                case 2: // Säule
                    if (ColumnDiameter <= 0 || ColumnHeight <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    Result = _craftEngine.CalculateConcrete(2, ColumnDiameter, 0, ColumnHeight, BagWeight);
                    break;
            }

            HasResult = true;
            CalculationPerformed?.Invoke();

            // In History speichern
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

        // Platte
        SlabLength = 4;
        SlabWidth = 3;
        SlabHeight = 15;
        // Fundament
        StripLength = 20;
        StripWidth = 30;
        StripDepth = 80;
        // Säule
        ColumnDiameter = 30;
        ColumnHeight = 250;
        // Gemeinsam
        BagWeight = 25;
        PricePerBag = 0;
        PricePerCubicMeter = 0;
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
