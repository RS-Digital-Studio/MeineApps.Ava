using SkiaSharp;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Generates deterministic pixel-art worker avatars from a seed string.
/// Caches bitmaps by id+tier+mood_bucket to avoid re-rendering every frame.
/// Caller is responsible for disposing returned SKBitmaps.
/// </summary>
public class WorkerAvatarRenderer
{
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

    // Cache: "id|tier|moodBucket" -> weak reference (bitmap kann GC'd werden)
    private static readonly Dictionary<string, WeakReference<SKBitmap>> _cache = new();
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

    /// <summary>
    /// Renders a deterministic pixel-art avatar for the given worker parameters.
    /// Returns a new SKBitmap that the caller must dispose.
    /// </summary>
    /// <param name="idSeed">Worker ID used as seed for deterministic generation.</param>
    /// <param name="tier">Worker tier (determines hat color).</param>
    /// <param name="mood">Worker mood (0-100, determines expression).</param>
    /// <param name="size">Output size in pixels (32, 64, or 128).</param>
    /// <param name="isFemale">Geschlecht: true = weiblich (laengere Haare, Lippen), false = maennlich (breiterer Kiefer).</param>
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
        string cacheKey = $"{idSeed}|{tier}|{moodBucket}|{size}|{(isFemale ? "f" : "m")}";

