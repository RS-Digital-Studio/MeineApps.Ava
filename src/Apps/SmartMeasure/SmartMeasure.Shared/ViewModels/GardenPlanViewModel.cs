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

    // Aktuell gezeichnetes Element (Punkte werden gesammelt)
    private readonly List<(double x, double y)> _currentDrawingPoints = [];

    // Undo/Redo
    private readonly Stack<GardenElement> _undoStack = new();
    [ObservableProperty] private bool _canUndo;

    // Statistiken
    [ObservableProperty] private string _totalAreaText = "—";

    /// <summary>Canvas muss neu gezeichnet werden</summary>
    public event Action? InvalidateRequested;

    public GardenPlanViewModel(
        IMeasurementService measurementService,
        ICoordinateService coordinateService,
        IGardenPlanService gardenPlanService)
    {
        _measurementService = measurementService;
        _coordinateService = coordinateService;
        _gardenPlanService = gardenPlanService;
        Renderer = new GardenPlanRenderer(gardenPlanService);

        _measurementService.PointAdded += _ => UpdateCoordinates();
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

    /// <summary>Punkt zum aktuellen Zeichnungselement hinzufuegen</summary>
    public void AddDrawingPoint(double localX, double localY)
    {
        _currentDrawingPoints.Add((localX, localY));
        IsDrawing = true;
        InvalidateRequested?.Invoke();
    }

    /// <summary>Aktuelles Zeichnungselement fertigstellen</summary>
    [RelayCommand]
    private void FinishDrawing()
    {
        if (_currentDrawingPoints.Count < 2)
        {
            _currentDrawingPoints.Clear();
            IsDrawing = false;
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

        Elements.Add(element);
        _undoStack.Push(element);
        CanUndo = true;

        _currentDrawingPoints.Clear();
        IsDrawing = false;

        RecalculateMaterials();
        InvalidateRequested?.Invoke();
    }

    /// <summary>Zeichnung abbrechen</summary>
    [RelayCommand]
    private void CancelDrawing()
    {
        _currentDrawingPoints.Clear();
        IsDrawing = false;
        InvalidateRequested?.Invoke();
    }

    /// <summary>Letztes Element rueckgaengig machen</summary>
    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        var element = _undoStack.Pop();
        Elements.Remove(element);
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
