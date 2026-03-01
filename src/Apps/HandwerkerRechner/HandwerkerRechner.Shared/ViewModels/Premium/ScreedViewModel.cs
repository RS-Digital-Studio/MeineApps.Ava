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
/// ViewModel für den Estrich-Rechner (Fläche, Dicke, Typ → Volumen, Gewicht, Säcke, Trocknungszeit)
/// </summary>
public partial class ScreedViewModel : ObservableObject, IDisposable
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

    public ScreedViewModel(
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
    [ObservableProperty] private double _floorArea = 20.0;         // Bodenfläche in m²
    [ObservableProperty] private double _thicknessCm = 6.0;        // Estrichdicke in cm
    [ObservableProperty] private int _selectedScreedType;           // 0=Zement, 1=Fließ, 2=Anhydrit

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnFloorAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnThicknessCmChanged(double value) => ScheduleAutoCalculate();
    partial void OnSelectedScreedTypeChanged(int value) => ScheduleAutoCalculate();

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcScreed") ?? "Screed";

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
    [ObservableProperty] private ScreedResult? _result;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _isCalculating;
    [ObservableProperty] private bool _isExporting;

    public string ScreedTypeDisplay => SelectedScreedType switch
    {
        0 => _localization.GetString("ScreedTypeZement") ?? "Cement screed",
        1 => _localization.GetString("ScreedTypeFlow") ?? "Self-leveling screed",
        2 => _localization.GetString("ScreedTypeAnhydrit") ?? "Anhydrite screed",
        _ => ""
    };

    public string VolumeDisplay => Result != null ? $"{Result.VolumeM3:F3} m\u00b3" : "";
    public string WeightDisplay => Result != null ? $"{Result.WeightKg:F0} kg" : "";
    public string BagsNeededDisplay => Result != null
        ? $"{Result.BagsNeeded} {_localization.GetString("UnitBags") ?? "bags"}" : "";
    public string DryingTimeDisplay => Result != null
        ? $"{Result.DryingDays} {_localization.GetString("UnitDays") ?? "days"}" : "";
    public string AreaDisplay => Result != null ? $"{Result.Area:F1} m\u00b2" : "";
    public string ThicknessDisplay => Result != null ? $"{Result.ThicknessCm:F1} cm" : "";

    partial void OnResultChanged(ScreedResult? value)
    {
        OnPropertyChanged(nameof(VolumeDisplay));
        OnPropertyChanged(nameof(WeightDisplay));
        OnPropertyChanged(nameof(BagsNeededDisplay));
        OnPropertyChanged(nameof(DryingTimeDisplay));
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(ThicknessDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(ScreedTypeDisplay));
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

            if (FloorArea <= 0 || ThicknessCm <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            string screedType = SelectedScreedType switch
            {
                1 => "Flie\u00df", 2 => "Anhydrit", _ => "Zement"
            };

            Result = _engine.CalculateScreed(FloorArea, ThicknessCm, screedType);
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
            var title = $"{FloorArea:F1} m\u00b2, {ThicknessCm:F1} cm {ScreedTypeDisplay}";
            var data = new Dictionary<string, object>
            {
                ["FloorArea"] = FloorArea,
                ["ThicknessCm"] = ThicknessCm,
                ["SelectedScreedType"] = SelectedScreedType,
                ["PricePerBag"] = PricePerBag,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["VolumeM3"] = Result.VolumeM3,
                    ["WeightKg"] = Result.WeightKg,
                    ["BagsNeeded"] = Result.BagsNeeded,
                    ["DryingDays"] = Result.DryingDays
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("ScreedCalculator", title, data);
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

        FloorArea = 20.0;
        ThicknessCm = 6.0;
        SelectedScreedType = 0;
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
                CalculatorType = CalculatorType.Screed
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["FloorArea"] = FloorArea,
                ["ThicknessCm"] = ThicknessCm,
                ["SelectedScreedType"] = SelectedScreedType,
                ["PricePerBag"] = PricePerBag
            };

            // Result-Daten mitspeichern
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["VolumeM3"] = $"{Result.VolumeM3:F3}",
                    ["WeightKg"] = $"{Result.WeightKg:F0}",
                    ["BagsNeeded"] = Result.BagsNeeded,
                    ["DryingDays"] = Result.DryingDays
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

            FloorArea = project.GetValue("FloorArea", 20.0);
            ThicknessCm = project.GetValue("ThicknessCm", 6.0);
            SelectedScreedType = (int)project.GetValue("SelectedScreedType", 0.0);
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

        var text = $"{_localization.GetString("CalcScreed") ?? "Screed"}\n" +
                   $"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n" +
                   $"{_localization.GetString("FloorArea") ?? "Floor area"}: {AreaDisplay}\n" +
                   $"{_localization.GetString("ScreedThickness") ?? "Thickness"}: {ThicknessDisplay}\n" +
                   $"{_localization.GetString("ScreedType") ?? "Type"}: {ScreedTypeDisplay}\n" +
                   $"{_localization.GetString("ScreedVolume") ?? "Volume"}: {VolumeDisplay}\n" +
                   $"{_localization.GetString("ScreedWeight") ?? "Weight"}: {WeightDisplay}\n" +
                   $"{_localization.GetString("ResultBagsNeeded") ?? "Bags"}: {BagsNeededDisplay}\n" +
                   $"{_localization.GetString("DryingTime") ?? "Drying time"}: {DryingTimeDisplay}";

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

            var calcType = _localization.GetString("CalcScreed") ?? "Screed";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("FloorArea") ?? "Floor area"] = $"{FloorArea:F1} m\u00b2",
                [_localization.GetString("ScreedThickness") ?? "Thickness"] = $"{ThicknessCm:F1} cm",
                [_localization.GetString("ScreedType") ?? "Type"] = ScreedTypeDisplay
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("ScreedVolume") ?? "Volume"] = VolumeDisplay,
                [_localization.GetString("ScreedWeight") ?? "Weight"] = WeightDisplay,
                [_localization.GetString("ResultBagsNeeded") ?? "Bags"] = BagsNeededDisplay,
                [_localization.GetString("DryingTime") ?? "Drying time"] = DryingTimeDisplay
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
