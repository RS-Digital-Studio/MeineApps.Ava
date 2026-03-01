using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert den gesamten Forschungsbaum einer Branch als 2D-Baum-Netzwerk.
/// Layout im Stil von "Top Heroes": Große Icons in einem verzweigten Grid,
/// verbunden durch geschwungene Pfeile, mit Fortschrittsbalken und Prozentanzeige.
///
/// Layout-Schema (15 Items pro Branch, 3 Spalten):
///   [1]           → Zeile 0: Startforschung (zentriert)
///    ↓
///  [2] [3]        → Zeile 1: Verzweigung (2 nebeneinander)
///    ↘ ↙
///   [4]           → Zeile 2: Zusammenführung (zentriert)
///    ↓
///  [5] [6]        → Zeile 3: Verzweigung
///    ↘ ↙
///   [7]           → Zeile 4: Zusammenführung
///    ↓
///  [8] [9]        → Zeile 5: Verzweigung
///    ↘ ↙
///  [10]           → Zeile 6: Zusammenführung
///    ↓
/// [11] [12]       → Zeile 7: Verzweigung
///    ↘ ↙
///  [13]           → Zeile 8: Zusammenführung
///    ↓
/// [14] [15]       → Zeile 9: Letzte Verzweigung (Meisterforschungen)
/// </summary>
public class ResearchTreeRenderer
{
    private float _time;

    // Layout
    private const float NodeSize = 72;        // Icon-Größe (72dp für bessere Touch-Targets)
    private const float RowHeight = 120;       // Vertikaler Abstand zwischen Zeilen
    private const float ProgressBarHeight = 8; // Höhe des Fortschrittsbalkens unter jedem Icon
    private const float TextHeight = 18;       // Höhe für Name + Prozent
    private const float TopPadding = 30;

    // Linien-Partikel (Fixed-Size struct-Pool, 0 GC)
    private const int MaxFlowParticles = 25;
    private readonly FlowParticle[] _flowParticles = new FlowParticle[MaxFlowParticles];
    private int _flowParticleCount;
    private float _particleTimer;

    // Farben
    private static readonly SKColor LineLocked = new(0x6A, 0x58, 0x48);
    private static readonly SKColor TextPrimary = new(0xF5, 0xF0, 0xEB);
    private static readonly SKColor TextSecondary = new(0xA0, 0x90, 0x80);
    private static readonly SKColor TextMuted = new(0x8A, 0x78, 0x68);
    private static readonly SKColor ProgressBg = new(0x38, 0x2C, 0x22);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _text = new() { IsAntialias = true };

    // Gecachte Font- und Path-Objekte (vermeidet Allokationen pro Frame)
    private readonly SKFont _fontSmall = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _fontMedium = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _fontBold = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKPath _connectionPath = new();
    private readonly SKPath _arrowPath = new();

