using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels.Premium;

public partial class RoofSolarViewModel : ObservableObject
{
    private readonly CraftEngine _engine;
    private readonly IProjectService _projectService;
    private readonly ILocalizationService _localization;
    private readonly ICalculationHistoryService _historyService;
    private readonly IMaterialExportService _exportService;
    private readonly IFileShareService _fileShareService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private string? _currentProjectId;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;
    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    public RoofSolarViewModel(
        CraftEngine engine,
        IProjectService projectService,
        ILocalizationService localization,
        ICalculationHistoryService historyService,
        IMaterialExportService exportService,
        IFileShareService fileShareService,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService)
    {
        _engine = engine;
        _projectService = projectService;
        _localization = localization;
        _historyService = historyService;
        _exportService = exportService;
        _fileShareService = fileShareService;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
    }

    /// <summary>
    /// Loads project data by ID (replaces IQueryAttributable)
    /// </summary>
    public async Task LoadFromProjectIdAsync(string projectId)
    {
        if (!string.IsNullOrEmpty(projectId))
        {
            _currentProjectId = projectId;
            await LoadProjectAsync(projectId);
        }
    }

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => Calculators[SelectedCalculator];

    // Calculator Selection
    [ObservableProperty] private int _selectedCalculator;

    partial void OnSelectedCalculatorChanged(int value)
    {
        // Reset cost values when switching calculators to prevent state leaking
        switch (value)
        {
            case 0: // Roof Pitch - reset costs
                PricePerTile = 0;
                SolarSystemCost = 0;
                break;
            case 1: // Roof Tiles - reset solar cost
                SolarSystemCost = 0;
                break;
            case 2: // Solar - reset tile cost
                PricePerTile = 0;
                break;
        }
        // Clear results when switching
        HasResult = false;
    }

    private List<string>? _calculators;
    public List<string> Calculators => _calculators ??= [
        _localization.GetString("RoofPitch"),
        _localization.GetString("RoofTiles"),
        _localization.GetString("SolarYield")
    ];

    // Roof Pitch Inputs
    [ObservableProperty] private double _run = 5;
    [ObservableProperty] private double _rise = 2;

    // Roof Tiles Inputs
    [ObservableProperty] private double _roofArea = 100;
    [ObservableProperty] private double _tilesPerSqm = 10;

    // Solar Yield Inputs
    [ObservableProperty] private double _solarRoofArea = 50;
    [ObservableProperty] private double _panelEfficiency = 20;
    [ObservableProperty] private int _selectedOrientation = 4; // South
    [ObservableProperty] private double _tiltDegrees = 30;

    public List<string> Orientations => [
        _localization.GetString("OrientationNorth"),
        _localization.GetString("OrientationNorthEast"),
        _localization.GetString("OrientationEast"),
        _localization.GetString("OrientationSouthEast"),
        _localization.GetString("OrientationSouth"),
        _localization.GetString("OrientationSouthWest"),
        _localization.GetString("OrientationWest"),
        _localization.GetString("OrientationNorthWest")
    ];

    // Results
    [ObservableProperty] private RoofPitchResult? _pitchResult;
    [ObservableProperty] private RoofTilesResult? _tilesResult;
    [ObservableProperty] private SolarYieldResult? _solarResult;
    [ObservableProperty] private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    #region Cost Calculation

    // Dachneigung: Keine Kostenberechnung (nur Winkelberechnung)

    // Dachziegel: Preis pro Ziegel
    [ObservableProperty]
    private double _pricePerTile = 0;

    [ObservableProperty]
    private bool _showTileCost = false;

    public string RoofTileCostDisplay => (ShowTileCost && PricePerTile > 0 && TilesResult != null && TilesResult.TilesWithReserve > 0)
        ? $"{_localization.GetString("TotalCost")}: {(TilesResult.TilesWithReserve * PricePerTile):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerTileChanged(double value)
    {
        ShowTileCost = value > 0;
        OnPropertyChanged(nameof(RoofTileCostDisplay));
    }

    partial void OnTilesResultChanged(RoofTilesResult? value)
    {
        OnPropertyChanged(nameof(RoofTileCostDisplay));
    }

