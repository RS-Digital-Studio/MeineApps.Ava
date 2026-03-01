using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Screen-Typ fuer unterschiedliche Hintergruende.
/// </summary>
public enum GameScreenType
{
    Dashboard,
    Buildings,
    Guild,
    Shop,
    Settings,
    Workshop,
    Research,
    Workers
}

/// <summary>
/// Partikel-Daten fuer Ambient-Effekte (Struct fuer GC-freie Performance).
/// </summary>
public struct BackgroundParticle
{
    public float X, Y;
    public float VelocityX, VelocityY;
    public float Alpha;
    public float Size;
    public float Phase;
    public float Life;
    public float MaxLife;
    public bool Active;
}

/// <summary>
/// Lebendige, animierte Hintergruende fuer alle Screens.
/// Jeder Screen hat einen eigenen visuellen Stil mit subtilen Partikeln.
/// Gecachte SKPaint-Objekte fuer GC-freie Performance im Render-Loop.
/// </summary>
public class GameBackgroundRenderer : IDisposable
{
    private bool _disposed;
    private const int MaxParticles = 20;

    // Gecachte Paints
    private readonly SKPaint _gradientPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _silhouettePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridPaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Partikel-Pool
    private readonly BackgroundParticle[] _particles = new BackgroundParticle[MaxParticles];

    // Workshop-Tint fuer Workshop-Screens
    private SKColor _workshopTint = new(0x8B, 0x45, 0x13); // SaddleBrown Default

    // Gecachte Paths (vermeidet Allokation pro Frame)
    private readonly SKPath _wavePath = new();
    private readonly SKPath _silhouettePath = new();

    // Gecachter Vignette-Shader (wird bei Bounds-Aenderung neu erstellt)
    private SKShader? _vignetteShader;
    private float _lastVignetteW, _lastVignetteH;

    /// <summary>
    /// Setzt die Workshop-Tint-Farbe fuer Workshop-Hintergruende.
    /// </summary>
    public void SetWorkshopTint(SKColor tint) => _workshopTint = tint;

    /// <summary>
    /// Hauptmethode: Rendert den animierten Hintergrund fuer den angegebenen Screen-Typ.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, GameScreenType screenType, float time)
    {
        switch (screenType)
        {
            case GameScreenType.Dashboard:
                RenderDashboard(canvas, bounds, time);
                break;
            case GameScreenType.Buildings:
                RenderBuildings(canvas, bounds, time);
                break;
            case GameScreenType.Guild:
                RenderGuild(canvas, bounds, time);
                break;
            case GameScreenType.Shop:
                RenderShop(canvas, bounds, time);
                break;
            case GameScreenType.Settings:
                RenderSettings(canvas, bounds, time);
                break;
            case GameScreenType.Workshop:
                RenderWorkshop(canvas, bounds, time);
                break;
            case GameScreenType.Research:
                RenderResearch(canvas, bounds, time);
                break;
            case GameScreenType.Workers:
                RenderWorkers(canvas, bounds, time);
                break;
        }

        // Vignette auf allen Screens
        RenderVignette(canvas, bounds, 0.35f);
    }

