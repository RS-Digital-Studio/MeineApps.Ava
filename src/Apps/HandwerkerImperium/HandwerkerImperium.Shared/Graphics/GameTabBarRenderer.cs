using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Daten die pro Frame an den Tab-Bar-Renderer übergeben werden.
/// </summary>
public struct TabBarState
{
    /// <summary>Aktuell aktiver Tab (0-4).</summary>
    public int ActiveTab;

    /// <summary>Notification-Badge-Zähler pro Tab (5 Einträge).</summary>
    public int[] BadgeCounts;

    /// <summary>Lokalisierte Tab-Titel (5 Einträge).</summary>
    public string[] Labels;

    /// <summary>Sekunden seit App-Start (für Animationen).</summary>
    public float Time;

    /// <summary>Zeitpunkt des letzten Tab-Wechsels in Sekunden seit Start.</summary>
    public float TabSwitchTime;
}

/// <summary>
/// SkiaSharp-basierter Tab-Bar-Renderer im Handwerker-Stil.
/// Zeichnet eine Holz-Textur mit Metall-Nieten, 5 illustrierte Icons,
/// goldenen Indikator, Notification-Badges und Bounce-Animationen.
/// Ersetzt die XAML-Tab-Bar durch eine visuell ansprechendere Darstellung.
/// </summary>
public class GameTabBarRenderer
{
    // Anzahl der Tabs
    private const int TabCount = 5;

    // Höhe der Tab-Bar in dp
    private const float BarHeight = 68f;

    // Abgerundete Ecken oben
    private const float CornerRadius = 12f;

    // ═══════════════════════════════════════════════════════════════════
    // FARBEN
    // ═══════════════════════════════════════════════════════════════════

    // Holz-Hintergrund
    private static readonly SKColor WoodBase = new(0x5D, 0x40, 0x37);
    private static readonly SKColor WoodLight = new(0x6D, 0x4C, 0x41);
    private static readonly SKColor WoodDark = new(0x4E, 0x34, 0x2E);

    // Nieten
    private static readonly SKColor RivetBase = new(0x78, 0x90, 0x9C);
    private static readonly SKColor RivetHighlight = new(0xB0, 0xBE, 0xC5);
    private static readonly SKColor RivetShadow = new(0x45, 0x5A, 0x64);

    // Aktiver Tab
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor GoldGlow = new(0xFF, 0xD7, 0x00, 0x60);
    private static readonly SKColor LabelActive = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor LabelInactive = new(0x9E, 0x9E, 0x9E);

    // Badge
    private static readonly SKColor BadgeRed = new(0xE5, 0x3E, 0x3E);
    private static readonly SKColor BadgeWhite = SKColors.White;

    // Icon-Farben
    private static readonly SKColor HouseRoof = new(0x8B, 0x45, 0x13);
    private static readonly SKColor HouseRoofEdge = new(0xDC, 0x26, 0x26);
    private static readonly SKColor HouseWall = new(0xD2, 0xB4, 0x8C);
    private static readonly SKColor HouseDoor = new(0x5D, 0x40, 0x37);
    private static readonly SKColor HouseWindow = new(0xFF, 0xEB, 0x3B);
    private static readonly SKColor SmokeGray = new(0x9E, 0x9E, 0x9E, 0x80);

    private static readonly SKColor Building1 = new(0x6D, 0x4C, 0x41);
    private static readonly SKColor Building2 = new(0x8D, 0x6E, 0x63);
    private static readonly SKColor Building3 = new(0xA1, 0x88, 0x7F);
    private static readonly SKColor CraneColor = new(0xEA, 0x58, 0x0C);

    private static readonly SKColor ShieldColor = new(0xB4, 0x53, 0x09);
    private static readonly SKColor HammerWhite = new(0xF5, 0xF5, 0xF5);
    private static readonly SKColor LaurelGreen = new(0x4C, 0xAF, 0x50);

    private static readonly SKColor ShopWood = new(0x8B, 0x69, 0x14);
    private static readonly SKColor AwningRed = new(0xDC, 0x26, 0x26);
    private static readonly SKColor CoinGold = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor CoinDark = new(0xB7, 0x8C, 0x00);

    private static readonly SKColor GearColor = new(0x78, 0x71, 0x6C);
    private static readonly SKColor GearHandle = new(0x8B, 0x45, 0x13);

    // Imperium-Tab
    private static readonly SKColor CrownGold = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor CrownDark = new(0xB7, 0x8C, 0x00);
    private static readonly SKColor TowerStone = new(0x8D, 0x6E, 0x63);
    private static readonly SKColor TowerDark = new(0x5D, 0x40, 0x37);

    // Missionen-Tab
    private static readonly SKColor ClipboardWood = new(0x8B, 0x69, 0x14);
    private static readonly SKColor ClipboardPaper = new(0xF5, 0xF5, 0xDC);
    private static readonly SKColor CheckGreen = new(0x4C, 0xAF, 0x50);

