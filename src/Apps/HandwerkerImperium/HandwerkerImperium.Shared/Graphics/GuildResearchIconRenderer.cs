using HandwerkerImperium.Models;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert Vektor-Icons für die 6 Gilden-Forschungs-Kategorien.
/// Alle Methoden sind statisch und allozieren keine eigenen Paints -
/// der Aufrufer übergibt fillPaint und strokePaint mit gewünschten Farben/IsAntialias.
/// Icons sind bei 36-72dp Größe gut erkennbar.
/// </summary>
public static class GuildResearchIconRenderer
{
    /// <summary>
    /// Zeichnet das Kategorie-Icon zentriert bei (cx, cy) innerhalb der angegebenen Größe.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="cx">Mittelpunkt X.</param>
    /// <param name="cy">Mittelpunkt Y.</param>
    /// <param name="size">Seitenlänge des verfügbaren Bereichs.</param>
    /// <param name="category">Gilden-Forschungs-Kategorie für Icon-Auswahl.</param>
    /// <param name="fillPaint">Paint für gefüllte Flächen (Farbe/Style vom Aufrufer gesetzt).</param>
    /// <param name="strokePaint">Paint für Konturen (Farbe/Style/StrokeWidth vom Aufrufer gesetzt).</param>
    public static void DrawIcon(SKCanvas canvas, float cx, float cy, float size,
        GuildResearchCategory category, SKPaint fillPaint, SKPaint strokePaint)
    {
        switch (category)
        {
            case GuildResearchCategory.Infrastructure:
                DrawBuildingIcon(canvas, cx, cy, size, fillPaint, strokePaint);
                break;
            case GuildResearchCategory.Economy:
                DrawCoinIcon(canvas, cx, cy, size, fillPaint, strokePaint);
                break;
            case GuildResearchCategory.Knowledge:
                DrawBookIcon(canvas, cx, cy, size, fillPaint, strokePaint);
                break;
            case GuildResearchCategory.Logistics:
                DrawTruckIcon(canvas, cx, cy, size, fillPaint, strokePaint);
                break;
            case GuildResearchCategory.Workforce:
                DrawWorkerIcon(canvas, cx, cy, size, fillPaint, strokePaint);
                break;
            case GuildResearchCategory.Mastery:
                DrawCrownIcon(canvas, cx, cy, size, fillPaint, strokePaint);
                break;
        }
    }

