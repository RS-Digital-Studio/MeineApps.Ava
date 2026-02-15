using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert große, detaillierte Illustrationen (64x64) für Forschungs-Items.
/// Jeder Effekt-Typ hat eine einzigartige, aufwändige Darstellung im Stil
/// von "Top Heroes" Research-Trees: Werkzeuge, Gebäude, Symbole.
/// Farben orientieren sich an der Branch-Farbe (Tools=Orange, Management=Braun, Marketing=Grün).
/// </summary>
public static class ResearchIconRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // FARB-PALETTE
    // ═══════════════════════════════════════════════════════════════════════

    // Metall & Holz
    private static readonly SKColor MetalLight = new(0xB0, 0xBE, 0xC5);
    private static readonly SKColor MetalDark = new(0x78, 0x90, 0x9C);
    private static readonly SKColor MetalShine = new(0xEC, 0xEF, 0xF1);
    private static readonly SKColor WoodLight = new(0xA1, 0x88, 0x7F);
    private static readonly SKColor WoodMedium = new(0x8D, 0x6E, 0x63);
    private static readonly SKColor WoodDark = new(0x6D, 0x4C, 0x41);

    // Akzente
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor GoldDark = new(0xD4, 0xA0, 0x00);
    private static readonly SKColor PaperColor = new(0xFA, 0xF3, 0xE0);
    private static readonly SKColor InkBlue = new(0x1A, 0x23, 0x7E);
    private static readonly SKColor LeatherBrown = new(0x5D, 0x40, 0x37);
    private static readonly SKColor GlassBlue = new(0x42, 0xA5, 0xF5);
    private static readonly SKColor CoinGold = new(0xFF, 0xC1, 0x07);
    private static readonly SKColor GreenStar = new(0x4C, 0xAF, 0x50);
    private static readonly SKColor RedAccent = new(0xF4, 0x43, 0x36);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    /// <summary>
    /// Rendert das große Forschungs-Icon in einen quadratischen Bereich.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="cx">Mittelpunkt X.</param>
    /// <param name="cy">Mittelpunkt Y.</param>
    /// <param name="size">Seitenlänge (empfohlen: 64).</param>
    /// <param name="effect">ResearchEffect für Icon-Typ-Erkennung.</param>
    /// <param name="branch">Branch für Farbgebung.</param>
    /// <param name="isResearched">Ob erforscht (beeinflusst Helligkeit).</param>
    /// <param name="isLocked">Ob gesperrt (grau/dunkel).</param>
    public static void DrawIcon(SKCanvas canvas, float cx, float cy, float size,
        ResearchEffect effect, ResearchBranch branch, bool isResearched, bool isLocked)
    {
        float s = size / 2; // Halbe Größe als Basis

        // Rahmen (runder Hintergrund wie in Top Heroes)
        DrawIconFrame(canvas, cx, cy, s, branch, isResearched, isLocked);

        // Grau-Filter für gesperrte Items
        if (isLocked)
        {
            canvas.Save();
            // Reduzierte Sättigung durch Overlay
        }

        // Icon basierend auf dominantem Effekt
        if (effect.EfficiencyBonus > 0)
            DrawAnvil(canvas, cx, cy, s);
        else if (effect.CostReduction > 0)
            DrawCoinStack(canvas, cx, cy, s);
        else if (effect.MiniGameZoneBonus > 0)
            DrawBlueprint(canvas, cx, cy, s);
        else if (effect.WageReduction > 0)
            DrawPurse(canvas, cx, cy, s);
        else if (effect.ExtraWorkerSlots > 0)
            DrawHardHat(canvas, cx, cy, s);
        else if (effect.TrainingSpeedMultiplier > 0)
            DrawBook(canvas, cx, cy, s);
        else if (effect.RewardMultiplier > 0)
            DrawTreasureChest(canvas, cx, cy, s);
        else if (effect.ExtraOrderSlots > 0)
            DrawClipboard(canvas, cx, cy, s);
        else if (effect.UnlocksAutoMaterial)
            DrawConveyorBelt(canvas, cx, cy, s);
        else if (effect.UnlocksHeadhunter)
            DrawMagnifyingGlass(canvas, cx, cy, s);
        else if (effect.UnlocksSTierWorkers)
            DrawCrown(canvas, cx, cy, s);
        else if (effect.UnlocksAutoAssign)
            DrawNetwork(canvas, cx, cy, s);
        else
            DrawFlask(canvas, cx, cy, s);

        if (isLocked)
        {
            // Grauer Overlay
            _fill.Color = new SKColor(0x18, 0x12, 0x0E, 0x90);
            canvas.DrawCircle(cx, cy, s * 0.95f, _fill);
            canvas.Restore();

            // Schloss-Symbol
            DrawLockOverlay(canvas, cx, cy, s * 0.4f);
        }

        // Glanz bei erforschten Items
        if (isResearched)
        {
            DrawResearchedShine(canvas, cx, cy, s);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RAHMEN
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawIconFrame(SKCanvas canvas, float cx, float cy, float s,
        ResearchBranch branch, bool isResearched, bool isLocked)
    {
        var branchColor = ResearchItemRenderer.GetBranchColor(branch);

        // Äußerer Ring-Schatten
        _fill.Color = new SKColor(0, 0, 0, 40);
        canvas.DrawCircle(cx + 1, cy + 2, s + 3, _fill);

        // Hintergrund-Kreis
        _fill.Color = isLocked ? new SKColor(0x2A, 0x22, 0x1A) : new SKColor(0x3E, 0x2C, 0x22);
        canvas.DrawCircle(cx, cy, s, _fill);

        // Farbiger Rand (Branch-Farbe)
        byte borderAlpha = isLocked ? (byte)80 : isResearched ? (byte)255 : (byte)180;
        _stroke.Color = branchColor.WithAlpha(borderAlpha);
        _stroke.StrokeWidth = isResearched ? 3.5f : 2.5f;
        canvas.DrawCircle(cx, cy, s - 1, _stroke);

        // Äußerer dekorativer Ring (dünn)
        if (isResearched)
        {
            _stroke.Color = GoldColor.WithAlpha(100);
            _stroke.StrokeWidth = 1.5f;
            canvas.DrawCircle(cx, cy, s + 1, _stroke);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EFFEKT-ILLUSTRATIONEN (große, detaillierte Icons)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Amboss mit Hammer (EfficiencyBonus) - Werkstatt-Symbol</summary>
    private static void DrawAnvil(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.65f;

        // Amboss-Körper (Trapez-Form)
        _fill.Color = MetalDark;
        using var anvilPath = new SKPath();
        anvilPath.MoveTo(cx - scale * 0.7f, cy + scale * 0.1f);
        anvilPath.LineTo(cx + scale * 0.5f, cy + scale * 0.1f);
        anvilPath.LineTo(cx + scale * 0.4f, cy + scale * 0.4f);
        anvilPath.LineTo(cx - scale * 0.5f, cy + scale * 0.4f);
        anvilPath.Close();
        canvas.DrawPath(anvilPath, _fill);

        // Amboss-Oberfläche (heller)
        _fill.Color = MetalLight;
        using var topPath = new SKPath();
        topPath.MoveTo(cx - scale * 0.8f, cy);
        topPath.LineTo(cx + scale * 0.6f, cy);
        topPath.LineTo(cx + scale * 0.5f, cy + scale * 0.12f);
        topPath.LineTo(cx - scale * 0.7f, cy + scale * 0.12f);
        topPath.Close();
        canvas.DrawPath(topPath, _fill);

        // Horn (links, spitz)
        _fill.Color = MetalDark;
        using var hornPath = new SKPath();
        hornPath.MoveTo(cx - scale * 0.8f, cy);
        hornPath.LineTo(cx - scale, cy + scale * 0.05f);
        hornPath.LineTo(cx - scale * 0.8f, cy + scale * 0.12f);
        hornPath.Close();
        canvas.DrawPath(hornPath, _fill);

        // Glanzlicht
        _fill.Color = MetalShine.WithAlpha(80);
        canvas.DrawRect(cx - scale * 0.3f, cy + scale * 0.01f, scale * 0.5f, scale * 0.04f, _fill);

        // Sockel (Holzblock)
        _fill.Color = WoodMedium;
        canvas.DrawRect(cx - scale * 0.4f, cy + scale * 0.4f, scale * 0.8f, scale * 0.3f, _fill);
        _fill.Color = WoodDark;
        canvas.DrawRect(cx - scale * 0.4f, cy + scale * 0.4f, scale * 0.8f, scale * 0.06f, _fill);

        // Hammer (schräg, über dem Amboss)
        canvas.Save();
        canvas.Translate(cx + scale * 0.2f, cy - scale * 0.4f);
        canvas.RotateDegrees(-30);

        // Hammer-Stiel
        _fill.Color = WoodLight;
        canvas.DrawRect(-1.5f, 0, 3, scale * 0.6f, _fill);

        // Hammer-Kopf
        _fill.Color = MetalDark;
        canvas.DrawRect(-scale * 0.2f, -scale * 0.1f, scale * 0.4f, scale * 0.15f, _fill);
        _fill.Color = MetalLight;
        canvas.DrawRect(-scale * 0.2f, -scale * 0.1f, scale * 0.4f, scale * 0.04f, _fill);

        canvas.Restore();
    }

    /// <summary>Münzstapel (CostReduction) - Ersparnisse</summary>
    private static void DrawCoinStack(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.55f;

        // Münzstapel (3 Münzen gestapelt)
        for (int i = 2; i >= 0; i--)
        {
            float coinY = cy + scale * 0.3f - i * scale * 0.28f;
            float coinW = scale * 0.8f;
            float coinH = scale * 0.2f;

            // Münzen-Kante
            _fill.Color = GoldDark;
            canvas.DrawOval(cx, coinY + scale * 0.06f, coinW, coinH, _fill);

            // Münzen-Oberfläche
            _fill.Color = CoinGold;
            canvas.DrawOval(cx, coinY, coinW, coinH, _fill);

            // Glanz
            _fill.Color = GoldColor.WithAlpha(120);
            canvas.DrawOval(cx - scale * 0.1f, coinY - scale * 0.02f, coinW * 0.3f, coinH * 0.5f, _fill);
        }

        // Pfeil nach unten (Reduktion)
        _fill.Color = GreenStar;
        float arrowCx = cx + scale * 0.7f;
        float arrowCy = cy;
        using var arrowPath = new SKPath();
        arrowPath.MoveTo(arrowCx, arrowCy + scale * 0.4f);
        arrowPath.LineTo(arrowCx - scale * 0.2f, arrowCy + scale * 0.15f);
        arrowPath.LineTo(arrowCx + scale * 0.2f, arrowCy + scale * 0.15f);
        arrowPath.Close();
        canvas.DrawPath(arrowPath, _fill);

        // Pfeil-Stiel
        canvas.DrawRect(arrowCx - scale * 0.06f, arrowCy - scale * 0.2f, scale * 0.12f, scale * 0.38f, _fill);
    }

    /// <summary>Blaupause/Bauplan (MiniGameZoneBonus)</summary>
    private static void DrawBlueprint(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Blaupause (leicht gerollt)
        _fill.Color = new SKColor(0x1A, 0x23, 0x7E, 0xC0);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.7f, cy - scale * 0.5f, cx + scale * 0.7f, cy + scale * 0.5f), 3), _fill);

        // Raster-Linien
        _stroke.Color = new SKColor(0x40, 0x50, 0x90, 0x80);
        _stroke.StrokeWidth = 0.8f;
        for (float lx = cx - scale * 0.5f; lx <= cx + scale * 0.5f; lx += scale * 0.25f)
            canvas.DrawLine(lx, cy - scale * 0.4f, lx, cy + scale * 0.4f, _stroke);
        for (float ly = cy - scale * 0.35f; ly <= cy + scale * 0.35f; ly += scale * 0.2f)
            canvas.DrawLine(cx - scale * 0.6f, ly, cx + scale * 0.6f, ly, _stroke);

        // Haus-Zeichnung (weiße Linien)
        _stroke.Color = SKColors.White.WithAlpha(200);
        _stroke.StrokeWidth = 1.5f;
        // Hauswände
        canvas.DrawRect(cx - scale * 0.3f, cy - scale * 0.05f, scale * 0.6f, scale * 0.35f, _stroke);
        // Dach
        canvas.DrawLine(cx - scale * 0.35f, cy - scale * 0.05f, cx, cy - scale * 0.3f, _stroke);
        canvas.DrawLine(cx, cy - scale * 0.3f, cx + scale * 0.35f, cy - scale * 0.05f, _stroke);
        // Tür
        canvas.DrawRect(cx - scale * 0.08f, cy + scale * 0.1f, scale * 0.16f, scale * 0.2f, _stroke);

        // Gerollte Ecke (oben rechts)
        _fill.Color = new SKColor(0x28, 0x33, 0x93);
        using var rollPath = new SKPath();
        rollPath.MoveTo(cx + scale * 0.7f, cy - scale * 0.5f);
        rollPath.LineTo(cx + scale * 0.5f, cy - scale * 0.5f);
        rollPath.LineTo(cx + scale * 0.7f, cy - scale * 0.3f);
        rollPath.Close();
        canvas.DrawPath(rollPath, _fill);
    }

    /// <summary>Geldbörse (WageReduction)</summary>
    private static void DrawPurse(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Beutel-Körper
        _fill.Color = LeatherBrown;
        using var pursePath = new SKPath();
        pursePath.MoveTo(cx - scale * 0.5f, cy - scale * 0.1f);
        pursePath.QuadTo(cx - scale * 0.6f, cy + scale * 0.5f, cx, cy + scale * 0.55f);
        pursePath.QuadTo(cx + scale * 0.6f, cy + scale * 0.5f, cx + scale * 0.5f, cy - scale * 0.1f);
        pursePath.Close();
        canvas.DrawPath(pursePath, _fill);

        // Beutel-Öffnung (oben, zusammengebunden)
        _fill.Color = new SKColor(0x4E, 0x34, 0x2E);
        canvas.DrawOval(cx, cy - scale * 0.1f, scale * 0.5f, scale * 0.12f, _fill);

        // Schnur
        _stroke.Color = GoldDark;
        _stroke.StrokeWidth = 2;
        canvas.DrawLine(cx - scale * 0.15f, cy - scale * 0.2f, cx, cy - scale * 0.35f, _stroke);
        canvas.DrawLine(cx + scale * 0.15f, cy - scale * 0.2f, cx, cy - scale * 0.35f, _stroke);

        // Münze oben rausguckend
        _fill.Color = CoinGold;
        canvas.DrawCircle(cx, cy - scale * 0.15f, scale * 0.15f, _fill);
        _fill.Color = GoldDark;
        canvas.DrawCircle(cx, cy - scale * 0.15f, scale * 0.08f, _fill);

        // Minus-Symbol (Reduktion)
        _stroke.Color = RedAccent;
        _stroke.StrokeWidth = 2.5f;
        canvas.DrawLine(cx + scale * 0.4f, cy + scale * 0.1f, cx + scale * 0.7f, cy + scale * 0.1f, _stroke);
    }

    /// <summary>Schutzhelm (ExtraWorkerSlots)</summary>
    private static void DrawHardHat(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Helm-Körper (Halbkugel)
        _fill.Color = CoinGold;
        using var helmPath = new SKPath();
        helmPath.MoveTo(cx - scale * 0.7f, cy + scale * 0.1f);
        helmPath.QuadTo(cx - scale * 0.7f, cy - scale * 0.6f, cx, cy - scale * 0.5f);
        helmPath.QuadTo(cx + scale * 0.7f, cy - scale * 0.6f, cx + scale * 0.7f, cy + scale * 0.1f);
        helmPath.Close();
        canvas.DrawPath(helmPath, _fill);

        // Helm-Krempe
        _fill.Color = GoldDark;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.8f, cy + scale * 0.05f, cx + scale * 0.8f, cy + scale * 0.2f), 2), _fill);

        // Mittelstreifen
        _fill.Color = GoldColor.WithAlpha(120);
        canvas.DrawRect(cx - scale * 0.05f, cy - scale * 0.45f, scale * 0.1f, scale * 0.5f, _fill);

        // Glanzlicht
        _fill.Color = SKColors.White.WithAlpha(60);
        canvas.DrawOval(cx - scale * 0.2f, cy - scale * 0.3f, scale * 0.2f, scale * 0.12f, _fill);

        // Plus-Symbol (mehr Arbeiter)
        _fill.Color = GreenStar;
        float plusX = cx + scale * 0.5f;
        float plusY = cy + scale * 0.4f;
        canvas.DrawCircle(plusX, plusY, scale * 0.22f, _fill);
        _stroke.Color = SKColors.White;
        _stroke.StrokeWidth = 2;
        canvas.DrawLine(plusX - scale * 0.1f, plusY, plusX + scale * 0.1f, plusY, _stroke);
        canvas.DrawLine(plusX, plusY - scale * 0.1f, plusX, plusY + scale * 0.1f, _stroke);
    }

    /// <summary>Aufgeschlagenes Buch (TrainingSpeedMultiplier)</summary>
    private static void DrawBook(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Buchdeckel (links + rechts, aufgeklappt)
        _fill.Color = new SKColor(0x8B, 0x00, 0x00);
        using var leftPage = new SKPath();
        leftPage.MoveTo(cx, cy - scale * 0.4f);
        leftPage.LineTo(cx - scale * 0.7f, cy - scale * 0.3f);
        leftPage.LineTo(cx - scale * 0.7f, cy + scale * 0.4f);
        leftPage.LineTo(cx, cy + scale * 0.5f);
        leftPage.Close();
        canvas.DrawPath(leftPage, _fill);

        _fill.Color = new SKColor(0x7B, 0x00, 0x00);
        using var rightPage = new SKPath();
        rightPage.MoveTo(cx, cy - scale * 0.4f);
        rightPage.LineTo(cx + scale * 0.7f, cy - scale * 0.3f);
        rightPage.LineTo(cx + scale * 0.7f, cy + scale * 0.4f);
        rightPage.LineTo(cx, cy + scale * 0.5f);
        rightPage.Close();
        canvas.DrawPath(rightPage, _fill);

        // Seiten (innere, heller)
        _fill.Color = PaperColor;
        using var leftInner = new SKPath();
        leftInner.MoveTo(cx, cy - scale * 0.35f);
        leftInner.LineTo(cx - scale * 0.6f, cy - scale * 0.25f);
        leftInner.LineTo(cx - scale * 0.6f, cy + scale * 0.35f);
        leftInner.LineTo(cx, cy + scale * 0.45f);
        leftInner.Close();
        canvas.DrawPath(leftInner, _fill);

        using var rightInner = new SKPath();
        rightInner.MoveTo(cx, cy - scale * 0.35f);
        rightInner.LineTo(cx + scale * 0.6f, cy - scale * 0.25f);
        rightInner.LineTo(cx + scale * 0.6f, cy + scale * 0.35f);
        rightInner.LineTo(cx, cy + scale * 0.45f);
        rightInner.Close();
        canvas.DrawPath(rightInner, _fill);

        // Text-Zeilen auf den Seiten
        _stroke.Color = new SKColor(0x90, 0x90, 0x90, 0x80);
        _stroke.StrokeWidth = 0.8f;
        for (int i = 0; i < 4; i++)
        {
            float ly = cy - scale * 0.15f + i * scale * 0.14f;
            canvas.DrawLine(cx - scale * 0.5f, ly, cx - scale * 0.1f, ly + scale * 0.02f, _stroke);
            canvas.DrawLine(cx + scale * 0.1f, ly, cx + scale * 0.5f, ly - scale * 0.02f, _stroke);
        }

        // Buchrücken (Mitte)
        _fill.Color = new SKColor(0x6B, 0x00, 0x00);
        canvas.DrawRect(cx - 1.5f, cy - scale * 0.4f, 3, scale * 0.9f, _fill);

        // Blitz-Symbol (Geschwindigkeit)
        _fill.Color = CoinGold;
        float bx = cx + scale * 0.55f;
        float by = cy - scale * 0.35f;
        using var boltPath = new SKPath();
        boltPath.MoveTo(bx - 3, by);
        boltPath.LineTo(bx + 4, by);
        boltPath.LineTo(bx, by + 5);
        boltPath.LineTo(bx + 5, by + 5);
        boltPath.LineTo(bx - 2, by + 12);
        boltPath.LineTo(bx + 1, by + 6);
        boltPath.LineTo(bx - 4, by + 6);
        boltPath.Close();
        canvas.DrawPath(boltPath, _fill);
    }

    /// <summary>Schatzkiste (RewardMultiplier)</summary>
    private static void DrawTreasureChest(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Kisten-Körper
        _fill.Color = WoodMedium;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.6f, cy - scale * 0.05f, cx + scale * 0.6f, cy + scale * 0.45f), 2), _fill);

        // Holzstruktur
        _fill.Color = WoodDark;
        canvas.DrawRect(cx - scale * 0.6f, cy + scale * 0.15f, scale * 1.2f, scale * 0.04f, _fill);

        // Kisten-Deckel (gewölbt)
        _fill.Color = WoodLight;
        using var lidPath = new SKPath();
        lidPath.MoveTo(cx - scale * 0.65f, cy - scale * 0.05f);
        lidPath.QuadTo(cx, cy - scale * 0.45f, cx + scale * 0.65f, cy - scale * 0.05f);
        lidPath.Close();
        canvas.DrawPath(lidPath, _fill);

        // Deckel-Kante
        _stroke.Color = WoodDark;
        _stroke.StrokeWidth = 1.5f;
        canvas.DrawLine(cx - scale * 0.65f, cy - scale * 0.05f, cx + scale * 0.65f, cy - scale * 0.05f, _stroke);

        // Metall-Beschläge
        _fill.Color = GoldDark;
        canvas.DrawRect(cx - scale * 0.06f, cy - scale * 0.3f, scale * 0.12f, scale * 0.35f, _fill);
        // Schloss
        _fill.Color = GoldColor;
        canvas.DrawCircle(cx, cy + scale * 0.02f, scale * 0.08f, _fill);

        // Glühende Münzen (oben rausguckend)
        _fill.Color = GoldColor;
        canvas.DrawCircle(cx - scale * 0.15f, cy - scale * 0.2f, scale * 0.1f, _fill);
        canvas.DrawCircle(cx + scale * 0.2f, cy - scale * 0.15f, scale * 0.08f, _fill);
        canvas.DrawCircle(cx, cy - scale * 0.28f, scale * 0.07f, _fill);

        // Doppelpfeil hoch
        _stroke.Color = GreenStar;
        _stroke.StrokeWidth = 2;
        float ax = cx + scale * 0.6f;
        float ay = cy - scale * 0.15f;
        canvas.DrawLine(ax, ay + scale * 0.15f, ax, ay - scale * 0.15f, _stroke);
        canvas.DrawLine(ax - scale * 0.08f, ay - scale * 0.05f, ax, ay - scale * 0.15f, _stroke);
        canvas.DrawLine(ax + scale * 0.08f, ay - scale * 0.05f, ax, ay - scale * 0.15f, _stroke);
    }

    /// <summary>Klemmbrett mit Aufträgen (ExtraOrderSlots)</summary>
    private static void DrawClipboard(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Klemmbrett-Körper
        _fill.Color = WoodMedium;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.5f, cy - scale * 0.5f, cx + scale * 0.5f, cy + scale * 0.55f), 4), _fill);

        // Klemme oben
        _fill.Color = MetalDark;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.2f, cy - scale * 0.6f, cx + scale * 0.2f, cy - scale * 0.4f), 2), _fill);

        // Papier
        _fill.Color = PaperColor;
        canvas.DrawRect(cx - scale * 0.4f, cy - scale * 0.35f, scale * 0.8f, scale * 0.8f, _fill);

        // Checkboxen (3 Zeilen)
        for (int i = 0; i < 3; i++)
        {
            float ly = cy - scale * 0.2f + i * scale * 0.22f;

            // Checkbox
            _stroke.Color = MetalDark;
            _stroke.StrokeWidth = 1;
            canvas.DrawRect(cx - scale * 0.3f, ly - scale * 0.04f, scale * 0.1f, scale * 0.1f, _stroke);

            // Häkchen (erste 2 abgehakt)
            if (i < 2)
            {
                _stroke.Color = GreenStar;
                _stroke.StrokeWidth = 1.5f;
                canvas.DrawLine(cx - scale * 0.28f, ly, cx - scale * 0.24f, ly + scale * 0.04f, _stroke);
                canvas.DrawLine(cx - scale * 0.24f, ly + scale * 0.04f, cx - scale * 0.2f, ly - scale * 0.03f, _stroke);
            }

            // Text-Linie
            _stroke.Color = new SKColor(0x90, 0x90, 0x90);
            _stroke.StrokeWidth = 1;
            canvas.DrawLine(cx - scale * 0.15f, ly + scale * 0.01f, cx + scale * 0.3f, ly + scale * 0.01f, _stroke);
        }

        // Plus-Badge
        _fill.Color = GreenStar;
        canvas.DrawCircle(cx + scale * 0.45f, cy + scale * 0.4f, scale * 0.18f, _fill);
        _stroke.Color = SKColors.White;
        _stroke.StrokeWidth = 2;
        canvas.DrawLine(cx + scale * 0.38f, cy + scale * 0.4f, cx + scale * 0.52f, cy + scale * 0.4f, _stroke);
        canvas.DrawLine(cx + scale * 0.45f, cy + scale * 0.33f, cx + scale * 0.45f, cy + scale * 0.47f, _stroke);
    }

    /// <summary>Förderband (UnlocksAutoMaterial)</summary>
    private static void DrawConveyorBelt(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Band-Rollen (links + rechts)
        _fill.Color = MetalDark;
        canvas.DrawCircle(cx - scale * 0.5f, cy + scale * 0.1f, scale * 0.2f, _fill);
        canvas.DrawCircle(cx + scale * 0.5f, cy + scale * 0.1f, scale * 0.2f, _fill);

        // Band (obere Linie)
        _stroke.Color = new SKColor(0x45, 0x45, 0x45);
        _stroke.StrokeWidth = scale * 0.15f;
        canvas.DrawLine(cx - scale * 0.5f, cy - scale * 0.08f, cx + scale * 0.5f, cy - scale * 0.08f, _stroke);

        // Band (untere Linie)
        canvas.DrawLine(cx - scale * 0.5f, cy + scale * 0.28f, cx + scale * 0.5f, cy + scale * 0.28f, _stroke);

        // Materialien auf dem Band (Holzblock, Metallplatte, Ziegel)
        _fill.Color = WoodLight;
        canvas.DrawRect(cx - scale * 0.35f, cy - scale * 0.28f, scale * 0.2f, scale * 0.18f, _fill);

        _fill.Color = MetalLight;
        canvas.DrawRect(cx - scale * 0.05f, cy - scale * 0.25f, scale * 0.18f, scale * 0.15f, _fill);

        _fill.Color = RedAccent.WithAlpha(180);
        canvas.DrawRect(cx + scale * 0.2f, cy - scale * 0.26f, scale * 0.16f, scale * 0.16f, _fill);

        // Rollen-Achsen
        _fill.Color = MetalLight;
        canvas.DrawCircle(cx - scale * 0.5f, cy + scale * 0.1f, scale * 0.06f, _fill);
        canvas.DrawCircle(cx + scale * 0.5f, cy + scale * 0.1f, scale * 0.06f, _fill);

        // "AUTO" Label
        using var font = new SKFont { Size = scale * 0.25f, Embolden = true };
        _fill.Color = GreenStar;
        canvas.DrawText("AUTO", cx, cy + scale * 0.55f, SKTextAlign.Center, font, _fill);
    }

    /// <summary>Lupe (UnlocksHeadhunter)</summary>
    private static void DrawMagnifyingGlass(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Griff
        _fill.Color = WoodMedium;
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(35);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(-scale * 0.08f, scale * 0.3f, scale * 0.08f, scale * 0.75f), 3), _fill);
        canvas.Restore();

        // Glas-Ring
        _stroke.Color = GoldDark;
        _stroke.StrokeWidth = scale * 0.12f;
        canvas.DrawCircle(cx - scale * 0.1f, cy - scale * 0.1f, scale * 0.4f, _stroke);

        // Glas (hellblau, transparent)
        _fill.Color = GlassBlue.WithAlpha(50);
        canvas.DrawCircle(cx - scale * 0.1f, cy - scale * 0.1f, scale * 0.34f, _fill);

        // Person in der Lupe (Silhouette)
        _fill.Color = new SKColor(0x60, 0x60, 0x60, 0xB0);
        // Kopf
        canvas.DrawCircle(cx - scale * 0.1f, cy - scale * 0.22f, scale * 0.1f, _fill);
        // Schultern
        using var bodyPath = new SKPath();
        bodyPath.MoveTo(cx - scale * 0.25f, cy + scale * 0.1f);
        bodyPath.QuadTo(cx - scale * 0.1f, cy - scale * 0.08f, cx + scale * 0.05f, cy + scale * 0.1f);
        bodyPath.Close();
        canvas.DrawPath(bodyPath, _fill);

        // Glanz auf dem Glas
        _fill.Color = SKColors.White.WithAlpha(50);
        canvas.DrawOval(cx - scale * 0.25f, cy - scale * 0.25f, scale * 0.12f, scale * 0.08f, _fill);
    }

    /// <summary>Krone (UnlocksSTierWorkers)</summary>
    private static void DrawCrown(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Kissen (unter der Krone)
        _fill.Color = new SKColor(0x7B, 0x1F, 0xA2);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.6f, cy + scale * 0.15f, cx + scale * 0.6f, cy + scale * 0.4f), 6), _fill);
        _fill.Color = new SKColor(0x6A, 0x1B, 0x9A);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - scale * 0.55f, cy + scale * 0.2f, cx + scale * 0.55f, cy + scale * 0.35f), 4), _fill);

        // Kronen-Körper
        _fill.Color = GoldColor;
        using var crownPath = new SKPath();
        crownPath.MoveTo(cx - scale * 0.55f, cy + scale * 0.15f);
        crownPath.LineTo(cx - scale * 0.55f, cy - scale * 0.1f);
        crownPath.LineTo(cx - scale * 0.35f, cy + scale * 0.02f);
        crownPath.LineTo(cx - scale * 0.15f, cy - scale * 0.35f);
        crownPath.LineTo(cx, cy - scale * 0.1f);
        crownPath.LineTo(cx + scale * 0.15f, cy - scale * 0.35f);
        crownPath.LineTo(cx + scale * 0.35f, cy + scale * 0.02f);
        crownPath.LineTo(cx + scale * 0.55f, cy - scale * 0.1f);
        crownPath.LineTo(cx + scale * 0.55f, cy + scale * 0.15f);
        crownPath.Close();
        canvas.DrawPath(crownPath, _fill);

        // Rand (Basisband)
        _fill.Color = GoldDark;
        canvas.DrawRect(cx - scale * 0.55f, cy + scale * 0.05f, scale * 1.1f, scale * 0.1f, _fill);

        // Edelsteine
        _fill.Color = RedAccent;
        canvas.DrawCircle(cx, cy + scale * 0.1f, scale * 0.07f, _fill);
        _fill.Color = GlassBlue;
        canvas.DrawCircle(cx - scale * 0.25f, cy + scale * 0.1f, scale * 0.05f, _fill);
        canvas.DrawCircle(cx + scale * 0.25f, cy + scale * 0.1f, scale * 0.05f, _fill);

        // Spitzen-Kugeln
        _fill.Color = GoldColor;
        canvas.DrawCircle(cx - scale * 0.15f, cy - scale * 0.35f, scale * 0.06f, _fill);
        canvas.DrawCircle(cx + scale * 0.15f, cy - scale * 0.35f, scale * 0.06f, _fill);

        // "S" auf der Krone
        using var font = new SKFont { Size = scale * 0.3f, Embolden = true };
        _fill.Color = SKColors.White;
        canvas.DrawText("S", cx, cy + scale * 0.03f, SKTextAlign.Center, font, _fill);
    }

    /// <summary>Netzwerk-Diagramm (UnlocksAutoAssign)</summary>
    private static void DrawNetwork(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Verbindungslinien (zuerst, damit Knoten darüber liegen)
        _stroke.Color = new SKColor(0x60, 0x80, 0x60, 0xB0);
        _stroke.StrokeWidth = 2;

        // Positionen der 5 Knoten
        float[][] nodes =
        [
            [cx, cy - scale * 0.35f],               // Oben-Mitte
            [cx - scale * 0.45f, cy + scale * 0.05f], // Links
            [cx + scale * 0.45f, cy + scale * 0.05f], // Rechts
            [cx - scale * 0.25f, cy + scale * 0.4f],  // Unten-links
            [cx + scale * 0.25f, cy + scale * 0.4f]   // Unten-rechts
        ];

        // Verbindungen
        canvas.DrawLine(nodes[0][0], nodes[0][1], nodes[1][0], nodes[1][1], _stroke);
        canvas.DrawLine(nodes[0][0], nodes[0][1], nodes[2][0], nodes[2][1], _stroke);
        canvas.DrawLine(nodes[1][0], nodes[1][1], nodes[3][0], nodes[3][1], _stroke);
        canvas.DrawLine(nodes[2][0], nodes[2][1], nodes[4][0], nodes[4][1], _stroke);
        canvas.DrawLine(nodes[3][0], nodes[3][1], nodes[4][0], nodes[4][1], _stroke);

        // Knoten
        for (int i = 0; i < nodes.Length; i++)
        {
            float nx = nodes[i][0], ny = nodes[i][1];

            // Glow
            _fill.Color = GreenStar.WithAlpha(40);
            canvas.DrawCircle(nx, ny, scale * 0.16f, _fill);

            // Knoten
            _fill.Color = i == 0 ? GreenStar : GlassBlue;
            canvas.DrawCircle(nx, ny, scale * 0.1f, _fill);

            // Innerer Punkt
            _fill.Color = SKColors.White.WithAlpha(180);
            canvas.DrawCircle(nx, ny, scale * 0.04f, _fill);
        }
    }

    /// <summary>Erlenmeyer-Kolben Fallback (unbekannter Effekt)</summary>
    private static void DrawFlask(SKCanvas canvas, float cx, float cy, float s)
    {
        float scale = s * 0.6f;

        // Kolben-Hals
        _stroke.Color = GlassBlue.WithAlpha(150);
        _stroke.StrokeWidth = 2;
        canvas.DrawRect(cx - scale * 0.1f, cy - scale * 0.5f, scale * 0.2f, scale * 0.3f, _stroke);

        // Kolben-Körper (Dreieck)
        using var flaskPath = new SKPath();
        flaskPath.MoveTo(cx - scale * 0.1f, cy - scale * 0.2f);
        flaskPath.LineTo(cx - scale * 0.5f, cy + scale * 0.4f);
        flaskPath.LineTo(cx + scale * 0.5f, cy + scale * 0.4f);
        flaskPath.LineTo(cx + scale * 0.1f, cy - scale * 0.2f);
        flaskPath.Close();
        canvas.DrawPath(flaskPath, _stroke);

        // Flüssigkeit
        _fill.Color = GreenStar.WithAlpha(120);
        using var liquidPath = new SKPath();
        liquidPath.MoveTo(cx - scale * 0.25f, cy + scale * 0.1f);
        liquidPath.LineTo(cx - scale * 0.45f, cy + scale * 0.35f);
        liquidPath.LineTo(cx + scale * 0.45f, cy + scale * 0.35f);
        liquidPath.LineTo(cx + scale * 0.25f, cy + scale * 0.1f);
        liquidPath.Close();
        canvas.DrawPath(liquidPath, _fill);

        // Blasen
        _fill.Color = SKColors.White.WithAlpha(80);
        canvas.DrawCircle(cx - scale * 0.1f, cy + scale * 0.2f, scale * 0.05f, _fill);
        canvas.DrawCircle(cx + scale * 0.15f, cy + scale * 0.15f, scale * 0.04f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // OVERLAYS
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawLockOverlay(SKCanvas canvas, float cx, float cy, float s)
    {
        // Schloss-Bügel
        _stroke.Color = new SKColor(0x80, 0x80, 0x80);
        _stroke.StrokeWidth = 2.5f;
        canvas.DrawArc(new SKRect(cx - s * 0.4f, cy - s * 0.8f, cx + s * 0.4f, cy - s * 0.1f), 180, 180, false, _stroke);

        // Schloss-Körper
        _fill.Color = new SKColor(0x70, 0x70, 0x70);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - s * 0.5f, cy - s * 0.2f, cx + s * 0.5f, cy + s * 0.5f), 2), _fill);

        // Schlüsselloch
        _fill.Color = new SKColor(0x40, 0x40, 0x40);
        canvas.DrawCircle(cx, cy + s * 0.05f, s * 0.12f, _fill);
        canvas.DrawRect(cx - s * 0.05f, cy + s * 0.05f, s * 0.1f, s * 0.2f, _fill);
    }

    private static void DrawResearchedShine(SKCanvas canvas, float cx, float cy, float s)
    {
        // Goldener Schimmer-Ring
        _stroke.Color = GoldColor.WithAlpha(60);
        _stroke.StrokeWidth = 2;
        canvas.DrawCircle(cx, cy, s * 0.85f, _stroke);

        // Glanz oben-links
        _fill.Color = SKColors.White.WithAlpha(30);
        canvas.DrawOval(cx - s * 0.3f, cy - s * 0.3f, s * 0.2f, s * 0.12f, _fill);
    }
}
