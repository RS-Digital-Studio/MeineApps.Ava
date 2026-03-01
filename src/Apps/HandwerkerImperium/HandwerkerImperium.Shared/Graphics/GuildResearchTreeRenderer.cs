using HandwerkerImperium.Models;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert den Gilden-Forschungsbaum als 2D-Netzwerk.
/// 18 Items in 6 Kategorien, festes Baum-Layout:
///
///      [Infra 1]
///          ↓
///      [Infra 2]
///          ↓
///      [Infra 3]
///     ╱         ╲
/// [Wirt 1]   [Wiss 1]
///     ↓          ↓
/// [Wirt 2]   [Wiss 2]
///     ↓          ↓
/// [Wirt 3]   [Wiss 3]
///     ↓          ╱
/// [Wirt 4]──╱
///     ╲ ╱
///  [Logistik 1]
///       ↓
///  [Logistik 2]
///       ↓
///  [Logistik 3]
///     ╱         ╲
/// [Arbeit 1] [Meister 1]
///     ↓          ↓
/// [Arbeit 2] [Meister 2]
///     ↓
/// [Arbeit 3]
/// </summary>
public class GuildResearchTreeRenderer
{
    private float _time;

    // Layout-Konstanten
    private const float NodeSize = 68;
    private const float RowHeight = 110;
    private const float ProgressBarHeight = 7;
    private const float TopPadding = 24;
    private const int TotalRows = 13;

    // Fließende Partikel entlang erforschter Verbindungen
    private readonly List<FlowParticle> _flowParticles = [];
    private float _particleTimer;

    // Farben pro Kategorie
    private static readonly SKColor InfraColor = new(0x3B, 0x82, 0xF6);   // Blau
    private static readonly SKColor EconomyColor = new(0x10, 0xB9, 0x81);  // Grün
    private static readonly SKColor KnowledgeColor = new(0x8B, 0x5C, 0xF6); // Violett
    private static readonly SKColor LogisticsColor = new(0xF5, 0x9E, 0x0B); // Amber
    private static readonly SKColor WorkforceColor = new(0xEF, 0x44, 0x44); // Rot
    private static readonly SKColor MasteryColor = new(0xFF, 0xD7, 0x00);   // Gold

    private static readonly SKColor LineLocked = new(0x5A, 0x48, 0x38);
    private static readonly SKColor TextPrimary = new(0xF5, 0xF0, 0xEB);
    private static readonly SKColor TextMuted = new(0x7A, 0x68, 0x58);
    private static readonly SKColor ProgressBg = new(0x30, 0x24, 0x1A);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _text = new() { IsAntialias = true };