    /// <summary>
    /// Zeichnet eine Tier-Anzeige als Punkte (römische Ziffern-Stil) unterhalb des Icons.
    /// 1=I (1 Punkt), 2=II (2 Punkte), 3=III (3 Punkte), 4=IIII (4 Punkte).
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="cx">Mittelpunkt X des Icon-Bereichs.</param>
    /// <param name="cy">Mittelpunkt Y des Icon-Bereichs.</param>
    /// <param name="size">Größe des Icon-Bereichs (Punkte werden unterhalb gezeichnet).</param>
    /// <param name="tier">Tier-Stufe (1-4).</param>
    /// <param name="fillPaint">Paint für die Punkte (Farbe vom Aufrufer gesetzt).</param>
    public static void DrawTierIndicator(SKCanvas canvas, float cx, float cy, float size,
        int tier, SKPaint fillPaint)
    {
        if (tier < 1 || tier > 4) return;

        float dotRadius = size * 0.04f;
        float spacing = size * 0.10f;
        float totalWidth = (tier - 1) * spacing;
        float startX = cx - totalWidth / 2f;
        float dotY = cy + size * 0.52f;

        for (int i = 0; i < tier; i++)
        {
            canvas.DrawCircle(startX + i * spacing, dotY, dotRadius, fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE-ICONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Infrastruktur: Gebäude-Silhouette mit Dreiecksdach, Tür und Aufwärts-Pfeil.
    /// </summary>
    private static void DrawBuildingIcon(SKCanvas canvas, float cx, float cy, float size,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float s = size * 0.4f;

        // Gebäude-Körper (Rechteck)
        float bodyLeft = cx - s * 0.55f;
        float bodyRight = cx + s * 0.25f;
        float bodyTop = cy - s * 0.15f;
        float bodyBottom = cy + s * 0.65f;

        canvas.DrawRect(bodyLeft, bodyTop, bodyRight - bodyLeft, bodyBottom - bodyTop, fillPaint);
        canvas.DrawRect(bodyLeft, bodyTop, bodyRight - bodyLeft, bodyBottom - bodyTop, strokePaint);

        // Dach (Dreieck)
        using var roofPath = new SKPath();
        roofPath.MoveTo(bodyLeft - s * 0.1f, bodyTop);
        roofPath.LineTo((bodyLeft + bodyRight) / 2f, cy - s * 0.55f);
        roofPath.LineTo(bodyRight + s * 0.1f, bodyTop);
        roofPath.Close();
        canvas.DrawPath(roofPath, fillPaint);
        canvas.DrawPath(roofPath, strokePaint);

        // Tür (kleines Rechteck unten Mitte)
        float doorW = s * 0.22f;
        float doorH = s * 0.32f;
        float doorX = (bodyLeft + bodyRight) / 2f - doorW / 2f;
        float doorY = bodyBottom - doorH;
        canvas.DrawRect(doorX, doorY, doorW, doorH, strokePaint);

        // Fenster (kleines Quadrat oben links im Gebäude)
        float winSize = s * 0.16f;
        float winX = bodyLeft + s * 0.12f;
        float winY = bodyTop + s * 0.12f;
        canvas.DrawRect(winX, winY, winSize, winSize, strokePaint);

        // Aufwärts-Pfeil rechts neben dem Gebäude
        float arrowCx = cx + s * 0.65f;
        float arrowTop = cy - s * 0.45f;
        float arrowBottom = cy + s * 0.25f;
        float arrowW = s * 0.18f;

        // Pfeil-Stiel
        canvas.DrawLine(arrowCx, arrowBottom, arrowCx, arrowTop + s * 0.15f, strokePaint);

        // Pfeil-Spitze
        using var arrowHead = new SKPath();
        arrowHead.MoveTo(arrowCx, arrowTop);
        arrowHead.LineTo(arrowCx - arrowW, arrowTop + s * 0.2f);
        arrowHead.LineTo(arrowCx + arrowW, arrowTop + s * 0.2f);
        arrowHead.Close();
        canvas.DrawPath(arrowHead, fillPaint);
    }

    /// <summary>
    /// Wirtschaft: 3 gestapelte Münzen (überlappende Ellipsen mit Schrägstrichen) + Aufwärts-Pfeil.
    /// </summary>
    private static void DrawCoinIcon(SKCanvas canvas, float cx, float cy, float size,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float s = size * 0.4f;

        // Münzstapel-Mittelpunkt leicht nach links versetzt
        float stackCx = cx - s * 0.15f;

        // 3 Münzen von unten nach oben (überlappend)
        for (int i = 0; i < 3; i++)
        {
            float coinY = cy + s * 0.35f - i * s * 0.3f;
            float coinW = s * 0.55f;
            float coinH = s * 0.18f;

            // Münzen-Kante (leicht versetzt nach unten)
            canvas.DrawOval(stackCx, coinY + s * 0.05f, coinW, coinH, fillPaint);

            // Münzen-Oberfläche
            canvas.DrawOval(stackCx, coinY, coinW, coinH, fillPaint);
            canvas.DrawOval(stackCx, coinY, coinW, coinH, strokePaint);

            // Diagonale Linie als Münz-Detail
            float lineOffset = coinW * 0.3f;
            canvas.DrawLine(
                stackCx - lineOffset, coinY - coinH * 0.3f,
                stackCx + lineOffset, coinY + coinH * 0.3f,
                strokePaint);
        }

        // Aufwärts-Pfeil rechts
        float arrowCx = cx + s * 0.6f;
        float arrowTop = cy - s * 0.5f;
        float arrowBottom = cy + s * 0.2f;
        float arrowW = s * 0.15f;

        // Pfeil-Stiel
        canvas.DrawLine(arrowCx, arrowBottom, arrowCx, arrowTop + s * 0.18f, strokePaint);

        // Pfeil-Spitze
        using var arrowHead = new SKPath();
        arrowHead.MoveTo(arrowCx, arrowTop);
        arrowHead.LineTo(arrowCx - arrowW, arrowTop + s * 0.2f);
        arrowHead.LineTo(arrowCx + arrowW, arrowTop + s * 0.2f);
        arrowHead.Close();
        canvas.DrawPath(arrowHead, fillPaint);
    }

    /// <summary>
    /// Wissen: Aufgeschlagenes Buch (zwei angewinkelte Seiten) mit kleiner Glühbirne darüber.
    /// </summary>
    private static void DrawBookIcon(SKCanvas canvas, float cx, float cy, float size,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float s = size * 0.4f;

        // Buch leicht nach unten versetzt, Glühbirne oben
        float bookCy = cy + s * 0.15f;

        // Linke Seite (leicht nach links geneigt)
        using var leftPage = new SKPath();
        leftPage.MoveTo(cx, bookCy - s * 0.4f);
        leftPage.LineTo(cx - s * 0.65f, bookCy - s * 0.3f);
        leftPage.LineTo(cx - s * 0.65f, bookCy + s * 0.4f);
        leftPage.LineTo(cx, bookCy + s * 0.5f);
        leftPage.Close();
        canvas.DrawPath(leftPage, fillPaint);
        canvas.DrawPath(leftPage, strokePaint);

        // Rechte Seite
        using var rightPage = new SKPath();
        rightPage.MoveTo(cx, bookCy - s * 0.4f);
        rightPage.LineTo(cx + s * 0.65f, bookCy - s * 0.3f);
        rightPage.LineTo(cx + s * 0.65f, bookCy + s * 0.4f);
        rightPage.LineTo(cx, bookCy + s * 0.5f);
        rightPage.Close();
        canvas.DrawPath(rightPage, fillPaint);
        canvas.DrawPath(rightPage, strokePaint);

        // Buchrücken (Mittellinie)
        canvas.DrawLine(cx, bookCy - s * 0.4f, cx, bookCy + s * 0.5f, strokePaint);

        // Text-Zeilen auf den Seiten (2 pro Seite)
        for (int i = 0; i < 2; i++)
        {
            float ly = bookCy - s * 0.05f + i * s * 0.2f;
            // Linke Seite
            canvas.DrawLine(cx - s * 0.5f, ly, cx - s * 0.12f, ly + s * 0.02f, strokePaint);
            // Rechte Seite
            canvas.DrawLine(cx + s * 0.12f, ly, cx + s * 0.5f, ly - s * 0.02f, strokePaint);
        }

        // Glühbirne oben Mitte
        float bulbCy = cy - s * 0.55f;
        float bulbR = s * 0.18f;

        // Birne (Kreis)
        canvas.DrawCircle(cx, bulbCy, bulbR, fillPaint);
        canvas.DrawCircle(cx, bulbCy, bulbR, strokePaint);

        // Sockel (2 kurze Striche unter der Birne)
        float sockelY = bulbCy + bulbR;
        canvas.DrawLine(cx - bulbR * 0.4f, sockelY, cx + bulbR * 0.4f, sockelY, strokePaint);
        canvas.DrawLine(cx - bulbR * 0.25f, sockelY + s * 0.06f, cx + bulbR * 0.25f, sockelY + s * 0.06f, strokePaint);

        // Strahlen (4 kurze Linien um die Birne)
        float rayLen = s * 0.1f;
        float rayDist = bulbR + s * 0.05f;
        canvas.DrawLine(cx, bulbCy - rayDist, cx, bulbCy - rayDist - rayLen, strokePaint);
        canvas.DrawLine(cx - rayDist, bulbCy, cx - rayDist - rayLen, bulbCy, strokePaint);
        canvas.DrawLine(cx + rayDist, bulbCy, cx + rayDist + rayLen, bulbCy, strokePaint);
        // Diagonale Strahlen
        float diag = rayDist * 0.7f;
        float diagLen = rayLen * 0.7f;
        canvas.DrawLine(cx - diag, bulbCy - diag, cx - diag - diagLen, bulbCy - diag - diagLen, strokePaint);
        canvas.DrawLine(cx + diag, bulbCy - diag, cx + diag + diagLen, bulbCy - diag - diagLen, strokePaint);
    }

    /// <summary>
    /// Logistik: Lieferwagen-Seitenansicht (Rechteck-Ladefläche + kleinere Kabine + 2 Räder).
    /// </summary>
    private static void DrawTruckIcon(SKCanvas canvas, float cx, float cy, float size,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float s = size * 0.4f;

        // Ladefläche (großes Rechteck, linke Hälfte)
        float cargoLeft = cx - s * 0.75f;
        float cargoRight = cx + s * 0.15f;
        float cargoTop = cy - s * 0.35f;
        float cargoBottom = cy + s * 0.3f;

        canvas.DrawRect(cargoLeft, cargoTop, cargoRight - cargoLeft, cargoBottom - cargoTop, fillPaint);
        canvas.DrawRect(cargoLeft, cargoTop, cargoRight - cargoLeft, cargoBottom - cargoTop, strokePaint);

        // Kabine (kleineres Rechteck, rechte Seite, niedriger)
        float cabLeft = cargoRight;
        float cabRight = cx + s * 0.7f;
        float cabTop = cy - s * 0.08f;
        float cabBottom = cargoBottom;

        canvas.DrawRect(cabLeft, cabTop, cabRight - cabLeft, cabBottom - cabTop, fillPaint);
        canvas.DrawRect(cabLeft, cabTop, cabRight - cabLeft, cabBottom - cabTop, strokePaint);

        // Windschutzscheibe (Linie in der Kabine)
        float windshieldX = cabLeft + (cabRight - cabLeft) * 0.35f;
        canvas.DrawLine(windshieldX, cabTop + s * 0.04f, windshieldX, cabBottom - s * 0.1f, strokePaint);

        // Motorhaube (schräge Linie vorne)
        using var hoodPath = new SKPath();
        hoodPath.MoveTo(cabRight, cabTop);
        hoodPath.LineTo(cx + s * 0.85f, cy + s * 0.05f);
        hoodPath.LineTo(cx + s * 0.85f, cabBottom);
        hoodPath.LineTo(cabRight, cabBottom);
        hoodPath.Close();
        canvas.DrawPath(hoodPath, fillPaint);
        canvas.DrawPath(hoodPath, strokePaint);

        // Unterseite (Verbindungslinie)
        canvas.DrawLine(cargoLeft, cargoBottom, cx + s * 0.85f, cargoBottom, strokePaint);

        // Räder (2 Kreise)
        float wheelR = s * 0.14f;
        float wheelY = cargoBottom + wheelR * 0.3f;

        // Hinterrad
        float wheel1X = cx - s * 0.4f;
        canvas.DrawCircle(wheel1X, wheelY, wheelR, fillPaint);
        canvas.DrawCircle(wheel1X, wheelY, wheelR, strokePaint);
        canvas.DrawCircle(wheel1X, wheelY, wheelR * 0.4f, strokePaint);

        // Vorderrad
        float wheel2X = cx + s * 0.55f;
        canvas.DrawCircle(wheel2X, wheelY, wheelR, fillPaint);
        canvas.DrawCircle(wheel2X, wheelY, wheelR, strokePaint);
        canvas.DrawCircle(wheel2X, wheelY, wheelR * 0.4f, strokePaint);
    }

    /// <summary>
    /// Arbeitsmarkt: Arbeiter-Silhouette (Kreis-Kopf + Trapez-Körper) mit kleinem Schraubenschlüssel.
    /// </summary>
    private static void DrawWorkerIcon(SKCanvas canvas, float cx, float cy, float size,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float s = size * 0.4f;

        // Arbeiter leicht nach links versetzt
        float workerCx = cx - s * 0.15f;

        // Kopf (Kreis)
        float headR = s * 0.22f;
        float headY = cy - s * 0.3f;
        canvas.DrawCircle(workerCx, headY, headR, fillPaint);
        canvas.DrawCircle(workerCx, headY, headR, strokePaint);

        // Schutzhelm (Halbkreis über dem Kopf)
        using var helmetPath = new SKPath();
        helmetPath.MoveTo(workerCx - headR * 1.15f, headY - headR * 0.1f);
        helmetPath.QuadTo(workerCx, headY - headR * 1.6f, workerCx + headR * 1.15f, headY - headR * 0.1f);
        helmetPath.Close();
        canvas.DrawPath(helmetPath, fillPaint);
        canvas.DrawPath(helmetPath, strokePaint);

        // Körper (Trapez)
        using var bodyPath = new SKPath();
        float bodyTop = headY + headR + s * 0.02f;
        float bodyBottom = cy + s * 0.6f;
        bodyPath.MoveTo(workerCx - s * 0.18f, bodyTop);
        bodyPath.LineTo(workerCx + s * 0.18f, bodyTop);
        bodyPath.LineTo(workerCx + s * 0.3f, bodyBottom);
        bodyPath.LineTo(workerCx - s * 0.3f, bodyBottom);
        bodyPath.Close();
        canvas.DrawPath(bodyPath, fillPaint);
        canvas.DrawPath(bodyPath, strokePaint);

        // Schraubenschlüssel rechts neben dem Arbeiter
        float wrenchCx = cx + s * 0.55f;
        float wrenchTop = cy - s * 0.35f;
        float wrenchBottom = cy + s * 0.35f;

        // Schaft (vertikale Linie)
        canvas.DrawLine(wrenchCx, wrenchTop + s * 0.15f, wrenchCx, wrenchBottom - s * 0.15f, strokePaint);

        // Oberer Kopf (offenes U)
        using var topJaw = new SKPath();
        topJaw.MoveTo(wrenchCx - s * 0.1f, wrenchTop + s * 0.15f);
        topJaw.LineTo(wrenchCx - s * 0.1f, wrenchTop);
        topJaw.LineTo(wrenchCx + s * 0.1f, wrenchTop);
        topJaw.LineTo(wrenchCx + s * 0.1f, wrenchTop + s * 0.15f);
        canvas.DrawPath(topJaw, strokePaint);

        // Unterer Kopf (offenes U umgedreht)
        using var bottomJaw = new SKPath();
        bottomJaw.MoveTo(wrenchCx - s * 0.1f, wrenchBottom - s * 0.15f);
        bottomJaw.LineTo(wrenchCx - s * 0.1f, wrenchBottom);
        bottomJaw.LineTo(wrenchCx + s * 0.1f, wrenchBottom);
        bottomJaw.LineTo(wrenchCx + s * 0.1f, wrenchBottom - s * 0.15f);
        canvas.DrawPath(bottomJaw, strokePaint);
    }

    /// <summary>
    /// Meisterschaft: Krone mit 3 Zacken und einem Stern in der Mitte.
    /// </summary>
    private static void DrawCrownIcon(SKCanvas canvas, float cx, float cy, float size,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float s = size * 0.4f;

        // Kronen-Körper (5 Punkte: 3 Zacken oben, 2 Täler)
        using var crownPath = new SKPath();
        float crownBottom = cy + s * 0.3f;
        float crownBaseTop = cy - s * 0.1f;
        float peakH = s * 0.55f;

        // Von links nach rechts: Basis links → Spitze links → Tal → Spitze Mitte → Tal → Spitze rechts → Basis rechts
        crownPath.MoveTo(cx - s * 0.65f, crownBottom);
        crownPath.LineTo(cx - s * 0.65f, crownBaseTop);
        crownPath.LineTo(cx - s * 0.45f, cy - peakH);           // Linke Zacke
        crownPath.LineTo(cx - s * 0.22f, crownBaseTop + s * 0.05f); // Tal
        crownPath.LineTo(cx, cy - peakH - s * 0.08f);            // Mittlere Zacke (höchste)
        crownPath.LineTo(cx + s * 0.22f, crownBaseTop + s * 0.05f); // Tal
        crownPath.LineTo(cx + s * 0.45f, cy - peakH);           // Rechte Zacke
        crownPath.LineTo(cx + s * 0.65f, crownBaseTop);
        crownPath.LineTo(cx + s * 0.65f, crownBottom);
        crownPath.Close();

        canvas.DrawPath(crownPath, fillPaint);
        canvas.DrawPath(crownPath, strokePaint);

        // Basis-Band (Querstreifen am unteren Rand der Krone)
        canvas.DrawLine(cx - s * 0.65f, crownBottom - s * 0.12f,
                        cx + s * 0.65f, crownBottom - s * 0.12f, strokePaint);

        // Kugeln auf den Zacken-Spitzen
        float ballR = s * 0.06f;
        canvas.DrawCircle(cx - s * 0.45f, cy - peakH, ballR, fillPaint);
        canvas.DrawCircle(cx, cy - peakH - s * 0.08f, ballR, fillPaint);
        canvas.DrawCircle(cx + s * 0.45f, cy - peakH, ballR, fillPaint);

        // Stern in der Mitte der Krone
        float starCx = cx;
        float starCy = cy + s * 0.02f;
        float outerR = s * 0.14f;
        float innerR = s * 0.06f;

        using var starPath = new SKPath();
        for (int i = 0; i < 5; i++)
        {
            // Äußerer Punkt
            float outerAngle = -90f + i * 72f;
            float outerRad = outerAngle * (float)System.Math.PI / 180f;
            float ox = starCx + outerR * (float)System.Math.Cos(outerRad);
            float oy = starCy + outerR * (float)System.Math.Sin(outerRad);

            // Innerer Punkt (zwischen zwei äußeren)
            float innerAngle = -90f + i * 72f + 36f;
            float innerRad = innerAngle * (float)System.Math.PI / 180f;
            float ix = starCx + innerR * (float)System.Math.Cos(innerRad);
            float iy = starCy + innerR * (float)System.Math.Sin(innerRad);

            if (i == 0)
                starPath.MoveTo(ox, oy);
            else
                starPath.LineTo(ox, oy);

            starPath.LineTo(ix, iy);
        }
        starPath.Close();

        canvas.DrawPath(starPath, fillPaint);
        canvas.DrawPath(starPath, strokePaint);
    }
}