    // Solar-Ertrag: Strompreis + Anlagenkosten
    [ObservableProperty]
    private double _pricePerKwh = 0.30;

    [ObservableProperty]
    private double _solarSystemCost = 0;

    [ObservableProperty]
    private bool _showSolarCost = false;

    public string SolarCostDisplay => ShowSolarCost && SolarSystemCost > 0
        ? $"{_localization.GetString("ResultSystemCost")}: {SolarSystemCost:F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string PaybackTimeDisplay => (ShowSolarCost && SolarSystemCost > 0 && SolarResult != null && SolarResult.AnnualYieldKwh > 0 && PricePerKwh > 0)
        ? $"{_localization.GetString("ResultPaybackTime")}: {(SolarSystemCost / (SolarResult.AnnualYieldKwh * PricePerKwh)):F1} {_localization.GetString("HistoryYears")}"
        : "";

    partial void OnPricePerKwhChanged(double value)
    {
        OnPropertyChanged(nameof(PaybackTimeDisplay));
    }

    partial void OnSolarSystemCostChanged(double value)
    {
        ShowSolarCost = value > 0;
        OnPropertyChanged(nameof(SolarCostDisplay));
        OnPropertyChanged(nameof(PaybackTimeDisplay));
    }

    partial void OnSolarResultChanged(SolarYieldResult? value)
    {
        OnPropertyChanged(nameof(PaybackTimeDisplay));
    }

