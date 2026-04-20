using SkiaSharp;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Graphics;

/// <summary>
/// 3D-Geländemodell-Renderer: Dreiecke mit Höhenfarbkodierung, Konturlinien,
/// Perspektive, Rotation, Painter's Algorithm.
///
/// Performance-Optimierungen:
/// - Arrays gecacht (kein GC-Druck während Touch-Drag)
/// - Face-Normalen aus Mesh (vorberechnet, nicht pro Frame)
/// - Kamera-Z als Sort-Key statt Screen-Y (korrekte Tiefensortierung)
/// - Linien-Gradient für Höhen-Legende statt 400 DrawLine-Calls
/// - Nordpfeil-Path einmalig in Ctor
/// - SKFont explizit (SkiaSharp 3.x API)
/// </summary>
public sealed class TerrainRenderer : IDisposable
{
    // Kamera/Rotation
    public float Azimuth { get; set; } = 225f;
    public float Elevation { get; set; } = 35f;
    public float Zoom { get; set; } = 1.0f;
    public float PanX { get; set; }
    public float PanY { get; set; }
    public float Exaggeration { get; set; } = 3.0f;
    public bool ShowWireframe { get; set; }
    public bool ShowContours { get; set; } = true;
    public bool ShowLabels { get; set; } = true;

