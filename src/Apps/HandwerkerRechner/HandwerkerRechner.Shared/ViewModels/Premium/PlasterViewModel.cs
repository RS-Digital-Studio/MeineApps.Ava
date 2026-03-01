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
/// ViewModel für den Putz-Rechner (Wandfläche, Dicke, Putzart → Säcke)
/// </summary>
public partial class PlasterViewModel : ObservableObject, IDisposable
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

    public PlasterViewModel(
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
    [ObservableProperty] private double _wallArea = 20.0;        // Wandfläche in m²
    [ObservableProperty] private double _thicknessMm = 15.0;     // Putzdicke in mm
    [ObservableProperty] private int _selectedPlasterType;        // 0=Innen, 1=Außen, 2=Kalk, 3=Gips

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnWallAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnThicknessMmChanged(double value) => ScheduleAutoCalculate();
    partial void OnSelectedPlasterTypeChanged(int value) => ScheduleAutoCalculate();

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcPlaster") ?? "Plaster";

    // Kostenberechnung
    [ObservableProperty] private double _pricePerBag;
    [ObservableProperty] private bool _showCost;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerBag > 0)
        ? $"{(Result.BagsNeeded * PricePerBag):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerBagChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        ScheduleAutoCalculate();
    }

    // --- Ergebnis-Properties ---
    [ObservableProperty] private PlasterResult? _result;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _isCalculating;
    [ObservableProperty] private bool _isExporting;

    public string PlasterTypeDisplay => SelectedPlasterType switch
    {
        0 => _localization.GetString("PlasterTypeInterior") ?? "Interior plaster",
        1 => _localization.GetString("PlasterTypeExterior") ?? "Exterior plaster",
        2 => _localization.GetString("PlasterTypeLime") ?? "Lime plaster",
        3 => _localization.GetString("PlasterTypeGypsum") ?? "Gypsum plaster",
        _ => ""
    };

    public string PlasterKgDisplay => Result != null ? $"{Result.PlasterKg:F1} kg" : "";
    public string BagsNeededDisplay => Result != null
        ? $"{Result.BagsNeeded} {_localization.GetString("UnitBags") ?? "Bags"}" : "";
    public string AreaDisplay => Result != null ? $"{Result.Area:F1} m\u00b2" : "";
    public string ThicknessDisplay => Result != null ? $"{Result.ThicknessMm:F0} mm" : "";

    partial void OnResultChanged(PlasterResult? value)
    {
        OnPropertyChanged(nameof(PlasterKgDisplay));
        OnPropertyChanged(nameof(BagsNeededDisplay));
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(ThicknessDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(PlasterTypeDisplay));
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

            if (WallArea <= 0 || ThicknessMm <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            string plasterType = SelectedPlasterType switch
            {
                1 => "Au\u00dfen", 2 => "Kalk", 3 => "Gips", _ => "Innen"
            };

            Result = _engine.CalculatePlaster(WallArea, ThicknessMm, plasterType);
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
            var title = $"{WallArea:F1} m\u00b2, {ThicknessMm:F0} mm {PlasterTypeDisplay}";
            var data = new Dictionary<string, object>
            {
                ["WallArea"] = WallArea,
                ["ThicknessMm"] = ThicknessMm,
                ["SelectedPlasterType"] = SelectedPlasterType,
                ["PricePerBag"] = PricePerBag,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["PlasterKg"] = Result.PlasterKg,
                    ["BagsNeeded"] = Result.BagsNeeded,
                    ["Area"] = Result.Area
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("PlasterCalculator", title, data);
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

        WallArea = 20.0;
        ThicknessMm = 15.0;
        SelectedPlasterType = 0;
        PricePerBag = 0;
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
                CalculatorType = CalculatorType.Plaster
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["WallArea"] = WallArea,
                ["ThicknessMm"] = ThicknessMm,
                ["SelectedPlasterType"] = SelectedPlasterType,
                ["PricePerBag"] = PricePerBag
            };

            // Result-Daten mitspeichern
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["PlasterKg"] = $"{Result.PlasterKg:F1}",
                    ["BagsNeeded"] = Result.BagsNeeded
                };
                if (ShowCost && PricePerBag > 0)
                    resultData["TotalCost"] = $"{(Result.BagsNeeded * PricePerBag):F2}";
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

            WallArea = project.GetValue("WallArea", 20.0);
            ThicknessMm = project.GetValue("ThicknessMm", 15.0);
            SelectedPlasterType = (int)project.GetValue("SelectedPlasterType", 0.0);
            PricePerBag = project.GetValue("PricePerBag", 0.0);

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

        var text = $"{_localization.GetString("CalcPlaster") ?? "Plaster"}\n" +
                   $"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n" +
                   $"{_localization.GetString("WallArea") ?? "Wall area"}: {AreaDisplay}\n" +
                   $"{_localization.GetString("PlasterThickness") ?? "Thickness"}: {ThicknessDisplay}\n" +
                   $"{_localization.GetString("PlasterType") ?? "Type"}: {PlasterTypeDisplay}\n" +
                   $"{_localization.GetString("PlasterAmount") ?? "Amount"}: {PlasterKgDisplay}\n" +
                   $"{_localization.GetString("ResultBagsNeeded") ?? "Bags"}: {BagsNeededDisplay}";

        if (ShowCost && PricePerBag > 0)
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

            var calcType = _localization.GetString("CalcPlaster") ?? "Plaster";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("WallArea") ?? "Wall area"] = $"{WallArea:F1} m\u00b2",
                [_localization.GetString("PlasterThickness") ?? "Thickness"] = $"{ThicknessMm:F0} mm",
                [_localization.GetString("PlasterType") ?? "Type"] = PlasterTypeDisplay
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("PlasterAmount") ?? "Amount"] = PlasterKgDisplay,
                [_localization.GetString("ResultBagsNeeded") ?? "Bags"] = BagsNeededDisplay
            };
            if (ShowCost && PricePerBag > 0)
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
