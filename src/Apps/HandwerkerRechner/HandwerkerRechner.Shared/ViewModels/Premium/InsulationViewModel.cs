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

/// <summary>
/// ViewModel für den Dämmung-Rechner (Fläche, U-Werte, Dämmstofftyp → Dicke, Platten, Kosten)
/// </summary>
public partial class InsulationViewModel : ObservableObject, IDisposable
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

    public InsulationViewModel(
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
    /// Lädt Projektdaten per ID (ersetzt IQueryAttributable)
    /// </summary>
    public async Task LoadFromProjectIdAsync(string projectId)
    {
        if (!string.IsNullOrEmpty(projectId))
        {
            _currentProjectId = projectId;
            await LoadProjectAsync(projectId);
        }
    }

    // --- Eingabe-Properties ---
    [ObservableProperty] private double _area = 50.0;                // Fläche in m²
    [ObservableProperty] private double _currentUValue = 1.5;        // Ist-U-Wert W/(m²·K)
    [ObservableProperty] private double _targetUValue = 0.24;        // Soll-U-Wert W/(m²·K)
    [ObservableProperty] private int _selectedInsulationType;         // 0=EPS, 1=XPS, 2=Mineralwolle, 3=Holzfaser

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnCurrentUValueChanged(double value) => ScheduleAutoCalculate();
    partial void OnTargetUValueChanged(double value) => ScheduleAutoCalculate();
    partial void OnSelectedInsulationTypeChanged(int value) => ScheduleAutoCalculate();

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcInsulation") ?? "Insulation";

    // Kostenberechnung (manueller Preis pro m²)
    [ObservableProperty] private double _pricePerSqm;
    [ObservableProperty] private bool _showCost;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerSqm > 0)
        ? $"{(Result.Area * PricePerSqm):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerSqmChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        ScheduleAutoCalculate();
    }

    // --- Ergebnis-Properties ---
    [ObservableProperty] private InsulationResult? _result;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _isCalculating;
    [ObservableProperty] private bool _isExporting;

    public string InsulationTypeDisplay => SelectedInsulationType switch
    {
        0 => _localization.GetString("InsulationTypeEPS") ?? "EPS (Polystyrene)",
        1 => _localization.GetString("InsulationTypeXPS") ?? "XPS (Extruded)",
        2 => _localization.GetString("InsulationTypeMineralWool") ?? "Mineral wool",
        3 => _localization.GetString("InsulationTypeWoodFiber") ?? "Wood fibre",
        _ => ""
    };

    public string ThicknessDisplay => Result != null ? $"{Result.ThicknessCm:F0} cm" : "";
    public string PiecesDisplay => Result != null
        ? $"{Result.PiecesNeeded} {_localization.GetString("UnitPieces") ?? "Pieces"}" : "";
    public string AreaDisplay => Result != null ? $"{Result.Area:F1} m\u00b2" : "";
    public string LambdaDisplay => Result != null ? $"\u03bb = {Result.Lambda:F3} W/(m\u00b7K)" : "";

    partial void OnResultChanged(InsulationResult? value)
    {
        OnPropertyChanged(nameof(ThicknessDisplay));
        OnPropertyChanged(nameof(PiecesDisplay));
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(LambdaDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(InsulationTypeDisplay));
    }

    // --- Debounce + Berechnung ---

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

            if (Area <= 0 || CurrentUValue <= 0 || TargetUValue <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            if (TargetUValue >= CurrentUValue)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("TargetUValueMustBeLess") ?? "Target U-Value must be less than current U-Value");
                return;
            }

            Result = _engine.CalculateInsulation(Area, CurrentUValue, TargetUValue, SelectedInsulationType);
            HasResult = true;

            // In History speichern
            await SaveToHistoryAsync();
        }
        finally
        {
            IsCalculating = false;
        }
    }

    // --- History ---

    private async Task SaveToHistoryAsync()
    {
        try
        {
            var title = $"{Area:F1} m\u00b2, {InsulationTypeDisplay}";
            var data = new Dictionary<string, object>
            {
                ["Area"] = Area,
                ["CurrentUValue"] = CurrentUValue,
                ["TargetUValue"] = TargetUValue,
                ["SelectedInsulationType"] = SelectedInsulationType,
                ["PricePerSqm"] = PricePerSqm,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["ThicknessCm"] = Result.ThicknessCm,
                    ["PiecesNeeded"] = Result.PiecesNeeded,
                    ["Lambda"] = Result.Lambda,
                    ["Area"] = Result.Area
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("InsulationCalculator", title, data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
        }
    }

    // --- Standard-Commands ---

    [RelayCommand]
    private void Reset()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        Area = 50.0;
        CurrentUValue = 1.5;
        TargetUValue = 0.24;
        SelectedInsulationType = 0;
        PricePerSqm = 0;
        Result = null;
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
                CalculatorType = CalculatorType.Insulation
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["Area"] = Area,
                ["CurrentUValue"] = CurrentUValue,
                ["TargetUValue"] = TargetUValue,
                ["SelectedInsulationType"] = SelectedInsulationType,
                ["PricePerSqm"] = PricePerSqm
            };

            // Result-Daten mitspeichern
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["ThicknessCm"] = $"{Result.ThicknessCm:F0}",
                    ["PiecesNeeded"] = Result.PiecesNeeded,
                    ["Lambda"] = $"{Result.Lambda:F3}"
                };
                if (ShowCost && PricePerSqm > 0)
                    resultData["TotalCost"] = $"{(Result.Area * PricePerSqm):F2}";
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

            Area = project.GetValue("Area", 50.0);
            CurrentUValue = project.GetValue("CurrentUValue", 1.5);
            TargetUValue = project.GetValue("TargetUValue", 0.24);
            SelectedInsulationType = (int)project.GetValue("SelectedInsulationType", 0.0);
            PricePerSqm = project.GetValue("PricePerSqm", 0.0);

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
        if (!HasResult || Result == null) return;

        var text = $"{_localization.GetString("CalcInsulation") ?? "Insulation"}\n" +
                   $"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n" +
                   $"{_localization.GetString("InsulationArea") ?? "Area"}: {AreaDisplay}\n" +
                   $"{_localization.GetString("CurrentUValue") ?? "Current U-Value"}: {CurrentUValue:F2} W/(m\u00b2\u00b7K)\n" +
                   $"{_localization.GetString("TargetUValue") ?? "Target U-Value"}: {TargetUValue:F2} W/(m\u00b2\u00b7K)\n" +
                   $"{_localization.GetString("InsulationType") ?? "Type"}: {InsulationTypeDisplay}\n" +
                   $"{_localization.GetString("InsulationThickness") ?? "Thickness"}: {ThicknessDisplay}\n" +
                   $"{_localization.GetString("InsulationLambda") ?? "Lambda"}: {LambdaDisplay}\n" +
                   $"{_localization.GetString("UnitPieces") ?? "Pieces"}: {PiecesDisplay}";

        if (ShowCost && PricePerSqm > 0)
            text += $"\n{_localization.GetString("TotalCost") ?? "Total cost"}: {TotalCostDisplay}";

        ClipboardRequested?.Invoke(text);
        FloatingTextRequested?.Invoke(_localization.GetString("CopiedToClipboard") ?? "Copied!", "success");
    }

    [RelayCommand]
    private async Task ExportMaterialList()
    {
        if (!HasResult || Result == null) return;
        if (IsExporting) return;

        try
        {
            IsExporting = true;

            if (!_purchaseService.IsPremium)
            {
                var adResult = await _rewardedAdService.ShowAdAsync("material_pdf");
                if (!adResult) return;
            }

            var calcType = _localization.GetString("CalcInsulation") ?? "Insulation";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("InsulationArea") ?? "Area"] = $"{Area:F1} m\u00b2",
                [_localization.GetString("CurrentUValue") ?? "Current U-Value"] = $"{CurrentUValue:F2} W/(m\u00b2\u00b7K)",
                [_localization.GetString("TargetUValue") ?? "Target U-Value"] = $"{TargetUValue:F2} W/(m\u00b2\u00b7K)",
                [_localization.GetString("InsulationType") ?? "Type"] = InsulationTypeDisplay
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("InsulationThickness") ?? "Thickness"] = ThicknessDisplay,
                [_localization.GetString("InsulationLambda") ?? "Lambda"] = LambdaDisplay,
                [_localization.GetString("UnitPieces") ?? "Pieces"] = PiecesDisplay
            };
            if (ShowCost && PricePerSqm > 0)
                results[_localization.GetString("TotalCost") ?? "Total cost"] = TotalCostDisplay;

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
