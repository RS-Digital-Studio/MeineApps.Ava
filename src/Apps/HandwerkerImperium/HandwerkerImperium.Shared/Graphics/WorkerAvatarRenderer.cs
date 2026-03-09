using SkiaSharp;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Generiert deterministische Pixel-Art Worker-Avatare aus einem Seed-String.
/// Bitmaps werden intern gecacht - Caller darf NICHT disposen!
/// Das gecachte Bitmap wird direkt zurueckgegeben (kein Copy, keine Allokation).
/// Statische wiederverwendbare SKPaint-Instanzen vermeiden Allokationen bei Cache-Miss.
/// </summary>
public sealed class WorkerAvatarRenderer
{
    // AI-Tier-Portrait-Service (optional, Fallback auf prozedurale Generierung)
    private static IGameAssetService? s_assetService;

    /// <summary>
    /// Initialisiert den AI-Asset-Service für Tier-Portraits.
    /// </summary>
    public static void InitializeAssetService(IGameAssetService assetService)
    {
        s_assetService = assetService;
    }

    // ═══════════════════════════════════════════════════════════════════
    // WIEDERVERWENDBARE STATISCHE SKPAINT-INSTANZEN
    // Sicher weil alle Aufrufe auf dem UI-Thread stattfinden.
    // ═══════════════════════════════════════════════════════════════════

    // Fill-Paint ohne Antialiasing (Pixel-Art: Koerper, Haare, Helm, Augen, Accessoires)
    private static readonly SKPaint s_fillNoAA = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    // Fill-Paint mit Antialiasing (Ovale, Kreise bei groesseren Scales: Kopf, Augen, Wangen)
    private static readonly SKPaint s_fillAA = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Stroke-Paint ohne Antialiasing (Brille, Pflaster-Kreuz, Schutzbrille)
    private static readonly SKPaint s_strokeNoAA = new() { IsAntialias = false, Style = SKPaintStyle.Stroke };

    // Wiederverwendbarer SKPath (fuer DrawSadEye, DrawMouth)
    private static readonly SKPath s_cachedPath = new();

    // Gecachte Asset-Pfade pro Tier (keine String-Allokation im Render-Pfad)
    private static readonly string[] s_tierAssetPaths = Enumerable.Range(1, 10)
        .Select(i => $"workers/tier_{i:D2}.webp")
        .ToArray();

    // ═══════════════════════════════════════════════════════════════════
    // FARB-PALETTEN
    // ═══════════════════════════════════════════════════════════════════

    // 6 Hauttoene
    private static readonly SKColor[] SkinTones =
    [
        new SKColor(0xFF, 0xDB, 0xAC), // Light
        new SKColor(0xF1, 0xC2, 0x7D), // Fair
        new SKColor(0xE0, 0xAC, 0x69), // Medium
        new SKColor(0xC6, 0x8C, 0x53), // Tan
        new SKColor(0x8D, 0x5E, 0x3C), // Brown
        new SKColor(0x6E, 0x40, 0x20)  // Dark
    ];

    // Tier -> Helm/Hut-Farbe (alle 10 Tiers)
    private static readonly Dictionary<WorkerTier, SKColor> TierHatColors = new()
    {
        { WorkerTier.F, new SKColor(0x9E, 0x9E, 0x9E) },          // Grey
        { WorkerTier.E, new SKColor(0x4C, 0xAF, 0x50) },          // Green
        { WorkerTier.D, new SKColor(0x21, 0x96, 0xF3) },          // Blue
        { WorkerTier.C, new SKColor(0x9C, 0x27, 0xB0) },          // Purple
        { WorkerTier.B, new SKColor(0xFF, 0xC1, 0x07) },          // Gold
        { WorkerTier.A, new SKColor(0xF4, 0x43, 0x36) },          // Red
        { WorkerTier.S, new SKColor(0xFF, 0x98, 0x00) },          // Orange
        { WorkerTier.SS, new SKColor(0xE0, 0x40, 0xFB) },         // Pink
        { WorkerTier.SSS, new SKColor(0x7C, 0x4D, 0xFF) },        // DeepPurple
        { WorkerTier.Legendary, new SKColor(0xFF, 0xD7, 0x00) }   // Gold (glaenzend)
    };

    private enum MoodBucket { High, Mid, Low }

    // Cache: "id|tier|moodBucket" -> direkte Referenz (Bitmap bleibt im Speicher, wird nie kopiert)
    private static readonly Dictionary<string, SKBitmap> _cache = new();
    private static readonly object _cacheLock = new();

    // Haarfarben fuer weibliche/maennliche Worker
    private static readonly SKColor[] HairColors =
    [
        new SKColor(0x3E, 0x27, 0x23), // Dunkelbraun
        new SKColor(0x5D, 0x40, 0x37), // Mittelbraun
        new SKColor(0x79, 0x55, 0x48), // Hellbraun
        new SKColor(0x21, 0x21, 0x21), // Schwarz
        new SKColor(0xBF, 0x36, 0x0C), // Rot
        new SKColor(0xF9, 0xA8, 0x25)  // Blond
    ];

