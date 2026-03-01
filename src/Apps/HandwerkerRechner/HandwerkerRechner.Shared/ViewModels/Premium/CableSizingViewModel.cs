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
/// ViewModel für den Leitungsquerschnitt-Rechner (Strom, Länge, Spannung → Querschnitt, Spannungsabfall, VDE)
/// </summary>
public partial class CableSizingViewModel : ObservableObject, IDisposable
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

    public CableSizingViewModel(
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
    [ObservableProperty] private double _currentAmps = 16.0;        // Strom in A
    [ObservableProperty] private double _cableLength = 20.0;        // Kabellänge in m
    [ObservableProperty] private int _selectedVoltage;               // 0=230V, 1=400V
    [ObservableProperty] private int _selectedMaterial;              // 0=Kupfer, 1=Aluminium
    [ObservableProperty] private double _maxDropPercent = 3.0;       // Max. Spannungsabfall %

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnCurrentAmpsChanged(double value) => ScheduleAutoCalculate();
    partial void OnCableLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnSelectedVoltageChanged(int value) => ScheduleAutoCalculate();
    partial void OnSelectedMaterialChanged(int value) => ScheduleAutoCalculate();
    partial void OnMaxDropPercentChanged(double value) => ScheduleAutoCalculate();

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcCableSizing") ?? "Cable Sizing";

    // --- Ergebnis-Properties ---
    [ObservableProperty] private CableSizingResult? _result;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _isCalculating;
    [ObservableProperty] private bool _isExporting;

    // --- Display-Properties ---
    public string MaterialDisplay => SelectedMaterial switch
    {
        1 => _localization.GetString("CableMaterialAluminum") ?? "Aluminum",
        _ => _localization.GetString("CableMaterialCopper") ?? "Copper"
    };

    public string VoltageDisplay => SelectedVoltage == 0 ? "230 V" : "400 V";

    public string RecommendedSizeDisplay => Result != null
        ? $"{Result.RecommendedCrossSection:F1} mm\u00b2" : "";

    public string MinSizeDisplay => Result != null
        ? $"{Result.MinCrossSection:F2} mm\u00b2" : "";

    public string DropDisplay => Result != null
        ? $"{Result.ActualDropV:F2} V ({Result.ActualDropPercent:F1} %)" : "";

    public string VdeStatusDisplay => Result != null
        ? (Result.IsVdeCompliant
            ? (_localization.GetString("VdeCompliant") ?? "VDE compliant")
            : (_localization.GetString("VdeNotCompliant") ?? "Not VDE compliant"))
        : "";

    partial void OnResultChanged(CableSizingResult? value)
    {
        OnPropertyChanged(nameof(RecommendedSizeDisplay));
        OnPropertyChanged(nameof(MinSizeDisplay));
        OnPropertyChanged(nameof(DropDisplay));
        OnPropertyChanged(nameof(VdeStatusDisplay));
        OnPropertyChanged(nameof(MaterialDisplay));
        OnPropertyChanged(nameof(VoltageDisplay));
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

            if (CurrentAmps <= 0 || CableLength <= 0)
            {
                HasResult = false;
                MessageRequested?.Invoke(
                    _localization.GetString("InvalidInputTitle"),
                    _localization.GetString("ValueMustBePositive"));
                return;
            }

            double voltageV = SelectedVoltage == 0 ? 230 : 400;

            Result = _engine.CalculateCableSize(CurrentAmps, CableLength, voltageV, SelectedMaterial, MaxDropPercent);
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
            var title = $"{CurrentAmps:F0} A, {CableLength:F0} m, {VoltageDisplay}";
            var data = new Dictionary<string, object>
            {
                ["CurrentAmps"] = CurrentAmps,
                ["CableLength"] = CableLength,
                ["SelectedVoltage"] = SelectedVoltage,
                ["SelectedMaterial"] = SelectedMaterial,
                ["MaxDropPercent"] = MaxDropPercent,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["RecommendedCrossSection"] = Result.RecommendedCrossSection,
                    ["MinCrossSection"] = Result.MinCrossSection,
                    ["ActualDropV"] = Result.ActualDropV,
                    ["ActualDropPercent"] = Result.ActualDropPercent,
                    ["IsVdeCompliant"] = Result.IsVdeCompliant
                } : new Dictionary<string, object>()
            };

            await _historyService.AddCalculationAsync("CableSizingCalculator", title, data);
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

        CurrentAmps = 16.0;
        CableLength = 20.0;
        SelectedVoltage = 0;
        SelectedMaterial = 0;
        MaxDropPercent = 3.0;
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
                CalculatorType = CalculatorType.CableSizing
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["CurrentAmps"] = CurrentAmps,
                ["CableLength"] = CableLength,
                ["SelectedVoltage"] = SelectedVoltage,
                ["SelectedMaterial"] = SelectedMaterial,
                ["MaxDropPercent"] = MaxDropPercent
            };

            // Result-Daten mitspeichern
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["RecommendedCrossSection"] = $"{Result.RecommendedCrossSection:F1}",
                    ["MinCrossSection"] = $"{Result.MinCrossSection:F2}",
                    ["ActualDropV"] = $"{Result.ActualDropV:F2}",
                    ["ActualDropPercent"] = $"{Result.ActualDropPercent:F1}",
                    ["IsVdeCompliant"] = Result.IsVdeCompliant
                };
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

            CurrentAmps = project.GetValue("CurrentAmps", 16.0);
            CableLength = project.GetValue("CableLength", 20.0);
            SelectedVoltage = (int)project.GetValue("SelectedVoltage", 0.0);
            SelectedMaterial = (int)project.GetValue("SelectedMaterial", 0.0);
            MaxDropPercent = project.GetValue("MaxDropPercent", 3.0);

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

        var text = $"{_localization.GetString("CalcCableSizing") ?? "Cable Sizing"}\n" +
                   $"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n" +
                   $"{_localization.GetString("CableCurrent") ?? "Current"}: {CurrentAmps:F1} A\n" +
                   $"{_localization.GetString("CableLength") ?? "Cable length"}: {CableLength:F1} m\n" +
                   $"{_localization.GetString("CableVoltage") ?? "Voltage"}: {VoltageDisplay}\n" +
                   $"{_localization.GetString("CableMaterial") ?? "Material"}: {MaterialDisplay}\n" +
                   $"{_localization.GetString("MaxVoltageDrop") ?? "Max. drop"}: {MaxDropPercent:F1} %\n" +
                   $"\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n" +
                   $"{_localization.GetString("RecommendedCrossSection") ?? "Recommended"}: {RecommendedSizeDisplay}\n" +
                   $"{_localization.GetString("MinCrossSection") ?? "Minimum"}: {MinSizeDisplay}\n" +
                   $"{_localization.GetString("VoltageDrop") ?? "Voltage drop"}: {DropDisplay}\n" +
                   $"{VdeStatusDisplay}";

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

            var calcType = _localization.GetString("CalcCableSizing") ?? "Cable Sizing";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("CableCurrent") ?? "Current"] = $"{CurrentAmps:F1} A",
                [_localization.GetString("CableLength") ?? "Cable length"] = $"{CableLength:F1} m",
                [_localization.GetString("CableVoltage") ?? "Voltage"] = VoltageDisplay,
                [_localization.GetString("CableMaterial") ?? "Material"] = MaterialDisplay,
                [_localization.GetString("MaxVoltageDrop") ?? "Max. drop"] = $"{MaxDropPercent:F1} %"
            };
            var results = new Dictionary<string, string>
            {
                [_localization.GetString("RecommendedCrossSection") ?? "Recommended"] = RecommendedSizeDisplay,
                [_localization.GetString("MinCrossSection") ?? "Minimum"] = MinSizeDisplay,
                [_localization.GetString("VoltageDrop") ?? "Voltage drop"] = DropDisplay,
                ["VDE"] = VdeStatusDisplay
            };

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
