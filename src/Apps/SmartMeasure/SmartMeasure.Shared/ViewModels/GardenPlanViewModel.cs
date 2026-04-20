using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Graphics;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>2D-Gartenplanung: Draufsicht + Zeichenwerkzeuge + Materialliste</summary>
public partial class GardenPlanViewModel : ViewModelBase
{
    private readonly IMeasurementService _measurementService;
    private readonly ICoordinateService _coordinateService;
    private readonly IGardenPlanService _gardenPlanService;

    public GardenPlanRenderer Renderer { get; }

    // Aktuelle metrische Koordinaten
    private double[] _x = [];
    private double[] _y = [];
    private double[] _z = [];
    private string[]? _labels;

    public double[] X => _x;
    public double[] Y => _y;
    public double[] Z => _z;
    public string[]? Labels => _labels;

    // Gartenelemente
    public ObservableCollection<GardenElement> Elements { get; } = [];

    // Materialliste
    public ObservableCollection<MaterialEstimate> Materials { get; } = [];

    // Zeichenmodus
    [ObservableProperty] private GardenElementType _selectedTool = GardenElementType.Weg;
    [ObservableProperty] private string _selectedMaterial = "Pflaster";
    [ObservableProperty] private float _selectedWidth = 1.0f;
    [ObservableProperty] private float _selectedHeight = 0.5f;
    [ObservableProperty] private string _selectedSubType = string.Empty;
    [ObservableProperty] private bool _isDrawing;
    [ObservableProperty] private int _drawingPointCount;

    // Aktuell gezeichnetes Element (Punkte werden gesammelt)
    private readonly List<(double x, double y)> _currentDrawingPoints = [];

    /// <summary>Aktuelle Zeichnungspunkte fuer Vorschau im Renderer</summary>
    public IReadOnlyList<(double x, double y)> CurrentDrawingPoints => _currentDrawingPoints;

    // Aktuelle Projekt-ID (wird von MainViewModel gesetzt)
    public int CurrentProjectId { get; set; }

    // Undo/Redo
    private readonly Stack<GardenElement> _undoStack = new();
    [ObservableProperty] private bool _canUndo;

    // Statistiken
    [ObservableProperty] private string _totalAreaText = "—";

    /// <summary>Canvas muss neu gezeichnet werden</summary>
    public event Action? InvalidateRequested;

    private readonly IProjectService _projectService;

    public GardenPlanViewModel(
        IMeasurementService measurementService,
        ICoordinateService coordinateService,
        IGardenPlanService gardenPlanService,
        IProjectService projectService)
    {
        _measurementService = measurementService;
        _coordinateService = coordinateService;
        _gardenPlanService = gardenPlanService;
        _projectService = projectService;
        Renderer = new GardenPlanRenderer(gardenPlanService);

        _measurementService.PointAdded += _ => UpdateCoordinates();
        _measurementService.PointsReset += UpdateCoordinates;
    }

