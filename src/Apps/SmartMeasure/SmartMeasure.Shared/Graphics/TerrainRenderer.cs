using SkiaSharp;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Graphics;

/// <summary>3D-Gelaendemodell Renderer: Dreiecke mit Hoehenfarbkodierung,
/// Konturlinien, Perspektive, Rotation. Painter's Algorithm + Bitmap-Cache.</summary>
public class TerrainRenderer
{
    // Kamera/Rotation
    public float Azimuth { get; set; } = 225f; // Grad (Blickrichtung)
    public float Elevation { get; set; } = 35f;  // Grad (Blick von oben)
    public float Zoom { get; set; } = 1.0f;
    public float PanX { get; set; }
    public float PanY { get; set; }
    public float Exaggeration { get; set; } = 3.0f; // Hoehenueberhoehung
    public bool ShowWireframe { get; set; }
    public bool ShowContours { get; set; } = true;
    public bool ShowLabels { get; set; } = true;

    // Hoehen-Farbverlauf (Gruen → Gelb → Orange → Braun)
    private static readonly SKColor[] HeightColors =
    [
        new(27, 94, 32),    // Dunkelgruen (tief)
        new(76, 175, 80),   // Hellgruen
        new(253, 216, 53),  // Gelb
        new(255, 143, 0),   // Orange
        new(93, 64, 55)     // Braun (hoch)
    ];

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _wirePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = new SKColor(255, 255, 255, 40) };
    private readonly SKPaint _contourPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(255, 255, 255, 100) };
    private readonly SKPaint _contourLabelPaint = new() { IsAntialias = true, Color = new SKColor(255, 255, 255, 180), TextSize = 10f };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true, Color = new SKColor(255, 107, 0), TextSize = 11f, FakeBoldText = true };
    private readonly SKPaint _northPaint = new() { IsAntialias = true, Color = new SKColor(239, 83, 80), TextSize = 14f, FakeBoldText = true };
    private readonly SKPaint _scalePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(200, 200, 200) };
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(26, 26, 46) }; // BgPrimaryColor

    public void Render(SKCanvas canvas, SKRect bounds, TerrainMesh? mesh,
        List<ContourLine>? contours, string[]? labels)
    {
        canvas.Clear(_bgPaint.Color);

        if (mesh == null || mesh.TriangleCount == 0)
        {
            DrawEmptyState(canvas, bounds);
            return;
        }

        canvas.Save();
        canvas.Translate(bounds.MidX + PanX, bounds.MidY + PanY);

        var scale = Math.Min(bounds.Width, bounds.Height) * 0.35f * Zoom;
        var rangeX = mesh.MaxX - mesh.MinX;
        var rangeY = mesh.MaxY - mesh.MinY;
        var range = Math.Max(rangeX, rangeY);
        if (range < 0.001) range = 1;
        var normalizeScale = scale / range;

        // 3D-Projektion: Isometrisch mit Rotation
        var azRad = Azimuth * MathF.PI / 180f;
        var elRad = Elevation * MathF.PI / 180f;
        var cosAz = MathF.Cos(azRad);
        var sinAz = MathF.Sin(azRad);
        var cosEl = MathF.Cos(elRad);
        var sinEl = MathF.Sin(elRad);

        // Vertices in Screen-Space projizieren
        var centerX = (mesh.MinX + mesh.MaxX) / 2.0;
        var centerY = (mesh.MinY + mesh.MaxY) / 2.0;
        var centerZ = (mesh.MinZ + mesh.MaxZ) / 2.0;

        var screenX = new float[mesh.VertexCount];
        var screenY = new float[mesh.VertexCount];

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var lx = (float)((mesh.X[i] - centerX) * normalizeScale);
            var ly = (float)((mesh.Y[i] - centerY) * normalizeScale);
            var lz = (float)((mesh.Z[i] - centerZ) * normalizeScale * Exaggeration);

            // Rotation um Y-Achse (Azimuth) + X-Achse (Elevation)
            var rx = lx * cosAz - ly * sinAz;
            var ry = lx * sinAz * sinEl + ly * cosAz * sinEl + lz * cosEl;
            var rz = -lx * sinAz * cosEl - ly * cosAz * cosEl + lz * sinEl;

            screenX[i] = rx;
            screenY[i] = -ry; // Y invertieren (Bildschirm: Y nach unten)
        }

        // Dreiecke nach Z-Tiefe sortieren (Painter's Algorithm)
        var triIndices = new int[mesh.TriangleCount];
        var triDepth = new float[mesh.TriangleCount];

        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            triIndices[t] = t;
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            // Mittlere Screen-Y als Tiefe (weiter oben = weiter weg)
            triDepth[t] = (screenY[i0] + screenY[i1] + screenY[i2]) / 3f;
        }

        Array.Sort(triDepth, triIndices);

        // Dreiecke zeichnen (von hinten nach vorne)
        using var path = new SKPath();
        for (int ti = 0; ti < triIndices.Length; ti++)
        {
            var t = triIndices[ti];
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            // Durchschnittshoehe fuer Farbe
            var avgZ = (mesh.Z[i0] + mesh.Z[i1] + mesh.Z[i2]) / 3.0;
            var zNorm = mesh.MaxZ > mesh.MinZ
                ? (avgZ - mesh.MinZ) / (mesh.MaxZ - mesh.MinZ)
                : 0.5;

            // Einfaches Diffuse-Shading (Normalvektor * Lichtrichtung)
            var nx = (mesh.Y[i1] - mesh.Y[i0]) * (mesh.Z[i2] - mesh.Z[i0]) -
                     (mesh.Z[i1] - mesh.Z[i0]) * (mesh.Y[i2] - mesh.Y[i0]);
            var ny = (mesh.Z[i1] - mesh.Z[i0]) * (mesh.X[i2] - mesh.X[i0]) -
                     (mesh.X[i1] - mesh.X[i0]) * (mesh.Z[i2] - mesh.Z[i0]);
            var nz = (mesh.X[i1] - mesh.X[i0]) * (mesh.Y[i2] - mesh.Y[i0]) -
                     (mesh.Y[i1] - mesh.Y[i0]) * (mesh.X[i2] - mesh.X[i0]);
            var nLen = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (nLen > 0) { nx /= nLen; ny /= nLen; nz /= nLen; }

            // Licht von oben-links
            var light = (float)Math.Max(0.3, Math.Min(1.0,
                0.3 + 0.7 * (nx * 0.3 + ny * 0.3 + nz * 0.8)));

            var baseColor = InterpolateHeightColor(zNorm);
            _fillPaint.Color = new SKColor(
                (byte)(baseColor.Red * light),
                (byte)(baseColor.Green * light),
                (byte)(baseColor.Blue * light));

            path.Reset();
            path.MoveTo(screenX[i0], screenY[i0]);
            path.LineTo(screenX[i1], screenY[i1]);
            path.LineTo(screenX[i2], screenY[i2]);
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
            for (int i = 0; i < Math.Min(labels.Length, mesh.VertexCount); i++)
            {
                if (string.IsNullOrEmpty(labels[i])) continue;
                canvas.DrawText(labels[i], screenX[i] + 4, screenY[i] - 4, _labelPaint);
            }
        }

        canvas.Restore();

        // Nordpfeil (oben rechts)
        DrawNorthArrow(canvas, bounds);

        // Massstab (unten links)
        DrawScale(canvas, bounds, range, scale);

        // Hoehenskala (rechts)
        DrawHeightLegend(canvas, bounds, mesh.MinZ, mesh.MaxZ);
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
        using var paint = new SKPaint
        {
            Color = new SKColor(136, 153, 170),
            TextSize = 16f,
            TextAlign = SKTextAlign.Center,
            IsAntialias = true
        };
        canvas.DrawText("Mindestens 3 Punkte für Geländemodell nötig",
            bounds.MidX, bounds.MidY, paint);
    }

    private void DrawNorthArrow(SKCanvas canvas, SKRect bounds)
    {
        var cx = bounds.Right - 30;
        var cy = bounds.Top + 40;

        // Nordpfeil rotieren mit Azimuth
        canvas.Save();
        canvas.RotateDegrees(-Azimuth + 180, cx, cy);

        using var arrowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(239, 83, 80)
        };

        using var path = new SKPath();
        path.MoveTo(cx, cy - 15);
        path.LineTo(cx - 6, cy + 8);
        path.LineTo(cx + 6, cy + 8);
        path.Close();
        canvas.DrawPath(path, arrowPaint);

        canvas.Restore();
        canvas.DrawText("N", cx - 4, cy - 18, _northPaint);
    }

    private void DrawScale(SKCanvas canvas, SKRect bounds, double rangeMeters, float scalePixels)
    {
        // Massstabsbalken unten links
        var scaleLength = 50f; // Pixel
        var metersPerPixel = rangeMeters / (scalePixels * 2);
        var scaleMeters = scaleLength * metersPerPixel;

        // Auf schoenen Wert runden (1, 2, 5, 10, 20, 50...)
        var rounded = RoundToNice(scaleMeters);
        var barLength = (float)(rounded / metersPerPixel);

        var x = bounds.Left + 16;
        var y = bounds.Bottom - 20;

        canvas.DrawLine(x, y, x + barLength, y, _scalePaint);
        canvas.DrawLine(x, y - 4, x, y + 4, _scalePaint);
        canvas.DrawLine(x + barLength, y - 4, x + barLength, y + 4, _scalePaint);

        using var textPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            TextSize = 10f,
            TextAlign = SKTextAlign.Center,
            IsAntialias = true
        };
        canvas.DrawText($"{rounded:G3} m", x + barLength / 2, y - 6, textPaint);
    }

    private void DrawHeightLegend(SKCanvas canvas, SKRect bounds, double minZ, double maxZ)
    {
        var x = bounds.Right - 20;
        var top = bounds.Top + 80;
        var height = bounds.Height * 0.4f;

        // Farbverlauf
        for (int i = 0; i < (int)height; i++)
        {
            var t = 1.0 - i / height;
            var color = InterpolateHeightColor(t);
            using var paint = new SKPaint { Color = color };
            canvas.DrawLine(x - 8, top + i, x, top + i, paint);
        }

        // Min/Max Beschriftung
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            TextSize = 9f,
            IsAntialias = true
        };
        canvas.DrawText($"{maxZ:F1}m", x - 30, top - 2, labelPaint);
        canvas.DrawText($"{minZ:F1}m", x - 30, top + height + 10, labelPaint);
    }

    private static double RoundToNice(double value)
    {
        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);

        double nice;
        if (fraction < 1.5) nice = 1;
        else if (fraction < 3.5) nice = 2;
        else if (fraction < 7.5) nice = 5;
        else nice = 10;

        return nice * Math.Pow(10, exponent);
    }

    /// <summary>Alle Paints freigeben</summary>
    public void Dispose()
    {
        _fillPaint.Dispose();
        _wirePaint.Dispose();
        _contourPaint.Dispose();
        _contourLabelPaint.Dispose();
        _labelPaint.Dispose();
        _northPaint.Dispose();
        _scalePaint.Dispose();
        _bgPaint.Dispose();
    }
}
