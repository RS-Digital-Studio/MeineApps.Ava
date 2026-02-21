using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

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

        // Workshop-Partikel (Rauch, Funken etc. - über den Gebäuden)
        DrawWorkshopParticles(canvas, bounds, state, workshopRowBottom, nightDim);
    }

    // ═════════════════════════════════════════════════════════════════
    // PARALLAX-HINTERGRUND (5 Layer)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Himmel mit Tag/Nacht-Gradient (Layer 1, statisch).
    /// </summary>
    private void DrawSkyLayer(SKCanvas canvas, SKRect bounds, float nightDim)
    {
        // Tages-Gradient: Lebhaftere Farben bei höheren Leveln
        var topColor = CityBuildingShapes.ApplyDim(new SKColor(0x5B, 0xA3, 0xD9), nightDim);
        var bottomColor = CityBuildingShapes.ApplyDim(new SKColor(0xB0, 0xD4, 0xF1), nightDim);
        topColor = CityProgressionHelper.ApplyVibrancy(topColor, _cachedVibrancy);
        bottomColor = CityProgressionHelper.ApplyVibrancy(bottomColor, _cachedVibrancy);

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.55f),
            new[] { topColor, bottomColor },
            null, SKShaderTileMode.Clamp);
        _skyPaint.Shader = shader;
        canvas.DrawRect(bounds, _skyPaint);
        _skyPaint.Shader = null;
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

                // Isometrisches Workshop-Gebäude
                CityBuildingShapes.DrawIsometricWorkshop(
                    canvas, x, buildingTop, buildingWidth, height,
                    type, level, nightDim, _time);

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
                _decoStrokePaint.PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0);
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
    /// Animierter Lieferwagen der alle ~10s über die Straße fährt.
    /// </summary>
    private void DrawDeliveryVan(SKCanvas canvas, SKRect bounds, float streetY, float nightDim)
    {
        float cycleDuration = 10f;
        float vanPhase = (_time % cycleDuration) / cycleDuration;

        // Nur sichtbar in Phase 0.3-0.9 (60% der Zeit fährt er durch)
        if (vanPhase < 0.3f || vanPhase > 0.9f) return;

        float drivePhase = (vanPhase - 0.3f) / 0.6f; // 0→1
        float vanX = bounds.Left - 30 + drivePhase * (bounds.Width + 60);
        float vanY = streetY + 1;
        float vanW = 24f;
        float vanH = 10f;

        // Karosserie
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xE8, 0xE8, 0xE8), nightDim);
        canvas.DrawRoundRect(vanX, vanY, vanW, vanH, 2, 2, _vanPaint);

        // Fahrerkabine (vorne)
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xD0, 0xD0, 0xD0), nightDim);
        canvas.DrawRoundRect(vanX + vanW * 0.7f, vanY + 1, vanW * 0.25f, vanH - 2, 1.5f, 1.5f, _vanPaint);

        // Windschutzscheibe
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x90, 0xCA, 0xF9), nightDim);
        canvas.DrawRect(vanX + vanW * 0.73f, vanY + 2, vanW * 0.18f, vanH * 0.35f, _vanPaint);

        // Räder
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x33, 0x33, 0x33), nightDim);
        canvas.DrawCircle(vanX + vanW * 0.2f, vanY + vanH, 2.5f, _vanPaint);
        canvas.DrawCircle(vanX + vanW * 0.75f, vanY + vanH, 2.5f, _vanPaint);
        // Felgen
        _vanPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0xA0, 0xA0, 0xA0), nightDim);
        canvas.DrawCircle(vanX + vanW * 0.2f, vanY + vanH, 1.2f, _vanPaint);
        canvas.DrawCircle(vanX + vanW * 0.75f, vanY + vanH, 1.2f, _vanPaint);
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
    /// Rauchpartikel (3 Partikel, steigen auf und verblassen).
    /// </summary>
    private void DrawSmoke(SKCanvas canvas, float smokeX, float startY, float nightDim)
    {
        for (int p = 0; p < 3; p++)
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
        for (int p = 0; p < 2; p++)
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
        for (int p = 0; p < 3; p++)
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
        for (int p = 0; p < 2; p++)
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
        for (int p = 0; p < 2; p++)
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
        for (int p = 0; p < 4; p++)
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
        int hour = DateTime.Now.Hour;
        if (hour >= 8 && hour < 18) return 1.0f;
        if (hour >= 20 || hour < 6) return 0.6f;
        if (hour >= 6 && hour < 8) return 0.6f + (hour - 6) * 0.2f;
        return 1.0f - (hour - 18) * 0.2f;
    }
}