    // Gecachter MaskFilter + Paint für Gold-Glow auf verfügbaren Nodes (statt pro Node neu erstellen)
    private static readonly SKMaskFilter _glowMaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8);
    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        MaskFilter = _glowMaskFilter
    };

    /// <summary>
    /// Berechnet die Gesamthöhe des Baums.
    /// </summary>
    public static float CalculateTotalHeight(int itemCount)
    {
        int rowCount = GetRowCount(itemCount);
        return TopPadding + rowCount * RowHeight + 20;
    }

    /// <summary>
    /// Rendert den gesamten Forschungsbaum als 2D-Netzwerk.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, List<ResearchDisplayItem> items,
        ResearchBranch branch, float deltaTime)
    {
        _time += deltaTime;
        if (items.Count == 0) return;

        var branchColor = ResearchItemRenderer.GetBranchColor(branch);
        float centerX = bounds.MidX;

        // Node-Positionen berechnen
        var positions = CalculateNodePositions(items, centerX, bounds.Top + TopPadding);

        // 1. Verbindungslinien zeichnen (hinter den Nodes)
        DrawConnections(canvas, items, positions, branch, branchColor);

        // 2. Fließende Partikel auf erforschten Verbindungen
        UpdateAndDrawFlowParticles(canvas, items, positions, branchColor, deltaTime);

        // 3. Nodes zeichnen (Icons + Fortschritt + Name)
        for (int i = 0; i < items.Count && i < positions.Count; i++)
        {
            DrawNode(canvas, items[i], positions[i], branch, branchColor);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NODE-POSITIONEN (2D-Baum-Layout)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet die (x,y)-Position für jeden Node im Baum.
    /// Alternierendes Layout: Zentriert → 2er-Reihe → Zentriert → 2er-Reihe...
    /// </summary>
    private static List<SKPoint> CalculateNodePositions(List<ResearchDisplayItem> items,
        float centerX, float startY)
    {
        var positions = new List<SKPoint>();
        float horizontalSpread = NodeSize * 1.5f; // Abstand zwischen 2er-Reihen

        int itemIndex = 0;
        int row = 0;

        while (itemIndex < items.Count)
        {
            float rowY = startY + row * RowHeight;

            if (row % 2 == 0)
            {
                // Ungerade Zeile: 1 Item zentriert
                positions.Add(new SKPoint(centerX, rowY));
                itemIndex++;
            }
            else
            {
                // Gerade Zeile: 2 Items nebeneinander
                if (itemIndex < items.Count)
                {
                    positions.Add(new SKPoint(centerX - horizontalSpread, rowY));
                    itemIndex++;
                }
                if (itemIndex < items.Count)
                {
                    positions.Add(new SKPoint(centerX + horizontalSpread, rowY));
                    itemIndex++;
                }
            }

            row++;
        }

        return positions;
    }

    private static int GetRowCount(int itemCount)
    {
        // Formel: Zeile 0 = 1 Item, Zeile 1 = 2 Items, Zeile 2 = 1, Zeile 3 = 2, ...
        // 15 Items: 1+2+1+2+1+2+1+2+1+2 = 10 Zeilen
        int count = 0;
        int row = 0;
        while (count < itemCount)
        {
            count += (row % 2 == 0) ? 1 : 2;
            row++;
        }
        return row;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VERBINDUNGSLINIEN
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawConnections(SKCanvas canvas, List<ResearchDisplayItem> items,
        List<SKPoint> positions, ResearchBranch branch, SKColor branchColor)
    {
        // Verbindungen basierend auf dem Baum-Layout:
        // Item 0 → 1, 2 (Zentriert → Links, Rechts)
        // Item 1 → 3, Item 2 → 3 (Links, Rechts → Zentriert)
        // Item 3 → 4, 5
        // etc.

        int row = 0;
        var rowItems = new List<List<int>>(); // Items pro Zeile

        // Zuerst: Items pro Zeile zuordnen
        int idx = 0;
        while (idx < items.Count)
        {
            var rowList = new List<int>();
            if (row % 2 == 0)
            {
                if (idx < items.Count) { rowList.Add(idx); idx++; }
            }
            else
            {
                if (idx < items.Count) { rowList.Add(idx); idx++; }
                if (idx < items.Count) { rowList.Add(idx); idx++; }
            }
            rowItems.Add(rowList);
            row++;
        }

        // Verbindungen zwischen aufeinanderfolgenden Zeilen
        for (int r = 0; r < rowItems.Count - 1; r++)
        {
            var currentRow = rowItems[r];
            var nextRow = rowItems[r + 1];

            foreach (int fromIdx in currentRow)
            {
                foreach (int toIdx in nextRow)
                {
                    if (fromIdx >= positions.Count || toIdx >= positions.Count) continue;

                    var from = positions[fromIdx];
                    var to = positions[toIdx];

                    // Status bestimmen
                    bool fromResearched = items[fromIdx].IsResearched;
                    bool toResearched = items[toIdx].IsResearched;
                    bool toLocked = items[toIdx].IsLocked;
                    bool toCanStart = items[toIdx].CanStart;

                    DrawConnectionLine(canvas, from, to, fromResearched, toResearched,
                        toLocked, toCanStart, branchColor);
                }
            }
        }
    }

    private void DrawConnectionLine(SKCanvas canvas, SKPoint from, SKPoint to,
        bool fromResearched, bool toResearched, bool toLocked, bool toCanStart,
        SKColor branchColor)
    {
        float startY = from.Y + NodeSize / 2 + ProgressBarHeight + TextHeight + 4;
        float endY = to.Y - NodeSize / 2 - 4;

        // Bezier-Kurve für geschwungene Verbindung (gecachter Path)
        _connectionPath.Reset();
        _connectionPath.MoveTo(from.X, startY);

        float midY = (startY + endY) / 2;
        _connectionPath.CubicTo(from.X, midY, to.X, midY, to.X, endY);

        if (fromResearched && (toResearched || toCanStart))
        {
            // Erforschte/verfügbare Verbindung: Branch-farbig, leuchtend mit Glow
            // Glow-Linie dahinter (breitere, transparente Linie)
            _stroke.Color = branchColor.WithAlpha(40);
            _stroke.StrokeWidth = 9f;
            _stroke.PathEffect = null;
            canvas.DrawPath(_connectionPath, _stroke);

            // Hauptlinie
            _stroke.Color = branchColor.WithAlpha(toResearched ? (byte)220 : (byte)160);
            _stroke.StrokeWidth = 5f;
            canvas.DrawPath(_connectionPath, _stroke);

            // Pfeilspitze
            DrawArrowHead(canvas, to.X, endY, branchColor.WithAlpha(220));
        }
        else if (fromResearched && toLocked)
        {
            // Nächste gesperrt: Gestrichelt, pulsierend
            _stroke.Color = branchColor.WithAlpha(100);
            _stroke.StrokeWidth = 4f;
            // PathEffect mit dynamischem Phase-Offset (wird sofort nach Verwendung disposed)
            using var dashEffect = SKPathEffect.CreateDash([8, 5], _time * 15 % 13);
            _stroke.PathEffect = dashEffect;
            canvas.DrawPath(_connectionPath, _stroke);
            _stroke.PathEffect = null;

            DrawArrowHead(canvas, to.X, endY, branchColor.WithAlpha(100));
        }
        else
        {
            // Beides gesperrt: Sichtbare graue Linie
            _stroke.Color = LineLocked;
            _stroke.StrokeWidth = 4f;
            _stroke.PathEffect = null;
            canvas.DrawPath(_connectionPath, _stroke);
        }
    }

    private void DrawArrowHead(SKCanvas canvas, float x, float y, SKColor color)
    {
        float size = 8;
        _fill.Color = color;
        _arrowPath.Reset();
        _arrowPath.MoveTo(x, y);
        _arrowPath.LineTo(x - size, y - size * 1.5f);
        _arrowPath.LineTo(x + size, y - size * 1.5f);
        _arrowPath.Close();
        canvas.DrawPath(_arrowPath, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NODES
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawNode(SKCanvas canvas, ResearchDisplayItem item, SKPoint pos,
        ResearchBranch branch, SKColor branchColor)
    {
        float cx = pos.X;
        float cy = pos.Y;

        // Glow-Effekt für erforschte Nodes (Branch-farbig)
        if (item.IsResearched)
        {
            float glowPulse = 0.7f + MathF.Sin(_time * 1.5f) * 0.3f;
            _fill.Color = branchColor.WithAlpha((byte)(glowPulse * 30));
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 10, _fill);
            _fill.Color = branchColor.WithAlpha((byte)(glowPulse * 15));
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 18, _fill);
        }

        // Gepunkteter Rahmen für gesperrte Nodes (sichtbar statt unsichtbar)
        if (item.IsLocked)
        {
            _stroke.Color = LineLocked.WithAlpha(128);
            _stroke.StrokeWidth = 2f;
            using var dotEffect = SKPathEffect.CreateDash([4, 4], _time * 5 % 8);
            _stroke.PathEffect = dotEffect;
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 3, _stroke);
            _stroke.PathEffect = null;
        }

        // Großes Icon
        ResearchIconRenderer.DrawIcon(canvas, cx, cy, NodeSize, item.Effect, branch,
            item.IsResearched, item.IsLocked);

        // Fortschrittsbalken (unter dem Icon)
        float barY = cy + NodeSize / 2 + 4;
        float barW = NodeSize * 1.1f;
        DrawNodeProgressBar(canvas, cx - barW / 2, barY, barW, ProgressBarHeight,
            item, branchColor);

        // Name + Prozent/Status (unter dem Balken)
        float textY = barY + ProgressBarHeight + 3;
        DrawNodeLabel(canvas, cx, textY, item, branchColor);

        // Gold-Glow für verfügbare Items (genug Geld + Voraussetzungen)
        if (item.CanStart && !item.IsResearched)
        {
            float glow = 0.5f + 0.3f * MathF.Sin(_time * 3f);
            // Gecachten Glow-Paint verwenden (nur Farbe ändern, MaskFilter ist statisch)
            _glowPaint.Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(glow * 80));
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 4, _glowPaint);
        }

        // "Startbereit"-Puls-Animation
        if (item.CanStart)
        {
            float pulse = 0.5f + MathF.Sin(_time * 3f) * 0.3f;
            _stroke.Color = branchColor.WithAlpha((byte)(pulse * 180));
            _stroke.StrokeWidth = 2.5f;
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 6 + pulse * 4, _stroke);
        }

        // Aktiv: Pulsierender Ring
        if (item.IsActive)
        {
            float activePulse = 0.6f + MathF.Sin(_time * 4f) * 0.4f;
            _stroke.Color = branchColor.WithAlpha((byte)(activePulse * 220));
            _stroke.StrokeWidth = 3f;
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 4, _stroke);
        }
    }

    private void DrawNodeProgressBar(SKCanvas canvas, float x, float y, float w, float h,
        ResearchDisplayItem item, SKColor branchColor)
    {
        // Hintergrund
        _fill.Color = ProgressBg;
        var bgRect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 3);
        canvas.DrawRoundRect(bgRect, _fill);

        // Fortschritt ermitteln
        float progress;
        SKColor barColor;

        if (item.IsResearched)
        {
            progress = 1.0f;
            barColor = branchColor;
        }
        else if (item.IsActive)
        {
            progress = (float)item.Progress;
            barColor = branchColor;
        }
        else
        {
            progress = 0;
            barColor = branchColor;
        }

        // Füllung mit Gradient
        float fillW = w * Math.Clamp(progress, 0, 1);
        if (fillW > 1)
        {
            var fillRect = new SKRect(x, y, x + fillW, y + h);
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(x, y), new SKPoint(x + fillW, y),
                [barColor.WithAlpha(180), barColor],
                SKShaderTileMode.Clamp);
            _fill.Shader = shader;
            canvas.DrawRoundRect(new SKRoundRect(fillRect, 4), _fill);
            _fill.Shader = null;

            // Glanz-Highlight oben
            _fill.Color = SKColors.White.WithAlpha(40);
            canvas.DrawRect(x + 1, y, fillW - 2, h * 0.4f, _fill);
        }

        // Prozent-Text im Balken (wenn erforscht oder aktiv)
        if (item.IsResearched || item.IsActive)
        {
            string percentText = $"{(int)(progress * 100)}%";
            _fontSmall.Size = 9;
            _fontSmall.Embolden = true;
            _text.Color = SKColors.White.WithAlpha(220);
            canvas.DrawText(percentText, x + w / 2, y + h - 1f, SKTextAlign.Center, _fontSmall, _text);
        }
    }

    private void DrawNodeLabel(SKCanvas canvas, float cx, float y,
        ResearchDisplayItem item, SKColor branchColor)
    {
        // Name (gecachter Font)
        _fontBold.Size = 14;
        _text.Color = item.IsLocked ? TextMuted : item.IsResearched ? branchColor : TextPrimary;

        // Text kürzen falls nötig
        string name = item.Name;
        float maxW = NodeSize * 1.8f;
        if (_fontBold.MeasureText(name) > maxW)
        {
            while (name.Length > 3 && _fontBold.MeasureText(name + "..") > maxW)
                name = name[..^1];
            name += "..";
        }

        canvas.DrawText(name, cx, y + 9, SKTextAlign.Center, _fontBold, _text);

        // Kosten (wenn nicht erforscht und nicht gesperrt)
        if (!item.IsResearched && !item.IsLocked && !item.IsActive)
        {
            _fontMedium.Size = 12;
            _fontMedium.Embolden = false;
            _text.Color = TextSecondary;
            canvas.DrawText($"\u20ac{item.CostDisplay}", cx, y + 19, SKTextAlign.Center, _fontMedium, _text);
        }

        // Level-Badge oben links am Icon
        _fontSmall.Size = 8;
        _fontSmall.Embolden = true;
        _fill.Color = branchColor;
        float badgeX = cx - NodeSize / 2 - 2;
        float badgeY = item.IsLocked ? cx - NodeSize / 2 + 2 : // Korrektur: badgeY berechnen
            y - NodeSize - ProgressBarHeight - TextHeight + 4;
        // Vereinfacht: Badge in die untere rechte Ecke des Icons
        float bx = cx + NodeSize / 2 - 12;
        float by = y - ProgressBarHeight - TextHeight - 2;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FLIEßENDE PARTIKEL
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawFlowParticles(SKCanvas canvas, List<ResearchDisplayItem> items,
        List<SKPoint> positions, SKColor branchColor, float deltaTime)
    {
        _particleTimer += deltaTime;

        if (_particleTimer >= 0.4f && _flowParticleCount < MaxFlowParticles)
        {
            _particleTimer = 0;

            // Partikel auf erforschten Verbindungen spawnen
            for (int i = 0; i < items.Count - 1 && i < positions.Count - 1; i++)
            {
                if (!items[i].IsResearched) continue;

                // Finde verbundene Nodes in der naechsten Zeile
                // (vereinfacht: naechsten 1-2 Items)
                int nextStart = i + 1;
                int nextEnd = Math.Min(i + 3, items.Count);

                for (int j = nextStart; j < nextEnd && j < positions.Count; j++)
                {
                    if (!items[j].IsResearched && !items[j].CanStart && !items[j].IsActive) continue;

                    if (Random.Shared.NextSingle() > 0.3f) continue; // Nicht alle Verbindungen

                    if (_flowParticleCount >= MaxFlowParticles) break;
                    _flowParticles[_flowParticleCount++] = new FlowParticle
                    {
                        StartX = positions[i].X,
                        StartY = positions[i].Y + NodeSize / 2 + 8,
                        EndX = positions[j].X,
                        EndY = positions[j].Y - NodeSize / 2 - 4,
                        Progress = 0,
                        Life = 1.0f
                    };
                    break;
                }
            }
        }

        // Aktualisieren und zeichnen (Compact-Loop)
        int aliveCount = 0;
        for (int i = 0; i < _flowParticleCount; i++)
        {
            var p = _flowParticles[i];
            p.Progress += deltaTime * 1.5f;
            p.Life -= deltaTime;

            if (p.Progress > 1 || p.Life <= 0) continue;

            _flowParticles[aliveCount++] = p;

            // Position auf der Bezier-Kurve
            float t = p.Progress;
            float midY = (p.StartY + p.EndY) / 2;

            float px = CubicBezierX(p.StartX, p.StartX, p.EndX, p.EndX, t);
            float py = CubicBezierY(p.StartY, midY, midY, p.EndY, t);

            byte alpha = (byte)(p.Life * 220);
            _fill.Color = branchColor.WithAlpha(alpha);
            canvas.DrawCircle(px, py, 5 * p.Life, _fill);

            // Glow
            _fill.Color = branchColor.WithAlpha((byte)(alpha / 3));
            canvas.DrawCircle(px, py, 10 * p.Life, _fill);
        }
        _flowParticleCount = aliveCount;
    }

    private static float CubicBezierX(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    private static float CubicBezierY(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HIT-TEST (Tap auf Node → Forschung starten)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob ein Tap auf einen Node trifft und gibt die Id des getroffenen Items zurück.
    /// Antippbar sind alle nicht-erforschten Nodes (auch gesperrte → ViewModel zeigt Feedback).
    /// </summary>
    public string? HitTest(float tapX, float tapY, List<ResearchDisplayItem> items, float centerX, float topY)
    {
        if (items.Count == 0) return null;

        var positions = CalculateNodePositions(items, centerX, topY + TopPadding);

        // Erweiterte Trefferzone (größer als der Node selbst)
        float hitRadius = NodeSize * 0.8f;

        for (int i = 0; i < items.Count && i < positions.Count; i++)
        {
            var item = items[i];
            // Bereits erforschte Items ignorieren
            if (item.IsResearched) continue;

            float dx = tapX - positions[i].X;
            float dy = tapY - positions[i].Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= hitRadius)
            {
                return item.Id;
            }
        }

        return null;
    }

    private struct FlowParticle
    {
        public float StartX, StartY, EndX, EndY, Progress, Life;
    }
}
