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

public partial class FlooringCalculatorViewModel : ObservableObject, IDisposable
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

    private string DefaultProjectName => _localization.GetString("CalcFlooring");

    /// <summary>
    /// Invoke navigation request
    /// </summary>
    private void NavigateTo(string route)
    {
        NavigationRequested?.Invoke(route);
    }

    public FlooringCalculatorViewModel(
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
    private double _roomLength = 4.0;

    [ObservableProperty]
    private double _roomWidth = 3.0;

    [ObservableProperty]
    private double _boardLength = 2.0;

    [ObservableProperty]
    private double _boardWidth = 15;

    [ObservableProperty]
    private double _wastePercentage = 10;

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnRoomLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnRoomWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnBoardLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnBoardWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnWastePercentageChanged(double value) => ScheduleAutoCalculate();

    #endregion

    #region Unit Labels

    public string LengthUnit => _unitConverter.GetLengthUnit();
    public string AreaUnit => _unitConverter.GetAreaUnit();
    public string BoardWidthUnit => _unitConverter.CurrentSystem == UnitSystem.Metric ? "cm" : "in";

    private void OnUnitSystemChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(LengthUnit));
        OnPropertyChanged(nameof(AreaUnit));
        OnPropertyChanged(nameof(BoardWidthUnit));
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(BoardsNeededDisplay));
        OnPropertyChanged(nameof(BoardsWithWasteDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

    #region Cost Calculation

    [ObservableProperty]
    private double _pricePerBoard = 0;

    [ObservableProperty]
    private bool _showCost = false;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerBoard > 0)
        ? $"{(Result.BoardsWithWaste * PricePerBoard):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string PricePerDisplay => ShowCost ? $"{_localization.GetString("PricePerBoard")}: {PricePerBoard:F2} {_localization.GetString("CurrencySymbol")}" : "";

    partial void OnPricePerBoardChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(PricePerDisplay));
        ScheduleAutoCalculate();
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private FlooringResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    public string AreaDisplay => Result != null
        ? _unitConverter.FormatArea(Result.RoomArea)
        : "";

    public string BoardsNeededDisplay => Result != null
        ? $"{Result.BoardsNeeded} {_localization.GetString("UnitBoards")}"
        : "";

    public string BoardsWithWasteDisplay => Result != null
        ? $"{Result.BoardsWithWaste} {_localization.GetString("UnitBoards")}"
        : "";

    partial void OnResultChanged(FlooringResult? value)
    {
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(BoardsNeededDisplay));
        OnPropertyChanged(nameof(BoardsWithWasteDisplay));
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

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            if (RoomLength <= 0 || RoomWidth <= 0 || BoardLength <= 0 || BoardWidth <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(_localization.GetString("InvalidInputTitle"), _localization.GetString("ValueMustBePositive"));
                return;
            }

            // Negativer Verschnitt ist nicht sinnvoll
            if (WastePercentage < 0) WastePercentage = 0;

            Result = _craftEngine.CalculateFlooring(RoomLength, RoomWidth, BoardLength, BoardWidth, WastePercentage);
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
            var title = $"{RoomLength:F1} \u00d7 {RoomWidth:F1} m, {BoardLength} m \u00d7 {BoardWidth} cm";
            var data = new Dictionary<string, object>
            {
                ["RoomLength"] = RoomLength,
                ["RoomWidth"] = RoomWidth,
                ["BoardLength"] = BoardLength,
                ["BoardWidth"] = BoardWidth,
                ["WastePercentage"] = WastePercentage,
                ["PricePerBoard"] = PricePerBoard,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["RoomArea"] = Result.RoomArea,
                    ["BoardsNeeded"] = Result.BoardsNeeded,
                    ["BoardsWithWaste"] = Result.BoardsWithWaste
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("FlooringCalculator", title, data);
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

        RoomLength = 4.0;
        RoomWidth = 3.0;
        BoardLength = 2.0;
        BoardWidth = 15;
        WastePercentage = 10;
        PricePerBoard = 0;
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
                CalculatorType = CalculatorType.Flooring
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["RoomLength"] = RoomLength,
                ["RoomWidth"] = RoomWidth,
                ["BoardLength"] = BoardLength,
                ["BoardWidth"] = BoardWidth,
                ["WastePercentage"] = WastePercentage,
                ["PricePerBoard"] = PricePerBoard
            };

            // Result-Daten mitspeichern für Export
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["Area"] = Result.RoomArea.ToString("F2"),
                    ["BoardsNeeded"] = Result.BoardsNeeded,
                    ["BoardsWithWaste"] = Result.BoardsWithWaste
                };
                if (ShowCost && PricePerBoard > 0)
                    resultData["TotalCost"] = (Result.BoardsWithWaste * PricePerBoard).ToString("F2");
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

            RoomLength = project.GetValue("RoomLength", 4.0);
            RoomWidth = project.GetValue("RoomWidth", 3.0);
            BoardLength = project.GetValue("BoardLength", 2.0);
            BoardWidth = project.GetValue("BoardWidth", 15.0);
            WastePercentage = project.GetValue("WastePercentage", 10.0);
            PricePerBoard = project.GetValue("PricePerBoard", 0.0);

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

        var title = _localization.GetString("CalcFlooring") ?? "Flooring";
        var text = $"{title}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("Area") ?? "Area"}: {AreaDisplay}\n" +
                   $"{_localization.GetString("BoardsNeeded") ?? "Boards needed"}: {BoardsNeededDisplay}\n" +
                   $"{_localization.GetString("BoardsWithWaste") ?? "With waste"}: {BoardsWithWasteDisplay}";

        if (ShowCost && PricePerBoard > 0)
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

            var calcType = _localization.GetString("CalcFlooring") ?? "Flooring";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("RoomLength") ?? "Room length"] = $"{RoomLength:F1} m",
                [_localization.GetString("RoomWidth") ?? "Room width"] = $"{RoomWidth:F1} m",
                [_localization.GetString("BoardLength") ?? "Board length"] = $"{BoardLength:F2} m",
                [_localization.GetString("BoardWidth") ?? "Board width"] = $"{BoardWidth} cm",
                [_localization.GetString("Waste") ?? "Waste"] = $"{WastePercentage} %"
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("Area") ?? "Area"] = AreaDisplay,
                [_localization.GetString("BoardsNeeded") ?? "Boards needed"] = BoardsNeededDisplay,
                [_localization.GetString("BoardsWithWaste") ?? "With waste"] = BoardsWithWasteDisplay
            };
            if (ShowCost && PricePerBoard > 0)
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
