using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using HandwerkerRechner.ViewModels;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerRechner.ViewModels.Premium;

/// <summary>
/// Material-Vergleich: Vergleicht Produkt A vs. B bei gleicher Fläche.
/// Zeigt Gesamtkosten, Ersparnis und günstigere Option.
/// </summary>
public sealed partial class MaterialCompareViewModel : ViewModelBase, IDisposable, ICalculatorViewModel
{
    private readonly ILocalizationService _localization;
    private readonly IProjectService _projectService;
    private readonly IMaterialExportService _exportService;
    private readonly IFileShareService _fileShareService;
    private Timer? _debounceTimer;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;

    #region Gemeinsame Eingabe

    [ObservableProperty] private double _area = 20.0;
    partial void OnAreaChanged(double value) => ScheduleAutoCalculate();

    #endregion

    #region Produkt A

    [ObservableProperty] private string _productAName = "Produkt A";
    [ObservableProperty] private double _priceA = 25.0;
    [ObservableProperty] private double _consumptionA = 1.0;
    [ObservableProperty] private double _wasteA = 10.0;

    partial void OnPriceAChanged(double value) => ScheduleAutoCalculate();
    partial void OnConsumptionAChanged(double value) => ScheduleAutoCalculate();
    partial void OnWasteAChanged(double value) => ScheduleAutoCalculate();

    #endregion

    #region Produkt B

    [ObservableProperty] private string _productBName = "Produkt B";
    [ObservableProperty] private double _priceB = 35.0;
    [ObservableProperty] private double _consumptionB = 0.8;
    [ObservableProperty] private double _wasteB = 10.0;

    partial void OnPriceBChanged(double value) => ScheduleAutoCalculate();
    partial void OnConsumptionBChanged(double value) => ScheduleAutoCalculate();
    partial void OnWasteBChanged(double value) => ScheduleAutoCalculate();

    #endregion

    #region Ergebnisse

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private double _totalCostA;
    [ObservableProperty] private double _totalCostB;
    [ObservableProperty] private double _savingsAmount;
    [ObservableProperty] private double _savingsPercent;
    [ObservableProperty] private string _cheaperProduct = "";
    [ObservableProperty] private bool _isAcheaper;

    #endregion

    #region Lokalisierte Texte

    public string PageTitle => _localization.GetString("CalcMaterialCompare") ?? "Material-Vergleich";
    public string AreaLabel => _localization.GetString("AreaSqm") ?? "Fläche (m²)";
    public string ProductALabel => _localization.GetString("ProductA") ?? "Produkt A";
    public string ProductBLabel => _localization.GetString("ProductB") ?? "Produkt B";
    public string ProductNameLabel => _localization.GetString("ProductName") ?? "Bezeichnung";
    public string PricePerUnitLabel => _localization.GetString("PricePerUnit") ?? "Preis je Einheit (€)";
    public string ConsumptionLabel => _localization.GetString("ConsumptionPerSqm") ?? "Verbrauch je m²";
    public string WasteLabel => _localization.GetString("WastePercent") ?? "Verschnitt (%)";
    public string TotalCostLabel => _localization.GetString("TotalCost") ?? "Gesamtkosten";
    public string SavingsLabel => _localization.GetString("Savings") ?? "Ersparnis";
    public string CheaperOptionLabel => _localization.GetString("CheaperOption") ?? "Günstigere Option";

    #endregion

    #region Projekt speichern

    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = "";

    #endregion

    public MaterialCompareViewModel(
        ILocalizationService localization,
        IProjectService projectService,
        IMaterialExportService exportService,
        IFileShareService fileShareService)
    {
        _localization = localization;
        _projectService = projectService;
        _exportService = exportService;
        _fileShareService = fileShareService;
    }

    private void ScheduleAutoCalculate()
    {
        if (_debounceTimer == null)
            _debounceTimer = new Timer(_ => Dispatcher.UIThread.Post(Calculate), null, 300, Timeout.Infinite);
        else
            _debounceTimer.Change(300, Timeout.Infinite);
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Area <= 0 || PriceA <= 0 || PriceB <= 0 || ConsumptionA <= 0 || ConsumptionB <= 0)
        {
            HasResult = false;
            return;
        }

        // Gesamtkosten = Fläche × Verbrauch/m² × (1 + Verschnitt%) × Preis
        TotalCostA = Area * ConsumptionA * (1 + WasteA / 100) * PriceA;
        TotalCostB = Area * ConsumptionB * (1 + WasteB / 100) * PriceB;

        SavingsAmount = Math.Abs(TotalCostA - TotalCostB);
        var expensive = Math.Max(TotalCostA, TotalCostB);
        SavingsPercent = expensive > 0 ? (SavingsAmount / expensive) * 100 : 0;

        IsAcheaper = TotalCostA <= TotalCostB;
        CheaperProduct = IsAcheaper ? ProductAName : ProductBName;

        HasResult = true;
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    [RelayCommand]
    private void OpenSaveDialog()
    {
        if (!HasResult) return;
        SaveProjectName = "";
        ShowSaveDialog = true;
    }

