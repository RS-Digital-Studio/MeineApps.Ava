using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartMeasure.Shared.Graphics;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>3D-Gelaendemodell: Delaunay, Hoehenfarbkodierung, Konturlinien, Rotation/Zoom</summary>
public partial class TerrainViewModel : ObservableObject
{
    private readonly IMeasurementService _measurementService;
    private readonly ITerrainService _terrainService;
    private readonly ICoordinateService _coordinateService;

    public TerrainRenderer Renderer { get; } = new();

    [ObservableProperty] private TerrainMesh? _mesh;
    [ObservableProperty] private List<ContourLine>? _contourLines;
    [ObservableProperty] private string[]? _labels;
    [ObservableProperty] private int _pointCount;
    [ObservableProperty] private string _areaText = "—";
    [ObservableProperty] private string _heightRangeText = "—";

    // Konturlinien-Intervall
    [ObservableProperty] private double _contourInterval = 0.25; // 25cm Default

    // UI-Steuerung
    [ObservableProperty] private float _exaggeration = 3.0f;
    [ObservableProperty] private bool _showWireframe;
    [ObservableProperty] private bool _showContours = true;

    /// <summary>Canvas muss neu gezeichnet werden</summary>
    public event Action? InvalidateRequested;

    public TerrainViewModel(
        IMeasurementService measurementService,
        ITerrainService terrainService,
        ICoordinateService coordinateService)
    {
        _measurementService = measurementService;
        _terrainService = terrainService;
        _coordinateService = coordinateService;

        // Wenn ein neuer Punkt hinzukommt, Mesh neu berechnen
        _measurementService.PointAdded += _ => RecalculateMesh();
    }

    /// <summary>Mesh aus aktuellen Messpunkten berechnen</summary>
    [RelayCommand]
    private void RecalculateMesh()
    {
        var points = _measurementService.CurrentPoints;
        if (points.Count < 3)
        {
            Mesh = null;
            ContourLines = null;
            Labels = null;
            PointCount = points.Count;
            AreaText = "—";
            HeightRangeText = "—";
            InvalidateRequested?.Invoke();
            return;
        }

        // In lokale metrische Koordinaten konvertieren
        var lats = points.Select(p => p.Latitude).ToArray();
        var lons = points.Select(p => p.Longitude).ToArray();
        var alts = points.Select(p => p.Altitude).ToArray();

        var (x, y, z) = _coordinateService.ToLocalMetric(lats, lons, alts);

        // Delaunay-Triangulierung
        Mesh = _terrainService.CreateMesh(x, y, z);
        PointCount = points.Count;

        // Labels
        Labels = points.Select(p => p.Label ?? string.Empty).ToArray();

        // Konturlinien
        ContourLines = _terrainService.CreateContourLines(Mesh, ContourInterval);

        // Statistiken
        var area2d = _terrainService.CalculateArea2D(x, y);
        AreaText = $"{area2d:F1} m²";
        HeightRangeText = Mesh.MaxZ - Mesh.MinZ > 0.01
            ? $"{Mesh.MinZ:F2}m — {Mesh.MaxZ:F2}m (Δ{Mesh.MaxZ - Mesh.MinZ:F2}m)"
            : "Flach";

        // Renderer-Settings synchronisieren
        Renderer.Exaggeration = Exaggeration;
        Renderer.ShowWireframe = ShowWireframe;
        Renderer.ShowContours = ShowContours;

        InvalidateRequested?.Invoke();
    }

    partial void OnExaggerationChanged(float value)
    {
        Renderer.Exaggeration = value;
        InvalidateRequested?.Invoke();
    }

    partial void OnShowWireframeChanged(bool value)
    {
        Renderer.ShowWireframe = value;
        InvalidateRequested?.Invoke();
    }

    partial void OnShowContoursChanged(bool value)
    {
        Renderer.ShowContours = value;
        InvalidateRequested?.Invoke();
    }

    partial void OnContourIntervalChanged(double value)
    {
        if (Mesh != null)
        {
            ContourLines = _terrainService.CreateContourLines(Mesh, value);
            InvalidateRequested?.Invoke();
        }
    }

    /// <summary>Touch-Rotation (Drag)</summary>
    public void HandleDrag(float deltaX, float deltaY)
    {
        Renderer.Azimuth += deltaX * 0.5f;
        Renderer.Elevation = Math.Clamp(Renderer.Elevation - deltaY * 0.5f, 5f, 85f);
        InvalidateRequested?.Invoke();
    }

    /// <summary>Touch-Zoom (Pinch)</summary>
    public void HandleZoom(float factor)
    {
        Renderer.Zoom = Math.Clamp(Renderer.Zoom * factor, 0.2f, 5f);
        InvalidateRequested?.Invoke();
    }

    /// <summary>Touch-Pan (2-Finger-Drag)</summary>
    public void HandlePan(float deltaX, float deltaY)
    {
        Renderer.PanX += deltaX;
        Renderer.PanY += deltaY;
        InvalidateRequested?.Invoke();
    }

    /// <summary>Ansicht zuruecksetzen</summary>
    [RelayCommand]
    private void ResetView()
    {
        Renderer.Azimuth = 225f;
        Renderer.Elevation = 35f;
        Renderer.Zoom = 1.0f;
        Renderer.PanX = 0;
        Renderer.PanY = 0;
        InvalidateRequested?.Invoke();
    }
}
