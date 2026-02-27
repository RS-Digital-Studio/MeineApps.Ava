using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Ergebnis-Typ für City-Tap: Kein Treffer, Workshop oder Gebäude.
/// </summary>
public enum CityTapTarget
{
    None,
    Workshop,
    Building
}

/// <summary>
/// Ergebnis eines HitTest auf die City-Skyline mit Ziel-Typ und Index.
/// </summary>
public struct CityTapResult
{
    public CityTapTarget Target;
    public int Index;
    public WorkshopType? WorkshopType;
    public BuildingType? BuildingType;
    /// <summary>Gebäude-Bounds für Highlight-Glow bei Tap.</summary>
    public float HitX, HitY, HitW, HitH;
}

/// <summary>
/// Rendert die City-Skyline als 2.5D-isometrische Szene.
/// 5-Layer Parallax-Hintergrund, isometrische Gebäude, animierte Mini-Figuren,
/// Schornstein-Rauch, Lieferwagen, Tag/Nacht-Palette, Workshop-Partikel.
/// </summary>
public class CityRenderer
{
    // Fortlaufende Animationszeit
    private float _time;

    // Wetter-System (saisonale Effekte über der City-Szene)
    private readonly CityWeatherSystem _weatherSystem = new();
    private bool _weatherInitialized;

    // Tap-Label (Workshop-Name bei Tap)
    private string? _tapLabel;
    private float _tapLabelX, _tapLabelY;
    private float _tapLabelTimer;
    private const float TapLabelDuration = 1.5f;

    // Tap-Highlight (Glow um getipptes Gebäude)
    private float _tapHighlightX, _tapHighlightY, _tapHighlightW, _tapHighlightH;
    private float _tapHighlightTimer;
    private SKColor _tapHighlightColor;

    // Lebhaftigkeits-Multiplikator (gecacht, wird beim Rendern aktualisiert)
    private float _cachedVibrancy = 1.0f;