    [RelayCommand]
    private async Task ConfirmSaveProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(SaveProjectName)) return;

        var project = new Models.Project
        {
            Name = SaveProjectName.Trim(),
            CalculatorType = Models.CalculatorType.MaterialCompare
        };

        project.SetData(new Dictionary<string, object>
        {
            ["Area"] = Area,
            ["ProductAName"] = ProductAName,
            ["PriceA"] = PriceA,
            ["ConsumptionA"] = ConsumptionA,
            ["WasteA"] = WasteA,
            ["ProductBName"] = ProductBName,
            ["PriceB"] = PriceB,
            ["ConsumptionB"] = ConsumptionB,
            ["WasteB"] = WasteB
        });

        await _projectService.SaveProjectAsync(project);
        ShowSaveDialog = false;
        FloatingTextRequested?.Invoke(
            _localization.GetString("ProjectSaved") ?? "Projekt gespeichert!", "success");
    }

    [RelayCommand]
    private void CancelSaveDialog() => ShowSaveDialog = false;

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (!HasResult) return;
        try
        {
            var inputs = new Dictionary<string, string>
            {
                [AreaLabel] = $"{Area:F1} m²",
                [$"{ProductAName} - {PricePerUnitLabel}"] = $"{PriceA:F2} €",
                [$"{ProductAName} - {ConsumptionLabel}"] = $"{ConsumptionA:F2}",
                [$"{ProductAName} - {WasteLabel}"] = $"{WasteA:F1} %",
                [$"{ProductBName} - {PricePerUnitLabel}"] = $"{PriceB:F2} €",
                [$"{ProductBName} - {ConsumptionLabel}"] = $"{ConsumptionB:F2}",
                [$"{ProductBName} - {WasteLabel}"] = $"{WasteB:F1} %",
            };

            var results = new Dictionary<string, string>
            {
                [$"{ProductAName} {TotalCostLabel}"] = $"{TotalCostA:F2} €",
                [$"{ProductBName} {TotalCostLabel}"] = $"{TotalCostB:F2} €",
                [SavingsLabel] = $"{SavingsAmount:F2} € ({SavingsPercent:F1}%)",
                [CheaperOptionLabel] = CheaperProduct
            };

            var path = await _exportService.ExportToPdfAsync(PageTitle, inputs, results);
            await _fileShareService.ShareFileAsync(path, PageTitle, "application/pdf");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Fehler", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (!HasResult) return;
        try
        {
            var inputs = new Dictionary<string, string>
            {
                [AreaLabel] = $"{Area:F1} m²",
                [$"{ProductAName} - {PricePerUnitLabel}"] = $"{PriceA:F2} €",
                [$"{ProductAName} - {ConsumptionLabel}"] = $"{ConsumptionA:F2}",
                [$"{ProductAName} - {WasteLabel}"] = $"{WasteA:F1} %",
                [$"{ProductBName} - {PricePerUnitLabel}"] = $"{PriceB:F2} €",
                [$"{ProductBName} - {ConsumptionLabel}"] = $"{ConsumptionB:F2}",
                [$"{ProductBName} - {WasteLabel}"] = $"{WasteB:F1} %",
            };

            var results = new Dictionary<string, string>
            {
                [$"{ProductAName} {TotalCostLabel}"] = $"{TotalCostA:F2} €",
                [$"{ProductBName} {TotalCostLabel}"] = $"{TotalCostB:F2} €",
                [SavingsLabel] = $"{SavingsAmount:F2} € ({SavingsPercent:F1}%)",
                [CheaperOptionLabel] = CheaperProduct
            };

            var path = await _exportService.ExportToCsvAsync(PageTitle, inputs, results);
            await _fileShareService.ShareFileAsync(path, PageTitle, "text/csv");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Fehler", ex.Message);
        }
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (!HasResult) return;
        var text = $"{ProductAName}: {TotalCostA:F2} €\n{ProductBName}: {TotalCostB:F2} €\n{SavingsLabel}: {SavingsAmount:F2} €";
        ClipboardRequested?.Invoke(text);
        FloatingTextRequested?.Invoke(_localization.GetString("CopiedToClipboard") ?? "Kopiert!", "info");
    }

    public async Task LoadFromProjectIdAsync(string projectId)
    {
        var project = await _projectService.LoadProjectAsync(projectId);
        if (project == null) return;

        Area = project.GetValue<double>("Area", 20.0);
        ProductAName = project.GetValue<string>("ProductAName", "Produkt A") ?? "Produkt A";
        PriceA = project.GetValue<double>("PriceA", 25.0);
        ConsumptionA = project.GetValue<double>("ConsumptionA", 1.0);
        WasteA = project.GetValue<double>("WasteA", 10.0);
        ProductBName = project.GetValue<string>("ProductBName", "Produkt B") ?? "Produkt B";
        PriceB = project.GetValue<double>("PriceB", 35.0);
        ConsumptionB = project.GetValue<double>("ConsumptionB", 0.8);
        WasteB = project.GetValue<double>("WasteB", 10.0);
        Calculate();
    }

    public void Cleanup() => _debounceTimer?.Dispose();
    public void Dispose() => _debounceTimer?.Dispose();
}
