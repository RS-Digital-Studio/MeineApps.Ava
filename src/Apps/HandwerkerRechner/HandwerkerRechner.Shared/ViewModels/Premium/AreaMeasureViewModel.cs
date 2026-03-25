using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using HandwerkerRechner.ViewModels;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace HandwerkerRechner.ViewModels.Premium;

/// <summary>
/// Aufmaß-Rechner: Berechnet Flächen für komplexe Raumformen.
/// Unterstützt Rechteck, L-Form, T-Form, Trapez, Dreieck und Kreis.
/// Teilflächen können summiert und kopiert werden.
/// </summary>
public sealed partial class AreaMeasureViewModel : ViewModelBase, IDisposable, ICalculatorViewModel
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

    #region Form-Auswahl

    [ObservableProperty] private int _selectedShapeIndex;
    partial void OnSelectedShapeIndexChanged(int value) => ScheduleAutoCalculate();

    /// <summary>Verfügbare Formen mit lokalisierten Namen</summary>
    public List<string> ShapeNames =>
    [
        _localization.GetString("ShapeRectangle") ?? "Rechteck",
        _localization.GetString("ShapeLShape") ?? "L-Form",
        _localization.GetString("ShapeTShape") ?? "T-Form",
        _localization.GetString("ShapeTrapezoid") ?? "Trapez",
        _localization.GetString("ShapeTriangle") ?? "Dreieck",
        _localization.GetString("ShapeCircle") ?? "Kreis"
    ];

    #endregion

    #region Eingaben (kontextabhängig je nach Form)

    [ObservableProperty] private double _dimension1 = 5.0;  // Länge / Basis / Durchmesser
    [ObservableProperty] private double _dimension2 = 4.0;  // Breite / Höhe
    [ObservableProperty] private double _dimension3 = 2.0;  // Ausschnitt-Länge (L/T-Form)
    [ObservableProperty] private double _dimension4 = 1.5;  // Ausschnitt-Breite (L/T-Form)
    [ObservableProperty] private double _dimension5 = 3.0;  // Parallelseite (Trapez)

    partial void OnDimension1Changed(double value) => ScheduleAutoCalculate();
    partial void OnDimension2Changed(double value) => ScheduleAutoCalculate();
    partial void OnDimension3Changed(double value) => ScheduleAutoCalculate();
    partial void OnDimension4Changed(double value) => ScheduleAutoCalculate();
    partial void OnDimension5Changed(double value) => ScheduleAutoCalculate();

    // Sichtbarkeit der Eingabefelder je nach Form
    public bool ShowDim3 => SelectedShapeIndex is 1 or 2; // L-Form, T-Form
    public bool ShowDim4 => SelectedShapeIndex is 1 or 2;
    public bool ShowDim5 => SelectedShapeIndex == 3; // Trapez
    public bool ShowDim2 => SelectedShapeIndex != 5; // Nicht bei Kreis

    // Kontextabhängige Labels
    public string Dim1Label => SelectedShapeIndex switch
    {
        5 => _localization.GetString("Diameter") ?? "Durchmesser (m)",
        3 => _localization.GetString("BaseA") ?? "Seite a (m)",
        _ => _localization.GetString("Length") ?? "Länge (m)"
    };

    public string Dim2Label => SelectedShapeIndex switch
    {
        4 => _localization.GetString("HeightTriangle") ?? "Höhe (m)",
        _ => _localization.GetString("Width") ?? "Breite (m)"
    };

    #endregion

    #region Ergebnisse

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private double _currentShapeArea;
    [ObservableProperty] private double _totalArea;
    public ObservableCollection<AreaPart> AreaParts { get; } = [];
    public bool HasParts => AreaParts.Count > 0;

    #endregion

    #region Lokalisierte Texte

    public string PageTitle => _localization.GetString("CalcAreaMeasure") ?? "Aufmaß-Rechner";
    public string TotalAreaLabel => _localization.GetString("TotalArea") ?? "Gesamtfläche";
    public string AddAreaLabel => _localization.GetString("AddArea") ?? "Teilfläche hinzufügen";
    public string CopyAreaLabel => _localization.GetString("CopyArea") ?? "Fläche kopieren";
    public string AreaPartsLabel => _localization.GetString("AreaParts") ?? "Teilflächen";
    public string CurrentAreaLabel => _localization.GetString("CurrentArea") ?? "Aktuelle Fläche";

    #endregion

    #region Projekt speichern

    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = "";

    #endregion

    public AreaMeasureViewModel(
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
        // Labels aktualisieren wenn Form wechselt
        OnPropertyChanged(nameof(ShowDim2));
        OnPropertyChanged(nameof(ShowDim3));
        OnPropertyChanged(nameof(ShowDim4));
        OnPropertyChanged(nameof(ShowDim5));
        OnPropertyChanged(nameof(Dim1Label));
        OnPropertyChanged(nameof(Dim2Label));

        if (_debounceTimer == null)
            _debounceTimer = new Timer(_ => Dispatcher.UIThread.Post(Calculate), null, 300, Timeout.Infinite);
        else
            _debounceTimer.Change(300, Timeout.Infinite);
    }

    [RelayCommand]
    private void Calculate()
    {
        CurrentShapeArea = SelectedShapeIndex switch
        {
            0 => CalculateRectangle(),
            1 => CalculateLShape(),
            2 => CalculateTShape(),
            3 => CalculateTrapezoid(),
            4 => CalculateTriangle(),
            5 => CalculateCircle(),
            _ => 0
        };

        HasResult = CurrentShapeArea > 0;
        RecalculateTotal();
    }

    private double CalculateRectangle() => Dimension1 * Dimension2;

    private double CalculateLShape()
    {
        // Gesamtrechteck minus Eckausschnitt
        var total = Dimension1 * Dimension2;
        var cutout = Dimension3 * Dimension4;
        return Math.Max(0, total - cutout);
    }

    private double CalculateTShape()
    {
        // Mittelteil + Querbalken (vereinfacht: T aus 2 Rechtecken)
        var stem = Dimension1 * Dimension2;
        var crossbar = Dimension3 * Dimension4;
        return stem + crossbar;
    }

    private double CalculateTrapezoid()
    {
        // (a + b) / 2 × h
        return (Dimension1 + Dimension5) / 2.0 * Dimension2;
    }

    private double CalculateTriangle()
    {
        // Basis × Höhe / 2
        return Dimension1 * Dimension2 / 2.0;
    }

    private double CalculateCircle()
    {
        // π × r²
        var radius = Dimension1 / 2.0;
        return Math.PI * radius * radius;
    }

    [RelayCommand]
    private void AddCurrentShape()
    {
        if (CurrentShapeArea <= 0) return;

        var shapeName = SelectedShapeIndex < ShapeNames.Count ? ShapeNames[SelectedShapeIndex] : "?";
        AreaParts.Add(new AreaPart(shapeName, CurrentShapeArea));
        OnPropertyChanged(nameof(HasParts));
        RecalculateTotal();

        FloatingTextRequested?.Invoke(
            $"+{CurrentShapeArea:F2} m²", "success");
    }

    [RelayCommand]
    private void RemovePart(AreaPart? part)
    {
        if (part == null) return;
        AreaParts.Remove(part);
        OnPropertyChanged(nameof(HasParts));
        RecalculateTotal();
    }

    [RelayCommand]
    private void ClearParts()
    {
        AreaParts.Clear();
        OnPropertyChanged(nameof(HasParts));
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        TotalArea = AreaParts.Sum(p => p.Area) + (HasResult ? CurrentShapeArea : 0);
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    [RelayCommand]
    private void CopyTotalArea()
    {
        var effectiveTotal = AreaParts.Count > 0 ? AreaParts.Sum(p => p.Area) : CurrentShapeArea;
        ClipboardRequested?.Invoke($"{effectiveTotal:F2}");
        FloatingTextRequested?.Invoke(
            $"{effectiveTotal:F2} m² " + (_localization.GetString("CopiedToClipboard") ?? "kopiert!"), "info");
    }

    [RelayCommand]
    private void OpenSaveDialog()
    {
        if (!HasResult && AreaParts.Count == 0) return;
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
            CalculatorType = Models.CalculatorType.AreaMeasure
        };

        project.SetData(new Dictionary<string, object>
        {
            ["SelectedShapeIndex"] = SelectedShapeIndex,
            ["Dimension1"] = Dimension1,
            ["Dimension2"] = Dimension2,
            ["Dimension3"] = Dimension3,
            ["Dimension4"] = Dimension4,
            ["Dimension5"] = Dimension5,
            ["CurrentShapeArea"] = CurrentShapeArea,
            ["TotalArea"] = TotalArea
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
        if (!HasResult && AreaParts.Count == 0) return;
        try
        {
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("Shape") ?? "Form"] = SelectedShapeIndex < ShapeNames.Count ? ShapeNames[SelectedShapeIndex] : "?",
                [Dim1Label] = $"{Dimension1:F2} m"
            };
            if (ShowDim2) inputs[Dim2Label] = $"{Dimension2:F2} m";

            var results = new Dictionary<string, string>
            {
                [CurrentAreaLabel] = $"{CurrentShapeArea:F2} m²",
                [TotalAreaLabel] = $"{TotalArea:F2} m²"
            };

            for (var i = 0; i < AreaParts.Count; i++)
                results[$"{AreaPartsLabel} #{i + 1} ({AreaParts[i].Label})"] = $"{AreaParts[i].Area:F2} m²";

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
        if (!HasResult && AreaParts.Count == 0) return;
        try
        {
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("Shape") ?? "Form"] = SelectedShapeIndex < ShapeNames.Count ? ShapeNames[SelectedShapeIndex] : "?",
                [Dim1Label] = $"{Dimension1:F2} m"
            };
            if (ShowDim2) inputs[Dim2Label] = $"{Dimension2:F2} m";

            var results = new Dictionary<string, string>
            {
                [CurrentAreaLabel] = $"{CurrentShapeArea:F2} m²",
                [TotalAreaLabel] = $"{TotalArea:F2} m²"
            };

            for (var i = 0; i < AreaParts.Count; i++)
                results[$"{AreaPartsLabel} #{i + 1} ({AreaParts[i].Label})"] = $"{AreaParts[i].Area:F2} m²";

            var path = await _exportService.ExportToCsvAsync(PageTitle, inputs, results);
            await _fileShareService.ShareFileAsync(path, PageTitle, "text/csv");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Fehler", ex.Message);
        }
    }

    public async Task LoadFromProjectIdAsync(string projectId)
    {
        var project = await _projectService.LoadProjectAsync(projectId);
        if (project == null) return;

        SelectedShapeIndex = project.GetValue<int>("SelectedShapeIndex");
        Dimension1 = project.GetValue<double>("Dimension1", 5.0);
        Dimension2 = project.GetValue<double>("Dimension2", 4.0);
        Dimension3 = project.GetValue<double>("Dimension3", 2.0);
        Dimension4 = project.GetValue<double>("Dimension4", 1.5);
        Dimension5 = project.GetValue<double>("Dimension5", 3.0);
        Calculate();
    }

    public void Cleanup() => _debounceTimer?.Dispose();
    public void Dispose() => _debounceTimer?.Dispose();
}

/// <summary>Eine Teilfläche im Aufmaß-Rechner</summary>
public record AreaPart(string Label, double Area);
