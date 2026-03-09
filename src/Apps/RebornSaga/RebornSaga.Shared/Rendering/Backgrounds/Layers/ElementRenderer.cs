namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>
/// Zeichnet Silhouetten-Elemente im Mittelgrund (hinter Charakteren).
/// Jeder ElementType hat eine eigene Draw-Methode. Elemente werden gleichmäßig
/// verteilt mit leichter Größen-Variation per Sinus.
/// </summary>
public static class ElementRenderer
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _accentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPath _path = new();

    public static void Render(SKCanvas canvas, SKRect bounds, ElementDef[] elements)
    {
        foreach (var elem in elements)
        {
            for (int i = 0; i < elem.Count; i++)
            {
                // X-Position: gleichmäßig verteilt oder per Spacing
                var x = bounds.Left + bounds.Width * (elem.Spacing > 0
                    ? elem.Spacing * i + elem.Spacing * 0.5f
                    : (i + 0.5f) / elem.Count);

                // Höhe mit Variation
                var heightFactor = 0.5f + MathF.Sin(i * 1.7f) * 0.5f;
                var h = bounds.Height * (elem.MinHeight + (elem.MaxHeight - elem.MinHeight) * heightFactor);

                // Breite proportional zur Verteilung
                var w = bounds.Width / Math.Max(elem.Count, 1) * 0.6f;

                // Basis-Y
                var baseY = bounds.Top + bounds.Height * elem.YBase;

                _fillPaint.Color = elem.Color;
                _strokePaint.Color = LightenColor(elem.Color, 0.15f).WithAlpha(40);
                _strokePaint.StrokeWidth = 1f;

                DrawElement(canvas, elem.Type, x, baseY, h, w, i);
            }
        }
    }

    private static void DrawElement(SKCanvas canvas, ElementType type, float x, float baseY,
        float h, float w, int index)
    {
        switch (type)
        {
            case ElementType.ConiferTree: DrawConiferTree(canvas, x, baseY, h, w); break;
            case ElementType.DeciduousTree: DrawDeciduousTree(canvas, x, baseY, h, w); break;
            case ElementType.Bush: DrawBush(canvas, x, baseY, h, w); break;
            case ElementType.DeadTree: DrawDeadTree(canvas, x, baseY, h, w); break;
            case ElementType.Willow: DrawWillow(canvas, x, baseY, h, w); break;
            case ElementType.House: DrawHouse(canvas, x, baseY, h, w, index); break;
            case ElementType.Well: DrawWell(canvas, x, baseY, h, w); break;
            case ElementType.Fence: DrawFence(canvas, x, baseY, h, w); break;
            case ElementType.Rock: DrawRock(canvas, x, baseY, h, w, index); break;
            case ElementType.Boulder: DrawBoulder(canvas, x, baseY, h, w); break;
            case ElementType.Stump: DrawStump(canvas, x, baseY, h, w); break;
            case ElementType.Log: DrawLog(canvas, x, baseY, h, w); break;
            case ElementType.Pillar: DrawPillar(canvas, x, baseY, h, w); break;
            case ElementType.Arch: DrawArch(canvas, x, baseY, h, w); break;
            case ElementType.BrokenWall: DrawBrokenWall(canvas, x, baseY, h, w, index); break;
            case ElementType.Bookshelf: DrawBookshelf(canvas, x, baseY, h, w); break;
            case ElementType.Table: DrawTable(canvas, x, baseY, h, w); break;
            case ElementType.Barrel: DrawBarrel(canvas, x, baseY, h, w); break;
            case ElementType.Throne: DrawThrone(canvas, x, baseY, h, w); break;
            case ElementType.Banner: DrawBanner(canvas, x, baseY, h, w, index); break;
            case ElementType.SwordInGround: DrawSwordInGround(canvas, x, baseY, h, w); break;
            case ElementType.Railing: DrawRailing(canvas, x, baseY, h, w); break;
            case ElementType.RuneCircle: DrawRuneCircle(canvas, x, baseY, h, w); break;
            case ElementType.GeometricFragment: DrawGeometricFragment(canvas, x, baseY, h, w, index); break;
        }
    }

    // --- Natur ---

    private static void DrawConiferTree(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var trunkW = w * 0.08f;
        var trunkH = h * 0.25f;

        // Stamm
        canvas.DrawRect(x - trunkW, baseY - trunkH, trunkW * 2, trunkH, _fillPaint);

        // 2 Dreieck-Ebenen
        for (int layer = 0; layer < 2; layer++)
        {
            var layerY = baseY - trunkH - h * (0.35f * layer);
            var layerW = w * (0.5f - layer * 0.12f);
            var layerH = h * 0.45f;
            _path.Rewind();
            _path.MoveTo(x, layerY - layerH);
            _path.LineTo(x - layerW, layerY);
            _path.LineTo(x + layerW, layerY);
            _path.Close();
            canvas.DrawPath(_path, _fillPaint);
        }
    }

    private static void DrawDeciduousTree(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var trunkW = w * 0.06f;
        var trunkH = h * 0.35f;

        // Stamm
        canvas.DrawRect(x - trunkW, baseY - trunkH, trunkW * 2, trunkH, _fillPaint);

        // Runde Krone
        var crownY = baseY - trunkH - h * 0.25f;
        var crownR = w * 0.4f;
        canvas.DrawCircle(x, crownY, crownR, _fillPaint);
        // Zweite, leicht versetzte Krone für Volumen
        canvas.DrawCircle(x - crownR * 0.3f, crownY + crownR * 0.15f, crownR * 0.7f, _fillPaint);
    }

    private static void DrawBush(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        // Flaches Oval, breiter als hoch
        canvas.DrawOval(x, baseY - h * 0.4f, w * 0.4f, h * 0.4f, _fillPaint);
        // Zweites Oval für Tiefe
        _fillPaint.Color = LightenColor(_fillPaint.Color, 0.08f);
        canvas.DrawOval(x + w * 0.1f, baseY - h * 0.5f, w * 0.25f, h * 0.3f, _fillPaint);
        _fillPaint.Color = DarkenColor(_fillPaint.Color, 0.93f);
    }

    private static void DrawDeadTree(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        // Dünner Stamm
        _strokePaint.StrokeWidth = w * 0.06f;
        canvas.DrawLine(x, baseY, x, baseY - h, _strokePaint);
        // Äste (2-3 seitlich)
        canvas.DrawLine(x, baseY - h * 0.6f, x - w * 0.3f, baseY - h * 0.75f, _strokePaint);
        canvas.DrawLine(x, baseY - h * 0.4f, x + w * 0.25f, baseY - h * 0.55f, _strokePaint);
        canvas.DrawLine(x, baseY - h * 0.8f, x + w * 0.2f, baseY - h * 0.9f, _strokePaint);
    }

    private static void DrawWillow(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        // Stamm
        var trunkW = w * 0.07f;
        canvas.DrawRect(x - trunkW, baseY - h * 0.5f, trunkW * 2, h * 0.5f, _fillPaint);
        // Krone
        canvas.DrawCircle(x, baseY - h * 0.6f, w * 0.35f, _fillPaint);
        // Hängende Strähnen
        _strokePaint.StrokeWidth = 1f;
        for (int i = 0; i < 7; i++)
        {
            var sx = x - w * 0.3f + i * w * 0.1f;
            var hangLen = h * 0.3f + MathF.Sin(i * 1.2f) * h * 0.1f;
            canvas.DrawLine(sx, baseY - h * 0.45f, sx + 3f, baseY - h * 0.45f + hangLen, _strokePaint);
        }
    }

    // --- Gebäude ---

    private static void DrawHouse(SKCanvas canvas, float x, float baseY, float h, float w, int index)
    {
        var houseW = w * 0.8f;
        var wallH = h * 0.6f;
        var roofH = h * 0.4f;

        // Wand
        canvas.DrawRect(x - houseW * 0.5f, baseY - wallH, houseW, wallH, _fillPaint);

        // Dach (Dreieck)
        _path.Rewind();
        _path.MoveTo(x - houseW * 0.6f, baseY - wallH);
        _path.LineTo(x, baseY - wallH - roofH);
        _path.LineTo(x + houseW * 0.6f, baseY - wallH);
        _path.Close();
        canvas.DrawPath(_path, _fillPaint);

        // Fenster (warmgelb)
        _accentPaint.Color = new SKColor(0xFF, 0xC8, 0x40, 180);
        var winSize = houseW * 0.12f;
        canvas.DrawRect(x - houseW * 0.2f, baseY - wallH * 0.65f, winSize, winSize, _accentPaint);
        canvas.DrawRect(x + houseW * 0.1f, baseY - wallH * 0.65f, winSize, winSize, _accentPaint);
    }

    private static void DrawWell(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var wellW = w * 0.3f;
        var wellH = h * 0.4f;

        // Brunnenwand (Zylinder als Rect)
        canvas.DrawRect(x - wellW, baseY - wellH, wellW * 2, wellH, _fillPaint);
        // Oval oben
        canvas.DrawOval(x, baseY - wellH, wellW, wellH * 0.15f, _fillPaint);
        // Dach-Pfosten + Dach
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(x - wellW * 0.7f, baseY - wellH, x - wellW * 0.7f, baseY - h * 0.8f, _strokePaint);
        canvas.DrawLine(x + wellW * 0.7f, baseY - wellH, x + wellW * 0.7f, baseY - h * 0.8f, _strokePaint);
        // Dachbalken
        canvas.DrawLine(x - wellW * 0.9f, baseY - h * 0.8f, x + wellW * 0.9f, baseY - h * 0.8f, _strokePaint);
    }

    private static void DrawFence(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var fenceH = h * 0.6f;
        _strokePaint.StrokeWidth = 2f;

        // Pfosten
        for (int i = -2; i <= 2; i++)
        {
            var px = x + i * w * 0.15f;
            canvas.DrawLine(px, baseY, px, baseY - fenceH, _strokePaint);
        }
        // Querbalken
        canvas.DrawLine(x - w * 0.35f, baseY - fenceH * 0.3f, x + w * 0.35f, baseY - fenceH * 0.3f, _strokePaint);
        canvas.DrawLine(x - w * 0.35f, baseY - fenceH * 0.7f, x + w * 0.35f, baseY - fenceH * 0.7f, _strokePaint);
    }

    // --- Felsen ---

    private static void DrawRock(SKCanvas canvas, float x, float baseY, float h, float w, int index)
    {
        // Unregelmäßiges Polygon (5 Punkte)
        _path.Rewind();
        var seed = index * 1.7f;
        _path.MoveTo(x - w * 0.3f, baseY);
        _path.LineTo(x - w * 0.35f, baseY - h * (0.5f + MathF.Sin(seed) * 0.15f));
        _path.LineTo(x - w * 0.05f, baseY - h * (0.8f + MathF.Sin(seed + 1f) * 0.1f));
        _path.LineTo(x + w * 0.3f, baseY - h * (0.55f + MathF.Sin(seed + 2f) * 0.15f));
        _path.LineTo(x + w * 0.25f, baseY);
        _path.Close();
        canvas.DrawPath(_path, _fillPaint);
    }

    private static void DrawBoulder(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        canvas.DrawOval(x, baseY - h * 0.4f, w * 0.35f, h * 0.4f, _fillPaint);
        // Highlight-Bogen
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawArc(new SKRect(x - w * 0.25f, baseY - h * 0.7f, x + w * 0.15f, baseY - h * 0.2f),
            200, 80, false, _strokePaint);
    }

    private static void DrawStump(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var stumpW = w * 0.25f;
        canvas.DrawRect(x - stumpW, baseY - h * 0.4f, stumpW * 2, h * 0.4f, _fillPaint);
        // Oval oben (Schnittfläche)
        _fillPaint.Color = LightenColor(_fillPaint.Color, 0.1f);
        canvas.DrawOval(x, baseY - h * 0.4f, stumpW, h * 0.08f, _fillPaint);
        _fillPaint.Color = DarkenColor(_fillPaint.Color, 0.91f);
    }

    private static void DrawLog(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        // Horizontales Oval
        canvas.DrawOval(x, baseY - h * 0.25f, w * 0.4f, h * 0.2f, _fillPaint);
        // Rinde-Linie
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawLine(x - w * 0.35f, baseY - h * 0.25f, x + w * 0.35f, baseY - h * 0.25f, _strokePaint);
    }

    // --- Architektur ---

    private static void DrawPillar(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var pillarW = w * 0.12f;
        // Schaft
        canvas.DrawRect(x - pillarW, baseY - h, pillarW * 2, h, _fillPaint);
        // Kapitell (breiter oben)
        canvas.DrawRect(x - pillarW * 1.5f, baseY - h, pillarW * 3, h * 0.06f, _fillPaint);
        // Basis (breiter unten)
        canvas.DrawRect(x - pillarW * 1.3f, baseY - h * 0.04f, pillarW * 2.6f, h * 0.04f, _fillPaint);
    }

    private static void DrawArch(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var archW = w * 0.5f;
        var pillarW = w * 0.08f;

        // 2 Säulen
        canvas.DrawRect(x - archW - pillarW, baseY - h, pillarW * 2, h, _fillPaint);
        canvas.DrawRect(x + archW - pillarW, baseY - h, pillarW * 2, h, _fillPaint);

        // Bogen oben
        _path.Rewind();
        _path.MoveTo(x - archW - pillarW, baseY - h);
        _path.QuadTo(x, baseY - h - h * 0.25f, x + archW + pillarW, baseY - h);
        _strokePaint.StrokeWidth = pillarW * 2;
        canvas.DrawPath(_path, _strokePaint);
        _strokePaint.StrokeWidth = 1f;
    }

    private static void DrawBrokenWall(SKCanvas canvas, float x, float baseY, float h, float w, int index)
    {
        var wallW = w * 0.6f;
        // Gezackte Oberkante
        _path.Rewind();
        _path.MoveTo(x - wallW * 0.5f, baseY);
        _path.LineTo(x - wallW * 0.5f, baseY - h * 0.7f);
        // Zacken
        var seed = index * 2.3f;
        _path.LineTo(x - wallW * 0.2f, baseY - h * (0.6f + MathF.Sin(seed) * 0.15f));
        _path.LineTo(x, baseY - h * (0.8f + MathF.Sin(seed + 1f) * 0.1f));
        _path.LineTo(x + wallW * 0.15f, baseY - h * (0.5f + MathF.Sin(seed + 2f) * 0.2f));
        _path.LineTo(x + wallW * 0.4f, baseY - h * (0.65f + MathF.Sin(seed + 3f) * 0.1f));
        _path.LineTo(x + wallW * 0.5f, baseY);
        _path.Close();
        canvas.DrawPath(_path, _fillPaint);
    }

    // --- Innenraum ---

    private static void DrawBookshelf(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var shelfW = w * 0.5f;
        // Regal-Rahmen
        canvas.DrawRect(x - shelfW, baseY - h, shelfW * 2, h, _fillPaint);
        // Regalböden (4 Linien)
        _strokePaint.StrokeWidth = 1.5f;
        for (int i = 1; i <= 4; i++)
            canvas.DrawLine(x - shelfW, baseY - h * (i * 0.2f),
                x + shelfW, baseY - h * (i * 0.2f), _strokePaint);
    }

    private static void DrawTable(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var tableW = w * 0.5f;
        var legW = w * 0.04f;

        // Platte
        canvas.DrawRect(x - tableW, baseY - h * 0.55f, tableW * 2, h * 0.08f, _fillPaint);
        // Beine
        canvas.DrawRect(x - tableW * 0.85f, baseY - h * 0.55f, legW, h * 0.55f, _fillPaint);
        canvas.DrawRect(x + tableW * 0.85f - legW, baseY - h * 0.55f, legW, h * 0.55f, _fillPaint);
    }

    private static void DrawBarrel(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var barrelW = w * 0.2f;
        // Ovaler Körper
        canvas.DrawOval(x, baseY - h * 0.4f, barrelW, h * 0.38f, _fillPaint);
        // 2 Reifen
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawLine(x - barrelW * 0.9f, baseY - h * 0.25f,
            x + barrelW * 0.9f, baseY - h * 0.25f, _strokePaint);
        canvas.DrawLine(x - barrelW * 0.9f, baseY - h * 0.55f,
            x + barrelW * 0.9f, baseY - h * 0.55f, _strokePaint);
    }

    // --- Spezial ---

    private static void DrawThrone(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var throneW = w * 0.35f;

        // Hohe Rückenlehne
        canvas.DrawRect(x - throneW * 0.6f, baseY - h, throneW * 1.2f, h, _fillPaint);
        // Spitze oben
        _path.Rewind();
        _path.MoveTo(x - throneW * 0.6f, baseY - h);
        _path.LineTo(x, baseY - h - h * 0.15f);
        _path.LineTo(x + throneW * 0.6f, baseY - h);
        _path.Close();
        canvas.DrawPath(_path, _fillPaint);
        // Sitzfläche (breiterer Bereich)
        canvas.DrawRect(x - throneW, baseY - h * 0.4f, throneW * 2, h * 0.08f, _fillPaint);
        // Armlehnen
        canvas.DrawRect(x - throneW, baseY - h * 0.55f, throneW * 0.15f, h * 0.25f, _fillPaint);
        canvas.DrawRect(x + throneW * 0.85f, baseY - h * 0.55f, throneW * 0.15f, h * 0.25f, _fillPaint);
    }

    private static void DrawBanner(SKCanvas canvas, float x, float baseY, float h, float w, int index)
    {
        var bannerW = w * 0.2f;
        var bannerH = h * 0.7f;

        // Stange
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(x, baseY - h, x, baseY, _strokePaint);

        // Tuch (Rechteck + dreieckiger Abschluss)
        canvas.DrawRect(x, baseY - h + h * 0.1f, bannerW, bannerH * 0.7f, _fillPaint);
        _path.Rewind();
        _path.MoveTo(x, baseY - h + h * 0.1f + bannerH * 0.7f);
        _path.LineTo(x + bannerW * 0.5f, baseY - h + h * 0.1f + bannerH);
        _path.LineTo(x + bannerW, baseY - h + h * 0.1f + bannerH * 0.7f);
        _path.Close();
        canvas.DrawPath(_path, _fillPaint);

        // Akzent-Streifen
        _accentPaint.Color = LightenColor(_fillPaint.Color, 0.2f).WithAlpha(80);
        canvas.DrawRect(x + bannerW * 0.3f, baseY - h + h * 0.15f,
            bannerW * 0.4f, bannerH * 0.5f, _accentPaint);
    }

    private static void DrawSwordInGround(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        // Klinge (schmales Dreieck)
        _path.Rewind();
        _path.MoveTo(x, baseY - h);
        _path.LineTo(x - w * 0.04f, baseY - h * 0.2f);
        _path.LineTo(x + w * 0.04f, baseY - h * 0.2f);
        _path.Close();
        _accentPaint.Color = new SKColor(0xA0, 0xA8, 0xB0, 150);
        canvas.DrawPath(_path, _accentPaint);

        // Parierstange
        canvas.DrawLine(x - w * 0.12f, baseY - h * 0.2f,
            x + w * 0.12f, baseY - h * 0.2f, _strokePaint);
        // Griff
        _strokePaint.StrokeWidth = 3f;
        canvas.DrawLine(x, baseY - h * 0.2f, x, baseY - h * 0.05f, _strokePaint);
        _strokePaint.StrokeWidth = 1f;
    }

    private static void DrawRailing(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        _strokePaint.StrokeWidth = 1.5f;
        // Elegante vertikale Stäbe
        for (int i = -3; i <= 3; i++)
        {
            var rx = x + i * w * 0.1f;
            canvas.DrawLine(rx, baseY, rx, baseY - h * 0.6f, _strokePaint);
        }
        // Handlauf (oben)
        canvas.DrawLine(x - w * 0.35f, baseY - h * 0.6f,
            x + w * 0.35f, baseY - h * 0.6f, _strokePaint);
    }

    private static void DrawRuneCircle(SKCanvas canvas, float x, float baseY, float h, float w)
    {
        var radius = Math.Min(w * 0.35f, h * 0.35f);
        var cy = baseY - h * 0.4f;

        // Äußerer Kreis
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawCircle(x, cy, radius, _strokePaint);
        // Innerer Kreis
        canvas.DrawCircle(x, cy, radius * 0.6f, _strokePaint);
        // 4 Segmentlinien (Kreuz)
        for (int i = 0; i < 4; i++)
        {
            var angle = i * MathF.PI * 0.5f + MathF.PI * 0.25f;
            canvas.DrawLine(
                x + MathF.Cos(angle) * radius * 0.6f,
                cy + MathF.Sin(angle) * radius * 0.6f,
                x + MathF.Cos(angle) * radius,
                cy + MathF.Sin(angle) * radius,
                _strokePaint);
        }
    }

    private static void DrawGeometricFragment(SKCanvas canvas, float x, float baseY, float h, float w, int index)
    {
        // Zufälliges Dreieck, leicht rotiert
        var seed = index * 3.7f;
        var size = h * 0.3f;

        canvas.Save();
        canvas.Translate(x, baseY - h * 0.5f);
        canvas.RotateDegrees(seed * 30f);

        _path.Rewind();
        _path.MoveTo(0, -size);
        _path.LineTo(-size * 0.7f, size * 0.5f);
        _path.LineTo(size * 0.7f, size * 0.5f);
        _path.Close();

        _fillPaint.Color = _fillPaint.Color.WithAlpha(100);
        canvas.DrawPath(_path, _fillPaint);
        _fillPaint.Color = _fillPaint.Color.WithAlpha(255);

        canvas.Restore();
    }

    public static void Cleanup()
    {
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _accentPaint.Dispose();
        _path.Dispose();
    }

    private static SKColor DarkenColor(SKColor c, float f) => new(
        (byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);

    private static SKColor LightenColor(SKColor c, float a) => new(
        (byte)Math.Min(255, c.Red + (255 - c.Red) * a),
        (byte)Math.Min(255, c.Green + (255 - c.Green) * a),
        (byte)Math.Min(255, c.Blue + (255 - c.Blue) * a), c.Alpha);
}