    // Gecachte Font- und Path-Objekte (vermeidet Allokationen pro Frame)
    private readonly SKFont _percentFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKPath _connectionPath = new();
    private readonly SKPath _arrowPath = new();
    private readonly SKPath _arcPath = new();

    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8);
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, MaskFilter = _glowFilter };

    // ═══════════════════════════════════════════════════════════════════════
    // LAYOUT-MAP: 18 Forschungen → (Zeile, Spalte) Positionen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Feste Zuordnung: Index in GetAll()-Liste → (row, column).
    /// Column: 0 = links, 1 = mitte, 2 = rechts.
    /// </summary>
    private static readonly (int row, int col)[] NodeLayout =
    [
        // Infrastruktur 1-3 (Indices 0-2)
        (0, 1), (1, 1), (2, 1),
        // Wirtschaft 1-4 (Indices 3-6)
        (3, 0), (4, 0), (5, 0), (6, 0),
        // Wissen 1-3 (Indices 7-9)
        (3, 2), (4, 2), (5, 2),
        // Logistik 1-3 (Indices 10-12)
        (7, 1), (8, 1), (9, 1),
        // Arbeitsmarkt 1-3 (Indices 13-15)
        (10, 0), (11, 0), (12, 0),
        // Meisterschaft 1-2 (Indices 16-17)
        (10, 2), (11, 2)
    ];

    /// <summary>
    /// Verbindungen zwischen Nodes (von-Index → nach-Index).
    /// </summary>
    private static readonly (int from, int to)[] Connections =
    [
        // Infrastruktur linear
        (0, 1), (1, 2),
        // Infrastruktur 3 verzweigt
        (2, 3), (2, 7),
        // Wirtschaft linear
        (3, 4), (4, 5), (5, 6),
        // Wissen linear
        (7, 8), (8, 9),
        // Wirtschaft 4 + Wissen 3 → Logistik 1 (Zusammenführung)
        (6, 10), (9, 10),
        // Logistik linear
        (10, 11), (11, 12),
        // Logistik 3 verzweigt
        (12, 13), (12, 16),
        // Arbeitsmarkt linear
        (13, 14), (14, 15),
        // Meisterschaft linear
        (16, 17)
    ];

    /// <summary>
    /// Gesamthöhe des Baums in Pixeln.
    /// </summary>
    public static float CalculateTotalHeight() =>
        TopPadding + TotalRows * RowHeight + 40;

    /// <summary>
    /// Rendert den gesamten Gilden-Forschungsbaum.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, List<GuildResearchDisplay> items, float deltaTime)
    {
        _time += deltaTime;
        if (items.Count == 0) return;

        float centerX = bounds.MidX;

        // Positionen berechnen
        var positions = CalculatePositions(centerX, bounds.Top + TopPadding);
        if (positions.Count != items.Count) return; // Sicherheitscheck

        // 1. Verbindungslinien (hinter den Nodes)
        DrawConnections(canvas, items, positions);

        // 2. Fließende Partikel
        UpdateAndDrawFlowParticles(canvas, items, positions, deltaTime);

        // 3. Nodes
        for (int i = 0; i < items.Count && i < positions.Count; i++)
        {
            DrawNode(canvas, items[i], positions[i], GetCategoryColor(items[i].Category), i);
        }
    }

    /// <summary>
    /// HitTest: Gibt den Index des getroffenen Nodes zurück (-1 wenn keiner).
    /// </summary>
    public int HitTest(float tapX, float tapY, float centerX, float topY, int itemCount)
    {
        var positions = CalculatePositions(centerX, topY + TopPadding);
        float hitRadius = NodeSize / 2 + 8;

        for (int i = 0; i < Math.Min(positions.Count, itemCount); i++)
        {
            float dx = tapX - positions[i].X;
            float dy = tapY - positions[i].Y;
            if (dx * dx + dy * dy <= hitRadius * hitRadius)
                return i;
        }
        return -1;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POSITIONEN
    // ═══════════════════════════════════════════════════════════════════════

    private static List<SKPoint> CalculatePositions(float centerX, float startY)
    {
        var positions = new List<SKPoint>(18);
        float spread = NodeSize * 1.6f; // Horizontaler Abstand links/rechts

        for (int i = 0; i < NodeLayout.Length; i++)
        {
            var (row, col) = NodeLayout[i];
            float x = col switch
            {
                0 => centerX - spread,
                2 => centerX + spread,
                _ => centerX
            };
            float y = startY + row * RowHeight;
            positions.Add(new SKPoint(x, y));
        }
        return positions;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VERBINDUNGSLINIEN
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawConnections(SKCanvas canvas, List<GuildResearchDisplay> items, List<SKPoint> positions)
    {
        foreach (var (from, to) in Connections)
        {
            if (from >= items.Count || to >= items.Count) continue;
            if (from >= positions.Count || to >= positions.Count) continue;

            var fromPos = positions[from];
            var toPos = positions[to];
            var fromItem = items[from];
            var toItem = items[to];

            bool fromDone = fromItem.IsCompleted;
            bool toDone = toItem.IsCompleted;
            bool toActive = toItem.IsActive;
            bool toLocked = toItem.IsLocked;

            var lineColor = GetCategoryColor(toItem.Category);

            float startY = fromPos.Y + NodeSize / 2 + 6;
            float endY = toPos.Y - NodeSize / 2 - 6;

            // Bezier-Kurve (gecachter Path)
            _connectionPath.Reset();
            _connectionPath.MoveTo(fromPos.X, startY);
            float midY = (startY + endY) / 2;
            _connectionPath.CubicTo(fromPos.X, midY, toPos.X, midY, toPos.X, endY);

            if (fromDone && (toDone || toActive))
            {
                // Erforschte Verbindung: Farbig mit Glow
                _stroke.Color = lineColor.WithAlpha(35);
                _stroke.StrokeWidth = 8f;
                _stroke.PathEffect = null;
                canvas.DrawPath(_connectionPath, _stroke);

                _stroke.Color = lineColor.WithAlpha(toDone ? (byte)200 : (byte)150);
                _stroke.StrokeWidth = 3.5f;
                canvas.DrawPath(_connectionPath, _stroke);

                DrawArrowHead(canvas, toPos.X, endY, lineColor.WithAlpha(200));
            }
            else if (fromDone && toLocked)
            {
                // Nächstes verfügbar: Gestrichelt, pulsierend
                _stroke.Color = lineColor.WithAlpha(90);
                _stroke.StrokeWidth = 2.5f;
                using var dash = SKPathEffect.CreateDash([6, 4], _time * 12 % 10);
                _stroke.PathEffect = dash;
                canvas.DrawPath(_connectionPath, _stroke);
                _stroke.PathEffect = null;

                DrawArrowHead(canvas, toPos.X, endY, lineColor.WithAlpha(80));
            }
            else
            {
                // Gesperrt: Dezentes Grau
                _stroke.Color = LineLocked;
                _stroke.StrokeWidth = 2f;
                _stroke.PathEffect = null;
                canvas.DrawPath(_connectionPath, _stroke);
            }
        }
    }

    private void DrawArrowHead(SKCanvas canvas, float x, float y, SKColor color)
    {
        _fill.Color = color;
        _arrowPath.Reset();
        _arrowPath.MoveTo(x, y);
        _arrowPath.LineTo(x - 6, y - 9);
        _arrowPath.LineTo(x + 6, y - 9);
        _arrowPath.Close();
        canvas.DrawPath(_arrowPath, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NODES
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawNode(SKCanvas canvas, GuildResearchDisplay item, SKPoint pos,
        SKColor catColor, int index)
    {
        float cx = pos.X;
        float cy = pos.Y;

        // Glow für abgeschlossene Nodes
        if (item.IsCompleted)
        {
            float glow = 0.6f + MathF.Sin(_time * 1.5f) * 0.25f;
            _fill.Color = catColor.WithAlpha((byte)(glow * 25));
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 12, _fill);
        }

        // Hintergrundkreis
        if (item.IsLocked)
        {
            _fill.Color = new SKColor(0x28, 0x20, 0x18, 160);
            canvas.DrawCircle(cx, cy, NodeSize / 2, _fill);

            // Gepunkteter Rahmen
            _stroke.Color = LineLocked.WithAlpha(100);
            _stroke.StrokeWidth = 1.5f;
            using var dotEffect = SKPathEffect.CreateDash([3, 3], _time * 4 % 6);
            _stroke.PathEffect = dotEffect;
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 2, _stroke);
            _stroke.PathEffect = null;
        }
        else if (item.IsCompleted)
        {
            // Voller Kreis in Kategorie-Farbe
            _fill.Color = catColor.WithAlpha(40);
            canvas.DrawCircle(cx, cy, NodeSize / 2, _fill);
            _stroke.Color = catColor.WithAlpha(180);
            _stroke.StrokeWidth = 2.5f;
            _stroke.PathEffect = null;
            canvas.DrawCircle(cx, cy, NodeSize / 2, _stroke);
        }
        else
        {
            // Aktiv: Dunkler Hintergrund mit Kategorie-Rand
            _fill.Color = new SKColor(0x20, 0x18, 0x10, 200);
            canvas.DrawCircle(cx, cy, NodeSize / 2, _fill);
            _stroke.Color = catColor.WithAlpha(140);
            _stroke.StrokeWidth = 2f;
            _stroke.PathEffect = null;
            canvas.DrawCircle(cx, cy, NodeSize / 2, _stroke);
        }

        // Icon in der Mitte
        float iconSize = NodeSize * 0.55f;
        byte iconAlpha = item.IsLocked ? (byte)80 : (byte)220;
        _fill.Color = item.IsLocked ? LineLocked.WithAlpha(iconAlpha) : catColor.WithAlpha(iconAlpha);
        _stroke.Color = _fill.Color;
        _stroke.StrokeWidth = 1.5f;
        GuildResearchIconRenderer.DrawIcon(canvas, cx, cy, iconSize, item.Category, _fill, _stroke);

        // Tier-Indikator (I, II, III, IV Punkte)
        int tier = GetTierInCategory(index);
        if (tier > 0)
        {
            _fill.Color = item.IsLocked ? LineLocked.WithAlpha(60) : catColor.WithAlpha(160);
            GuildResearchIconRenderer.DrawTierIndicator(canvas, cx, cy + NodeSize / 2 - 8, NodeSize, tier, _fill);
        }

        // Fortschrittsring (nur aktiv, nicht abgeschlossen und nicht gesperrt)
        if (item.IsActive && !item.IsCompleted)
        {
            DrawProgressRing(canvas, cx, cy, NodeSize / 2 + 5, (float)item.ProgressPercent, catColor);
        }

        // Häkchen bei abgeschlossen
        if (item.IsCompleted)
        {
            DrawCheckmark(canvas, cx + NodeSize / 2 - 4, cy - NodeSize / 2 + 4, catColor);
        }

        // Fortschrittsbalken unter dem Icon
        float barY = cy + NodeSize / 2 + 6;
        float barW = NodeSize * 1.0f;
        DrawProgressBar(canvas, cx - barW / 2, barY, barW, ProgressBarHeight, item, catColor);

        // Gold-Glow auf nächstem freischaltbaren Node
        if (item.IsActive && !item.IsCompleted)
        {
            float pulse = 0.4f + MathF.Sin(_time * 3f) * 0.3f;
            _glowPaint.Color = catColor.WithAlpha((byte)(pulse * 60));
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 6, _glowPaint);

            // Pulsierender Ring
            _stroke.Color = catColor.WithAlpha((byte)(pulse * 160));
            _stroke.StrokeWidth = 2f;
            _stroke.PathEffect = null;
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 4 + pulse * 3, _stroke);
        }
    }

    /// <summary>
    /// Kreisförmiger Fortschrittsring (Arc von 0 bis 360 Grad).
    /// </summary>
    private void DrawProgressRing(SKCanvas canvas, float cx, float cy, float radius,
        float progress, SKColor color)
    {
        // Hintergrund-Ring
        _stroke.Color = ProgressBg;
        _stroke.StrokeWidth = 3f;
        _stroke.PathEffect = null;
        canvas.DrawCircle(cx, cy, radius, _stroke);

        // Fortschritts-Arc (gecachter Path)
        float sweepAngle = progress * 360f;
        if (sweepAngle > 0.5f)
        {
            _stroke.Color = color.WithAlpha(200);
            _stroke.StrokeWidth = 3f;
            _stroke.StrokeCap = SKStrokeCap.Round;
            var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
            _arcPath.Reset();
            _arcPath.AddArc(arcRect, -90, sweepAngle);
            canvas.DrawPath(_arcPath, _stroke);
        }
    }

    /// <summary>
    /// Grünes Häkchen-Badge.
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, float cx, float cy, SKColor color)
    {
        // Grüner Kreis
        _fill.Color = new SKColor(0x4C, 0xAF, 0x50);
        canvas.DrawCircle(cx, cy, 8, _fill);

        // Häkchen
        _stroke.Color = SKColors.White;
        _stroke.StrokeWidth = 2f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        _stroke.PathEffect = null;
        canvas.DrawLine(cx - 3.5f, cy, cx - 1, cy + 3, _stroke);
        canvas.DrawLine(cx - 1, cy + 3, cx + 4, cy - 3.5f, _stroke);
    }

    /// <summary>
    /// Linearer Fortschrittsbalken unter dem Node.
    /// </summary>
    private void DrawProgressBar(SKCanvas canvas, float x, float y, float w, float h,
        GuildResearchDisplay item, SKColor color)
    {
        // Hintergrund
        _fill.Color = ProgressBg;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + w, y + h), 3), _fill);

        float progress = item.IsCompleted ? 1f : (float)item.ProgressPercent;
        float fillW = w * Math.Clamp(progress, 0, 1);

        if (fillW > 1)
        {
            var fillRect = new SKRect(x, y, x + fillW, y + h);
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(x, y), new SKPoint(x + fillW, y),
                [color.WithAlpha(160), color],
                SKShaderTileMode.Clamp);
            _fill.Shader = shader;
            canvas.DrawRoundRect(new SKRoundRect(fillRect, 3), _fill);
            _fill.Shader = null;

            // Glanz oben
            _fill.Color = SKColors.White.WithAlpha(35);
            canvas.DrawRect(x + 1, y, fillW - 2, h * 0.4f, _fill);
        }

        // Prozent-Text
        if (item.IsActive || item.IsCompleted)
        {
            string pct = $"{(int)(progress * 100)}%";
            _percentFont.Size = 8;
            _text.Color = SKColors.White.WithAlpha(200);
            canvas.DrawText(pct, x + w / 2, y + h - 0.5f, SKTextAlign.Center, _percentFont, _text);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FLIEßENDE PARTIKEL
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawFlowParticles(SKCanvas canvas, List<GuildResearchDisplay> items,
        List<SKPoint> positions, float deltaTime)
    {
        _particleTimer += deltaTime;

        // Neue Partikel spawnen auf abgeschlossenen Verbindungen
        if (_particleTimer >= 0.5f && _flowParticles.Count < 15)
        {
            _particleTimer = 0;
            foreach (var (from, to) in Connections)
            {
                if (from >= items.Count || to >= items.Count) continue;
                if (!items[from].IsCompleted) continue;
                if (!items[to].IsCompleted && !items[to].IsActive) continue;
                if (Random.Shared.NextSingle() > 0.25f) continue;

                _flowParticles.Add(new FlowParticle
                {
                    StartX = positions[from].X,
                    StartY = positions[from].Y + NodeSize / 2 + 6,
                    EndX = positions[to].X,
                    EndY = positions[to].Y - NodeSize / 2 - 6,
                    Progress = 0,
                    Life = 1.2f
                });
                break; // Nur 1 pro Tick
            }
        }

        // Partikel aktualisieren und zeichnen
        for (int i = _flowParticles.Count - 1; i >= 0; i--)
        {
            var p = _flowParticles[i];
            p.Progress += deltaTime * 1.2f;
            p.Life -= deltaTime;

            if (p.Progress > 1 || p.Life <= 0)
            {
                _flowParticles.RemoveAt(i);
                continue;
            }

            // Bezier-Position interpolieren
            float t = p.Progress;
            float midY = (p.StartY + p.EndY) / 2;
            float px = Bezier(p.StartX, p.StartX, p.EndX, p.EndX, t);
            float py = Bezier(p.StartY, midY, midY, p.EndY, t);

            byte alpha = (byte)(200 * Math.Min(p.Life, 0.5f) * 2);
            _fill.Color = new SKColor(0xFF, 0xD7, 0x00, alpha);
            canvas.DrawCircle(px, py, 3.5f, _fill);

            // Kleiner Glow
            _fill.Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(alpha / 4));
            canvas.DrawCircle(px, py, 7, _fill);

            _flowParticles[i] = p;
        }
    }

    /// <summary>
    /// Kubische Bezier-Interpolation.
    /// </summary>
    private static float Bezier(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die Kategorie-Farbe zurück (angepasste Palette für den Baum).
    /// </summary>
    private static SKColor GetCategoryColor(GuildResearchCategory category) => category switch
    {
        GuildResearchCategory.Infrastructure => InfraColor,
        GuildResearchCategory.Economy => EconomyColor,
        GuildResearchCategory.Knowledge => KnowledgeColor,
        GuildResearchCategory.Logistics => LogisticsColor,
        GuildResearchCategory.Workforce => WorkforceColor,
        GuildResearchCategory.Mastery => MasteryColor,
        _ => LineLocked
    };

    /// <summary>
    /// Gibt die Stufe innerhalb der Kategorie zurück (1-basiert).
    /// Index 0-2: Infra 1-3, 3-6: Wirtschaft 1-4, 7-9: Wissen 1-3,
    /// 10-12: Logistik 1-3, 13-15: Arbeitsmarkt 1-3, 16-17: Meisterschaft 1-2.
    /// </summary>
    private static int GetTierInCategory(int index) => index switch
    {
        0 => 1, 1 => 2, 2 => 3,               // Infrastruktur
        3 => 1, 4 => 2, 5 => 3, 6 => 4,       // Wirtschaft
        7 => 1, 8 => 2, 9 => 3,                // Wissen
        10 => 1, 11 => 2, 12 => 3,             // Logistik
        13 => 1, 14 => 2, 15 => 3,             // Arbeitsmarkt
        16 => 1, 17 => 2,                       // Meisterschaft
        _ => 0
    };

    // Partikel-Struct (gleich wie in ResearchTreeRenderer)
    private class FlowParticle
    {
        public float StartX, StartY, EndX, EndY;
        public float Progress;
        public float Life;
    }
}
