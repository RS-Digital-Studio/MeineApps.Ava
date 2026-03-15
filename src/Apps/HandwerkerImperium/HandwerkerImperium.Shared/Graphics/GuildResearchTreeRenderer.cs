using HandwerkerImperium.Models;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert die Gilden-Forschung als 2-Spalten-Layout.
/// 18 Items in 6 unabhängigen Kategorien (3 Paare à 2 Spalten):
///
/// [Infra 1]   [Wirt 1]
///     ↓           ↓
/// [Infra 2]   [Wirt 2]
///     ↓           ↓
/// [Infra 3]   [Wirt 3]
///                 ↓
///             [Wirt 4]
///    ─ ─ ─ ─ ─ ─ ─ ─ ─
/// [Wiss 1]    [Log 1]
///     ↓           ↓
/// [Wiss 2]    [Log 2]
///     ↓           ↓
/// [Wiss 3]    [Log 3]
///    ─ ─ ─ ─ ─ ─ ─ ─ ─
/// [Arbeit 1]  [Meister 1]
///     ↓           ↓
/// [Arbeit 2]  [Meister 2]
///     ↓
/// [Arbeit 3]
/// </summary>
public sealed class GuildResearchTreeRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    // Layout-Konstanten
    private const float NodeSize = 68;
    private const float RowHeight = 120;         // Platz für Name + Kosten unter Nodes
    private const float SectionGap = 50;         // Abstand zwischen Kategorie-Paaren
    private const float ProgressBarHeight = 10;  // Größerer Balken für bessere Lesbarkeit
    private const float TopPadding = 55;         // Platz für Kategorie-Header
    private const int TotalRows = 12;            // 0-11 (3 Sektionen à 4 Zeilen)

    // Fließende Partikel entlang erforschter Verbindungen (Array + Swap-Remove, 0 GC)
    private const int MaxFlowParticles = 16;
    private readonly FlowParticle[] _flowParticles = new FlowParticle[MaxFlowParticles];
    private int _flowParticleCount;
    private float _particleTimer;

    // Farben pro Kategorie
    private static readonly SKColor InfraColor = new(0x3B, 0x82, 0xF6);   // Blau
    private static readonly SKColor EconomyColor = new(0x10, 0xB9, 0x81);  // Grün
    private static readonly SKColor KnowledgeColor = new(0x8B, 0x5C, 0xF6); // Violett
    private static readonly SKColor LogisticsColor = new(0xF5, 0x9E, 0x0B); // Amber
    private static readonly SKColor WorkforceColor = new(0xEF, 0x44, 0x44); // Rot
    private static readonly SKColor MasteryColor = new(0xFF, 0xD7, 0x00);   // Gold
    private static readonly SKColor ResearchingColor = new(0xF5, 0x9E, 0x0B); // Amber (Timer-Phase)

    private static readonly SKColor LineLocked = new(0x5A, 0x48, 0x38);
    private static readonly SKColor TextPrimary = new(0xF5, 0xF0, 0xEB);
    private static readonly SKColor TextMuted = new(0x7A, 0x68, 0x58);
    private static readonly SKColor ProgressBg = new(0x30, 0x24, 0x1A);

    // Gecachte Paints (Instanz-Felder - NICHT static, da Farbe/Style pro Frame mutiert wird)
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _text = new() { IsAntialias = true };

    // Gecachte Font- und Path-Objekte (vermeidet Allokationen pro Frame)
    private readonly SKFont _percentFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKFont _nameFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _costFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKPath _connectionPath = new();
    private readonly SKPath _arrowPath = new();
    private readonly SKPath _arcPath = new();

    // Gecachte Dash-Intervall-Arrays (vermeidet Array-Allokation pro Frame)
    private static readonly float[] DashIntervals = [6, 4];
    private static readonly float[] DotIntervals = [3, 3];

    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8);
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, MaskFilter = _glowFilter };

    // Gecachte Positionen (vermeidet List-Allokation pro Frame)
    private readonly List<SKPoint> _cachedPositions = new(18);
    private float _lastCenterX = float.NaN;
    private float _lastStartY = float.NaN;

    // Gecachter Namenscache (TruncateName erstellt sonst Strings pro Frame)
    private readonly Dictionary<string, string> _truncatedNameCache = new();

    // Gecachter Kategorie-Label MeasureText (statische Namen, Breite ändert sich nie)
    private readonly Dictionary<GuildResearchCategory, float> _categoryLabelWidthCache = new();

    // Gecachte Prozent-Strings für Fortschrittsbalken (vermeidet Interpolation pro Node pro Frame)
    private static readonly string[] _percentStrings = new string[101];

    // Gecachte Kosten/Effekt-Strings pro Node-Index (nur bei Datenänderung neu berechnen)
    private readonly string[] _cachedCostStrings = new string[18];
    private readonly string[] _cachedEffectStrings = new string[18];
    private readonly long[] _lastCostValues = new long[18];
    private readonly long[] _lastProgressValues = new long[18];

    static GuildResearchTreeRenderer()
    {
        // Prozent-Strings 0%-100% vorberechnen
        for (int i = 0; i <= 100; i++)
            _percentStrings[i] = $"{i}%";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LAYOUT-MAP: 18 Forschungen → (Zeile, Spalte) Positionen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Feste Zuordnung: Index in GetAll()-Liste → (row, column).
    /// 2-Spalten-Layout: col 0 = links, col 2 = rechts.
    /// 3 Sektionen mit Lücke dazwischen (Zeilen 4, 8 sind leer).
    /// </summary>
    private static readonly (int row, int col)[] NodeLayout =
    [
        // ── Sektion 1 ──
        // Infrastruktur 1-3 (Indices 0-2, links)
        (0, 0), (1, 0), (2, 0),
        // Wirtschaft 1-4 (Indices 3-6, rechts)
        (0, 2), (1, 2), (2, 2), (3, 2),

        // ── Sektion 2 ──
        // Wissen 1-3 (Indices 7-9, links)
        (5, 0), (6, 0), (7, 0),
        // Logistik 1-3 (Indices 10-12, rechts)
        (5, 2), (6, 2), (7, 2),

        // ── Sektion 3 ──
        // Arbeitsmarkt 1-3 (Indices 13-15, links)
        (9, 0), (10, 0), (11, 0),
        // Meisterschaft 1-2 (Indices 16-17, rechts)
        (9, 2), (10, 2)
    ];

    /// <summary>
    /// Verbindungen zwischen Nodes (NUR innerhalb einer Kategorie - Kategorien sind unabhängig).
    /// </summary>
    private static readonly (int from, int to)[] Connections =
    [
        // Infrastruktur linear
        (0, 1), (1, 2),
        // Wirtschaft linear
        (3, 4), (4, 5), (5, 6),
        // Wissen linear
        (7, 8), (8, 9),
        // Logistik linear
        (10, 11), (11, 12),
        // Arbeitsmarkt linear
        (13, 14), (14, 15),
        // Meisterschaft linear
        (16, 17)
    ];

    /// <summary>
    /// Erste Indizes jeder Kategorie (für Header-Rendering).
    /// </summary>
    private static readonly (int firstIndex, GuildResearchCategory cat)[] CategorySections =
    [
        (0, GuildResearchCategory.Infrastructure),
        (3, GuildResearchCategory.Economy),
        (7, GuildResearchCategory.Knowledge),
        (10, GuildResearchCategory.Logistics),
        (13, GuildResearchCategory.Workforce),
        (16, GuildResearchCategory.Mastery),
    ];

    /// <summary>
    /// Gesamthöhe des Baums in Pixeln.
    /// </summary>
    public static float CalculateTotalHeight() =>
        TopPadding + TotalRows * RowHeight + 2 * SectionGap + 60;

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

        // 0. Sektions-Trennlinien
        DrawSectionDividers(canvas, centerX, bounds.Top + TopPadding, bounds.Width);

        // 1. Kategorie-Header (Icon-Badges über jeder Spalte)
        DrawCategoryHeaders(canvas, positions, items);

        // 2. Verbindungslinien (hinter den Nodes)
        DrawConnections(canvas, items, positions);

        // 3. Fließende Partikel
        UpdateAndDrawFlowParticles(canvas, items, positions, deltaTime);

        // 4. Nodes
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
    // SEKTIONS-TRENNLINIEN & KATEGORIE-HEADER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet horizontale Trennlinien zwischen den 3 Sektions-Paaren.
    /// </summary>
    private void DrawSectionDividers(SKCanvas canvas, float centerX, float startY, float width)
    {
        float halfW = width * 0.35f;
        float left = centerX - halfW;
        float right = centerX + halfW;

        // Trennlinie nach Sektion 1 (zwischen Zeile 3 und 5)
        float divY1 = startY + 4 * RowHeight + SectionGap * 0.5f;
        // Trennlinie nach Sektion 2 (zwischen Zeile 7 und 9)
        float divY2 = startY + 8 * RowHeight + SectionGap * 1.5f;

        // Dezente gestrichelte Linien (kein Array-Allokation pro Frame)
        _stroke.Color = LineLocked.WithAlpha(40);
        _stroke.StrokeWidth = 1f;
        using var dash = SKPathEffect.CreateDash(DashIntervals, 0);
        _stroke.PathEffect = dash;
        canvas.DrawLine(left, divY1, right, divY1, _stroke);
        canvas.DrawLine(left, divY2, right, divY2, _stroke);
        _stroke.PathEffect = null;
    }

    /// <summary>
    /// Zeichnet Kategorie-Badges über der ersten Node jeder Kategorie.
    /// </summary>
    private void DrawCategoryHeaders(SKCanvas canvas, List<SKPoint> positions, List<GuildResearchDisplay> items)
    {
        _nameFont.Size = 11;
        _nameFont.Embolden = true;

        foreach (var (firstIndex, cat) in CategorySections)
        {
            if (firstIndex >= positions.Count || firstIndex >= items.Count) continue;

            var pos = positions[firstIndex];
            var color = GetCategoryColor(cat);
            float headerY = pos.Y - NodeSize / 2 - 22;

            // Hintergrund-Pill (MeasureText gecacht, statische Kategorie-Namen)
            string label = GetCategoryLabel(cat);
            if (!_categoryLabelWidthCache.TryGetValue(cat, out float textW))
            {
                textW = _nameFont.MeasureText(label);
                _categoryLabelWidthCache[cat] = textW;
            }
            float pillW = textW + 18;
            float pillH = 20;
            float pillX = pos.X - pillW / 2;

            _fill.Color = color.WithAlpha(30);
            canvas.DrawRoundRect(pillX, headerY - pillH / 2, pillW, pillH, 10, 10, _fill);
            _stroke.Color = color.WithAlpha(70);
            _stroke.StrokeWidth = 1f;
            _stroke.PathEffect = null;
            canvas.DrawRoundRect(pillX, headerY - pillH / 2, pillW, pillH, 10, 10, _stroke);

            // Textschatten für Lesbarkeit
            _text.Color = new SKColor(0, 0, 0, 60);
            canvas.DrawText(label, pos.X + 0.5f, headerY + 4.5f, SKTextAlign.Center, _nameFont, _text);
            // Label-Text
            _text.Color = color.WithAlpha(220);
            canvas.DrawText(label, pos.X, headerY + 4, SKTextAlign.Center, _nameFont, _text);
        }

        _nameFont.Embolden = false;
    }

    /// <summary>
    /// Kurzer deutscher Kategorie-Name.
    /// </summary>
    private static string GetCategoryLabel(GuildResearchCategory cat) => cat switch
    {
        GuildResearchCategory.Infrastructure => "Infrastruktur",
        GuildResearchCategory.Economy => "Wirtschaft",
        GuildResearchCategory.Knowledge => "Wissen",
        GuildResearchCategory.Logistics => "Logistik",
        GuildResearchCategory.Workforce => "Arbeitsmarkt",
        GuildResearchCategory.Mastery => "Meisterschaft",
        _ => ""
    };

    // ═══════════════════════════════════════════════════════════════════════
    // POSITIONEN
    // ═══════════════════════════════════════════════════════════════════════

    private List<SKPoint> CalculatePositions(float centerX, float startY)
    {
        // Nur neu berechnen wenn sich die Parameter geändert haben
        if (_cachedPositions.Count == NodeLayout.Length &&
            MathF.Abs(_lastCenterX - centerX) < 0.5f &&
            MathF.Abs(_lastStartY - startY) < 0.5f)
            return _cachedPositions;

        _lastCenterX = centerX;
        _lastStartY = startY;
        _cachedPositions.Clear();
        float spread = NodeSize * 1.7f; // Spalten-Abstand (links/rechts vom Zentrum)

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
            // Sektions-Abstände zwischen Kategorie-Paaren
            if (row >= 5) y += SectionGap;
            if (row >= 9) y += SectionGap;
            _cachedPositions.Add(new SKPoint(x, y));
        }
        return _cachedPositions;
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
            bool toActive = toItem.IsActive || toItem.IsResearching;
            bool toLocked = toItem.IsLocked;

            var lineColor = GetCategoryColor(toItem.Category);

            // Innerhalb einer Spalte → gerade vertikale Linie
            float lineX = fromPos.X;
            float startY = fromPos.Y + NodeSize / 2 + 6;
            float endY = toPos.Y - NodeSize / 2 - 6;

            if (fromDone && (toDone || toActive))
            {
                // Erforschte Verbindung: Farbig mit Glow
                _stroke.Color = lineColor.WithAlpha(30);
                _stroke.StrokeWidth = 8f;
                _stroke.PathEffect = null;
                canvas.DrawLine(lineX, startY, lineX, endY, _stroke);

                _stroke.Color = lineColor.WithAlpha(toDone ? (byte)200 : (byte)150);
                _stroke.StrokeWidth = 3f;
                canvas.DrawLine(lineX, startY, lineX, endY, _stroke);

                DrawArrowHead(canvas, lineX, endY, lineColor.WithAlpha(200));
            }
            else if (fromDone && toLocked)
            {
                // Nächstes verfügbar: Gestrichelt, pulsierend
                _stroke.Color = lineColor.WithAlpha(90);
                _stroke.StrokeWidth = 2.5f;
                using var dash = SKPathEffect.CreateDash(DashIntervals, _time * 12 % 10);
                _stroke.PathEffect = dash;
                canvas.DrawLine(lineX, startY, lineX, endY, _stroke);
                _stroke.PathEffect = null;

                DrawArrowHead(canvas, lineX, endY, lineColor.WithAlpha(80));
            }
            else
            {
                // Gesperrt: Dezentes Grau
                _stroke.Color = LineLocked.WithAlpha(60);
                _stroke.StrokeWidth = 1.5f;
                _stroke.PathEffect = null;
                canvas.DrawLine(lineX, startY, lineX, endY, _stroke);
            }
        }
    }

    private void DrawArrowHead(SKCanvas canvas, float x, float y, SKColor color)
    {
        _fill.Color = color;
        _arrowPath.Rewind();
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

        // Schatten unter dem Node für Tiefenwirkung
        _fill.Color = new SKColor(0, 0, 0, 25);
        canvas.DrawCircle(cx + 1, cy + 3, NodeSize / 2 + 1, _fill);

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
            _fill.Color = new SKColor(0x22, 0x1A, 0x12, 180);
            canvas.DrawCircle(cx, cy, NodeSize / 2, _fill);

            // Gepunkteter Rahmen
            _stroke.Color = LineLocked.WithAlpha(80);
            _stroke.StrokeWidth = 1.5f;
            using var dotEffect = SKPathEffect.CreateDash(DotIntervals, _time * 4 % 6);
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
        else if (item.IsResearching)
        {
            // Forschung läuft: Amber-Hintergrund mit pulsierendem Rahmen
            _fill.Color = new SKColor(0x20, 0x18, 0x10, 200);
            canvas.DrawCircle(cx, cy, NodeSize / 2, _fill);

            float pulse = 0.5f + MathF.Sin(_time * 2.5f) * 0.35f;
            _stroke.Color = ResearchingColor.WithAlpha((byte)(pulse * 220));
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

        // Innerer Licht-Effekt (fake Highlight für Tiefe)
        if (!item.IsLocked)
        {
            _fill.Color = SKColors.White.WithAlpha(12);
            canvas.DrawCircle(cx, cy - NodeSize * 0.1f, NodeSize * 0.25f, _fill);
        }

        // Icon in der Mitte
        float iconSize = NodeSize * 0.55f;
        byte iconAlpha = item.IsLocked ? (byte)60 : (byte)220;
        var iconColor = item.IsLocked ? LineLocked.WithAlpha(iconAlpha)
            : item.IsResearching ? ResearchingColor.WithAlpha(iconAlpha)
            : catColor.WithAlpha(iconAlpha);
        _fill.Color = iconColor;
        _stroke.Color = iconColor;
        _stroke.StrokeWidth = 1.5f;

        if (item.IsResearching)
        {
            // Rotierendes Icon bei laufender Forschung
            canvas.Save();
            canvas.RotateDegrees(_time * 30f, cx, cy);
            GuildResearchIconRenderer.DrawIcon(canvas, cx, cy, iconSize, item.Category, _fill, _stroke);
            canvas.Restore();
        }
        else
        {
            GuildResearchIconRenderer.DrawIcon(canvas, cx, cy, iconSize, item.Category, _fill, _stroke);
        }

        // Lock-Badge für gesperrte Nodes (oben rechts, analog zum Häkchen)
        if (item.IsLocked)
        {
            DrawLockBadge(canvas, cx + NodeSize / 2 - 4, cy - NodeSize / 2 + 4);
        }

        // Tier-Indikator (I, II, III, IV Punkte)
        int tier = GetTierInCategory(index);
        if (tier > 0)
        {
            _fill.Color = item.IsLocked ? LineLocked.WithAlpha(60) : catColor.WithAlpha(160);
            GuildResearchIconRenderer.DrawTierIndicator(canvas, cx, cy + NodeSize / 2 - 8, NodeSize, tier, _fill);
        }

        // Fortschrittsring
        if (item.IsResearching)
        {
            // Timer-Fortschrittsring (Amber)
            float timerProgress = GetTimerProgress(item);
            DrawProgressRing(canvas, cx, cy, NodeSize / 2 + 5, timerProgress, ResearchingColor);
        }
        else if (item.IsActive && !item.IsCompleted)
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
        float barW = NodeSize * 1.1f;
        DrawProgressBar(canvas, cx - barW / 2, barY, barW, ProgressBarHeight, item, catColor);

        // Forschungsname unter dem Fortschrittsbalken
        float nameY = barY + ProgressBarHeight + 13;
        _nameFont.Size = 11;
        _nameFont.Embolden = true;
        string displayName = TruncateName(item.Name, 16);
        // Textschatten für Lesbarkeit auf dem Pergament
        _text.Color = new SKColor(0, 0, 0, 60);
        canvas.DrawText(displayName, cx + 0.5f, nameY + 0.5f, SKTextAlign.Center, _nameFont, _text);
        // Name in heller Farbe
        _text.Color = item.IsLocked ? TextMuted.WithAlpha(150) :
            item.IsCompleted ? catColor.WithAlpha(240) : TextPrimary;
        canvas.DrawText(displayName, cx, nameY, SKTextAlign.Center, _nameFont, _text);
        _nameFont.Embolden = false;

        // Kosten (offen) oder Effekt (abgeschlossen) unter dem Namen
        // Gecachte Strings: nur bei Datenänderung neu berechnen (nicht pro Frame)
        float infoY = nameY + 13;
        _costFont.Size = 10;
        if (item.IsCompleted)
        {
            // Abgeschlossen: Effekt in Kategorie-Farbe (gecacht)
            _text.Color = catColor.WithAlpha(200);
            if (index < _cachedEffectStrings.Length && _cachedEffectStrings[index] == null)
                _cachedEffectStrings[index] = FormatEffect(item.EffectType, item.EffectValue);
            string effectStr = index < _cachedEffectStrings.Length ? _cachedEffectStrings[index] ?? "" : FormatEffect(item.EffectType, item.EffectValue);
            canvas.DrawText(effectStr, cx, infoY, SKTextAlign.Center, _costFont, _text);
        }
        else
        {
            // Offen: Kosten-Anzeige (gecacht, invalidiert bei Kostenänderung)
            _text.Color = item.IsLocked ? TextMuted.WithAlpha(120) : new SKColor(0xD4, 0xA3, 0x73);
            string costStr;
            if (index < _cachedCostStrings.Length)
            {
                if (_lastCostValues[index] != item.Cost || _lastProgressValues[index] != item.Progress
                    || _cachedCostStrings[index] == null)
                {
                    _cachedCostStrings[index] = FormatCost(item.Cost, item.Progress);
                    _lastCostValues[index] = item.Cost;
                    _lastProgressValues[index] = item.Progress;
                }
                costStr = _cachedCostStrings[index]!;
            }
            else
            {
                costStr = FormatCost(item.Cost, item.Progress);
            }
            canvas.DrawText(costStr, cx, infoY, SKTextAlign.Center, _costFont, _text);
        }

        // Glow-Effekt auf aktiven/forschenden Nodes
        if (item.IsResearching)
        {
            // Amber-Glow bei laufender Forschung
            float pulse = 0.3f + MathF.Sin(_time * 2.5f) * 0.3f;
            _glowPaint.Color = ResearchingColor.WithAlpha((byte)(pulse * 50));
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 6, _glowPaint);

            // Pulsierender Amber-Ring
            _stroke.Color = ResearchingColor.WithAlpha((byte)(pulse * 140));
            _stroke.StrokeWidth = 2f;
            _stroke.PathEffect = null;
            canvas.DrawCircle(cx, cy, NodeSize / 2 + 4 + pulse * 3, _stroke);
        }
        else if (item.IsActive && !item.IsCompleted)
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
        // SkiaSharp ArcTo bei 360° erzeugt leeren Path (Start=Ende) → auf 359.5° begrenzen
        if (sweepAngle >= 359.5f) sweepAngle = 359.5f;
        if (sweepAngle > 0.5f)
        {
            _stroke.Color = color.WithAlpha(200);
            _stroke.StrokeWidth = 3f;
            _stroke.StrokeCap = SKStrokeCap.Round;
            var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
            _arcPath.Rewind();
            _arcPath.AddArc(arcRect, -90, sweepAngle);
            canvas.DrawPath(_arcPath, _stroke);
        }
    }

    /// <summary>
    /// Grünes Häkchen-Badge.
    /// </summary>
    private void DrawCheckmark(SKCanvas canvas, float cx, float cy, SKColor color)
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
    /// Lock-Badge für gesperrte Nodes (dunkler Kreis mit Schloss-Symbol).
    /// </summary>
    private void DrawLockBadge(SKCanvas canvas, float cx, float cy)
    {
        // Dunkler Hintergrund-Kreis
        _fill.Color = new SKColor(0x35, 0x28, 0x1A);
        canvas.DrawCircle(cx, cy, 8, _fill);
        _stroke.Color = LineLocked.WithAlpha(120);
        _stroke.StrokeWidth = 1f;
        _stroke.PathEffect = null;
        canvas.DrawCircle(cx, cy, 8, _stroke);

        // Schloss-Symbol
        _stroke.Color = LineLocked.WithAlpha(200);
        _stroke.StrokeWidth = 1.2f;
        float s = 3f;

        // Bügel (Halbkreis oben)
        _arcPath.Rewind();
        _arcPath.AddArc(new SKRect(cx - s * 0.6f, cy - s * 1.1f, cx + s * 0.6f, cy + s * 0.1f), 180, 180);
        canvas.DrawPath(_arcPath, _stroke);

        // Schloss-Körper (Rechteck unten)
        _fill.Color = LineLocked.WithAlpha(160);
        canvas.DrawRect(cx - s * 0.8f, cy - s * 0.05f, s * 1.6f, s * 1.2f, _fill);
    }

    /// <summary>
    /// Linearer Fortschrittsbalken unter dem Node.
    /// </summary>
    private void DrawProgressBar(SKCanvas canvas, float x, float y, float w, float h,
        GuildResearchDisplay item, SKColor color)
    {
        // Hintergrund
        _fill.Color = ProgressBg;
        canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), 4, 4, _fill);

        float progress;
        SKColor barColor;
        if (item.IsResearching)
        {
            progress = GetTimerProgress(item);
            barColor = ResearchingColor;
        }
        else
        {
            progress = item.IsCompleted ? 1f : (float)item.ProgressPercent;
            barColor = color;
        }
        float fillW = w * Math.Clamp(progress, 0, 1);

        if (fillW > 1)
        {
            var fillRect = new SKRect(x, y, x + fillW, y + h);

            // Basis-Füllung (dunklere Variante)
            _fill.Color = barColor.WithAlpha(160);
            canvas.DrawRoundRect(fillRect, 4, 4, _fill);

            // Hellere Schicht rechts (simuliert Gradient ohne Shader-Allokation)
            var halfRect = new SKRect(x + fillW * 0.4f, y, x + fillW, y + h);
            _fill.Color = barColor;
            canvas.DrawRoundRect(halfRect, 4, 4, _fill);

            // Glanz oben
            _fill.Color = SKColors.White.WithAlpha(35);
            canvas.DrawRect(x + 1, y, fillW - 2, h * 0.4f, _fill);
        }

        // Prozent-Text (gecachte Strings für 0-100%, vermeidet Interpolation pro Frame)
        if (item.IsActive || item.IsCompleted || item.IsResearching)
        {
            string pct;
            if (item.IsResearching)
            {
                float timerPct = GetTimerProgress(item);
                int pctIdx = Math.Clamp((int)(timerPct * 100), 0, 100);
                pct = _percentStrings[pctIdx];
            }
            else
            {
                int pctIdx = Math.Clamp((int)(progress * 100), 0, 100);
                pct = _percentStrings[pctIdx];
            }
            _percentFont.Size = 10;
            _text.Color = item.IsResearching ? ResearchingColor.WithAlpha(220) : SKColors.White.WithAlpha(200);
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
        if (_particleTimer >= 0.5f && _flowParticleCount < MaxFlowParticles)
        {
            _particleTimer = 0;
            foreach (var (from, to) in Connections)
            {
                if (from >= items.Count || to >= items.Count) continue;
                if (!items[from].IsCompleted) continue;
                if (!items[to].IsCompleted && !items[to].IsActive) continue;
                if (Random.Shared.NextSingle() > 0.25f) continue;

                _flowParticles[_flowParticleCount++] = new FlowParticle
                {
                    StartX = positions[from].X,
                    StartY = positions[from].Y + NodeSize / 2 + 6,
                    EndX = positions[to].X,
                    EndY = positions[to].Y - NodeSize / 2 - 6,
                    Progress = 0,
                    Life = 1.2f
                };
                break; // Nur 1 pro Tick
            }
        }

        // Partikel aktualisieren und zeichnen (Swap-Remove, 0 GC)
        for (int i = _flowParticleCount - 1; i >= 0; i--)
        {
            var p = _flowParticles[i];
            p.Progress += deltaTime * 1.2f;
            p.Life -= deltaTime;

            if (p.Progress > 1 || p.Life <= 0)
            {
                // Swap-Remove: Letztes Element an diese Stelle, Count verringern
                _flowParticles[i] = _flowParticles[--_flowParticleCount];
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
    /// Berechnet den Timer-Fortschritt (0.0 = gerade gestartet, 1.0 = fertig).
    /// </summary>
    private static float GetTimerProgress(GuildResearchDisplay item)
    {
        if (item.DurationHours <= 0 || item.RemainingTime == null) return 0f;
        var totalDuration = TimeSpan.FromHours(item.DurationHours);
        if (totalDuration.TotalSeconds <= 0) return 0f;
        var elapsed = totalDuration - item.RemainingTime.Value;
        return Math.Clamp((float)(elapsed.TotalSeconds / totalDuration.TotalSeconds), 0f, 1f);
    }

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

    /// <summary>
    /// Kompakte Kosten-Anzeige mit Fortschritt (z.B. "15M / 50M").
    /// </summary>
    private static string FormatCost(long cost, long progress)
    {
        if (progress > 0)
            return $"{FormatCompact(progress)} / {FormatCompact(cost)}";
        return FormatCompact(cost);
    }

    /// <summary>
    /// Kompakte Zahl mit B/M/K-Suffix.
    /// </summary>
    private static string FormatCompact(long value)
    {
        if (value >= 1_000_000_000) return $"{value / 1_000_000_000.0:0.#}B";
        if (value >= 1_000_000) return $"{value / 1_000_000.0:0.#}M";
        if (value >= 1_000) return $"{value / 1_000.0:0.#}K";
        return value.ToString("N0");
    }

    /// <summary>
    /// Kompakte Effekt-Zusammenfassung für abgeschlossene Forschungen.
    /// </summary>
    private static string FormatEffect(GuildResearchEffectType type, decimal value) => type switch
    {
        GuildResearchEffectType.MaxMembers => $"+{(int)value} Slots",
        GuildResearchEffectType.IncomeBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.CostReduction => $"-{value * 100:0}%",
        GuildResearchEffectType.RewardBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.XpBonus => $"+{value * 100:0}% XP",
        GuildResearchEffectType.EfficiencyBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.MiniGameBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.OrderSlotBonus => $"+{(int)value} Slot",
        GuildResearchEffectType.OrderQualityBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.WorkerSlotBonus => $"+{(int)value} Slot",
        GuildResearchEffectType.TrainingSpeedBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.FatigueReduction => $"-{value * 100:0}%",
        GuildResearchEffectType.ResearchSpeedBonus => $"+{value * 100:0}%",
        GuildResearchEffectType.PrestigePointBonus => $"+{value * 100:0}%",
        _ => ""
    };

    /// <summary>
    /// Kürzt einen Namen auf maxChars Zeichen mit Ellipsis (gecacht).
    /// </summary>
    private string TruncateName(string name, int maxChars)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxChars) return name;
        if (_truncatedNameCache.TryGetValue(name, out var cached)) return cached;
        var truncated = string.Concat(name.AsSpan(0, maxChars - 1), "\u2026");
        _truncatedNameCache[name] = truncated;
        return truncated;
    }

    // Partikel-Struct (vermeidet Heap-Allokationen)
    private struct FlowParticle
    {
        public float StartX, StartY, EndX, EndY;
        public float Progress;
        public float Life;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fill.Dispose();
        _stroke.Dispose();
        _text.Dispose();
        _glowPaint.Dispose();
        _percentFont.Dispose();
        _nameFont.Dispose();
        _costFont.Dispose();
        _connectionPath.Dispose();
        _arrowPath.Dispose();
        _arcPath.Dispose();
    }
}
