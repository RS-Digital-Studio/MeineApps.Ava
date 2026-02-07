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

    // Tier -> Helm/Hut-Farbe
    private static readonly Dictionary<WorkerTier, SKColor> TierHatColors = new()
    {
        { WorkerTier.F, new SKColor(0x9E, 0x9E, 0x9E) },  // Grey
        { WorkerTier.E, new SKColor(0x4C, 0xAF, 0x50) },  // Green
        { WorkerTier.D, new SKColor(0x21, 0x96, 0xF3) },  // Blue
        { WorkerTier.C, new SKColor(0x9C, 0x27, 0xB0) },  // Purple
        { WorkerTier.B, new SKColor(0xFF, 0xC1, 0x07) },  // Gold
        { WorkerTier.A, new SKColor(0xF4, 0x43, 0x36) },  // Red
        { WorkerTier.S, new SKColor(0xFF, 0x98, 0x00) }   // Orange
    };

    private enum MoodBucket { High, Mid, Low }

    // Cache: "id|tier|moodBucket" -> weak reference (bitmap kann GC'd werden)
    private static readonly Dictionary<string, WeakReference<SKBitmap>> _cache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Renders a deterministic pixel-art avatar for the given worker parameters.
    /// Returns a new SKBitmap that the caller must dispose.
    /// </summary>
    /// <param name="idSeed">Worker ID used as seed for deterministic generation.</param>
    /// <param name="tier">Worker tier (determines hat color).</param>
    /// <param name="mood">Worker mood (0-100, determines expression).</param>
    /// <param name="size">Output size in pixels (32, 64, or 128).</param>
    public static SKBitmap RenderAvatar(string idSeed, WorkerTier tier, decimal mood, int size)
    {
        // Groesse auf erlaubte Werte begrenzen
        size = size switch
        {
            <= 32 => 32,
            <= 64 => 64,
            _ => 128
        };

        var moodBucket = GetMoodBucket(mood);
        string cacheKey = $"{idSeed}|{tier}|{moodBucket}|{size}";

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

            DrawHead(canvas, hash, scale);
            DrawHat(canvas, tier, scale);
            DrawEyes(canvas, hash, moodBucket, scale);
            DrawMouth(canvas, moodBucket, scale);
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

    private static void DrawHead(SKCanvas canvas, int hash, float scale)
    {
        // Hautton aus Hash ableiten
        int skinIndex = Math.Abs(hash) % SkinTones.Length;
        var skinColor = SkinTones[skinIndex];

        using (var headPaint = new SKPaint { Color = skinColor, IsAntialias = false })
        {
            float cx = 16 * scale;
            float cy = 18 * scale;
            float radius = 10 * scale;
            canvas.DrawCircle(cx, cy, radius, headPaint);
        }

        // Ohren
        using (var earPaint = new SKPaint { Color = SkinTones[Math.Abs(hash) % SkinTones.Length], IsAntialias = false })
        {
            float earRadius = 2.5f * scale;
            canvas.DrawCircle(5 * scale, 18 * scale, earRadius, earPaint);
            canvas.DrawCircle(27 * scale, 18 * scale, earRadius, earPaint);
        }
    }

    private static void DrawHat(SKCanvas canvas, WorkerTier tier, float scale)
    {
        var hatColor = TierHatColors.GetValueOrDefault(tier, new SKColor(0x90, 0x90, 0x90));

        using (var hatPaint = new SKPaint { Color = hatColor, IsAntialias = false })
        {
            // Helm-Koerper (Halbkreis oben auf dem Kopf)
            float left = 8 * scale;
            float top = 5 * scale;
            float width = 16 * scale;
            float height = 10 * scale;
            canvas.DrawRect(left, top, width, height, hatPaint);

            // Helm-Krempe (breiterer Streifen)
            var brimColor = DarkenColor(hatColor, 0.2f);
            using (var brimPaint = new SKPaint { Color = brimColor, IsAntialias = false })
            {
                canvas.DrawRect((left - 2 * scale), (top + height - 2 * scale), (width + 4 * scale), 3 * scale, brimPaint);
            }
        }

        // S-Tier: Stern-Markierung auf dem Helm
        if (tier == WorkerTier.S)
        {
            using (var starPaint = new SKPaint { Color = SKColors.White, IsAntialias = false })
            {
                float sx = 16 * scale;
                float sy = 9 * scale;
                float starSize = 2 * scale;
                canvas.DrawRect(sx - starSize / 2, sy - starSize / 2, starSize, starSize, starPaint);
            }
        }
    }

    private static void DrawEyes(SKCanvas canvas, int hash, MoodBucket mood, float scale)
    {
        float eyeY = 17 * scale;
        float leftEyeX = 13 * scale;
        float rightEyeX = 19 * scale;

        // Augenfarbe aus Hash
        bool hasBrownEyes = (hash % 3) == 0;
        var eyeColor = hasBrownEyes ? new SKColor(0x5D, 0x40, 0x37) : new SKColor(0x21, 0x21, 0x21);

        using (var eyePaint = new SKPaint { Color = eyeColor, IsAntialias = false })
        {
            float dotSize = 2 * scale;

            switch (mood)
            {
                case MoodBucket.High:
                    // Froehlich: einfache Punkte
                    canvas.DrawCircle(leftEyeX, eyeY, dotSize, eyePaint);
                    canvas.DrawCircle(rightEyeX, eyeY, dotSize, eyePaint);
                    break;

                case MoodBucket.Mid:
                    // Neutral: etwas groessere Punkte
                    canvas.DrawCircle(leftEyeX, eyeY, dotSize * 1.2f, eyePaint);
                    canvas.DrawCircle(rightEyeX, eyeY, dotSize * 1.2f, eyePaint);
                    break;

                case MoodBucket.Low:
                    // Traurig: Dreiecke (nach unten zeigend)
                    DrawSadEye(canvas, leftEyeX, eyeY, dotSize, eyePaint);
                    DrawSadEye(canvas, rightEyeX, eyeY, dotSize, eyePaint);
                    break;
            }
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

    private static void DrawMouth(SKCanvas canvas, MoodBucket mood, float scale)
    {
        float mouthY = 22 * scale;
        float cx = 16 * scale;

        using var mouthPaint = new SKPaint
        {
            Color = new SKColor(0x5D, 0x40, 0x37),
            IsAntialias = false,
            StrokeWidth = Math.Max(1, scale),
            Style = SKPaintStyle.Stroke
        };

        float halfWidth = 3 * scale;

        switch (mood)
        {
            case MoodBucket.High:
                // Laecheln (Bogen nach oben)
                canvas.DrawLine(cx - halfWidth, mouthY, cx, mouthY + 2 * scale, mouthPaint);
                canvas.DrawLine(cx, mouthY + 2 * scale, cx + halfWidth, mouthY, mouthPaint);
                break;

            case MoodBucket.Mid:
                // Neutral (gerade Linie)
                canvas.DrawLine(cx - halfWidth, mouthY, cx + halfWidth, mouthY, mouthPaint);
                break;

            case MoodBucket.Low:
                // Traurig (Bogen nach unten)
                canvas.DrawLine(cx - halfWidth, mouthY + 2 * scale, cx, mouthY, mouthPaint);
                canvas.DrawLine(cx, mouthY, cx + halfWidth, mouthY + 2 * scale, mouthPaint);
                break;
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
