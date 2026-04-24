using SkiaSharp;
using HandwerkerImperium.Graphics;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// Reine Hit-Testing-Logik fuer das 2x4-Workshop-Grid im Dashboard.
///
/// Zweck: Die Gesture-Event-Handler in <see cref="Views.DashboardView"/> waren mit
/// Koordinaten-Konvertierung (Avalonia → SkiaSharp, DPI-Skalierung), Grid-Berechnung
/// und Button-Hit-Testing verflochten — 60+ Zeilen in <c>OnWorkshopCardsPointerPressed</c>.
/// Dieser Helper zieht die reine Geometrie raus, laesst die Event-Handler kurz.
///
/// Nicht extrahiert (bleibt absichtlich in der View):
/// - State-Machine (TapDistance, Hold-Timer, IsScrolling) — zu eng an Avalonia-Events gebunden
/// - VM-Commands — die Ausfuehrungslogik gehoert zur View-Ebene
/// </summary>
public static class WorkshopCardHitTester
{
    /// <summary>Toleranz in dp fuer Tap-vs-Scroll-Erkennung.</summary>
    public const double TapDistanceThreshold = 15.0;

    /// <summary>Maximale Tap-Dauer in ms (darueber zaehlt es als Hold oder Scroll-Abbruch).</summary>
    public const double TapMaxDurationMs = 400.0;

    /// <summary>ScrollViewer-Bewegung in dp, die als "Scroll begonnen" gilt.</summary>
    public const double ScrollOffsetThreshold = 2.0;

    /// <summary>
    /// Ergebnis eines Hit-Tests auf das 2x4-Grid.
    /// <see cref="WorkshopIndex"/> ist <c>-1</c> wenn nichts getroffen wurde.
    /// </summary>
    public readonly record struct HitResult(
        int WorkshopIndex,
        bool IsUpgradeButton,
        float SkiaX,
        float SkiaY,
        SKRect CardBounds);

    /// <summary>
    /// Testet einen Avalonia-Pointer-Event-Pixel gegen das Workshop-Grid.
    /// Konvertiert Avalonia- zu SkiaSharp-Koordinaten (DPI-Skalierung),
    /// findet die getroffene Karte und prueft ob der Upgrade-Button im Trefferbereich liegt.
    /// </summary>
    /// <param name="pointerOnCanvas">Pointer-Position relativ zum Canvas (Avalonia-dp).</param>
    /// <param name="canvasWidth">Canvas-Breite in Avalonia-dp.</param>
    /// <param name="canvasHeight">Canvas-Hoehe in Avalonia-dp.</param>
    /// <param name="lastCardsBounds">Skalierter Ziel-Bereich aus dem letzten Render-Pass (Skia-Pixel).</param>
    /// <param name="workshopCount">Anzahl sichtbarer Workshops (fuer Zeilen-Berechnung).</param>
    /// <param name="getUpgradeButtonBounds">Delegate fuer den Upgrade-Button-Hit-Test (Test-freundlich).</param>
    /// <returns>HitResult mit Workshop-Index oder -1 wenn nichts getroffen.</returns>
    public static HitResult HitTest(
        Avalonia.Point pointerOnCanvas,
        double canvasWidth,
        double canvasHeight,
        SKRect lastCardsBounds,
        int workshopCount,
        Func<SKRect, SKRect>? getUpgradeButtonBounds = null)
    {
        if (workshopCount <= 0 || canvasWidth <= 0 || canvasHeight <= 0)
            return new HitResult(-1, false, 0, 0, default);

        // Avalonia → SkiaSharp Koordinaten (DPI-Skalierung)
        float scaleX = lastCardsBounds.Width / (float)canvasWidth;
        float scaleY = lastCardsBounds.Height / (float)canvasHeight;
        float skiaX = (float)pointerOnCanvas.X * scaleX;
        float skiaY = (float)pointerOnCanvas.Y * scaleY;

        // Grid-Layout: 2 Spalten, dynamische Zeilen
        const int cols = 2;
        const float gap = 8f;
        float cardW = (lastCardsBounds.Width - (cols - 1) * gap) / cols;
        int rows = (int)Math.Ceiling(workshopCount / (double)cols);
        if (rows <= 0) return new HitResult(-1, false, skiaX, skiaY, default);
        float cardH = (lastCardsBounds.Height - (rows - 1) * gap) / rows;

        int hitCol = (int)(skiaX / (cardW + gap));
        int hitRow = (int)(skiaY / (cardH + gap));

        if (hitCol < 0 || hitCol >= cols || hitRow < 0 || hitRow >= rows)
            return new HitResult(-1, false, skiaX, skiaY, default);

        int index = hitRow * cols + hitCol;
        if (index >= workshopCount)
            return new HitResult(-1, false, skiaX, skiaY, default);

        float cardX = hitCol * (cardW + gap);
        float cardY = hitRow * (cardH + gap);
        var cardBounds = new SKRect(cardX, cardY, cardX + cardW, cardY + cardH);

        // Im Gap getroffen? → keine Karte
        if (!cardBounds.Contains(skiaX, skiaY))
            return new HitResult(-1, false, skiaX, skiaY, default);

        // Upgrade-Button-Hit-Test (Delegate fuer Testbarkeit — nutzt im Produktionscode
        // WorkshopGameCardRenderer.GetUpgradeButtonBounds)
        var buttonTest = getUpgradeButtonBounds ?? WorkshopGameCardRenderer.GetUpgradeButtonBounds;
        var upgradeBounds = buttonTest(cardBounds);
        bool isUpgrade = upgradeBounds.Contains(skiaX, skiaY);

        return new HitResult(index, isUpgrade, skiaX, skiaY, cardBounds);
    }

    /// <summary>
    /// Prueft ob ein Pointer seit dem Press weit genug bewegt wurde, um als Scroll zu zaehlen.
    /// </summary>
    public static bool IsScrollDistance(Avalonia.Point pressPos, Avalonia.Point currentPos)
    {
        var dx = currentPos.X - pressPos.X;
        var dy = currentPos.Y - pressPos.Y;
        var distanceSquared = dx * dx + dy * dy;
        return distanceSquared > TapDistanceThreshold * TapDistanceThreshold;
    }

    /// <summary>
    /// Prueft ob sich der ScrollViewer seit dem Press merklich bewegt hat.
    /// </summary>
    public static bool HasScrollViewerMoved(double offsetAtPress, double currentOffset)
    {
        return Math.Abs(currentOffset - offsetAtPress) > ScrollOffsetThreshold;
    }

    /// <summary>
    /// Prueft ob der Tap kurz genug war um als normaler Tap zu zaehlen (nicht als Hold).
    /// </summary>
    public static bool IsTapDuration(DateTime pressedAt)
    {
        var elapsed = (DateTime.UtcNow - pressedAt).TotalMilliseconds;
        return elapsed <= TapMaxDurationMs;
    }
}
