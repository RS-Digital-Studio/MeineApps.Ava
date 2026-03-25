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
/// Stundenrechner: Stundensatz × Arbeitszeit × Aufschlag → Kundenpreis mit MwSt.
/// Berechnet Lohnkosten für Handwerker inkl. Pause, Overhead und Mehrwertsteuer.
/// </summary>
public sealed partial class HourlyRateViewModel : ViewModelBase, IDisposable, ICalculatorViewModel
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

    #region Eingaben

    [ObservableProperty] private double _hourlyRate = 45.0;
    [ObservableProperty] private double _workHours = 8.0;
    [ObservableProperty] private double _breakMinutes = 30.0;
    [ObservableProperty] private double _overheadPercent = 20.0;
    [ObservableProperty] private double _vatPercent = 19.0;
    [ObservableProperty] private int _workers = 1;

    partial void OnHourlyRateChanged(double value) => ScheduleAutoCalculate();
    partial void OnWorkHoursChanged(double value) => ScheduleAutoCalculate();
    partial void OnBreakMinutesChanged(double value) => ScheduleAutoCalculate();
    partial void OnOverheadPercentChanged(double value) => ScheduleAutoCalculate();
    partial void OnVatPercentChanged(double value) => ScheduleAutoCalculate();
    partial void OnWorkersChanged(int value) => ScheduleAutoCalculate();

    #endregion

    #region Ergebnisse

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private double _netWorkHours;
    [ObservableProperty] private double _netLaborCost;
    [ObservableProperty] private double _overheadAmount;
    [ObservableProperty] private double _totalNet;
    [ObservableProperty] private double _vatAmount;
    [ObservableProperty] private double _totalGross;

    #endregion

    #region Lokalisierte Texte

    public string PageTitle => _localization.GetString("CalcHourlyRate") ?? "Stundenrechner";
    public string HourlyRateLabel => _localization.GetString("HourlyRate") ?? "Stundensatz (€/h)";
    public string WorkHoursLabel => _localization.GetString("WorkHours") ?? "Arbeitszeit (h)";
    public string BreakMinutesLabel => _localization.GetString("BreakMinutes") ?? "Pause (Min)";
    public string OverheadPercentLabel => _localization.GetString("OverheadPercent") ?? "Aufschlag (%)";
    public string VatPercentLabel => _localization.GetString("VatPercent") ?? "MwSt (%)";
    public string WorkerCountLabel => _localization.GetString("WorkerCount") ?? "Mitarbeiter";
    public string NetWorkHoursLabel => _localization.GetString("NetWorkHours") ?? "Effektive Arbeitszeit";
    public string NetLaborCostLabel => _localization.GetString("NetLaborCost") ?? "Lohnkosten netto";
    public string OverheadAmountLabel => _localization.GetString("OverheadAmount") ?? "Aufschlag";
    public string TotalNetLabel => _localization.GetString("TotalNet") ?? "Gesamt netto";
    public string VatAmountLabel => _localization.GetString("VatAmount") ?? "MwSt-Betrag";
    public string TotalGrossLabel => _localization.GetString("TotalGross") ?? "Gesamt brutto";

    #endregion

    #region Projekt speichern

    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = "";

    #endregion

    public HourlyRateViewModel(
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
        if (WorkHours <= 0 || HourlyRate <= 0 || Workers <= 0)
        {
            HasResult = false;
            return;
        }

        // Effektive Arbeitszeit = (Stunden - Pause) × Mitarbeiter
        var breakHours = BreakMinutes / 60.0;
        NetWorkHours = Math.Max(0, (WorkHours - breakHours) * Workers);

        // Lohnkosten netto
        NetLaborCost = NetWorkHours * HourlyRate;

        // Aufschlag
        OverheadAmount = NetLaborCost * OverheadPercent / 100.0;

        // Gesamt netto
        TotalNet = NetLaborCost + OverheadAmount;

        // MwSt
        VatAmount = TotalNet * VatPercent / 100.0;

        // Gesamt brutto (Kundenpreis)
        TotalGross = TotalNet + VatAmount;

        HasResult = true;
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    [RelayCommand]
    private void OpenSaveDialog()
    {
        if (!HasResult)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler",
                _localization.GetString("CalculateFirst") ?? "Bitte zuerst berechnen");
            return;
        }
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
            CalculatorType = Models.CalculatorType.HourlyRate
        };

        project.SetData(new Dictionary<string, object>
        {
            ["HourlyRate"] = HourlyRate,
            ["WorkHours"] = WorkHours,
            ["BreakMinutes"] = BreakMinutes,
            ["OverheadPercent"] = OverheadPercent,
            ["VatPercent"] = VatPercent,
            ["Workers"] = Workers
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
                [HourlyRateLabel] = $"{HourlyRate:F2} €/h",
                [WorkHoursLabel] = $"{WorkHours:F1} h",
                [BreakMinutesLabel] = $"{BreakMinutes:F0} min",
                [OverheadPercentLabel] = $"{OverheadPercent:F1} %",
                [VatPercentLabel] = $"{VatPercent:F1} %",
                [WorkerCountLabel] = $"{Workers}"
            };

            var results = new Dictionary<string, string>
            {
                [NetWorkHoursLabel] = $"{NetWorkHours:F2} h",
                [NetLaborCostLabel] = $"{NetLaborCost:F2} €",
                [OverheadAmountLabel] = $"{OverheadAmount:F2} €",
                [TotalNetLabel] = $"{TotalNet:F2} €",
                [VatAmountLabel] = $"{VatAmount:F2} €",
                [TotalGrossLabel] = $"{TotalGross:F2} €"
            };

            var path = await _exportService.ExportToPdfAsync(PageTitle, inputs, results);
            await _fileShareService.ShareFileAsync(path, PageTitle, "application/pdf");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler", ex.Message);
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
                [HourlyRateLabel] = $"{HourlyRate:F2} €/h",
                [WorkHoursLabel] = $"{WorkHours:F1} h",
                [BreakMinutesLabel] = $"{BreakMinutes:F0} min",
                [OverheadPercentLabel] = $"{OverheadPercent:F1} %",
                [VatPercentLabel] = $"{VatPercent:F1} %",
                [WorkerCountLabel] = $"{Workers}"
            };

            var results = new Dictionary<string, string>
            {
                [NetWorkHoursLabel] = $"{NetWorkHours:F2} h",
                [NetLaborCostLabel] = $"{NetLaborCost:F2} €",
                [OverheadAmountLabel] = $"{OverheadAmount:F2} €",
                [TotalNetLabel] = $"{TotalNet:F2} €",
                [VatAmountLabel] = $"{VatAmount:F2} €",
                [TotalGrossLabel] = $"{TotalGross:F2} €"
            };

            var path = await _exportService.ExportToCsvAsync(PageTitle, inputs, results);
            await _fileShareService.ShareFileAsync(path, PageTitle, "text/csv");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler", ex.Message);
        }
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (!HasResult) return;
        var text = $"{TotalGrossLabel}: {TotalGross:F2} €\n{TotalNetLabel}: {TotalNet:F2} €";
        ClipboardRequested?.Invoke(text);
        FloatingTextRequested?.Invoke(
            _localization.GetString("CopiedToClipboard") ?? "Kopiert!", "info");
    }

    /// <summary>Lädt Werte aus einem gespeicherten Projekt</summary>
    public async Task LoadFromProjectIdAsync(string projectId)
    {
        var project = await _projectService.LoadProjectAsync(projectId);
        if (project == null) return;

        HourlyRate = project.GetValue<double>("HourlyRate", 45.0);
        WorkHours = project.GetValue<double>("WorkHours", 8.0);
        BreakMinutes = project.GetValue<double>("BreakMinutes", 30.0);
        OverheadPercent = project.GetValue<double>("OverheadPercent", 20.0);
        VatPercent = project.GetValue<double>("VatPercent", 19.0);
        Workers = project.GetValue<int>("Workers", 1);
        Calculate();
    }

    public void Cleanup() => _debounceTimer?.Dispose();

    public void Dispose() => _debounceTimer?.Dispose();
}
