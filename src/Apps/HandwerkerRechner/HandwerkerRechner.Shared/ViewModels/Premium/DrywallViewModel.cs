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

public partial class DrywallViewModel : ObservableObject, IDisposable
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

    public DrywallViewModel(
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

    // Inputs
    [ObservableProperty] private double _wallLength = 3.0;
    [ObservableProperty] private double _wallHeight = 2.5;
    [ObservableProperty] private bool _doublePlated;

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnWallLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnWallHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnDoublePlatedChanged(bool value) => ScheduleAutoCalculate();

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CategoryDrywall");

    // Cost Calculation
    [ObservableProperty] private double _pricePerSqm = 0;
    [ObservableProperty] private bool _showCost = false;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerSqm > 0)
        ? $"{(Result.WallArea * PricePerSqm):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerSqmChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        ScheduleAutoCalculate();
    }

    // Results
    [ObservableProperty] private DrywallResult? _result;
    [ObservableProperty] private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    public string PlatesNeeded => Result != null ? $"{Result.Plates} {_localization.GetString("UnitPlates")}" : "";
    public string CwProfilesNeeded => Result != null ? $"{Result.CwProfiles} {_localization.GetString("UnitPieces")}" : "";
    public string UwLengthNeeded => Result != null ? $"{Result.UwLengthMeters:F1} m" : "";
    public string ScrewsNeeded => Result != null ? $"{Result.Screws} {_localization.GetString("UnitPieces")}" : "";

    partial void OnResultChanged(DrywallResult? value)
    {
        OnPropertyChanged(nameof(PlatesNeeded));
        OnPropertyChanged(nameof(CwProfilesNeeded));
        OnPropertyChanged(nameof(UwLengthNeeded));
        OnPropertyChanged(nameof(ScrewsNeeded));
        OnPropertyChanged(nameof(TotalCostDisplay));
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

            if (WallLength <= 0 || WallHeight <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            Result = _engine.CalculateDrywall(WallLength, WallHeight, DoublePlated);
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
            var title = $"{WallLength:F1} \u00d7 {WallHeight:F1} m{(DoublePlated ? $" ({_localization.GetString("HistoryDoubleLayered")})" : "")}";
            var data = new Dictionary<string, object>
            {
                ["WallLength"] = WallLength,
                ["WallHeight"] = WallHeight,
                ["DoublePlated"] = DoublePlated,
                ["PricePerSqm"] = PricePerSqm,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["WallArea"] = Result.WallArea,
                    ["Plates"] = Result.Plates,
                    ["CwProfiles"] = Result.CwProfiles,
                    ["UwLengthMeters"] = Result.UwLengthMeters,
                    ["Screws"] = Result.Screws
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("DrywallCalculator", title, data);
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

        WallLength = 3.0;
        WallHeight = 2.5;
        DoublePlated = false;
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
                CalculatorType = CalculatorType.DrywallFraming
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["WallLength"] = WallLength,
                ["WallHeight"] = WallHeight,
                ["DoublePlated"] = DoublePlated,
                ["PricePerSqm"] = PricePerSqm
            };

            // Result-Daten mitspeichern
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["DrywallSheets"] = Result.Plates,
                    ["CWProfilesStuds"] = Result.CwProfiles,
                    ["UWProfilesTopBottom"] = $"{Result.UwLengthMeters:F1} m",
                    ["Screws"] = Result.Screws
                };
                if (ShowCost && PricePerSqm > 0)
                    resultData["TotalCost"] = $"{(Result.WallArea * PricePerSqm):F2}";
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

            WallLength = project.GetValue("WallLength", 3.0);
            WallHeight = project.GetValue("WallHeight", 2.5);
            DoublePlated = project.GetValue("DoublePlated", false);
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

        var text = $"{_localization.GetString("CategoryDrywall")}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("DrywallSheets")}: {PlatesNeeded}\n" +
                   $"{_localization.GetString("CWProfilesStuds")}: {CwProfilesNeeded}\n" +
                   $"{_localization.GetString("UWProfilesTopBottom")}: {UwLengthNeeded}\n" +
                   $"{_localization.GetString("Screws")}: {ScrewsNeeded}";

        if (ShowCost && PricePerSqm > 0)
            text += $"\n{_localization.GetString("TotalCost")}: {TotalCostDisplay}";

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

            var calcType = _localization.GetString("CategoryDrywall") ?? "Drywall";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("WallLength") ?? "Wall length"] = $"{WallLength:F1} m",
                [_localization.GetString("RoomHeight") ?? "Height"] = $"{WallHeight:F1} m",
                [_localization.GetString("DoublePlated") ?? "Double layered"] = DoublePlated
                    ? (_localization.GetString("Yes") ?? "Yes")
                    : (_localization.GetString("No") ?? "No")
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("DrywallSheets") ?? "Sheets"] = PlatesNeeded,
                [_localization.GetString("CWProfilesStuds") ?? "CW profiles"] = CwProfilesNeeded,
                [_localization.GetString("UWProfilesTopBottom") ?? "UW profiles"] = UwLengthNeeded,
                [_localization.GetString("Screws") ?? "Screws"] = ScrewsNeeded
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