    #endregion

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            switch (SelectedCalculator)
            {
                case 0: // Roof Pitch
                    if (Run <= 0 || Rise <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    PitchResult = _engine.CalculateRoofPitch(Run, Rise);
                    TilesResult = null;
                    SolarResult = null;
                    break;

                case 1: // Roof Tiles
                    if (RoofArea <= 0 || TilesPerSqm <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    TilesResult = _engine.CalculateRoofTiles(RoofArea, TilesPerSqm);
                    PitchResult = null;
                    SolarResult = null;
                    break;

                case 2: // Solar Yield
                    if (SolarRoofArea <= 0 || PanelEfficiency <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    // Physikalische Obergrenzen: Effizienz max 100%, Neigung 0-90°
                    if (PanelEfficiency > 100) PanelEfficiency = 100;
                    if (TiltDegrees < 0) TiltDegrees = 0;
                    if (TiltDegrees > 90) TiltDegrees = 90;
                    var orientation = (Orientation)SelectedOrientation;
                    SolarResult = _engine.EstimateSolarYield(SolarRoofArea, PanelEfficiency / 100, orientation, TiltDegrees);
                    PitchResult = null;
                    TilesResult = null;
                    break;
            }
            HasResult = true;

            // Save to history
            await SaveToHistoryAsync();
        }
        finally
        {
            IsCalculating = false;
        }
    }

    private async Task SaveToHistoryAsync()
    {
        try
        {
            string calcType, title;
            Dictionary<string, object> data;

            switch (SelectedCalculator)
            {
                case 0: // Roof Pitch
                    calcType = "RoofPitchCalculator";
                    title = string.Format(_localization.GetString("HistoryRoofPitch"), Run, Rise);
                    data = new Dictionary<string, object>
                    {
                        ["Run"] = Run,
                        ["Rise"] = Rise,
                        ["Result"] = PitchResult != null ? new Dictionary<string, object>
                        {
                            ["PitchDegrees"] = PitchResult.PitchDegrees,
                            ["PitchPercent"] = PitchResult.PitchPercent
                        } : new Dictionary<string, object>()
                    };
                    break;

                case 1: // Roof Tiles
                    calcType = "RoofTilesCalculator";
                    title = $"{RoofArea} m\u00b2 {_localization.GetString("HistoryRoofArea")}";
                    data = new Dictionary<string, object>
                    {
                        ["RoofArea"] = RoofArea,
                        ["TilesPerSqm"] = TilesPerSqm,
                        ["Result"] = TilesResult != null ? new Dictionary<string, object>
                        {
                            ["TilesNeeded"] = TilesResult.TilesNeeded,
                            ["TilesWithReserve"] = TilesResult.TilesWithReserve
                        } : new Dictionary<string, object>()
                    };
                    break;

                default: // Solar Yield
                    calcType = "SolarYieldCalculator";
                    title = $"{SolarRoofArea} m\u00b2, {Orientations[SelectedOrientation]}";
                    data = new Dictionary<string, object>
                    {
                        ["SolarRoofArea"] = SolarRoofArea,
                        ["PanelEfficiency"] = PanelEfficiency,
                        ["SelectedOrientation"] = SelectedOrientation,
                        ["TiltDegrees"] = TiltDegrees,
                        ["Result"] = SolarResult != null ? new Dictionary<string, object>
                        {
                            ["KwPeak"] = SolarResult.KwPeak,
                            ["AnnualYieldKwh"] = SolarResult.AnnualYieldKwh,
                            ["UsableArea"] = SolarResult.UsableArea
                        } : new Dictionary<string, object>()
                    };
                    break;
            }

            await _historyService.AddCalculationAsync(calcType, title, data);
        }
        catch (Exception)
        {
            // Silently ignore history save errors
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Run = 5;
        Rise = 2;
        RoofArea = 100;
        TilesPerSqm = 10;
        SolarRoofArea = 50;
        PanelEfficiency = 20;
        SelectedOrientation = 4;
        TiltDegrees = 30;
        PricePerTile = 0;
        PricePerKwh = 0.30;
        SolarSystemCost = 0;
        PitchResult = null;
        TilesResult = null;
        SolarResult = null;
        HasResult = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    [RelayCommand]
    private void SaveProject()
    {
        if (!HasResult) return;
        SaveProjectName = _currentProjectId != null ? string.Empty : DefaultProjectName;
        ShowSaveDialog = true;
    }

    [RelayCommand]
    private async Task ConfirmSaveProject()
    {
        var name = SaveProjectName;
        if (string.IsNullOrWhiteSpace(name))
            name = DefaultProjectName;

        ShowSaveDialog = false;

        try
        {
            var calcType = SelectedCalculator switch
            {
                0 => CalculatorType.RoofPitch,
                1 => CalculatorType.RoofTiles,
                2 => CalculatorType.SolarYield,
                _ => CalculatorType.RoofPitch
            };

            var project = new Project
            {
                Name = name,
                CalculatorType = calcType
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["SelectedCalculator"] = SelectedCalculator,
                // Roof Pitch
                ["Run"] = Run,
                ["Rise"] = Rise,
                // Roof Tiles
                ["RoofArea"] = RoofArea,
                ["TilesPerSqm"] = TilesPerSqm,
                ["PricePerTile"] = PricePerTile,
                // Solar Yield
                ["SolarRoofArea"] = SolarRoofArea,
                ["PanelEfficiency"] = PanelEfficiency,
                ["SelectedOrientation"] = SelectedOrientation,
                ["TiltDegrees"] = TiltDegrees,
                ["PricePerKwh"] = PricePerKwh,
                ["SolarSystemCost"] = SolarSystemCost
            };

            // Result-Daten mitspeichern (je nach aktuellem Sub-Rechner)
            if (HasResult)
            {
                switch (SelectedCalculator)
                {
                    case 0 when PitchResult != null:
                    {
                        data["Result"] = new Dictionary<string, object>
                        {
                            ["ResultPitchDegrees"] = $"{PitchResult.PitchDegrees:F1}\u00b0",
                            ["ResultPitchPercent"] = $"{PitchResult.PitchPercent:F1} %"
                        };
                        break;
                    }
                    case 1 when TilesResult != null:
                    {
                        var resultData = new Dictionary<string, object>
                        {
                            ["ResultTilesNeeded"] = TilesResult.TilesNeeded,
                            ["ResultTilesWithReserve"] = TilesResult.TilesWithReserve
                        };
                        if (PricePerTile > 0)
                            resultData["TotalCost"] = $"{(TilesResult.TilesWithReserve * PricePerTile):F2}";
                        data["Result"] = resultData;
                        break;
                    }
                    case 2 when SolarResult != null:
                    {
                        var resultData = new Dictionary<string, object>
                        {
                            ["ResultPeakPower"] = $"{SolarResult.KwPeak:F1} kWp",
                            ["ResultAnnualYield"] = $"{SolarResult.AnnualYieldKwh:F0} kWh",
                            ["ResultUsableArea"] = $"{SolarResult.UsableArea:F1} m\u00b2"
                        };
                        if (SolarSystemCost > 0 && SolarResult.AnnualYieldKwh > 0 && PricePerKwh > 0)
                        {
                            var paybackYears = SolarSystemCost / (SolarResult.AnnualYieldKwh * PricePerKwh);
                            resultData["ResultPaybackTime"] = $"{paybackYears:F1} {_localization.GetString("HistoryYears") ?? "years"}";
                        }
                        data["Result"] = resultData;
                        break;
                    }
                }
            }

            project.SetData(data);
            await _projectService.SaveProjectAsync(project);
            _currentProjectId = project.Id;

            MessageRequested?.Invoke(
                _localization.GetString("Success"),
                _localization.GetString("ProjectSaved"));
            FloatingTextRequested?.Invoke(_localization.GetString("ProjectSaved"), "success");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error"),
                _localization.GetString("ProjectSaveFailed"));
        }
    }

    [RelayCommand]
    private void CancelSaveProject()
    {
        ShowSaveDialog = false;
        SaveProjectName = string.Empty;
    }

    private async Task LoadProjectAsync(string projectId)
    {
        try
        {
            var project = await _projectService.LoadProjectAsync(projectId);
            if (project == null)
                return;

            SelectedCalculator = project.GetValue("SelectedCalculator", 0);

            // Roof Pitch
            Run = project.GetValue("Run", 5.0);
            Rise = project.GetValue("Rise", 2.0);

            // Roof Tiles
            RoofArea = project.GetValue("RoofArea", 100.0);
            TilesPerSqm = project.GetValue("TilesPerSqm", 10.0);
            PricePerTile = project.GetValue("PricePerTile", 0.0);

            // Solar Yield
            SolarRoofArea = project.GetValue("SolarRoofArea", 50.0);
            PanelEfficiency = project.GetValue("PanelEfficiency", 20.0);
            SelectedOrientation = project.GetValue("SelectedOrientation", 4);
            TiltDegrees = project.GetValue("TiltDegrees", 30.0);
            PricePerKwh = project.GetValue("PricePerKwh", 0.30);
            SolarSystemCost = project.GetValue("SolarSystemCost", 0.0);

            await Calculate();
        }
        catch (Exception)
        {
            // Silently ignore load errors
        }
    }

    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult) return;

        string text;
        switch (SelectedCalculator)
        {
            case 0 when PitchResult != null:
                text = $"{Calculators[0]}\n" +
                       $"─────────────\n" +
                       $"{_localization.GetString("ResultPitchDegrees")}: {PitchResult.PitchDegrees:F1}\u00b0\n" +
                       $"{_localization.GetString("ResultPitchPercent")}: {PitchResult.PitchPercent:F1} %";
                break;

            case 1 when TilesResult != null:
                text = $"{Calculators[1]}\n" +
                       $"─────────────\n" +
                       $"{_localization.GetString("ResultTilesNeeded")}: {TilesResult.TilesNeeded}\n" +
                       $"{_localization.GetString("ResultWithReserveFivePercent")}: {TilesResult.TilesWithReserve}";
                if (ShowTileCost && PricePerTile > 0)
                    text += $"\n{_localization.GetString("TotalCost")}: {RoofTileCostDisplay}";
                break;

            case 2 when SolarResult != null:
                text = $"{Calculators[2]}\n" +
                       $"─────────────\n" +
                       $"{_localization.GetString("ResultPeakPower")}: {SolarResult.KwPeak:F1} kWp\n" +
                       $"{_localization.GetString("ResultAnnualYield")}: {SolarResult.AnnualYieldKwh:F0} kWh\n" +
                       $"{_localization.GetString("ResultUsableArea")}: {SolarResult.UsableArea:F1} m\u00b2";
                if (ShowSolarCost && SolarSystemCost > 0)
                    text += $"\n{SolarCostDisplay}";
                if (ShowSolarCost && SolarSystemCost > 0 && !string.IsNullOrEmpty(PaybackTimeDisplay))
                    text += $"\n{PaybackTimeDisplay}";
                break;

            default:
                return;
        }

        ClipboardRequested?.Invoke(text);
        FloatingTextRequested?.Invoke(_localization.GetString("CopiedToClipboard") ?? "Copied!", "success");
    }

