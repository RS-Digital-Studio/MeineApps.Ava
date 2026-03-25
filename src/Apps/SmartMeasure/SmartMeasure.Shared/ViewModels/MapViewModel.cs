using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;
using SmartMeasure.Shared.Services;
using Color = Mapsui.Styles.Color;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Kartenansicht mit OpenStreetMap: Messpunkte, Polygon, Flaeche</summary>
public partial class MapViewModel : ObservableObject
{
    private readonly IMeasurementService _measurementService;

    public Map MapInstance { get; }

    private WritableLayer? _pointLayer;
    private WritableLayer? _polygonLayer;

    [ObservableProperty] private string _areaText = "—";
    [ObservableProperty] private string _perimeterText = "—";
    [ObservableProperty] private int _pointCount;

    public MapViewModel(IMeasurementService measurementService)
    {
        _measurementService = measurementService;

        // Karte mit OpenStreetMap initialisieren
        MapInstance = new Map();
        MapInstance.Layers.Add(OpenStreetMap.CreateTileLayer());

        // Punkt-Layer
        _pointLayer = new WritableLayer { Name = "Messpunkte", Style = null };
        MapInstance.Layers.Add(_pointLayer);

        // Polygon-Layer
        _polygonLayer = new WritableLayer { Name = "Grundstück", Style = null };
        MapInstance.Layers.Add(_polygonLayer);

        // Wenn Punkte hinzugefuegt werden
        _measurementService.PointAdded += _ => UpdateMap();
    }

    /// <summary>Karte mit aktuellen Messpunkten aktualisieren</summary>
    [RelayCommand]
    private void UpdateMap()
    {
        var points = _measurementService.CurrentPoints;
        PointCount = points.Count;

        _pointLayer?.Clear();
        _polygonLayer?.Clear();

        if (points.Count == 0)
        {
            AreaText = "—";
            PerimeterText = "—";
            return;
        }

        // Punkte als Marker hinzufuegen
        foreach (var point in points)
        {
            var (x, y) = SphericalMercator.FromLonLat(point.Longitude, point.Latitude);
            var feature = new GeometryFeature(new Point(x, y));
            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.4,
                Fill = new Brush(new Color(255, 107, 0)), // Primary Orange
                Outline = new Pen(Color.White, 2)
            });

            // Label
            if (!string.IsNullOrEmpty(point.Label))
            {
                feature.Styles.Add(new LabelStyle
                {
                    Text = point.Label,
                    ForeColor = new Color(255, 107, 0),
                    BackColor = new Brush(new Color(26, 26, 46, 200)),
                    Font = new Font { Size = 12 },
                    Offset = new Offset(0, -20)
                });
            }

            _pointLayer?.Add(feature);
        }

        // Polygon zeichnen wenn >= 3 Punkte
        if (points.Count >= 3)
        {
            var coordinates = points
                .Select(p =>
                {
                    var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                    return new Coordinate(x, y);
                })
                .ToList();

            // Polygon schliessen
            coordinates.Add(coordinates[0]);

            var polygon = new Polygon(new LinearRing(coordinates.ToArray()));
            var feature = new GeometryFeature(polygon);
            feature.Styles.Add(new VectorStyle
            {
                Fill = new Brush(new Color(33, 150, 243, 40)), // Blau halbtransparent
                Outline = new Pen(new Color(33, 150, 243), 2),
                Line = new Pen(new Color(33, 150, 243), 2)
            });
            _polygonLayer?.Add(feature);

            // Flaeche + Umfang berechnen
            var area = _measurementService.CalculateArea(points);
            var perimeter = _measurementService.CalculatePerimeter(points);
            AreaText = $"{area:F1} m²";
            PerimeterText = $"{perimeter:F1} m";
        }
        else
        {
            AreaText = "—";
            PerimeterText = "—";
        }

        // Karte auf Punkte zentrieren
        if (points.Count > 0)
        {
            var centerLat = points.Average(p => p.Latitude);
            var centerLon = points.Average(p => p.Longitude);
            var (cx, cy) = SphericalMercator.FromLonLat(centerLon, centerLat);
            MapInstance.Navigator.CenterOn(new MPoint(cx, cy));
            MapInstance.Navigator.ZoomTo(MapInstance.Navigator.Resolutions.Count > 5 ? MapInstance.Navigator.Resolutions[5] : 1);
        }

        _pointLayer?.DataHasChanged();
        _polygonLayer?.DataHasChanged();
    }
}
