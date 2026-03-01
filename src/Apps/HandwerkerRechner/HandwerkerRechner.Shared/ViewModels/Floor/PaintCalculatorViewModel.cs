using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels.Floor;

public partial class PaintCalculatorViewModel : ObservableObject, IDisposable
{
    private readonly CraftEngine _craftEngine;
    private Timer? _debounceTimer;
    private readonly IProjectService _projectService;
    private readonly ILocalizationService _localization;
    private readonly ICalculationHistoryService _historyService;
    private readonly IUnitConverterService _unitConverter;
    private readonly IMaterialExportService _exportService;
    private readonly IFileShareService _fileShareService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
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

    [ObservableProperty]
    private bool _showSaveDialog;

    [ObservableProperty]
    private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcPaint");

    /// <summary>
    /// Invoke navigation request
    /// </summary>
    private void NavigateTo(string route)
    {
        NavigationRequested?.Invoke(route);
    }

    public PaintCalculatorViewModel(
        CraftEngine craftEngine,
        IProjectService projectService,
        ILocalizationService localization,
        ICalculationHistoryService historyService,
        IUnitConverterService unitConverter,
        IMaterialExportService exportService,
        IFileShareService fileShareService,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService)
    {
        _craftEngine = craftEngine;
        _projectService = projectService;
        _localization = localization;
        _historyService = historyService;
        _unitConverter = unitConverter;
        _exportService = exportService;
        _fileShareService = fileShareService;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;

        _unitConverter.UnitSystemChanged += OnUnitSystemChanged;
    }

    /// <summary>
    /// Load project data from a project ID (replaces IQueryAttributable)
    /// </summary>
    public async Task LoadFromProjectIdAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
            return;

        _currentProjectId = projectId;
        try
        {
            await LoadProjectAsync(projectId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
        }
    }

    #region Input Properties

    [ObservableProperty]
    private double _area = 20.0;

    [ObservableProperty]
    private double _coveragePerLiter = 10.0;

    [ObservableProperty]
    private int _numberOfCoats = 2;

    // Tür-/Fenster-Abzüge (optional)
    [ObservableProperty] private bool _showDeductions;
    [ObservableProperty] private int _doorCount;
    [ObservableProperty] private double _doorWidth = 0.8;
    [ObservableProperty] private double _doorHeight = 2.0;
    [ObservableProperty] private int _windowCount;
    [ObservableProperty] private double _windowWidth = 1.2;
    [ObservableProperty] private double _windowHeight = 1.0;

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnCoveragePerLiterChanged(double value) => ScheduleAutoCalculate();
    partial void OnNumberOfCoatsChanged(int value) => ScheduleAutoCalculate();

    // Live-Berechnung bei Abzugs-Änderungen
    partial void OnShowDeductionsChanged(bool value) => ScheduleAutoCalculate();
    partial void OnDoorCountChanged(int value) => ScheduleAutoCalculate();
    partial void OnDoorWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnDoorHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnWindowCountChanged(int value) => ScheduleAutoCalculate();
    partial void OnWindowWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnWindowHeightChanged(double value) => ScheduleAutoCalculate();

    #endregion

    #region Unit Labels

    public string AreaUnit => _unitConverter.GetAreaUnit();
    public string VolumeUnit => _unitConverter.GetVolumeUnit();