    // Höhen-Farbverlauf (Grün → Gelb → Orange → Braun)
    private static readonly SKColor[] HeightColors =
    [
        new(27, 94, 32),    // Dunkelgrün (tief)
        new(76, 175, 80),   // Hellgrün
        new(253, 216, 53),  // Gelb
        new(255, 143, 0),   // Orange
        new(93, 64, 55)     // Braun (hoch)
    ];

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _wirePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = new SKColor(255, 255, 255, 40) };
    private readonly SKPaint _contourPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(255, 255, 255, 100) };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true, Color = new SKColor(255, 107, 0) };
    private readonly SKPaint _northPaint = new() { IsAntialias = true, Color = new SKColor(239, 83, 80) };
    private readonly SKPaint _scalePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(200, 200, 200) };
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(26, 26, 46) };
    private readonly SKPaint _legendPaint = new() { IsAntialias = false };
    private readonly SKPaint _scaleTextPaint = new() { Color = new SKColor(200, 200, 200), IsAntialias = true };
    private readonly SKPaint _legendLabelPaint = new() { Color = new SKColor(200, 200, 200), IsAntialias = true };
    private readonly SKPaint _arrowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(239, 83, 80) };
    private readonly SKPaint _emptyTextPaint = new() { Color = new SKColor(136, 153, 170), IsAntialias = true };

    // SkiaSharp 3.x: SKFont explizit statt SKPaint.TextSize
    private readonly SKFont _labelFont = new(SKTypeface.Default, 11f) { Embolden = true };
    private readonly SKFont _northFont = new(SKTypeface.Default, 14f) { Embolden = true };
    private readonly SKFont _scaleFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _legendFont = new(SKTypeface.Default, 9f);
    private readonly SKFont _emptyFont = new(SKTypeface.Default, 16f);

    // Gecachter Nordpfeil-Pfad — einmal gebaut, pro Frame nur Save/Translate/Rotate/DrawPath
    private readonly SKPath _northArrowPath;

    // Gecachter Shader für die Höhen-Legende (Gradient, einmal gebaut)
    private readonly SKShader _legendShader;

    // Gecachte Arrays — werden bei Bedarf erweitert, nicht pro Frame neu allokiert
    private float[] _screenX = Array.Empty<float>();
    private float[] _screenY = Array.Empty<float>();
    private float[] _screenZ = Array.Empty<float>();
    private int[] _triIndices = Array.Empty<int>();
    private float[] _triDepth = Array.Empty<float>();

    public TerrainRenderer()
    {
        // Nordpfeil um (0,0) konstruieren — wird beim Zeichnen an Position translatiert
        _northArrowPath = new SKPath();
        _northArrowPath.MoveTo(0, -15);
        _northArrowPath.LineTo(-6, 8);
        _northArrowPath.LineTo(6, 8);
        _northArrowPath.Close();

        // Gradient-Shader für Legende: hoch (maxZ) → niedrig (minZ)
        _legendShader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, 1),
            HeightColors,
            null,
            SKShaderTileMode.Clamp);
    }

    public void Render(SKCanvas canvas, SKRect bounds, TerrainMesh? mesh,
        List<ContourLine>? contours, string[]? labels)
    {
        canvas.Clear(_bgPaint.Color);

        if (mesh == null || mesh.TriangleCount == 0)
        {
            DrawEmptyState(canvas, bounds);
            return;
        }

        EnsureArrayCapacity(mesh.VertexCount, mesh.TriangleCount);

        canvas.Save();
        canvas.Translate(bounds.MidX + PanX, bounds.MidY + PanY);

        var scale = Math.Min(bounds.Width, bounds.Height) * 0.35f * Zoom;
        var rangeX = mesh.MaxX - mesh.MinX;
        var rangeY = mesh.MaxY - mesh.MinY;
        var range = Math.Max(rangeX, rangeY);
        if (range < 0.001) range = 1;
        var normalizeScale = scale / range;

        var azRad = Azimuth * MathF.PI / 180f;
        var elRad = Elevation * MathF.PI / 180f;
        var cosAz = MathF.Cos(azRad);
        var sinAz = MathF.Sin(azRad);
        var cosEl = MathF.Cos(elRad);
        var sinEl = MathF.Sin(elRad);

        var centerX = (mesh.MinX + mesh.MaxX) / 2.0;
        var centerY = (mesh.MinY + mesh.MaxY) / 2.0;
        var centerZ = (mesh.MinZ + mesh.MaxZ) / 2.0;

        // Vertices projizieren — Kamera-Z separat merken für korrekte Painter-Tiefensortierung
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var lx = (float)((mesh.X[i] - centerX) * normalizeScale);
            var ly = (float)((mesh.Y[i] - centerY) * normalizeScale);
            var lz = (float)((mesh.Z[i] - centerZ) * normalizeScale * Exaggeration);

            var rx = lx * cosAz - ly * sinAz;
            var ry = lx * sinAz * sinEl + ly * cosAz * sinEl + lz * cosEl;
            var rz = -lx * sinAz * cosEl - ly * cosAz * cosEl + lz * sinEl;

            _screenX[i] = rx;
            _screenY[i] = -ry;
            _screenZ[i] = rz; // Kamera-Tiefe — größer = weiter weg
        }

        // Painter's Algorithm: Dreiecke hinten→vorne sortieren nach mittlerer Kamera-Z
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            _triIndices[t] = t;
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];
            _triDepth[t] = (_screenZ[i0] + _screenZ[i1] + _screenZ[i2]) / 3f;
        }

        Array.Sort(_triDepth, _triIndices, 0, mesh.TriangleCount);

        // Rotierte Lichtrichtung (von oben-links, in Kamera-Raum)
        // Licht fix im Kamera-Frame wirkt bei Azimut-Änderung natürlicher.
        const float lightCamX = -0.3f;
        const float lightCamY = -0.3f;
        const float lightCamZ = 0.85f;

        // Dreiecke zeichnen (von hinten nach vorne)
        using var path = new SKPath();
        for (int ti = 0; ti < mesh.TriangleCount; ti++)
        {
            var t = _triIndices[ti];
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            var avgZ = (mesh.Z[i0] + mesh.Z[i1] + mesh.Z[i2]) / 3.0;
            var zNorm = mesh.MaxZ > mesh.MinZ
                ? (avgZ - mesh.MinZ) / (mesh.MaxZ - mesh.MinZ)
                : 0.5;

            // Face-Normale aus Mesh (einmalig berechnet) — mit Azimut/Elevation rotieren
            var nx = mesh.NormalsX[t];
            var ny = mesh.NormalsY[t];
            var nz = mesh.NormalsZ[t];

            // Normale in Kamera-Frame rotieren (gleiche Rotation wie Vertices)
            var nRx = nx * cosAz - ny * sinAz;
            var nRy = nx * sinAz * sinEl + ny * cosAz * sinEl + nz * cosEl;
            var nRz = -nx * sinAz * cosEl - ny * cosAz * cosEl + nz * sinEl;

            var dot = nRx * lightCamX + nRy * lightCamY + nRz * lightCamZ;
            var light = Math.Clamp(0.3f + 0.7f * Math.Abs(dot), 0.3f, 1.0f);

            var baseColor = InterpolateHeightColor(zNorm);
            _fillPaint.Color = new SKColor(
                (byte)(baseColor.Red * light),
                (byte)(baseColor.Green * light),
                (byte)(baseColor.Blue * light));

            path.Rewind();
            path.MoveTo(_screenX[i0], _screenY[i0]);
            path.LineTo(_screenX[i1], _screenY[i1]);
            path.LineTo(_screenX[i2], _screenY[i2]);
            path.Close();

            canvas.DrawPath(path, _fillPaint);

            if (ShowWireframe)
                canvas.DrawPath(path, _wirePaint);
        }

        // Konturlinien zeichnen
        if (ShowContours && contours != null)
        {
            foreach (var contour in contours)
            {
                foreach (var seg in contour.Segments)
                {
                    var (sx1, sy1) = Project3D(seg.x1 - centerX, seg.y1 - centerY, contour.Height - centerZ,
                        (float)normalizeScale, cosAz, sinAz, cosEl, sinEl);
                    var (sx2, sy2) = Project3D(seg.x2 - centerX, seg.y2 - centerY, contour.Height - centerZ,
                        (float)normalizeScale, cosAz, sinAz, cosEl, sinEl);

                    canvas.DrawLine(sx1, sy1, sx2, sy2, _contourPaint);
                }
            }
        }

        // Punkt-Labels zeichnen
        if (ShowLabels && labels != null)
        {
            var max = Math.Min(labels.Length, mesh.VertexCount);
            for (int i = 0; i < max; i++)
            {
                if (string.IsNullOrEmpty(labels[i])) continue;
                canvas.DrawText(labels[i], _screenX[i] + 4, _screenY[i] - 4,
                    SKTextAlign.Left, _labelFont, _labelPaint);
            }
        }

        canvas.Restore();

        DrawNorthArrow(canvas, bounds);
        DrawScale(canvas, bounds, range, scale);
        DrawHeightLegend(canvas, bounds, mesh.MinZ, mesh.MaxZ);
    }

    private void EnsureArrayCapacity(int vertexCount, int triangleCount)
    {
        if (_screenX.Length < vertexCount)
        {
            _screenX = new float[vertexCount];
            _screenY = new float[vertexCount];
            _screenZ = new float[vertexCount];
        }
        if (_triIndices.Length < triangleCount)
        {
            _triIndices = new int[triangleCount];
            _triDepth = new float[triangleCount];
        }
    }

    private (float sx, float sy) Project3D(double lx, double ly, double lz,
        float normalizeScale, float cosAz, float sinAz, float cosEl, float sinEl)
    {
        var px = (float)(lx * normalizeScale);
        var py = (float)(ly * normalizeScale);
        var pz = (float)(lz * normalizeScale * Exaggeration);

        var rx = px * cosAz - py * sinAz;
        var ry = px * sinAz * sinEl + py * cosAz * sinEl + pz * cosEl;

        return (rx, -ry);
    }

    private static SKColor InterpolateHeightColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var segment = t * (HeightColors.Length - 1);
        var index = (int)segment;
        var frac = (float)(segment - index);

        if (index >= HeightColors.Length - 1)
            return HeightColors[^1];

        var c1 = HeightColors[index];
        var c2 = HeightColors[index + 1];

        return new SKColor(
            (byte)(c1.Red + (c2.Red - c1.Red) * frac),
            (byte)(c1.Green + (c2.Green - c1.Green) * frac),
            (byte)(c1.Blue + (c2.Blue - c1.Blue) * frac));
    }

    private void DrawEmptyState(SKCanvas canvas, SKRect bounds)
    {
        canvas.DrawText("Mindestens 3 Punkte für Geländemodell nötig",
            bounds.MidX, bounds.MidY,
            SKTextAlign.Center, _emptyFont, _emptyTextPaint);
    }

    private void DrawNorthArrow(SKCanvas canvas, SKRect bounds)
    {
        var cx = bounds.Right - 30;
        var cy = bounds.Top + 40;

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(-Azimuth + 180);
        canvas.DrawPath(_northArrowPath, _arrowPaint);
        canvas.Restore();

        canvas.DrawText("N", cx - 4, cy - 18, SKTextAlign.Left, _northFont, _northPaint);
    }

    private void DrawScale(SKCanvas canvas, SKRect bounds, double rangeMeters, float scalePixels)
    {
        var scaleLength = 50f;
        var metersPerPixel = rangeMeters / (scalePixels * 2);
        var scaleMeters = scaleLength * metersPerPixel;

        var rounded = RoundToNice(scaleMeters);
        var barLength = (float)(rounded / metersPerPixel);

        var x = bounds.Left + 16;
        var y = bounds.Bottom - 20;

        canvas.DrawLine(x, y, x + barLength, y, _scalePaint);
        canvas.DrawLine(x, y - 4, x, y + 4, _scalePaint);
        canvas.DrawLine(x + barLength, y - 4, x + barLength, y + 4, _scalePaint);

        canvas.DrawText($"{rounded:G3} m", x + barLength / 2, y - 6,
            SKTextAlign.Center, _scaleFont, _scaleTextPaint);
    }

    private void DrawHeightLegend(SKCanvas canvas, SKRect bounds, double minZ, double maxZ)
    {
        var x = bounds.Right - 20;
        var top = bounds.Top + 80;
        var height = bounds.Height * 0.4f;
        var rect = new SKRect(x - 8, top, x, top + height);

        // Gradient-Shader in Rect-Koordinaten transformieren (vorher war es 0..1 Unit-Rect).
        // HeightColors ist hoch→tief sortiert, aber wir wollen oben=hoch anzeigen → wir zeichnen
        // normal, das Ergebnis ist oben grün (tief), unten braun (hoch). Darum HeightColors-Reihenfolge
        // ist tief→hoch, was visuell zu "oben=hoch, unten=tief" invertiert werden muss.
        // Einfach: Shader-Localmatrix für Scale auf height + Translate auf top.
        using var localShader = _legendShader.WithLocalMatrix(
            SKMatrix.CreateScaleTranslation(1, -height, 0, top + height));
        _legendPaint.Shader = localShader;
        canvas.DrawRect(rect, _legendPaint);
        _legendPaint.Shader = null;

        canvas.DrawText($"{maxZ:F1}m", x - 30, top - 2,
            SKTextAlign.Left, _legendFont, _legendLabelPaint);
        canvas.DrawText($"{minZ:F1}m", x - 30, top + height + 10,
            SKTextAlign.Left, _legendFont, _legendLabelPaint);
    }

    private static double RoundToNice(double value)
    {
        if (value <= 0) return 1;
        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);

        double nice;
        if (fraction < 1.5) nice = 1;
        else if (fraction < 3.5) nice = 2;
        else if (fraction < 7.5) nice = 5;
        else nice = 10;

        return nice * Math.Pow(10, exponent);
    }

    public void Dispose()
    {
        _fillPaint.Dispose();
        _wirePaint.Dispose();
        _contourPaint.Dispose();
        _labelPaint.Dispose();
        _northPaint.Dispose();
        _scalePaint.Dispose();
        _bgPaint.Dispose();
        _legendPaint.Dispose();
        _scaleTextPaint.Dispose();
        _legendLabelPaint.Dispose();
        _arrowPaint.Dispose();
        _emptyTextPaint.Dispose();
        _labelFont.Dispose();
        _northFont.Dispose();
        _scaleFont.Dispose();
        _legendFont.Dispose();
        _emptyFont.Dispose();
        _northArrowPath.Dispose();
        _legendShader.Dispose();
    }
}