    [RelayCommand]
    private async Task ExportMaterialList()
    {
        if (!HasResult) return;
        if (IsExporting) return;

        try
        {
            IsExporting = true;

            if (!_purchaseService.IsPremium)
            {
                var adResult = await _rewardedAdService.ShowAdAsync("material_pdf");
                if (!adResult) return;
            }

            var calcType = Calculators[SelectedCalculator];
            var inputs = new Dictionary<string, string>();
            var results = new Dictionary<string, string>();

            switch (SelectedCalculator)
            {
                case 0 when PitchResult != null:
                    inputs[_localization.GetString("LabelBaseLengthM") ?? "Base length"] = $"{Run:F1} m";
                    inputs[_localization.GetString("LabelRiseM") ?? "Rise"] = $"{Rise:F1} m";
                    results[_localization.GetString("ResultPitchDegrees") ?? "Pitch (deg)"] = $"{PitchResult.PitchDegrees:F1}\u00b0";
                    results[_localization.GetString("ResultPitchPercent") ?? "Pitch (%)"] = $"{PitchResult.PitchPercent:F1} %";
                    break;
                case 1 when TilesResult != null:
                    inputs[_localization.GetString("LabelRoofAreaSqm") ?? "Roof area"] = $"{RoofArea:F1} m\u00b2";
                    inputs[_localization.GetString("LabelTilesPerSqm") ?? "Tiles/m\u00b2"] = $"{TilesPerSqm}";
                    results[_localization.GetString("ResultTilesNeeded") ?? "Tiles"] = $"{TilesResult.TilesNeeded}";
                    results[_localization.GetString("ResultTilesWithReserve") ?? "With reserve"] = $"{TilesResult.TilesWithReserve}";
                    if (PricePerTile > 0)
                        results[_localization.GetString("TotalCost") ?? "Total cost"] = $"{TilesResult.TilesWithReserve * PricePerTile:F2} {_localization.GetString("CurrencySymbol")}";
                    break;
                case 2 when SolarResult != null:
                    inputs[_localization.GetString("LabelRoofAreaSqm") ?? "Roof area"] = $"{SolarRoofArea:F1} m\u00b2";
                    inputs[_localization.GetString("LabelEfficiencyPercent") ?? "Efficiency"] = $"{PanelEfficiency} %";
                    inputs[_localization.GetString("LabelOrientation") ?? "Orientation"] = Orientations[SelectedOrientation];
                    results[_localization.GetString("ResultPeakPower") ?? "Peak power"] = $"{SolarResult.KwPeak:F1} kWp";
                    results[_localization.GetString("ResultAnnualYield") ?? "Annual yield"] = $"{SolarResult.AnnualYieldKwh:F0} kWh";
                    if (SolarSystemCost > 0 && SolarResult.AnnualYieldKwh > 0 && PricePerKwh > 0)
                    {
                        var paybackYears = SolarSystemCost / (SolarResult.AnnualYieldKwh * PricePerKwh);
                        results[_localization.GetString("ResultPaybackTime") ?? "Payback"] = $"{paybackYears:F1} {_localization.GetString("HistoryYears") ?? "years"}";
                    }
                    break;
                default:
                    return;
            }

            var path = await _exportService.ExportToPdfAsync(calcType, inputs, results);
            await _fileShareService.ShareFileAsync(path, _localization.GetString("ShareMaterialList") ?? "Share", "application/pdf");
            MessageRequested?.Invoke(_localization.GetString("Success") ?? "Success", _localization.GetString("PdfExportSuccess") ?? "PDF exported!");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Error", _localization.GetString("PdfExportFailed") ?? "Export failed.");
        }
        finally
        {
            IsExporting = false;
        }
    }
}
