using System;
using System.Collections.Generic;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using MeineApps.UI.SkiaSharp.Shaders;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert Workshop-Szenen als AI-generierte Bitmap-Hintergründe.
/// Overlay-Effekte: Level-Sterne/Glow/Shimmer, Idle-Warnung.
/// Kein prozeduraler Fallback - alle 10 Workshop-Typen haben AI-Bilder.
/// </summary>
public sealed class WorkshopSceneRenderer : IDisposable
{
    // Workshop-Typ → Asset-Dateiname
    private static readonly Dictionary<WorkshopType, string> AssetNames = new()
    {
        { WorkshopType.Carpenter, "carpenter" },
        { WorkshopType.Plumber, "plumber" },
        { WorkshopType.Electrician, "electrician" },
        { WorkshopType.Painter, "painter" },
        { WorkshopType.Roofer, "roofer" },
        { WorkshopType.Contractor, "contractor" },
        { WorkshopType.Architect, "architect" },
        { WorkshopType.GeneralContractor, "general_contractor" },
        { WorkshopType.MasterSmith, "master_smith" },
        { WorkshopType.InnovationLab, "innovation_lab" },
    };

    private IGameAssetService? _assetService;

    // Gecachte Paints (Idle-Overlay + Level-Effekte)
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    public void Initialize(IGameAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Zeichnet die Workshop-Szene: AI-Bitmap + Level-Overlay-Effekte + Partikel-Callbacks.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, Workshop workshop,
        float phase, int activeWorkers, float speed, int particleRate, int productCount,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        // AI-Hintergrund zeichnen
        DrawBackground(canvas, bounds, workshop.Type);

        // Level-basierte Overlay-Effekte (Sterne, Gold-Aura, Shimmer)
        DrawLevelEffects(canvas, bounds, workshop.Level, phase);

        // Partikel-Callbacks feuern (WorkParticle + CoinParticle)
        float p = phase * speed;
        if (activeWorkers > 0)
        {
            if (p % 2.0f < 0.05f)
                addWorkParticle?.Invoke(bounds.MidX, bounds.Top, GetWorkshopColor(workshop.Type));
            if (p % 5.0f < 0.05f)
                addCoinParticle?.Invoke(bounds.MidX, bounds.Top);
        }
    }

    /// <summary>
    /// Zeichnet den Leerlauf-Zustand (0 aktive Worker): Gedimmtes Bild + Warnsymbol.
    /// </summary>
    public void RenderIdle(SKCanvas canvas, SKRect bounds, Workshop workshop)
    {
        // AI-Hintergrund zeichnen
        DrawBackground(canvas, bounds, workshop.Type);

        // Dimm-Overlay
        _fillPaint.Color = new SKColor(0x00, 0x00, 0x00, 80);
        canvas.DrawRect(bounds, _fillPaint);

        // Warnsymbol (⚠ Dreieck) zentral
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float triSize = 20;

        // Dreieck-Hintergrund (gelb)
        _fillPaint.Color = new SKColor(0xFF, 0xC1, 0x07, 200);
        canvas.DrawCircle(cx, cy, triSize * 0.8f, _fillPaint);

        // Ausrufezeichen
        _fillPaint.Color = new SKColor(0x42, 0x42, 0x42);
        canvas.DrawRect(cx - 1.5f, cy - triSize * 0.3f, 3, triSize * 0.35f, _fillPaint);
        canvas.DrawCircle(cx, cy + triSize * 0.25f, 2, _fillPaint);
    }

    /// <summary>
    /// Lädt und zeichnet das AI-Hintergrundbild für den Workshop-Typ.
    /// </summary>
    private void DrawBackground(SKCanvas canvas, SKRect bounds, WorkshopType type)
    {
        if (_assetService == null) return;

        var assetName = AssetNames.GetValueOrDefault(type, "carpenter");
        var assetPath = $"workshops/{assetName}.webp";
        var bitmap = _assetService.GetBitmap(assetPath);
        if (bitmap == null)
        {
            _ = _assetService.LoadBitmapAsync(assetPath);
            return;
        }

        canvas.DrawBitmap(bitmap, bounds);
    }

    /// <summary>
    /// Workshop-Farbe für Partikel-Effekte.
    /// </summary>
    private static SKColor GetWorkshopColor(WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => new SKColor(0xA0, 0x52, 0x2D),
        WorkshopType.Plumber => new SKColor(0x0E, 0x74, 0x90),
        WorkshopType.Electrician => new SKColor(0xF9, 0x73, 0x16),
        WorkshopType.Painter => new SKColor(0xEC, 0x48, 0x99),
        WorkshopType.Roofer => new SKColor(0xDC, 0x26, 0x26),
        WorkshopType.Contractor => new SKColor(0xEA, 0x58, 0x0C),
        WorkshopType.Architect => new SKColor(0x78, 0x71, 0x6C),
        WorkshopType.GeneralContractor => new SKColor(0xFF, 0xD7, 0x00),
        WorkshopType.MasterSmith => new SKColor(0xD4, 0xA3, 0x73),
        WorkshopType.InnovationLab => new SKColor(0x6A, 0x5A, 0xCD),
        _ => new SKColor(0xEA, 0x58, 0x0C)
    };

    // ═══════════════════════════════════════════════════════════════════════
    // LEVEL-EFFEKTE (Overlay auf AI-Hintergrund)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawLevelEffects(SKCanvas canvas, SKRect bounds, int level, float phase)
    {
        if (level < 50) return;

        // Level 250+: Pulsierende goldene Sterne in den Ecken
        if (level >= 250)
        {
            float starPulse = 0.5f + MathF.Sin(phase * 2.5f) * 0.4f;
            float starSize = 5 + starPulse * 3;
            DrawMiniStar(canvas, bounds.Left + 14, bounds.Top + 12, starSize, phase);
            DrawMiniStar(canvas, bounds.Right - 14, bounds.Top + 12, starSize, phase * 1.3f);
            DrawMiniStar(canvas, bounds.Left + 14, bounds.Bottom - 12, starSize, phase * 0.7f);
            if (level >= 500)
                DrawMiniStar(canvas, bounds.Right - 14, bounds.Bottom - 12, starSize, phase * 0.8f);
        }

        // Level 500+: Premium-Gold-Aura (GPU-Shader)
        if (level >= 500)
        {
            SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, phase,
                new SKColor(0xFF, 0xD7, 0x00, 80),
                pulseSpeed: 1.5f, pulseMin: 0.02f, pulseMax: 0.07f);
        }

        // Level 1000: Gold-Shimmer über gesamte Szene
        if (level >= 1000)
        {
            SkiaShimmerEffect.DrawGoldShimmer(canvas, bounds, phase);
        }
    }

    /// <summary>
    /// Kleiner pulsierender Stern mit 8 Strahlen und hellem Kern.
    /// </summary>
    private void DrawMiniStar(SKCanvas canvas, float cx, float cy, float size, float phase)
    {
        float kernPulse = 0.6f + MathF.Sin(phase * 3f) * 0.4f;
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(kernPulse * 100));
        canvas.DrawCircle(cx, cy, size * 0.5f, _fillPaint);

        _strokePaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        for (int i = 0; i < 8; i++)
        {
            float a = phase * 0.5f + i * MathF.PI / 4;
            float rayLen = (i % 2 == 0) ? size : size * 0.6f;
            canvas.DrawLine(cx, cy,
                cx + MathF.Cos(a) * rayLen, cy + MathF.Sin(a) * rayLen, _strokePaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _fillPaint.Dispose();
        _strokePaint.Dispose();
    }
}