    /// <summary>Gartenelemente aus der Datenbank laden (nach AR-Transfer oder Projekt-Wechsel)</summary>
    public async Task LoadElementsFromProjectAsync(int projectId)
    {
        var dbElements = await _projectService.GetGardenElementsAsync(projectId);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Elements.Clear();
            foreach (var elem in dbElements)
                Elements.Add(elem);

            RecalculateMaterials();
            InvalidateRequested?.Invoke();
        });
    }

    /// <summary>Koordinaten aus Messpunkten aktualisieren</summary>
    public void UpdateCoordinates()
    {
        var points = _measurementService.CurrentPoints;
        if (points.Count == 0)
        {
            _x = [];
            _y = [];
            _z = [];
            _labels = null;
            InvalidateRequested?.Invoke();
            return;
        }

        var lats = points.Select(p => p.Latitude).ToArray();
        var lons = points.Select(p => p.Longitude).ToArray();
        var alts = points.Select(p => p.Altitude).ToArray();

        (_x, _y, _z) = _coordinateService.ToLocalMetric(lats, lons, alts);
        _labels = points.Select(p => p.Label ?? string.Empty).ToArray();

        InvalidateRequested?.Invoke();
    }

    /// <summary>Werkzeug wechseln (wird von Toolbar-Buttons aufgerufen)</summary>
    [RelayCommand]
    private void SelectTool(string toolName)
    {
        if (!Enum.TryParse<GardenElementType>(toolName, out var type)) return;

        SelectedTool = type;

        // Standard-Material je nach Werkzeug setzen
        SelectedMaterial = type switch
        {
            GardenElementType.Weg => "Pflaster",
            GardenElementType.Beet => "Erde",
            GardenElementType.Rasen => "Rasen",
            GardenElementType.Mauer => "Naturstein",
            GardenElementType.Zaun => "Holz",
            GardenElementType.Terrasse => "Holz",
            _ => "Pflaster"
        };

        // Standard-Dimensionen je nach Werkzeug
        (SelectedWidth, SelectedHeight) = type switch
        {
            GardenElementType.Weg => (1.2f, 0f),
            GardenElementType.Mauer => (0.24f, 1.0f),
            GardenElementType.Zaun => (0f, 1.2f),
            _ => (0f, 0f)
        };
    }

    /// <summary>Touch auf Canvas: Punkt in lokalen Meter-Koordinaten hinzufuegen</summary>
    public void OnCanvasTapped(float canvasX, float canvasY)
    {
        if (_x.Length == 0) return;

        // Inverse der Renderer-Transformation: canvas → lokal
        // Renderer exponiert LastScale, LastCenterX, LastCenterY fuer konsistente Umrechnung
        var scale = Renderer.LastScale;
        if (scale < 0.001) return;

        var localX = (double)canvasX / scale + Renderer.LastCenterX;
        var localY = -((double)canvasY / scale) + Renderer.LastCenterY;

        AddDrawingPoint(localX, localY);
    }

    /// <summary>Punkt zum aktuellen Zeichnungselement hinzufuegen</summary>
    public void AddDrawingPoint(double localX, double localY)
    {
        _currentDrawingPoints.Add((localX, localY));
        IsDrawing = true;
        DrawingPointCount = _currentDrawingPoints.Count;
        InvalidateRequested?.Invoke();
    }

    /// <summary>Letzten Zeichnungspunkt entfernen</summary>
    [RelayCommand]
    private void UndoDrawPoint()
    {
        if (_currentDrawingPoints.Count == 0) return;

        _currentDrawingPoints.RemoveAt(_currentDrawingPoints.Count - 1);
        DrawingPointCount = _currentDrawingPoints.Count;

        if (_currentDrawingPoints.Count == 0)
            IsDrawing = false;

        InvalidateRequested?.Invoke();
    }

    /// <summary>Aktuelles Zeichnungselement fertigstellen</summary>
    [RelayCommand]
    private async Task FinishDrawingAsync()
    {
        if (_currentDrawingPoints.Count < 2)
        {
            _currentDrawingPoints.Clear();
            IsDrawing = false;
            DrawingPointCount = 0;
            return;
        }

        var element = new GardenElement
        {
            ElementType = SelectedTool,
            PointsJson = _gardenPlanService.SerializePoints(_currentDrawingPoints),
            Width = SelectedWidth,
            Height = SelectedHeight,
            Material = SelectedMaterial,
            SubType = SelectedSubType,
            LayerThicknessCm = GetDefaultThickness(SelectedMaterial),
            SortOrder = Elements.Count
        };

        // Masse berechnen
        switch (SelectedTool)
        {
            case GardenElementType.Weg:
                element.LengthMeters = _gardenPlanService.CalculatePolylineLength(_currentDrawingPoints);
                element.AreaSquareMeters = element.LengthMeters * element.Width;
                break;
            case GardenElementType.Beet:
            case GardenElementType.Rasen:
            case GardenElementType.Terrasse:
                element.AreaSquareMeters = _gardenPlanService.CalculatePolygonArea(_currentDrawingPoints);
                break;
            case GardenElementType.Mauer:
            case GardenElementType.Zaun:
                element.LengthMeters = _gardenPlanService.CalculatePolylineLength(_currentDrawingPoints);
                break;
        }

        // In DB persistieren (await damit sqlite-net die Id auf dem Objekt setzt)
        if (CurrentProjectId > 0)
        {
            element.ProjectId = CurrentProjectId;
            await _projectService.AddGardenElementAsync(CurrentProjectId, element);
        }

        Elements.Add(element);
        _undoStack.Push(element);
        CanUndo = true;

        _currentDrawingPoints.Clear();
        IsDrawing = false;
        DrawingPointCount = 0;

        RecalculateMaterials();
        InvalidateRequested?.Invoke();
    }

    /// <summary>Zeichnung abbrechen</summary>
    [RelayCommand]
    private void CancelDrawing()
    {
        _currentDrawingPoints.Clear();
        IsDrawing = false;
        DrawingPointCount = 0;
        InvalidateRequested?.Invoke();
    }

    /// <summary>Letztes Element rueckgaengig machen (auch aus DB loeschen)</summary>
    [RelayCommand]
    private async Task UndoAsync()
    {
        if (_undoStack.Count == 0) return;

        var element = _undoStack.Pop();
        Elements.Remove(element);

        // Aus DB loeschen (Id wurde durch await in FinishDrawingAsync korrekt gesetzt)
        if (element.Id > 0)
            await _projectService.DeleteGardenElementAsync(element.Id);

        CanUndo = _undoStack.Count > 0;

        RecalculateMaterials();
        InvalidateRequested?.Invoke();
    }

    /// <summary>Materialliste neu berechnen</summary>
    private void RecalculateMaterials()
    {
        Materials.Clear();
        var estimates = _gardenPlanService.CalculateMaterials(Elements.ToList());
        foreach (var est in estimates)
            Materials.Add(est);

        // Gesamtflaeche
        var totalArea = Elements.Sum(e => e.AreaSquareMeters);
        TotalAreaText = totalArea > 0 ? $"{totalArea:F1} m²" : "—";
    }

    /// <summary>Touch-Pan</summary>
    public void HandlePan(float deltaX, float deltaY)
    {
        Renderer.PanX += deltaX;
        Renderer.PanY += deltaY;
        InvalidateRequested?.Invoke();
    }

    /// <summary>Touch-Zoom</summary>
    public void HandleZoom(float factor)
    {
        Renderer.Zoom = Math.Clamp(Renderer.Zoom * factor, 0.2f, 10f);
        InvalidateRequested?.Invoke();
    }

    private static float GetDefaultThickness(string material) => material switch
    {
        "Pflaster" => 13f,
        "Kies" => 20f,
        "Naturstein" => 15f,
        "Beton" => 15f,
        "Holz" => 3f,
        "Erde" => 30f,
        _ => 15f
    };
}
