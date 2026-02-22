using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Statische Bibliothek mit prozeduralen Textur-Funktionen für welt-spezifische Details.
/// Alle Methoden sind deterministisch (gleiche Eingabe → gleiches Ergebnis).
/// </summary>
public static class ProceduralTextures
{
    // Permutation-LUT für Noise (256 Werte, doppelt für Overflow)
    private static readonly int[] _perm = GeneratePermutation();

    private static int[] GeneratePermutation()
    {
        var p = new int[512];
        // Feste Permutation (keine Zufalls-Abhängigkeit)
        int[] base256 =
        [
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
            140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
            247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,
            57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
            74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,
            60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
            65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,
            200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
            52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,
            207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
            119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
            129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
            218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,
            81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
            184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,
            222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        ];
        for (int i = 0; i < 256; i++)
            p[i] = p[i + 256] = base256[i];
        return p;
    }

    /// <summary>
    /// Einfaches 2D-Value-Noise (0..1)
    /// </summary>
    public static float Noise2D(float x, float y)
    {
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;
        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = _perm[_perm[xi] + yi];
        int ab = _perm[_perm[xi] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi];
        int bb = _perm[_perm[xi + 1] + yi + 1];

        float x1 = Lerp(aa / 255f, ba / 255f, u);
        float x2 = Lerp(ab / 255f, bb / 255f, u);
        return Lerp(x1, x2, v);
    }