    // ═══════════════════════════════════════════════════════════════════
    // GECACHTE PAINTS (kein new im Render-Loop)
    // ═══════════════════════════════════════════════════════════════════

    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, TextAlign = SKTextAlign.Center };
    private readonly SKPaint _shadowPaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0x00, 0x00, 0x00, 0x50),
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
    };
    private readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
    };

    // Gecachte Pfade für Icons (werden beim ersten Render erstellt)
    private SKPath? _shieldPath;
    private SKPath? _gearPath;

    // ═══════════════════════════════════════════════════════════════════
    // HAUPT-RENDER-METHODE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rendert die komplette Tab-Bar auf den Canvas.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas.</param>
    /// <param name="bounds">Verfügbarer Zeichenbereich (gesamte Canvas-Fläche).</param>
    /// <param name="state">Aktueller Frame-Zustand mit Tabs, Badges, Labels, Zeit.</param>
    public void Render(SKCanvas canvas, SKRect bounds, TabBarState state)
    {
        float barTop = bounds.Bottom - BarHeight;
        var barBounds = new SKRect(bounds.Left, barTop, bounds.Right, bounds.Bottom);
        float tabWidth = barBounds.Width / TabCount;

        // 1. Schatten nach oben
        DrawTopShadow(canvas, barBounds);

        // 2. Holz-Hintergrund mit reicher Maserung (CraftTextures)
        DrawWoodWithCraftTexture(canvas, barBounds);

        // 4. Metall-Nieten in den 4 Ecken
        DrawRivets(canvas, barBounds);

        // 5. Goldener Unterstrich für aktiven Tab
        DrawActiveIndicator(canvas, barBounds, tabWidth, state);

        // 6. Icons + Labels + Badges
        for (int i = 0; i < TabCount; i++)
        {
            float tabCenterX = barBounds.Left + tabWidth * i + tabWidth / 2f;
            float iconCenterY = barBounds.Top + 22f;
            float labelY = barBounds.Bottom - 10f;

            bool isActive = i == state.ActiveTab;

            // Bounce-Animation bei Tab-Wechsel
            float scale = 1.0f;
            if (isActive)
            {
                float elapsed = state.Time - state.TabSwitchTime;
                if (elapsed < 0.2f)
                {
                    // 0→1 über 200ms
                    float t = Math.Clamp(elapsed / 0.2f, 0f, 1f);
                    float eased = EasingFunctions.EaseOutBack(t);
                    scale = EasingFunctions.Lerp(0.85f, 1.1f, eased);
                }
                else
                {
                    scale = 1.1f;
                }
            }

            // Icon zeichnen
            canvas.Save();
            canvas.Translate(tabCenterX, iconCenterY);
            canvas.Scale(scale);
            canvas.Translate(-tabCenterX, -iconCenterY);

            DrawTabIcon(canvas, i, tabCenterX, iconCenterY, 40f, isActive, state.Time);

            canvas.Restore();

            // Label
            DrawLabel(canvas, state.Labels != null && i < state.Labels.Length
                ? state.Labels[i] : "", tabCenterX, labelY, isActive);

            // Badge
            if (state.BadgeCounts != null && i < state.BadgeCounts.Length && state.BadgeCounts[i] > 0)
            {
                DrawBadge(canvas, state.BadgeCounts[i], tabCenterX + 14f, iconCenterY - 14f, state.Time);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // HIT-TEST
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bestimmt welcher Tab bei der gegebenen Position getroffen wurde.
    /// </summary>
    /// <param name="bounds">Gesamte Canvas-Bounds.</param>
    /// <param name="x">X-Koordinate des Touch/Klick.</param>
    /// <param name="y">Y-Koordinate des Touch/Klick.</param>
    /// <returns>Tab-Index (0-4) oder -1 wenn kein Tab getroffen.</returns>
    public int HitTest(SKRect bounds, float x, float y)
    {
        float barTop = bounds.Bottom - BarHeight;

        // Außerhalb der Tab-Bar?
        if (y < barTop || y > bounds.Bottom || x < bounds.Left || x > bounds.Right)
            return -1;

        float tabWidth = bounds.Width / TabCount;
        int index = (int)((x - bounds.Left) / tabWidth);
        return Math.Clamp(index, 0, TabCount - 1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HOLZ-HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den Schatten oberhalb der Tab-Bar für Tiefenwirkung.
    /// </summary>
    private void DrawTopShadow(SKCanvas canvas, SKRect barBounds)
    {
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 0x40);
        canvas.DrawRect(barBounds.Left, barBounds.Top - 4, barBounds.Width, 6, _shadowPaint);
    }

    /// <summary>
    /// Zeichnet Holz-Hintergrund mit CraftTextures-Maserung (Wellenlinien, Astlöcher, Gradient).
    /// Clippt auf abgerundete Ecken oben.
    /// </summary>
    private void DrawWoodWithCraftTexture(SKCanvas canvas, SKRect barBounds)
    {
        canvas.Save();

        // Clip auf abgerundete Ecken oben
        SKPoint[] radii =
        [
            new(CornerRadius, CornerRadius),   // oben-links
            new(CornerRadius, CornerRadius),   // oben-rechts
            new(0, 0),                          // unten-rechts
            new(0, 0)                           // unten-links
        ];
        using var rrect = new SKRoundRect();
        rrect.SetRectRadii(barBounds, radii);
        canvas.ClipRoundRect(rrect);

        // Reiche Holztextur via CraftTextures (Gradient, Sinus-Wellenlinien, Astlöcher)
        CraftTextures.DrawWoodGrain(canvas, barBounds, WoodBase, 0.5f);

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet 4 Metall-Nieten in den Ecken der Tab-Bar.
    /// Jede Niete hat Highlight oben-links und Schatten unten-rechts.
    /// </summary>
    private void DrawRivets(SKCanvas canvas, SKRect barBounds)
    {
        float rivetRadius = 3f;
        float margin = 8f;

        // 4 Positionen: oben-links, oben-rechts, unten-links, unten-rechts
        float[] rivetX =
        [
            barBounds.Left + margin,
            barBounds.Right - margin,
            barBounds.Left + margin,
            barBounds.Right - margin
        ];
        float[] rivetY =
        [
            barBounds.Top + margin,
            barBounds.Top + margin,
            barBounds.Bottom - margin,
            barBounds.Bottom - margin
        ];

        for (int i = 0; i < 4; i++)
        {
            float cx = rivetX[i];
            float cy = rivetY[i];

            // Nieten-Körper
            _fillPaint.Color = RivetBase;
            _fillPaint.Shader = null;
            canvas.DrawCircle(cx, cy, rivetRadius, _fillPaint);

            // Highlight oben-links (Lichtquelle simulieren)
            _fillPaint.Color = RivetHighlight;
            canvas.DrawCircle(cx - 1f, cy - 1f, rivetRadius * 0.5f, _fillPaint);

            // Schatten unten-rechts
            _fillPaint.Color = RivetShadow;
            canvas.DrawCircle(cx + 0.8f, cy + 0.8f, rivetRadius * 0.4f, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // AKTIVER TAB INDIKATOR
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den goldenen Unterstrich mit Glow unter dem aktiven Tab-Icon.
    /// </summary>
    private void DrawActiveIndicator(SKCanvas canvas, SKRect barBounds, float tabWidth, TabBarState state)
    {
        float indicatorWidth = 32f;
        float indicatorHeight = 4f;
        float centerX = barBounds.Left + tabWidth * state.ActiveTab + tabWidth / 2f;
        float indicatorY = barBounds.Top + 44f;

        var indicatorRect = new SKRect(
            centerX - indicatorWidth / 2f,
            indicatorY,
            centerX + indicatorWidth / 2f,
            indicatorY + indicatorHeight);

        // Glow-Aura (breiter, transparent)
        _glowPaint.Color = GoldGlow;
        canvas.DrawRoundRect(new SKRoundRect(indicatorRect, 2f), _glowPaint);

        // Goldener Strich
        _fillPaint.Color = GoldColor;
        _fillPaint.Shader = null;
        canvas.DrawRoundRect(new SKRoundRect(indicatorRect, 2f), _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // LABELS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet das Textlabel unter dem Tab-Icon.
    /// </summary>
    private void DrawLabel(SKCanvas canvas, string text, float centerX, float y, bool isActive)
    {
        if (string.IsNullOrEmpty(text)) return;

        _textPaint.Color = isActive ? LabelActive : LabelInactive;
        _textPaint.TextSize = 9f;
        _textPaint.FakeBoldText = isActive;

        canvas.DrawText(text, centerX, y, _textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // NOTIFICATION BADGES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen pulsierenden roten Notification-Badge mit weißer Zahl.
    /// Pulse-Animation: Scale oszilliert zwischen 0.9 und 1.1 (Sinus, 2Hz).
    /// </summary>
    private void DrawBadge(SKCanvas canvas, int count, float cx, float cy, float time)
    {
        float badgeRadius = 6f;

        // Pulse-Animation (2Hz Sinus zwischen 0.9 und 1.1)
        float pulse = 0.9f + 0.2f * (0.5f + 0.5f * MathF.Sin(time * 2f * MathF.PI * 2f));

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Scale(pulse);
        canvas.Translate(-cx, -cy);

        // Roter Kreis
        _fillPaint.Color = BadgeRed;
        _fillPaint.Shader = null;
        canvas.DrawCircle(cx, cy, badgeRadius, _fillPaint);

        // Weißer Rand für bessere Lesbarkeit
        _strokePaint.Color = WoodBase;
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawCircle(cx, cy, badgeRadius, _strokePaint);

        // Weiße Zahl
        _textPaint.Color = BadgeWhite;
        _textPaint.TextSize = 8f;
        _textPaint.FakeBoldText = true;

        string text = count > 99 ? "99+" : count.ToString();
        var textBounds = new SKRect();
        _textPaint.MeasureText(text, ref textBounds);
        canvas.DrawText(text, cx, cy + textBounds.Height * 0.35f, _textPaint);
        _textPaint.FakeBoldText = false;

        canvas.Restore();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON DISPATCHER
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet das passende Icon für den gegebenen Tab-Index.
    /// </summary>
    private void DrawTabIcon(SKCanvas canvas, int tabIndex, float cx, float cy,
        float size, bool isActive, float time)
    {
        switch (tabIndex)
        {
            case 0: DrawHomeIcon(canvas, cx, cy, size, isActive); break;
            case 1: DrawImperiumIcon(canvas, cx, cy, size, isActive); break;
            case 2: DrawMissionenIcon(canvas, cx, cy, size, isActive); break;
            case 3: DrawGuildIcon(canvas, cx, cy, size, isActive); break;
            case 4: DrawShopIcon(canvas, cx, cy, size, isActive); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: HOME (DASHBOARD) - Haus mit Schornstein + Rauch
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein einfaches Haus: Dreieck-Dach, Wand, Tür, leuchtendes Fenster,
    /// Schornstein mit 2 kleinen Rauchkreisen.
    /// </summary>
    private void DrawHomeIcon(SKCanvas canvas, float cx, float cy, float size, bool isActive)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        // --- Dach (Dreieck) ---
        using var roofPath = new SKPath();
        roofPath.MoveTo(cx, cy - s * 0.6f);              // Spitze
        roofPath.LineTo(cx - s * 0.65f, cy - s * 0.05f); // Links unten
        roofPath.LineTo(cx + s * 0.65f, cy - s * 0.05f); // Rechts unten
        roofPath.Close();

        _fillPaint.Color = HouseRoof.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawPath(roofPath, _fillPaint);

        // Rote Dachkante
        _strokePaint.Color = HouseRoofEdge.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawLine(cx - s * 0.6f, cy - s * 0.05f, cx, cy - s * 0.6f, _strokePaint);
        canvas.DrawLine(cx, cy - s * 0.6f, cx + s * 0.6f, cy - s * 0.05f, _strokePaint);

        // --- Wand (Rechteck) ---
        float wallLeft = cx - s * 0.5f;
        float wallTop = cy - s * 0.05f;
        float wallWidth = s * 1.0f;
        float wallHeight = s * 0.65f;

        _fillPaint.Color = HouseWall.WithAlpha(alpha);
        canvas.DrawRect(wallLeft, wallTop, wallWidth, wallHeight, _fillPaint);

        // --- Tür (Mitte unten) ---
        float doorW = s * 0.2f;
        float doorH = s * 0.35f;
        _fillPaint.Color = HouseDoor.WithAlpha(alpha);
        canvas.DrawRect(cx - doorW / 2f, wallTop + wallHeight - doorH, doorW, doorH, _fillPaint);

        // --- Fenster (links, gelb leuchtend) ---
        float winSize = s * 0.16f;
        _fillPaint.Color = HouseWindow.WithAlpha(alpha);
        canvas.DrawRect(cx - s * 0.35f, wallTop + s * 0.1f, winSize, winSize, _fillPaint);

        // Fensterkreuz
        _strokePaint.Color = HouseDoor.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 0.8f;
        float wCx = cx - s * 0.35f + winSize / 2f;
        float wCy = wallTop + s * 0.1f + winSize / 2f;
        canvas.DrawLine(wCx - winSize / 2f, wCy, wCx + winSize / 2f, wCy, _strokePaint);
        canvas.DrawLine(wCx, wCy - winSize / 2f, wCx, wCy + winSize / 2f, _strokePaint);

        // --- Schornstein ---
        float chimX = cx + s * 0.25f;
        float chimY = cy - s * 0.45f;
        _fillPaint.Color = HouseRoof.WithAlpha(alpha);
        canvas.DrawRect(chimX, chimY, s * 0.12f, s * 0.3f, _fillPaint);

        // Schornstein-Kappe
        _fillPaint.Color = WoodDark.WithAlpha(alpha);
        canvas.DrawRect(chimX - 1f, chimY - 2f, s * 0.14f, 3f, _fillPaint);

        // --- Rauch (2 graue Kreise über dem Schornstein) ---
        _fillPaint.Color = SmokeGray.WithAlpha((byte)(alpha * 0.5f));
        canvas.DrawCircle(chimX + s * 0.06f, chimY - 6f, 2.5f, _fillPaint);
        canvas.DrawCircle(chimX + s * 0.09f, chimY - 11f, 3.0f, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: BUILDINGS (GEBÄUDE) - 3 gestaffelte Rechtecke + Mini-Kran
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet 3 unterschiedlich hohe Gebäude mit Fenster-Raster
    /// und einem Mini-Kran rechts oben.
    /// </summary>
    private void DrawBuildingsIcon(SKCanvas canvas, float cx, float cy, float size, bool isActive)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        float baseY = cy + s * 0.5f;

        // --- Gebäude 1 (klein, links) ---
        float b1X = cx - s * 0.55f;
        float b1H = s * 0.5f;
        _fillPaint.Color = Building1.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawRect(b1X, baseY - b1H, s * 0.3f, b1H, _fillPaint);

        // Fenster Gebäude 1
        _fillPaint.Color = HouseWindow.WithAlpha((byte)(alpha * 0.7f));
        canvas.DrawRect(b1X + 3f, baseY - b1H + 4f, 4f, 4f, _fillPaint);
        canvas.DrawRect(b1X + 3f, baseY - b1H + 12f, 4f, 4f, _fillPaint);

        // --- Gebäude 2 (mittel, mitte) ---
        float b2X = cx - s * 0.2f;
        float b2H = s * 0.75f;
        _fillPaint.Color = Building2.WithAlpha(alpha);
        canvas.DrawRect(b2X, baseY - b2H, s * 0.35f, b2H, _fillPaint);

        // Fenster Gebäude 2 (2x3 Raster)
        _fillPaint.Color = HouseWindow.WithAlpha((byte)(alpha * 0.7f));
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                canvas.DrawRect(b2X + 3f + col * 8f, baseY - b2H + 4f + row * 9f, 4f, 4f, _fillPaint);
            }
        }

        // --- Gebäude 3 (groß, rechts) ---
        float b3X = cx + s * 0.2f;
        float b3H = s * 1.0f;
        _fillPaint.Color = Building3.WithAlpha(alpha);
        canvas.DrawRect(b3X, baseY - b3H, s * 0.3f, b3H, _fillPaint);

        // Fenster Gebäude 3 (2x4 Raster)
        _fillPaint.Color = HouseWindow.WithAlpha((byte)(alpha * 0.7f));
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                canvas.DrawRect(b3X + 2f + col * 7f, baseY - b3H + 4f + row * 8f, 3.5f, 3.5f, _fillPaint);
            }
        }

        // --- Mini-Kran (rechts oben) ---
        float craneX = cx + s * 0.45f;
        float craneY = cy - s * 0.55f;

        // Kran-Mast (vertikal)
        _strokePaint.Color = CraneColor.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawLine(craneX, craneY, craneX, craneY + s * 0.5f, _strokePaint);

        // Kran-Ausleger (horizontal)
        canvas.DrawLine(craneX - s * 0.25f, craneY, craneX + s * 0.05f, craneY, _strokePaint);

        // Kran-Dreieck (Abstützung)
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawLine(craneX, craneY + s * 0.15f, craneX - s * 0.15f, craneY, _strokePaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: IMPERIUM - Steinturm mit Krone
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen Steinturm mit 3-zackiger Krone, Zinnen, Fenster und Tür.
    /// Handwerker-Stil passend zu den anderen Tab-Icons.
    /// </summary>
    private void DrawImperiumIcon(SKCanvas canvas, float cx, float cy, float size, bool isActive)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        // --- Turm-Körper ---
        _fillPaint.Color = TowerStone.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawRoundRect(cx - s * 0.35f, cy - s * 0.2f, s * 0.7f, s * 0.8f, 2, 2, _fillPaint);

        // Turm-Schatten (dunklere rechte Seite für Tiefenwirkung)
        _fillPaint.Color = TowerDark.WithAlpha(alpha);
        canvas.DrawRect(cx + s * 0.1f, cy - s * 0.2f, s * 0.25f, s * 0.8f, _fillPaint);

        // --- Zinnen oben (3 Stück) ---
        float zinneW = s * 0.18f;
        float zinneH = s * 0.15f;
        float zinneY = cy - s * 0.2f - zinneH;
        _fillPaint.Color = TowerStone.WithAlpha(alpha);
        canvas.DrawRect(cx - s * 0.32f, zinneY, zinneW, zinneH, _fillPaint);
        canvas.DrawRect(cx - zinneW * 0.5f, zinneY, zinneW, zinneH, _fillPaint);
        canvas.DrawRect(cx + s * 0.32f - zinneW, zinneY, zinneW, zinneH, _fillPaint);

        // --- Krone (3 Zacken, über den Zinnen) ---
        float crownY = zinneY - s * 0.25f;
        _fillPaint.Color = CrownGold.WithAlpha(alpha);
        using var crownPath = new SKPath();
        crownPath.MoveTo(cx - s * 0.25f, zinneY);
        crownPath.LineTo(cx - s * 0.2f, crownY + s * 0.05f);
        crownPath.LineTo(cx - s * 0.07f, zinneY - s * 0.05f);
        crownPath.LineTo(cx, crownY);
        crownPath.LineTo(cx + s * 0.07f, zinneY - s * 0.05f);
        crownPath.LineTo(cx + s * 0.2f, crownY + s * 0.05f);
        crownPath.LineTo(cx + s * 0.25f, zinneY);
        crownPath.Close();
        canvas.DrawPath(crownPath, _fillPaint);

        // Kronen-Schatten (dunklere Unterseite)
        _strokePaint.Color = CrownDark.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawLine(cx - s * 0.25f, zinneY, cx + s * 0.25f, zinneY, _strokePaint);

        // --- Fenster (kleines Bogenfenster) ---
        _fillPaint.Color = HouseWindow.WithAlpha(alpha);
        canvas.DrawRoundRect(cx - s * 0.1f, cy + s * 0.0f, s * 0.2f, s * 0.22f, 3, 3, _fillPaint);

        // Fensterkreuz
        _strokePaint.Color = TowerDark.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 0.8f;
        canvas.DrawLine(cx, cy + s * 0.0f, cx, cy + s * 0.22f, _strokePaint);
        canvas.DrawLine(cx - s * 0.1f, cy + s * 0.11f, cx + s * 0.1f, cy + s * 0.11f, _strokePaint);

        // --- Tür (unten Mitte) ---
        _fillPaint.Color = HouseDoor.WithAlpha(alpha);
        canvas.DrawRoundRect(cx - s * 0.1f, cy + s * 0.35f, s * 0.2f, s * 0.25f, 2, 2, _fillPaint);

        // Tür-Griff
        _fillPaint.Color = CrownGold.WithAlpha(alpha);
        canvas.DrawCircle(cx + s * 0.04f, cy + s * 0.47f, 1.2f, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: MISSIONEN - Clipboard mit Haken
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Holz-Clipboard mit Papier-Fläche, Metallclip oben
    /// und 3 Haken-Zeilen (Checkmarks mit Linien).
    /// </summary>
    private void DrawMissionenIcon(SKCanvas canvas, float cx, float cy, float size, bool isActive)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        // --- Clipboard-Brett (Holz) ---
        _fillPaint.Color = ClipboardWood.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawRoundRect(cx - s * 0.38f, cy - s * 0.45f, s * 0.76f, s * 1.0f, 3, 3, _fillPaint);

        // --- Papier ---
        _fillPaint.Color = ClipboardPaper.WithAlpha(alpha);
        canvas.DrawRoundRect(cx - s * 0.3f, cy - s * 0.3f, s * 0.6f, s * 0.8f, 2, 2, _fillPaint);

        // --- Metallclip oben (Mitte) ---
        _fillPaint.Color = RivetBase.WithAlpha(alpha);
        canvas.DrawRoundRect(cx - s * 0.12f, cy - s * 0.5f, s * 0.24f, s * 0.15f, 2, 2, _fillPaint);
        // Clip-Highlight
        _fillPaint.Color = RivetHighlight.WithAlpha(alpha);
        canvas.DrawRoundRect(cx - s * 0.08f, cy - s * 0.48f, s * 0.16f, s * 0.05f, 1, 1, _fillPaint);

        // --- 3 Haken-Zeilen (Checkmarks + Linien) ---
        _strokePaint.Color = CheckGreen.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;

        float lineStartX = cx - s * 0.1f;
        float lineEndX = cx + s * 0.2f;

        for (int i = 0; i < 3; i++)
        {
            float lineY = cy - s * 0.12f + i * s * 0.22f;

            // Haken (Checkmark) - grüne Striche
            float checkX = cx - s * 0.22f;
            canvas.DrawLine(checkX, lineY, checkX + s * 0.06f, lineY + s * 0.06f, _strokePaint);
            canvas.DrawLine(checkX + s * 0.06f, lineY + s * 0.06f, checkX + s * 0.14f, lineY - s * 0.04f, _strokePaint);

            // Text-Linie (gedämpftes Grau)
            _fillPaint.Color = new SKColor(0xCC, 0xCC, 0xBB).WithAlpha(alpha);
            canvas.DrawRect(lineStartX, lineY, lineEndX - lineStartX, 2, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: GUILD (GILDE) - Wappen-Schild + gekreuzte Hämmer + Lorbeer
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine Wappen-Schild-Form mit 2 gekreuzten Hämmern in der Mitte
    /// und Lorbeerkranz-Andeutung links und rechts.
    /// </summary>
    private void DrawGuildIcon(SKCanvas canvas, float cx, float cy, float size, bool isActive)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        // --- Schild-Form (Pentagon/Wappen) ---
        _shieldPath ??= CreateShieldPath(0, 0, 1f);

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Scale(s * 0.7f);

        _fillPaint.Color = ShieldColor.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawPath(_shieldPath, _fillPaint);

        // Schild-Rand
        _strokePaint.Color = new SKColor(0xD4, 0xA0, 0x00).WithAlpha(alpha);
        _strokePaint.StrokeWidth = 2f / (s * 0.7f); // Skalierung kompensieren
        canvas.DrawPath(_shieldPath, _strokePaint);

        canvas.Restore();

        // --- 2 gekreuzte Hämmer ---
        float hammerLen = s * 0.35f;

        // Hammer 1 (links oben → rechts unten geneigt)
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(-35);

        _strokePaint.Color = HammerWhite.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(0, -hammerLen * 0.4f, 0, hammerLen * 0.5f, _strokePaint);

        // Hammer-Kopf
        _fillPaint.Color = HammerWhite.WithAlpha(alpha);
        canvas.DrawRect(-s * 0.1f, -hammerLen * 0.55f, s * 0.2f, s * 0.12f, _fillPaint);

        canvas.Restore();

        // Hammer 2 (gespiegelt)
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(35);

        _strokePaint.Color = HammerWhite.WithAlpha(alpha);
        canvas.DrawLine(0, -hammerLen * 0.4f, 0, hammerLen * 0.5f, _strokePaint);

        _fillPaint.Color = HammerWhite.WithAlpha(alpha);
        canvas.DrawRect(-s * 0.1f, -hammerLen * 0.55f, s * 0.2f, s * 0.12f, _fillPaint);

        canvas.Restore();

        // --- Lorbeerkranz-Andeutung (2 gebogene grüne Linien) ---
        _strokePaint.Color = LaurelGreen.WithAlpha((byte)(alpha * 0.7f));
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;

        // Linke Seite
        using var leftLaurel = new SKPath();
        leftLaurel.MoveTo(cx - s * 0.5f, cy + s * 0.35f);
        leftLaurel.QuadTo(cx - s * 0.65f, cy - s * 0.1f, cx - s * 0.3f, cy - s * 0.5f);
        canvas.DrawPath(leftLaurel, _strokePaint);

        // Rechte Seite
        using var rightLaurel = new SKPath();
        rightLaurel.MoveTo(cx + s * 0.5f, cy + s * 0.35f);
        rightLaurel.QuadTo(cx + s * 0.65f, cy - s * 0.1f, cx + s * 0.3f, cy - s * 0.5f);
        canvas.DrawPath(rightLaurel, _strokePaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: SHOP - Schaufenster + Markise + Münzen
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Holz-Schaufenster mit rot-weißer Markise oben
    /// und goldenen Münzen mit Euro-Zeichen im Fenster.
    /// </summary>
    private void DrawShopIcon(SKCanvas canvas, float cx, float cy, float size, bool isActive)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        float shopW = s * 0.9f;
        float shopH = s * 0.65f;
        float shopLeft = cx - shopW / 2f;
        float shopTop = cy - s * 0.15f;

        // --- Schaufenster (Holz-Rechteck) ---
        _fillPaint.Color = ShopWood.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(shopLeft, shopTop, shopLeft + shopW, shopTop + shopH), 2f), _fillPaint);

        // Fenster-Fläche (etwas dunkler)
        _fillPaint.Color = new SKColor(0x3E, 0x27, 0x23).WithAlpha(alpha);
        float winMargin = 2.5f;
        canvas.DrawRect(shopLeft + winMargin, shopTop + winMargin,
            shopW - winMargin * 2, shopH - winMargin * 2, _fillPaint);

        // --- Markise (rot mit weißen Streifen als 3 Dreiecke) ---
        float awningY = shopTop - 1f;
        float awningH = s * 0.25f;
        int stripes = 6;
        float stripeW = shopW / stripes;

        for (int i = 0; i < stripes; i++)
        {
            using var stripePath = new SKPath();
            float sx = shopLeft + i * stripeW;
            stripePath.MoveTo(sx, awningY);
            stripePath.LineTo(sx + stripeW, awningY);
            stripePath.LineTo(sx + stripeW / 2f, awningY + awningH);
            stripePath.Close();

            _fillPaint.Color = (i % 2 == 0 ? AwningRed : SKColors.White).WithAlpha(alpha);
            canvas.DrawPath(stripePath, _fillPaint);
        }

        // Markisen-Stange
        _strokePaint.Color = WoodDark.WithAlpha(alpha);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawLine(shopLeft, awningY, shopLeft + shopW, awningY, _strokePaint);

        // --- Münzen im Fenster (3 goldene Kreise mit €) ---
        float coinR = s * 0.1f;
        float coinY = shopTop + shopH * 0.5f;
        float[] coinXOffsets = [-0.2f, 0f, 0.2f];

        for (int i = 0; i < 3; i++)
        {
            float coinCx = cx + s * coinXOffsets[i];

            // Münz-Körper
            _fillPaint.Color = CoinGold.WithAlpha(alpha);
            canvas.DrawCircle(coinCx, coinY, coinR, _fillPaint);

            // Münz-Rand
            _strokePaint.Color = CoinDark.WithAlpha(alpha);
            _strokePaint.StrokeWidth = 0.8f;
            canvas.DrawCircle(coinCx, coinY, coinR * 0.75f, _strokePaint);

            // Euro-Zeichen
            _textPaint.Color = CoinDark.WithAlpha(alpha);
            _textPaint.TextSize = coinR * 1.0f;
            _textPaint.FakeBoldText = true;
            var tb = new SKRect();
            _textPaint.MeasureText("\u20AC", ref tb);
            canvas.DrawText("\u20AC", coinCx, coinY + tb.Height * 0.35f, _textPaint);
            _textPaint.FakeBoldText = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ICON: SETTINGS - Zahnrad + Holzgriff + optionale Rotation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Zahnrad mit 6 Zähnen und einem Holzgriff links.
    /// Bei aktivem Tab rotiert das Zahnrad langsam.
    /// </summary>
    private void DrawSettingsIcon(SKCanvas canvas, float cx, float cy, float size,
        bool isActive, float time)
    {
        float s = size * 0.45f;
        byte alpha = isActive ? (byte)255 : (byte)160;

        // Rotation bei aktivem Tab
        float rotation = 0f;
        if (isActive)
        {
            rotation = MathF.Sin(time * 0.5f) * 0.15f * (180f / MathF.PI); // Radians → Grad
        }

        // --- Holzgriff (links) ---
        _fillPaint.Color = GearHandle.WithAlpha(alpha);
        _fillPaint.Shader = null;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - s * 0.7f, cy - s * 0.07f, cx - s * 0.15f, cy + s * 0.07f), 2f), _fillPaint);

        // --- Zahnrad ---
        canvas.Save();
        canvas.Translate(cx + s * 0.05f, cy);
        canvas.RotateDegrees(rotation);
        canvas.Translate(-(cx + s * 0.05f), -cy);

        float gearCx = cx + s * 0.05f;
        float gearCy = cy;

        // Zahnrad-Pfad erzeugen (gecacht)
        _gearPath ??= CreateGearPath(0, 0, 1f, 6);

        canvas.Save();
        canvas.Translate(gearCx, gearCy);
        canvas.Scale(s * 0.42f);

        _fillPaint.Color = GearColor.WithAlpha(alpha);
        canvas.DrawPath(_gearPath, _fillPaint);

        // Innerer Kreis (Achse)
        _fillPaint.Color = WoodDark.WithAlpha(alpha);
        canvas.DrawCircle(0, 0, 0.3f, _fillPaint);

        // Achsen-Highlight
        _fillPaint.Color = RivetHighlight.WithAlpha((byte)(alpha * 0.5f));
        canvas.DrawCircle(-0.08f, -0.08f, 0.12f, _fillPaint);

        canvas.Restore();
        canvas.Restore();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PFAD-ERZEUGUNG (gecacht)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt einen Wappen-Schild-Pfad (Pentagon-Form) um den Ursprung.
    /// Die Koordinaten sind normalisiert (-1 bis +1), Skalierung erfolgt per Canvas.Scale().
    /// </summary>
    private static SKPath CreateShieldPath(float cx, float cy, float scale)
    {
        var path = new SKPath();

        // Pentagon/Schild: Oben breit, unten spitz
        path.MoveTo(cx - 0.8f * scale, cy - 0.9f * scale);   // Oben links
        path.LineTo(cx + 0.8f * scale, cy - 0.9f * scale);   // Oben rechts
        path.LineTo(cx + 0.8f * scale, cy + 0.1f * scale);   // Rechts Mitte
        path.LineTo(cx, cy + 1.0f * scale);                    // Untere Spitze
        path.LineTo(cx - 0.8f * scale, cy + 0.1f * scale);   // Links Mitte
        path.Close();

        return path;
    }

    /// <summary>
    /// Erstellt einen Zahnrad-Pfad mit der angegebenen Anzahl Zähne.
    /// Normalisierte Koordinaten (Outer ~1.0, Inner ~0.7), Skalierung per Canvas.Scale().
    /// </summary>
    private static SKPath CreateGearPath(float cx, float cy, float outerR, int teeth)
    {
        var path = new SKPath();
        float innerR = outerR * 0.65f;
        float toothWidth = MathF.PI * 2f / teeth;
        float toothHalf = toothWidth * 0.3f;

        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * toothWidth - MathF.PI / 2f;

            // Äußere Ecke links (Zahnfuß links)
            float a1 = baseAngle - toothHalf;
            // Äußere Ecke rechts (Zahnfuß rechts)
            float a2 = baseAngle + toothHalf;
            // Zwischen-Winkel (Zahnlücke links)
            float a3 = baseAngle + toothWidth / 2f - toothHalf;
            // Zwischen-Winkel (Zahnlücke rechts)
            float a4 = baseAngle + toothWidth / 2f + toothHalf;

            float ox1 = cx + outerR * MathF.Cos(a1);
            float oy1 = cy + outerR * MathF.Sin(a1);
            float ox2 = cx + outerR * MathF.Cos(a2);
            float oy2 = cy + outerR * MathF.Sin(a2);
            float ix1 = cx + innerR * MathF.Cos(a3);
            float iy1 = cy + innerR * MathF.Sin(a3);
            float ix2 = cx + innerR * MathF.Cos(a4);
            float iy2 = cy + innerR * MathF.Sin(a4);

            if (i == 0)
                path.MoveTo(ox1, oy1);
            else
                path.LineTo(ox1, oy1);

            path.LineTo(ox2, oy2);
            path.LineTo(ix1, iy1);
            path.LineTo(ix2, iy2);
        }

        path.Close();
        return path;
    }
}
