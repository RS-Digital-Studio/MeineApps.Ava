using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels.Premium;

public partial class ElectricalViewModel : ObservableObject
{
    #region Default Values

    private static class Defaults
    {
        // Voltage Drop Calculator
        public const double Voltage = 230.0;           // Standard EU voltage (V)
        public const double Current = 16.0;            // Common circuit breaker amperage (A)
        public const double CableLength = 20.0;        // Typical cable length (m)
        public const double CrossSection = 2.5;        // Standard household cable (mm²)

        // Power Cost Calculator
        public const double Power = 1000.0;            // 1 kW reference (W)
        public const double HoursPerDay = 4.0;         // Average daily usage (h)
        public const double PricePerKwh = 0.35;        // EU average electricity price (€/kWh)
    }

    #endregion

    private readonly CraftEngine _engine;
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
    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    public ElectricalViewModel(
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

    // Save Dialog
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    private string DefaultProjectName => Calculators[SelectedCalculator];

    // Calculator Selection
    [ObservableProperty] private int _selectedCalculator;

    partial void OnSelectedCalculatorChanged(int value)
    {
        // Reset cost values when switching calculators to prevent state leaking
        if (value != 0)
        {
            CablePrice = 0;
        }
        // Clear results when switching
        HasResult = false;
    }

    public List<string> Calculators => [
        _localization.GetString("VoltageDrop"),
        _localization.GetString("PowerCost"),
        _localization.GetString("OhmsLaw")
    ];

    // Voltage Drop Inputs
    [ObservableProperty] private double _voltage = Defaults.Voltage;
    [ObservableProperty] private double _current = Defaults.Current;
    [ObservableProperty] private double _cableLength = Defaults.CableLength;
    [ObservableProperty] private double _crossSection = Defaults.CrossSection;
    [ObservableProperty] private bool _isCopper = true;

    // Power Cost Inputs
    [ObservableProperty] private double _watts = Defaults.Power;
    [ObservableProperty] private double _hoursPerDay = Defaults.HoursPerDay;
    [ObservableProperty] private double _pricePerKwh = Defaults.PricePerKwh;

    // Ohms Law Inputs
    [ObservableProperty] private string _ohmsVoltage = "";
    [ObservableProperty] private string _ohmsCurrent = "";
    [ObservableProperty] private string _ohmsResistance = "";
    [ObservableProperty] private string _ohmsPower = "";

    // Results
    [ObservableProperty] private VoltageDropResult? _voltageDropResult;
    [ObservableProperty] private PowerCostResult? _powerCostResult;
    [ObservableProperty] private OhmsLawResult? _ohmsLawResult;
    [ObservableProperty] private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    #region Cost Calculation

    // Spannungsabfall: Kabelkosten
    [ObservableProperty]
    private double _cablePrice = 0;

    [ObservableProperty]
    private bool _showCableCost = false;

    public string CableCostDisplay => (ShowCableCost && CablePrice > 0 && CableLength > 0)
        ? $"{_localization.GetString("CableCostLabel")}: {(CableLength * CablePrice):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnCablePriceChanged(double value)
    {
        ShowCableCost = value > 0;
        OnPropertyChanged(nameof(CableCostDisplay));
    }

    partial void OnCableLengthChanged(double value)
    {
        OnPropertyChanged(nameof(CableCostDisplay));
    }

    // Stromkosten: (bereits in der Berechnung enthalten, nur Anzeige verbessern)

    // Ohm: Keine Kostenberechnung nötig (theoretischer Rechner)

    #endregion

    [RelayCommand]
    private async Task Calculate()
    {
        if (IsCalculating) return;

        try
        {
            IsCalculating = true;

            switch (SelectedCalculator)
            {
                case 0: // Voltage Drop
                    if (Voltage <= 0 || Current <= 0 || CableLength <= 0 || CrossSection <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    VoltageDropResult = _engine.CalculateVoltageDrop(Voltage, Current, CableLength, CrossSection, IsCopper);
                    PowerCostResult = null;
                    OhmsLawResult = null;
                    break;

                case 1: // Power Cost
                    if (Watts <= 0 || HoursPerDay <= 0 || PricePerKwh <= 0)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("ValueMustBePositive"));
                        return;
                    }
                    PowerCostResult = _engine.CalculatePowerCost(Watts, HoursPerDay, PricePerKwh);
                    VoltageDropResult = null;
                    OhmsLawResult = null;
                    break;

                case 2: // Ohms Law
                    double? v = string.IsNullOrEmpty(OhmsVoltage) ? null : double.TryParse(OhmsVoltage, out var vv) ? vv : null;
                    double? i = string.IsNullOrEmpty(OhmsCurrent) ? null : double.TryParse(OhmsCurrent, out var ii) ? ii : null;
                    double? r = string.IsNullOrEmpty(OhmsResistance) ? null : double.TryParse(OhmsResistance, out var rr) ? rr : null;
                    double? p = string.IsNullOrEmpty(OhmsPower) ? null : double.TryParse(OhmsPower, out var pp) ? pp : null;

                    var filledCount = (v.HasValue ? 1 : 0) + (i.HasValue ? 1 : 0) + (r.HasValue ? 1 : 0) + (p.HasValue ? 1 : 0);
                    if (filledCount < 2)
                    {
                        HasResult = false;
                        MessageRequested?.Invoke(
                            _localization.GetString("InvalidInputTitle"),
                            _localization.GetString("OhmsLawMinValues") ?? "Enter at least 2 values");
                        return;
                    }

                    OhmsLawResult = _engine.CalculateOhmsLaw(v, i, r, p);
                    VoltageDropResult = null;
                    PowerCostResult = null;
                    break;
            }
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
            string calcType, title;
            Dictionary<string, object> data;

            switch (SelectedCalculator)
            {
                case 0: // Voltage Drop
                    calcType = "VoltageDropCalculator";
                    title = $"{Voltage}V, {Current}A, {CableLength}m";
                    data = new Dictionary<string, object>
                    {
                        ["Voltage"] = Voltage,
                        ["Current"] = Current,
                        ["CableLength"] = CableLength,
                        ["CrossSection"] = CrossSection,
                        ["IsCopper"] = IsCopper,
                        ["Result"] = VoltageDropResult != null ? new Dictionary<string, object>
                        {
                            ["VoltageDrop"] = VoltageDropResult.VoltageDrop,
                            ["PercentDrop"] = VoltageDropResult.PercentDrop,
                            ["IsAcceptable"] = VoltageDropResult.IsAcceptable
                        } : new Dictionary<string, object>()
                    };
                    break;

                case 1: // Power Cost
                    calcType = "PowerCostCalculator";
                    title = $"{Watts}W, {HoursPerDay}{_localization.GetString("HistoryHoursPerDay")}";
                    data = new Dictionary<string, object>
                    {
                        ["Watts"] = Watts,
                        ["HoursPerDay"] = HoursPerDay,
                        ["PricePerKwh"] = PricePerKwh,
                        ["Result"] = PowerCostResult != null ? new Dictionary<string, object>
                        {
                            ["CostPerDay"] = PowerCostResult.CostPerDay,
                            ["CostPerMonth"] = PowerCostResult.CostPerMonth,
                            ["CostPerYear"] = PowerCostResult.CostPerYear
                        } : new Dictionary<string, object>()
                    };
                    break;

                default: // Ohms Law
                    calcType = "OhmsLawCalculator";
                    title = _localization.GetString("OhmsLaw");
                    data = new Dictionary<string, object>
                    {
                        ["OhmsVoltage"] = OhmsVoltage,
                        ["OhmsCurrent"] = OhmsCurrent,
                        ["OhmsResistance"] = OhmsResistance,
                        ["OhmsPower"] = OhmsPower,
                        ["Result"] = OhmsLawResult != null ? new Dictionary<string, object>
                        {
                            ["Voltage"] = OhmsLawResult.Voltage,
                            ["Current"] = OhmsLawResult.Current,
                            ["Resistance"] = OhmsLawResult.Resistance,
                            ["Power"] = OhmsLawResult.Power
                        } : new Dictionary<string, object>()
                    };
                    break;
            }

            await _historyService.AddCalculationAsync(calcType, title, data);
        }
        catch (Exception)
        {
            // Silently ignore history save errors
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Voltage = Defaults.Voltage;
        Current = Defaults.Current;
        CableLength = Defaults.CableLength;
        CrossSection = Defaults.CrossSection;
        IsCopper = true;
        Watts = Defaults.Power;
        HoursPerDay = Defaults.HoursPerDay;
        PricePerKwh = Defaults.PricePerKwh;
        OhmsVoltage = "";
        OhmsCurrent = "";
        OhmsResistance = "";
        OhmsPower = "";
        CablePrice = 0;
        VoltageDropResult = null;
        PowerCostResult = null;
        OhmsLawResult = null;
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
        SaveProjectName = string.Empty;
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
                0 => CalculatorType.VoltageDrop,
                1 => CalculatorType.PowerCost,
                2 => CalculatorType.OhmsLaw,
                _ => CalculatorType.VoltageDrop
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
                // Voltage Drop
                ["Voltage"] = Voltage,
                ["Current"] = Current,
                ["CableLength"] = CableLength,
                ["CrossSection"] = CrossSection,
                ["IsCopper"] = IsCopper,
                ["CablePrice"] = CablePrice,
                // Power Cost
                ["Watts"] = Watts,
                ["HoursPerDay"] = HoursPerDay,
                ["PricePerKwh"] = PricePerKwh,
                // Ohms Law
                ["OhmsVoltage"] = OhmsVoltage,
                ["OhmsCurrent"] = OhmsCurrent,
                ["OhmsResistance"] = OhmsResistance,
                ["OhmsPower"] = OhmsPower
            };

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

            // Voltage Drop
            Voltage = project.GetValue("Voltage", Defaults.Voltage);
            Current = project.GetValue("Current", Defaults.Current);
            CableLength = project.GetValue("CableLength", Defaults.CableLength);
            CrossSection = project.GetValue("CrossSection", Defaults.CrossSection);
            IsCopper = project.GetValue("IsCopper", true);
            CablePrice = project.GetValue("CablePrice", 0.0);

            // Power Cost
            Watts = project.GetValue("Watts", Defaults.Power);
            HoursPerDay = project.GetValue("HoursPerDay", Defaults.HoursPerDay);
            PricePerKwh = project.GetValue("PricePerKwh", Defaults.PricePerKwh);

            // Ohms Law
            OhmsVoltage = project.GetValue("OhmsVoltage", "") ?? "";
            OhmsCurrent = project.GetValue("OhmsCurrent", "") ?? "";
            OhmsResistance = project.GetValue("OhmsResistance", "") ?? "";
            OhmsPower = project.GetValue("OhmsPower", "") ?? "";

            await Calculate();
        }
        catch (Exception)
        {
            // Silently ignore load errors
        }
    }

    [RelayCommand]
    private async Task ExportMaterialList()
    {
        if (!HasResult) return;

        try
        {
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
                case 0 when VoltageDropResult != null:
                    inputs[_localization.GetString("Voltage") ?? "Voltage"] = $"{Voltage} V";
                    inputs[_localization.GetString("Current") ?? "Current"] = $"{Current} A";
                    inputs[_localization.GetString("CableLength") ?? "Cable length"] = $"{CableLength} m";
                    inputs[_localization.GetString("CrossSection") ?? "Cross-section"] = $"{CrossSection} mm\u00b2";
                    results[_localization.GetString("InfoVoltageDrop") ?? "Voltage drop"] = $"{VoltageDropResult.VoltageDrop:F2} V ({VoltageDropResult.PercentDrop:F1} %)";
                    break;
                case 1 when PowerCostResult != null:
                    inputs[_localization.GetString("Power") ?? "Power"] = $"{Watts} W";
                    inputs[_localization.GetString("HoursPerDay") ?? "hrs/day"] = $"{HoursPerDay}";
                    inputs[_localization.GetString("PricePerKwh") ?? "EUR/kWh"] = $"{PricePerKwh:F2}";
                    results[_localization.GetString("CostPerDay") ?? "Cost/day"] = $"{PowerCostResult.CostPerDay:F2} {_localization.GetString("CurrencySymbol")}";
                    results[_localization.GetString("CostPerMonth") ?? "Cost/month"] = $"{PowerCostResult.CostPerMonth:F2} {_localization.GetString("CurrencySymbol")}";
                    results[_localization.GetString("CostPerYear") ?? "Cost/year"] = $"{PowerCostResult.CostPerYear:F2} {_localization.GetString("CurrencySymbol")}";
                    break;
                case 2 when OhmsLawResult != null:
                    inputs[_localization.GetString("VoltageULabel") ?? "Voltage U"] = OhmsVoltage;
                    inputs[_localization.GetString("CurrentILabel") ?? "Current I"] = OhmsCurrent;
                    inputs[_localization.GetString("ResistanceRLabel") ?? "Resistance R"] = OhmsResistance;
                    results[_localization.GetString("VoltageULabel") ?? "U"] = $"{OhmsLawResult.Voltage:F2} V";
                    results[_localization.GetString("CurrentILabel") ?? "I"] = $"{OhmsLawResult.Current:F3} A";
                    results[_localization.GetString("ResistanceRLabel") ?? "R"] = $"{OhmsLawResult.Resistance:F2} \u03a9";
                    results[_localization.GetString("PowerPLabel") ?? "P"] = $"{OhmsLawResult.Power:F2} W";
                    break;
                default:
                    return;
            }

            var path = await _exportService.ExportToPdfAsync(calcType, inputs, results);
            await _fileShareService.ShareFileAsync(path, _localization.GetString("ShareMaterialList") ?? "Share", "application/pdf");
            MessageRequested?.Invoke(_localization.GetString("Success") ?? "Success", _localization.GetString("PdfExportSuccess") ?? "PDF exported!");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Error", _localization.GetString("PdfExportFailed") ?? "Export failed.");
        }
    }
}
