using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert ein Forschungslabor/Werkstatt-Szene als SkiaSharp-Header.
/// Warme Braun-Töne mit Craft-Orange Akzenten, animierte Elemente:
/// Dampf aus Kolben, rotierende Zahnräder, blinkende Glühbirne, Funkenpartikel.
/// SKPaint-Objekte werden gecacht um Allokationen pro Frame zu vermeiden.
/// </summary>
public class ResearchLabRenderer : IDisposable
{
    private bool _disposed;
    // Animationszeit (wird intern hochgezählt)
    private float _time;

    // Funkenpartikel fuer aktive Forschung (Fixed-Size struct-Pool, 0 GC)
    private const int MaxSparks = 35;
    private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];
    private int _sparkCount;
    private float _sparkTimer;

    // ═══════════════════════════════════════════════════════════════════════
    // FARB-PALETTE
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKColor WallColor = new(0x3E, 0x27, 0x23);         // Dunkles Braun (Wand)
    private static readonly SKColor WallLightColor = new(0x5D, 0x40, 0x37);    // Helleres Braun (Akzent)
    private static readonly SKColor FloorColor = new(0x4E, 0x34, 0x2E);        // Boden
    private static readonly SKColor TableColor = new(0x6D, 0x4C, 0x41);        // Holz-Tisch
    private static readonly SKColor TableTopColor = new(0x8D, 0x6E, 0x63);     // Tischplatte
    private static readonly SKColor ShelfColor = new(0x5D, 0x40, 0x37);        // Regal
    private static readonly SKColor CraftOrange = new(0xEA, 0x58, 0x0C);       // Craft-Orange Akzent
    private static readonly SKColor FlaskGreen = new(0x4C, 0xAF, 0x50);        // Glaskolben grün
    private static readonly SKColor FlaskBlue = new(0x42, 0xA5, 0xF5);         // Glaskolben blau
    private static readonly SKColor FlaskAmber = new(0xFF, 0xB3, 0x00);        // Glaskolben bernstein
    private static readonly SKColor BookRed = new(0xC6, 0x28, 0x28);           // Buch rot
    private static readonly SKColor BookBrown = new(0x79, 0x55, 0x48);         // Buch braun
    private static readonly SKColor BookGreen = new(0x2E, 0x7D, 0x32);         // Buch grün
    private static readonly SKColor GearColor = new(0x78, 0x90, 0x9C);         // Zahnrad-Metall
    private static readonly SKColor BulbGlow = new(0xFF, 0xEB, 0x3B);          // Glühbirnen-Licht
    private static readonly SKColor BlueprintColor = new(0x1A, 0x23, 0x7E, 0x60); // Blaupause
    private static readonly SKColor SteamColor = new(0xB0, 0xBE, 0xC5);       // Dampf

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE PAINTS - STATISCH (feste Farben, unveränderlich)
    // ═══════════════════════════════════════════════════════════════════════

    // Hintergrund
    private static readonly SKPaint PaintWall = new() { Color = WallColor, IsAntialias = false };
    private static readonly SKPaint PaintStripe = new() { Color = WallLightColor, IsAntialias = false };
    private static readonly SKPaint PaintFloor = new() { Color = FloorColor, IsAntialias = false };
    private static readonly SKPaint PaintFloorLine = new() { Color = new SKColor(0x33, 0x1E, 0x1A), IsAntialias = false, StrokeWidth = 2 };
    private static readonly SKPaint PaintBoard = new() { Color = new SKColor(0x44, 0x2C, 0x25), IsAntialias = false, StrokeWidth = 1 };

    // Bücherregal
    private static readonly SKPaint PaintShelf = new() { Color = ShelfColor, IsAntialias = false };
    private static readonly SKPaint PaintShelfSupport = new() { Color = new SKColor(0x4E, 0x34, 0x2E), IsAntialias = false };
    private static readonly SKPaint PaintBookRed = new() { Color = BookRed, IsAntialias = false };
    private static readonly SKPaint PaintBookBrown = new() { Color = BookBrown, IsAntialias = false };
    private static readonly SKPaint PaintBookGreen = new() { Color = BookGreen, IsAntialias = false };
    private static readonly SKPaint PaintBookNavy = new() { Color = new SKColor(0x1A, 0x23, 0x7E), IsAntialias = false };

    // Tisch
    private static readonly SKPaint PaintTableTop = new() { Color = TableTopColor, IsAntialias = false };
    private static readonly SKPaint PaintTableEdge = new() { Color = TableColor, IsAntialias = false };
    private static readonly SKPaint PaintTableLeg = new() { Color = TableColor, IsAntialias = false };

    // Glaskolben (feste Alpha-Werte)
    private static readonly SKPaint PaintFlaskBody = new() { Color = FlaskGreen.WithAlpha(0xB0), IsAntialias = false };
    private static readonly SKPaint PaintFlaskNeck = new() { Color = FlaskGreen.WithAlpha(0x90), IsAntialias = false };
    private static readonly SKPaint PaintFlaskLiquid = new() { Color = FlaskGreen.WithAlpha(0xD0), IsAntialias = false };
    private static readonly SKPaint PaintSmallFlask = new() { Color = FlaskBlue.WithAlpha(0xA0), IsAntialias = false };
    private static readonly SKPaint PaintSmallFlaskNeck = new() { Color = FlaskBlue.WithAlpha(0x80), IsAntialias = false };
    private static readonly SKPaint PaintTinyFlask = new() { Color = FlaskAmber.WithAlpha(0xB0), IsAntialias = false };
    private static readonly SKPaint PaintFlaskShine = new() { Color = new SKColor(0xFF, 0xFF, 0xFF, 0x40), IsAntialias = false };

    // Blaupausen
    private static readonly SKPaint PaintBlueprint = new() { Color = new SKColor(0xE8, 0xEA, 0xED, 0xD0), IsAntialias = false };
    private static readonly SKPaint PaintBlueprintLine = new() { Color = BlueprintColor, IsAntialias = false, StrokeWidth = 1 };
    private static readonly SKPaint PaintStamp = new() { Color = new SKColor(0xC6, 0x28, 0x28, 0x80), IsAntialias = false };

    // Werkzeuge
    private static readonly SKPaint PaintHammerHandle = new() { Color = new SKColor(0x8D, 0x6E, 0x63), IsAntialias = false };
    private static readonly SKPaint PaintHammerHead = new() { Color = new SKColor(0x60, 0x60, 0x60), IsAntialias = false };
    private static readonly SKPaint PaintScrewdriverGrip = new() { Color = CraftOrange, IsAntialias = false };
    private static readonly SKPaint PaintScrewdriverBlade = new() { Color = new SKColor(0x90, 0x90, 0x90), IsAntialias = false };

    // Zahnräder
    private static readonly SKPaint PaintGear = new() { Color = GearColor, IsAntialias = false, Style = SKPaintStyle.Fill };
    private static readonly SKPaint PaintGearHole = new() { Color = WallColor, IsAntialias = false, Style = SKPaintStyle.Fill };
    private static readonly SKPaint PaintGearTooth = new() { Color = GearColor, IsAntialias = false };
    private static readonly SKPaint PaintGearSpoke = new() { Color = new SKColor(0x60, 0x70, 0x78), IsAntialias = false, StrokeWidth = 2 };

    // Glühbirne (statische Teile)
    private static readonly SKPaint PaintWire = new() { Color = new SKColor(0x40, 0x40, 0x40), IsAntialias = false, StrokeWidth = 2 };
    private static readonly SKPaint PaintSocket = new() { Color = new SKColor(0x60, 0x60, 0x60), IsAntialias = false };

    // Fortschrittsbalken (statische Teile)
    private static readonly SKPaint PaintProgressBg = new() { Color = new SKColor(0x20, 0x15, 0x12), IsAntialias = false };
    private static readonly SKPaint PaintProgressFill = new() { Color = CraftOrange, IsAntialias = false };
    private static readonly SKPaint PaintProgressBorder = new() { Color = new SKColor(0x6D, 0x4C, 0x41), IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE PAINTS - DYNAMISCH (Farbe wird pro Frame mutiert)
    // ═══════════════════════════════════════════════════════════════════════

    // Glühbirne (animierte Alpha-Werte)
    private readonly SKPaint _bulbGlowPaint = new() { IsAntialias = false };
    private readonly SKPaint _bulbCorePaint = new() { IsAntialias = false };
    private readonly SKPaint _bulbBodyPaint = new() { IsAntialias = false };

    // Dampf (variables Alpha pro Partikel)
    private readonly SKPaint _steamPaint = new() { IsAntialias = false };

    // Funken (variable Farbe + Alpha pro Partikel)
    private readonly SKPaint _sparkPaint = new() { IsAntialias = false };

    // Fortschrittsbalken Glow (animiertes Alpha)
    private readonly SKPaint _progressGlowPaint = new() { IsAntialias = false };

    /// <summary>
    /// Rendert die Forschungslabor-Szene.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfügbarer Zeichenbereich.</param>
    /// <param name="hasActiveResearch">Ob gerade geforscht wird (für Funken + Fortschritt).</param>
    /// <param name="progress">Forschungsfortschritt 0.0 bis 1.0.</param>
    /// <param name="deltaTime">Zeitdelta seit letztem Frame in Sekunden.</param>
    public void Render(SKCanvas canvas, SKRect bounds, bool hasActiveResearch, float progress, float deltaTime)
    {
        _time += deltaTime;

        float w = bounds.Width;
        float h = bounds.Height;
        float left = bounds.Left;
        float top = bounds.Top;

        // Hintergrund: Werkstatt-Wand
        DrawBackground(canvas, left, top, w, h);

        // Holzregal links mit Büchern
        DrawBookshelf(canvas, left, top, w, h);

        // Tisch in der Mitte
        DrawTable(canvas, left, top, w, h);

        // Glaskolben/Fläschchen auf dem Tisch
        DrawFlasks(canvas, left, top, w, h);

        // Blaupausen auf dem Tisch
        DrawBlueprints(canvas, left, top, w, h);

        // Werkzeuge auf dem Tisch
        DrawTools(canvas, left, top, w, h);

        // Zahnräder (rechts oben, an der Wand)
        DrawGears(canvas, left, top, w, h);

        // Glühbirne (oben Mitte, an der Decke)
        DrawLightBulb(canvas, left, top, w, h);

        // Dampf aus dem großen Kolben
        DrawSteam(canvas, left, top, w, h);

        // Funkenpartikel bei aktiver Forschung
        if (hasActiveResearch)
        {
            UpdateAndDrawSparks(canvas, left, top, w, h, deltaTime);
        }
        else
        {
            _sparkCount = 0;
        }

        // Fortschrittsbalken bei aktiver Forschung
        if (hasActiveResearch)
        {
            DrawProgressBar(canvas, left, top, w, h, progress);
        }
    }

    /// <summary>
    /// Zeichnet den Werkstatt-Hintergrund mit Holzwand und Boden.
    /// </summary>
    private static void DrawBackground(SKCanvas canvas, float left, float top, float w, float h)
    {
        // Oberer Wand-Bereich (dunkel, leicht bläulich)
        canvas.DrawRect(left, top, w, h, PaintWall);

        // Horizontale Holzleisten-Akzente (3 Streifen)
        float stripeH = 3;
        canvas.DrawRect(left, top + h * 0.15f, w, stripeH, PaintStripe);
        canvas.DrawRect(left, top + h * 0.35f, w, stripeH, PaintStripe);
        canvas.DrawRect(left, top + h * 0.55f, w, stripeH, PaintStripe);

        // Boden (untere 30%)
        float floorY = top + h * 0.70f;
        canvas.DrawRect(left, floorY, w, h * 0.30f, PaintFloor);

        // Boden-Linie (Übergang Wand -> Boden)
        canvas.DrawLine(left, floorY, left + w, floorY, PaintFloorLine);

        // Dielenmuster im Boden
        for (float x = left; x < left + w; x += 40)
        {
            canvas.DrawLine(x, floorY, x, top + h, PaintBoard);
        }
    }

    /// <summary>
    /// Zeichnet ein Holzregal links mit gestapelten Büchern.
    /// </summary>
    private static void DrawBookshelf(SKCanvas canvas, float left, float top, float w, float h)
    {
        float shelfX = left + w * 0.04f;
        float shelfW = w * 0.15f;

        // Regal-Bretter (2 Stück)
        float shelf1Y = top + h * 0.25f;
        float shelf2Y = top + h * 0.48f;
        float shelfThick = 4;

        canvas.DrawRect(shelfX, shelf1Y, shelfW, shelfThick, PaintShelf);
        canvas.DrawRect(shelfX, shelf2Y, shelfW, shelfThick, PaintShelf);

        // Regal-Stützen
        canvas.DrawRect(shelfX, shelf1Y, 3, shelf2Y - shelf1Y + shelfThick, PaintShelfSupport);
        canvas.DrawRect(shelfX + shelfW - 3, shelf1Y, 3, shelf2Y - shelf1Y + shelfThick, PaintShelfSupport);

        // Bücher auf dem oberen Regal (gecachte Paints pro Buchfarbe)
        SKPaint[] bookPaints = [PaintBookRed, PaintBookBrown, PaintBookGreen, PaintBookNavy, PaintBookRed];
        float bookX = shelfX + 3;
        for (int i = 0; i < 5; i++)
        {
            float bookW = shelfW / 6.5f;
            float bookH = 12 + (i % 3) * 4;
            canvas.DrawRect(bookX, shelf1Y - bookH, bookW, bookH, bookPaints[i]);
            bookX += bookW + 1;
        }

        // Bücher auf dem unteren Regal (liegend)
        canvas.DrawRect(shelfX + 4, shelf2Y - 5, shelfW * 0.5f, 5, PaintBookBrown);
        canvas.DrawRect(shelfX + 4, shelf2Y - 9, shelfW * 0.45f, 4, PaintBookNavy);
    }

    /// <summary>
    /// Zeichnet den Arbeitstisch in der Mitte der Szene.
    /// </summary>
    private static void DrawTable(SKCanvas canvas, float left, float top, float w, float h)
    {
        float tableX = left + w * 0.20f;
        float tableW = w * 0.60f;
        float tableY = top + h * 0.58f;
        float tableH = 8;
        float legH = h * 0.12f;

        // Tischplatte (obere Fläche - heller)
        canvas.DrawRect(tableX, tableY, tableW, tableH, PaintTableTop);

        // Tischkante (Schatten)
        canvas.DrawRect(tableX, tableY + tableH, tableW, 3, PaintTableEdge);

        // Tischbeine
        float legW = 6;
        canvas.DrawRect(tableX + 8, tableY + tableH + 3, legW, legH, PaintTableLeg);
        canvas.DrawRect(tableX + tableW - 14, tableY + tableH + 3, legW, legH, PaintTableLeg);
    }

    /// <summary>
    /// Zeichnet Glaskolben und Fläschchen auf dem Tisch.
    /// Der große Kolben dampft (Animation in DrawSteam).
    /// Alle Kolben-Paints haben feste Alpha-Werte und sind statisch gecacht.
    /// </summary>
    private void DrawFlasks(SKCanvas canvas, float left, float top, float w, float h)
    {
        float tableY = top + h * 0.58f;

        // Großer Kolben (Mitte-links) - der mit dem Dampf
        float bigFlaskX = left + w * 0.35f;
        float bigFlaskBottom = tableY;
        float bigFlaskW = 16;
        float bigFlaskH = 24;

        // Glaskolben-Körper
        canvas.DrawRect(bigFlaskX, bigFlaskBottom - bigFlaskH, bigFlaskW, bigFlaskH, PaintFlaskBody);

        // Hals (schmaler oben)
        canvas.DrawRect(bigFlaskX + 5, bigFlaskBottom - bigFlaskH - 8, 6, 8, PaintFlaskNeck);

        // Flüssigkeitslevel (pulsierende Füllhöhe - nur Position animiert, Paint bleibt gleich)
        float fillPulse = 0.6f + MathF.Sin(_time * 1.5f) * 0.1f;
        float fillH = bigFlaskH * fillPulse;
        canvas.DrawRect(bigFlaskX + 1, bigFlaskBottom - fillH, bigFlaskW - 2, fillH - 1, PaintFlaskLiquid);

        // Kleiner Kolben blau (rechts davon)
        float smallFlaskX = left + w * 0.48f;
        float smallFlaskH = 16;
        canvas.DrawRect(smallFlaskX, bigFlaskBottom - smallFlaskH, 10, smallFlaskH, PaintSmallFlask);
        canvas.DrawRect(smallFlaskX + 3, bigFlaskBottom - smallFlaskH - 5, 4, 5, PaintSmallFlaskNeck);

        // Kleines Fläschchen bernstein (ganz rechts)
        float tinyFlaskX = left + w * 0.55f;
        float tinyFlaskH = 12;
        canvas.DrawRect(tinyFlaskX, bigFlaskBottom - tinyFlaskH, 8, tinyFlaskH, PaintTinyFlask);

        // Glanzlicht auf Kolben (kleine weiße Linie)
        canvas.DrawRect(bigFlaskX + 2, bigFlaskBottom - bigFlaskH + 2, 2, bigFlaskH - 6, PaintFlaskShine);
    }

    /// <summary>
    /// Zeichnet Blaupausen/Pläne auf dem Tisch.
    /// </summary>
    private static void DrawBlueprints(SKCanvas canvas, float left, float top, float w, float h)
    {
        float tableY = top + h * 0.58f;

        // Großer Plan (leicht schräg simuliert durch versetztes Rechteck)
        float bpX = left + w * 0.60f;
        float bpW = w * 0.14f;
        float bpH = 18;

        canvas.DrawRect(bpX, tableY - bpH, bpW, bpH, PaintBlueprint);

        // Linien auf der Blaupause
        for (float y = tableY - bpH + 4; y < tableY - 3; y += 4)
        {
            canvas.DrawLine(bpX + 3, y, bpX + bpW - 3, y, PaintBlueprintLine);
        }

        // Kleiner roter "Stempel" Punkt
        canvas.DrawCircle(bpX + bpW - 6, tableY - 5, 3, PaintStamp);
    }

    /// <summary>
    /// Zeichnet Werkzeuge (Hammer, Schraubendreher) auf dem Tisch.
    /// </summary>
    private static void DrawTools(SKCanvas canvas, float left, float top, float w, float h)
    {
        float tableY = top + h * 0.58f;

        // Hammer (links auf dem Tisch)
        float hammerX = left + w * 0.25f;
        // Stiel
        canvas.DrawRect(hammerX, tableY - 14, 3, 14, PaintHammerHandle);
        // Kopf
        canvas.DrawRect(hammerX - 4, tableY - 16, 11, 5, PaintHammerHead);

        // Schraubendreher (rechts auf dem Tisch, liegend)
        float sdX = left + w * 0.68f;
        canvas.DrawRect(sdX, tableY - 4, 12, 4, PaintScrewdriverGrip);
        canvas.DrawRect(sdX + 12, tableY - 3, 10, 2, PaintScrewdriverBlade);
    }

    /// <summary>
    /// Zeichnet zwei rotierende Zahnräder an der Wand (rechts oben).
    /// </summary>
    private void DrawGears(SKCanvas canvas, float left, float top, float w, float h)
    {
        float gearCenterX1 = left + w * 0.85f;
        float gearCenterY1 = top + h * 0.22f;
        float gearRadius1 = 14;

        float gearCenterX2 = gearCenterX1 + 18;
        float gearCenterY2 = gearCenterY1 + 14;
        float gearRadius2 = 10;

        // Beide Zahnräder rotieren gegenläufig
        float angle1 = _time * 0.8f;
        float angle2 = -_time * 1.1f;

        DrawSingleGear(canvas, gearCenterX1, gearCenterY1, gearRadius1, angle1, 8);
        DrawSingleGear(canvas, gearCenterX2, gearCenterY2, gearRadius2, angle2, 6);
    }

    /// <summary>
    /// Zeichnet ein einzelnes Zahnrad mit Zähnen.
    /// Nutzt statisch gecachte Paints (Farben ändern sich nie).
    /// </summary>
    private static void DrawSingleGear(SKCanvas canvas, float cx, float cy, float radius, float angle, int teeth)
    {
        // Äußerer Ring
        canvas.DrawCircle(cx, cy, radius, PaintGear);

        // Innerer Kreis (Loch)
        canvas.DrawCircle(cx, cy, radius * 0.35f, PaintGearHole);

        // Zähne
        float toothLen = radius * 0.35f;
        float toothW = 4;
        for (int i = 0; i < teeth; i++)
        {
            float a = angle + i * (MathF.PI * 2 / teeth);
            float tx = cx + MathF.Cos(a) * radius;
            float ty = cy + MathF.Sin(a) * radius;
            float dx = MathF.Cos(a) * toothLen;
            float dy = MathF.Sin(a) * toothLen;

            // Zahn als Linie mit Breite
            using var path = new SKPath();
            float perpX = -MathF.Sin(a) * toothW / 2;
            float perpY = MathF.Cos(a) * toothW / 2;
            path.MoveTo(tx + perpX, ty + perpY);
            path.LineTo(tx + dx + perpX, ty + dy + perpY);
            path.LineTo(tx + dx - perpX, ty + dy - perpY);
            path.LineTo(tx - perpX, ty - perpY);
            path.Close();
            canvas.DrawPath(path, PaintGearTooth);
        }

        // Speichen (Kreuz im Inneren)
        for (int i = 0; i < 4; i++)
        {
            float a = angle + i * (MathF.PI / 2);
            float innerR = radius * 0.35f;
            float outerR = radius * 0.85f;
            canvas.DrawLine(
                cx + MathF.Cos(a) * innerR, cy + MathF.Sin(a) * innerR,
                cx + MathF.Cos(a) * outerR, cy + MathF.Sin(a) * outerR,
                PaintGearSpoke);
        }
    }

    /// <summary>
    /// Zeichnet eine animierte Glühbirne an der Decke (oben Mitte).
    /// Blinkt sanft auf und ab, heller bei aktiver Forschung.
    /// Dynamische Paints werden in-place mutiert (nur Color-Property ändern).
    /// </summary>
    private void DrawLightBulb(SKCanvas canvas, float left, float top, float w, float h)
    {
        float bulbX = left + w * 0.50f;
        float bulbY = top + h * 0.06f;

        // Kabel von der Decke
        canvas.DrawLine(bulbX, top, bulbX, bulbY, PaintWire);

        // Fassung
        canvas.DrawRect(bulbX - 4, bulbY, 8, 5, PaintSocket);

        // Glühbirne (pulsierendes Leuchten)
        float glowIntensity = 0.6f + MathF.Sin(_time * 2.5f) * 0.4f;
        byte glowAlpha = (byte)(glowIntensity * 200);

        // Glow-Kreis (größer, transparent) - Farbe in-place mutieren
        _bulbGlowPaint.Color = BulbGlow.WithAlpha((byte)(glowAlpha / 3));
        canvas.DrawCircle(bulbX, bulbY + 11, 12, _bulbGlowPaint);

        // Birne selbst
        _bulbBodyPaint.Color = BulbGlow.WithAlpha(glowAlpha);
        canvas.DrawCircle(bulbX, bulbY + 11, 6, _bulbBodyPaint);

        // Weißer Kern
        _bulbCorePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(glowIntensity * 160));
        canvas.DrawCircle(bulbX, bulbY + 10, 3, _bulbCorePaint);
    }

    /// <summary>
    /// Zeichnet aufsteigenden Dampf aus dem großen Glaskolben.
    /// Wiederverwendet einen einzelnen Paint pro Partikel (Farbe wird in-place gesetzt).
    /// </summary>
    private void DrawSteam(SKCanvas canvas, float left, float top, float w, float h)
    {
        float flaskTopX = left + w * 0.35f + 8; // Mitte des großen Kolbens
        float flaskTopY = top + h * 0.58f - 32;  // Über dem Kolben-Hals

        // 5 Dampfpartikel in verschiedenen Phasen
        for (int p = 0; p < 5; p++)
        {
            float phase = (_time * 0.6f + p * 0.8f) % 3.5f;
            if (phase > 2.5f) continue; // Pause

            float progress = phase / 2.5f; // 0 bis 1
            float steamX = flaskTopX + MathF.Sin(progress * 4f + p * 1.3f) * 6;
            float steamY = flaskTopY - progress * 20;
            byte alpha = (byte)((1.0f - progress) * 100);
            float size = 3 + progress * 5;

            // Farbe in-place auf gecachtem Paint mutieren
            _steamPaint.Color = SteamColor.WithAlpha(alpha);
            canvas.DrawCircle(steamX, steamY, size, _steamPaint);
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet Funkenpartikel bei aktiver Forschung.
    /// Funken entstehen am Tisch und fliegen nach oben/seitlich.
    /// Wiederverwendet einen einzelnen Paint für alle Partikel.
    /// </summary>
    private void UpdateAndDrawSparks(SKCanvas canvas, float left, float top, float w, float h, float deltaTime)
    {
        _sparkTimer += deltaTime;

        // Alle ~0.15s neue Funken erzeugen
        if (_sparkTimer >= 0.15f && _sparkCount < MaxSparks)
        {
            _sparkTimer = 0;
            float spawnX = left + w * 0.40f + Random.Shared.Next(0, (int)(w * 0.25f));
            float spawnY = top + h * 0.56f;

            _sparks[_sparkCount++] = new SparkParticle
            {
                X = spawnX,
                Y = spawnY,
                VelocityX = (Random.Shared.NextSingle() - 0.5f) * 40,
                VelocityY = -(20 + Random.Shared.NextSingle() * 30),
                Life = 1.0f,
                Size = 1.5f + Random.Shared.NextSingle() * 2
            };
        }

        // Partikel aktualisieren und zeichnen (Compact-Loop)
        int aliveCount = 0;
        for (int i = 0; i < _sparkCount; i++)
        {
            var spark = _sparks[i];
            spark.X += spark.VelocityX * deltaTime;
            spark.Y += spark.VelocityY * deltaTime;
            spark.VelocityY += 15 * deltaTime; // Leichte Gravitation
            spark.Life -= deltaTime * 1.2f;

            if (spark.Life <= 0) continue;

            _sparks[aliveCount++] = spark;

            // Farbe: Orange -> Gelb -> verblassend (in-place mutieren)
            byte alpha = (byte)(spark.Life * 255);
            byte red = 0xFF;
            byte green = (byte)(0x8B + (1.0f - spark.Life) * 0x74);
            _sparkPaint.Color = new SKColor(red, green, 0x00, alpha);
            canvas.DrawCircle(spark.X, spark.Y, spark.Size * spark.Life, _sparkPaint);
        }
        _sparkCount = aliveCount;
    }

    /// <summary>
    /// Zeichnet einen glühenden Fortschrittsbalken am unteren Rand.
    /// Statische Paints für Hintergrund/Füllung/Rahmen, dynamischer Paint für Glow.
    /// </summary>
    private void DrawProgressBar(SKCanvas canvas, float left, float top, float w, float h, float progress)
    {
        float barX = left + w * 0.10f;
        float barW = w * 0.80f;
        float barY = top + h * 0.90f;
        float barH = 6;

        // Hintergrund
        canvas.DrawRect(barX, barY, barW, barH, PaintProgressBg);

        // Fortschritt (Craft-Orange)
        float fillW = barW * Math.Clamp(progress, 0, 1);
        if (fillW > 0)
        {
            canvas.DrawRect(barX, barY, fillW, barH, PaintProgressFill);

            // Glow-Effekt am Ende des Balkens (dynamisches Alpha)
            float glowPulse = 0.5f + MathF.Sin(_time * 4f) * 0.5f;
            byte glowAlpha = (byte)(glowPulse * 120);
            _progressGlowPaint.Color = CraftOrange.WithAlpha(glowAlpha);
            float glowX = barX + fillW - 4;
            canvas.DrawRect(glowX, barY - 2, 8, barH + 4, _progressGlowPaint);
        }

        // Rahmen
        canvas.DrawRect(barX, barY, barW, barH, PaintProgressBorder);
    }

    /// <summary>
    /// Funkenpartikel-Daten.
    /// </summary>
    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, Size;
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bulbGlowPaint?.Dispose();
        _bulbCorePaint?.Dispose();
        _bulbBodyPaint?.Dispose();
        _steamPaint?.Dispose();
        _sparkPaint?.Dispose();
        _progressGlowPaint?.Dispose();
    }
}
