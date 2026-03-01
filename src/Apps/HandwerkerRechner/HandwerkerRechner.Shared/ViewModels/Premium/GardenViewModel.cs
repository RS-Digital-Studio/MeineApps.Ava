using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels.Premium;

public partial class GardenViewModel : ObservableObject, IDisposable
{
    private readonly CraftEngine _engine;
    private Timer? _debounceTimer;
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

    public GardenViewModel(
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
            case 0: // Paving - reset other costs
                PricePerBag = 0;
                PricePerSqmLiner = 0;
                break;
            case 1: // Soil - reset other costs
                PricePerStone = 0;
                PricePerSqmLiner = 0;
                break;
            case 2: // Pond - reset other costs
                PricePerStone = 0;
                PricePerBag = 0;
                break;
        }
        // Clear results when switching
        HasResult = false;
    }

    private List<string>? _calculators;
    public List<string> Calculators => _calculators ??= [
        _localization.GetString("Paving"),
        _localization.GetString("Soil"),
        _localization.GetString("PondLiner")
    ];

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnPavingAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnStoneLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnStoneWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnJointWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnSoilAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnSoilDepthChanged(double value) => ScheduleAutoCalculate();
    partial void OnBagLitersChanged(double value) => ScheduleAutoCalculate();
    partial void OnPondLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnPondWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnPondDepthChanged(double value) => ScheduleAutoCalculate();
    partial void OnOverlapChanged(double value) => ScheduleAutoCalculate();

    // Paving Inputs
    [ObservableProperty] private double _pavingArea = 20;
    [ObservableProperty] private double _stoneLength = 20;
    [ObservableProperty] private double _stoneWidth = 10;
    [ObservableProperty] private double _jointWidth = 3;

    // Soil Inputs
    [ObservableProperty] private double _soilArea = 10;
    [ObservableProperty] private double _soilDepth = 5;
    [ObservableProperty] private double _bagLiters = 40;

    // Pond Liner Inputs
    [ObservableProperty] private double _pondLength = 3;
    [ObservableProperty] private double _pondWidth = 2;
    [ObservableProperty] private double _pondDepth = 1;
    [ObservableProperty] private double _overlap = 0.5;

    #region Cost Calculation

    // Pflastersteine: Preis pro Stein
    [ObservableProperty]
    private double _pricePerStone = 0;

    [ObservableProperty]
    private bool _showPavingCost = false;

    public string PavingCostDisplay => (ShowPavingCost && PricePerStone > 0 && PavingResult != null && PavingResult.StonesWithReserve > 0)
        ? $"{_localization.GetString("TotalCost")}: {(PavingResult.StonesWithReserve * PricePerStone):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerStoneChanged(double value)
    {
        ShowPavingCost = value > 0;
        OnPropertyChanged(nameof(PavingCostDisplay));
        ScheduleAutoCalculate();
    }

    // Erde/Mulch: Preis pro Sack
    [ObservableProperty]
    private double _pricePerBag = 0;

    [ObservableProperty]
    private bool _showSoilCost = false;

    public string SoilCostDisplay => (ShowSoilCost && PricePerBag > 0 && SoilResult != null && SoilResult.BagsNeeded > 0)
        ? $"{_localization.GetString("TotalCost")}: {(SoilResult.BagsNeeded * PricePerBag):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerBagChanged(double value)
    {
        ShowSoilCost = value > 0;
        OnPropertyChanged(nameof(SoilCostDisplay));
        ScheduleAutoCalculate();
    }

    // Teichfolie: Preis pro m²
    [ObservableProperty]
    private double _pricePerSqmLiner = 0;

    [ObservableProperty]
    private bool _showLinerCost = false;

    public string LinerCostDisplay => (ShowLinerCost && PricePerSqmLiner > 0 && PondResult != null && PondResult.LinerArea > 0)
        ? $"{_localization.GetString("TotalCost")}: {(PondResult.LinerArea * PricePerSqmLiner):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerSqmLinerChanged(double value)
    {
        ShowLinerCost = value > 0;
        OnPropertyChanged(nameof(LinerCostDisplay));
        ScheduleAutoCalculate();
    }

    #endregion

    // Results
    [ObservableProperty] private PavingResult? _pavingResult;
    [ObservableProperty] private SoilResult? _soilResult;
    [ObservableProperty] private PondLinerResult? _pondResult;
    [ObservableProperty] private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    partial void OnPavingResultChanged(PavingResult? value)
    {
        OnPropertyChanged(nameof(PavingCostDisplay));
    }

    partial void OnSoilResultChanged(SoilResult? value)
    {
        OnPropertyChanged(nameof(SoilCostDisplay));
    }

    partial void OnPondResultChanged(PondLinerResult? value)
    {
        OnPropertyChanged(nameof(LinerCostDisplay));
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

            switch (SelectedCalculator)
            {
                case 0: // Paving
                    if (PavingArea <= 0 || StoneLength <= 0 || StoneWidth <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    // Negative Fugenbreite ergibt keinen Sinn und kann Division/0 verursachen
                    if (JointWidth < 0) JointWidth = 0;
                    PavingResult = _engine.CalculatePaving(PavingArea, StoneLength, StoneWidth, JointWidth);
                    SoilResult = null;
                    PondResult = null;
                    break;

                case 1: // Soil
                    if (SoilArea <= 0 || SoilDepth <= 0 || BagLiters <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    SoilResult = _engine.CalculateSoil(SoilArea, SoilDepth, BagLiters);
                    PavingResult = null;
                    PondResult = null;
                    break;

                case 2: // Pond Liner
                    if (PondLength <= 0 || PondWidth <= 0 || PondDepth <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    // Negativer Überstand ist nicht sinnvoll
                    if (Overlap < 0) Overlap = 0;
                    PondResult = _engine.CalculatePondLiner(PondLength, PondWidth, PondDepth, Overlap);
                    PavingResult = null;
                    SoilResult = null;
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
                case 0: // Paving
                    calcType = "PavingCalculator";
                    title = $"{PavingArea} m\u00b2, {StoneLength}\u00d7{StoneWidth} cm";
                    data = new Dictionary<string, object>
                    {
                        ["PavingArea"] = PavingArea,
                        ["StoneLength"] = StoneLength,
                        ["StoneWidth"] = StoneWidth,
                        ["JointWidth"] = JointWidth,
                        ["Result"] = PavingResult != null ? new Dictionary<string, object>
                        {
                            ["StonesNeeded"] = PavingResult.StonesNeeded,
                            ["StonesWithReserve"] = PavingResult.StonesWithReserve
                        } : new Dictionary<string, object>()
                    };
                    break;

                case 1: // Soil
                    calcType = "SoilCalculator";
                    title = string.Format(_localization.GetString("HistorySoilDepth"), SoilArea, SoilDepth);
                    data = new Dictionary<string, object>
                    {
                        ["SoilArea"] = SoilArea,
                        ["SoilDepth"] = SoilDepth,
                        ["BagLiters"] = BagLiters,
                        ["Result"] = SoilResult != null ? new Dictionary<string, object>
                        {
                            ["VolumeLiters"] = SoilResult.VolumeLiters,
                            ["BagsNeeded"] = SoilResult.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;

                default: // Pond Liner
                    calcType = "PondLinerCalculator";
                    title = $"{PondLength}\u00d7{PondWidth}\u00d7{PondDepth} m";
                    data = new Dictionary<string, object>
                    {
                        ["PondLength"] = PondLength,
                        ["PondWidth"] = PondWidth,
                        ["PondDepth"] = PondDepth,
                        ["Overlap"] = Overlap,
                        ["Result"] = PondResult != null ? new Dictionary<string, object>
                        {
                            ["LinerLength"] = PondResult.LinerLength,
                            ["LinerWidth"] = PondResult.LinerWidth,
                            ["LinerArea"] = PondResult.LinerArea
                        } : new Dictionary<string, object>()
                    };
                    break;
            }

            await _historyService.AddCalculationAsync(calcType, title, data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        PavingArea = 20;
        StoneLength = 20;
        StoneWidth = 10;
        JointWidth = 3;
        SoilArea = 10;
        SoilDepth = 5;
        BagLiters = 40;
        PondLength = 3;
        PondWidth = 2;
        PondDepth = 1;
        Overlap = 0.5;
        PricePerStone = 0;
        PricePerBag = 0;
        PricePerSqmLiner = 0;
        PavingResult = null;
        SoilResult = null;
        PondResult = null;
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
                0 => CalculatorType.Paving,
                1 => CalculatorType.Soil,
                2 => CalculatorType.PondLiner,
                _ => CalculatorType.Paving
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
                // Paving
                ["PavingArea"] = PavingArea,
                ["StoneLength"] = StoneLength,
                ["StoneWidth"] = StoneWidth,
                ["JointWidth"] = JointWidth,
                ["PricePerStone"] = PricePerStone,
                // Soil
                ["SoilArea"] = SoilArea,
                ["SoilDepth"] = SoilDepth,
                ["BagLiters"] = BagLiters,
                ["PricePerBag"] = PricePerBag,
                // Pond Liner
                ["PondLength"] = PondLength,
                ["PondWidth"] = PondWidth,
                ["PondDepth"] = PondDepth,
                ["Overlap"] = Overlap,
                ["PricePerSqmLiner"] = PricePerSqmLiner
            };

            // Result-Daten mitspeichern (je nach aktuellem Sub-Rechner)
            if (HasResult)
            {
                switch (SelectedCalculator)
                {
                    case 0 when PavingResult != null:
                    {
                        var resultData = new Dictionary<string, object>
                        {
                            ["ResultStonesNeeded"] = PavingResult.StonesNeeded,
                            ["ResultWithReserveFivePercent"] = PavingResult.StonesWithReserve
                        };
                        if (PricePerStone > 0)
                            resultData["TotalCost"] = $"{(PavingResult.StonesWithReserve * PricePerStone):F2}";
                        data["Result"] = resultData;
                        break;
                    }
                    case 1 when SoilResult != null:
                    {
                        var resultData = new Dictionary<string, object>
                        {
                            ["ResultVolumeNeeded"] = $"{SoilResult.VolumeLiters:F1} L",
                            ["ResultBagsNeeded"] = SoilResult.BagsNeeded
                        };
                        if (PricePerBag > 0)
                            resultData["TotalCost"] = $"{(SoilResult.BagsNeeded * PricePerBag):F2}";
                        data["Result"] = resultData;
                        break;
                    }
                    case 2 when PondResult != null:
                    {
                        var resultData = new Dictionary<string, object>
                        {
                            ["ResultLinerLength"] = $"{PondResult.LinerLength:F2} m",
                            ["ResultLinerWidth"] = $"{PondResult.LinerWidth:F2} m",
                            ["ResultLinerArea"] = $"{PondResult.LinerArea:F2} m\u00b2"
                        };
                        if (PricePerSqmLiner > 0)
                            resultData["TotalCost"] = $"{(PondResult.LinerArea * PricePerSqmLiner):F2}";
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

            // Paving
            PavingArea = project.GetValue("PavingArea", 20.0);
            StoneLength = project.GetValue("StoneLength", 20.0);
            StoneWidth = project.GetValue("StoneWidth", 10.0);
            JointWidth = project.GetValue("JointWidth", 3.0);
            PricePerStone = project.GetValue("PricePerStone", 0.0);

            // Soil
            SoilArea = project.GetValue("SoilArea", 10.0);
            SoilDepth = project.GetValue("SoilDepth", 5.0);
            BagLiters = project.GetValue("BagLiters", 40.0);
            PricePerBag = project.GetValue("PricePerBag", 0.0);

            // Pond Liner
            PondLength = project.GetValue("PondLength", 3.0);
            PondWidth = project.GetValue("PondWidth", 2.0);
            PondDepth = project.GetValue("PondDepth", 1.0);
            Overlap = project.GetValue("Overlap", 0.5);
            PricePerSqmLiner = project.GetValue("PricePerSqmLiner", 0.0);

            await Calculate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult) return;

        string text;
        switch (SelectedCalculator)
        {
            case 0 when PavingResult != null:
                text = $"{Calculators[0]}\n" +
                       $"─────────────\n" +
                       $"{_localization.GetString("ResultStonesNeeded")}: {PavingResult.StonesNeeded}\n" +
                       $"{_localization.GetString("ResultWithReserveFivePercent")}: {PavingResult.StonesWithReserve}";
                if (ShowPavingCost && PricePerStone > 0)
                    text += $"\n{_localization.GetString("TotalCost")}: {PavingCostDisplay}";
                break;

            case 1 when SoilResult != null:
                text = $"{Calculators[1]}\n" +
                       $"─────────────\n" +
                       $"{_localization.GetString("ResultVolumeNeeded")}: {SoilResult.VolumeLiters:F1} L\n" +
                       $"{_localization.GetString("ResultBagsNeeded")}: {SoilResult.BagsNeeded}";
                if (ShowSoilCost && PricePerBag > 0)
                    text += $"\n{_localization.GetString("TotalCost")}: {SoilCostDisplay}";
                break;

            case 2 when PondResult != null:
                text = $"{Calculators[2]}\n" +
                       $"─────────────\n" +
                       $"{_localization.GetString("ResultLinerLength")}: {PondResult.LinerLength:F2} m\n" +
                       $"{_localization.GetString("ResultLinerWidth")}: {PondResult.LinerWidth:F2} m\n" +
                       $"{_localization.GetString("ResultLinerArea")}: {PondResult.LinerArea:F2} m\u00b2";
                if (ShowLinerCost && PricePerSqmLiner > 0)
                    text += $"\n{_localization.GetString("TotalCost")}: {LinerCostDisplay}";
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
                case 0 when PavingResult != null:
                    inputs[_localization.GetString("LabelAreaSqm") ?? "Area"] = $"{PavingArea:F1} m\u00b2";
                    inputs[_localization.GetString("LabelStoneLengthCm") ?? "Stone length"] = $"{StoneLength} cm";
                    inputs[_localization.GetString("LabelStoneWidthCm") ?? "Stone width"] = $"{StoneWidth} cm";
                    results[_localization.GetString("ResultStonesNeeded") ?? "Stones"] = $"{PavingResult.StonesNeeded}";
                    results[_localization.GetString("ResultWithReserveFivePercent") ?? "+5%"] = $"{PavingResult.StonesWithReserve}";
                    if (PricePerStone > 0)
                        results[_localization.GetString("TotalCost") ?? "Total cost"] = $"{PavingResult.StonesWithReserve * PricePerStone:F2} {_localization.GetString("CurrencySymbol")}";
                    break;
                case 1 when SoilResult != null:
                    inputs[_localization.GetString("LabelAreaSqm") ?? "Area"] = $"{SoilArea:F1} m\u00b2";
                    inputs[_localization.GetString("LabelDepthCm") ?? "Depth"] = $"{SoilDepth} cm";
                    results[_localization.GetString("ResultVolumeNeeded") ?? "Volume"] = $"{SoilResult.VolumeLiters:F1} L";
                    results[_localization.GetString("ResultBagsNeeded") ?? "Bags"] = $"{SoilResult.BagsNeeded}";
                    if (PricePerBag > 0)
                        results[_localization.GetString("TotalCost") ?? "Total cost"] = $"{SoilResult.BagsNeeded * PricePerBag:F2} {_localization.GetString("CurrencySymbol")}";
                    break;
                case 2 when PondResult != null:
                    inputs[_localization.GetString("LabelLengthM") ?? "Length"] = $"{PondLength:F1} m";
                    inputs[_localization.GetString("LabelWidthM") ?? "Width"] = $"{PondWidth:F1} m";
                    inputs[_localization.GetString("LabelDepthM") ?? "Depth"] = $"{PondDepth:F1} m";
                    results[_localization.GetString("ResultLinerLength") ?? "Liner length"] = $"{PondResult.LinerLength:F2} m";
                    results[_localization.GetString("ResultLinerWidth") ?? "Liner width"] = $"{PondResult.LinerWidth:F2} m";
                    results[_localization.GetString("ResultLinerArea") ?? "Liner area"] = $"{PondResult.LinerArea:F2} m\u00b2";
                    if (PricePerSqmLiner > 0)
                        results[_localization.GetString("TotalCost") ?? "Total cost"] = $"{PondResult.LinerArea * PricePerSqmLiner:F2} {_localization.GetString("CurrencySymbol")}";
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

    /// <summary>
    /// Räumt Event-Subscriptions und Timer auf (wird von MainViewModel beim Navigieren aufgerufen)
    /// </summary>
    public void Cleanup() => _debounceTimer?.Dispose();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
