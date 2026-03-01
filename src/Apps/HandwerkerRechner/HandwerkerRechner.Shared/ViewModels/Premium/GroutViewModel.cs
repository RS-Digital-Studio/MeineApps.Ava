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
/// ViewModel für den Fugenmasse-Rechner (Fläche, Fliesenmaße, Fugenbreite, Fugentiefe → kg, Eimer, Kosten)
/// </summary>
public partial class GroutViewModel : ObservableObject, IDisposable
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

    public GroutViewModel(
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
    /// Lädt Projektdaten per ID
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
    [ObservableProperty] private double _areaSqm = 20.0;           // Bodenfläche in m²
    [ObservableProperty] private double _tileLengthCm = 30.0;      // Fliesenlänge in cm
    [ObservableProperty] private double _tileWidthCm = 30.0;       // Fliesenbreite in cm
    [ObservableProperty] private double _groutWidthMm = 3.0;       // Fugenbreite in mm
    [ObservableProperty] private double _groutDepthMm = 6.0;       // Fugentiefe in mm
    [ObservableProperty] private double _pricePerKg = 2.50;        // Preis pro kg Fugenmasse

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnAreaSqmChanged(double value) => ScheduleAutoCalculate();
    partial void OnTileLengthCmChanged(double value) => ScheduleAutoCalculate();
    partial void OnTileWidthCmChanged(double value) => ScheduleAutoCalculate();
    partial void OnGroutWidthMmChanged(double value) => ScheduleAutoCalculate();
    partial void OnGroutDepthMmChanged(double value) => ScheduleAutoCalculate();
    partial void OnPricePerKgChanged(double value) => ScheduleAutoCalculate();

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcGrout") ?? "Fugenmasse";

    // --- Ergebnis-Properties ---
    [ObservableProperty] private GroutResult? _result;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _isExporting;

    // Display-Properties (für Binding)
    [ObservableProperty] private string _totalKgDisplay = "";
    [ObservableProperty] private double _totalKgValue;
    [ObservableProperty] private string _totalWithReserveDisplay = "";
    [ObservableProperty] private string _bucketsDisplay = "";
    [ObservableProperty] private string _consumptionDisplay = "";
    [ObservableProperty] private string _costDisplay = "";

    // --- Berechnung ---
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
        var result = CraftEngine.CalculateGrout(AreaSqm, TileLengthCm, TileWidthCm, GroutWidthMm, GroutDepthMm, PricePerKg);
        Result = result;
        HasResult = result.TotalKg > 0;

        if (HasResult)
        {
            TotalKgValue = result.TotalWithReserveKg;
            TotalKgDisplay = $"{result.TotalWithReserveKg:F1} kg";
            TotalWithReserveDisplay = $"{result.TotalWithReserveKg:F1} kg";
            BucketsDisplay = $"{result.BucketsNeeded} \u00d7 5 kg";
            ConsumptionDisplay = $"{result.ConsumptionPerSqm:F2} kg/m\u00b2";
            CostDisplay = $"{result.TotalCost:F2} {_localization.GetString("CurrencySymbol") ?? "\u20ac"}";

            await SaveToHistoryAsync();
        }
    }

    // --- History ---
    private async Task SaveToHistoryAsync()
    {
        try
        {
            if (Result == null) return;
            var title = $"{Result.TotalWithReserveKg:F1} kg ({Result.BucketsNeeded} {_localization.GetString("UnitBuckets") ?? "Eimer"})";
            var data = new Dictionary<string, object>
            {
                ["AreaSqm"] = AreaSqm,
                ["TileLengthCm"] = TileLengthCm,
                ["TileWidthCm"] = TileWidthCm,
                ["GroutWidthMm"] = GroutWidthMm,
                ["GroutDepthMm"] = GroutDepthMm,
                ["PricePerKg"] = PricePerKg,
                ["Result"] = new Dictionary<string, object>
                {
                    ["TotalKg"] = Result.TotalKg,
                    ["TotalWithReserveKg"] = Result.TotalWithReserveKg,
                    ["BucketsNeeded"] = Result.BucketsNeeded,
                    ["ConsumptionPerSqm"] = Result.ConsumptionPerSqm,
                    ["TotalCost"] = Result.TotalCost
                }
            };

            await _historyService.AddCalculationAsync("GroutCalculator", title, data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
        }
    }

    // --- Projekte ---
    [RelayCommand]
    private void SaveProject()
    {
        if (!HasResult) return;
        SaveProjectName = _currentProjectId != null ? string.Empty : DefaultProjectName;
        ShowSaveDialog = true;
    }

    [RelayCommand]
    private void CancelSaveProject()
    {
        ShowSaveDialog = false;
        SaveProjectName = string.Empty;
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
                CalculatorType = CalculatorType.Grout
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
                project.Id = _currentProjectId;

            var data = new Dictionary<string, object>
            {
                ["AreaSqm"] = AreaSqm,
                ["TileLengthCm"] = TileLengthCm,
                ["TileWidthCm"] = TileWidthCm,
                ["GroutWidthMm"] = GroutWidthMm,
                ["GroutDepthMm"] = GroutDepthMm,
                ["PricePerKg"] = PricePerKg
            };

            // Result-Daten mitspeichern
            if (HasResult && Result != null)
            {
                data["Result"] = new Dictionary<string, object>
                {
                    ["TotalKg"] = $"{Result.TotalKg:F1}",
                    ["TotalWithReserveKg"] = $"{Result.TotalWithReserveKg:F1}",
                    ["BucketsNeeded"] = Result.BucketsNeeded,
                    ["ConsumptionPerSqm"] = $"{Result.ConsumptionPerSqm:F2}",
                    ["TotalCost"] = $"{Result.TotalCost:F2}"
                };
            }

            project.SetData(data);
            await _projectService.SaveProjectAsync(project);
            _currentProjectId = project.Id;

            MessageRequested?.Invoke(
                _localization.GetString("Success") ?? "Erfolg",
                _localization.GetString("ProjectSaved") ?? "Projekt gespeichert");
            FloatingTextRequested?.Invoke(_localization.GetString("ProjectSaved") ?? "Projekt gespeichert!", "success");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler",
                _localization.GetString("ProjectSaveFailed") ?? "Speichern fehlgeschlagen");
        }
    }

    private async Task LoadProjectAsync(string projectId)
    {
        try
        {
            var project = await _projectService.LoadProjectAsync(projectId);
            if (project == null) return;

            AreaSqm = project.GetValue("AreaSqm", 20.0);
            TileLengthCm = project.GetValue("TileLengthCm", 30.0);
            TileWidthCm = project.GetValue("TileWidthCm", 30.0);
            GroutWidthMm = project.GetValue("GroutWidthMm", 3.0);
            GroutDepthMm = project.GetValue("GroutDepthMm", 6.0);
            PricePerKg = project.GetValue("PricePerKg", 2.50);

            await Calculate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
        }
    }

    // --- Share ---
    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult || Result == null) return;

        var text = $"{_localization.GetString("CalcGrout") ?? "Fugenmasse"}\n" +
                   $"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n" +
                   $"{_localization.GetString("GroutArea") ?? "Fl\u00e4che"}: {Result.AreaSqm:F1} m\u00b2\n" +
                   $"{_localization.GetString("GroutTileSize") ?? "Fliesengr\u00f6\u00dfe"}: {Result.TileLengthCm:F0} \u00d7 {Result.TileWidthCm:F0} cm\n" +
                   $"{_localization.GetString("GroutWidth") ?? "Fugenbreite"}: {Result.GroutWidthMm:F1} mm\n" +
                   $"{_localization.GetString("GroutDepth") ?? "Fugentiefe"}: {Result.GroutDepthMm:F1} mm\n" +
                   $"{_localization.GetString("GroutTotal") ?? "Gesamt"}: {Result.TotalWithReserveKg:F1} kg\n" +
                   $"{_localization.GetString("UnitBuckets") ?? "Eimer"}: {Result.BucketsNeeded} \u00d7 5 kg\n" +
                   $"{_localization.GetString("ResultCost") ?? "Kosten"}: {Result.TotalCost:F2} {_localization.GetString("CurrencySymbol") ?? "\u20ac"}";

        ClipboardRequested?.Invoke(text);
        FloatingTextRequested?.Invoke(_localization.GetString("CopiedToClipboard") ?? "Kopiert!", "success");
    }

    // --- PDF Export ---
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

            var calcType = _localization.GetString("CalcGrout") ?? "Fugenmasse";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("GroutArea") ?? "Fl\u00e4che"] = $"{AreaSqm:F1} m\u00b2",
                [_localization.GetString("GroutTileSize") ?? "Fliesengr\u00f6\u00dfe"] = $"{TileLengthCm:F0} \u00d7 {TileWidthCm:F0} cm",
                [_localization.GetString("GroutWidth") ?? "Fugenbreite"] = $"{GroutWidthMm:F1} mm",
                [_localization.GetString("GroutDepth") ?? "Fugentiefe"] = $"{GroutDepthMm:F1} mm"
            };

            var results = new Dictionary<string, string>
            {
                [_localization.GetString("GroutConsumption") ?? "Verbrauch"] = ConsumptionDisplay,
                [_localization.GetString("GroutTotal") ?? "Gesamt"] = TotalWithReserveDisplay,
                [_localization.GetString("UnitBuckets") ?? "Eimer"] = BucketsDisplay,
                [_localization.GetString("ResultCost") ?? "Kosten"] = CostDisplay
            };

            var path = await _exportService.ExportToPdfAsync(calcType, inputs, results);
            await _fileShareService.ShareFileAsync(path, _localization.GetString("ShareMaterialList") ?? "Share", "application/pdf");
            MessageRequested?.Invoke(_localization.GetString("Success") ?? "Erfolg", _localization.GetString("PdfExportSuccess") ?? "PDF exportiert!");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Fehler", _localization.GetString("PdfExportFailed") ?? "Export fehlgeschlagen.");
        }
        finally
        {
            IsExporting = false;
        }
    }

    // --- Reset ---
    [RelayCommand]
    private void Reset()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        AreaSqm = 20.0;
        TileLengthCm = 30.0;
        TileWidthCm = 30.0;
        GroutWidthMm = 3.0;
        GroutDepthMm = 6.0;
        PricePerKg = 2.50;
        Result = null;
        HasResult = false;
        TotalKgDisplay = "";
        TotalKgValue = 0;
        TotalWithReserveDisplay = "";
        BucketsDisplay = "";
        ConsumptionDisplay = "";
        CostDisplay = "";
        _currentProjectId = null;
        SaveProjectName = "";
    }

    // --- Navigation ---
    [RelayCommand]
    private void GoBack() => NavigateTo("..");

    public void Cleanup() => _debounceTimer?.Dispose();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
