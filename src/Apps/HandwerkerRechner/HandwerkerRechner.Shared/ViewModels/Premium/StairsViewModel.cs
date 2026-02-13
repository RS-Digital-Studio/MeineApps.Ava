using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels.Premium;

/// <summary>
/// Treppen-Rechner ViewModel (Premium) - Berechnet Treppenmaße nach DIN 18065
/// </summary>
public partial class StairsViewModel : ObservableObject
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

    public StairsViewModel(
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
    /// Lädt Projektdaten anhand der ID (ersetzt IQueryAttributable)
    /// </summary>
    public async Task LoadFromProjectIdAsync(string projectId)
    {
        if (!string.IsNullOrEmpty(projectId))
        {
            _currentProjectId = projectId;
            await LoadProjectAsync(projectId);
        }
    }

    // Speicher-Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcStairs");

    // Eingabewerte
    [ObservableProperty] private double _floorHeight = 260;
    [ObservableProperty] private double _stairWidth = 100;
    [ObservableProperty] private int _customStepCount = 0;

    #region Kostenberechnung

    // Preis pro Stufe
    [ObservableProperty]
    private double _pricePerStep = 0;

    [ObservableProperty]
    private bool _showCost = false;

    /// <summary>
    /// Gesamtkosten-Anzeige: Stufenanzahl * Preis pro Stufe
    /// </summary>
    public string TotalCostDisplay => (ShowCost && PricePerStep > 0 && StairsResult != null && StairsResult.StepCount > 0)
        ? $"{_localization.GetString("TotalCost")}: {(StairsResult.StepCount * PricePerStep):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerStepChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

    // Ergebnis
    [ObservableProperty] private StairsResult? _stairsResult;
    [ObservableProperty] private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    partial void OnStairsResultChanged(StairsResult? value)
    {
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(StepCountDisplay));
        OnPropertyChanged(nameof(StepHeightDisplay));
        OnPropertyChanged(nameof(TreadDepthDisplay));
        OnPropertyChanged(nameof(RunLengthDisplay));
        OnPropertyChanged(nameof(AngleDisplay));
        OnPropertyChanged(nameof(StepMeasureDisplay));
        OnPropertyChanged(nameof(StairLengthDisplay));
        OnPropertyChanged(nameof(DinStatusDisplay));
        OnPropertyChanged(nameof(ComfortStatusDisplay));
    }

    #region Ergebnis-Anzeige Properties

    /// <summary>Stufenanzahl, z.B. "15 Stufen"</summary>
    public string StepCountDisplay => StairsResult != null
        ? $"{StairsResult.StepCount} {_localization.GetString("StepsUnit")}"
        : "";

    /// <summary>Stufenhöhe, z.B. "17.3 cm"</summary>
    public string StepHeightDisplay => StairsResult != null
        ? $"{StairsResult.StepHeight:F1} cm"
        : "";

    /// <summary>Auftrittstiefe, z.B. "28.4 cm"</summary>
    public string TreadDepthDisplay => StairsResult != null
        ? $"{StairsResult.TreadDepth:F1} cm"
        : "";

    /// <summary>Lauflänge in Metern, z.B. "3.98 m"</summary>
    public string RunLengthDisplay => StairsResult != null
        ? $"{StairsResult.RunLength / 100:F2} m"
        : "";

    /// <summary>Steigungswinkel, z.B. "31.4°"</summary>
    public string AngleDisplay => StairsResult != null
        ? $"{StairsResult.Angle:F1}\u00b0"
        : "";

    /// <summary>Schrittmaß (2h+g), z.B. "63.0 cm"</summary>
    public string StepMeasureDisplay => StairsResult != null
        ? $"{StairsResult.StepMeasure:F1} cm"
        : "";

    /// <summary>Treppenlänge (Hypotenuse) in Metern, z.B. "4.85 m"</summary>
    public string StairLengthDisplay => StairsResult != null
        ? $"{StairsResult.StairLength / 100:F2} m"
        : "";

    /// <summary>DIN-Konformitäts-Status</summary>
    public string DinStatusDisplay => StairsResult != null
        ? (StairsResult.IsDinCompliant
            ? $"{_localization.GetString("DinCompliant")}"
            : $"{_localization.GetString("NotDinCompliant")}")
        : "";

    /// <summary>Komfort-Status basierend auf Schrittmaß</summary>
    public string ComfortStatusDisplay => StairsResult != null
        ? (StairsResult.IsComfortable
            ? $"{_localization.GetString("ComfortableStairs")}"
            : $"{_localization.GetString("UncomfortableStairs")}")
        : "";

    #endregion

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            // Validierung
            if (FloorHeight <= 0 || StairWidth <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            if (CustomStepCount < 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            // Engine-Berechnung
            StairsResult = _engine.CalculateStairs(FloorHeight, StairWidth, CustomStepCount);
            HasResult = true;

            // In Verlauf speichern
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
            var calcType = "StairsCalculator";
            var title = $"{FloorHeight}cm, {StairsResult?.StepCount ?? 0} {_localization.GetString("StepsUnit")}";
            var data = new Dictionary<string, object>
            {
                ["FloorHeight"] = FloorHeight,
                ["StairWidth"] = StairWidth,
                ["CustomStepCount"] = CustomStepCount,
                ["Result"] = StairsResult != null ? new Dictionary<string, object>
                {
                    ["StepCount"] = StairsResult.StepCount,
                    ["StepHeight"] = StairsResult.StepHeight,
                    ["TreadDepth"] = StairsResult.TreadDepth,
                    ["RunLength"] = StairsResult.RunLength,
                    ["Angle"] = StairsResult.Angle,
                    ["StepMeasure"] = StairsResult.StepMeasure,
                    ["StairLength"] = StairsResult.StairLength,
                    ["IsComfortable"] = StairsResult.IsComfortable,
                    ["IsDinCompliant"] = StairsResult.IsDinCompliant
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync(calcType, title, data);
        }
        catch (Exception)
        {
            // Verlauf-Fehler stillschweigend ignorieren
        }
    }

    [RelayCommand]
    private void Reset()
    {
        FloorHeight = 260;
        StairWidth = 100;
        CustomStepCount = 0;
        PricePerStep = 0;
        StairsResult = null;
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
            var project = new Project
            {
                Name = name,
                CalculatorType = CalculatorType.Stairs
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["FloorHeight"] = FloorHeight,
                ["StairWidth"] = StairWidth,
                ["CustomStepCount"] = CustomStepCount,
                ["PricePerStep"] = PricePerStep
            };

            // Result-Daten mitspeichern
            if (HasResult && StairsResult != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["ResultStepCount"] = StairsResult.StepCount,
                    ["ResultStepHeight"] = $"{StairsResult.StepHeight:F1} cm",
                    ["ResultTreadDepth"] = $"{StairsResult.TreadDepth:F1} cm",
                    ["ResultRunLength"] = $"{StairsResult.RunLength / 100:F2} m",
                    ["ResultAngle"] = $"{StairsResult.Angle:F1}\u00b0",
                    ["ResultStepMeasure"] = $"{StairsResult.StepMeasure:F1} cm",
                    ["ResultStairLength"] = $"{StairsResult.StairLength / 100:F2} m",
                    ["DinCompliant"] = StairsResult.IsDinCompliant
                        ? (_localization.GetString("DinCompliant") ?? "DIN 18065 \u2713")
                        : (_localization.GetString("NotDinCompliant") ?? "Nicht DIN-konform")
                };
                if (PricePerStep > 0)
                    resultData["TotalCost"] = $"{(StairsResult.StepCount * PricePerStep):F2}";
                data["Result"] = resultData;
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

            FloorHeight = project.GetValue("FloorHeight", 260.0);
            StairWidth = project.GetValue("StairWidth", 100.0);
            CustomStepCount = project.GetValue("CustomStepCount", 0);
            PricePerStep = project.GetValue("PricePerStep", 0.0);

            await Calculate();
        }
        catch (Exception)
        {
            // Lade-Fehler stillschweigend ignorieren
        }
    }

    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult || StairsResult == null) return;

        var text = $"{_localization.GetString("CalcStairs") ?? "Stairs"}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("ResultStepCount") ?? "Steps"}: {StepCountDisplay}\n" +
                   $"{_localization.GetString("ResultStepHeight") ?? "Step Height"}: {StepHeightDisplay}\n" +
                   $"{_localization.GetString("ResultTreadDepth") ?? "Tread"}: {TreadDepthDisplay}\n" +
                   $"{_localization.GetString("ResultRunLength") ?? "Run"}: {RunLengthDisplay}\n" +
                   $"{_localization.GetString("ResultAngle") ?? "Angle"}: {AngleDisplay}\n" +
                   $"{_localization.GetString("ResultStepMeasure") ?? "Step Measure"}: {StepMeasureDisplay}\n" +
                   $"{_localization.GetString("ResultStairLength") ?? "Length"}: {StairLengthDisplay}\n" +
                   $"DIN 18065: {DinStatusDisplay}\n" +
                   $"{_localization.GetString("ComfortLevel") ?? "Comfort"}: {ComfortStatusDisplay}";

        if (ShowCost && PricePerStep > 0)
            text += $"\n{_localization.GetString("TotalCost") ?? "Cost"}: {TotalCostDisplay}";

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

            var calcType = _localization.GetString("CalcStairs");
            var inputs = new Dictionary<string, string>();
            var results = new Dictionary<string, string>();

            if (StairsResult != null)
            {
                inputs[_localization.GetString("FloorHeight") ?? "Geschosshöhe"] = $"{FloorHeight:F1} cm";
                inputs[_localization.GetString("StairWidth") ?? "Treppenbreite"] = $"{StairWidth:F1} cm";
                if (CustomStepCount > 0)
                    inputs[_localization.GetString("CustomStepCount") ?? "Stufenanzahl"] = $"{CustomStepCount}";

                results[_localization.GetString("ResultStepCount") ?? "Stufen"] = $"{StairsResult.StepCount}";
                results[_localization.GetString("ResultStepHeight") ?? "Stufenhöhe"] = $"{StairsResult.StepHeight:F1} cm";
                results[_localization.GetString("ResultTreadDepth") ?? "Auftritt"] = $"{StairsResult.TreadDepth:F1} cm";
                results[_localization.GetString("ResultRunLength") ?? "Lauflänge"] = $"{StairsResult.RunLength / 100:F2} m";
                results[_localization.GetString("ResultAngle") ?? "Steigungswinkel"] = $"{StairsResult.Angle:F1}\u00b0";
                results[_localization.GetString("ResultStepMeasure") ?? "Schrittmaß"] = $"{StairsResult.StepMeasure:F1} cm";
                results[_localization.GetString("ResultStairLength") ?? "Treppenlänge"] = $"{StairsResult.StairLength / 100:F2} m";
                results[_localization.GetString("DinCompliant") ?? "DIN 18065"] = StairsResult.IsDinCompliant
                    ? _localization.GetString("DinCompliant") ?? "DIN 18065 \u2713"
                    : _localization.GetString("NotDinCompliant") ?? "Nicht DIN-konform";

                if (PricePerStep > 0)
                    results[_localization.GetString("TotalCost") ?? "Gesamtkosten"] = $"{StairsResult.StepCount * PricePerStep:F2} {_localization.GetString("CurrencySymbol")}";
            }
            else
            {
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
