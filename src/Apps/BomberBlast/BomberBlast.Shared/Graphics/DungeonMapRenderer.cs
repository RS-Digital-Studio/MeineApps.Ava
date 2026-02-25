using BomberBlast.Models.Dungeon;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// SkiaSharp-Renderer für die Dungeon Node-Map (Slay the Spire-Inspiration).
/// Zeichnet eine vertikale Map mit farbigen Nodes, Verbindungslinien und Puls-Effekten.
/// Struct-basiert, gepoolte SKPaint - keine per-Frame Allokationen.
/// </summary>
public static class DungeonMapRenderer
{
    // Konstanten
    private const float NODE_RADIUS = 15f;
    private const float BOSS_NODE_RADIUS = 20f;
    private const float ROW_SPACING = 64f;
    private const float COL_SPACING = 80f;
    private const float TOP_PADDING = 30f;
    private const float BOTTOM_PADDING = 20f;
    private const float LINE_WIDTH = 2f;
    private const float COMPLETED_LINE_WIDTH = 3f;
    private const float GLOW_RADIUS = 6f;

    // Gepoolte SKPaint-Objekte
    private static readonly SKPaint _nodePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _nodeBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = LINE_WIDTH };
    private static readonly SKPaint _completedLinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = COMPLETED_LINE_WIDTH };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, TextSize = 10f, TextAlign = SKTextAlign.Center };
    private static readonly SKPaint _modifierBadgePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _labelPaint = new() { IsAntialias = true, TextSize = 9f, TextAlign = SKTextAlign.Center };
    private static readonly SKPaint _floorLabelPaint = new() { IsAntialias = true, TextSize = 11f, TextAlign = SKTextAlign.Right };

    // Raum-Typ Farben
    private static readonly SKColor COLOR_NORMAL = new(158, 158, 158);   // Grau
    private static readonly SKColor COLOR_ELITE = new(244, 67, 54);      // Rot
    private static readonly SKColor COLOR_TREASURE = new(255, 215, 0);   // Gold
    private static readonly SKColor COLOR_CHALLENGE = new(255, 152, 0);  // Orange
    private static readonly SKColor COLOR_REST = new(76, 175, 80);       // Grün
    private static readonly SKColor COLOR_BOSS = new(156, 39, 176);      // Lila
    private static readonly SKColor COLOR_COMPLETED = new(100, 100, 100); // Dunkelgrau
    private static readonly SKColor COLOR_REACHABLE_GLOW = new(255, 255, 255, 60); // Weißer Glow
    private static readonly SKColor COLOR_LINE_DASHED = new(100, 100, 100, 120);
    private static readonly SKColor COLOR_LINE_REACHABLE = new(200, 200, 200, 180);
    private static readonly SKColor COLOR_LINE_COMPLETED = new(255, 215, 0, 200);

    /// <summary>
    /// Berechnet die Gesamthöhe der Map für Scroll-Berechnung.
    /// </summary>
    public static float GetMapHeight(int rowCount)
    {
        return TOP_PADDING + BOTTOM_PADDING + (rowCount - 1) * ROW_SPACING + BOSS_NODE_RADIUS * 2;
    }

    /// <summary>
    /// Rendert die komplette Dungeon Node-Map.
    /// </summary>
    /// <param name="canvas">SKCanvas zum Zeichnen</param>
    /// <param name="width">Verfügbare Breite</param>
    /// <param name="height">Verfügbare Höhe</param>
    /// <param name="mapData">Map-Daten (Rows, ChosenColumns)</param>
    /// <param name="currentFloor">Aktueller Floor des Spielers (1-basiert)</param>
    /// <param name="time">Animations-Zeit in Sekunden</param>
    /// <param name="scrollOffset">Vertikaler Scroll-Offset (0 = oben)</param>
    public static void Render(SKCanvas canvas, float width, float height,
        DungeonMapData mapData, int currentFloor, float time, float scrollOffset = 0)
    {
        if (mapData.Rows.Count == 0) return;

        canvas.Save();
        canvas.Translate(0, -scrollOffset);

        float centerX = width / 2f;
        int totalRows = mapData.Rows.Count;

        // Von unten nach oben zeichnen (Floor 1 = unten, Floor 10 = oben)
        // → Y-Koordinate: höherer Floor = kleineres Y

        // Verbindungslinien zuerst (unter den Nodes)
        for (int rowIdx = 0; rowIdx < totalRows - 1; rowIdx++)
        {
            var row = mapData.Rows[rowIdx];
            var nextRow = mapData.Rows[rowIdx + 1];

            foreach (var node in row)
            {
                float nodeX = GetNodeX(node.Column, row.Count, centerX);
                float nodeY = GetNodeY(rowIdx, totalRows);

                foreach (int connectedCol in node.ConnectsTo)
                {
                    var targetNode = nextRow.Find(n => n.Column == connectedCol);
                    if (targetNode == null) continue;

                    float targetX = GetNodeX(targetNode.Column, nextRow.Count, centerX);
                    float targetY = GetNodeY(rowIdx + 1, totalRows);

                    // Linien-Stil basierend auf Zustand
                    if (node.IsCompleted && targetNode.IsCompleted)
                    {
                        // Abgeschlossener Pfad: Gold, durchgezogen
                        _completedLinePaint.Color = COLOR_LINE_COMPLETED;
                        canvas.DrawLine(nodeX, nodeY, targetX, targetY, _completedLinePaint);
                    }
                    else if (targetNode.IsReachable)
                    {
                        // Erreichbar: Weiß, durchgezogen
                        _linePaint.Color = COLOR_LINE_REACHABLE;
                        _linePaint.PathEffect = null;
                        canvas.DrawLine(nodeX, nodeY, targetX, targetY, _linePaint);
                    }
                    else
                    {
                        // Unerreicht: Grau, gestrichelt
                        _linePaint.Color = COLOR_LINE_DASHED;
                        _linePaint.PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0);
                        canvas.DrawLine(nodeX, nodeY, targetX, targetY, _linePaint);
                        _linePaint.PathEffect = null;
                    }
                }
            }
        }

        // Nodes zeichnen
        for (int rowIdx = 0; rowIdx < totalRows; rowIdx++)
        {
            var row = mapData.Rows[rowIdx];
            int floor = rowIdx + 1;

            // Floor-Label links
            float labelY = GetNodeY(rowIdx, totalRows);
            _floorLabelPaint.Color = new SKColor(200, 200, 200, 120);
            canvas.DrawText($"F{floor}", 28f, labelY + 4f, _floorLabelPaint);

            foreach (var node in row)
            {
                float nodeX = GetNodeX(node.Column, row.Count, centerX);
                float nodeY = GetNodeY(rowIdx, totalRows);

                bool isBoss = DungeonBuffCatalog.IsBossFloor(floor);
                float radius = isBoss ? BOSS_NODE_RADIUS : NODE_RADIUS;

                // Node-Farbe bestimmen
                SKColor nodeColor;
                if (node.IsCompleted)
                {
                    nodeColor = COLOR_COMPLETED;
                }
                else if (isBoss)
                {
                    nodeColor = COLOR_BOSS;
                }
                else
                {
                    nodeColor = GetRoomTypeColor(node.RoomType);
                }

                // Glow-Effekt für erreichbare und aktuelle Nodes
                if (node.IsReachable && !node.IsCompleted)
                {
                    float pulse = 1f + 0.15f * MathF.Sin(time * 3f);
                    _glowPaint.Color = nodeColor.WithAlpha(50);
                    _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, GLOW_RADIUS * pulse);
                    canvas.DrawCircle(nodeX, nodeY, radius + 4f, _glowPaint);
                    _glowPaint.MaskFilter = null;
                }

                // Pulsierender Ring für aktuellen Node
                if (node.IsCurrent)
                {
                    float pulse = 1f + 0.2f * MathF.Sin(time * 4f);
                    _nodeBorderPaint.Color = new SKColor(255, 255, 255, (byte)(180 + 75 * MathF.Sin(time * 4f)));
                    _nodeBorderPaint.StrokeWidth = 3f * pulse;
                    canvas.DrawCircle(nodeX, nodeY, radius + 3f, _nodeBorderPaint);
                }

                // Node-Hintergrund
                _nodePaint.Color = nodeColor;
                canvas.DrawCircle(nodeX, nodeY, radius, _nodePaint);

                // Node-Border
                _nodeBorderPaint.Color = node.IsCompleted
                    ? new SKColor(80, 80, 80)
                    : nodeColor.WithAlpha(200);
                _nodeBorderPaint.StrokeWidth = 2f;
                canvas.DrawCircle(nodeX, nodeY, radius, _nodeBorderPaint);

                // Raum-Typ Icon/Symbol im Node
                _textPaint.Color = node.IsCompleted ? new SKColor(60, 60, 60) : SKColors.White;
                _textPaint.TextSize = isBoss ? 14f : 11f;
                _textPaint.FakeBoldText = isBoss;
                string symbol = GetRoomTypeSymbol(node.RoomType, isBoss);
                canvas.DrawText(symbol, nodeX, nodeY + 4f, _textPaint);
                _textPaint.FakeBoldText = false;

                // Modifikator-Badge (kleiner Punkt rechts-oben)
                if (node.Modifier != DungeonFloorModifier.None && !node.IsCompleted)
                {
                    float badgeX = nodeX + radius * 0.7f;
                    float badgeY = nodeY - radius * 0.7f;
                    _modifierBadgePaint.Color = GetModifierColor(node.Modifier);
                    canvas.DrawCircle(badgeX, badgeY, 4f, _modifierBadgePaint);
                }

                // Abgeschlossen-Häkchen
                if (node.IsCompleted)
                {
                    _textPaint.Color = new SKColor(76, 175, 80);
                    _textPaint.TextSize = 12f;
                    canvas.DrawText("\u2713", nodeX, nodeY + 4f, _textPaint);
                }

                // Raum-Typ Label unter dem Node (nur für erreichbare/aktuelle, nicht abgeschlossene)
                if ((node.IsReachable || node.IsCurrent) && !node.IsCompleted)
                {
                    _labelPaint.Color = nodeColor.WithAlpha(180);
                    string label = GetRoomTypeLabel(node.RoomType, isBoss);
                    canvas.DrawText(label, nodeX, nodeY + radius + 12f, _labelPaint);
                }
            }
        }

        canvas.Restore();
    }

    // === Position-Berechnung ===

    private static float GetNodeX(int column, int nodeCount, float centerX)
    {
        if (nodeCount == 1) return centerX;

        // Nodes gleichmäßig um die Mitte verteilen
        float totalWidth = (nodeCount - 1) * COL_SPACING;
        float startX = centerX - totalWidth / 2f;
        return startX + column * COL_SPACING;
    }

    private static float GetNodeY(int rowIndex, int totalRows)
    {
        // Floor 1 = unten (großes Y), Floor 10 = oben (kleines Y)
        float mapHeight = TOP_PADDING + (totalRows - 1) * ROW_SPACING;
        return mapHeight - rowIndex * ROW_SPACING + TOP_PADDING;
    }

    // === Farb-/Symbol-Helfer ===

    private static SKColor GetRoomTypeColor(DungeonRoomType type) => type switch
    {
        DungeonRoomType.Elite => COLOR_ELITE,
        DungeonRoomType.Treasure => COLOR_TREASURE,
        DungeonRoomType.Challenge => COLOR_CHALLENGE,
        DungeonRoomType.Rest => COLOR_REST,
        _ => COLOR_NORMAL
    };

    private static string GetRoomTypeSymbol(DungeonRoomType type, bool isBoss)
    {
        if (isBoss) return "\u265A"; // Krone Unicode
        return type switch
        {
            DungeonRoomType.Elite => "\u2620",     // Totenkopf
            DungeonRoomType.Treasure => "\u2B50",  // Stern (Truhe)
            DungeonRoomType.Challenge => "!",
            DungeonRoomType.Rest => "\u2665",      // Herz
            _ => "\u2694"                           // Schwerter
        };
    }

    private static string GetRoomTypeLabel(DungeonRoomType type, bool isBoss)
    {
        if (isBoss) return "BOSS";
        return type switch
        {
            DungeonRoomType.Elite => "ELITE",
            DungeonRoomType.Treasure => "LOOT",
            DungeonRoomType.Challenge => "TRIAL",
            DungeonRoomType.Rest => "REST",
            _ => ""
        };
    }

    /// <summary>
    /// Hit-Test: Ermittelt welcher Node an der gegebenen Position liegt.
    /// Gibt null zurück wenn kein Node getroffen wurde.
    /// </summary>
    /// <param name="tapX">X-Koordinate des Taps (in Canvas-Koordinaten)</param>
    /// <param name="tapY">Y-Koordinate des Taps (in Canvas-Koordinaten, ohne Scroll-Offset)</param>
    /// <param name="mapData">Map-Daten</param>
    /// <param name="canvasWidth">Breite des Canvas</param>
    /// <param name="hitRadiusMultiplier">Vergrößerungsfaktor für Touch-Toleranz (Standard: 1.5)</param>
    public static DungeonMapNode? HitTestNode(float tapX, float tapY,
        DungeonMapData mapData, float canvasWidth, float hitRadiusMultiplier = 1.5f)
    {
        if (mapData.Rows.Count == 0) return null;

        float centerX = canvasWidth / 2f;
        int totalRows = mapData.Rows.Count;

        for (int rowIdx = 0; rowIdx < totalRows; rowIdx++)
        {
            var row = mapData.Rows[rowIdx];
            int floor = rowIdx + 1;
            bool isBoss = DungeonBuffCatalog.IsBossFloor(floor);
            float radius = (isBoss ? BOSS_NODE_RADIUS : NODE_RADIUS) * hitRadiusMultiplier;

            foreach (var node in row)
            {
                float nodeX = GetNodeX(node.Column, row.Count, centerX);
                float nodeY = GetNodeY(rowIdx, totalRows);

                float dx = tapX - nodeX;
                float dy = tapY - nodeY;

                if (dx * dx + dy * dy <= radius * radius)
                    return node;
            }
        }

        return null;
    }

    private static SKColor GetModifierColor(DungeonFloorModifier mod) => mod switch
    {
        DungeonFloorModifier.LavaBorders => new SKColor(255, 87, 34),
        DungeonFloorModifier.Darkness => new SKColor(120, 120, 160),
        DungeonFloorModifier.DoubleSpawns => new SKColor(244, 67, 54),
        DungeonFloorModifier.FastBombs => new SKColor(255, 193, 7),
        DungeonFloorModifier.BigExplosions => new SKColor(255, 152, 0),
        DungeonFloorModifier.Regeneration => new SKColor(76, 175, 80),
        DungeonFloorModifier.Wealthy => new SKColor(255, 215, 0),
        _ => new SKColor(150, 150, 150)
    };
}
