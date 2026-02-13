using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels.Floor;

public partial class TileCalculatorViewModel : ObservableObject
{
    private readonly CraftEngine _craftEngine;
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

    private string DefaultProjectName => _localization.GetString("CalcTiles");

    /// <summary>
    /// Invoke navigation request
    /// </summary>
    private void NavigateTo(string route)
    {
        NavigationRequested?.Invoke(route);
    }

    public TileCalculatorViewModel(
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

        // Subscribe to unit system changes
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
        catch (Exception)
        {
        }
    }

    #region Input Properties

    [ObservableProperty]
    private double _roomLength = 4.0;

    [ObservableProperty]
    private double _roomWidth = 3.0;

    [ObservableProperty]
    private double _tileLength = 30;

    [ObservableProperty]
    private double _tileWidth = 30;

    [ObservableProperty]
    private double _wastePercentage = 10;

    #endregion

    #region Unit Labels

    public string LengthUnit => _unitConverter.GetLengthUnit();
    public string AreaUnit => _unitConverter.GetAreaUnit();
    public string TileSizeUnit => _unitConverter.CurrentSystem == UnitSystem.Metric ? "cm" : "in";

    private void OnUnitSystemChanged(object? sender, EventArgs e)
    {
        // Refresh all unit-dependent display properties
        OnPropertyChanged(nameof(LengthUnit));
        OnPropertyChanged(nameof(AreaUnit));
        OnPropertyChanged(nameof(TileSizeUnit));
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(TilesNeededDisplay));
        OnPropertyChanged(nameof(TilesWithWasteDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

    #region Cost Calculation

    [ObservableProperty]
    private double _pricePerTile = 0;

    [ObservableProperty]
    private bool _showCost = false;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerTile > 0)
        ? $"{(Result.TilesWithWaste * PricePerTile):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string PricePerDisplay => ShowCost ? $"{_localization.GetString("PricePerTile")}: {PricePerTile:F2} {_localization.GetString("CurrencySymbol")}" : "";

    partial void OnPricePerTileChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(PricePerDisplay));
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private TileResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    public string AreaDisplay => Result != null
        ? _unitConverter.FormatArea(Result.RoomArea)
        : "";

    public string TilesNeededDisplay => Result != null
        ? $"{Result.TilesNeeded} {_localization.GetString("UnitPieces")}"
        : "";

    public string TilesWithWasteDisplay => Result != null
        ? $"{Result.TilesWithWaste} {_localization.GetString("UnitPieces")}"
        : "";

    partial void OnResultChanged(TileResult? value)
    {
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(TilesNeededDisplay));
        OnPropertyChanged(nameof(TilesWithWasteDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            if (RoomLength <= 0 || RoomWidth <= 0 || TileLength <= 0 || TileWidth <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(_localization.GetString("InvalidInputTitle"), _localization.GetString("ValueMustBePositive"));
                return;
            }

            // Negativer Verschnitt ist nicht sinnvoll
            if (WastePercentage < 0) WastePercentage = 0;

            Result = _craftEngine.CalculateTiles(RoomLength, RoomWidth, TileLength, TileWidth, WastePercentage);
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
            var title = $"{RoomLength:F1} \u00d7 {RoomWidth:F1} m, {TileLength}\u00d7{TileWidth} cm";
            var data = new Dictionary<string, object>
            {
                ["RoomLength"] = RoomLength,
                ["RoomWidth"] = RoomWidth,
                ["TileLength"] = TileLength,
                ["TileWidth"] = TileWidth,
                ["WastePercentage"] = WastePercentage,
                ["PricePerTile"] = PricePerTile,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["RoomArea"] = Result.RoomArea,
                    ["TilesNeeded"] = Result.TilesNeeded,
                    ["TilesWithWaste"] = Result.TilesWithWaste
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("TileCalculator", title, data);
        }
        catch (Exception)
        {
        }
    }

    [RelayCommand]
    private void Reset()
    {
        RoomLength = 4.0;
        RoomWidth = 3.0;
        TileLength = 30;
        TileWidth = 30;
        WastePercentage = 10;
        PricePerTile = 0;
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
                CalculatorType = CalculatorType.Tiles
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["RoomLength"] = RoomLength,
                ["RoomWidth"] = RoomWidth,
                ["TileLength"] = TileLength,
                ["TileWidth"] = TileWidth,
                ["WastePercentage"] = WastePercentage,
                ["PricePerTile"] = PricePerTile
            };

            // Result-Daten mitspeichern für Export
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["Area"] = Result.RoomArea.ToString("F2"),
                    ["TilesNeeded"] = Result.TilesNeeded,
                    ["TilesWithWaste"] = Result.TilesWithWaste
                };
                if (ShowCost && PricePerTile > 0)
                    resultData["TotalCost"] = (Result.TilesWithWaste * PricePerTile).ToString("F2");
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
            TileLength = project.GetValue("TileLength", 30.0);
            TileWidth = project.GetValue("TileWidth", 30.0);
            WastePercentage = project.GetValue("WastePercentage", 10.0);
            PricePerTile = project.GetValue("PricePerTile", 0.0);

            await Calculate();
        }
        catch (Exception)
        {
        }
    }

    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult || Result == null) return;

        var title = _localization.GetString("CalcTiles") ?? "Tiles";
        var text = $"{title}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("Area") ?? "Area"}: {AreaDisplay}\n" +
                   $"{_localization.GetString("TilesNeeded") ?? "Tiles needed"}: {TilesNeededDisplay}\n" +
                   $"{_localization.GetString("TilesWithWaste") ?? "With waste"}: {TilesWithWasteDisplay}";

        if (ShowCost && PricePerTile > 0)
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

            // Premium: Direkt. Free: Rewarded Ad
            if (!_purchaseService.IsPremium)
            {
                var adResult = await _rewardedAdService.ShowAdAsync("material_pdf");
                if (!adResult) return;
            }

            var calcType = _localization.GetString("CalcTiles") ?? "Tiles";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("RoomLength") ?? "Room length"] = $"{RoomLength:F1} m",
                [_localization.GetString("RoomWidth") ?? "Room width"] = $"{RoomWidth:F1} m",
                [_localization.GetString("TileLength") ?? "Tile length"] = $"{TileLength} cm",
                [_localization.GetString("TileWidth") ?? "Tile width"] = $"{TileWidth} cm",
                [_localization.GetString("Waste") ?? "Waste"] = $"{WastePercentage} %"
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("Area") ?? "Area"] = AreaDisplay,
                [_localization.GetString("TilesNeeded") ?? "Tiles needed"] = TilesNeededDisplay,
                [_localization.GetString("TilesWithWaste") ?? "With waste"] = TilesWithWasteDisplay
            };
            if (ShowCost && PricePerTile > 0)
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
}
