using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert eine einzelne Forschungs-Karte als SkiaSharp-Grafik.
/// Enthält: Branch-Farbbalken, Effekt-Icon, Name, Beschreibung,
/// Kosten/Dauer, Status-Anzeige (erforscht/gesperrt/aktiv/startbereit).
/// </summary>
public static class ResearchItemRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // FARB-PALETTE
    // ═══════════════════════════════════════════════════════════════════════

    // Branch-Farben
    private static readonly SKColor ToolsColor = new(0xEA, 0x58, 0x0C);       // Craft-Orange
    private static readonly SKColor ManagementColor = new(0x92, 0x40, 0x0E);   // Braun/Indigo
    private static readonly SKColor MarketingColor = new(0x65, 0xA3, 0x0D);    // Grün

    // Karten-Farben
    private static readonly SKColor CardBg = new(0x2A, 0x1F, 0x1A);            // Dunkles Holz
    private static readonly SKColor CardBgLocked = new(0x1E, 0x18, 0x14);      // Noch dunkler (gesperrt)
    private static readonly SKColor CardBorder = new(0x4E, 0x34, 0x2E);        // Holzrahmen
    private static readonly SKColor TextPrimary = new(0xF5, 0xF0, 0xEB);       // Heller Text
    private static readonly SKColor TextSecondary = new(0xA0, 0x90, 0x80);     // Gedimmter Text
    private static readonly SKColor TextMuted = new(0x6A, 0x5A, 0x50);         // Stark gedimmt
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);         // Gold
    private static readonly SKColor WarningColor = new(0xFF, 0xB3, 0x00);      // Orange/Warnung
    private static readonly SKColor SuccessColor = new(0x4C, 0xAF, 0x50);      // Grün/Erfolg
    private static readonly SKColor ErrorColor = new(0xF4, 0x43, 0x36);        // Rot
    private static readonly SKColor WoodGrainLight = new(0x35, 0x28, 0x20);    // Holzmaserung hell
    private static readonly SKColor WoodGrainDark = new(0x22, 0x1A, 0x14);     // Holzmaserung dunkel
    private static readonly SKColor IconBgColor = new(0x3A, 0x2C, 0x24);       // Icon-Hintergrund

    // Gecachte Paints (pro Aufruf wiederverwendet)
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    // Gecachte Font- und Path-Objekte (vermeidet Allokationen pro Frame, nur UI-Thread)
    private static readonly SKFont _fontRegular = new() { Edging = SKFontEdging.Antialias };
    private static readonly SKFont _fontBold = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private static readonly SKPath _cachedPath = new();

    /// <summary>
    /// Rendert eine einzelne Forschungs-Karte.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfügbarer Bereich für diese Karte.</param>
    /// <param name="item">Display-Daten der Forschung.</param>
    /// <param name="branch">Zugehöriger Forschungszweig.</param>
    /// <param name="time">Animationszeit in Sekunden.</param>
    public static void Render(SKCanvas canvas, SKRect bounds, ResearchDisplayItem item,
        ResearchBranch branch, float time)
    {
        float x = bounds.Left;
        float y = bounds.Top;
        float w = bounds.Width;
        float h = bounds.Height;

        // Opacity für gesperrte Items
        if (item.IsLocked)
        {
            canvas.SaveLayer(null);
        }

        // Karten-Hintergrund mit Holztextur
        DrawCardBackground(canvas, x, y, w, h, item.IsLocked, item.IsActive, branch, time);

        // Branch-farbiger Seitenbalken (links)
        DrawBranchBar(canvas, x, y, h, branch);

        // Effekt-Icon (links, nach dem Seitenbalken)
        float iconX = x + 14;
        float iconY = y + h * 0.35f;
        float iconSize = Math.Min(h * 0.35f, 26);
        DrawEffectIcon(canvas, iconX, iconY, iconSize, item.Effect, branch);

        // Level-Badge (rechts neben Icon)
        float levelX = iconX + iconSize + 10;
        float levelY = y + 10;
        DrawLevelBadge(canvas, levelX, levelY, item.Level, branch);

        // Name (rechts neben Level-Badge)
        float nameX = levelX + 38;
        float nameY = y + 12;
        DrawName(canvas, nameX, nameY, w - (nameX - x) - 34, item.Name, item.IsLocked);

        // Beschreibung
        float descY = nameY + 18;
        DrawDescription(canvas, nameX, descY, w - (nameX - x) - 34, item.Description, item.IsLocked);

        // Status-Icon (rechts oben)
        float statusX = x + w - 28;
        float statusY = y + 10;
        DrawStatusIcon(canvas, statusX, statusY, item, time);

        // Kosten & Dauer (unten)
        if (!item.IsResearched)
        {
            float costY = y + h - 22;
            DrawCostAndDuration(canvas, nameX, costY, item);
        }

        // Aktiver Fortschrittsbalken
        if (item.IsActive)
        {
            float barY = y + h - 6;
            DrawProgressBar(canvas, x + 6, barY, w - 12, 4, (float)item.Progress, branch, time);
        }

        // Startbereit-Shimmer
        if (item.CanStart)
        {
            DrawStartReadyShimmer(canvas, x, y, w, h, branch, time);
        }

        if (item.IsLocked)
        {
            // Opacity 0.45 für gesperrte Items
            using var dimPaint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(
                new SKColor(0, 0, 0, 140), SKBlendMode.DstIn) };
            canvas.Restore();
        }
    }

    /// <summary>
    /// Berechnet die benötigte Höhe für eine Karte.
    /// </summary>
    public static float GetItemHeight(bool isResearched) => isResearched ? 64 : 80;

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE RENDER-METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawCardBackground(SKCanvas canvas, float x, float y, float w, float h,
        bool isLocked, bool isActive, ResearchBranch branch, float time)
    {
        var bgColor = isLocked ? CardBgLocked : CardBg;

        // Karten-Hintergrund
        var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 8);
        _fillPaint.Color = bgColor;
        canvas.DrawRoundRect(rect, _fillPaint);

        // Dezente Holzmaserung (3 horizontale Streifen)
        var grainColor = isLocked ? WoodGrainDark : WoodGrainLight;
        _fillPaint.Color = grainColor;
        for (int i = 0; i < 3; i++)
        {
            float grainY = y + h * (0.2f + i * 0.3f);
            canvas.DrawRect(x + 6, grainY, w - 12, 1, _fillPaint);
        }

        // Rahmen
        _strokePaint.Color = isActive ? GetBranchColor(branch) : CardBorder;
        _strokePaint.StrokeWidth = isActive ? 2 : 1;
        canvas.DrawRoundRect(rect, _strokePaint);

        // Aktiv: Pulsierender Glow-Rahmen
        if (isActive)
        {
            float glowAlpha = 0.3f + MathF.Sin(time * 3f) * 0.2f;
            var glowColor = GetBranchColor(branch).WithAlpha((byte)(glowAlpha * 255));
            _strokePaint.Color = glowColor;
            _strokePaint.StrokeWidth = 3;
            var glowRect = new SKRoundRect(new SKRect(x - 1, y - 1, x + w + 1, y + h + 1), 9);
            canvas.DrawRoundRect(glowRect, _strokePaint);
        }
    }

    private static void DrawBranchBar(SKCanvas canvas, float x, float y, float h, ResearchBranch branch)
    {
        _fillPaint.Color = GetBranchColor(branch);
        var barRect = new SKRoundRect(new SKRect(x, y, x + 5, y + h), 4, 0);
        // Nur links abgerundet
        barRect = new SKRoundRect(new SKRect(x, y + 4, x + 5, y + h - 4), 2);
        canvas.DrawRoundRect(barRect, _fillPaint);
    }

    private static void DrawEffectIcon(SKCanvas canvas, float cx, float cy, float size,
        ResearchEffect effect, ResearchBranch branch)
    {
        // Kreisförmiger Hintergrund
        _fillPaint.Color = IconBgColor;
        canvas.DrawCircle(cx, cy, size * 0.55f, _fillPaint);

        // Farbiger Rand
        _strokePaint.Color = GetBranchColor(branch).WithAlpha(180);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawCircle(cx, cy, size * 0.55f, _strokePaint);

        // Icon basierend auf dem dominanten Effekt zeichnen
        _fillPaint.Color = GetBranchColor(branch);
        _strokePaint.Color = GetBranchColor(branch);
        _strokePaint.StrokeWidth = size * 0.1f;

        DrawEffectSymbol(canvas, cx, cy, size * 0.35f, effect, branch);
    }

    /// <summary>
    /// Zeichnet das Symbol für den Effekt-Typ der Forschung.
    /// </summary>
    private static void DrawEffectSymbol(SKCanvas canvas, float cx, float cy, float s,
        ResearchEffect effect, ResearchBranch branch)
    {
        var color = GetBranchColor(branch);
        _fillPaint.Color = color;
        _strokePaint.Color = color;
        _strokePaint.StrokeWidth = s * 0.18f;

        // Erkennung des dominanten Effekts
        if (effect.UnlocksAutoMaterial)
        {
            // Roboter-Arm: Winkelform
            DrawRobotArm(canvas, cx, cy, s);
        }
        else if (effect.UnlocksHeadhunter)
        {
            // Lupe + Person
            DrawHeadhunter(canvas, cx, cy, s);
        }
        else if (effect.UnlocksSTierWorkers)
        {
            // Stern-S
            DrawStarS(canvas, cx, cy, s);
        }
        else if (effect.UnlocksAutoAssign)
        {
            // Routing-Netz
            DrawAutoAssign(canvas, cx, cy, s);
        }
        else if (effect.EfficiencyBonus > 0)
        {
            // Zahnrad
            DrawGearIcon(canvas, cx, cy, s);
        }
        else if (effect.CostReduction > 0)
        {
            // Münze mit Pfeil runter
            DrawCostReduction(canvas, cx, cy, s);
        }
        else if (effect.MiniGameZoneBonus > 0)
        {
            // Puzzle-Stück
            DrawPuzzle(canvas, cx, cy, s);
        }
        else if (effect.WageReduction > 0)
        {
            // Münze mit Minus
            DrawWageReduction(canvas, cx, cy, s);
        }
        else if (effect.ExtraWorkerSlots > 0)
        {
            // Plus-Person
            DrawExtraWorker(canvas, cx, cy, s);
        }
        else if (effect.TrainingSpeedMultiplier > 0)
        {
            // Beschleunigte Uhr
            DrawTrainingSpeed(canvas, cx, cy, s);
        }
        else if (effect.RewardMultiplier > 0)
        {
            // Doppelpfeil hoch
            DrawRewardMultiplier(canvas, cx, cy, s);
        }
        else if (effect.ExtraOrderSlots > 0)
        {
            // Kiste mit Plus
            DrawExtraOrders(canvas, cx, cy, s);
        }
        else
        {
            // Fallback: Fragezeichen
            DrawFallbackIcon(canvas, cx, cy, s);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EFFEKT-ICONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Zahnrad (EfficiencyBonus)</summary>
    private static void DrawGearIcon(SKCanvas canvas, float cx, float cy, float s)
    {
        // Äußerer Ring
        canvas.DrawCircle(cx, cy, s * 0.7f, _strokePaint);

        // Innerer Punkt
        canvas.DrawCircle(cx, cy, s * 0.2f, _fillPaint);

        // 6 Zähne
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3;
            float outerR = s * 0.9f;
            float innerR = s * 0.65f;
            float tx = cx + MathF.Cos(angle) * innerR;
            float ty = cy + MathF.Sin(angle) * innerR;
            float ox = cx + MathF.Cos(angle) * outerR;
            float oy = cy + MathF.Sin(angle) * outerR;
            canvas.DrawLine(tx, ty, ox, oy, _strokePaint);
        }
    }

    /// <summary>Münze mit Pfeil runter (CostReduction)</summary>
    private static void DrawCostReduction(SKCanvas canvas, float cx, float cy, float s)
    {
        // Münze (Kreis)
        canvas.DrawCircle(cx - s * 0.15f, cy, s * 0.5f, _strokePaint);

        // Euro-Zeichen in der Münze
        _fontRegular.Size = s * 0.8f;
        _fontRegular.Embolden = false;
        canvas.DrawText("\u20ac", cx - s * 0.35f, cy + s * 0.25f, SKTextAlign.Center, _fontRegular, _fillPaint);

        // Pfeil runter (rechts)
        float ax = cx + s * 0.5f;
        canvas.DrawLine(ax, cy - s * 0.4f, ax, cy + s * 0.4f, _strokePaint);
        canvas.DrawLine(ax - s * 0.2f, cy + s * 0.15f, ax, cy + s * 0.4f, _strokePaint);
        canvas.DrawLine(ax + s * 0.2f, cy + s * 0.15f, ax, cy + s * 0.4f, _strokePaint);
    }

    /// <summary>Puzzle-Stück (MiniGameZoneBonus)</summary>
    private static void DrawPuzzle(SKCanvas canvas, float cx, float cy, float s)
    {
        // Quadrat-Basis
        float half = s * 0.5f;
        canvas.DrawRect(cx - half, cy - half, s, s, _strokePaint);

        // Puzzle-Nase oben
        canvas.DrawCircle(cx, cy - half, s * 0.2f, _fillPaint);

        // Puzzle-Nase rechts
        canvas.DrawCircle(cx + half, cy, s * 0.2f, _fillPaint);
    }

    /// <summary>Münze mit Minus (WageReduction)</summary>
    private static void DrawWageReduction(SKCanvas canvas, float cx, float cy, float s)
    {
        // Münze
        canvas.DrawCircle(cx, cy, s * 0.55f, _strokePaint);

        // Minus-Zeichen
        canvas.DrawLine(cx - s * 0.3f, cy, cx + s * 0.3f, cy, _strokePaint);
    }

    /// <summary>Plus-Person (ExtraWorkerSlots)</summary>
    private static void DrawExtraWorker(SKCanvas canvas, float cx, float cy, float s)
    {
        // Kopf
        canvas.DrawCircle(cx - s * 0.15f, cy - s * 0.35f, s * 0.25f, _fillPaint);

        // Körper
        canvas.DrawLine(cx - s * 0.15f, cy - s * 0.1f, cx - s * 0.15f, cy + s * 0.4f, _strokePaint);

        // Plus rechts
        float px = cx + s * 0.45f;
        float py = cy - s * 0.1f;
        canvas.DrawLine(px - s * 0.2f, py, px + s * 0.2f, py, _strokePaint);
        canvas.DrawLine(px, py - s * 0.2f, px, py + s * 0.2f, _strokePaint);
    }

    /// <summary>Beschleunigte Uhr (TrainingSpeedMultiplier)</summary>
    private static void DrawTrainingSpeed(SKCanvas canvas, float cx, float cy, float s)
    {
        // Uhr-Kreis
        canvas.DrawCircle(cx, cy, s * 0.6f, _strokePaint);

        // Zeiger (schnell = schräg nach rechts-oben)
        canvas.DrawLine(cx, cy, cx + s * 0.35f, cy - s * 0.25f, _strokePaint);
        canvas.DrawLine(cx, cy, cx, cy - s * 0.4f, _strokePaint);

        // Geschwindigkeits-Linien rechts
        for (int i = 0; i < 3; i++)
        {
            float lx = cx + s * 0.7f + i * s * 0.12f;
            float ly = cy - s * 0.2f + i * s * 0.2f;
            canvas.DrawLine(lx, ly, lx + s * 0.2f, ly, _strokePaint);
        }
    }

    /// <summary>Doppelpfeil hoch (RewardMultiplier)</summary>
    private static void DrawRewardMultiplier(SKCanvas canvas, float cx, float cy, float s)
    {
        // Linker Pfeil hoch
        float lx = cx - s * 0.2f;
        canvas.DrawLine(lx, cy + s * 0.5f, lx, cy - s * 0.5f, _strokePaint);
        canvas.DrawLine(lx - s * 0.2f, cy - s * 0.2f, lx, cy - s * 0.5f, _strokePaint);
        canvas.DrawLine(lx + s * 0.2f, cy - s * 0.2f, lx, cy - s * 0.5f, _strokePaint);

        // Rechter Pfeil hoch
        float rx = cx + s * 0.2f;
        canvas.DrawLine(rx, cy + s * 0.5f, rx, cy - s * 0.5f, _strokePaint);
        canvas.DrawLine(rx - s * 0.2f, cy - s * 0.2f, rx, cy - s * 0.5f, _strokePaint);
        canvas.DrawLine(rx + s * 0.2f, cy - s * 0.2f, rx, cy - s * 0.5f, _strokePaint);
    }

    /// <summary>Kiste mit Plus (ExtraOrderSlots)</summary>
    private static void DrawExtraOrders(SKCanvas canvas, float cx, float cy, float s)
    {
        // Kiste (Rechteck)
        float half = s * 0.55f;
        canvas.DrawRect(cx - half, cy - half * 0.7f, half * 2, half * 1.4f, _strokePaint);

        // Deckel
        canvas.DrawLine(cx - half * 1.1f, cy - half * 0.7f, cx + half * 1.1f, cy - half * 0.7f, _strokePaint);

        // Plus in der Kiste
        canvas.DrawLine(cx - s * 0.2f, cy + s * 0.1f, cx + s * 0.2f, cy + s * 0.1f, _strokePaint);
        canvas.DrawLine(cx, cy - s * 0.1f, cx, cy + s * 0.3f, _strokePaint);
    }

    /// <summary>Roboter-Arm (UnlocksAutoMaterial)</summary>
    private static void DrawRobotArm(SKCanvas canvas, float cx, float cy, float s)
    {
        // Basis
        canvas.DrawRect(cx - s * 0.3f, cy + s * 0.3f, s * 0.6f, s * 0.2f, _fillPaint);

        // Arm-Segment 1 (nach oben)
        canvas.DrawLine(cx, cy + s * 0.3f, cx - s * 0.2f, cy - s * 0.1f, _strokePaint);

        // Arm-Segment 2 (nach rechts oben)
        canvas.DrawLine(cx - s * 0.2f, cy - s * 0.1f, cx + s * 0.3f, cy - s * 0.5f, _strokePaint);

        // Greifer
        canvas.DrawLine(cx + s * 0.3f, cy - s * 0.5f, cx + s * 0.15f, cy - s * 0.35f, _strokePaint);
        canvas.DrawLine(cx + s * 0.3f, cy - s * 0.5f, cx + s * 0.45f, cy - s * 0.35f, _strokePaint);
    }

    /// <summary>Lupe + Person (UnlocksHeadhunter)</summary>
    private static void DrawHeadhunter(SKCanvas canvas, float cx, float cy, float s)
    {
        // Lupe (Kreis + Stiel)
        canvas.DrawCircle(cx - s * 0.1f, cy - s * 0.15f, s * 0.35f, _strokePaint);
        canvas.DrawLine(cx + s * 0.15f, cy + s * 0.15f, cx + s * 0.5f, cy + s * 0.5f, _strokePaint);

        // Kleine Person in der Lupe
        canvas.DrawCircle(cx - s * 0.1f, cy - s * 0.25f, s * 0.12f, _fillPaint);
        canvas.DrawLine(cx - s * 0.1f, cy - s * 0.13f, cx - s * 0.1f, cy + s * 0.05f, _strokePaint);
    }

    /// <summary>Stern-S (UnlocksSTierWorkers)</summary>
    private static void DrawStarS(SKCanvas canvas, float cx, float cy, float s)
    {
        // 5-zackiger Stern
        _cachedPath.Reset();
        for (int i = 0; i < 5; i++)
        {
            float outerAngle = -MathF.PI / 2 + i * MathF.Tau / 5;
            float innerAngle = outerAngle + MathF.Tau / 10;
            float outerR = s * 0.7f;
            float innerR = s * 0.3f;

            float ox = cx + MathF.Cos(outerAngle) * outerR;
            float oy = cy + MathF.Sin(outerAngle) * outerR;
            float ix = cx + MathF.Cos(innerAngle) * innerR;
            float iy = cy + MathF.Sin(innerAngle) * innerR;

            if (i == 0) _cachedPath.MoveTo(ox, oy);
            else _cachedPath.LineTo(ox, oy);
            _cachedPath.LineTo(ix, iy);
        }
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillPaint);
    }

    /// <summary>Routing-Netz (UnlocksAutoAssign)</summary>
    private static void DrawAutoAssign(SKCanvas canvas, float cx, float cy, float s)
    {
        // 3 Punkte + Verbindungslinien (Netzwerk)
        float r = s * 0.15f;

        // Zentrum
        canvas.DrawCircle(cx, cy, r, _fillPaint);

        // Oben-links
        float ax = cx - s * 0.5f, ay = cy - s * 0.4f;
        canvas.DrawCircle(ax, ay, r, _fillPaint);
        canvas.DrawLine(cx, cy, ax, ay, _strokePaint);

        // Oben-rechts
        float bx = cx + s * 0.5f, by = cy - s * 0.4f;
        canvas.DrawCircle(bx, by, r, _fillPaint);
        canvas.DrawLine(cx, cy, bx, by, _strokePaint);

        // Unten
        float dx = cx, dy = cy + s * 0.5f;
        canvas.DrawCircle(dx, dy, r, _fillPaint);
        canvas.DrawLine(cx, cy, dx, dy, _strokePaint);
    }

    /// <summary>Fragezeichen (Fallback)</summary>
    private static void DrawFallbackIcon(SKCanvas canvas, float cx, float cy, float s)
    {
        _fontRegular.Size = s * 1.4f;
        _fontRegular.Embolden = false;
        canvas.DrawText("?", cx, cy + s * 0.35f, SKTextAlign.Center, _fontRegular, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEXT & UI
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawLevelBadge(SKCanvas canvas, float x, float y, int level, ResearchBranch branch)
    {
        // Badge-Hintergrund
        var badgeRect = new SKRoundRect(new SKRect(x, y, x + 34, y + 18), 4);
        _fillPaint.Color = GetBranchColor(branch);
        canvas.DrawRoundRect(badgeRect, _fillPaint);

        // Level-Text
        _fontBold.Size = 11;
        _textPaint.Color = SKColors.White;
        canvas.DrawText($"Lv.{level}", x + 17, y + 13, SKTextAlign.Center, _fontBold, _textPaint);
    }

    private static void DrawName(SKCanvas canvas, float x, float y, float maxWidth, string name, bool isLocked)
    {
        _fontBold.Size = 14;
        _textPaint.Color = isLocked ? TextMuted : TextPrimary;

        // Text abschneiden wenn zu lang
        string displayText = TruncateText(name, _fontBold, maxWidth);
        canvas.DrawText(displayText, x, y + 12, _fontBold, _textPaint);
    }

    private static void DrawDescription(SKCanvas canvas, float x, float y, float maxWidth, string desc, bool isLocked)
    {
        _fontRegular.Size = 11;
        _fontRegular.Embolden = false;
        _textPaint.Color = isLocked ? TextMuted : TextSecondary;

        string displayText = TruncateText(desc, _fontRegular, maxWidth);
        canvas.DrawText(displayText, x, y + 10, _fontRegular, _textPaint);
    }

    private static void DrawStatusIcon(SKCanvas canvas, float x, float y, ResearchDisplayItem item, float time)
    {
        if (item.IsResearched)
        {
            // Grüner Check-Kreis mit Glow
            float glowAlpha = 0.4f + MathF.Sin(time * 1.5f) * 0.15f;
            _fillPaint.Color = SuccessColor.WithAlpha((byte)(glowAlpha * 255));
            canvas.DrawCircle(x + 10, y + 10, 14, _fillPaint);

            _fillPaint.Color = SuccessColor;
            canvas.DrawCircle(x + 10, y + 10, 10, _fillPaint);

            // Häkchen
            _strokePaint.Color = SKColors.White;
            _strokePaint.StrokeWidth = 2;
            canvas.DrawLine(x + 5, y + 10, x + 9, y + 14, _strokePaint);
            canvas.DrawLine(x + 9, y + 14, x + 16, y + 6, _strokePaint);
        }
        else if (item.IsLocked)
        {
            // Schloss-Icon
            _strokePaint.Color = TextMuted;
            _strokePaint.StrokeWidth = 1.5f;

            // Bügel
            canvas.DrawArc(new SKRect(x + 4, y + 2, x + 16, y + 12), 180, 180, false, _strokePaint);

            // Schloss-Körper
            _fillPaint.Color = TextMuted;
            canvas.DrawRect(x + 3, y + 10, 14, 10, _fillPaint);

            // Schlüsselloch
            _fillPaint.Color = CardBgLocked;
            canvas.DrawCircle(x + 10, y + 15, 2, _fillPaint);
        }
        else if (item.IsActive)
        {
            // Pulsierender Kreis
            float pulse = 0.7f + MathF.Sin(time * 4f) * 0.3f;
            _fillPaint.Color = WarningColor.WithAlpha((byte)(pulse * 200));
            canvas.DrawCircle(x + 10, y + 10, 10 * pulse, _fillPaint);

            // Sanduhr
            _strokePaint.Color = SKColors.White;
            _strokePaint.StrokeWidth = 1.5f;
            canvas.DrawLine(x + 5, y + 5, x + 15, y + 5, _strokePaint);
            canvas.DrawLine(x + 5, y + 15, x + 15, y + 15, _strokePaint);
            canvas.DrawLine(x + 7, y + 5, x + 10, y + 10, _strokePaint);
            canvas.DrawLine(x + 13, y + 5, x + 10, y + 10, _strokePaint);
            canvas.DrawLine(x + 7, y + 15, x + 10, y + 10, _strokePaint);
            canvas.DrawLine(x + 13, y + 15, x + 10, y + 10, _strokePaint);
        }
    }

    private static void DrawCostAndDuration(SKCanvas canvas, float x, float y, ResearchDisplayItem item)
    {
        _fontBold.Size = 11;
        _fontRegular.Size = 11;
        _fontRegular.Embolden = false;

        // Kosten (Euro)
        _textPaint.Color = WarningColor;
        string costText = $"\u20ac {item.CostDisplay}";
        canvas.DrawText(costText, x, y + 10, _fontBold, _textPaint);

        float costWidth = _fontBold.MeasureText(costText);

        // Dauer (Uhr)
        _textPaint.Color = TextSecondary;
        canvas.DrawText($"\u23f0 {item.DurationDisplay}", x + costWidth + 16, y + 10, _fontRegular, _textPaint);

        // Goldschrauben für Sofort-Finish (wenn verfügbar)
        if (item.HasInstantFinishOption)
        {
            float screwX = x + costWidth + 16 + _fontRegular.MeasureText($"\u23f0 {item.DurationDisplay}") + 16;
            _textPaint.Color = GoldColor;
            canvas.DrawText($"\ud83d\udd29 {item.InstantFinishScrewCost}", screwX, y + 10, _fontRegular, _textPaint);
        }
    }

    private static void DrawProgressBar(SKCanvas canvas, float x, float y, float w, float h,
        float progress, ResearchBranch branch, float time)
    {
        // Hintergrund
        _fillPaint.Color = new SKColor(0x20, 0x15, 0x12);
        var bgRect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 2);
        canvas.DrawRoundRect(bgRect, _fillPaint);

        // Fortschritt
        float fillW = w * Math.Clamp(progress, 0, 1);
        if (fillW > 0)
        {
            _fillPaint.Color = GetBranchColor(branch);
            var fillRect = new SKRoundRect(new SKRect(x, y, x + fillW, y + h), 2);
            canvas.DrawRoundRect(fillRect, _fillPaint);

            // Glow am Ende
            float glowPulse = 0.5f + MathF.Sin(time * 4f) * 0.5f;
            _fillPaint.Color = GetBranchColor(branch).WithAlpha((byte)(glowPulse * 120));
            canvas.DrawCircle(x + fillW, y + h / 2, h + 2, _fillPaint);
        }
    }

    private static void DrawStartReadyShimmer(SKCanvas canvas, float x, float y, float w, float h,
        ResearchBranch branch, float time)
    {
        // Wandernder Lichtstreifen über die Karte
        float shimmerPhase = (time * 0.5f) % 1.0f;
        float shimmerX = x + shimmerPhase * (w + 40) - 20;

        var shimmerColor = GetBranchColor(branch).WithAlpha(25);
        _fillPaint.Color = shimmerColor;

        // Schmaler leuchtender Streifen
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(x, y, x + w, y + h), 8));
        canvas.DrawRect(shimmerX - 15, y, 30, h, _fillPaint);
        canvas.Restore();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die Branch-Farbe zurück.
    /// </summary>
    public static SKColor GetBranchColor(ResearchBranch branch) => branch switch
    {
        ResearchBranch.Tools => ToolsColor,
        ResearchBranch.Management => ManagementColor,
        ResearchBranch.Marketing => MarketingColor,
        _ => ToolsColor
    };

    private static string TruncateText(string text, SKFont font, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (font.MeasureText(text) <= maxWidth) return text;

        // Binäre Suche nach der maximalen Länge
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (font.MeasureText(text[..mid] + "...") <= maxWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo > 0 ? text[..lo] + "..." : "...";
    }
}
