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

public partial class ConcreteCalculatorViewModel : ObservableObject, IDisposable
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
    /// Event fuer Navigation (ersetzt Shell.Current.GoToAsync)
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event fuer Alerts/Nachrichten (Titel, Nachricht)
    /// </summary>
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;

    [ObservableProperty]
    private bool _showSaveDialog;

    [ObservableProperty]
    private string _saveProjectName = string.Empty;

    private string DefaultProjectName => Calculators[SelectedCalculator];

    /// <summary>
    /// Navigation auslösen
    /// </summary>
    private void NavigateTo(string route)
    {
        NavigationRequested?.Invoke(route);
    }

    public ConcreteCalculatorViewModel(
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

        // Einheitensystem-Änderungen abonnieren
        _unitConverter.UnitSystemChanged += OnUnitSystemChanged;
    }

    /// <summary>
    /// Projektdaten per ID laden (ersetzt IQueryAttributable)
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

    #region Sub-Rechner Auswahl

    [ObservableProperty]
    private int _selectedCalculator;

    partial void OnSelectedCalculatorChanged(int value)
    {
        // Kosten zurücksetzen beim Wechsel, um State-Leaking zu vermeiden
        PricePerBag = 0;
        PricePerCubicMeter = 0;
        // Ergebnisse zurücksetzen
        HasResult = false;
        // Sichtbarkeit der Sub-Rechner aktualisieren
        OnPropertyChanged(nameof(IsSlabSelected));
        OnPropertyChanged(nameof(IsStripSelected));
        OnPropertyChanged(nameof(IsColumnSelected));
    }

    public List<string> Calculators =>
    [
        _localization.GetString("ConcreteSlab"),
        _localization.GetString("StripFoundation"),
        _localization.GetString("ConcreteColumn")
    ];

    /// <summary>Sub-Rechner Sichtbarkeit: Platte</summary>
    public bool IsSlabSelected => SelectedCalculator == 0;

    /// <summary>Sub-Rechner Sichtbarkeit: Streifenfundament</summary>
    public bool IsStripSelected => SelectedCalculator == 1;

    /// <summary>Sub-Rechner Sichtbarkeit: Säule</summary>
    public bool IsColumnSelected => SelectedCalculator == 2;

    /// <summary>Verfügbare Sackgewichte für Fertigbeton</summary>
    public List<double> BagWeights => [25, 40];

    #endregion

    #region Input Properties

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnSlabLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnSlabWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnSlabHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnStripLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnStripWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnStripDepthChanged(double value) => ScheduleAutoCalculate();
    partial void OnColumnDiameterChanged(double value) => ScheduleAutoCalculate();
    partial void OnColumnHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnBagWeightChanged(double value) => ScheduleAutoCalculate();

    // Platte (Länge m, Breite m, Höhe cm)
    [ObservableProperty]
    private double _slabLength = 4;

    [ObservableProperty]
    private double _slabWidth = 3;

    [ObservableProperty]
    private double _slabHeight = 15;

    // Streifenfundament (Gesamtlänge m, Breite cm, Tiefe cm)
    [ObservableProperty]
    private double _stripLength = 20;

    [ObservableProperty]
    private double _stripWidth = 30;

    [ObservableProperty]
    private double _stripDepth = 80;

    // Säule (Durchmesser cm, Höhe cm)
    [ObservableProperty]
    private double _columnDiameter = 30;

    [ObservableProperty]
    private double _columnHeight = 250;

    // Gemeinsam: Sackgewicht (25 oder 40 kg Fertigbeton)
    [ObservableProperty]
    private double _bagWeight = 25;

    #endregion

    #region Unit Labels

    public string LengthUnit => _unitConverter.GetLengthUnit();

    private void OnUnitSystemChanged(object? sender, EventArgs e)
    {
        // Alle einheitenabhängigen Anzeige-Properties aktualisieren
        OnPropertyChanged(nameof(LengthUnit));
        OnPropertyChanged(nameof(VolumeDisplay));
        OnPropertyChanged(nameof(CementDisplay));
        OnPropertyChanged(nameof(SandDisplay));
        OnPropertyChanged(nameof(GravelDisplay));
        OnPropertyChanged(nameof(WaterDisplay));
        OnPropertyChanged(nameof(BagsDisplay));
        OnPropertyChanged(nameof(BagCostDisplay));
        OnPropertyChanged(nameof(CubicMeterCostDisplay));
    }

    #endregion

    #region Cost Calculation

    // Preis pro Sack Fertigbeton
    [ObservableProperty]
    private double _pricePerBag = 0;

    [ObservableProperty]
    private bool _showBagCost = false;

    // Preis pro m³ Fertigbeton
    [ObservableProperty]
    private double _pricePerCubicMeter = 0;

    [ObservableProperty]
    private bool _showCubicMeterCost = false;

    public string BagCostDisplay => (ShowBagCost && PricePerBag > 0 && Result != null && Result.BagsNeeded > 0)
        ? $"{(Result.BagsNeeded * PricePerBag):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string CubicMeterCostDisplay => (ShowCubicMeterCost && PricePerCubicMeter > 0 && Result != null && Result.VolumeM3 > 0)
        ? $"{(Result.VolumeM3 * PricePerCubicMeter):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerBagChanged(double value)
    {
        ShowBagCost = value > 0;
        OnPropertyChanged(nameof(BagCostDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnPricePerCubicMeterChanged(double value)
    {
        ShowCubicMeterCost = value > 0;
        OnPropertyChanged(nameof(CubicMeterCostDisplay));
        ScheduleAutoCalculate();
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private ConcreteResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    public string VolumeDisplay => Result != null
        ? $"{Result.VolumeM3:F2} m\u00b3"
        : "";

    public string CementDisplay => Result != null
        ? $"{Result.CementKg:F1} kg"
        : "";

    public string SandDisplay => Result != null
        ? $"{Result.SandKg:F1} kg"
        : "";

    public string GravelDisplay => Result != null
        ? $"{Result.GravelKg:F1} kg"
        : "";

    public string WaterDisplay => Result != null
        ? $"{Result.WaterLiters:F1} L"
        : "";

    public string BagsDisplay => Result != null
        ? $"{Result.BagsNeeded} \u00d7 {Result.BagWeight} kg"
        : "";

    partial void OnResultChanged(ConcreteResult? value)
    {
        OnPropertyChanged(nameof(VolumeDisplay));
        OnPropertyChanged(nameof(CementDisplay));
        OnPropertyChanged(nameof(SandDisplay));
        OnPropertyChanged(nameof(GravelDisplay));
        OnPropertyChanged(nameof(WaterDisplay));
        OnPropertyChanged(nameof(BagsDisplay));
        OnPropertyChanged(nameof(BagCostDisplay));
        OnPropertyChanged(nameof(CubicMeterCostDisplay));
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

            // Validierung je nach ausgewähltem Sub-Rechner
            switch (SelectedCalculator)
            {
                case 0: // Platte
                    if (SlabLength <= 0 || SlabWidth <= 0 || SlabHeight <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    Result = _craftEngine.CalculateConcrete(0, SlabLength, SlabWidth, SlabHeight, BagWeight);
                    break;

                case 1: // Streifenfundament
                    if (StripLength <= 0 || StripWidth <= 0 || StripDepth <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    Result = _craftEngine.CalculateConcrete(1, StripLength, StripWidth, StripDepth, BagWeight);
                    break;

                case 2: // Säule
                    if (ColumnDiameter <= 0 || ColumnHeight <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    Result = _craftEngine.CalculateConcrete(2, ColumnDiameter, 0, ColumnHeight, BagWeight);
                    break;
            }

            HasResult = true;

            // In History speichern
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
            string calcType, title;
            Dictionary<string, object> data;

            switch (SelectedCalculator)
            {
                case 0: // Platte
                    calcType = "ConcreteSlabCalculator";
                    title = $"{SlabLength:F1} \u00d7 {SlabWidth:F1} m, h={SlabHeight} cm \u2192 {Result?.VolumeM3:F2} m\u00b3";
                    data = new Dictionary<string, object>
                    {
                        ["SlabLength"] = SlabLength,
                        ["SlabWidth"] = SlabWidth,
                        ["SlabHeight"] = SlabHeight,
                        ["BagWeight"] = BagWeight,
                        ["PricePerBag"] = PricePerBag,
                        ["PricePerCubicMeter"] = PricePerCubicMeter,
                        ["Result"] = Result != null ? new Dictionary<string, object>
                        {
                            ["VolumeM3"] = Result.VolumeM3,
                            ["BagsNeeded"] = Result.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;

                case 1: // Streifenfundament
                    calcType = "StripFoundationCalculator";
                    title = $"{StripLength:F1} m, {StripWidth}\u00d7{StripDepth} cm \u2192 {Result?.VolumeM3:F2} m\u00b3";
                    data = new Dictionary<string, object>
                    {
                        ["StripLength"] = StripLength,
                        ["StripWidth"] = StripWidth,
                        ["StripDepth"] = StripDepth,
                        ["BagWeight"] = BagWeight,
                        ["PricePerBag"] = PricePerBag,
                        ["PricePerCubicMeter"] = PricePerCubicMeter,
                        ["Result"] = Result != null ? new Dictionary<string, object>
                        {
                            ["VolumeM3"] = Result.VolumeM3,
                            ["BagsNeeded"] = Result.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;

                default: // Säule
                    calcType = "ConcreteColumnCalculator";
                    title = $"\u00d8{ColumnDiameter} cm, h={ColumnHeight} cm \u2192 {Result?.VolumeM3:F2} m\u00b3";
                    data = new Dictionary<string, object>
                    {
                        ["ColumnDiameter"] = ColumnDiameter,
                        ["ColumnHeight"] = ColumnHeight,
                        ["BagWeight"] = BagWeight,
                        ["PricePerBag"] = PricePerBag,
                        ["PricePerCubicMeter"] = PricePerCubicMeter,
                        ["Result"] = Result != null ? new Dictionary<string, object>
                        {
                            ["VolumeM3"] = Result.VolumeM3,
                            ["BagsNeeded"] = Result.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;
            }

            await _historyService.AddCalculationAsync(calcType, title, data);
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

        // Platte
        SlabLength = 4;
        SlabWidth = 3;
        SlabHeight = 15;
        // Fundament
        StripLength = 20;
        StripWidth = 30;
        StripDepth = 80;
        // Säule
        ColumnDiameter = 30;
        ColumnHeight = 250;
        // Gemeinsam
        BagWeight = 25;
        PricePerBag = 0;
        PricePerCubicMeter = 0;
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
            var calcType = SelectedCalculator switch
            {
                0 => CalculatorType.ConcreteSlab,
                1 => CalculatorType.ConcreteStrip,
                2 => CalculatorType.ConcreteColumn,
                _ => CalculatorType.ConcreteSlab
            };

            var project = new Project
            {
                Name = name,
                CalculatorType = calcType
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["SelectedCalculator"] = SelectedCalculator,
                // Platte
                ["SlabLength"] = SlabLength,
                ["SlabWidth"] = SlabWidth,
                ["SlabHeight"] = SlabHeight,
                // Fundament
                ["StripLength"] = StripLength,
                ["StripWidth"] = StripWidth,
                ["StripDepth"] = StripDepth,
                // Säule
                ["ColumnDiameter"] = ColumnDiameter,
                ["ColumnHeight"] = ColumnHeight,
                // Gemeinsam
                ["BagWeight"] = BagWeight,
                ["PricePerBag"] = PricePerBag,
                ["PricePerCubicMeter"] = PricePerCubicMeter
            };

            // Result-Daten mitspeichern für Export (nur aktiver Sub-Rechner)
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["ResultVolume"] = Result.VolumeM3.ToString("F2"),
                    ["ResultCite"] = Result.CementKg.ToString("F1"),
                    ["ResultSand"] = Result.SandKg.ToString("F1"),
                    ["ResultGravel"] = Result.GravelKg.ToString("F1"),
                    ["ResultWater"] = Result.WaterLiters.ToString("F1"),
                    ["ResultBags"] = $"{Result.BagsNeeded} \u00d7 {Result.BagWeight} kg"
                };
                if (ShowBagCost && PricePerBag > 0)
                    resultData["CostBags"] = (Result.BagsNeeded * PricePerBag).ToString("F2");
                if (ShowCubicMeterCost && PricePerCubicMeter > 0)
                    resultData["CostCubicMeter"] = (Result.VolumeM3 * PricePerCubicMeter).ToString("F2");
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

            SelectedCalculator = project.GetValue("SelectedCalculator", 0);

            // Platte
            SlabLength = project.GetValue("SlabLength", 4.0);
            SlabWidth = project.GetValue("SlabWidth", 3.0);
            SlabHeight = project.GetValue("SlabHeight", 15.0);
            // Fundament
            StripLength = project.GetValue("StripLength", 20.0);
            StripWidth = project.GetValue("StripWidth", 30.0);
            StripDepth = project.GetValue("StripDepth", 80.0);
            // Säule
            ColumnDiameter = project.GetValue("ColumnDiameter", 30.0);
            ColumnHeight = project.GetValue("ColumnHeight", 250.0);
            // Gemeinsam
            BagWeight = project.GetValue("BagWeight", 25.0);
            PricePerBag = project.GetValue("PricePerBag", 0.0);
            PricePerCubicMeter = project.GetValue("PricePerCubicMeter", 0.0);

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

        var shapeName = Calculators[SelectedCalculator];
        var text = $"{shapeName}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("ResultVolume") ?? "Volume"}: {VolumeDisplay}\n" +
                   $"{_localization.GetString("ResultBags") ?? "Bags"}: {BagsDisplay}\n" +
                   $"\n{_localization.GetString("SelfMixing") ?? "Self-Mixing"}:\n" +
                   $"{_localization.GetString("ResultCite") ?? "Cement"}: {CementDisplay}\n" +
                   $"{_localization.GetString("ResultSand") ?? "Sand"}: {SandDisplay}\n" +
                   $"{_localization.GetString("ResultGravel") ?? "Gravel"}: {GravelDisplay}\n" +
                   $"{_localization.GetString("ResultWater") ?? "Water"}: {WaterDisplay}";

        if (ShowBagCost && PricePerBag > 0)
            text += $"\n{_localization.GetString("CostBags") ?? "Bag Cost"}: {BagCostDisplay}";
        if (ShowCubicMeterCost && PricePerCubicMeter > 0)
            text += $"\n{_localization.GetString("CostCubicMeter") ?? "Cost/m³"}: {CubicMeterCostDisplay}";

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

            var calcType = Calculators[SelectedCalculator];
            var inputs = new Dictionary<string, string>();
            var results = new Dictionary<string, string>();

            switch (SelectedCalculator)
            {
                case 0: // Platte
                    inputs[_localization.GetString("SlabLength") ?? "Length"] = $"{SlabLength:F1} m";
                    inputs[_localization.GetString("SlabWidth") ?? "Width"] = $"{SlabWidth:F1} m";
                    inputs[_localization.GetString("SlabHeight") ?? "Height"] = $"{SlabHeight} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;

                case 1: // Streifenfundament
                    inputs[_localization.GetString("StripLength") ?? "Length"] = $"{StripLength:F1} m";
                    inputs[_localization.GetString("StripWidth") ?? "Width"] = $"{StripWidth} cm";
                    inputs[_localization.GetString("StripDepth") ?? "Depth"] = $"{StripDepth} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;

                case 2: // Säule
                    inputs[_localization.GetString("ColumnDiameter") ?? "Diameter"] = $"{ColumnDiameter} cm";
                    inputs[_localization.GetString("ColumnHeight") ?? "Height"] = $"{ColumnHeight} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;
            }

            // Ergebnisse (gleich fuer alle Sub-Rechner)
            results[_localization.GetString("ResultVolume") ?? "Volume"] = VolumeDisplay;
            results[_localization.GetString("ResultCite") ?? "Cement"] = CementDisplay;
            results[_localization.GetString("ResultSand") ?? "Sand"] = SandDisplay;
            results[_localization.GetString("ResultGravel") ?? "Gravel"] = GravelDisplay;
            results[_localization.GetString("ResultWater") ?? "Water"] = WaterDisplay;
            results[_localization.GetString("ResultBags") ?? "Bags"] = BagsDisplay;

            if (ShowBagCost && PricePerBag > 0)
                results[_localization.GetString("PricePerBag") ?? "Bag cost"] = BagCostDisplay;
            if (ShowCubicMeterCost && PricePerCubicMeter > 0)
                results[_localization.GetString("PricePerCubicMeter") ?? "m\u00b3 cost"] = CubicMeterCostDisplay;

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
    /// Cleanup bei ViewModel-Dispose
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