    private void OnUnitSystemChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AreaUnit));
        OnPropertyChanged(nameof(VolumeUnit));
        OnPropertyChanged(nameof(TotalAreaDisplay));
        OnPropertyChanged(nameof(LitersNeededDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

    #region Cost Calculation

    [ObservableProperty]
    private double _pricePerLiter = 0;

    [ObservableProperty]
    private bool _showCost = false;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerLiter > 0)
        ? $"{(Result.LitersNeeded * PricePerLiter):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string PricePerDisplay => ShowCost ? $"{_localization.GetString("PricePerLiter")}: {PricePerLiter:F2} {_localization.GetString("CurrencySymbol")}" : "";

    partial void OnPricePerLiterChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(PricePerDisplay));
        ScheduleAutoCalculate();
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private PaintResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string _deductedAreaDisplay = "";

    public string TotalAreaDisplay => Result != null
        ? _unitConverter.FormatArea(Result.TotalArea)
        : "";

    public string LitersNeededDisplay => Result != null
        ? _unitConverter.FormatVolume(Result.LitersNeeded, 1)
        : "";

    partial void OnResultChanged(PaintResult? value)
    {
        OnPropertyChanged(nameof(TotalAreaDisplay));
        OnPropertyChanged(nameof(LitersNeededDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

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
    /// Berechnet die Gesamtfläche der Abzüge (Türen + Fenster)
    /// </summary>
    private double CalculateDeductionArea()
    {
        if (!ShowDeductions) return 0;
        var doorArea = DoorCount * Math.Max(0, DoorWidth) * Math.Max(0, DoorHeight);
        var windowArea = WindowCount * Math.Max(0, WindowWidth) * Math.Max(0, WindowHeight);
        return doorArea + windowArea;
    }

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            if (Area <= 0 || CoveragePerLiter <= 0 || NumberOfCoats <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(_localization.GetString("InvalidInputTitle"), _localization.GetString("ValueMustBePositive"));
                return;
            }

            // Abzugsfläche berechnen
            var deduction = CalculateDeductionArea();
            var effectiveArea = Math.Max(0.1, Area - deduction);
            DeductedAreaDisplay = deduction > 0 ? $"-{deduction:F1} m²" : "";

            Result = _craftEngine.CalculatePaint(effectiveArea, CoveragePerLiter, NumberOfCoats);
            HasResult = true;

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
            var title = string.Format(_localization.GetString("HistoryPaintCoats") ?? "{0} m\u00b2, {1}x coat", Area.ToString("F1"), NumberOfCoats);
            var data = new Dictionary<string, object>
            {
                ["Area"] = Area,
                ["CoveragePerLiter"] = CoveragePerLiter,
                ["NumberOfCoats"] = NumberOfCoats,
                ["PricePerLiter"] = PricePerLiter,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["TotalArea"] = Result.TotalArea,
                    ["LitersNeeded"] = Result.LitersNeeded
                } : new Dictionary<string, object>()
            };

            if (ShowDeductions)
            {
                data["DoorCount"] = DoorCount;
                data["DoorWidth"] = DoorWidth;
                data["DoorHeight"] = DoorHeight;
                data["WindowCount"] = WindowCount;
                data["WindowWidth"] = WindowWidth;
                data["WindowHeight"] = WindowHeight;
            }

            await _historyService.AddCalculationAsync("PaintCalculator", title, data);
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

        Area = 20.0;
        CoveragePerLiter = 10.0;
        NumberOfCoats = 2;
        PricePerLiter = 0;
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

    [RelayCommand]
    private void SaveProject()
    {
        if (!HasResult) return;

        SaveProjectName = _currentProjectId != null ? "" : DefaultProjectName;
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
                CalculatorType = CalculatorType.Paint
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["Area"] = Area,
                ["CoveragePerLiter"] = CoveragePerLiter,
                ["NumberOfCoats"] = NumberOfCoats,
                ["PricePerLiter"] = PricePerLiter
            };

            if (ShowDeductions)
            {
                data["DoorCount"] = DoorCount;
                data["DoorWidth"] = DoorWidth;
                data["DoorHeight"] = DoorHeight;
                data["WindowCount"] = WindowCount;
                data["WindowWidth"] = WindowWidth;
                data["WindowHeight"] = WindowHeight;
            }

            // Result-Daten mitspeichern für Export
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["TotalArea"] = Result.TotalArea.ToString("F2"),
                    ["LitersNeeded"] = Result.LitersNeeded.ToString("F1")
                };
                if (ShowCost && PricePerLiter > 0)
                    resultData["TotalCost"] = (Result.LitersNeeded * PricePerLiter).ToString("F2");
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

            Area = project.GetValue("Area", 20.0);
            CoveragePerLiter = project.GetValue("CoveragePerLiter", 10.0);
            NumberOfCoats = project.GetValue("NumberOfCoats", 2);
            PricePerLiter = project.GetValue("PricePerLiter", 0.0);

            DoorCount = project.GetValue("DoorCount", 0);
            DoorWidth = project.GetValue("DoorWidth", 0.8);
            DoorHeight = project.GetValue("DoorHeight", 2.0);
            WindowCount = project.GetValue("WindowCount", 0);
            WindowWidth = project.GetValue("WindowWidth", 1.2);
            WindowHeight = project.GetValue("WindowHeight", 1.0);
            ShowDeductions = DoorCount > 0 || WindowCount > 0;

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

        var title = _localization.GetString("CalcPaint") ?? "Paint";
        var text = $"{title}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("TotalArea") ?? "Total area"}: {TotalAreaDisplay}\n" +
                   $"{_localization.GetString("LitersNeeded") ?? "Liters needed"}: {LitersNeededDisplay}\n" +
                   $"{_localization.GetString("Coats") ?? "Coats"}: {NumberOfCoats}";

        var deduction = CalculateDeductionArea();
        if (deduction > 0)
            text += $"\n{_localization.GetString("DeductedArea") ?? "Deducted area"}: -{deduction:F1} m²";

        if (ShowCost && PricePerLiter > 0)
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

            var calcType = _localization.GetString("CalcPaint") ?? "Paint";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("Area") ?? "Area"] = $"{Area:F1} m\u00b2",
                [_localization.GetString("Coverage") ?? "Coverage"] = $"{CoveragePerLiter:F1} m\u00b2/L",
                [_localization.GetString("Coats") ?? "Coats"] = $"{NumberOfCoats}"
            };

            var deduction = CalculateDeductionArea();
            if (deduction > 0)
                inputs[_localization.GetString("DeductedArea") ?? "Deducted area"] = $"-{deduction:F1} m²";

            var results = new Dictionary<string, string>
            {
                [_localization.GetString("TotalArea") ?? "Total area"] = TotalAreaDisplay,
                [_localization.GetString("LitersNeeded") ?? "Liters needed"] = LitersNeededDisplay
            };
            if (ShowCost && PricePerLiter > 0)
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
    /// Cleanup when ViewModel is disposed
    /// </summary>
    public void Cleanup()
    {
        _unitConverter.UnitSystemChanged -= OnUnitSystemChanged;
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
