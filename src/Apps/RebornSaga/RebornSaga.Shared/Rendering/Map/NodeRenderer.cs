namespace RebornSaga.Rendering.Map;

using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Zeichnet einzelne Map-Knoten mit Typ-spezifischen Farben, Glow-Effekten und Status-Indikatoren.
/// Alle Paints statisch gepooled.
/// </summary>
public static class NodeRenderer
{
    // Knoten-Farben (aus Design-Dokument)
    private static readonly SKColor StoryColor = new(0xF3, 0x9C, 0x12);     // Gold
    private static readonly SKColor SideQuestColor = new(0xC0, 0xC0, 0xC0); // Silber
    private static readonly SKColor BossColor = new(0xE7, 0x4C, 0x3C);      // Rot
    private static readonly SKColor NpcColor = new(0x4A, 0x90, 0xD9);       // Blau
    private static readonly SKColor DungeonColor = new(0x9B, 0x59, 0xB6);   // Lila
    private static readonly SKColor RestColor = new(0x2E, 0xCC, 0x71);      // Grün
    private static readonly SKColor LockedColor = new(0x6E, 0x76, 0x81);    // Grau
    private static readonly SKColor FogColor = new(0x30, 0x36, 0x3D);       // Nebel

    // Gepoolte Paints
    private static readonly SKPaint _nodePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _checkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, StrokeCap = SKStrokeCap.Round };

    // Gecachte MaskFilter
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);

    // SpriteCache für AI-generierte Node-Icons
    private static SpriteCache? _spriteCache;
    private static readonly SKPaint _nodeBitmapPaint = new() { IsAntialias = true };

    /// <summary>
    /// Setzt die SpriteCache-Referenz für AI-generierte Node-Icons.
    /// Wird beim Szenen-Setup aufgerufen.
    /// </summary>
    public static void SetSpriteCache(SpriteCache? spriteCache) => _spriteCache = spriteCache;

    /// <summary>
    /// Zeichnet einen Map-Knoten an der gegebenen Position.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="node">Map-Knoten mit Typ und Status.</param>
    /// <param name="cx">Zentrum-X in Canvas-Koordinaten.</param>
    /// <param name="cy">Zentrum-Y in Canvas-Koordinaten.</param>
    /// <param name="radius">Radius des Knotens.</param>
    /// <param name="pulsePhase">Puls-Animation Phase (0-1) für aktiven Knoten.</param>
    public static void Draw(SKCanvas canvas, MapNode node, float cx, float cy, float radius, float pulsePhase)
    {
        if (!node.IsRevealed)
        {
            // Nebel-Knoten: Dunkler Kreis mit diffusem Rand
            _nodePaint.Color = FogColor.WithAlpha(80);
            canvas.DrawCircle(cx, cy, radius * 0.6f, _nodePaint);
            return;
        }

        var color = GetNodeColor(node);

        // Glow-Effekt für aktiven/erreichbaren Knoten
        if (node.IsCurrent || node.IsAccessible)
        {
            var glowAlpha = node.IsCurrent
                ? (byte)Math.Min(255, 60 + (int)(40 * MathF.Sin(pulsePhase * MathF.PI * 2f)))
                : (byte)30;

            _glowPaint.Color = color.WithAlpha(glowAlpha);
            _glowPaint.MaskFilter = _glowFilter;
            canvas.DrawCircle(cx, cy, radius * 1.4f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Äußerer Ring
        _borderPaint.Color = node.IsAccessible || node.IsCompleted ? color : LockedColor;
        canvas.DrawCircle(cx, cy, radius, _borderPaint);

        // Knoten-Körper
        var bodyAlpha = node.IsAccessible || node.IsCompleted ? (byte)255 : (byte)100;
        _nodePaint.Color = color.WithAlpha(bodyAlpha);
        canvas.DrawCircle(cx, cy, radius - 2f, _nodePaint);

        // Inneres Icon je nach Typ
        DrawNodeIcon(canvas, node.Type, cx, cy, radius * 0.5f);

        // Goldener Haken bei erledigten Knoten
        if (node.IsCompleted)
            DrawCheckmark(canvas, cx, cy, radius);

        // Schloss-Symbol bei gesperrten Knoten
        if (!node.IsAccessible && !node.IsCompleted && node.Type != MapNodeType.Locked)
            DrawLockIcon(canvas, cx + radius * 0.6f, cy - radius * 0.6f, radius * 0.3f);
    }

    /// <summary>
    /// Gibt die Farbe für den Knoten-Typ zurück.
    /// </summary>
    public static SKColor GetNodeColor(MapNode node)
    {
        if (!node.IsAccessible && !node.IsCompleted) return LockedColor;

        return node.Type switch
        {
            MapNodeType.Story => StoryColor,
            MapNodeType.SideQuest => SideQuestColor,
            MapNodeType.Boss => BossColor,
            MapNodeType.Npc => NpcColor,
            MapNodeType.Dungeon => DungeonColor,
            MapNodeType.Rest => RestColor,
            MapNodeType.Locked => LockedColor,
            _ => LockedColor
        };
    }

    /// <summary>
    /// Zeichnet ein Typ-spezifisches Icon im Knoten-Zentrum.
    /// Versucht zuerst AI-generiertes Icon zu laden, sonst prozeduraler Fallback.
    /// </summary>
    private static void DrawNodeIcon(SKCanvas canvas, MapNodeType type, float cx, float cy, float size)
    {
        // AI-Icon versuchen
        var iconKey = type switch
        {
            MapNodeType.Story => "map/nodes/story",
            MapNodeType.Boss => "map/nodes/boss",
            MapNodeType.SideQuest => "map/nodes/sidequest",
            MapNodeType.Rest => "map/nodes/rest",
            MapNodeType.Npc => "map/nodes/npc",
            MapNodeType.Dungeon => "map/nodes/dungeon",
            _ => null
        };

        if (iconKey != null && _spriteCache != null)
        {
            var icon = _spriteCache.GetMapNodeIcon(iconKey);
            if (icon != null)
            {
                var iconRect = new SKRect(cx - size, cy - size, cx + size, cy + size);
                var srcRect = new SKRect(0, 0, icon.Width, icon.Height);
                canvas.DrawBitmap(icon, srcRect, iconRect, _nodeBitmapPaint);
                return;
            }
        }

        // Fallback: Prozedurales Icon
        DrawProceduralNodeIcon(canvas, type, cx, cy, size);
    }

    /// <summary>
    /// Prozeduraler Fallback für Map-Node-Icons (geometrische Symbole).
    /// </summary>
    private static void DrawProceduralNodeIcon(SKCanvas canvas, MapNodeType type, float cx, float cy, float size)
    {
        _iconPaint.Color = SKColors.White.WithAlpha(200);

        switch (type)
        {
            case MapNodeType.Story:
                // Ausrufezeichen
                UIRenderer.DrawText(canvas, "!", cx, cy, size * 2f, SKColors.White, SKTextAlign.Center, true);
                break;

            case MapNodeType.SideQuest:
                // Fragezeichen
                UIRenderer.DrawText(canvas, "?", cx, cy, size * 2f, SKColors.White, SKTextAlign.Center, true);
                break;

            case MapNodeType.Boss:
                // Totenkopf-Symbol (Kreuz)
                using (var path = new SKPath())
                {
                    path.MoveTo(cx, cy - size);
                    path.LineTo(cx, cy + size);
                    path.MoveTo(cx - size * 0.7f, cy - size * 0.3f);
                    path.LineTo(cx + size * 0.7f, cy - size * 0.3f);
                    _borderPaint.Color = SKColors.White.WithAlpha(200);
                    canvas.DrawPath(path, _borderPaint);
                }
                break;

            case MapNodeType.Npc:
                // Sprechblase
                canvas.DrawCircle(cx, cy - size * 0.2f, size * 0.6f, _iconPaint);
                break;

            case MapNodeType.Dungeon:
                // Eingangs-Symbol (Bogen)
                using (var path = new SKPath())
                {
                    path.MoveTo(cx - size, cy + size);
                    path.LineTo(cx - size, cy - size * 0.5f);
                    path.ArcTo(new SKRect(cx - size, cy - size * 1.5f, cx + size, cy - size * 0.5f + size), 180, -180, false);
                    path.LineTo(cx + size, cy + size);
                    _borderPaint.Color = SKColors.White.WithAlpha(200);
                    canvas.DrawPath(path, _borderPaint);
                }
                break;

            case MapNodeType.Rest:
                // Flammen-Symbol (Dreieck)
                using (var path = new SKPath())
                {
                    path.MoveTo(cx, cy - size);
                    path.LineTo(cx - size * 0.7f, cy + size * 0.5f);
                    path.LineTo(cx + size * 0.7f, cy + size * 0.5f);
                    path.Close();
                    canvas.DrawPath(path, _iconPaint);
                }
                break;
        }
    }

    /// <summary>
    /// Zeichnet einen goldenen Haken über einem erledigten Knoten.
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, float cx, float cy, float radius)
    {
        _checkPaint.Color = StoryColor; // Gold
        using var path = new SKPath();
        var ox = cx + radius * 0.6f;
        var oy = cy - radius * 0.6f;
        path.MoveTo(ox - 5f, oy);
        path.LineTo(ox - 1f, oy + 4f);
        path.LineTo(ox + 5f, oy - 4f);
        canvas.DrawPath(path, _checkPaint);
    }

    /// <summary>
    /// Zeichnet ein kleines Schloss-Symbol.
    /// </summary>
    private static void DrawLockIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        _iconPaint.Color = LockedColor;
        // Schloss-Körper
        canvas.DrawRect(cx - size, cy, size * 2, size * 1.5f, _iconPaint);
        // Bügel
        _borderPaint.Color = LockedColor;
        _borderPaint.StrokeWidth = 1.5f;
        using var path = new SKPath();
        path.AddArc(new SKRect(cx - size * 0.6f, cy - size * 1.2f, cx + size * 0.6f, cy + size * 0.2f), 180, -180);
        canvas.DrawPath(path, _borderPaint);
        _borderPaint.StrokeWidth = 2f;
    }

    /// <summary>
    /// Gibt alle statischen nativen Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _nodePaint.Dispose();
        _borderPaint.Dispose();
        _glowPaint.Dispose();
        _iconPaint.Dispose();
        _checkPaint.Dispose();
        _nodeBitmapPaint.Dispose();
        // _glowFilter ist static readonly — NICHT disposen
    }
}