        // Cache pruefen
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var weakRef) &&
                weakRef.TryGetTarget(out var cached))
            {
                // Kopie zurueckgeben (Caller verwaltet Disposal)
                return cached.Copy();
            }
        }

        // Neues Bitmap erzeugen
        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
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

        // Im Cache speichern (als weak reference)
        lock (_cacheLock)
        {
            _cache[cacheKey] = new WeakReference<SKBitmap>(bitmap);

            // Cache-Groesse begrenzen
            if (_cache.Count > 200)
            {
                PruneCache();
            }
        }

        // Kopie zurueckgeben
        return bitmap.Copy();
    }

    /// <summary>
    /// Zeichnet Schultern/Koerperansatz am unteren Rand (für ein vollständigeres Bild).
    /// </summary>
    private static void DrawBody(SKCanvas canvas, int hash, float scale, bool isFemale)
    {
        // Kleidungsfarbe aus Hash (6 verschiedene Arbeitskleidungs-Farben)
        var workColors = new SKColor[]
        {
            new(0x42, 0xA5, 0xF5), // Blau (Mechaniker)
            new(0x66, 0xBB, 0x6A), // Gruen (Gaertner)
            new(0xEF, 0x6C, 0x00), // Orange (Bauarbeiter)
            new(0x78, 0x90, 0x9C), // Blaugrau (Installateur)
            new(0x8D, 0x6E, 0x63), // Braun (Schreiner)
            new(0x5C, 0x6B, 0xC0), // Indigo (Elektriker)
        };
        var clothColor = workColors[Math.Abs(hash / 13) % workColors.Length];
        var clothDark = DarkenColor(clothColor, 0.15f);

        using var bodyPaint = new SKPaint { Color = clothColor, IsAntialias = false };
        using var bodyDarkPaint = new SKPaint { Color = clothDark, IsAntialias = false };

        float cx = 16 * scale;
        float shoulderY = 27 * scale;

        if (isFemale)
        {
            // Schmalere Schultern
            canvas.DrawRect(cx - 10 * scale, shoulderY, 20 * scale, 5 * scale, bodyPaint);
            // Kragen (V-Ausschnitt)
            canvas.DrawRect(cx - 2 * scale, shoulderY, 4 * scale, 2 * scale, bodyDarkPaint);
        }
        else
        {
            // Breitere Schultern
            canvas.DrawRect(cx - 12 * scale, shoulderY, 24 * scale, 5 * scale, bodyPaint);
            // Kragen (rund)
            canvas.DrawRect(cx - 3 * scale, shoulderY, 6 * scale, 1.5f * scale, bodyDarkPaint);
        }
    }

    private static void DrawHead(SKCanvas canvas, int hash, float scale, bool isFemale)
    {
        // Hautton aus Hash ableiten
        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var skinColor = SkinTones[skinIndex];

        using (var headPaint = new SKPaint { Color = skinColor, IsAntialias = false })
        {
            float cx = 16 * scale;
            float cy = 18 * scale;

            if (isFemale)
            {
                // Weiblich: Schmalerer, runderer Kopf
                float radiusX = 9.5f * scale;
                float radiusY = 10 * scale;
                canvas.DrawOval(cx, cy, radiusX, radiusY, headPaint);

                // Kinn: Spitzer zulaufend (schmaler nach unten)
                using var chinPaint = new SKPaint { Color = skinColor, IsAntialias = false };
                canvas.DrawOval(cx, cy + 3 * scale, 7 * scale, 6 * scale, chinPaint);
            }
            else
            {
                // Maennlich: Breiterer, kantigerer Kopf
                float radius = 10 * scale;
                canvas.DrawCircle(cx, cy, radius, headPaint);

                // Kantiger Kiefer (2px breiter auf jeder Seite)
                float jawWidth = 2 * scale;
                float jawTop = 19 * scale;
                float jawHeight = 6 * scale;
                canvas.DrawRect(cx - radius - jawWidth, jawTop, jawWidth + 1 * scale, jawHeight, headPaint);
                canvas.DrawRect(cx + radius - 1 * scale, jawTop, jawWidth + 1 * scale, jawHeight, headPaint);

                // Kinn-Kante (breiter als weiblich)
                canvas.DrawRect(cx - 8 * scale, cy + 8 * scale, 16 * scale, 2 * scale, headPaint);
            }
        }

        // Ohren
        using (var earPaint = new SKPaint { Color = SkinTones[Math.Abs(hash) % SkinTones.Length], IsAntialias = false })
        {
            float earRadius = isFemale ? 2 * scale : 2.5f * scale;
            canvas.DrawCircle(5 * scale, 18 * scale, earRadius, earPaint);
            canvas.DrawCircle(27 * scale, 18 * scale, earRadius, earPaint);

            // Weiblich: Ohrring-Punkte (dezentes Gold)
            if (isFemale)
            {
                using var earringPaint = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00), IsAntialias = false };
                float earringSize = 1 * scale;
                canvas.DrawCircle(4.5f * scale, 20 * scale, earringSize, earringPaint);
                canvas.DrawCircle(27.5f * scale, 20 * scale, earringSize, earringPaint);
            }
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

        using var hairPaint = new SKPaint { Color = hairColor, IsAntialias = false };
        using var hairDarkPaint = new SKPaint { Color = hairDark, IsAntialias = false };

        if (isFemale)
        {
            // ===== Langes wallendes Haar =====

            // Haarvolumen oben am Kopf (unter dem Helm herausschauend)
            float topY = 13 * scale;
            canvas.DrawRect(6 * scale, topY, 20 * scale, 3 * scale, hairPaint);

            // Linke Seite: Langes Haar bis zur Schulter
            float leftX = 3 * scale;
            float strandTop = 14 * scale;
            canvas.DrawRect(leftX, strandTop, 3 * scale, 14 * scale, hairPaint);
            canvas.DrawRect(leftX - 1 * scale, strandTop + 2 * scale, 2 * scale, 10 * scale, hairPaint);
            // Wellung (hellere Strähne)
            canvas.DrawRect(leftX + 1 * scale, strandTop + 4 * scale, 1 * scale, 3 * scale, hairDarkPaint);

            // Rechte Seite: Langes Haar bis zur Schulter
            float rightX = 26 * scale;
            canvas.DrawRect(rightX, strandTop, 3 * scale, 14 * scale, hairPaint);
            canvas.DrawRect(rightX + 1 * scale, strandTop + 2 * scale, 2 * scale, 10 * scale, hairPaint);
            // Wellung (dunklere Strähne)
            canvas.DrawRect(rightX + 1 * scale, strandTop + 5 * scale, 1 * scale, 3 * scale, hairDarkPaint);

            // Pony (kurze Fransen ueber der Stirn)
            bool hasBangs = (hash % 3) != 0; // 66% Chance auf Pony
            if (hasBangs)
            {
                canvas.DrawRect(8 * scale, 14 * scale, 4 * scale, 2 * scale, hairPaint);
                canvas.DrawRect(20 * scale, 14 * scale, 4 * scale, 2 * scale, hairPaint);
                canvas.DrawRect(12 * scale, 14.5f * scale, 8 * scale, 1.5f * scale, hairDarkPaint);
            }
        }
        else
        {
            // ===== Kurzhaar mit markanter Form =====

            // Seitliche Haare (kurz, unter dem Helm)
            float hairTop = 13 * scale;
            canvas.DrawRect(6 * scale, hairTop, 4 * scale, 3 * scale, hairPaint);
            canvas.DrawRect(22 * scale, hairTop, 4 * scale, 3 * scale, hairPaint);

            // Koteletten (seitliche Haar-Ansaetze neben Ohren)
            canvas.DrawRect(6 * scale, 15 * scale, 2 * scale, 4 * scale, hairPaint);
            canvas.DrawRect(24 * scale, 15 * scale, 2 * scale, 4 * scale, hairPaint);

            // Bart-Schatten (50% Chance, nur bei dunkleren Haaren)
            bool hasStubble = (hash % 2) == 0 && hairIndex <= 3;
            if (hasStubble)
            {
                var stubbleColor = new SKColor(hairColor.Red, hairColor.Green, hairColor.Blue, 60);
                using var stubblePaint = new SKPaint { Color = stubbleColor, IsAntialias = false };
                // Kinn-Bereich
                canvas.DrawRect(11 * scale, 24 * scale, 10 * scale, 3 * scale, stubblePaint);
                // Wangen
                canvas.DrawRect(8 * scale, 22 * scale, 3 * scale, 4 * scale, stubblePaint);
                canvas.DrawRect(21 * scale, 22 * scale, 3 * scale, 4 * scale, stubblePaint);
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

        using var hatPaint = new SKPaint { Color = hatColor, IsAntialias = false };
        using var brimPaint = new SKPaint { Color = brimColor, IsAntialias = false };

        float cx = 16 * scale;

        switch (hatStyle)
        {
            case 0:
                // Variante 1: Klassischer Bauhelm (abgerundet)
                canvas.DrawRect(8 * scale, 5 * scale, 16 * scale, 10 * scale, hatPaint);
                canvas.DrawRect(6 * scale, 13 * scale, 20 * scale, 3 * scale, brimPaint);
                break;

            case 1:
                // Variante 2: Muetze/Kappe (flacher, mit Schirm nach vorne)
                canvas.DrawRect(7 * scale, 7 * scale, 18 * scale, 8 * scale, hatPaint);
                // Muetzen-Knopf oben
                using (var knobPaint = new SKPaint { Color = brimColor, IsAntialias = false })
                    canvas.DrawCircle(cx, 7 * scale, 1.5f * scale, knobPaint);
                // Schirm (nach rechts geneigt fuer Charakter)
                canvas.DrawRect(8 * scale, 14 * scale, 14 * scale, 2.5f * scale, brimPaint);
                break;

            case 2:
                // Variante 3: Schutzhelm mit hoher Kuppel
                canvas.DrawOval(cx, 10 * scale, 9 * scale, 7 * scale, hatPaint);
                // Breite Krempe
                canvas.DrawRect(5 * scale, 14 * scale, 22 * scale, 2.5f * scale, brimPaint);
                // Mittelstreifen (Helmnaht)
                using (var stripePaint = new SKPaint { Color = DarkenColor(hatColor, 0.1f), IsAntialias = false })
                    canvas.DrawRect(cx - 0.5f * scale, 4 * scale, 1 * scale, 10 * scale, stripePaint);
                break;
        }

        // Helm-Glanz (nur bei scale >= 2)
        if (scale >= 2)
        {
            var highlightColor = new SKColor(0xFF, 0xFF, 0xFF, 45);
            using var glossPaint = new SKPaint { Color = highlightColor, IsAntialias = true };
            // Glanz-Streifen auf dem Helm (schraeg, oben)
            canvas.DrawRect(10 * scale, 8 * scale, 8 * scale, 2 * scale, glossPaint);
        }

        // S+ Tiers: Stern-Markierung auf dem Helm
        if (tier >= WorkerTier.S)
        {
            using var starPaint = new SKPaint { Color = SKColors.White, IsAntialias = false };
            float sy = 9 * scale;
            float starSize = 2 * scale;
            canvas.DrawRect(cx - starSize / 2, sy - starSize / 2, starSize, starSize, starPaint);

            if (tier >= WorkerTier.SS)
                canvas.DrawRect(cx + 2 * scale, sy - starSize / 2, starSize, starSize, starPaint);

            if (tier == WorkerTier.Legendary)
                canvas.DrawRect(cx - 4 * scale, sy - starSize / 2, starSize, starSize, starPaint);
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

        using (var eyePaint = new SKPaint { Color = eyeColor, IsAntialias = false })
        {
            float dotSize = isFemale ? 2.2f * scale : 2 * scale; // Weiblich: etwas groessere Augen

            switch (mood)
            {
                case MoodBucket.High:
                    if (scale >= 2)
                    {
                        // Augenweiß (ovaler Hintergrund)
                        using var highWhitePaint = new SKPaint { Color = new SKColor(0xF0, 0xF0, 0xF0), IsAntialias = true };
                        float highWhiteRx = dotSize * 1.3f;
                        float highWhiteRy = dotSize * 1.1f;
                        canvas.DrawOval(leftEyeX, eyeY, highWhiteRx, highWhiteRy, highWhitePaint);
                        canvas.DrawOval(rightEyeX, eyeY, highWhiteRx, highWhiteRy, highWhitePaint);

                        // Pupille (kleiner als vorher)
                        eyePaint.Color = eyeColor;
                        float highPupilSize = dotSize * 0.7f;
                        canvas.DrawCircle(leftEyeX, eyeY, highPupilSize, eyePaint);
                        canvas.DrawCircle(rightEyeX, eyeY, highPupilSize, eyePaint);

                        // Weißer Glanzpunkt (oben-rechts der Pupille)
                        using var highHlPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                        float highHlSize = scale * 0.6f;
                        canvas.DrawCircle(leftEyeX + 0.7f * scale, eyeY - 0.6f * scale, highHlSize, highHlPaint);
                        canvas.DrawCircle(rightEyeX + 0.7f * scale, eyeY - 0.6f * scale, highHlSize, highHlPaint);
                    }
                    else
                    {
                        // Pixel-Art bei 32px
                        canvas.DrawCircle(leftEyeX, eyeY, dotSize, eyePaint);
                        canvas.DrawCircle(rightEyeX, eyeY, dotSize, eyePaint);
                    }
                    break;

                case MoodBucket.Mid:
                    if (scale >= 2)
                    {
                        // Augenweiß (ovaler Hintergrund)
                        using var midWhitePaint = new SKPaint { Color = new SKColor(0xF0, 0xF0, 0xF0), IsAntialias = true };
                        float midWhiteRx = dotSize * 1.3f * 1.1f;
                        float midWhiteRy = dotSize * 1.1f * 1.1f;
                        canvas.DrawOval(leftEyeX, eyeY, midWhiteRx, midWhiteRy, midWhitePaint);
                        canvas.DrawOval(rightEyeX, eyeY, midWhiteRx, midWhiteRy, midWhitePaint);

                        // Pupille (kleiner als vorher)
                        eyePaint.Color = eyeColor;
                        float midPupilSize = dotSize * 1.1f * 0.7f;
                        canvas.DrawCircle(leftEyeX, eyeY, midPupilSize, eyePaint);
                        canvas.DrawCircle(rightEyeX, eyeY, midPupilSize, eyePaint);

                        // Weißer Glanzpunkt (oben-rechts der Pupille)
                        using var midHlPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                        float midHlSize = scale * 0.6f;
                        canvas.DrawCircle(leftEyeX + 0.7f * scale, eyeY - 0.6f * scale, midHlSize, midHlPaint);
                        canvas.DrawCircle(rightEyeX + 0.7f * scale, eyeY - 0.6f * scale, midHlSize, midHlPaint);
                    }
                    else
                    {
                        // Pixel-Art bei 32px
                        canvas.DrawCircle(leftEyeX, eyeY, dotSize * 1.1f, eyePaint);
                        canvas.DrawCircle(rightEyeX, eyeY, dotSize * 1.1f, eyePaint);
                    }
                    break;

                case MoodBucket.Low:
                    if (scale >= 2)
                    {
                        // Augenweiß vor den traurigen Dreiecken
                        using var lowWhitePaint = new SKPaint { Color = new SKColor(0xF0, 0xF0, 0xF0), IsAntialias = true };
                        float lowWhiteRx = dotSize * 1.3f;
                        float lowWhiteRy = dotSize * 1.1f;
                        canvas.DrawOval(leftEyeX, eyeY, lowWhiteRx, lowWhiteRy, lowWhitePaint);
                        canvas.DrawOval(rightEyeX, eyeY, lowWhiteRx, lowWhiteRy, lowWhitePaint);
                    }
                    DrawSadEye(canvas, leftEyeX, eyeY, dotSize, eyePaint);
                    DrawSadEye(canvas, rightEyeX, eyeY, dotSize, eyePaint);
                    break;
            }
        }

        if (isFemale)
        {
            // Wimpern (2 kleine Striche nach oben-aussen pro Auge)
            using var lashPaint = new SKPaint
            {
                Color = new SKColor(0x21, 0x21, 0x21),
                IsAntialias = false,
                StrokeWidth = Math.Max(1, 0.8f * scale),
                Style = SKPaintStyle.Stroke
            };
            // Linkes Auge
            canvas.DrawLine(leftEyeX - 1.5f * scale, eyeY - 2 * scale, leftEyeX - 2.5f * scale, eyeY - 3.5f * scale, lashPaint);
            canvas.DrawLine(leftEyeX + 0.5f * scale, eyeY - 2.2f * scale, leftEyeX + 0.5f * scale, eyeY - 3.5f * scale, lashPaint);
            // Rechtes Auge
            canvas.DrawLine(rightEyeX + 1.5f * scale, eyeY - 2 * scale, rightEyeX + 2.5f * scale, eyeY - 3.5f * scale, lashPaint);
            canvas.DrawLine(rightEyeX - 0.5f * scale, eyeY - 2.2f * scale, rightEyeX - 0.5f * scale, eyeY - 3.5f * scale, lashPaint);
        }
        else
        {
            // Maennlich: Kräftige Augenbrauen
            int hairIndex = Math.Abs(hash / 7) % HairColors.Length;
            var browColor = DarkenColor(HairColors[hairIndex], 0.1f);
            using var browPaint = new SKPaint { Color = browColor, IsAntialias = false };
            float browY = eyeY - 3 * scale;
            float browWidth = 4 * scale;
            float browHeight = 1.2f * scale;
            canvas.DrawRect(leftEyeX - browWidth / 2, browY, browWidth, browHeight, browPaint);
            canvas.DrawRect(rightEyeX - browWidth / 2, browY, browWidth, browHeight, browPaint);
        }
    }

    private static void DrawSadEye(SKCanvas canvas, float cx, float cy, float size, SKPaint paint)
    {
        // Dreieck: nach unten zeigend fuer traurigen Ausdruck
        using var path = new SKPath();
        path.MoveTo(cx - size, cy - size);
        path.LineTo(cx + size, cy - size);
        path.LineTo(cx, cy + size);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Zeichnet eine dezente Nase (nur bei scale >= 2, also 64px+).
    /// </summary>
    private static void DrawNose(SKCanvas canvas, int hash, float scale)
    {
        if (scale < 2) return;

        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var noseColor = DarkenColor(SkinTones[skinIndex], 0.12f);

        using var nosePaint = new SKPaint { Color = noseColor, IsAntialias = true };
        float cx = 16 * scale;
        float noseY = 20 * scale;

        canvas.DrawOval(cx, noseY, 1.8f * scale, 1.2f * scale, nosePaint);
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

            using var lipFillPaint = new SKPaint { Color = lipColor, IsAntialias = false, Style = SKPaintStyle.Fill };
            using var lipOutlinePaint = new SKPaint
            {
                Color = lipDark, IsAntialias = false, StrokeWidth = Math.Max(1, 0.8f * scale), Style = SKPaintStyle.Stroke
            };

            float halfWidth = 3.5f * scale;

            switch (mood)
            {
                case MoodBucket.High:
                    // Laecheln: Gefuellter Bogen
                    using (var path = new SKPath())
                    {
                        path.MoveTo(cx - halfWidth, mouthY);
                        path.QuadTo(cx, mouthY + 3 * scale, cx + halfWidth, mouthY);
                        path.Close();
                        canvas.DrawPath(path, lipFillPaint);
                        canvas.DrawPath(path, lipOutlinePaint);
                    }
                    break;

                case MoodBucket.Mid:
                    // Neutral: Dezente Lippen
                    canvas.DrawRect(cx - halfWidth, mouthY - 0.5f * scale, halfWidth * 2, 1.5f * scale, lipFillPaint);
                    break;

                case MoodBucket.Low:
                    // Traurig: Bogen nach unten
                    using (var path = new SKPath())
                    {
                        path.MoveTo(cx - halfWidth, mouthY);
                        path.QuadTo(cx, mouthY - 2 * scale, cx + halfWidth, mouthY);
                        canvas.DrawPath(path, lipOutlinePaint);
                    }
                    break;
            }
        }
        else
        {
            // Maennlich: Einfachere, breitere Mundlinien
            var mouthColor = new SKColor(0x5D, 0x40, 0x37);
            using var mouthPaint = new SKPaint
            {
                Color = mouthColor, IsAntialias = false,
                StrokeWidth = Math.Max(1, 1.2f * scale), Style = SKPaintStyle.Stroke
            };

            float halfWidth = 3.5f * scale;

            switch (mood)
            {
                case MoodBucket.High:
                    canvas.DrawLine(cx - halfWidth, mouthY, cx, mouthY + 2 * scale, mouthPaint);
                    canvas.DrawLine(cx, mouthY + 2 * scale, cx + halfWidth, mouthY, mouthPaint);
                    break;

                case MoodBucket.Mid:
                    canvas.DrawLine(cx - halfWidth, mouthY, cx + halfWidth, mouthY, mouthPaint);
                    break;

                case MoodBucket.Low:
                    canvas.DrawLine(cx - halfWidth, mouthY + 2 * scale, cx, mouthY, mouthPaint);
                    canvas.DrawLine(cx, mouthY, cx + halfWidth, mouthY + 2 * scale, mouthPaint);
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
        using var blushPaint = new SKPaint { Color = new SKColor(0xFF, 0x99, 0x99, alpha), IsAntialias = true };
        float cx = 16 * scale;
        float cheekY = 21 * scale;
        float radius = isFemale ? 2.5f * scale : 2 * scale;

        canvas.DrawCircle(cx - 6 * scale, cheekY, radius, blushPaint);
        canvas.DrawCircle(cx + 6 * scale, cheekY, radius, blushPaint);
    }

    /// <summary>
    /// Zeichnet einen Kinn-Schatten (nur bei scale >= 3, also 96px+).
    /// </summary>
    private static void DrawChinShadow(SKCanvas canvas, int hash, float scale)
    {
        if (scale < 3) return;

        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var shadowColor = DarkenColor(SkinTones[skinIndex], 0.2f).WithAlpha(60);

        using var shadowPaint = new SKPaint { Color = shadowColor, IsAntialias = true };
        float cx = 16 * scale;
        float chinY = 26 * scale;

        canvas.DrawOval(cx, chinY, 7 * scale, 2.5f * scale, shadowPaint);
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
                using (var glassPaint = new SKPaint
                {
                    Color = new SKColor(0x60, 0x60, 0x60), IsAntialias = false,
                    StrokeWidth = Math.Max(1, 0.8f * scale), Style = SKPaintStyle.Stroke
                })
                {
                    canvas.DrawCircle(13 * scale, 17 * scale, 2.8f * scale, glassPaint);
                    canvas.DrawCircle(19 * scale, 17 * scale, 2.8f * scale, glassPaint);
                    // Nasenstueck
                    canvas.DrawLine(15.5f * scale, 17 * scale, 16.5f * scale, 17 * scale, glassPaint);
                    // Buegel
                    canvas.DrawLine(10 * scale, 17 * scale, 7 * scale, 16.5f * scale, glassPaint);
                    canvas.DrawLine(22 * scale, 17 * scale, 25 * scale, 16.5f * scale, glassPaint);
                }
                break;

            case 2:
                // Schutzbrille oben auf dem Kopf (orange Band)
                using (var gogglePaint = new SKPaint
                {
                    Color = new SKColor(0xFF, 0x98, 0x00, 180), IsAntialias = false,
                    StrokeWidth = Math.Max(1, 1.5f * scale), Style = SKPaintStyle.Stroke
                })
                {
                    canvas.DrawLine(6 * scale, 14 * scale, 26 * scale, 14 * scale, gogglePaint);
                }
                break;

            case 3:
                if (isFemale)
                {
                    // Wangenroetung (dezent rosa)
                    using var blushPaint = new SKPaint { Color = new SKColor(0xFF, 0xAB, 0xAB, 80), IsAntialias = false };
                    canvas.DrawCircle(10 * scale, 20 * scale, 2 * scale, blushPaint);
                    canvas.DrawCircle(22 * scale, 20 * scale, 2 * scale, blushPaint);
                }
                else
                {
                    // Narbe / Pflaster auf Wange
                    using var patchPaint = new SKPaint
                    {
                        Color = new SKColor(0xF5, 0xE6, 0xCC), IsAntialias = false
                    };
                    canvas.DrawRect(21 * scale, 20 * scale, 3 * scale, 3 * scale, patchPaint);
                    // Pflaster-Kreuz
                    using var crossPaint = new SKPaint
                    {
                        Color = new SKColor(0xCC, 0xAA, 0x88), IsAntialias = false,
                        StrokeWidth = Math.Max(1, 0.5f * scale), Style = SKPaintStyle.Stroke
                    };
                    canvas.DrawLine(21.5f * scale, 21.5f * scale, 23.5f * scale, 21.5f * scale, crossPaint);
                    canvas.DrawLine(22.5f * scale, 20.5f * scale, 22.5f * scale, 22.5f * scale, crossPaint);
                }
                break;

            case 4:
                // Bleistift hinterm Ohr
                using (var pencilPaint = new SKPaint { Color = new SKColor(0xFF, 0xD5, 0x4F), IsAntialias = false })
                {
                    // Stift-Koerper
                    canvas.DrawRect(26 * scale, 12 * scale, 1.5f * scale, 7 * scale, pencilPaint);
                    // Spitze
                    using var tipPaint = new SKPaint { Color = new SKColor(0x4E, 0x34, 0x2E), IsAntialias = false };
                    canvas.DrawRect(26 * scale, 19 * scale, 1.5f * scale, 1.5f * scale, tipPaint);
                }
                break;

                // case 0, 5: Keine Accessoires
        }
    }

    private static MoodBucket GetMoodBucket(decimal mood)
    {
        if (mood >= 65) return MoodBucket.High;
        if (mood >= 35) return MoodBucket.Mid;
        return MoodBucket.Low;
    }

    /// <summary>
    /// Stable hash from string (deterministic, not GetHashCode which varies per runtime).
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
    /// Removes expired weak references from the cache.
    /// </summary>
    private static void PruneCache()
    {
        var deadKeys = new List<string>();
        foreach (var kvp in _cache)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                deadKeys.Add(kvp.Key);
            }
        }
        foreach (var key in deadKeys)
        {
            _cache.Remove(key);
        }
    }
}