    /// <summary>
    /// Fractal Brownian Motion (mehrere Noise-Oktaven überlagert)
    /// </summary>
    public static float Fbm(float x, float y, int octaves = 4)
    {
        float value = 0f;
        float amplitude = 0.5f;
        float frequency = 1f;

        for (int i = 0; i < octaves; i++)
        {
            value += amplitude * Noise2D(x * frequency, y * frequency);
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return value;
    }

    /// <summary>
    /// Deterministische Pseudo-Random pro Gitterzelle (stabile Varianz, kein Random)
    /// </summary>
    public static float CellRandom(int cellX, int cellY, int seed = 0)
    {
        int h = cellX * 374761393 + cellY * 668265263 + seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    /// <summary>
    /// Dünne gekrümmte Grashalme auf einer Zelle
    /// </summary>
    public static void DrawGrassBlades(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, float globalTimer, byte alpha = 60)
    {
        int bladeCount = 2 + (int)(CellRandom(gx, gy, 42) * 3); // 2-4 Halme

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.8f;
        paint.MaskFilter = null;

        for (int i = 0; i < bladeCount; i++)
        {
            float rx = CellRandom(gx, gy, i * 7 + 1);
            float ry = CellRandom(gx, gy, i * 7 + 2);
            float rheight = CellRandom(gx, gy, i * 7 + 3);

            float baseX = px + rx * cs;
            float baseY = py + cs * 0.7f + ry * cs * 0.25f;
            float height = cs * (0.15f + rheight * 0.2f);

            // Wind-Effekt
            float wind = MathF.Sin(globalTimer * 1.2f + gx * 0.5f + i * 0.8f) * 3f;

            // Grün-Variation
            byte g = (byte)(100 + CellRandom(gx, gy, i * 7 + 4) * 80);
            paint.Color = new SKColor(30, g, 20, alpha);

            using var path = new SKPath();
            path.MoveTo(baseX, baseY);
            path.QuadTo(baseX + wind * 0.5f, baseY - height * 0.5f,
                        baseX + wind, baseY - height);
            canvas.DrawPath(path, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Zufällige Haar-Risslinien (Stein, Mauer)
    /// </summary>
    public static void DrawCracks(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, SKColor color, byte alpha = 40)
    {
        int crackCount = 1 + (int)(CellRandom(gx, gy, 100) * 2); // 1-2 Risse

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.6f;
        paint.Color = color.WithAlpha(alpha);
        paint.MaskFilter = null;

        for (int i = 0; i < crackCount; i++)
        {
            float startX = px + CellRandom(gx, gy, 101 + i * 4) * cs;
            float startY = py + CellRandom(gx, gy, 102 + i * 4) * cs;
            float endX = px + CellRandom(gx, gy, 103 + i * 4) * cs;
            float endY = py + CellRandom(gx, gy, 104 + i * 4) * cs;
            float midX = (startX + endX) * 0.5f + (CellRandom(gx, gy, 105 + i) - 0.5f) * cs * 0.3f;
            float midY = (startY + endY) * 0.5f + (CellRandom(gx, gy, 106 + i) - 0.5f) * cs * 0.3f;

            using var path = new SKPath();
            path.MoveTo(startX, startY);
            path.QuadTo(midX, midY, endX, endY);
            canvas.DrawPath(path, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Viele kleine Punkte mit Farbvariation (Sand, Staub)
    /// </summary>
    public static void DrawSandGrain(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, SKColor baseColor, byte alpha = 30)
    {
        int grainCount = 4 + (int)(CellRandom(gx, gy, 200) * 4); // 4-7 Körner

        paint.MaskFilter = null;

        for (int i = 0; i < grainCount; i++)
        {
            float x = px + CellRandom(gx, gy, 201 + i * 2) * cs;
            float y = py + CellRandom(gx, gy, 202 + i * 2) * cs;
            float size = 0.5f + CellRandom(gx, gy, 203 + i) * 1f;

            // Leichte Farbvariation
            int variation = (int)((CellRandom(gx, gy, 204 + i) - 0.5f) * 30);
            byte r = (byte)Math.Clamp(baseColor.Red + variation, 0, 255);
            byte g = (byte)Math.Clamp(baseColor.Green + variation, 0, 255);
            byte b = (byte)Math.Clamp(baseColor.Blue + variation, 0, 255);

            paint.Color = new SKColor(r, g, b, alpha);
            canvas.DrawCircle(x, y, size, paint);
        }
    }

    /// <summary>
    /// Versetzt angeordnete Ziegel-Reihen
    /// </summary>
    public static void DrawBrickPattern(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        SKColor mortarColor, byte alpha = 60)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.8f;
        paint.Color = mortarColor.WithAlpha(alpha);
        paint.MaskFilter = null;

        int rows = 3;
        float rowHeight = cs / (float)rows;

        for (int row = 0; row < rows; row++)
        {
            float y = py + row * rowHeight;
            // Horizontale Fuge
            canvas.DrawLine(px + 2, y, px + cs - 2, y, paint);

            // Vertikale Fugen (versetzt pro Reihe)
            float offset = (row % 2 == 0) ? 0 : cs * 0.5f;
            for (float vx = offset; vx < cs; vx += cs * 0.5f)
            {
                if (vx > 2 && vx < cs - 2)
                    canvas.DrawLine(px + vx, y, px + vx, y + rowHeight, paint);
            }
        }

        paint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Parallele wellige Maserungslinien (Holz)
    /// </summary>
    public static void DrawWoodGrain(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, SKColor grainColor, byte alpha = 35)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.5f;
        paint.Color = grainColor.WithAlpha(alpha);
        paint.MaskFilter = null;

        int lineCount = 3 + (int)(CellRandom(gx, gy, 300) * 2);
        for (int i = 0; i < lineCount; i++)
        {
            float y = py + cs * (0.15f + i * 0.7f / lineCount);
            float wave = CellRandom(gx, gy, 301 + i) * 2f;

            using var path = new SKPath();
            path.MoveTo(px + 2, y);
            for (float x = 0.1f; x <= 1f; x += 0.1f)
            {
                float wx = px + x * cs;
                float wy = y + MathF.Sin(x * MathF.PI * 2 + wave) * 1.5f;
                path.LineTo(wx, wy);
            }
            canvas.DrawPath(path, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Sternförmige Eis-Kristall-Strukturen
    /// </summary>
    public static void DrawIceCrystals(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, byte alpha = 50)
    {
        float cx = px + cs * 0.5f;
        float cy = py + cs * 0.5f;
        float size = cs * 0.2f;

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.6f;
        paint.Color = new SKColor(200, 230, 255, alpha);
        paint.MaskFilter = null;

        // 6-strahlige Schneeflocke
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3f + CellRandom(gx, gy, 400) * 0.3f;
            float ex = cx + MathF.Cos(angle) * size;
            float ey = cy + MathF.Sin(angle) * size;
            canvas.DrawLine(cx, cy, ex, ey, paint);

            // Kleine Verzweigungen
            float bx = cx + MathF.Cos(angle) * size * 0.6f;
            float by = cy + MathF.Sin(angle) * size * 0.6f;
            float bangle1 = angle + 0.5f;
            float bangle2 = angle - 0.5f;
            canvas.DrawLine(bx, by, bx + MathF.Cos(bangle1) * size * 0.3f,
                           by + MathF.Sin(bangle1) * size * 0.3f, paint);
            canvas.DrawLine(bx, by, bx + MathF.Cos(bangle2) * size * 0.3f,
                           by + MathF.Sin(bangle2) * size * 0.3f, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Grüne Moos-Flecken an Kanten
    /// </summary>
    public static void DrawMossPatches(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, byte alpha = 45)
    {
        int patchCount = 1 + (int)(CellRandom(gx, gy, 500) * 2);

        paint.MaskFilter = null;

        for (int i = 0; i < patchCount; i++)
        {
            // Moos an zufälliger Kante
            float edge = CellRandom(gx, gy, 501 + i * 3);
            float pos = CellRandom(gx, gy, 502 + i * 3);
            float size = 2f + CellRandom(gx, gy, 503 + i * 3) * 3f;

            float mx, my;
            if (edge < 0.25f) { mx = px + pos * cs; my = py + 1; }          // Oben
            else if (edge < 0.5f) { mx = px + pos * cs; my = py + cs - 1; } // Unten
            else if (edge < 0.75f) { mx = px + 1; my = py + pos * cs; }     // Links
            else { mx = px + cs - 1; my = py + pos * cs; }                   // Rechts

            byte g = (byte)(80 + CellRandom(gx, gy, 504 + i) * 60);
            paint.Color = new SKColor(20, g, 15, alpha);
            canvas.DrawOval(mx, my, size, size * 0.6f, paint);
        }
    }

    /// <summary>
    /// Nieten-Punkte (Metall)
    /// </summary>
    public static void DrawMetalRivets(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        SKColor rivetColor, byte alpha = 80)
    {
        paint.MaskFilter = null;
        float inset = cs * 0.15f;
        float rivetSize = 1.5f;

        // 4 Ecken
        float[] posX = [px + inset, px + cs - inset, px + inset, px + cs - inset];
        float[] posY = [py + inset, py + inset, py + cs - inset, py + cs - inset];

        for (int i = 0; i < 4; i++)
        {
            // Nieten-Schatten
            paint.Color = new SKColor(0, 0, 0, (byte)(alpha * 0.5f));
            canvas.DrawCircle(posX[i] + 0.5f, posY[i] + 0.5f, rivetSize, paint);

            // Nieten-Highlight
            paint.Color = rivetColor.WithAlpha(alpha);
            canvas.DrawCircle(posX[i], posY[i], rivetSize, paint);

            // Glanzpunkt
            paint.Color = new SKColor(255, 255, 255, (byte)(alpha * 0.4f));
            canvas.DrawCircle(posX[i] - 0.3f, posY[i] - 0.3f, rivetSize * 0.4f, paint);
        }
    }

    /// <summary>
    /// Runde organische Auswüchse (Korallen)
    /// </summary>
    public static void DrawCoralGrowth(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, SKColor coralColor, byte alpha = 50)
    {
        int growthCount = 1 + (int)(CellRandom(gx, gy, 600) * 2);

        paint.MaskFilter = null;

        for (int i = 0; i < growthCount; i++)
        {
            float edge = CellRandom(gx, gy, 601 + i * 3);
            float pos = CellRandom(gx, gy, 602 + i * 3);
            float size = 2f + CellRandom(gx, gy, 603 + i * 3) * 3f;

            float cx, cy;
            if (edge < 0.25f) { cx = px + pos * cs; cy = py + 2; }
            else if (edge < 0.5f) { cx = px + pos * cs; cy = py + cs - 2; }
            else if (edge < 0.75f) { cx = px + 2; cy = py + pos * cs; }
            else { cx = px + cs - 2; cy = py + pos * cs; }

            // Variierte Korallenfarbe
            int rv = (int)((CellRandom(gx, gy, 604 + i) - 0.5f) * 40);
            byte r = (byte)Math.Clamp(coralColor.Red + rv, 0, 255);
            byte g = (byte)Math.Clamp(coralColor.Green + rv / 2, 0, 255);
            byte b = (byte)Math.Clamp(coralColor.Blue + rv / 3, 0, 255);

            paint.Color = new SKColor(r, g, b, alpha);
            canvas.DrawCircle(cx, cy, size, paint);

            // Kleinere Auswüchse drumherum
            paint.Color = new SKColor(r, g, b, (byte)(alpha * 0.6f));
            canvas.DrawCircle(cx + size * 0.8f, cy - size * 0.3f, size * 0.5f, paint);
            canvas.DrawCircle(cx - size * 0.5f, cy + size * 0.6f, size * 0.4f, paint);
        }
    }

    /// <summary>
    /// Glut-Risse (orange leuchtende Linien in dunklem Stein)
    /// </summary>
    public static void DrawEmberCracks(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, float globalTimer, byte alpha = 70)
    {
        int crackCount = 1 + (int)(CellRandom(gx, gy, 700) * 2);
        float pulse = MathF.Sin(globalTimer * 2f + gx * 0.7f + gy * 0.5f) * 0.3f + 0.7f;

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.2f;
        paint.MaskFilter = null;

        for (int i = 0; i < crackCount; i++)
        {
            byte a = (byte)(alpha * pulse);
            paint.Color = new SKColor(255, 120, 20, a);

            float startX = px + CellRandom(gx, gy, 701 + i * 4) * cs;
            float startY = py + CellRandom(gx, gy, 702 + i * 4) * cs;
            float endX = px + CellRandom(gx, gy, 703 + i * 4) * cs;
            float endY = py + CellRandom(gx, gy, 704 + i * 4) * cs;

            canvas.DrawLine(startX, startY, endX, endY, paint);

            // Glow um die Risse
            paint.Color = new SKColor(255, 80, 0, (byte)(a * 0.3f));
            paint.StrokeWidth = 3f;
            canvas.DrawLine(startX, startY, endX, endY, paint);
            paint.StrokeWidth = 1.2f;
        }

        paint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Marmor-Adern (dünne gewellte Linien)
    /// </summary>
    public static void DrawMarbleVeins(SKCanvas canvas, SKPaint paint, float px, float py, int cs,
        int gx, int gy, SKColor veinColor, byte alpha = 25)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.5f;
        paint.Color = veinColor.WithAlpha(alpha);
        paint.MaskFilter = null;

        int veinCount = 1 + (int)(CellRandom(gx, gy, 800) * 2);
        for (int i = 0; i < veinCount; i++)
        {
            float startY = py + CellRandom(gx, gy, 801 + i * 2) * cs;
            float wave = CellRandom(gx, gy, 802 + i * 2) * 3f;

            using var path = new SKPath();
            path.MoveTo(px, startY);
            for (float t = 0.1f; t <= 1f; t += 0.05f)
            {
                float x = px + t * cs;
                float y = startY + MathF.Sin(t * MathF.PI * 2 * wave) * cs * 0.08f;
                path.LineTo(x, y);
            }
            canvas.DrawPath(path, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    // --- Hilfs-Funktionen ---

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);
}
