using SkiaSharp;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// V7 (Plan Section 7.2): Procedurale Material-Icons (128×128) via SkiaSharp.
/// Schliesst die Asset-Pipeline-Luecke ohne externe AI-Bild-Tools — alle 33 Produkte +
/// 5 Affinity-Badges werden zur Laufzeit gerendert mit thematischer Form + Workshop-Farbe.
///
/// Cache: pro (ProductId, Size) ein SKBitmap (LRU-fähig, manuelle Dispose).
/// </summary>
public sealed class MaterialIconRenderer : IDisposable
{
    private bool _disposed;
    private const int CanvasSize = 128;

    private readonly Dictionary<string, SKBitmap> _cache = new();

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        Color = new SKColor(0x1A, 0x1A, 0x1A, 200)
    };
    private readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f)
    };
    private readonly SKFont _labelFont = new()
    {
        Size = 36f,
        Embolden = true,
        Edging = SKFontEdging.Antialias
    };

    /// <summary>
    /// Rendert das Material-Icon fuer eine Produkt-ID (gecacht).
    /// </summary>
    public SKBitmap GetIcon(string productId)
    {
        if (_cache.TryGetValue(productId, out var cached)) return cached;

        var bitmap = new SKBitmap(CanvasSize, CanvasSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        DrawProductIcon(canvas, productId);
        _cache[productId] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Rendert den Affinity-Badge (Wood/Metal/Stone/Art/Tech) direkt auf einen Canvas.
    /// </summary>
    public void RenderAffinityBadge(SKCanvas canvas, SKRect bounds, MaterialAffinity affinity)
    {
        if (_disposed) return;

        var color = GetAffinityColor(affinity);
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float r = Math.Min(bounds.Width, bounds.Height) * 0.45f;

        // Glow
        _glowPaint.Color = color.WithAlpha(120);
        canvas.DrawCircle(cx, cy, r * 1.1f, _glowPaint);

        // Hintergrund-Kreis mit Tier-Gradient
        using (var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - r * 0.3f, cy - r * 0.3f), r * 1.3f,
            [LightenColor(color, 0.4f), color, DarkenColor(color, 0.3f)],
            [0f, 0.6f, 1f],
            SKShaderTileMode.Clamp))
        {
            _fillPaint.Shader = shader;
            _fillPaint.Color = SKColors.White;
            canvas.DrawCircle(cx, cy, r, _fillPaint);
            _fillPaint.Shader = null;
        }

        // Stroke
        _strokePaint.Color = DarkenColor(color, 0.5f);
        canvas.DrawCircle(cx, cy, r, _strokePaint);

        // Affinity-Symbol in der Mitte
        DrawAffinitySymbol(canvas, cx, cy, r * 0.6f, affinity);
    }

    private void DrawProductIcon(SKCanvas canvas, string productId)
    {
        // Workshop-Farbe ermitteln (für Hintergrund-Tönung)
        var workshopColor = GetProductWorkshopColor(productId);
        int tier = GetProductTier(productId);

        // Hintergrund-Quad mit abgerundeten Ecken + Tier-Gradient
        var bgRect = new SKRect(8, 8, CanvasSize - 8, CanvasSize - 8);

        // Tier-Border-Glow (T4 = Gold-Halo)
        if (tier >= 4)
        {
            _glowPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180);
            using var rrPath = new SKPath();
            rrPath.AddRoundRect(bgRect, 14, 14);
            canvas.DrawPath(rrPath, _glowPaint);
        }

        // Hintergrund
        using (var shader = SKShader.CreateLinearGradient(
            new SKPoint(bgRect.Left, bgRect.Top),
            new SKPoint(bgRect.Right, bgRect.Bottom),
            [LightenColor(workshopColor, 0.25f), DarkenColor(workshopColor, 0.2f)],
            SKShaderTileMode.Clamp))
        {
            _fillPaint.Shader = shader;
            _fillPaint.Color = SKColors.White;
            canvas.DrawRoundRect(bgRect, 14, 14, _fillPaint);
            _fillPaint.Shader = null;
        }

        // Stroke + Tier-spezifische Akzent-Farbe
        _strokePaint.Color = tier switch
        {
            >= 4 => new SKColor(0xFF, 0xD7, 0x00),
            3 => new SKColor(0xC0, 0xC0, 0xC0),
            2 => new SKColor(0xCD, 0x7F, 0x32),
            _ => DarkenColor(workshopColor, 0.4f)
        };
        canvas.DrawRoundRect(bgRect, 14, 14, _strokePaint);

        // Produkt-spezifisches Symbol in der Mitte
        DrawProductSymbol(canvas, productId, tier);

        // Tier-Badge unten rechts (T2/T3/T4)
        if (tier >= 2)
        {
            DrawTierBadge(canvas, tier);
        }
    }

    private void DrawProductSymbol(SKCanvas canvas, string productId, int tier)
    {
        float cx = CanvasSize / 2f;
        float cy = CanvasSize / 2f - 6f;
        float scale = 1f + tier * 0.05f; // T4 etwas größer

        switch (productId)
        {
            case "planks":         DrawPlank(canvas, cx, cy, 36f * scale); break;
            case "furniture":      DrawChair(canvas, cx, cy, 38f * scale); break;
            case "luxury_furniture": DrawCrown(canvas, cx, cy, 36f * scale); break;
            case "pipes":          DrawPipe(canvas, cx, cy, 36f * scale); break;
            case "plumbing_system": DrawValve(canvas, cx, cy, 36f * scale); break;
            case "bathroom_installation": DrawShower(canvas, cx, cy, 36f * scale); break;
            case "cables":         DrawCable(canvas, cx, cy, 36f * scale); break;
            case "circuit":        DrawCircuit(canvas, cx, cy, 36f * scale); break;
            case "smart_home":     DrawWifi(canvas, cx, cy, 36f * scale); break;
            case "paint_mix":      DrawPaintBucket(canvas, cx, cy, 36f * scale); break;
            case "wall_design":    DrawPattern(canvas, cx, cy, 36f * scale); break;
            case "artwork":        DrawArtworkFrame(canvas, cx, cy, 36f * scale); break;
            case "roof_tiles":     DrawTile(canvas, cx, cy, 36f * scale); break;
            case "roofing_system": DrawRoof(canvas, cx, cy, 36f * scale); break;
            case "roof_structure": DrawRoofTrass(canvas, cx, cy, 36f * scale); break;
            case "concrete":       DrawConcreteBlock(canvas, cx, cy, 36f * scale); break;
            case "concrete_foundation": DrawFoundation(canvas, cx, cy, 36f * scale); break;
            case "skyscraper_frame": DrawSkyscraperFrame(canvas, cx, cy, 36f * scale); break;
            case "blueprint":      DrawBlueprint(canvas, cx, cy, 36f * scale); break;
            case "framework":      DrawFramework(canvas, cx, cy, 36f * scale); break;
            case "master_blueprint": DrawMasterBlueprint(canvas, cx, cy, 36f * scale); break;
            case "contract":       DrawContract(canvas, cx, cy, 36f * scale); break;
            case "contract_complex": DrawContractStack(canvas, cx, cy, 36f * scale); break;
            case "general_contract": DrawGoldenContract(canvas, cx, cy, 36f * scale); break;
            case "fittings":       DrawScrew(canvas, cx, cy, 36f * scale); break;
            case "master_fittings": DrawDoubleFitting(canvas, cx, cy, 36f * scale); break;
            case "masterpiece_fittings": DrawTrophyScrew(canvas, cx, cy, 36f * scale); break;
            case "prototype":      DrawLightbulb(canvas, cx, cy, 36f * scale); break;
            case "innovation":     DrawAtom(canvas, cx, cy, 36f * scale); break;
            case "patent":         DrawPatentStamp(canvas, cx, cy, 36f * scale); break;
            case "villa":          DrawVilla(canvas, cx, cy, 38f * scale); break;
            case "skyscraper":     DrawSkyscraper(canvas, cx, cy, 38f * scale); break;
            case "imperium_hq":    DrawCastle(canvas, cx, cy, 40f * scale); break;
            default:               DrawQuestionMark(canvas, cx, cy, 36f); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIMITIVE-SHAPES
    // ═══════════════════════════════════════════════════════════════════

    private void DrawPlank(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xA0, 0x52, 0x2D);
        c.DrawRoundRect(cx - s, cy - s * 0.35f, s * 2, s * 0.7f, 4, 4, _fillPaint);
        // Maserung
        _strokePaint.Color = new SKColor(0x6B, 0x2E, 0x0A);
        _strokePaint.StrokeWidth = 2;
        c.DrawLine(cx - s * 0.7f, cy - s * 0.1f, cx + s * 0.7f, cy - s * 0.1f, _strokePaint);
        c.DrawLine(cx - s * 0.7f, cy + s * 0.1f, cx + s * 0.7f, cy + s * 0.1f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
    }

    private void DrawChair(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x8B, 0x45, 0x13);
        // Lehne
        c.DrawRect(cx - s * 0.4f, cy - s * 0.8f, s * 0.8f, s * 0.5f, _fillPaint);
        // Sitz
        c.DrawRect(cx - s * 0.6f, cy - s * 0.2f, s * 1.2f, s * 0.3f, _fillPaint);
        // Beine
        c.DrawRect(cx - s * 0.55f, cy + s * 0.1f, s * 0.15f, s * 0.7f, _fillPaint);
        c.DrawRect(cx + s * 0.4f, cy + s * 0.1f, s * 0.15f, s * 0.7f, _fillPaint);
    }

    private void DrawCrown(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        using var path = new SKPath();
        path.MoveTo(cx - s, cy + s * 0.4f);
        path.LineTo(cx - s, cy - s * 0.2f);
        path.LineTo(cx - s * 0.55f, cy + s * 0.1f);
        path.LineTo(cx - s * 0.3f, cy - s * 0.6f);
        path.LineTo(cx, cy + s * 0.1f);
        path.LineTo(cx + s * 0.3f, cy - s * 0.6f);
        path.LineTo(cx + s * 0.55f, cy + s * 0.1f);
        path.LineTo(cx + s, cy - s * 0.2f);
        path.LineTo(cx + s, cy + s * 0.4f);
        path.Close();
        c.DrawPath(path, _fillPaint);
    }

    private void DrawPipe(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x71, 0x80, 0x8C);
        c.DrawRoundRect(cx - s, cy - s * 0.25f, s * 2, s * 0.5f, 4, 4, _fillPaint);
        _fillPaint.Color = new SKColor(0x45, 0x55, 0x60);
        c.DrawCircle(cx - s, cy, s * 0.3f, _fillPaint);
        c.DrawCircle(cx + s, cy, s * 0.3f, _fillPaint);
    }

    private void DrawValve(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x71, 0x80, 0x8C);
        c.DrawCircle(cx, cy, s * 0.7f, _fillPaint);
        _fillPaint.Color = new SKColor(0xC0, 0xC0, 0xC0);
        c.DrawCircle(cx, cy, s * 0.45f, _fillPaint);
        _strokePaint.Color = new SKColor(0x45, 0x55, 0x60);
        c.DrawLine(cx - s * 0.7f, cy, cx + s * 0.7f, cy, _strokePaint);
        c.DrawLine(cx, cy - s * 0.7f, cx, cy + s * 0.7f, _strokePaint);
    }

    private void DrawShower(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xC0, 0xC0, 0xC0);
        c.DrawRoundRect(cx - s * 0.5f, cy - s * 0.8f, s, s * 0.4f, 6, 6, _fillPaint);
        _fillPaint.Color = new SKColor(0x4A, 0xC8, 0xE8);
        for (int i = -2; i <= 2; i++)
        {
            float dx = i * s * 0.2f;
            c.DrawLine(cx + dx, cy - s * 0.3f, cx + dx, cy + s * 0.7f, _strokePaint);
        }
        _strokePaint.Color = new SKColor(0x4A, 0xC8, 0xE8);
        _strokePaint.StrokeWidth = 3;
        for (int i = -2; i <= 2; i++)
        {
            float dx = i * s * 0.2f;
            c.DrawLine(cx + dx, cy - s * 0.3f, cx + dx, cy + s * 0.7f, _strokePaint);
        }
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawCable(SKCanvas c, float cx, float cy, float s)
    {
        _strokePaint.Color = new SKColor(0xF9, 0x73, 0x16);
        _strokePaint.StrokeWidth = 8;
        using var path = new SKPath();
        path.MoveTo(cx - s, cy - s * 0.5f);
        path.CubicTo(cx - s * 0.3f, cy - s, cx + s * 0.3f, cy + s, cx + s, cy + s * 0.5f);
        c.DrawPath(path, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        c.DrawCircle(cx - s, cy - s * 0.5f, s * 0.2f, _fillPaint);
        c.DrawCircle(cx + s, cy + s * 0.5f, s * 0.2f, _fillPaint);
    }

    private void DrawCircuit(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x10, 0xB9, 0x81);
        c.DrawRoundRect(cx - s * 0.8f, cy - s * 0.8f, s * 1.6f, s * 1.6f, 6, 6, _fillPaint);
        _fillPaint.Color = new SKColor(0x05, 0x60, 0x40);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                c.DrawCircle(cx - s * 0.45f + x * s * 0.45f, cy - s * 0.45f + y * s * 0.45f, s * 0.1f, _fillPaint);
    }

    private void DrawWifi(SKCanvas c, float cx, float cy, float s)
    {
        _strokePaint.Color = new SKColor(0x3B, 0x82, 0xF6);
        _strokePaint.StrokeWidth = 6;
        for (int i = 1; i <= 3; i++)
        {
            var rect = new SKRect(cx - s * 0.3f * i, cy - s * 0.3f * i, cx + s * 0.3f * i, cy + s * 0.3f * i);
            c.DrawArc(rect, -135, 90, false, _strokePaint);
        }
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
        _fillPaint.Color = new SKColor(0x3B, 0x82, 0xF6);
        c.DrawCircle(cx, cy + s * 0.2f, s * 0.15f, _fillPaint);
    }

    private void DrawPaintBucket(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xEC, 0x48, 0x99);
        using var path = new SKPath();
        path.MoveTo(cx - s * 0.7f, cy - s * 0.4f);
        path.LineTo(cx + s * 0.7f, cy - s * 0.4f);
        path.LineTo(cx + s * 0.55f, cy + s * 0.7f);
        path.LineTo(cx - s * 0.55f, cy + s * 0.7f);
        path.Close();
        c.DrawPath(path, _fillPaint);
        // Pinsel-Drip
        _fillPaint.Color = new SKColor(0x9D, 0x17, 0x6E);
        c.DrawCircle(cx + s * 0.5f, cy + s * 0.8f, s * 0.12f, _fillPaint);
    }

    private void DrawPattern(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xEC, 0x48, 0x99);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            {
                var col = (x + y) % 2 == 0 ? new SKColor(0xEC, 0x48, 0x99) : new SKColor(0xFB, 0xCF, 0xE8);
                _fillPaint.Color = col;
                c.DrawRect(cx - s * 0.7f + x * s * 0.45f, cy - s * 0.7f + y * s * 0.45f, s * 0.4f, s * 0.4f, _fillPaint);
            }
    }

    private void DrawArtworkFrame(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        c.DrawRoundRect(cx - s * 0.8f, cy - s * 0.8f, s * 1.6f, s * 1.6f, 4, 4, _fillPaint);
        _fillPaint.Color = new SKColor(0x4A, 0x90, 0xD9);
        c.DrawRect(cx - s * 0.55f, cy - s * 0.55f, s * 1.1f, s * 1.1f, _fillPaint);
        // Pinselstrich
        _strokePaint.Color = new SKColor(0xEC, 0x48, 0x99);
        _strokePaint.StrokeWidth = 6;
        c.DrawLine(cx - s * 0.3f, cy + s * 0.2f, cx + s * 0.3f, cy - s * 0.2f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawTile(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26);
        using var path = new SKPath();
        path.MoveTo(cx - s * 0.8f, cy + s * 0.3f);
        path.LineTo(cx - s * 0.6f, cy - s * 0.3f);
        path.LineTo(cx + s * 0.6f, cy - s * 0.3f);
        path.LineTo(cx + s * 0.8f, cy + s * 0.3f);
        path.Close();
        c.DrawPath(path, _fillPaint);
        _strokePaint.Color = new SKColor(0x7F, 0x1D, 0x1D);
        _strokePaint.StrokeWidth = 3;
        c.DrawLine(cx, cy - s * 0.3f, cx, cy + s * 0.3f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawRoof(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26);
        using var path = new SKPath();
        path.MoveTo(cx, cy - s);
        path.LineTo(cx + s, cy + s * 0.3f);
        path.LineTo(cx - s, cy + s * 0.3f);
        path.Close();
        c.DrawPath(path, _fillPaint);
        _fillPaint.Color = new SKColor(0x7F, 0x1D, 0x1D);
        c.DrawRect(cx - s * 0.9f, cy + s * 0.3f, s * 1.8f, s * 0.4f, _fillPaint);
    }

    private void DrawRoofTrass(SKCanvas c, float cx, float cy, float s)
    {
        _strokePaint.Color = new SKColor(0xDC, 0x26, 0x26);
        _strokePaint.StrokeWidth = 6;
        c.DrawLine(cx - s, cy + s * 0.6f, cx, cy - s, _strokePaint);
        c.DrawLine(cx, cy - s, cx + s, cy + s * 0.6f, _strokePaint);
        c.DrawLine(cx - s, cy + s * 0.6f, cx + s, cy + s * 0.6f, _strokePaint);
        c.DrawLine(cx - s * 0.5f, cy - s * 0.2f, cx + s * 0.5f, cy - s * 0.2f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawConcreteBlock(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x9C, 0xA3, 0xAF);
        c.DrawRect(cx - s * 0.9f, cy - s * 0.6f, s * 1.8f, s * 1.2f, _fillPaint);
        _strokePaint.Color = new SKColor(0x4B, 0x55, 0x63);
        _strokePaint.StrokeWidth = 3;
        c.DrawRect(cx - s * 0.9f, cy - s * 0.6f, s * 1.8f, s * 1.2f, _strokePaint);
        c.DrawLine(cx, cy - s * 0.6f, cx, cy + s * 0.6f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawFoundation(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x4B, 0x55, 0x63);
        c.DrawRect(cx - s * 0.95f, cy + s * 0.1f, s * 1.9f, s * 0.7f, _fillPaint);
        _fillPaint.Color = new SKColor(0x6B, 0x72, 0x80);
        c.DrawRect(cx - s * 0.7f, cy - s * 0.4f, s * 1.4f, s * 0.5f, _fillPaint);
        // Stahltraeger
        _strokePaint.Color = new SKColor(0xC0, 0xC0, 0xC0);
        _strokePaint.StrokeWidth = 5;
        c.DrawLine(cx - s * 0.6f, cy - s * 0.7f, cx - s * 0.6f, cy + s * 0.7f, _strokePaint);
        c.DrawLine(cx + s * 0.6f, cy - s * 0.7f, cx + s * 0.6f, cy + s * 0.7f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawSkyscraperFrame(SKCanvas c, float cx, float cy, float s)
    {
        _strokePaint.Color = new SKColor(0xC0, 0xC0, 0xC0);
        _strokePaint.StrokeWidth = 5;
        for (int row = 0; row < 4; row++)
        {
            float y = cy - s + row * s * 0.55f;
            c.DrawRect(cx - s * 0.5f, y, s * 1.0f, s * 0.55f, _strokePaint);
        }
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawBlueprint(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x1E, 0x40, 0xAF);
        c.DrawRect(cx - s * 0.9f, cy - s * 0.7f, s * 1.8f, s * 1.4f, _fillPaint);
        _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 200);
        _strokePaint.StrokeWidth = 2;
        // Grid + ein Rechteck als "Plan"
        for (int i = -2; i <= 2; i++)
        {
            c.DrawLine(cx - s * 0.9f, cy + i * s * 0.3f, cx + s * 0.9f, cy + i * s * 0.3f, _strokePaint);
            c.DrawLine(cx + i * s * 0.3f, cy - s * 0.7f, cx + i * s * 0.3f, cy + s * 0.7f, _strokePaint);
        }
        _strokePaint.StrokeWidth = 4;
        c.DrawRect(cx - s * 0.4f, cy - s * 0.3f, s * 0.8f, s * 0.6f, _strokePaint);
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawFramework(SKCanvas c, float cx, float cy, float s)
    {
        DrawBlueprint(c, cx, cy, s);
        _strokePaint.Color = new SKColor(0xFB, 0xBF, 0x24);
        _strokePaint.StrokeWidth = 4;
        c.DrawLine(cx - s * 0.7f, cy + s * 0.5f, cx + s * 0.7f, cy - s * 0.5f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawMasterBlueprint(SKCanvas c, float cx, float cy, float s)
    {
        DrawFramework(c, cx, cy, s);
        DrawCrown(c, cx + s * 0.55f, cy - s * 0.55f, s * 0.35f);
    }

    private void DrawContract(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xF5, 0xF5, 0xDC);
        c.DrawRoundRect(cx - s * 0.65f, cy - s * 0.85f, s * 1.3f, s * 1.7f, 6, 6, _fillPaint);
        _strokePaint.Color = new SKColor(0x4B, 0x55, 0x63);
        _strokePaint.StrokeWidth = 2;
        for (int i = 0; i < 5; i++)
            c.DrawLine(cx - s * 0.5f, cy - s * 0.5f + i * s * 0.25f, cx + s * 0.5f, cy - s * 0.5f + i * s * 0.25f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
        // Stempel
        _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26, 200);
        c.DrawCircle(cx + s * 0.35f, cy + s * 0.55f, s * 0.18f, _fillPaint);
    }

    private void DrawContractStack(SKCanvas c, float cx, float cy, float s)
    {
        DrawContract(c, cx + s * 0.15f, cy + s * 0.1f, s * 0.85f);
        DrawContract(c, cx - s * 0.15f, cy - s * 0.1f, s * 0.85f);
    }

    private void DrawGoldenContract(SKCanvas c, float cx, float cy, float s)
    {
        // Goldener Stempel-Auftrag
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        c.DrawRoundRect(cx - s * 0.65f, cy - s * 0.85f, s * 1.3f, s * 1.7f, 6, 6, _fillPaint);
        _strokePaint.Color = new SKColor(0xB4, 0x88, 0x06);
        _strokePaint.StrokeWidth = 3;
        for (int i = 0; i < 5; i++)
            c.DrawLine(cx - s * 0.5f, cy - s * 0.5f + i * s * 0.25f, cx + s * 0.5f, cy - s * 0.5f + i * s * 0.25f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
        DrawCrown(c, cx, cy - s * 0.3f, s * 0.35f);
    }

    private void DrawScrew(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        c.DrawCircle(cx, cy - s * 0.55f, s * 0.35f, _fillPaint);
        c.DrawRect(cx - s * 0.15f, cy - s * 0.4f, s * 0.3f, s, _fillPaint);
        // Spitze
        using var path = new SKPath();
        path.MoveTo(cx - s * 0.15f, cy + s * 0.6f);
        path.LineTo(cx, cy + s * 0.95f);
        path.LineTo(cx + s * 0.15f, cy + s * 0.6f);
        path.Close();
        c.DrawPath(path, _fillPaint);
        // Schlitz
        _strokePaint.Color = new SKColor(0xB4, 0x88, 0x06);
        _strokePaint.StrokeWidth = 3;
        c.DrawLine(cx - s * 0.2f, cy - s * 0.55f, cx + s * 0.2f, cy - s * 0.55f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawDoubleFitting(SKCanvas c, float cx, float cy, float s)
    {
        DrawScrew(c, cx - s * 0.4f, cy, s * 0.7f);
        DrawScrew(c, cx + s * 0.4f, cy, s * 0.7f);
    }

    private void DrawTrophyScrew(SKCanvas c, float cx, float cy, float s)
    {
        DrawCrown(c, cx, cy - s * 0.55f, s * 0.4f);
        DrawScrew(c, cx, cy + s * 0.2f, s * 0.7f);
    }

    private void DrawLightbulb(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xFD, 0xE0, 0x47);
        c.DrawCircle(cx, cy - s * 0.2f, s * 0.5f, _fillPaint);
        _fillPaint.Color = new SKColor(0x4B, 0x55, 0x63);
        c.DrawRect(cx - s * 0.25f, cy + s * 0.3f, s * 0.5f, s * 0.4f, _fillPaint);
        _strokePaint.Color = new SKColor(0xB4, 0x88, 0x06);
        _strokePaint.StrokeWidth = 2;
        c.DrawLine(cx - s * 0.25f, cy + s * 0.45f, cx + s * 0.25f, cy + s * 0.45f, _strokePaint);
        c.DrawLine(cx - s * 0.25f, cy + s * 0.6f, cx + s * 0.25f, cy + s * 0.6f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawAtom(SKCanvas c, float cx, float cy, float s)
    {
        _strokePaint.Color = new SKColor(0x8B, 0x5C, 0xF6);
        _strokePaint.StrokeWidth = 4;
        using var ellipse1 = new SKPath();
        ellipse1.AddOval(new SKRect(cx - s * 0.8f, cy - s * 0.3f, cx + s * 0.8f, cy + s * 0.3f));
        c.Save();
        c.RotateDegrees(30, cx, cy);
        c.DrawPath(ellipse1, _strokePaint);
        c.Restore();
        c.Save();
        c.RotateDegrees(-30, cx, cy);
        c.DrawPath(ellipse1, _strokePaint);
        c.Restore();
        _fillPaint.Color = new SKColor(0xC4, 0xB5, 0xFD);
        c.DrawCircle(cx, cy, s * 0.2f, _fillPaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
    }

    private void DrawPatentStamp(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        c.DrawCircle(cx, cy, s * 0.85f, _fillPaint);
        _strokePaint.Color = new SKColor(0xB4, 0x88, 0x06);
        _strokePaint.StrokeWidth = 5;
        c.DrawCircle(cx, cy, s * 0.85f, _strokePaint);
        _strokePaint.StrokeWidth = 4;
        _strokePaint.Color = new SKColor(0x1A, 0x1A, 0x1A, 200);
        DrawStarShape(c, cx, cy, s * 0.5f, new SKColor(0xB4, 0x88, 0x06));
    }

    private void DrawVilla(SKCanvas c, float cx, float cy, float s)
    {
        // Dach
        _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26);
        using var path = new SKPath();
        path.MoveTo(cx - s, cy - s * 0.2f);
        path.LineTo(cx, cy - s);
        path.LineTo(cx + s, cy - s * 0.2f);
        path.Close();
        c.DrawPath(path, _fillPaint);
        // Hauptgebäude
        _fillPaint.Color = new SKColor(0xF5, 0xF5, 0xDC);
        c.DrawRect(cx - s * 0.8f, cy - s * 0.2f, s * 1.6f, s * 0.9f, _fillPaint);
        // Tür
        _fillPaint.Color = new SKColor(0x8B, 0x45, 0x13);
        c.DrawRect(cx - s * 0.15f, cy + s * 0.2f, s * 0.3f, s * 0.5f, _fillPaint);
        // Fenster
        _fillPaint.Color = new SKColor(0x4A, 0x90, 0xD9);
        c.DrawRect(cx - s * 0.55f, cy + s * 0.05f, s * 0.25f, s * 0.25f, _fillPaint);
        c.DrawRect(cx + s * 0.3f, cy + s * 0.05f, s * 0.25f, s * 0.25f, _fillPaint);
    }

    private void DrawSkyscraper(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x6B, 0x72, 0x80);
        c.DrawRect(cx - s * 0.45f, cy - s, s * 0.9f, s * 1.9f, _fillPaint);
        _fillPaint.Color = new SKColor(0x4A, 0xC8, 0xE8);
        for (int row = 0; row < 6; row++)
            for (int col = 0; col < 3; col++)
            {
                c.DrawRect(
                    cx - s * 0.35f + col * s * 0.3f,
                    cy - s * 0.85f + row * s * 0.3f,
                    s * 0.2f, s * 0.2f, _fillPaint);
            }
    }

    private void DrawCastle(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x6B, 0x72, 0x80);
        // Hauptkörper
        c.DrawRect(cx - s, cy - s * 0.4f, s * 2, s * 1.2f, _fillPaint);
        // Zinnen (3 Türme)
        for (int i = 0; i < 3; i++)
        {
            float tx = cx - s + i * s;
            c.DrawRect(tx - s * 0.18f, cy - s, s * 0.36f, s * 0.6f, _fillPaint);
            // Zinnen-Spitzen
            using var path = new SKPath();
            path.MoveTo(tx - s * 0.18f, cy - s);
            path.LineTo(tx - s * 0.18f, cy - s * 1.1f);
            path.LineTo(tx - s * 0.05f, cy - s * 1.1f);
            path.LineTo(tx - s * 0.05f, cy - s);
            path.MoveTo(tx + s * 0.05f, cy - s);
            path.LineTo(tx + s * 0.05f, cy - s * 1.1f);
            path.LineTo(tx + s * 0.18f, cy - s * 1.1f);
            path.LineTo(tx + s * 0.18f, cy - s);
            c.DrawPath(path, _fillPaint);
        }
        // Tor
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        c.DrawRect(cx - s * 0.18f, cy + s * 0.2f, s * 0.36f, s * 0.6f, _fillPaint);
    }

    private void DrawQuestionMark(SKCanvas c, float cx, float cy, float s)
    {
        _fillPaint.Color = new SKColor(0x9C, 0xA3, 0xAF);
        c.DrawCircle(cx, cy, s * 0.7f, _fillPaint);
        _fillPaint.Color = SKColors.White;
        var labelBounds = new SKRect();
        _labelFont.MeasureText("?", out labelBounds);
        c.DrawText("?", cx - labelBounds.Width * 0.5f, cy + labelBounds.Height * 0.35f, _labelFont, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BADGES + HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private void DrawTierBadge(SKCanvas c, int tier)
    {
        // unten rechts
        float bx = CanvasSize - 24;
        float by = CanvasSize - 24;
        SKColor color = tier switch
        {
            >= 4 => new SKColor(0xFF, 0xD7, 0x00),
            3 => new SKColor(0xC0, 0xC0, 0xC0),
            _ => new SKColor(0xCD, 0x7F, 0x32)
        };
        _glowPaint.Color = color.WithAlpha(140);
        c.DrawCircle(bx, by, 18, _glowPaint);
        _fillPaint.Color = color;
        c.DrawCircle(bx, by, 14, _fillPaint);
        _fillPaint.Color = new SKColor(0x1A, 0x1A, 0x1A);
        var tt = "T" + tier;
        var tw = _labelFont.MeasureText(tt);
        _labelFont.Size = 18;
        c.DrawText(tt, bx - tw * 0.5f, by + 6, _labelFont, _fillPaint);
        _labelFont.Size = 36;
    }

    private void DrawAffinitySymbol(SKCanvas c, float cx, float cy, float r, MaterialAffinity affinity)
    {
        _fillPaint.Color = SKColors.White;
        switch (affinity)
        {
            case MaterialAffinity.Wood: DrawPlank(c, cx, cy, r * 0.8f); break;
            case MaterialAffinity.Metal: DrawScrew(c, cx, cy, r * 0.7f); break;
            case MaterialAffinity.Stone: DrawConcreteBlock(c, cx, cy, r * 0.7f); break;
            case MaterialAffinity.Art: DrawPaintBucket(c, cx, cy, r * 0.7f); break;
            case MaterialAffinity.Tech: DrawCircuit(c, cx, cy, r * 0.7f); break;
            default: DrawQuestionMark(c, cx, cy, r * 0.7f); break;
        }
    }

    private void DrawStarShape(SKCanvas c, float cx, float cy, float r, SKColor color)
    {
        _fillPaint.Color = color;
        using var path = new SKPath();
        for (int i = 0; i < 10; i++)
        {
            double angle = i * Math.PI / 5 - Math.PI / 2;
            double radius = i % 2 == 0 ? r : r * 0.45;
            float x = cx + (float)(Math.Cos(angle) * radius);
            float y = cy + (float)(Math.Sin(angle) * radius);
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        c.DrawPath(path, _fillPaint);
    }

    private static SKColor LightenColor(SKColor c, float amount)
    {
        return new SKColor(
            (byte)Math.Min(255, c.Red + (255 - c.Red) * amount),
            (byte)Math.Min(255, c.Green + (255 - c.Green) * amount),
            (byte)Math.Min(255, c.Blue + (255 - c.Blue) * amount),
            c.Alpha);
    }

    private static SKColor DarkenColor(SKColor c, float amount)
    {
        return new SKColor(
            (byte)(c.Red * (1 - amount)),
            (byte)(c.Green * (1 - amount)),
            (byte)(c.Blue * (1 - amount)),
            c.Alpha);
    }

    private static SKColor GetAffinityColor(MaterialAffinity a) => a switch
    {
        MaterialAffinity.Wood => new SKColor(0xA0, 0x52, 0x2D),
        MaterialAffinity.Metal => new SKColor(0x71, 0x80, 0x8C),
        MaterialAffinity.Stone => new SKColor(0x9C, 0xA3, 0xAF),
        MaterialAffinity.Art => new SKColor(0xEC, 0x48, 0x99),
        MaterialAffinity.Tech => new SKColor(0x10, 0xB9, 0x81),
        _ => new SKColor(0x6B, 0x72, 0x80)
    };

    private static SKColor GetProductWorkshopColor(string productId) => productId switch
    {
        "planks" or "furniture" or "luxury_furniture" => new SKColor(0xA0, 0x52, 0x2D),
        "pipes" or "plumbing_system" or "bathroom_installation" => new SKColor(0x0E, 0x74, 0x90),
        "cables" or "circuit" or "smart_home" => new SKColor(0xF9, 0x73, 0x16),
        "paint_mix" or "wall_design" or "artwork" => new SKColor(0xEC, 0x48, 0x99),
        "roof_tiles" or "roofing_system" or "roof_structure" => new SKColor(0xDC, 0x26, 0x26),
        "concrete" or "concrete_foundation" or "skyscraper_frame" => new SKColor(0xEA, 0x58, 0x0C),
        "blueprint" or "framework" or "master_blueprint" => new SKColor(0x78, 0x71, 0x6C),
        "contract" or "contract_complex" or "general_contract" => new SKColor(0xFF, 0xD7, 0x00),
        "fittings" or "master_fittings" or "masterpiece_fittings" => new SKColor(0xD4, 0xA3, 0x73),
        "prototype" or "innovation" or "patent" => new SKColor(0x6A, 0x5A, 0xCD),
        "villa" or "skyscraper" or "imperium_hq" => new SKColor(0xFF, 0xD7, 0x00),
        _ => new SKColor(0x6B, 0x72, 0x80)
    };

    private static int GetProductTier(string productId)
    {
        var product = Models.CraftingProduct.GetAllProducts().GetValueOrDefault(productId);
        return product?.Tier ?? 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var bm in _cache.Values) bm.Dispose();
        _cache.Clear();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _glowPaint.Dispose();
        _labelFont.Dispose();
    }
}