    /// <summary>
    /// Aktualisiert die Ambient-Partikel. Muss pro Frame aufgerufen werden.
    /// </summary>
    public void UpdateParticles(float deltaTime, GameScreenType screenType, SKRect bounds)
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active) continue;

            _particles[i].X += _particles[i].VelocityX * deltaTime;
            _particles[i].Y += _particles[i].VelocityY * deltaTime;
            _particles[i].Life += deltaTime;
            _particles[i].Phase += deltaTime;

            // Lebensdauer abgelaufen oder aus dem Bild
            if (_particles[i].Life >= _particles[i].MaxLife ||
                _particles[i].Y < bounds.Top - 20 ||
                _particles[i].Y > bounds.Bottom + 20 ||
                _particles[i].X < bounds.Left - 20 ||
                _particles[i].X > bounds.Right + 20)
            {
                _particles[i].Active = false;
            }
        }

        // Neue Partikel spawnen (ca. alle 0.3s)
        SpawnParticleForScreen(screenType, bounds);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Screen-spezifische Hintergruende
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dashboard: Warmer Craft-Gradient + schwebende Werkzeug-Silhouetten.
    /// </summary>
    private void RenderDashboard(SKCanvas canvas, SKRect bounds, float time)
    {
        // Warmer Gradient (#2A1810 -> #3D2317 -> #1A0F0A)
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x2A, 0x18, 0x10),
            new SKColor(0x3D, 0x23, 0x17),
            new SKColor(0x1A, 0x0F, 0x0A));

        // Schwebende Werkzeug-Silhouetten (Alpha 5%, langsam driftend)
        _silhouettePaint.Color = new SKColor(0xFF, 0xD7, 0x00, 12); // Gold, sehr subtil

        float w = bounds.Width;
        float h = bounds.Height;

        // 5 Werkzeug-Silhouetten mit Sinus-Drift
        DrawHammerSilhouette(canvas, bounds.Left + w * 0.15f, bounds.Top + h * 0.2f + MathF.Sin(time * 0.4f) * 8f, 28f);
        DrawSawSilhouette(canvas, bounds.Left + w * 0.75f, bounds.Top + h * 0.35f + MathF.Sin(time * 0.35f + 1f) * 10f, 30f);
        DrawWrenchSilhouette(canvas, bounds.Left + w * 0.4f, bounds.Top + h * 0.6f + MathF.Sin(time * 0.5f + 2f) * 6f, 24f);
        DrawHammerSilhouette(canvas, bounds.Left + w * 0.85f, bounds.Top + h * 0.75f + MathF.Sin(time * 0.3f + 3f) * 12f, 22f);
        DrawSawSilhouette(canvas, bounds.Left + w * 0.25f, bounds.Top + h * 0.85f + MathF.Sin(time * 0.45f + 4f) * 7f, 26f);

        // Ambient-Partikel (Holzspaene)
        RenderParticles(canvas, new SKColor(0xD4, 0xA5, 0x74, 40));
    }

    /// <summary>
    /// Buildings: Warme Steinmauer-Atmosphäre mit Ziegel-Raster.
    /// </summary>
    private void RenderBuildings(SKCanvas canvas, SKRect bounds, float time)
    {
        // Warmer dunkler Steinmauer-Gradient
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x1C, 0x14, 0x0E),
            new SKColor(0x2A, 0x1F, 0x14),
            new SKColor(0x15, 0x10, 0x0A));

        // Horizontale Fugenlinien (Ziegel-Optik, 24dp Abstand)
        _gridPaint.Color = new SKColor(0x3D, 0x2B, 0x1C, 25);
        _gridPaint.StrokeWidth = 0.5f;
        float rowHeight = 24f;

        for (float y = bounds.Top; y < bounds.Bottom; y += rowHeight)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _gridPaint);

        // Versetzte vertikale Fugen (Ziegel-Verband)
        _gridPaint.Color = new SKColor(0x3D, 0x2B, 0x1C, 18);
        float brickW = 48f;
        int row = 0;
        for (float y = bounds.Top; y < bounds.Bottom; y += rowHeight)
        {
            float offset = (row % 2) * brickW * 0.5f;
            for (float x = bounds.Left + offset; x < bounds.Right; x += brickW)
                canvas.DrawLine(x, y, x, y + rowHeight, _gridPaint);
            row++;
        }

        // 4 Gebäude-Silhouetten (subtil)
        _silhouettePaint.Color = new SKColor(0xD9, 0x77, 0x06, 10); // CraftPrimary, sehr subtil
        DrawBuildingSilhouette(canvas, bounds.Left + bounds.Width * 0.15f, bounds.Top + bounds.Height * 0.3f, 36f);
        DrawBuildingSilhouette(canvas, bounds.Left + bounds.Width * 0.55f, bounds.Top + bounds.Height * 0.5f, 30f);
        DrawBuildingSilhouette(canvas, bounds.Left + bounds.Width * 0.8f, bounds.Top + bounds.Height * 0.25f, 34f);
        DrawBuildingSilhouette(canvas, bounds.Left + bounds.Width * 0.35f, bounds.Top + bounds.Height * 0.75f, 28f);

        // Partikel (warmer Staub)
        RenderParticles(canvas, new SKColor(0xD4, 0xA5, 0x74, 25));
    }

    /// <summary>
    /// Guild: Dunkles Braun + Fackel-Glow an den Raendern + Wappen-Silhouetten.
    /// </summary>
    private void RenderGuild(SKCanvas canvas, SKRect bounds, float time)
    {
        // Dunkler Braun-Gradient
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x1A, 0x12, 0x0A),
            new SKColor(0x2D, 0x1B, 0x0E),
            new SKColor(0x14, 0x0E, 0x08));

        // Fackel-Glow links und rechts (pulsierend)
        float glowIntensity = 0.5f + 0.2f * MathF.Sin(time * 2.5f);
        DrawTorchGlow(canvas, bounds.Left + 20f, bounds.Top + bounds.Height * 0.3f, glowIntensity, bounds);
        DrawTorchGlow(canvas, bounds.Right - 20f, bounds.Top + bounds.Height * 0.3f, glowIntensity * 0.8f, bounds);

        // 3 Wappen-Silhouetten
        _silhouettePaint.Color = new SKColor(0xCD, 0x7F, 0x32, 10); // Bronze, sehr subtil
        DrawShieldSilhouette(canvas, bounds.Left + bounds.Width * 0.2f, bounds.Top + bounds.Height * 0.5f + MathF.Sin(time * 0.3f) * 5f, 36f);
        DrawShieldSilhouette(canvas, bounds.Left + bounds.Width * 0.5f, bounds.Top + bounds.Height * 0.65f + MathF.Sin(time * 0.35f + 1.5f) * 4f, 30f);
        DrawShieldSilhouette(canvas, bounds.Left + bounds.Width * 0.8f, bounds.Top + bounds.Height * 0.45f + MathF.Sin(time * 0.4f + 3f) * 6f, 32f);

        // Partikel (Fackel-Funken)
        RenderParticles(canvas, new SKColor(0xFF, 0xA5, 0x00, 50));
    }

    /// <summary>
    /// Shop: Gold-Gradient + langsam fallende Muenz-Silhouetten.
    /// </summary>
    private void RenderShop(SKCanvas canvas, SKRect bounds, float time)
    {
        // Dunkler Gold-Gradient
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x1A, 0x15, 0x10),
            new SKColor(0x2A, 0x20, 0x12),
            new SKColor(0x12, 0x0E, 0x08));

        // 10 langsam fallende Muenz-Silhouetten (Alpha 8%)
        _silhouettePaint.Color = new SKColor(0xFF, 0xD7, 0x00, 20);
        for (int i = 0; i < 10; i++)
        {
            float xBase = bounds.Left + bounds.Width * (0.05f + i * 0.1f);
            float speed = 12f + (i % 3) * 4f;
            float yPos = bounds.Top + ((time * speed + i * 80f) % (bounds.Height + 40f)) - 20f;
            float size = 4f + (i % 4) * 2f;

            // Muenze als Kreis mit Linie
            canvas.DrawCircle(xBase, yPos, size, _silhouettePaint);
            canvas.DrawLine(xBase - size * 0.3f, yPos, xBase + size * 0.3f, yPos, _silhouettePaint);
        }

        // Gold-Shimmer-Partikel
        RenderParticles(canvas, new SKColor(0xFF, 0xD7, 0x00, 35));
    }

    /// <summary>
    /// Settings: Werkstatt-Holzwand + Werkzeug-Silhouetten.
    /// </summary>
    private void RenderSettings(SKCanvas canvas, SKRect bounds, float time)
    {
        // Holz-Braun Gradient
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x2E, 0x1A, 0x0E),
            new SKColor(0x3A, 0x24, 0x14),
            new SKColor(0x20, 0x14, 0x0A));

        // Holzmaserung-Linien (7 wellenfoermige Linien)
        _gridPaint.Color = new SKColor(0x5D, 0x40, 0x37, 25);
        _gridPaint.StrokeWidth = 1.5f;
        for (int i = 0; i < 7; i++)
        {
            float yBase = bounds.Top + bounds.Height * (0.1f + i * 0.13f);
            _wavePath.Reset();
            _wavePath.MoveTo(bounds.Left, yBase);
            for (float x = bounds.Left; x < bounds.Right; x += 20f)
            {
                float wave = MathF.Sin(x * 0.02f + i * 0.7f) * 3f;
                _wavePath.LineTo(x, yBase + wave);
            }
            canvas.DrawPath(_wavePath, _gridPaint);
        }

        // 4 Werkzeug-Silhouetten
        _silhouettePaint.Color = new SKColor(0xA0, 0x80, 0x60, 12);
        DrawHammerSilhouette(canvas, bounds.Left + bounds.Width * 0.2f, bounds.Top + bounds.Height * 0.25f, 30f);
        DrawWrenchSilhouette(canvas, bounds.Left + bounds.Width * 0.7f, bounds.Top + bounds.Height * 0.4f, 28f);
        DrawSawSilhouette(canvas, bounds.Left + bounds.Width * 0.45f, bounds.Top + bounds.Height * 0.7f, 26f);
        DrawHammerSilhouette(canvas, bounds.Left + bounds.Width * 0.85f, bounds.Top + bounds.Height * 0.8f, 24f);

        // Partikel (Saegespaene)
        RenderParticles(canvas, new SKColor(0xD4, 0xA5, 0x74, 30));
    }

    /// <summary>
    /// Workshop: Warmer Gradient mit optionalem Workshop-Tint.
    /// </summary>
    private void RenderWorkshop(SKCanvas canvas, SKRect bounds, float time)
    {
        // Basis-Gradient mit Workshop-Tint eingemischt
        byte r = (byte)((0x2A + _workshopTint.Red / 6) / 2);
        byte g = (byte)((0x18 + _workshopTint.Green / 6) / 2);
        byte b = (byte)((0x10 + _workshopTint.Blue / 6) / 2);

        DrawVerticalGradient(canvas, bounds,
            new SKColor(r, g, b),
            new SKColor((byte)(r + 0x12), (byte)(g + 0x0A), (byte)(b + 0x06)),
            new SKColor((byte)(r / 2), (byte)(g / 2), (byte)(b / 2)));

        // Subtile Workshop-Farbflaeche in der Mitte
        _fillPaint.Color = _workshopTint.WithAlpha(8);
        canvas.DrawRect(bounds, _fillPaint);

        // Partikel (typ-spezifisch)
        RenderParticles(canvas, _workshopTint.WithAlpha(25));
    }

    /// <summary>
    /// Research: Warme Werkstatt-Atmosphäre mit Holzmaserung + Zahnrad-Silhouetten.
    /// </summary>
    private void RenderResearch(SKCanvas canvas, SKRect bounds, float time)
    {
        // Warmer Nussholz-Gradient (passend zum ResearchBackgroundRenderer)
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x1C, 0x14, 0x0E),
            new SKColor(0x24, 0x18, 0x0F),
            new SKColor(0x12, 0x0D, 0x08));

        // Holzmaserung-Linien (10 wellenförmige Linien)
        _gridPaint.Color = new SKColor(0x3A, 0x28, 0x18, 20);
        _gridPaint.StrokeWidth = 1f;
        for (int i = 0; i < 10; i++)
        {
            float yBase = bounds.Top + bounds.Height * (0.08f + i * 0.09f);
            _wavePath.Reset();
            _wavePath.MoveTo(bounds.Left, yBase);
            for (float x = bounds.Left; x < bounds.Right; x += 20f)
            {
                float wave = MathF.Sin(x * 0.015f + i * 0.8f) * 4f;
                _wavePath.LineTo(x, yBase + wave);
            }
            canvas.DrawPath(_wavePath, _gridPaint);
        }

        // 3 Zahnrad-Silhouetten (subtil, langsam rotierend)
        _silhouettePaint.Color = new SKColor(0xEA, 0x58, 0x0C, 8); // CraftPrimary, sehr subtil
        DrawGearSilhouette(canvas, bounds.Left + bounds.Width * 0.2f, bounds.Top + bounds.Height * 0.35f, 32f, time * 0.3f);
        DrawGearSilhouette(canvas, bounds.Left + bounds.Width * 0.7f, bounds.Top + bounds.Height * 0.55f, 28f, -time * 0.25f);
        DrawGearSilhouette(canvas, bounds.Left + bounds.Width * 0.45f, bounds.Top + bounds.Height * 0.8f, 24f, time * 0.35f);

        // Partikel (warmer aufsteigender Dampf)
        RenderParticles(canvas, new SKColor(0xD4, 0xA5, 0x74, 25));
    }

    /// <summary>
    /// Workers: Holzboden-Textur + Spind-Silhouetten.
    /// </summary>
    private void RenderWorkers(SKCanvas canvas, SKRect bounds, float time)
    {
        // Holzboden-Gradient
        DrawVerticalGradient(canvas, bounds,
            new SKColor(0x3E, 0x27, 0x16),
            new SKColor(0x4A, 0x30, 0x1C),
            new SKColor(0x2A, 0x1A, 0x0E));

        // Horizontale Planken-Linien (alle 30dp)
        _gridPaint.Color = new SKColor(0x20, 0x14, 0x0A, 40);
        _gridPaint.StrokeWidth = 1f;
        for (float y = bounds.Top + 30f; y < bounds.Bottom; y += 30f)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _gridPaint);
        }

        // Vertikale Planken-Fugen (versetzt pro Reihe)
        _gridPaint.Color = new SKColor(0x20, 0x14, 0x0A, 25);
        _gridPaint.StrokeWidth = 0.5f;
        float plankW = 60f;
        int row = 0;
        for (float y = bounds.Top; y < bounds.Bottom; y += 30f)
        {
            float offset = (row % 2) * plankW * 0.5f;
            for (float x = bounds.Left + offset; x < bounds.Right; x += plankW)
            {
                canvas.DrawLine(x, y, x, y + 30f, _gridPaint);
            }
            row++;
        }

        // 3 Spind-Silhouetten
        _silhouettePaint.Color = new SKColor(0x80, 0x80, 0x90, 10);
        DrawLockerSilhouette(canvas, bounds.Left + bounds.Width * 0.15f, bounds.Top + bounds.Height * 0.3f, 40f);
        DrawLockerSilhouette(canvas, bounds.Left + bounds.Width * 0.5f, bounds.Top + bounds.Height * 0.4f, 36f);
        DrawLockerSilhouette(canvas, bounds.Left + bounds.Width * 0.82f, bounds.Top + bounds.Height * 0.35f, 38f);

        // Partikel (Staub-Motes)
        RenderParticles(canvas, new SKColor(0xC0, 0xB0, 0xA0, 25));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Vignette-Overlay
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen Vignette-Effekt (dunkle Ecken, helle Mitte) fuer Tiefe.
    /// </summary>
    private void RenderVignette(SKCanvas canvas, SKRect bounds, float intensity)
    {
        if (intensity < 0.01f) return;

        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur neu erstellen wenn sich die Groesse geaendert hat
        if (_vignetteShader == null || MathF.Abs(w - _lastVignetteW) > 1f || MathF.Abs(h - _lastVignetteH) > 1f)
        {
            _vignetteShader?.Dispose();

            float centerX = bounds.MidX;
            float centerY = bounds.MidY;
            float radius = MathF.Max(w, h) * 0.75f;

            byte alpha = (byte)(160 * intensity);
            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(centerX, centerY),
                radius,
                new[] { SKColors.Transparent, new SKColor(0, 0, 0, alpha) },
                new[] { 0.4f, 1.0f },
                SKShaderTileMode.Clamp);

            _lastVignetteW = w;
            _lastVignetteH = h;
        }

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel-Rendering
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet alle aktiven Partikel mit der angegebenen Farbe.
    /// Alpha wird per Lebensdauer moduliert (fade-in/fade-out).
    /// </summary>
    private void RenderParticles(SKCanvas canvas, SKColor baseColor)
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active) continue;

            ref var p = ref _particles[i];
            float lifeRatio = p.Life / p.MaxLife;

            // Fade-In (0-20%) -> Voll (20-70%) -> Fade-Out (70-100%)
            float alpha;
            if (lifeRatio < 0.2f)
                alpha = lifeRatio / 0.2f;
            else if (lifeRatio < 0.7f)
                alpha = 1f;
            else
                alpha = 1f - (lifeRatio - 0.7f) / 0.3f;

            byte finalAlpha = (byte)(baseColor.Alpha * alpha * p.Alpha);
            if (finalAlpha < 2) continue;

            _particlePaint.Color = baseColor.WithAlpha(finalAlpha);
            canvas.DrawCircle(p.X, p.Y, p.Size, _particlePaint);
        }
    }

    /// <summary>
    /// Spawnt einen neuen Partikel passend zum Screen-Typ.
    /// Begrenzt auf ~3 pro Sekunde (jeder 6. Frame bei 20fps).
    /// </summary>
    private void SpawnParticleForScreen(GameScreenType screenType, SKRect bounds)
    {
        // Nur spawnen wenn ein freier Slot da ist
        int freeSlot = -1;
        int activeCount = 0;
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active)
            {
                if (freeSlot < 0) freeSlot = i;
            }
            else
            {
                activeCount++;
            }
        }

        if (freeSlot < 0) return;

        // Max aktive Partikel pro Typ
        int maxActive = screenType switch
        {
            GameScreenType.Guild => 8,
            GameScreenType.Shop => 10,
            _ => 6
        };

        if (activeCount >= maxActive) return;

        // Spawn-Wahrscheinlichkeit pro Aufruf (20fps, ca. 3 Partikel/s)
        if (Random.Shared.NextSingle() > 0.15f) return;

        ref var p = ref _particles[freeSlot];
        p.Active = true;
        p.Alpha = 0.6f + Random.Shared.NextSingle() * 0.4f;
        p.Phase = Random.Shared.NextSingle() * MathF.Tau;
        p.Size = 1.5f + Random.Shared.NextSingle() * 2.5f;

        switch (screenType)
        {
            case GameScreenType.Dashboard:
                // Holzspaene: Von oben nach unten driftend
                p.X = bounds.Left + Random.Shared.NextSingle() * bounds.Width;
                p.Y = bounds.Top - 5f;
                p.VelocityX = -5f + Random.Shared.NextSingle() * 10f;
                p.VelocityY = 8f + Random.Shared.NextSingle() * 12f;
                p.MaxLife = 3f + Random.Shared.NextSingle() * 2f;
                break;

            case GameScreenType.Buildings:
                // Warmer Staub: Langsam schwebend
                p.X = bounds.Left + Random.Shared.NextSingle() * bounds.Width;
                p.Y = bounds.Top + Random.Shared.NextSingle() * bounds.Height;
                p.VelocityX = -3f + Random.Shared.NextSingle() * 6f;
                p.VelocityY = -2f + Random.Shared.NextSingle() * 4f;
                p.MaxLife = 4f + Random.Shared.NextSingle() * 3f;
                p.Size = 1f + Random.Shared.NextSingle() * 1.5f;
                break;

            case GameScreenType.Guild:
                // Fackel-Funken: Von Seiten aufsteigend
                bool fromLeft = Random.Shared.NextSingle() < 0.5f;
                p.X = fromLeft ? bounds.Left + 20f : bounds.Right - 20f;
                p.Y = bounds.Top + bounds.Height * 0.3f + Random.Shared.NextSingle() * 20f;
                p.VelocityX = fromLeft ? 5f + Random.Shared.NextSingle() * 15f : -5f - Random.Shared.NextSingle() * 15f;
                p.VelocityY = -15f - Random.Shared.NextSingle() * 25f;
                p.MaxLife = 0.8f + Random.Shared.NextSingle() * 1.2f;
                p.Size = 1f + Random.Shared.NextSingle() * 2f;
                break;

            case GameScreenType.Shop:
                // Gold-Shimmer: Langsam nach oben schwebend
                p.X = bounds.Left + Random.Shared.NextSingle() * bounds.Width;
                p.Y = bounds.Bottom + 5f;
                p.VelocityX = -2f + Random.Shared.NextSingle() * 4f;
                p.VelocityY = -6f - Random.Shared.NextSingle() * 8f;
                p.MaxLife = 4f + Random.Shared.NextSingle() * 3f;
                p.Size = 1f + Random.Shared.NextSingle() * 2f;
                break;

            case GameScreenType.Research:
                // Warmer Dampf: Von Mitte-unten aufsteigend
                p.X = bounds.MidX + (-30f + Random.Shared.NextSingle() * 60f);
                p.Y = bounds.Bottom - 20f;
                p.VelocityX = -3f + Random.Shared.NextSingle() * 6f;
                p.VelocityY = -10f - Random.Shared.NextSingle() * 15f;
                p.MaxLife = 3f + Random.Shared.NextSingle() * 2f;
                p.Size = 2f + Random.Shared.NextSingle() * 3f;
                break;

            default:
                // Standard: Staub-Motes
                p.X = bounds.Left + Random.Shared.NextSingle() * bounds.Width;
                p.Y = bounds.Top + Random.Shared.NextSingle() * bounds.Height;
                p.VelocityX = -2f + Random.Shared.NextSingle() * 4f;
                p.VelocityY = -1f + Random.Shared.NextSingle() * 2f;
                p.MaxLife = 5f + Random.Shared.NextSingle() * 3f;
                p.Size = 1f + Random.Shared.NextSingle() * 1.5f;
                break;
        }

        p.Life = 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Gradient-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen vertikalen 2-Farben-Gradient.
    /// </summary>
    private void DrawVerticalGradient(SKCanvas canvas, SKRect bounds, SKColor top, SKColor bottom)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.MidX, bounds.Top),
            new SKPoint(bounds.MidX, bounds.Bottom),
            new[] { top, bottom },
            null,
            SKShaderTileMode.Clamp);

        _gradientPaint.Shader = shader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    /// <summary>
    /// Zeichnet einen vertikalen 3-Farben-Gradient (oben -> mitte -> unten).
    /// </summary>
    private void DrawVerticalGradient(SKCanvas canvas, SKRect bounds, SKColor top, SKColor mid, SKColor bottom)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.MidX, bounds.Top),
            new SKPoint(bounds.MidX, bounds.Bottom),
            new[] { top, mid, bottom },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        _gradientPaint.Shader = shader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Silhouetten-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine Hammer-Silhouette (T-Form).
    /// </summary>
    private void DrawHammerSilhouette(SKCanvas canvas, float x, float y, float size)
    {
        // Stiel (vertikal)
        float stielW = size * 0.12f;
        float stielH = size * 0.7f;
        canvas.DrawRect(x - stielW / 2, y, stielW, stielH, _silhouettePaint);

        // Kopf (horizontal oben)
        float kopfW = size * 0.6f;
        float kopfH = size * 0.25f;
        canvas.DrawRect(x - kopfW / 2, y - kopfH * 0.3f, kopfW, kopfH, _silhouettePaint);
    }

    /// <summary>
    /// Zeichnet eine Saege-Silhouette (Rechteck mit Zaehnen).
    /// </summary>
    private void DrawSawSilhouette(SKCanvas canvas, float x, float y, float size)
    {
        // Blatt
        float blattW = size * 0.8f;
        float blattH = size * 0.3f;
        canvas.DrawRect(x - blattW / 2, y - blattH / 2, blattW, blattH, _silhouettePaint);

        // Griff (rechts)
        float griffR = size * 0.15f;
        canvas.DrawCircle(x + blattW / 2 + griffR * 0.5f, y, griffR, _silhouettePaint);
    }

    /// <summary>
    /// Zeichnet eine Schraubenschluessel-Silhouette.
    /// </summary>
    private void DrawWrenchSilhouette(SKCanvas canvas, float x, float y, float size)
    {
        // Schaft
        float schaftW = size * 0.12f;
        float schaftH = size * 0.65f;
        canvas.DrawRect(x - schaftW / 2, y - schaftH / 2, schaftW, schaftH, _silhouettePaint);

        // Mauloefffnung oben (zwei Dreiecke)
        float maulW = size * 0.3f;
        float maulH = size * 0.2f;
        canvas.DrawRect(x - maulW / 2, y - schaftH / 2 - maulH, maulW, maulH, _silhouettePaint);

        // Rundung unten
        canvas.DrawCircle(x, y + schaftH / 2, schaftW * 0.8f, _silhouettePaint);
    }

    /// <summary>
    /// Zeichnet eine Wappen-/Schild-Silhouette.
    /// </summary>
    private void DrawShieldSilhouette(SKCanvas canvas, float x, float y, float size)
    {
        _silhouettePath.Reset();
        float halfW = size * 0.4f;
        float topH = size * 0.3f;
        float botH = size * 0.5f;

        // Oben abgerundet, unten spitz
        _silhouettePath.MoveTo(x - halfW, y - topH);
        _silhouettePath.LineTo(x + halfW, y - topH);
        _silhouettePath.LineTo(x + halfW, y + botH * 0.3f);
        _silhouettePath.LineTo(x, y + botH);
        _silhouettePath.LineTo(x - halfW, y + botH * 0.3f);
        _silhouettePath.Close();

        canvas.DrawPath(_silhouettePath, _silhouettePaint);
    }

    /// <summary>
    /// Zeichnet eine Spind-Silhouette (hohes Rechteck mit Tuer-Teilung).
    /// </summary>
    private void DrawLockerSilhouette(SKCanvas canvas, float x, float y, float size)
    {
        float w = size * 0.4f;
        float h = size;

        // Korpus
        canvas.DrawRoundRect(x - w / 2, y, w, h, 2f, 2f, _silhouettePaint);

        // Tuer-Teilung (vertikale Linie in der Mitte)
        _gridPaint.Color = _silhouettePaint.Color;
        _gridPaint.StrokeWidth = 0.5f;
        canvas.DrawLine(x, y + 3f, x, y + h - 3f, _gridPaint);

        // Griffe (2 kleine Kreise)
        canvas.DrawCircle(x - 2f, y + h * 0.4f, 1.5f, _silhouettePaint);
        canvas.DrawCircle(x + 2f, y + h * 0.4f, 1.5f, _silhouettePaint);
    }

    /// <summary>
    /// Zeichnet eine Gebäude-Silhouette (Haus mit Dach + Schornstein).
    /// </summary>
    private void DrawBuildingSilhouette(SKCanvas canvas, float x, float y, float size)
    {
        _silhouettePath.Reset();
        float halfW = size * 0.4f;
        float bodyH = size * 0.5f;
        float roofH = size * 0.35f;

        // Dach (Dreieck)
        _silhouettePath.MoveTo(x - halfW - 4f, y);
        _silhouettePath.LineTo(x, y - roofH);
        _silhouettePath.LineTo(x + halfW + 4f, y);

        // Körper (Rechteck)
        _silhouettePath.LineTo(x + halfW, y);
        _silhouettePath.LineTo(x + halfW, y + bodyH);
        _silhouettePath.LineTo(x - halfW, y + bodyH);
        _silhouettePath.LineTo(x - halfW, y);
        _silhouettePath.Close();

        canvas.DrawPath(_silhouettePath, _silhouettePaint);

        // Schornstein
        canvas.DrawRect(x + halfW * 0.3f, y - roofH * 0.8f, size * 0.1f, roofH * 0.5f, _silhouettePaint);

        // Tür
        canvas.DrawRect(x - size * 0.07f, y + bodyH * 0.4f, size * 0.14f, bodyH * 0.6f, _silhouettePaint);
    }

    /// <summary>
    /// Zeichnet eine rotierende Zahnrad-Silhouette.
    /// </summary>
    private void DrawGearSilhouette(SKCanvas canvas, float x, float y, float size, float rotation)
    {
        int teeth = 8;
        float innerR = size * 0.35f;
        float outerR = size * 0.5f;

        _silhouettePath.Reset();
        for (int i = 0; i < teeth; i++)
        {
            float angle1 = rotation + i * MathF.Tau / teeth;
            float angle2 = rotation + (i + 0.35f) * MathF.Tau / teeth;
            float angle3 = rotation + (i + 0.5f) * MathF.Tau / teeth;
            float angle4 = rotation + (i + 0.85f) * MathF.Tau / teeth;

            float ix1 = x + innerR * MathF.Cos(angle1);
            float iy1 = y + innerR * MathF.Sin(angle1);
            float ox1 = x + outerR * MathF.Cos(angle2);
            float oy1 = y + outerR * MathF.Sin(angle2);
            float ox2 = x + outerR * MathF.Cos(angle3);
            float oy2 = y + outerR * MathF.Sin(angle3);
            float ix2 = x + innerR * MathF.Cos(angle4);
            float iy2 = y + innerR * MathF.Sin(angle4);

            if (i == 0)
                _silhouettePath.MoveTo(ix1, iy1);
            else
                _silhouettePath.LineTo(ix1, iy1);

            _silhouettePath.LineTo(ox1, oy1);
            _silhouettePath.LineTo(ox2, oy2);
            _silhouettePath.LineTo(ix2, iy2);
        }
        _silhouettePath.Close();

        canvas.DrawPath(_silhouettePath, _silhouettePaint);

        // Zentrales Loch
        canvas.DrawCircle(x, y, innerR * 0.4f, _silhouettePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Fackel-Glow
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen warmen Fackel-Lichtschein an der angegebenen Position.
    /// </summary>
    private void DrawTorchGlow(SKCanvas canvas, float x, float y, float intensity, SKRect bounds)
    {
        float radius = bounds.Height * 0.35f * intensity;
        byte alpha = (byte)(40 * intensity);

        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(x, y),
            radius,
            new[] { new SKColor(0xFF, 0x8C, 0x00, alpha), SKColors.Transparent },
            null,
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = shader;
        canvas.DrawRect(
            x - radius, y - radius,
            radius * 2, radius * 2,
            _fillPaint);
        _fillPaint.Shader = null;
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gradientPaint?.Dispose();
        _silhouettePaint?.Dispose();
        _particlePaint?.Dispose();
        _vignettePaint?.Dispose();
        _gridPaint?.Dispose();
        _fillPaint?.Dispose();
        _vignetteShader?.Dispose();
    }
}
