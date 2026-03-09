using System;
using Avalonia.Platform;
using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Splash-Screen-Renderer "Die Schmiede" für HandwerkerImperium.
/// Zeigt zwei gegenläufige Zahnräder, einen hämmernden Amboss mit Funken,
/// aufsteigende Glut-Partikel und einen Craft-Orange→Gold Fortschrittsbalken.
/// Aufgewertet gegenüber dem alten LoadingScreenRenderer: Vignette, Amboss+Hammer-Animation,
/// Aufschlag-Funken, Feuer-Glut am unteren Rand.
/// </summary>
public sealed class HandwerkerImperiumSplashRenderer : SplashRendererBase
{
    // --- Partikel-Konfiguration ---
    private const int MaxSparks = 12;
    private const int MaxEmbers = 10;
    private const int MaxGearSparks = 6;

    // --- Partikel-Pools (Struct-Arrays, kein GC-Druck) ---
    private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];
    private readonly EmberParticle[] _embers = new EmberParticle[MaxEmbers];
    private int _activeSparks;

    // --- Hammer-Animation ---
    private float _hammerPhase; // 0-1 zyklisch
    private const float HammerCycleDuration = 1.5f;

    // --- Gecachte Paints (kein per-frame Allokation) ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titleGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _subtitlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gearPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gearStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _anvilPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _anvilStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _hammerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _sparkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _emberPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gearSparkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts ---
    private readonly SKFont _titleFont = new() { Embolden = true, Size = 28f };
    private readonly SKFont _subtitleFont = new() { Size = 14f };

    // --- Gecachte Pfade ---
    private readonly SKPath _gearPath = new();
    private readonly SKPath _hammerPath = new();

    // --- AI-Splash-Hintergrund (direkt aus AvaloniaResource, kein DI nötig) ---
    private SKBitmap? _splashBackground;
    private bool _splashLoadAttempted;

    // --- Gecachte MaskFilter ---
    private SKMaskFilter? _titleGlowFilter;

    // --- Farb-Konstanten ---
    private static readonly SKColor BgTop = new(0x1A, 0x1A, 0x2E);
    private static readonly SKColor BgBottom = new(0x0D, 0x0D, 0x1A);
    private static readonly SKColor CraftOrange = new(0xEA, 0x58, 0x0C);
    private static readonly SKColor GoldOrange = new(0xFF, 0xA7, 0x26);
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor GoldenSpark = new(0xFF, 0xD5, 0x4F);
    private static readonly SKColor AnvilBody = new(0x2A, 0x2A, 0x3E);
    private static readonly SKColor AnvilStroke = new(0x3A, 0x3A, 0x4E);
    private static readonly SKColor HammerHead = new(0x60, 0x60, 0x70);
    private static readonly SKColor HammerHandle = new(0x8B, 0x69, 0x14);

    // --- Structs ---
    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife;
        public byte Alpha;
    }

    private struct EmberParticle
    {
        public float X, Y, Alpha, Speed, Phase;
    }

    private void InitializeEmbers(float w, float h)
    {
        if (IsInitialized) return;
        IsInitialized = true;

        for (var i = 0; i < MaxEmbers; i++)
        {
            _embers[i] = new EmberParticle
            {
                X = (float)Rng.NextDouble() * w,
                Y = h * (0.85f + (float)Rng.NextDouble() * 0.15f),
                Alpha = 60f + (float)Rng.NextDouble() * 140f,
                Speed = 15f + (float)Rng.NextDouble() * 30f,
                Phase = (float)(Rng.NextDouble() * Math.PI * 2)
            };
        }
    }

    // ═══════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════

    protected override void OnUpdate(float deltaTime)
    {
        // Hammer-Zyklus
        _hammerPhase += deltaTime / HammerCycleDuration;
        if (_hammerPhase >= 1f)
        {
            _hammerPhase -= 1f;
        }

        // Bei Aufschlag (Phase ~0.5) Funken erzeugen
        if (_hammerPhase is > 0.49f and < 0.52f)
        {
            SpawnImpactSparks();
        }

        // Funken-Update
        for (var i = 0; i < _activeSparks; i++)
        {
            ref var s = ref _sparks[i];
            s.Life -= deltaTime;
            if (s.Life <= 0f)
            {
                // Kompaktierung: letzten aktiven an diese Stelle
                if (i < _activeSparks - 1)
                    _sparks[i] = _sparks[_activeSparks - 1];
                _activeSparks--;
                i--;
                continue;
            }
            s.X += s.VelocityX * deltaTime;
            s.Y += s.VelocityY * deltaTime;
            s.VelocityY += 120f * deltaTime; // Schwerkraft
            s.Alpha = (byte)(255f * (s.Life / s.MaxLife));
        }

        // Glut-Partikel Update
        for (var i = 0; i < MaxEmbers; i++)
        {
            ref var e = ref _embers[i];
            e.Phase += deltaTime * 1.5f;
            e.Y -= e.Speed * deltaTime;
            e.Alpha -= 30f * deltaTime;

            // Glut zurücksetzen wenn unsichtbar oder oben angekommen
            if (e.Alpha <= 0f || e.Y < 0f)
            {
                e.Y = 0f; // Wird beim Rendern auf echte Höhe gesetzt
                e.Alpha = 80f + (float)Rng.NextDouble() * 120f;
                e.Speed = 15f + (float)Rng.NextDouble() * 30f;
                e.Phase = (float)(Rng.NextDouble() * Math.PI * 2);
            }
        }
    }

    private void SpawnImpactSparks()
    {
        // 8-10 Funken pro Aufschlag
        var count = 8 + Rng.Next(3);
        for (var i = 0; i < count && _activeSparks < MaxSparks; i++)
        {
            var angle = (float)(Rng.NextDouble() * Math.PI) - MathF.PI; // nach oben/seitlich
            var speed = 80f + (float)Rng.NextDouble() * 120f;
            _sparks[_activeSparks++] = new SparkParticle
            {
                X = 0f, // Wird beim Rendern relativ zum Amboss gesetzt
                Y = 0f,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed - 40f, // Zusätzlicher Aufwärts-Impuls
                Life = 0.3f + (float)Rng.NextDouble() * 0.3f,
                MaxLife = 0.6f,
                Alpha = 255
            };
        }
    }

    // ═══════════════════════════════════════════════
    // RENDER
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Lädt das AI-Splash-Bild direkt aus AvaloniaResource (kein DI, da vor Container-Init).
    /// </summary>
    private SKBitmap? LoadSplashFromResource()
    {
        try
        {
            var uri = new Uri("avares://HandwerkerImperium.Shared/Assets/visuals/splash/splash.webp");
            using var stream = AssetLoader.Open(uri);
            return SKBitmap.Decode(stream);
        }
        catch
        {
            // Asset fehlt → prozeduraler Hintergrund bleibt
            return null;
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeEmbers(w, h);

        _titleGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

        // AI-Splash als Hintergrund (einmalig laden, kein erneuter Versuch bei Fehler)
        if (!_splashLoadAttempted)
        {
            _splashLoadAttempted = true;
            _splashBackground = LoadSplashFromResource();
        }

        if (_splashBackground != null)
            canvas.DrawBitmap(_splashBackground, new SKRect(0, 0, w, h));
        else
            RenderBackground(canvas, bounds, w, h);

        // Zahnräder, Amboss, Hammer, Funken DARÜBER (bleiben immer)
        RenderTitle(canvas, w, h);
        RenderGears(canvas, w, h);
        RenderGearSparks(canvas, w, h);
        RenderAnvil(canvas, w, h);
        RenderHammer(canvas, w, h);
        RenderImpactSparks(canvas, w, h);
        RenderEmbers(canvas, w, h);

        // Fortschrittsbalken bei y ~ 72%
        var barWidth = Math.Min(260f, w * 0.6f);
        DrawProgressBar(canvas, w, h * 0.72f, barWidth, 8f, 4f,
            CraftOrange, GoldColor, BgBottom);

        // Status-Text bei y ~ 77%
        DrawStatusText(canvas, w, h * 0.77f);

        // Version bei y ~ 92%
        DrawVersion(canvas, w, h * 0.92f);
    }

    // ═══════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════

    private void RenderBackground(SKCanvas canvas, SKRect bounds, float w, float h)
    {
        // Vertikaler Gradient
        using var bgShader = SKShader.CreateLinearGradient(
            new SKPoint(w / 2f, 0f),
            new SKPoint(w / 2f, h),
            new[] { BgTop, BgBottom },
            null, SKShaderTileMode.Clamp);
        _bgPaint.Shader = bgShader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;

        // Subtile Vignette (radial, schwarz Alpha 128 am Rand)
        var maxDim = Math.Max(w, h);
        using var vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(w / 2f, h / 2f),
            maxDim * 0.7f,
            new[] { SKColors.Transparent, new SKColor(0x00, 0x00, 0x00, 0x80) },
            null, SKShaderTileMode.Clamp);
        _bgPaint.Shader = vignetteShader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;
    }

    // ═══════════════════════════════════════════════
    // TITEL + UNTERTITEL
    // ═══════════════════════════════════════════════

    private void RenderTitle(SKCanvas canvas, float w, float h)
    {
        var titleY = h * 0.20f;

        // App-Name mit Glow-Puls (Alpha 230-255, ~2Hz Sinus)
        var pulse = 230f + 25f * MathF.Sin(Time * 4f * MathF.PI);
        var alpha = (byte)Math.Clamp(pulse, 230, 255);

        _titleFont.Size = Math.Min(28f, w * 0.065f);
        _titlePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);

        // Leichter Glow hinter dem Text
        _titleGlowPaint.Color = new SKColor(0xFF, 0xA7, 0x26, (byte)(alpha * 0.15f));
        _titleGlowPaint.MaskFilter = _titleGlowFilter;
        DrawCenteredText(canvas, AppName, titleY, _titleFont, _titleGlowPaint, w);
        _titleGlowPaint.MaskFilter = null;

        DrawCenteredText(canvas, AppName, titleY, _titleFont, _titlePaint, w);

        // Untertitel bei y ~ 25%, Gold-Orange, Alpha leicht pulsierend
        var subAlpha = (byte)(180f + 40f * MathF.Sin(Time * 3f * MathF.PI));
        _subtitleFont.Size = Math.Min(14f, w * 0.035f);
        _subtitlePaint.Color = GoldOrange.WithAlpha(subAlpha);
        DrawCenteredText(canvas, "Baue dein Imperium", titleY + _titleFont.Size + 8f, _subtitleFont, _subtitlePaint, w);
    }

    // ═══════════════════════════════════════════════
    // ZAHNRÄDER (zwei gegenläufig)
    // ═══════════════════════════════════════════════

    private void RenderGears(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var cy = h * 0.40f;

        float gearRadius1 = 22f;
        float gearRadius2 = 16f;
        float offset = gearRadius1 + gearRadius2 - 4f;

        // Linkes Zahnrad (10 Zähne, Uhrzeigersinn, 40 Grad/s)
        DrawGear(canvas, cx - offset / 2f, cy, gearRadius1, 10, Time * 40f, CraftOrange);

        // Rechtes Zahnrad (8 Zähne, gegenläufig, korrektes Übersetzungsverhältnis)
        float gearRatio = (float)10 / 8;
        DrawGear(canvas, cx + offset / 2f, cy, gearRadius2, 8, -Time * 40f * gearRatio, GoldOrange);
    }

    private void DrawGear(SKCanvas canvas, float cx, float cy,
        float radius, int teeth, float angleDeg, SKColor color)
    {
        canvas.Save();
        canvas.RotateDegrees(angleDeg, cx, cy);

        _gearPath.Reset();

        float toothDepth = radius * 0.2f;
        float innerRadius = radius - toothDepth;
        float toothAngle = 360f / teeth;
        float halfTooth = toothAngle * 0.3f;

        // Zahnrad-Pfad: Zahnflanken Innen→Außen→Spitze→Außen→Innen
        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * toothAngle;
            float rad1 = (baseAngle - halfTooth) * MathF.PI / 180f;
            float rad2 = (baseAngle + halfTooth) * MathF.PI / 180f;
            float radMid1 = (baseAngle - halfTooth * 0.8f) * MathF.PI / 180f;
            float radMid2 = (baseAngle + halfTooth * 0.8f) * MathF.PI / 180f;

            if (i == 0)
                _gearPath.MoveTo(cx + innerRadius * MathF.Cos(rad1), cy + innerRadius * MathF.Sin(rad1));
            else
                _gearPath.LineTo(cx + innerRadius * MathF.Cos(rad1), cy + innerRadius * MathF.Sin(rad1));

            // Flanke hoch → Spitze → Flanke runter
            _gearPath.LineTo(cx + radius * MathF.Cos(radMid1), cy + radius * MathF.Sin(radMid1));
            _gearPath.LineTo(cx + radius * MathF.Cos(radMid2), cy + radius * MathF.Sin(radMid2));
            _gearPath.LineTo(cx + innerRadius * MathF.Cos(rad2), cy + innerRadius * MathF.Sin(rad2));
        }
        _gearPath.Close();

        // Zahnrad füllen
        _gearPaint.Color = color;
        canvas.DrawPath(_gearPath, _gearPaint);

        // Dunkler Stroke-Rand (70% Helligkeit)
        _gearStrokePaint.Color = new SKColor(
            (byte)(color.Red * 0.7f),
            (byte)(color.Green * 0.7f),
            (byte)(color.Blue * 0.7f));
        canvas.DrawPath(_gearPath, _gearStrokePaint);

        // Nabe (85% Helligkeit)
        float hubRadius = radius * 0.35f;
        _gearPaint.Color = new SKColor(
            (byte)(color.Red * 0.85f),
            (byte)(color.Green * 0.85f),
            (byte)(color.Blue * 0.85f));
        canvas.DrawCircle(cx, cy, hubRadius, _gearPaint);

        // Naben-Loch (Hintergrundfarbe)
        _gearPaint.Color = BgTop;
        canvas.DrawCircle(cx, cy, hubRadius * 0.4f, _gearPaint);

        canvas.Restore();
    }

    // ═══════════════════════════════════════════════
    // FUNKEN UM DIE ZAHNRÄDER
    // ═══════════════════════════════════════════════

    private void RenderGearSparks(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var cy = h * 0.40f;

        for (int i = 0; i < MaxGearSparks; i++)
        {
            float phase = (Time * 1.5f + i * 1.047f) % 2f;
            if (phase > 1f) continue;

            float angle = (i * 60f + Time * 30f) * MathF.PI / 180f;
            float dist = 28f + phase * 18f;
            float sx = cx + MathF.Cos(angle) * dist;
            float sy = cy + MathF.Sin(angle) * dist;

            byte alpha = (byte)((1f - phase) * 200);
            float size = (1f - phase) * 2.5f;

            _gearSparkPaint.Color = GoldenSpark.WithAlpha(alpha);
            canvas.DrawCircle(sx, sy, size, _gearSparkPaint);
        }
    }

    // ═══════════════════════════════════════════════
    // AMBOSS
    // ═══════════════════════════════════════════════

    private void RenderAnvil(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var anvilY = h * 0.40f + 35f; // Unter den Zahnrädern

        // Basis (breites Rechteck unten)
        float baseWidth = 52f;
        float baseHeight = 14f;
        var baseRect = new SKRect(
            cx - baseWidth / 2f, anvilY + 8f,
            cx + baseWidth / 2f, anvilY + 8f + baseHeight);

        _anvilPaint.Color = AnvilBody;
        canvas.DrawRoundRect(baseRect, 2f, 2f, _anvilPaint);
        _anvilStrokePaint.Color = AnvilStroke;
        canvas.DrawRoundRect(baseRect, 2f, 2f, _anvilStrokePaint);

        // Arbeitsfläche (schmaleres Rechteck oben)
        float topWidth = 36f;
        float topHeight = 10f;
        var topRect = new SKRect(
            cx - topWidth / 2f, anvilY,
            cx + topWidth / 2f, anvilY + topHeight);

        _anvilPaint.Color = new SKColor(0x34, 0x34, 0x48);
        canvas.DrawRoundRect(topRect, 2f, 2f, _anvilPaint);
        _anvilStrokePaint.Color = AnvilStroke;
        canvas.DrawRoundRect(topRect, 2f, 2f, _anvilStrokePaint);
    }

    // ═══════════════════════════════════════════════
    // HAMMER (zyklische Animation)
    // ═══════════════════════════════════════════════

    private void RenderHammer(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var anvilTopY = h * 0.40f + 35f; // Amboss-Oberkante

        // Pivot-Punkt: Stiel-Ende (unten am Amboss)
        float pivotX = cx;
        float pivotY = anvilTopY;

        // Hammer-Rotation basierend auf Phase
        float rotation;
        if (_hammerPhase < 0.4f)
        {
            // Phase 0-0.4: Hammer hebt sich (0° → -30°)
            float t = _hammerPhase / 0.4f;
            rotation = -30f * t;
        }
        else if (_hammerPhase < 0.6f)
        {
            // Phase 0.4-0.6: Hammer schlägt runter (-30° → +5°)
            float t = (_hammerPhase - 0.4f) / 0.2f;
            rotation = -30f + 35f * t;
        }
        else
        {
            // Phase 0.6-1.0: Pause bei +5° → 0°
            float t = (_hammerPhase - 0.6f) / 0.4f;
            rotation = 5f * (1f - t);
        }

        canvas.Save();
        canvas.RotateDegrees(rotation, pivotX, pivotY);

        // Stiel (Linie nach oben vom Pivot)
        float stielLength = 30f;
        _hammerPaint.Color = HammerHandle;
        _hammerPaint.Style = SKPaintStyle.Stroke;
        _hammerPaint.StrokeWidth = 3f;
        canvas.DrawLine(pivotX, pivotY, pivotX, pivotY - stielLength, _hammerPaint);
        _hammerPaint.Style = SKPaintStyle.Fill;

        // Kopf (Rechteck am oberen Ende)
        float headWidth = 18f;
        float headHeight = 8f;
        var headRect = new SKRect(
            pivotX - headWidth / 2f, pivotY - stielLength - headHeight / 2f,
            pivotX + headWidth / 2f, pivotY - stielLength + headHeight / 2f);

        _hammerPaint.Color = HammerHead;
        canvas.DrawRoundRect(headRect, 1.5f, 1.5f, _hammerPaint);

        // Glanz-Highlight auf dem Hammer-Kopf
        _hammerPaint.Color = new SKColor(0x90, 0x90, 0xA0, 80);
        canvas.DrawRect(headRect.Left + 2f, headRect.Top + 1f, headWidth - 4f, 2f, _hammerPaint);

        canvas.Restore();
    }

    // ═══════════════════════════════════════════════
    // AUFSCHLAG-FUNKEN
    // ═══════════════════════════════════════════════

    private void RenderImpactSparks(SKCanvas canvas, float w, float h)
    {
        if (_activeSparks == 0) return;

        // Funken-Ursprung: Amboss-Oberfläche
        float originX = w / 2f;
        float originY = h * 0.40f + 35f;

        for (var i = 0; i < _activeSparks; i++)
        {
            ref var s = ref _sparks[i];
            float px = originX + s.X;
            float py = originY + s.Y;

            // Farb-Interpolation: gelb→orange basierend auf Lebensdauer
            float lifeRatio = s.Life / s.MaxLife;
            byte r = (byte)(255f);
            byte g = (byte)(213f * lifeRatio + 88f * (1f - lifeRatio)); // Gelb→Orange
            byte b = (byte)(79f * lifeRatio);

            _sparkPaint.Color = new SKColor(r, g, b, s.Alpha);
            float size = 1.5f + lifeRatio * 1.5f;
            canvas.DrawCircle(px, py, size, _sparkPaint);
        }
    }

    // ═══════════════════════════════════════════════
    // FEUER-GLUT AM UNTEREN RAND
    // ═══════════════════════════════════════════════

    private void RenderEmbers(SKCanvas canvas, float w, float h)
    {
        for (var i = 0; i < MaxEmbers; i++)
        {
            ref var e = ref _embers[i];

            // Glut zurücksetzen falls nötig (echte Y-Werte)
            if (e.Y <= 0f)
            {
                e.X = (float)Rng.NextDouble() * w;
                e.Y = h * (0.85f + (float)Rng.NextDouble() * 0.15f);
            }

            if (e.Alpha <= 0f) continue;

            // Sinus-Drift für organische Bewegung
            float driftX = MathF.Sin(e.Phase) * 8f;

            // Farbe basierend auf Lebensdauer: Rot→Orange→Gelb
            float lifeNorm = Math.Clamp(e.Alpha / 200f, 0f, 1f);
            byte r = (byte)(220f + 35f * lifeNorm);
            byte g = (byte)(60f + 150f * lifeNorm); // Rot(60) → Gelb(210)
            byte b = (byte)(20f * lifeNorm);

            _emberPaint.Color = new SKColor(r, g, b, (byte)Math.Clamp(e.Alpha, 0, 255));
            float radius = 1.5f + lifeNorm * 2f;
            canvas.DrawCircle(e.X + driftX, e.Y, radius, _emberPaint);
        }
    }

    // ═══════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════

    protected override void OnDispose()
    {
        _splashBackground?.Dispose();
        _bgPaint.Dispose();
        _titlePaint.Dispose();
        _titleGlowPaint.Dispose();
        _subtitlePaint.Dispose();
        _gearPaint.Dispose();
        _gearStrokePaint.Dispose();
        _anvilPaint.Dispose();
        _anvilStrokePaint.Dispose();
        _hammerPaint.Dispose();
        _sparkPaint.Dispose();
        _emberPaint.Dispose();
        _gearSparkPaint.Dispose();
        _titleFont.Dispose();
        _subtitleFont.Dispose();
        _gearPath.Dispose();
        _hammerPath.Dispose();
        _titleGlowFilter?.Dispose();
    }
}