    // 6 verschiedene Arbeitskleidungs-Farben (gecacht statt pro Aufruf alloziert)
    private static readonly SKColor[] WorkColors =
    [
        new(0x42, 0xA5, 0xF5), // Blau (Mechaniker)
        new(0x66, 0xBB, 0x6A), // Gruen (Gaertner)
        new(0xEF, 0x6C, 0x00), // Orange (Bauarbeiter)
        new(0x78, 0x90, 0x9C), // Blaugrau (Installateur)
        new(0x8D, 0x6E, 0x63), // Braun (Schreiner)
        new(0x5C, 0x6B, 0xC0), // Indigo (Elektriker)
    ];

    /// <summary>
    /// Rendert einen deterministischen Pixel-Art Avatar fuer die gegebenen Worker-Parameter.
    /// Gibt ein gecachtes Bitmap zurueck - Caller darf NICHT disposen!
    /// </summary>
    public static SKBitmap RenderAvatar(string idSeed, WorkerTier tier, decimal mood, int size, bool isFemale = false)
    {
        // Groesse auf erlaubte Werte begrenzen
        size = size switch
        {
            <= 32 => 32,
            <= 64 => 64,
            _ => 128
        };

        var moodBucket = GetMoodBucket(mood);

        // AI-Verfügbarkeit im Cache-Key: Wenn AI-Portrait nachgeladen wird,
        // erzeugt der neue Key einen Cache-Miss → AI-Version ersetzt prozedurale
        var tierIdx = (int)tier;
        bool hasAI = tierIdx < s_tierAssetPaths.Length
                     && s_assetService?.GetBitmap(s_tierAssetPaths[tierIdx]) != null;
        string cacheKey = $"{idSeed}|{tier}|{moodBucket}|{size}|{(isFemale ? "f" : "m")}{(hasAI ? "|ai" : "")}";

        // Cache pruefen - direkte Referenz, kein Copy
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        // AI-Tier-Portrait versuchen (skaliert in Zielgröße)
        var tierPortrait = GetTierPortrait(tier);
        if (tierPortrait != null)
        {
            var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                var srcRect = new SKRect(0, 0, tierPortrait.Width, tierPortrait.Height);
                var dstRect = new SKRect(0, 0, size, size);
                canvas.DrawBitmap(tierPortrait, srcRect, dstRect);
            }

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var existing))
                {
                    bitmap.Dispose();
                    return existing;
                }
                _cache[cacheKey] = bitmap;
                if (_cache.Count > 200) PruneCache();
            }
            return bitmap;
        }

        // Prozedurales Pixel-Art Rendering (Fallback)
        var procBitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(procBitmap))
        {
            canvas.Clear(SKColors.Transparent);

            int hash = GetStableHash(idSeed);
            float scale = size / 32f;

            DrawBody(canvas, hash, scale, isFemale);
            DrawHead(canvas, hash, scale, isFemale);
            DrawHair(canvas, hash, scale, isFemale);
            DrawHat(canvas, tier, hash, scale);
            DrawEyes(canvas, hash, moodBucket, scale, isFemale);
            DrawNose(canvas, hash, scale);
            DrawMouth(canvas, moodBucket, scale, isFemale);
            DrawCheekBlush(canvas, scale, isFemale);
            DrawChinShadow(canvas, hash, scale);
            DrawAccessories(canvas, hash, scale, isFemale);
        }
        var bitmap2 = procBitmap;

        // Im Cache speichern (direkte Referenz, kein Copy)
        lock (_cacheLock)
        {
            // Race-Condition: Anderer Thread koennte bereits gecacht haben
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                bitmap2.Dispose();
                return existing;
            }

            _cache[cacheKey] = bitmap2;

            // Cache-Groesse begrenzen (aelteste Eintraege entfernen)
            if (_cache.Count > 200)
            {
                PruneCache();
            }
        }

        return bitmap2;
    }

    /// <summary>
    /// Zeichnet Schultern/Koerperansatz am unteren Rand (fuer ein vollstaendigeres Bild).
    /// </summary>
    private static void DrawBody(SKCanvas canvas, int hash, float scale, bool isFemale)
    {
        var clothColor = WorkColors[Math.Abs(hash / 13) % WorkColors.Length];
        var clothDark = DarkenColor(clothColor, 0.15f);

        float cx = 16 * scale;
        float shoulderY = 27 * scale;

        if (isFemale)
        {
            // Schmalere Schultern
            s_fillNoAA.Color = clothColor;
            canvas.DrawRect(cx - 10 * scale, shoulderY, 20 * scale, 5 * scale, s_fillNoAA);
            // Kragen (V-Ausschnitt)
            s_fillNoAA.Color = clothDark;
            canvas.DrawRect(cx - 2 * scale, shoulderY, 4 * scale, 2 * scale, s_fillNoAA);
        }
        else
        {
            // Breitere Schultern
            s_fillNoAA.Color = clothColor;
            canvas.DrawRect(cx - 12 * scale, shoulderY, 24 * scale, 5 * scale, s_fillNoAA);
            // Kragen (rund)
            s_fillNoAA.Color = clothDark;
            canvas.DrawRect(cx - 3 * scale, shoulderY, 6 * scale, 1.5f * scale, s_fillNoAA);
        }
    }

    private static void DrawHead(SKCanvas canvas, int hash, float scale, bool isFemale)
    {
        // Hautton aus Hash ableiten
        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var skinColor = SkinTones[skinIndex];

        s_fillNoAA.Color = skinColor;
        float cx = 16 * scale;
        float cy = 18 * scale;

        if (isFemale)
        {
            // Weiblich: Schmalerer, runderer Kopf
            float radiusX = 9.5f * scale;
            float radiusY = 10 * scale;
            canvas.DrawOval(cx, cy, radiusX, radiusY, s_fillNoAA);

            // Kinn: Spitzer zulaufend (schmaler nach unten)
            canvas.DrawOval(cx, cy + 3 * scale, 7 * scale, 6 * scale, s_fillNoAA);
        }
        else
        {
            // Maennlich: Breiterer, kantigerer Kopf
            float radius = 10 * scale;
            canvas.DrawCircle(cx, cy, radius, s_fillNoAA);

            // Kantiger Kiefer (2px breiter auf jeder Seite)
            float jawWidth = 2 * scale;
            float jawTop = 19 * scale;
            float jawHeight = 6 * scale;
            canvas.DrawRect(cx - radius - jawWidth, jawTop, jawWidth + 1 * scale, jawHeight, s_fillNoAA);
            canvas.DrawRect(cx + radius - 1 * scale, jawTop, jawWidth + 1 * scale, jawHeight, s_fillNoAA);

            // Kinn-Kante (breiter als weiblich)
            canvas.DrawRect(cx - 8 * scale, cy + 8 * scale, 16 * scale, 2 * scale, s_fillNoAA);
        }

        // Ohren (gleiche Hautfarbe, bereits gesetzt)
        float earRadius = isFemale ? 2 * scale : 2.5f * scale;
        canvas.DrawCircle(5 * scale, 18 * scale, earRadius, s_fillNoAA);
        canvas.DrawCircle(27 * scale, 18 * scale, earRadius, s_fillNoAA);

        // Weiblich: Ohrring-Punkte (dezentes Gold)
        if (isFemale)
        {
            s_fillNoAA.Color = new SKColor(0xFF, 0xD7, 0x00);
            float earringSize = 1 * scale;
            canvas.DrawCircle(4.5f * scale, 20 * scale, earringSize, s_fillNoAA);
            canvas.DrawCircle(27.5f * scale, 20 * scale, earringSize, s_fillNoAA);
        }
    }

    /// <summary>
    /// Zeichnet geschlechtsspezifische Haare.
    /// Weiblich: Langes wallendes Haar mit Volumen, bis zu den Schultern.
    /// Maennlich: Kurzhaar mit Seitenscheitel, optional Bart-Schatten.
    /// </summary>
    private static void DrawHair(SKCanvas canvas, int hash, float scale, bool isFemale)
    {
        int hairIndex = Math.Abs(hash / 7) % HairColors.Length;
        var hairColor = HairColors[hairIndex];
        var hairDark = DarkenColor(hairColor, 0.2f);

        if (isFemale)
        {
            // ===== Langes wallendes Haar =====

            // Haarvolumen oben am Kopf (unter dem Helm herausschauend)
            s_fillNoAA.Color = hairColor;
            float topY = 13 * scale;
            canvas.DrawRect(6 * scale, topY, 20 * scale, 3 * scale, s_fillNoAA);

            // Linke Seite: Langes Haar bis zur Schulter
            float leftX = 3 * scale;
            float strandTop = 14 * scale;
            canvas.DrawRect(leftX, strandTop, 3 * scale, 14 * scale, s_fillNoAA);
            canvas.DrawRect(leftX - 1 * scale, strandTop + 2 * scale, 2 * scale, 10 * scale, s_fillNoAA);
            // Wellung (dunklere Straehne)
            s_fillNoAA.Color = hairDark;
            canvas.DrawRect(leftX + 1 * scale, strandTop + 4 * scale, 1 * scale, 3 * scale, s_fillNoAA);

            // Rechte Seite: Langes Haar bis zur Schulter
            s_fillNoAA.Color = hairColor;
            float rightX = 26 * scale;
            canvas.DrawRect(rightX, strandTop, 3 * scale, 14 * scale, s_fillNoAA);
            canvas.DrawRect(rightX + 1 * scale, strandTop + 2 * scale, 2 * scale, 10 * scale, s_fillNoAA);
            // Wellung (dunklere Straehne)
            s_fillNoAA.Color = hairDark;
            canvas.DrawRect(rightX + 1 * scale, strandTop + 5 * scale, 1 * scale, 3 * scale, s_fillNoAA);

            // Pony (kurze Fransen ueber der Stirn)
            bool hasBangs = (hash % 3) != 0; // 66% Chance auf Pony
            if (hasBangs)
            {
                s_fillNoAA.Color = hairColor;
                canvas.DrawRect(8 * scale, 14 * scale, 4 * scale, 2 * scale, s_fillNoAA);
                canvas.DrawRect(20 * scale, 14 * scale, 4 * scale, 2 * scale, s_fillNoAA);
                s_fillNoAA.Color = hairDark;
                canvas.DrawRect(12 * scale, 14.5f * scale, 8 * scale, 1.5f * scale, s_fillNoAA);
            }
        }
        else
        {
            // ===== Kurzhaar mit markanter Form =====

            // Seitliche Haare (kurz, unter dem Helm)
            s_fillNoAA.Color = hairColor;
            float hairTop = 13 * scale;
            canvas.DrawRect(6 * scale, hairTop, 4 * scale, 3 * scale, s_fillNoAA);
            canvas.DrawRect(22 * scale, hairTop, 4 * scale, 3 * scale, s_fillNoAA);

            // Koteletten (seitliche Haar-Ansaetze neben Ohren)
            canvas.DrawRect(6 * scale, 15 * scale, 2 * scale, 4 * scale, s_fillNoAA);
            canvas.DrawRect(24 * scale, 15 * scale, 2 * scale, 4 * scale, s_fillNoAA);

            // Bart-Schatten (50% Chance, nur bei dunkleren Haaren)
            bool hasStubble = (hash % 2) == 0 && hairIndex <= 3;
            if (hasStubble)
            {
                s_fillNoAA.Color = new SKColor(hairColor.Red, hairColor.Green, hairColor.Blue, 60);
                // Kinn-Bereich
                canvas.DrawRect(11 * scale, 24 * scale, 10 * scale, 3 * scale, s_fillNoAA);
                // Wangen
                canvas.DrawRect(8 * scale, 22 * scale, 3 * scale, 4 * scale, s_fillNoAA);
                canvas.DrawRect(21 * scale, 22 * scale, 3 * scale, 4 * scale, s_fillNoAA);
            }
        }
    }

    /// <summary>
    /// Zeichnet verschiedene Hut-/Helm-Stile basierend auf Hash und Tier.
    /// 3 Varianten: Bauhelm (Standard), Muetze, Schutzhelm mit Visier.
    /// </summary>
    private static void DrawHat(SKCanvas canvas, WorkerTier tier, int hash, float scale)
    {
        var hatColor = TierHatColors.GetValueOrDefault(tier, new SKColor(0x90, 0x90, 0x90));
        var brimColor = DarkenColor(hatColor, 0.2f);
        int hatStyle = Math.Abs(hash / 11) % 3; // 3 Hut-Varianten

        float cx = 16 * scale;

        switch (hatStyle)
        {
            case 0:
                // Variante 1: Klassischer Bauhelm (abgerundet)
                s_fillNoAA.Color = hatColor;
                canvas.DrawRect(8 * scale, 5 * scale, 16 * scale, 10 * scale, s_fillNoAA);
                s_fillNoAA.Color = brimColor;
                canvas.DrawRect(6 * scale, 13 * scale, 20 * scale, 3 * scale, s_fillNoAA);
                break;

            case 1:
                // Variante 2: Muetze/Kappe (flacher, mit Schirm nach vorne)
                s_fillNoAA.Color = hatColor;
                canvas.DrawRect(7 * scale, 7 * scale, 18 * scale, 8 * scale, s_fillNoAA);
                // Muetzen-Knopf oben
                s_fillNoAA.Color = brimColor;
                canvas.DrawCircle(cx, 7 * scale, 1.5f * scale, s_fillNoAA);
                // Schirm (nach rechts geneigt fuer Charakter)
                canvas.DrawRect(8 * scale, 14 * scale, 14 * scale, 2.5f * scale, s_fillNoAA);
                break;

            case 2:
                // Variante 3: Schutzhelm mit hoher Kuppel
                s_fillNoAA.Color = hatColor;
                canvas.DrawOval(cx, 10 * scale, 9 * scale, 7 * scale, s_fillNoAA);
                // Breite Krempe
                s_fillNoAA.Color = brimColor;
                canvas.DrawRect(5 * scale, 14 * scale, 22 * scale, 2.5f * scale, s_fillNoAA);
                // Mittelstreifen (Helmnaht)
                s_fillNoAA.Color = DarkenColor(hatColor, 0.1f);
                canvas.DrawRect(cx - 0.5f * scale, 4 * scale, 1 * scale, 10 * scale, s_fillNoAA);
                break;
        }

        // Helm-Glanz (nur bei scale >= 2)
        if (scale >= 2)
        {
            s_fillAA.Color = new SKColor(0xFF, 0xFF, 0xFF, 45);
            // Glanz-Streifen auf dem Helm (schraeg, oben)
            canvas.DrawRect(10 * scale, 8 * scale, 8 * scale, 2 * scale, s_fillAA);
        }

        // S+ Tiers: Stern-Markierung auf dem Helm
        if (tier >= WorkerTier.S)
        {
            s_fillNoAA.Color = SKColors.White;
            float sy = 9 * scale;
            float starSize = 2 * scale;
            canvas.DrawRect(cx - starSize / 2, sy - starSize / 2, starSize, starSize, s_fillNoAA);

            if (tier >= WorkerTier.SS)
                canvas.DrawRect(cx + 2 * scale, sy - starSize / 2, starSize, starSize, s_fillNoAA);

            if (tier == WorkerTier.Legendary)
                canvas.DrawRect(cx - 4 * scale, sy - starSize / 2, starSize, starSize, s_fillNoAA);
        }
    }

    private static void DrawEyes(SKCanvas canvas, int hash, MoodBucket mood, float scale, bool isFemale = false)
    {
        float eyeY = 17 * scale;
        float leftEyeX = 13 * scale;
        float rightEyeX = 19 * scale;

        // Augenfarbe aus Hash (weiblich: Chance auf blaue/gruene Augen)
        int eyeVariant = Math.Abs(hash % 5);
        var eyeColor = eyeVariant switch
        {
            0 => new SKColor(0x5D, 0x40, 0x37), // Braun
            1 => new SKColor(0x21, 0x21, 0x21), // Dunkelbraun/Schwarz
            2 when isFemale => new SKColor(0x2E, 0x7D, 0x32), // Gruen (nur weiblich)
            3 when isFemale => new SKColor(0x1E, 0x88, 0xE5), // Blau (nur weiblich)
            _ => new SKColor(0x5D, 0x40, 0x37)  // Braun (Fallback)
        };

        s_fillNoAA.Color = eyeColor;
        float dotSize = isFemale ? 2.2f * scale : 2 * scale; // Weiblich: etwas groessere Augen

        switch (mood)
        {
            case MoodBucket.High:
                if (scale >= 2)
                {
                    // Augenweiss (ovaler Hintergrund)
                    s_fillAA.Color = new SKColor(0xF0, 0xF0, 0xF0);
                    float highWhiteRx = dotSize * 1.3f;
                    float highWhiteRy = dotSize * 1.1f;
                    canvas.DrawOval(leftEyeX, eyeY, highWhiteRx, highWhiteRy, s_fillAA);
                    canvas.DrawOval(rightEyeX, eyeY, highWhiteRx, highWhiteRy, s_fillAA);

                    // Pupille (kleiner als vorher)
                    s_fillNoAA.Color = eyeColor;
                    float highPupilSize = dotSize * 0.7f;
                    canvas.DrawCircle(leftEyeX, eyeY, highPupilSize, s_fillNoAA);
                    canvas.DrawCircle(rightEyeX, eyeY, highPupilSize, s_fillNoAA);

                    // Weisser Glanzpunkt (oben-rechts der Pupille)
                    s_fillAA.Color = SKColors.White;
                    float highHlSize = scale * 0.6f;
                    canvas.DrawCircle(leftEyeX + 0.7f * scale, eyeY - 0.6f * scale, highHlSize, s_fillAA);
                    canvas.DrawCircle(rightEyeX + 0.7f * scale, eyeY - 0.6f * scale, highHlSize, s_fillAA);
                }
                else
                {
                    // Pixel-Art bei 32px
                    canvas.DrawCircle(leftEyeX, eyeY, dotSize, s_fillNoAA);
                    canvas.DrawCircle(rightEyeX, eyeY, dotSize, s_fillNoAA);
                }
                break;

            case MoodBucket.Mid:
                if (scale >= 2)
                {
                    // Augenweiss (ovaler Hintergrund)
                    s_fillAA.Color = new SKColor(0xF0, 0xF0, 0xF0);
                    float midWhiteRx = dotSize * 1.3f * 1.1f;
                    float midWhiteRy = dotSize * 1.1f * 1.1f;
                    canvas.DrawOval(leftEyeX, eyeY, midWhiteRx, midWhiteRy, s_fillAA);
                    canvas.DrawOval(rightEyeX, eyeY, midWhiteRx, midWhiteRy, s_fillAA);

                    // Pupille (kleiner als vorher)
                    s_fillNoAA.Color = eyeColor;
                    float midPupilSize = dotSize * 1.1f * 0.7f;
                    canvas.DrawCircle(leftEyeX, eyeY, midPupilSize, s_fillNoAA);
                    canvas.DrawCircle(rightEyeX, eyeY, midPupilSize, s_fillNoAA);

                    // Weisser Glanzpunkt (oben-rechts der Pupille)
                    s_fillAA.Color = SKColors.White;
                    float midHlSize = scale * 0.6f;
                    canvas.DrawCircle(leftEyeX + 0.7f * scale, eyeY - 0.6f * scale, midHlSize, s_fillAA);
                    canvas.DrawCircle(rightEyeX + 0.7f * scale, eyeY - 0.6f * scale, midHlSize, s_fillAA);
                }
                else
                {
                    // Pixel-Art bei 32px
                    canvas.DrawCircle(leftEyeX, eyeY, dotSize * 1.1f, s_fillNoAA);
                    canvas.DrawCircle(rightEyeX, eyeY, dotSize * 1.1f, s_fillNoAA);
                }
                break;

            case MoodBucket.Low:
                if (scale >= 2)
                {
                    // Augenweiss vor den traurigen Dreiecken
                    s_fillAA.Color = new SKColor(0xF0, 0xF0, 0xF0);
                    float lowWhiteRx = dotSize * 1.3f;
                    float lowWhiteRy = dotSize * 1.1f;
                    canvas.DrawOval(leftEyeX, eyeY, lowWhiteRx, lowWhiteRy, s_fillAA);
                    canvas.DrawOval(rightEyeX, eyeY, lowWhiteRx, lowWhiteRy, s_fillAA);
                }
                s_fillNoAA.Color = eyeColor;
                DrawSadEye(canvas, leftEyeX, eyeY, dotSize);
                DrawSadEye(canvas, rightEyeX, eyeY, dotSize);
                break;
        }

        if (isFemale)
        {
            // Wimpern (2 kleine Striche nach oben-aussen pro Auge)
            s_strokeNoAA.Color = new SKColor(0x21, 0x21, 0x21);
            s_strokeNoAA.StrokeWidth = Math.Max(1, 0.8f * scale);
            // Linkes Auge
            canvas.DrawLine(leftEyeX - 1.5f * scale, eyeY - 2 * scale, leftEyeX - 2.5f * scale, eyeY - 3.5f * scale, s_strokeNoAA);
            canvas.DrawLine(leftEyeX + 0.5f * scale, eyeY - 2.2f * scale, leftEyeX + 0.5f * scale, eyeY - 3.5f * scale, s_strokeNoAA);
            // Rechtes Auge
            canvas.DrawLine(rightEyeX + 1.5f * scale, eyeY - 2 * scale, rightEyeX + 2.5f * scale, eyeY - 3.5f * scale, s_strokeNoAA);
            canvas.DrawLine(rightEyeX - 0.5f * scale, eyeY - 2.2f * scale, rightEyeX - 0.5f * scale, eyeY - 3.5f * scale, s_strokeNoAA);
        }
        else
        {
            // Maennlich: Kraeftige Augenbrauen
            int hairIndex = Math.Abs(hash / 7) % HairColors.Length;
            var browColor = DarkenColor(HairColors[hairIndex], 0.1f);
            s_fillNoAA.Color = browColor;
            float browY = eyeY - 3 * scale;
            float browWidth = 4 * scale;
            float browHeight = 1.2f * scale;
            canvas.DrawRect(leftEyeX - browWidth / 2, browY, browWidth, browHeight, s_fillNoAA);
            canvas.DrawRect(rightEyeX - browWidth / 2, browY, browWidth, browHeight, s_fillNoAA);
        }
    }

    /// <summary>
    /// Zeichnet ein trauriges Auge (Dreieck nach unten).
    /// Nutzt s_fillNoAA (Farbe muss vorher gesetzt sein).
    /// </summary>
    private static void DrawSadEye(SKCanvas canvas, float cx, float cy, float size)
    {
        // Dreieck: nach unten zeigend fuer traurigen Ausdruck
        s_cachedPath.Rewind();
        s_cachedPath.MoveTo(cx - size, cy - size);
        s_cachedPath.LineTo(cx + size, cy - size);
        s_cachedPath.LineTo(cx, cy + size);
        s_cachedPath.Close();
        canvas.DrawPath(s_cachedPath, s_fillNoAA);
    }

    /// <summary>
    /// Zeichnet eine dezente Nase (nur bei scale >= 2, also 64px+).
    /// </summary>
    private static void DrawNose(SKCanvas canvas, int hash, float scale)
    {
        if (scale < 2) return;

        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var noseColor = DarkenColor(SkinTones[skinIndex], 0.12f);

        s_fillAA.Color = noseColor;
        float cx = 16 * scale;
        float noseY = 20 * scale;

        canvas.DrawOval(cx, noseY, 1.8f * scale, 1.2f * scale, s_fillAA);
    }

    private static void DrawMouth(SKCanvas canvas, MoodBucket mood, float scale, bool isFemale = false)
    {
        float mouthY = 22 * scale;
        float cx = 16 * scale;

        if (isFemale)
        {
            // Weiblich: Vollere, rosafarbene Lippen
            var lipColor = new SKColor(0xE0, 0x6B, 0x7A);
            var lipDark = new SKColor(0xC4, 0x55, 0x65);

            float halfWidth = 3.5f * scale;

            switch (mood)
            {
                case MoodBucket.High:
                    // Laecheln: Gefuellter Bogen
                    s_cachedPath.Rewind();
                    s_cachedPath.MoveTo(cx - halfWidth, mouthY);
                    s_cachedPath.QuadTo(cx, mouthY + 3 * scale, cx + halfWidth, mouthY);
                    s_cachedPath.Close();
                    s_fillNoAA.Color = lipColor;
                    canvas.DrawPath(s_cachedPath, s_fillNoAA);
                    s_strokeNoAA.Color = lipDark;
                    s_strokeNoAA.StrokeWidth = Math.Max(1, 0.8f * scale);
                    canvas.DrawPath(s_cachedPath, s_strokeNoAA);
                    break;

                case MoodBucket.Mid:
                    // Neutral: Dezente Lippen
                    s_fillNoAA.Color = lipColor;
                    canvas.DrawRect(cx - halfWidth, mouthY - 0.5f * scale, halfWidth * 2, 1.5f * scale, s_fillNoAA);
                    break;

                case MoodBucket.Low:
                    // Traurig: Bogen nach unten
                    s_cachedPath.Rewind();
                    s_cachedPath.MoveTo(cx - halfWidth, mouthY);
                    s_cachedPath.QuadTo(cx, mouthY - 2 * scale, cx + halfWidth, mouthY);
                    s_strokeNoAA.Color = lipDark;
                    s_strokeNoAA.StrokeWidth = Math.Max(1, 0.8f * scale);
                    canvas.DrawPath(s_cachedPath, s_strokeNoAA);
                    break;
            }
        }
        else
        {
            // Maennlich: Einfachere, breitere Mundlinien
            s_strokeNoAA.Color = new SKColor(0x5D, 0x40, 0x37);
            s_strokeNoAA.StrokeWidth = Math.Max(1, 1.2f * scale);

            float halfWidth = 3.5f * scale;

            switch (mood)
            {
                case MoodBucket.High:
                    canvas.DrawLine(cx - halfWidth, mouthY, cx, mouthY + 2 * scale, s_strokeNoAA);
                    canvas.DrawLine(cx, mouthY + 2 * scale, cx + halfWidth, mouthY, s_strokeNoAA);
                    break;

                case MoodBucket.Mid:
                    canvas.DrawLine(cx - halfWidth, mouthY, cx + halfWidth, mouthY, s_strokeNoAA);
                    break;

                case MoodBucket.Low:
                    canvas.DrawLine(cx - halfWidth, mouthY + 2 * scale, cx, mouthY, s_strokeNoAA);
                    canvas.DrawLine(cx, mouthY, cx + halfWidth, mouthY + 2 * scale, s_strokeNoAA);
                    break;
            }
        }
    }

    /// <summary>
    /// Zeichnet dezente Wangenroete (nur bei scale >= 2).
    /// Weiblich: staerkere Roete, maennlich: subtiler.
    /// </summary>
    private static void DrawCheekBlush(SKCanvas canvas, float scale, bool isFemale)
    {
        if (scale < 2) return;

        byte alpha = isFemale ? (byte)50 : (byte)30;
        s_fillAA.Color = new SKColor(0xFF, 0x99, 0x99, alpha);
        float cx = 16 * scale;
        float cheekY = 21 * scale;
        float radius = isFemale ? 2.5f * scale : 2 * scale;

        canvas.DrawCircle(cx - 6 * scale, cheekY, radius, s_fillAA);
        canvas.DrawCircle(cx + 6 * scale, cheekY, radius, s_fillAA);
    }

    /// <summary>
    /// Zeichnet einen Kinn-Schatten (nur bei scale >= 3, also 96px+).
    /// </summary>
    private static void DrawChinShadow(SKCanvas canvas, int hash, float scale)
    {
        if (scale < 3) return;

        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var shadowColor = DarkenColor(SkinTones[skinIndex], 0.2f).WithAlpha(60);

        s_fillAA.Color = shadowColor;
        float cx = 16 * scale;
        float chinY = 26 * scale;

        canvas.DrawOval(cx, chinY, 7 * scale, 2.5f * scale, s_fillAA);
    }

    /// <summary>
    /// Zeichnet optionale Accessoires fuer mehr Abwechslung.
    /// Hash-basiert: Brille, Schutzbrillen-Halterung, Wangenroetung, Pflaster, Werkzeug am Ohr.
    /// </summary>
    private static void DrawAccessories(SKCanvas canvas, int hash, float scale, bool isFemale)
    {
        int accessory = Math.Abs(hash / 17) % 6; // 6 Moeglichkeiten (0 = nichts)

        float cx = 16 * scale;

        switch (accessory)
        {
            case 1:
                // Brille (runde Glaeser)
                s_strokeNoAA.Color = new SKColor(0x60, 0x60, 0x60);
                s_strokeNoAA.StrokeWidth = Math.Max(1, 0.8f * scale);
                canvas.DrawCircle(13 * scale, 17 * scale, 2.8f * scale, s_strokeNoAA);
                canvas.DrawCircle(19 * scale, 17 * scale, 2.8f * scale, s_strokeNoAA);
                // Nasenstueck
                canvas.DrawLine(15.5f * scale, 17 * scale, 16.5f * scale, 17 * scale, s_strokeNoAA);
                // Buegel
                canvas.DrawLine(10 * scale, 17 * scale, 7 * scale, 16.5f * scale, s_strokeNoAA);
                canvas.DrawLine(22 * scale, 17 * scale, 25 * scale, 16.5f * scale, s_strokeNoAA);
                break;

            case 2:
                // Schutzbrille oben auf dem Kopf (orange Band)
                s_strokeNoAA.Color = new SKColor(0xFF, 0x98, 0x00, 180);
                s_strokeNoAA.StrokeWidth = Math.Max(1, 1.5f * scale);
                canvas.DrawLine(6 * scale, 14 * scale, 26 * scale, 14 * scale, s_strokeNoAA);
                break;

            case 3:
                if (isFemale)
                {
                    // Wangenroetung (dezent rosa)
                    s_fillNoAA.Color = new SKColor(0xFF, 0xAB, 0xAB, 80);
                    canvas.DrawCircle(10 * scale, 20 * scale, 2 * scale, s_fillNoAA);
                    canvas.DrawCircle(22 * scale, 20 * scale, 2 * scale, s_fillNoAA);
                }
                else
                {
                    // Narbe / Pflaster auf Wange
                    s_fillNoAA.Color = new SKColor(0xF5, 0xE6, 0xCC);
                    canvas.DrawRect(21 * scale, 20 * scale, 3 * scale, 3 * scale, s_fillNoAA);
                    // Pflaster-Kreuz
                    s_strokeNoAA.Color = new SKColor(0xCC, 0xAA, 0x88);
                    s_strokeNoAA.StrokeWidth = Math.Max(1, 0.5f * scale);
                    canvas.DrawLine(21.5f * scale, 21.5f * scale, 23.5f * scale, 21.5f * scale, s_strokeNoAA);
                    canvas.DrawLine(22.5f * scale, 20.5f * scale, 22.5f * scale, 22.5f * scale, s_strokeNoAA);
                }
                break;

            case 4:
                // Bleistift hinterm Ohr
                s_fillNoAA.Color = new SKColor(0xFF, 0xD5, 0x4F);
                // Stift-Koerper
                canvas.DrawRect(26 * scale, 12 * scale, 1.5f * scale, 7 * scale, s_fillNoAA);
                // Spitze
                s_fillNoAA.Color = new SKColor(0x4E, 0x34, 0x2E);
                canvas.DrawRect(26 * scale, 19 * scale, 1.5f * scale, 1.5f * scale, s_fillNoAA);
                break;

                // case 0, 5: Keine Accessoires
        }
    }

    /// <summary>
    /// Gibt das AI-generierte Tier-Portrait zurück oder null (Fallback auf prozedural).
    /// </summary>
    private static SKBitmap? GetTierPortrait(WorkerTier tier)
    {
        if (s_assetService == null) return null;
        var tierIdx = (int)tier;
        if (tierIdx < 0 || tierIdx >= s_tierAssetPaths.Length) return null;
        var assetPath = s_tierAssetPaths[tierIdx];
        var portrait = s_assetService.GetBitmap(assetPath);
        if (portrait == null)
            _ = s_assetService.LoadBitmapAsync(assetPath);
        return portrait;
    }

    private static MoodBucket GetMoodBucket(decimal mood)
    {
        if (mood >= 65) return MoodBucket.High;
        if (mood >= 35) return MoodBucket.Mid;
        return MoodBucket.Low;
    }

    /// <summary>
    /// Stabiler Hash aus String (deterministisch, nicht GetHashCode der per Runtime variiert).
    /// </summary>
    private static int GetStableHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        unchecked
        {
            int hash = 17;
            foreach (char c in input)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }

    private static SKColor DarkenColor(SKColor color, float amount)
    {
        float factor = 1.0f - amount;
        return new SKColor(
            (byte)(color.Red * factor),
            (byte)(color.Green * factor),
            (byte)(color.Blue * factor),
            color.Alpha);
    }

    /// <summary>
    /// Entfernt die aelteste Haelfte des Caches wenn er zu gross wird.
    /// </summary>
    private static void PruneCache()
    {
        // Haelfte der Eintraege entfernen (FIFO-aehnlich via Dictionary-Reihenfolge)
        // NICHT disposen! Bitmaps koennten noch von WorkerAvatarControls referenziert werden.
        // GC finalisiert sie wenn keine Referenzen mehr bestehen.
        int toRemove = _cache.Count / 2;
        var keysToRemove = _cache.Keys.Take(toRemove).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }
    }
}
