namespace RebornSaga.Rendering.Map;

using RebornSaga.Models;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Orchestriert das Rendering der Overworld-Map.
/// Zeichnet Hintergrund, Pfade, Knoten, Spieler-Sprite und HUD.
/// </summary>
public static class OverworldRenderer
{
    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true };
    private static readonly SKPaint _hudBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _playerDotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gecachter Hintergrund-Shader
    private static SKShader? _bgShader;
    private static SKRect _cachedBounds;

    // AI-generierter Regions-Hintergrund (von OverworldScene gesetzt)
    private static SKBitmap? _regionBackground;
    private static readonly SKPaint _bgBitmapPaint = new() { IsAntialias = true };

    // Gecachte Stern-Positionen (deterministische Positionen, einmal berechnet)
    private static float[]? _starNormX;
    private static float[]? _starNormY;
    private static float[]? _starSizes;
    private const int StarCount = 30;

    // Gecachte Gradient-Farben (vermeidet Array-Allokation bei Bounds-Wechsel)
    private static readonly SKColor[] _bgColors = { new(0x08, 0x0C, 0x12), new(0x12, 0x18, 0x22), new(0x0D, 0x11, 0x17) };
    private static readonly float[] _bgPositions = { 0f, 0.5f, 1f };

    // Gecachter Pfad für Spieler-Marker (vermeidet new SKPath() pro Frame)
    private static readonly SKPath _playerMarkerPath = new();

    // Konstanten
    private const float NodeRadius = 18f;
    private const float MapPadding = 40f;
    private const float HudHeight = 60f;

    /// <summary>
    /// Zeichnet die gesamte Overworld-Map.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Sichtbarer Bereich.</param>
    /// <param name="map">Aktuelle Kapitel-Map.</param>
    /// <param name="animTime">Laufende Animations-Zeit in Sekunden.</param>
    /// <param name="cameraX">Kamera-Offset X (für Scrolling).</param>
    /// <param name="cameraY">Kamera-Offset Y (für Scrolling).</param>
    /// <param name="zoom">Zoom-Faktor (1.0 = normal).</param>
    public static void Draw(SKCanvas canvas, SKRect bounds, ChapterMap map, float animTime,
        float cameraX, float cameraY, float zoom, Dictionary<string, string>? displayNames = null)
    {
        // Hintergrund
        DrawBackground(canvas, bounds, animTime);

        // Map-Bereich (ohne HUD)
        var mapArea = new SKRect(
            bounds.Left + MapPadding,
            bounds.Top + HudHeight + MapPadding,
            bounds.Right - MapPadding,
            bounds.Bottom - MapPadding);

        // Kamera-Transformation anwenden
        canvas.Save();
        canvas.Translate(bounds.MidX + cameraX, bounds.MidY + cameraY);
        canvas.Scale(zoom);
        canvas.Translate(-bounds.MidX, -bounds.MidY);

        // Pfade zeichnen (unter den Knoten)
        DrawPaths(canvas, map, mapArea, animTime);

        // Knoten zeichnen
        DrawNodes(canvas, map, mapArea, animTime, displayNames);

        // Spieler-Position Marker
        DrawPlayerMarker(canvas, map, mapArea, animTime);

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet das HUD (Kapitel-Name, Level, HP, Gold).
    /// Alle Strings müssen vorgecacht vom Aufrufer kommen (keine Allokation pro Frame).
    /// </summary>
    public static void DrawHud(SKCanvas canvas, SKRect bounds, string chapterName,
        string levelText, string hpText, string goldText,
        int playerHp, int playerMaxHp)
    {
        // HUD Hintergrund
        var hudRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + HudHeight);
        _hudBgPaint.Color = UIRenderer.DarkBg.WithAlpha(220);
        canvas.DrawRect(hudRect, _hudBgPaint);

        // Trennlinie
        _hudBgPaint.Color = UIRenderer.Border;
        canvas.DrawRect(new SKRect(bounds.Left, hudRect.Bottom - 1, bounds.Right, hudRect.Bottom), _hudBgPaint);

        var y = hudRect.MidY;
        var leftX = bounds.Left + 16f;

        // Kapitel-Name
        UIRenderer.DrawText(canvas, chapterName, leftX, y, 16f, UIRenderer.TextPrimary, SKTextAlign.Left, true);

        // Rechte Seite: Lv + HP + Gold
        var rightX = bounds.Right - 16f;

        // Gold
        UIRenderer.DrawText(canvas, goldText, rightX, y, 14f, UIRenderer.Accent, SKTextAlign.Right, true);

        // HP
        rightX -= 80f;
        var hpColor = playerHp < playerMaxHp * 0.3f ? UIRenderer.Danger
            : playerHp < playerMaxHp * 0.6f ? UIRenderer.Accent
            : UIRenderer.Success;
        UIRenderer.DrawText(canvas, hpText, rightX, y, 13f, hpColor, SKTextAlign.Right, true);

        // Level
        rightX -= 90f;
        UIRenderer.DrawText(canvas, levelText, rightX, y, 14f, UIRenderer.PrimaryGlow, SKTextAlign.Right, true);
    }

    /// <summary>
    /// Konvertiert normalisierte Knoten-Koordinaten (0-1) in Canvas-Koordinaten.
    /// </summary>
    public static SKPoint NodeToCanvas(MapNode node, SKRect mapArea)
    {
        return new SKPoint(
            mapArea.Left + node.X * mapArea.Width,
            mapArea.Top + node.Y * mapArea.Height);
    }

    /// <summary>
    /// Hit-Test: Gibt die ID des Knotens zurück der unter dem Punkt liegt, oder null.
    /// </summary>
    public static string? HitTestNode(SKPoint point, ChapterMap map, SKRect mapArea,
        float cameraX, float cameraY, float zoom, SKRect bounds)
    {
        // Punkt in Map-Koordinaten transformieren (inverse Kamera-Transformation)
        var invZoom = 1f / zoom;
        var px = (point.X - bounds.MidX - cameraX) * invZoom + bounds.MidX;
        var py = (point.Y - bounds.MidY - cameraY) * invZoom + bounds.MidY;

        foreach (var node in map.Nodes)
        {
            if (!node.IsRevealed) continue;

            var pos = NodeToCanvas(node, mapArea);
            var dx = px - pos.X;
            var dy = py - pos.Y;
            // Erweiterter Touch-Bereich (1.5x Radius)
            if (dx * dx + dy * dy <= NodeRadius * NodeRadius * 2.25f)
                return node.Id;
        }
        return null;
    }

    /// <summary>
    /// Berechnet den Map-Bereich (für externe Hit-Tests).
    /// </summary>
    public static SKRect GetMapArea(SKRect bounds) => new(
        bounds.Left + MapPadding,
        bounds.Top + HudHeight + MapPadding,
        bounds.Right - MapPadding,
        bounds.Bottom - MapPadding);

    /// <summary>
    /// Setzt einen AI-generierten Regions-Hintergrund. Null = prozeduraler Fallback.
    /// </summary>
    public static void SetRegionBackground(SKBitmap? background) => _regionBackground = background;

    // --- Private Render-Methoden ---

    private static void DrawBackground(SKCanvas canvas, SKRect bounds, float animTime)
    {
        // AI-generierter Regions-Hintergrund (wenn vorhanden)
        if (_regionBackground != null)
        {
            var srcRect = new SKRect(0, 0, _regionBackground.Width, _regionBackground.Height);
            canvas.DrawBitmap(_regionBackground, srcRect, bounds, _bgBitmapPaint);
            return;
        }

        // Fallback: Dunkler Gradient mit subtilen Sternen
        if (_bgShader == null || _cachedBounds != bounds)
        {
            _bgShader?.Dispose();
            _bgShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, bounds.Top),
                new SKPoint(bounds.MidX, bounds.Bottom),
                _bgColors, _bgPositions,
                SKShaderTileMode.Clamp);
            _cachedBounds = bounds;
        }
        _bgPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;

        // Gecachte Stern-Positionen (einmal berechnet, dann wiederverwendet)
        if (_starNormX == null)
        {
            var rng = new Random(42);
            _starNormX = new float[StarCount];
            _starNormY = new float[StarCount];
            _starSizes = new float[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                _starNormX[i] = (float)rng.NextDouble();
                _starNormY[i] = (float)rng.NextDouble();
                _starSizes[i] = 1f + (float)rng.NextDouble() * 0.5f;
            }
        }

        for (int i = 0; i < StarCount; i++)
        {
            var sx = bounds.Left + _starNormX![i] * bounds.Width;
            var sy = bounds.Top + _starNormY![i] * bounds.Height;
            var brightness = (byte)Math.Min(255, 40 + (int)(30 * MathF.Sin(animTime * 0.5f + i * 0.7f)));
            _playerDotPaint.Color = SKColors.White.WithAlpha(brightness);
            canvas.DrawCircle(sx, sy, _starSizes![i], _playerDotPaint);
        }
    }

    private static void DrawPaths(SKCanvas canvas, ChapterMap map, SKRect mapArea, float animTime)
    {
        // Knoten als Dictionary für schnellen Zugriff
        foreach (var node in map.Nodes)
        {
            if (!node.IsRevealed) continue;
            var fromPos = NodeToCanvas(node, mapArea);

            foreach (var connId in node.Connections)
            {
                var target = FindNode(map.Nodes, connId);
                if (target == null || !target.IsRevealed) continue;

                var toPos = NodeToCanvas(target, mapArea);
                var isActive = (node.IsAccessible || node.IsCompleted) && (target.IsAccessible || target.IsCompleted);
                var isCompleted = node.IsCompleted && target.IsCompleted;

                PathRenderer.Draw(canvas, fromPos.X, fromPos.Y, toPos.X, toPos.Y, isActive, isCompleted, animTime);
            }
        }
    }

    private static void DrawNodes(SKCanvas canvas, ChapterMap map, SKRect mapArea, float animTime,
        Dictionary<string, string>? displayNames = null)
    {
        foreach (var node in map.Nodes)
        {
            var pos = NodeToCanvas(node, mapArea);
            var pulsePhase = animTime + node.X * 3f + node.Y * 2f; // Leicht versetzte Phasen
            NodeRenderer.Draw(canvas, node, pos.X, pos.Y, NodeRadius, pulsePhase);

            // Knoten-Name unter dem Kreis (nur für sichtbare, erreichbare Knoten)
            if (node.IsRevealed && (node.IsAccessible || node.IsCompleted) && displayNames != null
                && displayNames.TryGetValue(node.Id, out var displayName))
            {
                UIRenderer.DrawText(canvas, displayName, pos.X, pos.Y + NodeRadius + 14f, 11f,
                    UIRenderer.TextSecondary, SKTextAlign.Center, false);
            }
        }
    }

    private static void DrawPlayerMarker(SKCanvas canvas, ChapterMap map, SKRect mapArea, float animTime)
    {
        MapNode? currentNode = null;
        foreach (var node in map.Nodes)
        {
            if (node.IsCurrent) { currentNode = node; break; }
        }
        if (currentNode == null) return;

        var pos = NodeToCanvas(currentNode, mapArea);

        // Pulsierender Spieler-Marker über dem Knoten
        var bounce = MathF.Sin(animTime * 3f) * 4f;
        var markerY = pos.Y - NodeRadius - 12f + bounce;

        // Dreieck-Pfeil nach unten (gecachter Pfad, Rewind statt new)
        _playerMarkerPath.Rewind();
        _playerMarkerPath.MoveTo(pos.X, pos.Y - NodeRadius - 4f + bounce);
        _playerMarkerPath.LineTo(pos.X - 6f, markerY - 4f);
        _playerMarkerPath.LineTo(pos.X + 6f, markerY - 4f);
        _playerMarkerPath.Close();

        _playerDotPaint.Color = UIRenderer.PrimaryGlow;
        canvas.DrawPath(_playerMarkerPath, _playerDotPaint);

        // Leuchtender Punkt darüber
        _playerDotPaint.Color = UIRenderer.PrimaryGlow.WithAlpha(180);
        canvas.DrawCircle(pos.X, markerY - 10f, 4f, _playerDotPaint);
    }

    private static MapNode? FindNode(List<MapNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
        }
        return null;
    }

    /// <summary>
    /// Gibt alle statischen nativen Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _bgShader?.Dispose();
        _bgShader = null;
        _bgPaint.Dispose();
        _hudBgPaint.Dispose();
        _playerDotPaint.Dispose();
        _bgBitmapPaint.Dispose();
        _playerMarkerPath.Dispose();
        _regionBackground = null;
    }
}