    // Gecachte Paints (werden pro Frame wiederverwendet)
    private readonly SKPaint _skyPaint = new() { IsAntialias = true };
    private readonly SKPaint _groundPaint = new() { IsAntialias = true };
    private readonly SKPaint _streetPaint = new() { IsAntialias = true };
    private readonly SKPaint _stripePaint = new() { IsAntialias = true };
    private readonly SKPaint _cloudPaint = new() { IsAntialias = true };
    private readonly SKPaint _smokePaint = new() { IsAntialias = true };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private readonly SKPaint _vanPaint = new() { IsAntialias = true };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true, Color = SKColors.White };
    private readonly SKFont _labelFont = new(SKTypeface.Default, 8);
    private readonly SKPaint _hillPaint = new() { IsAntialias = true };
    private readonly SKPaint _decoStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _starPaint = new() { IsAntialias = true };

    // Gecachter PathEffect für gestricheltes Outline (statt pro Frame neu erstellen)
    private static readonly SKPathEffect _dashedEffect = SKPathEffect.CreateDash([4, 4], 0);

    // Parallax-Scroll-Offset (wird extern gesetzt, z.B. vom ScrollViewer)
    public float ScrollOffset { get; set; }

    /// <summary>
    /// Rendert die komplette City-Ansicht.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, GameState state, List<Building> buildings, float deltaTime = 0.016f)
    {
        _time += deltaTime;
        float nightDim = GetNightDimFactor();

        // Wetter-System initialisieren (einmalig) + updaten
        if (!_weatherInitialized)
        {
            _weatherSystem.SetWeatherByMonth();
            _weatherInitialized = true;
        }
        _weatherSystem.Update(deltaTime);

        // Layer 1: Himmel (mit Tag/Nacht-Gradient)
        DrawSkyLayer(canvas, bounds, nightDim);

        // Layer 2: Sterne nachts
        if (nightDim < 0.8f)
            DrawStars(canvas, bounds, nightDim);

        // Layer 3: Ferne Hügel (0.1x Parallax)
        DrawDistantHills(canvas, bounds, nightDim);

        // Layer 4: Wolken (0.3x Parallax)
        DrawClouds(canvas, bounds, nightDim);

        // Layer 5: Nahe Hügel mit Bäumen (0.2x Parallax)
        DrawNearHills(canvas, bounds, nightDim);

        // Welt-Progression aktualisieren
        int worldTier = GetWorldTier(state.PlayerLevel);
        _cachedVibrancy = CityProgressionHelper.GetVibrancyMultiplier(worldTier);

        // Boden + Straße (mit Progression)
        float groundY = bounds.Top + bounds.Height * 0.52f;
        DrawGround(canvas, bounds, groundY, nightDim, state);

        // Wolken-Schatten auf dem Boden (dunkle Ovale die mit Wolken wandern)
        DrawCloudShadows(canvas, bounds, groundY, nightDim);

        float streetY = groundY + 6;
        CityProgressionHelper.DrawProgressiveStreet(canvas, bounds, streetY, 12, worldTier, nightDim, _time);

        // Workshops (oberhalb der Straße) - isometrisch
        float workshopRowBottom = streetY - 2;
        DrawWorkshopRow(canvas, bounds, state, workshopRowBottom, nightDim);

        // Lieferwagen auf der Straße
        DrawDeliveryVan(canvas, bounds, streetY, nightDim);

        // Straßen-Dekorationen (Bäume, Laternen, Bänke - zwischen Workshops und Straße)
        CityProgressionHelper.DrawStreetDecorations(canvas, bounds, streetY, worldTier, nightDim, _time);

        // Support-Gebäude (unterhalb der Straße)
        float buildingRowTop = streetY + 14;
        DrawBuildingRow(canvas, bounds, buildings, buildingRowTop, nightDim);

        // Wetter-Overlay (Regen, Schnee, Blätter, Sonnenstrahlen)
        _weatherSystem.Render(canvas, bounds);

        // Boden-Wetter-Effekte (Pfützen, Schnee-Haufen, liegende Blätter)
        DrawGroundWeatherEffects(canvas, bounds, streetY, nightDim);

        // Workshop-Partikel (Rauch, Funken etc. - über den Gebäuden)
        DrawWorkshopParticles(canvas, bounds, state, workshopRowBottom, nightDim);

        // Tap-Label + Highlight-Glow (über allem)
        DrawTapLabel(canvas, deltaTime);
    }

    // ═════════════════════════════════════════════════════════════════
    // PARALLAX-HINTERGRUND (5 Layer)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Himmel mit Tag/Nacht-Gradient + Sonnenauf-/Untergang (Layer 1).
    /// </summary>
    private void DrawSkyLayer(SKCanvas canvas, SKRect bounds, float nightDim)
    {
        int hour = DateTime.Now.Hour;
        int minute = DateTime.Now.Minute;
        float hourF = hour + minute / 60f;

        // Tages-Gradient: Lebhaftere Farben bei höheren Leveln
        var topColor = CityBuildingShapes.ApplyDim(new SKColor(0x5B, 0xA3, 0xD9), nightDim);
        var bottomColor = CityBuildingShapes.ApplyDim(new SKColor(0xB0, 0xD4, 0xF1), nightDim);
        topColor = CityProgressionHelper.ApplyVibrancy(topColor, _cachedVibrancy);
        bottomColor = CityProgressionHelper.ApplyVibrancy(bottomColor, _cachedVibrancy);

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.55f),
            [topColor, bottomColor],
            null, SKShaderTileMode.Clamp);
        _skyPaint.Shader = shader;
        canvas.DrawRect(bounds, _skyPaint);
        _skyPaint.Shader = null;

        // Sonnenaufgang (6-8h) / Sonnenuntergang (18-20h) Overlay
        float sunsetAlpha = 0;
        if (hourF >= 6f && hourF < 8f)
            sunsetAlpha = 1f - Math.Abs(hourF - 7f); // Peak bei 7h
        else if (hourF >= 18f && hourF < 20f)
            sunsetAlpha = 1f - Math.Abs(hourF - 19f); // Peak bei 19h

        if (sunsetAlpha > 0.05f)
        {
            sunsetAlpha = Math.Clamp(sunsetAlpha, 0, 1);
            byte alpha = (byte)(sunsetAlpha * 50);
            using var sunsetShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.2f),
                new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.55f),
                [
                    new SKColor(0xFF, 0x6B, 0x6B, (byte)(alpha * 0.6f)),
                    new SKColor(0xFF, 0x9E, 0x40, alpha),
                    new SKColor(0xFF, 0xD7, 0x00, (byte)(alpha * 0.4f))
                ],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp);
            _skyPaint.Shader = sunsetShader;
            canvas.DrawRect(bounds, _skyPaint);
            _skyPaint.Shader = null;
        }
    }

    /// <summary>
    /// Sterne am Nachthimmel (nur sichtbar bei nightDim < 0.8).
    /// </summary>
    private void DrawStars(SKCanvas canvas, SKRect bounds, float nightDim)
    {
        float starAlpha = (0.8f - nightDim) / 0.2f; // 0→1 bei 0.6→0.8 nightDim
        starAlpha = Math.Clamp(starAlpha, 0f, 1f);
        byte a = (byte)(starAlpha * 200);

        // 15 deterministische Sterne
        for (int i = 0; i < 15; i++)
        {
            uint hash = (uint)(i * 7919 + 3571);
            float sx = bounds.Left + (hash % 1000) / 1000f * bounds.Width;
            hash = hash * 1664525 + 1013904223;
            float sy = bounds.Top + (hash % 1000) / 1000f * bounds.Height * 0.4f;
            hash = hash * 1664525 + 1013904223;
            float size = 0.5f + (hash % 100) / 100f * 1.5f;

            // Leichtes Funkeln
            float twinkle = MathF.Sin(_time * (1f + i * 0.3f) + i * 2.1f);
            byte finalA = (byte)(a * (0.5f + 0.5f * twinkle));

            _starPaint.Color = new SKColor(0xFF, 0xFF, 0xE0, finalA);
            canvas.DrawCircle(sx, sy, size, _starPaint);
        }
    }

    /// <summary>
    /// Ferne Hügel-Silhouette (Layer 2, 0.1x Parallax).
    /// </summary>
    private void DrawDistantHills(SKCanvas canvas, SKRect bounds, float nightDim)
    {
        float parallaxOffset = ScrollOffset * 0.1f;
        float hillBaseY = bounds.Top + bounds.Height * 0.42f;
        var hillColor = CityBuildingShapes.ApplyDim(new SKColor(0x6B, 0x8E, 0x6B), nightDim);
        _hillPaint.Color = hillColor;

        using var path = new SKPath();
        path.MoveTo(bounds.Left, hillBaseY + 20);

        // Sanfte Hügelkurve
        for (float px = bounds.Left; px <= bounds.Right; px += 4)
        {
            float normalizedX = (px - bounds.Left + parallaxOffset) / bounds.Width;
            float hill = MathF.Sin(normalizedX * 2.5f) * 12
                       + MathF.Sin(normalizedX * 5f + 1.3f) * 6
                       + MathF.Sin(normalizedX * 8f + 2.7f) * 3;
            path.LineTo(px, hillBaseY + hill);
        }

        path.LineTo(bounds.Right, hillBaseY + 20);
        path.Close();
        canvas.DrawPath(path, _hillPaint);
    }

    /// <summary>
    /// Nahe Hügel mit Bäumen (Layer 3, 0.2x Parallax).
    /// </summary>
    private void DrawNearHills(SKCanvas canvas, SKRect bounds, float nightDim)
    {
        float parallaxOffset = ScrollOffset * 0.2f;
        float hillBaseY = bounds.Top + bounds.Height * 0.48f;
        var hillColor = CityBuildingShapes.ApplyDim(new SKColor(0x4A, 0x7C, 0x4A), nightDim);
        _hillPaint.Color = hillColor;

        using var path = new SKPath();
        path.MoveTo(bounds.Left, hillBaseY + 15);

        for (float px = bounds.Left; px <= bounds.Right; px += 3)
        {
            float normalizedX = (px - bounds.Left + parallaxOffset) / bounds.Width;
            float hill = MathF.Sin(normalizedX * 3f + 0.5f) * 8
                       + MathF.Sin(normalizedX * 7f + 2f) * 4;
            path.LineTo(px, hillBaseY + hill);
        }

        path.LineTo(bounds.Right, hillBaseY + 15);
        path.Close();
        canvas.DrawPath(path, _hillPaint);

        // Bäume auf den Hügeln (kleine Dreiecke)
        var treeColor = CityBuildingShapes.ApplyDim(new SKColor(0x2E, 0x6B, 0x2E), nightDim);
        var trunkColor = CityBuildingShapes.ApplyDim(new SKColor(0x5D, 0x40, 0x37), nightDim);

        for (int i = 0; i < 8; i++)
        {
            uint hash = (uint)(i * 4261 + 1597);
            float tx = bounds.Left + (hash % 1000) / 1000f * bounds.Width;
            float normalizedX = (tx - bounds.Left + parallaxOffset) / bounds.Width;
            float hillH = MathF.Sin(normalizedX * 3f + 0.5f) * 8
                        + MathF.Sin(normalizedX * 7f + 2f) * 4;
            float ty = hillBaseY + hillH;

            hash = hash * 1664525 + 1013904223;
            float treeH = 6f + (hash % 100) / 100f * 6f;

            // Stamm
            _hillPaint.Color = trunkColor;
            canvas.DrawRect(tx - 1, ty - treeH * 0.3f, 2, treeH * 0.3f, _hillPaint);

            // Krone (Dreieck)
            _hillPaint.Color = treeColor;
            using var treePath = new SKPath();
            treePath.MoveTo(tx, ty - treeH);
            treePath.LineTo(tx - treeH * 0.4f, ty - treeH * 0.25f);
            treePath.LineTo(tx + treeH * 0.4f, ty - treeH * 0.25f);
            treePath.Close();
            canvas.DrawPath(treePath, _hillPaint);
        }
    }

    /// <summary>
    /// 4 Wolken mit verschiedenen Geschwindigkeiten (Layer 4, 0.3x Parallax).
    /// </summary>
    private void DrawClouds(SKCanvas canvas, SKRect bounds, float nightDim)
    {
        var color = CityBuildingShapes.ApplyDim(new SKColor(0xFF, 0xFF, 0xFF, 0xA0), nightDim);
        _cloudPaint.Color = color;

        float[] speeds = { 8f, 12f, 6f, 15f };
        float[] heights = { 0.06f, 0.14f, 0.20f, 0.03f };
        float[] widths = { 40f, 30f, 50f, 25f };
        float[] offsets = { 0f, 0.25f, 0.55f, 0.8f };
        float parallaxOffset = ScrollOffset * 0.3f;

        for (int i = 0; i < 4; i++)
        {
            float cloudX = ((offsets[i] * bounds.Width + _time * speeds[i] + parallaxOffset) % (bounds.Width + widths[i] * 2)) - widths[i];
            float cloudY = bounds.Top + bounds.Height * heights[i];
            float w = widths[i];

            // Wolke aus abgerundeten Formen (weicher als Pixel-Art)
            canvas.DrawRoundRect(cloudX + w * 0.15f, cloudY - 2, w * 0.7f, 6, 3, 3, _cloudPaint);
            canvas.DrawRoundRect(cloudX, cloudY + 1, w, 5, 2.5f, 2.5f, _cloudPaint);
            canvas.DrawRoundRect(cloudX + w * 0.1f, cloudY + 4, w * 0.8f, 4, 2, 2, _cloudPaint);
        }
    }

    /// <summary>
    /// Wolken-Schatten auf dem Boden (dunkle Ovale die mit den Wolken wandern).
    /// </summary>
    private void DrawCloudShadows(SKCanvas canvas, SKRect bounds, float groundY, float nightDim)
    {
        // Nur tagsüber sichtbar (Schatten brauchen Sonne)
        if (nightDim < 0.6f) return;

        float[] speeds = { 8f, 12f, 6f, 15f };
        float[] widths = { 40f, 30f, 50f, 25f };
        float[] offsets = { 0f, 0.25f, 0.55f, 0.8f };
        float parallaxOffset = ScrollOffset * 0.3f;

        byte shadowAlpha = (byte)(15 * nightDim);

        for (int i = 0; i < 4; i++)
        {
            float cloudX = ((offsets[i] * bounds.Width + _time * speeds[i] + parallaxOffset)
                % (bounds.Width + widths[i] * 2)) - widths[i];
            float w = widths[i];

            // Schatten auf dem Boden (leicht versetzt, breiter, flacher)
            _cloudPaint.Color = new SKColor(0x00, 0x00, 0x00, shadowAlpha);
            canvas.DrawOval(cloudX + w * 0.5f, groundY + 4, w * 0.6f, 3, _cloudPaint);
        }

        // Wolkenfarbe wiederherstellen (wird danach in DrawClouds benutzt)
        _cloudPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xFF, 0xFF, 0xFF, 0xA0), nightDim);
    }

    // ═════════════════════════════════════════════════════════════════
    // BODEN + STRASSE
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Boden mit Welt-Progression (Feldweg → Pflaster → Gold).
    /// </summary>
    private void DrawGround(SKCanvas canvas, SKRect bounds, float groundY, float nightDim, GameState state)
    {
        int worldTier = GetWorldTier(state.PlayerLevel);

        // Grundfarbe je nach Tier
        SKColor grassColor = worldTier switch
        {
            <= 2 => new SKColor(0x6D, 0x8B, 0x54),  // Feldweg-Gras
            <= 4 => new SKColor(0x5A, 0x7D, 0x4A),  // Gepflegtes Gras
            <= 6 => new SKColor(0x4A, 0x70, 0x3E),  // Dunkles Gras
            _ => new SKColor(0x3D, 0x63, 0x34),       // Premium-Rasen
        };

        _groundPaint.Color = CityBuildingShapes.ApplyDim(grassColor, nightDim);
        canvas.DrawRect(bounds.Left, groundY, bounds.Width, bounds.Height - (groundY - bounds.Top), _groundPaint);
    }

    // DrawStreet wurde durch CityProgressionHelper.DrawProgressiveStreet() ersetzt

    // ═════════════════════════════════════════════════════════════════
    // GEBÄUDE-REIHEN
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Workshop-Reihe als isometrische 2.5D-Gebäude.
    /// </summary>
    private void DrawWorkshopRow(SKCanvas canvas, SKRect bounds, GameState state, float rowBottom, float nightDim)
    {
        var allTypes = Enum.GetValues<WorkshopType>();
        int count = allTypes.Length;
        if (count == 0) return;

        float totalWidth = bounds.Width - 16;
        float gap = 5;
        float buildingWidth = Math.Max(22, (totalWidth - (count - 1) * gap) / count);

        // Höchstes Level finden für Spotlight
        int highestLevel = 0;
        int highestIdx = -1;
        for (int i = 0; i < count; i++)
        {
            var ws = state.Workshops.FirstOrDefault(w => w.Type == allTypes[i]);
            if (ws != null && state.IsWorkshopUnlocked(allTypes[i]) && ws.Level > highestLevel)
            {
                highestLevel = ws.Level;
                highestIdx = i;
            }
        }

        float x = bounds.Left + 8;
        for (int i = 0; i < count; i++)
        {
            var type = allTypes[i];
            var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
            bool isUnlocked = state.IsWorkshopUnlocked(type);

            if (isUnlocked && workshop != null)
            {
                int level = workshop.Level;
                float height = CityBuildingShapes.GetBuildingHeight(level);
                float buildingTop = rowBottom - height;

                // Spotlight auf höchstes Gebäude (warmer Lichtkreis)
                if (i == highestIdx)
                {
                    float spotPulse = 0.8f + MathF.Sin(_time * 1.5f) * 0.15f;
                    _particlePaint.Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(12 * spotPulse));
                    canvas.DrawCircle(x + buildingWidth / 2, buildingTop + height * 0.4f,
                        buildingWidth * 1.2f, _particlePaint);
                }

                // Isometrisches Workshop-Gebäude
                CityBuildingShapes.DrawIsometricWorkshop(
                    canvas, x, buildingTop, buildingWidth, height,
                    type, level, nightDim, _time);

                // Kleine farbige Fahne am Gebäude (Workshop-Farbe)
                DrawWorkshopFlag(canvas, x + buildingWidth - 3, buildingTop + 2, type, nightDim);

                // Aufsteigende Gold-Münzen bei hohem Level (Lv100+)
                if (level >= 100 && MathF.Sin(_time * 0.7f + i * 2.3f) > 0.85f)
                {
                    float coinPhase = (_time * 0.5f + i * 1.7f) % 2f;
                    if (coinPhase < 1.5f)
                    {
                        float coinY = buildingTop + height * 0.3f - coinPhase * 8;
                        byte coinAlpha = (byte)(180 * (1 - coinPhase / 1.5f));
                        _particlePaint.Color = new SKColor(0xFF, 0xD7, 0x00, coinAlpha);
                        canvas.DrawCircle(x + buildingWidth * 0.5f + MathF.Sin(coinPhase * 3) * 3,
                            coinY, 2, _particlePaint);
                    }
                }

                // Mini-Arbeiter vor dem Workshop (1-3 je nach Worker-Anzahl)
                int workerCount = workshop.Workers?.Count(w => !w.IsResting) ?? 0;
                int displayWorkers = Math.Min(workerCount, 3);
                for (int w = 0; w < displayWorkers; w++)
                {
                    float wx = x + buildingWidth * 0.2f + w * (buildingWidth * 0.25f);
                    float wy = rowBottom + 1;
                    CityBuildingShapes.DrawMiniWorker(canvas, wx, wy, type,
                        _time + w * 1.3f, nightDim);
                }

                // Level-Label unter dem Gebäude
                _labelPaint.Color = CityBuildingShapes.ApplyDim(SKColors.White, nightDim);
                string label = $"Lv{level}";
                float textWidth = _labelFont.MeasureText(label);
                canvas.DrawText(label, x + (buildingWidth - textWidth) / 2f, rowBottom + 15,
                    SKTextAlign.Left, _labelFont, _labelPaint);

                // Workshop-Mini-Icon unter dem Level-Label
                CityBuildingShapes.DrawWorkshopMiniIcon(
                    canvas, x + buildingWidth / 2f, rowBottom + 26, type, nightDim);
            }
            else
            {
                // Gesperrtes Gebäude
                float lockedHeight = 28;
                CityBuildingShapes.DrawLockedBuilding(
                    canvas, x, rowBottom - lockedHeight, buildingWidth, lockedHeight, nightDim);
            }

            x += buildingWidth + gap;
        }
    }

    /// <summary>
    /// Support-Gebäude-Reihe (unterhalb der Straße, kleiner).
    /// </summary>
    private void DrawBuildingRow(SKCanvas canvas, SKRect bounds, List<Building> buildings, float rowTop, float nightDim)
    {
        var allTypes = Enum.GetValues<BuildingType>();
        int count = allTypes.Length;
        if (count == 0) return;

        float totalWidth = bounds.Width - 16;
        float gap = 4;
        float buildingWidth = Math.Max(18, (totalWidth - (count - 1) * gap) / count);

        float x = bounds.Left + 8;
        for (int i = 0; i < count; i++)
        {
            var type = allTypes[i];
            var building = buildings.FirstOrDefault(b => b.Type == type);
            bool isBuilt = building?.IsBuilt ?? false;

            if (isBuilt)
            {
                CityBuildingShapes.DrawIsometricBuilding(
                    canvas, x, rowTop, buildingWidth, type, building!.Level, nightDim);
            }
            else
            {
                // Nicht gebaut: gestricheltes Outline
                var outlineColor = CityBuildingShapes.ApplyDim(new SKColor(0x60, 0x60, 0x60), nightDim);
                _decoStrokePaint.Color = outlineColor;
                _decoStrokePaint.StrokeWidth = 1;
                _decoStrokePaint.PathEffect = _dashedEffect;
                canvas.DrawRoundRect(x, rowTop, buildingWidth, 18, 2, 2, _decoStrokePaint);
                _decoStrokePaint.PathEffect = null;
            }

            x += buildingWidth + gap;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // LIEFERWAGEN (fährt periodisch über die Straße)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 2 Lieferwagen (Hin- und Gegenrichtung) + 2-3 Fußgänger.
    /// </summary>
    private void DrawDeliveryVan(SKCanvas canvas, SKRect bounds, float streetY, float nightDim)
    {
        // Lieferwagen 1: links→rechts (10s Zyklus)
        DrawSingleVan(canvas, bounds, streetY, nightDim, 10f, 0f, false);
        // Lieferwagen 2: rechts→links (14s Zyklus, versetzt)
        DrawSingleVan(canvas, bounds, streetY, nightDim, 14f, 5f, true);

        // 2-3 Fußgänger auf dem Bürgersteig
        DrawPedestrians(canvas, bounds, streetY, nightDim);
    }

    private void DrawSingleVan(SKCanvas canvas, SKRect bounds, float streetY,
        float nightDim, float cycleDuration, float offset, bool reversed)
    {
        float vanPhase = ((_time + offset) % cycleDuration) / cycleDuration;
        if (vanPhase < 0.3f || vanPhase > 0.9f) return;

        float drivePhase = (vanPhase - 0.3f) / 0.6f;
        if (reversed) drivePhase = 1 - drivePhase;

        float vanX = bounds.Left - 30 + drivePhase * (bounds.Width + 60);
        float vanY = reversed ? streetY + 5 : streetY + 1;
        float vanW = 24f;
        float vanH = 10f;

        // Karosserie
        var bodyColor = reversed ? new SKColor(0xEA, 0x58, 0x0C) : new SKColor(0xE8, 0xE8, 0xE8);
        _vanPaint.Color = CityBuildingShapes.ApplyDim(bodyColor, nightDim);
        canvas.DrawRoundRect(vanX, vanY, vanW, vanH, 2, 2, _vanPaint);

        // Fahrerkabine
        float cabX = reversed ? vanX : vanX + vanW * 0.7f;
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xD0, 0xD0, 0xD0), nightDim);
        canvas.DrawRoundRect(cabX, vanY + 1, vanW * 0.25f, vanH - 2, 1.5f, 1.5f, _vanPaint);

        // Windschutzscheibe
        float windX = reversed ? vanX + 2 : vanX + vanW * 0.73f;
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x90, 0xCA, 0xF9), nightDim);
        canvas.DrawRect(windX, vanY + 2, vanW * 0.18f, vanH * 0.35f, _vanPaint);

        // Räder
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x33, 0x33, 0x33), nightDim);
        canvas.DrawCircle(vanX + vanW * 0.2f, vanY + vanH, 2.5f, _vanPaint);
        canvas.DrawCircle(vanX + vanW * 0.75f, vanY + vanH, 2.5f, _vanPaint);
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xA0, 0xA0, 0xA0), nightDim);
        canvas.DrawCircle(vanX + vanW * 0.2f, vanY + vanH, 1.2f, _vanPaint);
        canvas.DrawCircle(vanX + vanW * 0.75f, vanY + vanH, 1.2f, _vanPaint);
    }

    /// <summary>
    /// 3 Fußgänger auf dem Bürgersteig (hin und her laufend).
    /// </summary>
    private void DrawPedestrians(SKCanvas canvas, SKRect bounds, float streetY, float nightDim)
    {
        for (int i = 0; i < 3; i++)
        {
            float speed = 8f + i * 3f;
            float offset = i * bounds.Width * 0.3f + 20;
            float cycleLen = bounds.Width * 1.4f;
            float pos = ((_time * speed + offset) % cycleLen);
            bool goingRight = pos < cycleLen / 2;
            float pedestrianX = goingRight
                ? bounds.Left + pos * 2 / cycleLen * bounds.Width
                : bounds.Left + (1 - (pos - cycleLen / 2) * 2 / cycleLen) * bounds.Width;
            float pedestrianY = streetY - 2;

            // Mini-Figur (5dp hoch)
            byte headAlpha = (byte)(nightDim * 200);
            // Kopf
            _particlePaint.Color = new SKColor(0xFF, 0xDA, 0xB9, headAlpha);
            canvas.DrawCircle(pedestrianX, pedestrianY - 4, 1.5f, _particlePaint);
            // Körper (verschiedene Farben)
            var bodyColors = new SKColor[]
            {
                new(0x42, 0xA5, 0xF5), new(0xEF, 0x53, 0x50), new(0x66, 0xBB, 0x6A)
            };
            _particlePaint.Color = CityBuildingShapes.ApplyDim(bodyColors[i], nightDim);
            canvas.DrawRect(pedestrianX - 1, pedestrianY - 2.5f, 2, 3, _particlePaint);
            // Beine (animiert)
            float legPhase = MathF.Sin(_time * 6f + i * 2f) * 1.5f;
            _particlePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x44, 0x44, 0x44), nightDim);
            canvas.DrawLine(pedestrianX - 0.5f, pedestrianY + 0.5f,
                pedestrianX - 0.5f + legPhase * 0.3f, pedestrianY + 2.5f, _particlePaint);
            canvas.DrawLine(pedestrianX + 0.5f, pedestrianY + 0.5f,
                pedestrianX + 0.5f - legPhase * 0.3f, pedestrianY + 2.5f, _particlePaint);
        }
    }

    /// <summary>
    /// Kleine farbige Fahne am Workshop-Gebäude.
    /// </summary>
    private void DrawWorkshopFlag(SKCanvas canvas, float x, float y, WorkshopType type, float nightDim)
    {
        var flagColor = CityBuildingShapes.GetWorkshopColor(type);
        flagColor = CityBuildingShapes.ApplyDim(flagColor, nightDim);

        // Stange
        _particlePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x60, 0x60, 0x60), nightDim);
        canvas.DrawRect(x, y, 0.8f, 8, _particlePaint);

        // Fahne (weht im Wind)
        float wave = MathF.Sin(_time * 2.5f) * 1.5f;
        _particlePaint.Color = flagColor;
        using var flagPath = new SKPath();
        flagPath.MoveTo(x + 1, y);
        flagPath.LineTo(x + 6 + wave, y + 1.5f);
        flagPath.LineTo(x + 1, y + 4);
        flagPath.Close();
        canvas.DrawPath(flagPath, _particlePaint);
    }

    // ═════════════════════════════════════════════════════════════════
    // WORKSHOP-PARTIKEL (Rauch, Funken, Tropfen etc.)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Workshop-spezifische Partikel über den Gebäuden.
    /// Rauch (Carpenter), Wassertropfen (Plumber), Funken (Electrician), etc.
    /// </summary>
    private void DrawWorkshopParticles(SKCanvas canvas, SKRect bounds, GameState state, float rowBottom, float nightDim)
    {
        var allTypes = Enum.GetValues<WorkshopType>();
        int count = allTypes.Length;
        float totalWidth = bounds.Width - 16;
        float gap = 5;
        float bw = Math.Max(22, (totalWidth - (count - 1) * gap) / count);

        float x = bounds.Left + 8;
        for (int i = 0; i < count; i++)
        {
            var type = allTypes[i];
            var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
            bool isUnlocked = state.IsWorkshopUnlocked(type);

            if (isUnlocked && workshop != null)
            {
                float height = CityBuildingShapes.GetBuildingHeight(workshop.Level);
                float buildingTop = rowBottom - height;
                float centerX = x + bw / 2f;
                float roofTop = buildingTop - bw * 0.15f * 0.6f;

                switch (type)
                {
                    case WorkshopType.Carpenter:
                        // Rauch aus dem Schornstein
                        DrawSmoke(canvas, x + bw * 0.7f + 3, roofTop - 10, nightDim);
                        break;
                    case WorkshopType.Plumber:
                        // Wasser-Tropfen
                        DrawWaterDrops(canvas, centerX, buildingTop, nightDim);
                        break;
                    case WorkshopType.Electrician:
                        // Funken
                        DrawSparks(canvas, x + bw * 0.8f, roofTop - 12);
                        break;
                    case WorkshopType.Painter:
                        // Farb-Tropfen (werden schon im RoofDetail gezeichnet)
                        break;
                    case WorkshopType.Roofer:
                        // Staub
                        DrawDust(canvas, centerX, buildingTop, nightDim);
                        break;
                    case WorkshopType.Contractor:
                        // Staub (mehr)
                        DrawDust(canvas, centerX, buildingTop + height * 0.3f, nightDim);
                        break;
                    case WorkshopType.Architect:
                        // Papier-Fetzen
                        DrawPaperShreds(canvas, centerX, buildingTop, nightDim);
                        break;
                    case WorkshopType.GeneralContractor:
                        // Gold-Glitzer
                        DrawGoldSparkle(canvas, centerX, buildingTop, bw);
                        break;
                }
            }

            x += bw + gap;
        }
    }

    /// <summary>
    /// Rauchpartikel (5 Partikel, steigen auf und verblassen).
    /// </summary>
    private void DrawSmoke(SKCanvas canvas, float smokeX, float startY, float nightDim)
    {
        for (int p = 0; p < 5; p++)
        {
            float phase = (_time * 0.8f + p * 1.2f) % 3.0f;
            if (phase > 2.0f) continue;

            float progress = phase / 2.0f;
            float px = smokeX + MathF.Sin(progress * 3f + p) * 4;
            float py = startY - progress * 18;
            byte alpha = (byte)((1f - progress) * 100);
            float size = 2.5f + progress * 4;

            _smokePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xA0, 0xA0, 0xA0, alpha), nightDim);
            canvas.DrawCircle(px, py, size, _smokePaint);
        }
    }

    /// <summary>
    /// Wasser-Tropfen (Plumber).
    /// </summary>
    private void DrawWaterDrops(SKCanvas canvas, float cx, float startY, float nightDim)
    {
        for (int p = 0; p < 4; p++)
        {
            float phase = (_time * 1.2f + p * 1.5f) % 2.5f;
            if (phase > 1.5f) continue;

            float progress = phase / 1.5f;
            float py = startY + progress * 10;
            byte alpha = (byte)((1f - progress) * 180);

            _particlePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x42, 0xA5, 0xF5, alpha), nightDim);
            canvas.DrawCircle(cx + (p - 0.5f) * 6, py, 1.5f - progress * 0.5f, _particlePaint);
        }
    }

    /// <summary>
    /// Funken (Electrician).
    /// </summary>
    private void DrawSparks(SKCanvas canvas, float cx, float cy)
    {
        for (int p = 0; p < 5; p++)
        {
            float phase = (_time * 2f + p * 0.8f) % 1.5f;
            if (phase > 0.5f) continue;

            float progress = phase / 0.5f;
            byte alpha = (byte)((1f - progress) * 255);
            float dx = MathF.Cos(p * 2.1f + _time) * 5 * progress;
            float dy = -progress * 8 + MathF.Sin(p * 1.7f) * 3;

            _particlePaint.Color = new SKColor(0xFF, 0xD5, 0x4F, alpha);
            canvas.DrawCircle(cx + dx, cy + dy, 1.5f - progress, _particlePaint);
        }
    }

    /// <summary>
    /// Staub-Partikel (Roofer, Contractor).
    /// </summary>
    private void DrawDust(SKCanvas canvas, float cx, float startY, float nightDim)
    {
        for (int p = 0; p < 4; p++)
        {
            float phase = (_time * 0.5f + p * 1.8f) % 3.0f;
            if (phase > 1.5f) continue;

            float progress = phase / 1.5f;
            float px = cx + MathF.Sin(progress * 2f + p * 3f) * 8;
            float py = startY - progress * 6;
            byte alpha = (byte)((1f - progress) * 60);

            _particlePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xA0, 0x90, 0x80, alpha), nightDim);
            canvas.DrawCircle(px, py, 2f + progress * 2, _particlePaint);
        }
    }

    /// <summary>
    /// Papier-Fetzen (Architect).
    /// </summary>
    private void DrawPaperShreds(SKCanvas canvas, float cx, float startY, float nightDim)
    {
        for (int p = 0; p < 4; p++)
        {
            float phase = (_time * 0.4f + p * 2.2f) % 4.0f;
            if (phase > 2.0f) continue;

            float progress = phase / 2.0f;
            float px = cx + MathF.Sin(progress * MathF.PI + p * 2f) * 10;
            float py = startY - 5 + progress * 12;
            byte alpha = (byte)((1f - progress) * 120);
            float rotation = progress * 360f + p * 90f;

            canvas.Save();
            canvas.Translate(px, py);
            canvas.RotateDegrees(rotation);
            _particlePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xF5, 0xF5, 0xDC, alpha), nightDim);
            canvas.DrawRect(-2, -1, 4, 2, _particlePaint);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Gold-Glitzer (GeneralContractor).
    /// </summary>
    private void DrawGoldSparkle(SKCanvas canvas, float cx, float startY, float width)
    {
        for (int p = 0; p < 6; p++)
        {
            float phase = (_time * 1.5f + p * 0.7f) % 2.0f;
            if (phase > 0.8f) continue;

            float progress = phase / 0.8f;
            byte alpha = (byte)((1f - progress) * 200);

            uint hash = (uint)(p * 3571 + (int)(_time * 2f));
            float px = cx + ((hash % 100) / 100f - 0.5f) * width;
            float py = startY + ((hash * 7 % 100) / 100f) * 15 - 10;

            _particlePaint.Color = new SKColor(0xFF, 0xD7, 0x00, alpha);
            float size = 1f + progress * 1.5f;

            // Stern-Form (4 Strahlen)
            canvas.DrawLine(px - size, py, px + size, py, _particlePaint);
            canvas.DrawLine(px, py - size, px, py + size, _particlePaint);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // BODEN-WETTER-EFFEKTE
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Statische Wetter-Effekte am Boden (Pfützen bei Regen, liegende Blätter im Herbst).
    /// </summary>
    private void DrawGroundWeatherEffects(SKCanvas canvas, SKRect bounds, float streetY, float nightDim)
    {
        var weather = _weatherSystem.CurrentWeather;

        switch (weather)
        {
            case CityWeatherSystem.WeatherType.Rain:
                // Pfützen auf der Straße (kleine blaue Ovale mit Tropfen-Splash)
                for (int i = 0; i < 6; i++)
                {
                    uint hash = (uint)(i * 4273 + 1931);
                    float px = bounds.Left + (hash % 1000) / 1000f * bounds.Width;
                    hash = hash * 1664525 + 1013904223;
                    float pw = 5 + (hash % 100) / 100f * 8;

                    _particlePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x60, 0x90, 0xC0, 0x30), nightDim);
                    canvas.DrawOval(px, streetY + 5, pw, 1.5f, _particlePaint);

                    // Tropfen-Splash (kleiner Kreis der pulsiert)
                    float splashPhase = (_time * 2f + i * 1.3f) % 2f;
                    if (splashPhase < 0.5f)
                    {
                        float splashR = splashPhase * 6;
                        byte splashAlpha = (byte)((1 - splashPhase * 2) * 50);
                        _particlePaint.Color = new SKColor(0x80, 0xB0, 0xE0, splashAlpha);
                        _particlePaint.Style = SKPaintStyle.Stroke;
                        _particlePaint.StrokeWidth = 0.5f;
                        canvas.DrawCircle(px, streetY + 5, splashR, _particlePaint);
                        _particlePaint.Style = SKPaintStyle.Fill;
                    }
                }
                break;

            case CityWeatherSystem.WeatherType.Leaves:
                // Blätter die auf dem Boden liegen (statisch, nicht fallend)
                SKColor[] groundLeafColors =
                [
                    new(0xFF, 0x8C, 0x00), new(0xCD, 0x53, 0x1B),
                    new(0xD4, 0xA0, 0x17), new(0x8B, 0x45, 0x13)
                ];
                for (int i = 0; i < 10; i++)
                {
                    uint hash = (uint)(i * 6091 + 2383);
                    float lx = bounds.Left + (hash % 1000) / 1000f * bounds.Width;
                    hash = hash * 1664525 + 1013904223;
                    float ly = streetY + 2 + (hash % 100) / 100f * 8;
                    hash = hash * 1664525 + 1013904223;
                    float rot = (hash % 360);

                    _particlePaint.Color = CityBuildingShapes.ApplyDim(
                        groundLeafColors[i % groundLeafColors.Length].WithAlpha(0x80), nightDim);
                    canvas.Save();
                    canvas.Translate(lx, ly);
                    canvas.RotateDegrees(rot);
                    canvas.DrawOval(0, 0, 2.5f, 1.2f, _particlePaint);
                    canvas.Restore();
                }
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // TAP-LABEL + HIGHLIGHT
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeigt ein Tap-Label mit dem Workshop-/Building-Namen an der angegebenen Position.
    /// </summary>
    public void ShowTapLabel(string text, float x, float y, float buildingX, float buildingY, float buildingW, float buildingH, SKColor highlightColor)
    {
        _tapLabel = text;
        _tapLabelX = x;
        _tapLabelY = y - 20; // Über dem Gebäude
        _tapLabelTimer = TapLabelDuration;

        _tapHighlightX = buildingX;
        _tapHighlightY = buildingY;
        _tapHighlightW = buildingW;
        _tapHighlightH = buildingH;
        _tapHighlightTimer = 0.3f;
        _tapHighlightColor = highlightColor;
    }

    /// <summary>
    /// Rendert das Tap-Label (Fade-In/Fade-Out Pill) und den Highlight-Glow.
    /// Aufgerufen am Ende von Render(), über allen anderen Elementen.
    /// </summary>
    private void DrawTapLabel(SKCanvas canvas, float deltaTime)
    {
        // Highlight-Glow um das getippte Gebäude
        if (_tapHighlightTimer > 0)
        {
            _tapHighlightTimer -= deltaTime;
            float highlightAlpha = Math.Clamp(_tapHighlightTimer / 0.3f, 0f, 1f);
            byte a = (byte)(60 * highlightAlpha);
            _particlePaint.Color = new SKColor(_tapHighlightColor.Red, _tapHighlightColor.Green,
                _tapHighlightColor.Blue, a);
            canvas.DrawRoundRect(_tapHighlightX - 2, _tapHighlightY - 2,
                _tapHighlightW + 4, _tapHighlightH + 4, 4, 4, _particlePaint);
        }

        // Tap-Label (Name-Popup)
        if (_tapLabel == null || _tapLabelTimer <= 0) return;

        _tapLabelTimer -= deltaTime;
        if (_tapLabelTimer <= 0)
        {
            _tapLabel = null;
            return;
        }

        // Alpha: 0-0.2s Fade-In, 0.2-1.2s voll, 1.2-1.5s Fade-Out
        float remaining = _tapLabelTimer;
        float elapsed = TapLabelDuration - remaining;
        float alpha;
        if (elapsed < 0.2f)
            alpha = elapsed / 0.2f; // Fade-In
        else if (remaining < 0.3f)
            alpha = remaining / 0.3f; // Fade-Out
        else
            alpha = 1f; // Voll sichtbar

        alpha = Math.Clamp(alpha, 0f, 1f);

        // Text messen
        using var font = new SKFont(SKTypeface.Default, 11);
        float textWidth = font.MeasureText(_tapLabel);
        float pillW = textWidth + 14;
        float pillH = 18;
        float pillX = _tapLabelX - pillW / 2f;
        float pillY = _tapLabelY - pillH / 2f;

        // Hintergrund-Pill (semi-transparent dunkel)
        _particlePaint.Color = new SKColor(0x18, 0x12, 0x10, (byte)(200 * alpha));
        canvas.DrawRoundRect(pillX, pillY, pillW, pillH, pillH / 2f, pillH / 2f, _particlePaint);

        // Rand (Workshop-Farbe, subtil)
        _decoStrokePaint.Color = new SKColor(_tapHighlightColor.Red, _tapHighlightColor.Green,
            _tapHighlightColor.Blue, (byte)(160 * alpha));
        _decoStrokePaint.StrokeWidth = 1f;
        _decoStrokePaint.PathEffect = null;
        canvas.DrawRoundRect(pillX, pillY, pillW, pillH, pillH / 2f, pillH / 2f, _decoStrokePaint);

        // Text (weiß)
        _labelPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(255 * alpha));
        canvas.DrawText(_tapLabel, _tapLabelX - textWidth / 2f, _tapLabelY + 4f,
            SKTextAlign.Left, font, _labelPaint);
    }

    // ═════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bestimmt die Welt-Stufe basierend auf Spieler-Level (1-8).
    /// </summary>
    public static int GetWorldTier(int playerLevel)
    {
        if (playerLevel <= 10) return 1;
        if (playerLevel <= 25) return 2;
        if (playerLevel <= 50) return 3;
        if (playerLevel <= 100) return 4;
        if (playerLevel <= 250) return 5;
        if (playerLevel <= 500) return 6;
        if (playerLevel <= 1000) return 7;
        return 8;
    }

    /// <summary>
    /// Tag/Nacht-Dimm-Faktor (0.6 nachts, 1.0 tagsüber, Übergänge).
    /// </summary>
    private static float GetNightDimFactor()
    {
        // Lokalzeit für visuelle Darstellung (Tag/Nacht-Zyklus)
        int hour = DateTime.Now.Hour;
        if (hour >= 8 && hour < 18) return 1.0f;
        if (hour >= 20 || hour < 6) return 0.6f;
        if (hour >= 6 && hour < 8) return 0.6f + (hour - 6) * 0.2f;
        return 1.0f - (hour - 18) * 0.2f;
    }

    // ═════════════════════════════════════════════════════════════════
    // HIT-TEST (Touch-Navigation auf City-Gebäude)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft welches Gebäude in der City-Skyline getippt wurde.
    /// Verwendet die gleiche Positions-Logik wie DrawWorkshopRow() und DrawBuildingRow().
    /// </summary>
    public CityTapResult HitTest(SKRect bounds, float touchX, float touchY, GameState state, List<Building> buildings)
    {
        // Y-Positionen (identisch zu Render())
        float groundY = bounds.Top + bounds.Height * 0.52f;
        float streetY = groundY + 6;
        float workshopRowBottom = streetY - 2;
        float buildingRowTop = streetY + 14;

        // 1. Workshop-Reihe testen (oberhalb der Straße)
        var workshopTypes = Enum.GetValues<WorkshopType>();
        int wsCount = workshopTypes.Length;
        if (wsCount > 0)
        {
            float totalWidth = bounds.Width - 16;
            float gap = 5;
            float bw = Math.Max(22, (totalWidth - (wsCount - 1) * gap) / wsCount);
            float x = bounds.Left + 8;

            for (int i = 0; i < wsCount; i++)
            {
                var ws = state.Workshops.FirstOrDefault(w => w.Type == workshopTypes[i]);
                bool isUnlocked = state.IsWorkshopUnlocked(workshopTypes[i]);
                float height = (isUnlocked && ws != null)
                    ? CityBuildingShapes.GetBuildingHeight(ws.Level)
                    : 28f;
                float top = workshopRowBottom - height;

                if (touchX >= x && touchX <= x + bw &&
                    touchY >= top && touchY <= workshopRowBottom)
                {
                    return new CityTapResult
                    {
                        Target = CityTapTarget.Workshop,
                        Index = i,
                        WorkshopType = workshopTypes[i],
                        HitX = x, HitY = top, HitW = bw, HitH = height
                    };
                }
                x += bw + gap;
            }
        }

        // 2. Building-Reihe testen (unterhalb der Straße)
        var buildingTypes = Enum.GetValues<BuildingType>();
        int bldCount = buildingTypes.Length;
        if (bldCount > 0)
        {
            float totalWidth = bounds.Width - 16;
            float gap = 4;
            float bw = Math.Max(18, (totalWidth - (bldCount - 1) * gap) / bldCount);
            float x = bounds.Left + 8;

            for (int i = 0; i < bldCount; i++)
            {
                var building = buildings.FirstOrDefault(b => b.Type == buildingTypes[i]);
                float height = (building?.IsBuilt ?? false) ? 18f + building!.Level * 3f : 18f;

                if (touchX >= x && touchX <= x + bw &&
                    touchY >= buildingRowTop && touchY <= buildingRowTop + height)
                {
                    return new CityTapResult
                    {
                        Target = CityTapTarget.Building,
                        Index = i,
                        BuildingType = buildingTypes[i],
                        HitX = x, HitY = buildingRowTop, HitW = bw, HitH = height
                    };
                }
                x += bw + gap;
            }
        }

        return new CityTapResult { Target = CityTapTarget.None };
    }
}
