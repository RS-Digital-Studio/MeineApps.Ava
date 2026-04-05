using HandwerkerImperium.Services;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer fuer das Saege-Minigame.
/// AAA-Qualitaet: Realistisches Holz mit Bezier-Maserung, 3D-Astloecher mit
/// Jahresringen, Rinden-Textur, Schneide-Animation mit Holzspaltung,
/// Saegemehl-Explosion und Stirnholz-Sicht.
/// </summary>
public sealed class SawingGameRenderer : IDisposable
{
    private bool _disposed;

    // AI-Hintergrund (optionaler Layer unter den Spielelementen)
    private IGameAssetService? _assetService;
    private SKBitmap? _background;

    // Holz-Farben (reicher/waermer)
    private static readonly SKColor WoodDark = new(0x5C, 0x3A, 0x21);
    private static readonly SKColor WoodMedium = new(0x8B, 0x5E, 0x3C);
    private static readonly SKColor WoodLight = new(0xA8, 0x7E, 0x56);
    private static readonly SKColor WoodGrain = new(0xC4, 0x9A, 0x6C);
    private static readonly SKColor WoodHighlight = new(0xD4, 0xAA, 0x7C);
    private static readonly SKColor BarkDark = new(0x3E, 0x27, 0x14);
    private static readonly SKColor BarkMedium = new(0x4A, 0x30, 0x1A);
    private static readonly SKColor EndGrainLight = new(0xC0, 0x90, 0x60);
    private static readonly SKColor EndGrainDark = new(0x6E, 0x48, 0x28);
    private static readonly SKColor EndGrainRing = new(0x8A, 0x60, 0x3A);

    // Saege-Farben (metallischer)
    private static readonly SKColor SawBlade = new(0xC8, 0xC8, 0xC8);
    private static readonly SKColor SawBladeShine = new(0xE8, 0xE8, 0xF0);
    private static readonly SKColor SawHandle = new(0x8B, 0x45, 0x13);
    private static readonly SKColor SawHandleLight = new(0xA5, 0x5B, 0x2A);
    private static readonly SKColor SawHandleDark = new(0x6D, 0x33, 0x0E);
    private static readonly SKColor SawTooth = new(0xA0, 0xA0, 0xA0);

    // Zonen-Farben
    private static readonly SKColor PerfectZone = new(0x4C, 0xAF, 0x50);
    private static readonly SKColor GoodZone = new(0xE8, 0xA0, 0x0E);
    private static readonly SKColor OkZone = new(0xFF, 0xC1, 0x07);
    private static readonly SKColor MissZone = new(0xEF, 0x44, 0x44);
    private static readonly SKColor MarkerColor = SKColors.White;

    // Wiederverwendbare Paint-Instanzen (vermeiden ~43 Allokationen pro Frame)
    // Gefuellte Flaechen ohne Antialiasing (Rects, Hintergruende, Partikel)
    private readonly SKPaint _fillPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    // Gefuellte Flaechen mit Antialiasing (Kreise, abgerundete Formen)
    private readonly SKPaint _fillAAPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    // Linien/Rahmen ohne Antialiasing
    private readonly SKPaint _strokePaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke };
    // Linien/Rahmen mit Antialiasing (Schnittlinien, Jahresringe)
    private readonly SKPaint _strokeAAPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    // Shader-basierte Fuellungen (Gradienten fuer Holz, Saegeblatt, Griff)
    private readonly SKPaint _shaderPaint = new() { IsAntialias = false };
    // Weichgezeichnete Formen (Astloch-Schatten mit MaskFilter)
    private readonly SKPaint _blurPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    // Gecachter MaskFilter fuer Astloch-Schatten (vermeidet Native Memory Leak pro DrawKnot-Aufruf)
    private readonly SKMaskFilter _knotBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2);
    // Holzmaserung: Hauptlinien (dick, 1.5px)
    private readonly SKPaint _grainPaint1 = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    // Holzmaserung: Sekundaerlinien (mittel, 1px)
    private readonly SKPaint _grainPaint2 = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    // Holzmaserung: Tertiaerlinien (duenn, 0.8px)
    private readonly SKPaint _grainPaint3 = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f };
    // Holzmaserung: Feine Zwischenlinien (0.5px)
    private readonly SKPaint _fineGrainPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };

    // Saegemehl-Partikel (Fixed-Size struct-Pool, 0 GC)
    private const int MaxSawdust = 50;
    private readonly SawdustParticle[] _sawdust = new SawdustParticle[MaxSawdust];
    private int _sawdustCount;
    private float _sawAnimTime;

    // Holzsplitter-Partikel (beim Schneiden, Fixed-Size struct-Pool, 0 GC)
    private const int MaxWoodChips = 20;
    private readonly WoodChipParticle[] _woodChips = new WoodChipParticle[MaxWoodChips];
    private int _woodChipCount;

    // Schneide-Animation
    private bool _prevIsResultShown;
    private bool _cutStarted;
    private float _cutAnimTime;
    private bool _cutBurstDone;

    // Gecachter SKPath fuer wiederholte Nutzung (vermeidet GC-Allokationen pro Frame)
    private readonly SKPath _cachedPath = new();

    // ═══════════════════════════════════════════════════════════════════
    // GECACHTE SHADER (nur bei Bounds-Aenderung neu erstellt)
    // Spart 1 Shader-Allokation/Frame (Holz-Gradient ist statisch,
    // Saege-Shader sind positionsabhaengig durch Animation → bleiben dynamisch)
    // ═══════════════════════════════════════════════════════════════════

    private SKRect _lastBounds;
    private SKShader? _woodGradientShader;

    // Gecachte Gradient-Positionen (vermeidet Array-Allokation pro Frame)
    private static readonly float[] s_woodGradientPositions = [0, 0.5f, 1.0f];

    private struct SawdustParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
    }

    private struct WoodChipParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Width, Height, Rotation, RotSpeed;
        public SKColor Color;
    }

    // ═══════════════════════════════════════════════════════════════════
    // VORBERECHNETE RNG-ARRAYS (ersetzen new Random(seed) pro Frame)
    // seed=42: 24 Werte fuer Saegeriss-Splitter (6 Iterationen x 4 Aufrufe)
    // seed=123/456: 512 Werte fuer Rinden-Textur (bis ~128 Kacheln breit)
    // ═══════════════════════════════════════════════════════════════════

    private static readonly int[] s_woodRng = PrecomputeRng(42, 24);
    private static readonly int[] s_barkRngTop = PrecomputeRng(123, 512);
    private static readonly int[] s_barkRngBottom = PrecomputeRng(456, 512);

    private static int[] PrecomputeRng(int seed, int count)
    {
        var rng = new Random(seed);
        var arr = new int[count];
        for (int i = 0; i < count; i++) arr[i] = rng.Next();
        return arr;
    }

    // Deterministische Astloch-Positionen (relativ zum Brett)
    private static readonly (float relX, float relY, float radius)[] KnotPositions =
    [
        (0.18f, 0.30f, 8f),
        (0.72f, 0.55f, 6f),
        (0.42f, 0.72f, 5f),
        (0.85f, 0.25f, 4f),
        (0.28f, 0.85f, 3.5f),
    ];

    /// <summary>
    /// Initialisiert den AI-Asset-Service für den Hintergrund.
    /// </summary>
    public void Initialize(IGameAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Rendert das gesamte Saege-Spielfeld.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
        double markerPosition,
        double perfectStart, double perfectWidth,
        double goodStart, double goodWidth,
        double okStart, double okWidth,
        bool isPlaying, bool isResultShown,
        float deltaTime)
    {
        // AI-Hintergrund als Atmosphäre-Layer
        if (_assetService != null)
        {
            _background ??= _assetService.GetBitmap("minigames/sawing_bg.webp");
            if (_background == null)
                _ = _assetService.LoadBitmapAsync("minigames/sawing_bg.webp");
            if (_background != null)
                canvas.DrawBitmap(_background, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        }

        _sawAnimTime += deltaTime;

        // Statischen Holz-Shader bei Bounds-Aenderung neu erstellen
        if (_lastBounds != bounds)
        {
            _lastBounds = bounds;
            float p = 16;
            float bat = bounds.Top + p;
            float bah = (bounds.Bottom - p) - bat;
            float wt = bat;
            float wh = bah * 0.55f;
            float wx = bounds.Left + p;

            _woodGradientShader?.Dispose();
            _woodGradientShader = SKShader.CreateLinearGradient(
                new SKPoint(wx, wt), new SKPoint(wx, wt + wh),
                new SKColor[] { WoodDark.WithAlpha(60), SKColors.Transparent, WoodDark.WithAlpha(40) },
                s_woodGradientPositions,
                SKShaderTileMode.Clamp);
        }

        // Schneide-Animation zuruecksetzen wenn neues Spiel beginnt
        if (!isResultShown && _prevIsResultShown && _cutStarted)
        {
            _cutStarted = false;
            _cutAnimTime = 0;
            _cutBurstDone = false;
            _sawdustCount = 0;
            _woodChipCount = 0;
        }

        // Schneide-Animation starten wenn Ergebnis erstmals angezeigt wird
        if (isResultShown && !_prevIsResultShown)
        {
            _cutStarted = true;
            _cutAnimTime = 0;
            _cutBurstDone = false;
        }
        _prevIsResultShown = isResultShown;

        if (_cutStarted)
            _cutAnimTime += deltaTime;

        float padding = 16;
        float barAreaTop = bounds.Top + padding;
        float barAreaBottom = bounds.Bottom - padding;
        float barAreaHeight = barAreaBottom - barAreaTop;

        // Obere Haelfte: Holzbrett mit Saege
        float woodTop = barAreaTop;
        float woodHeight = barAreaHeight * 0.55f;
        float woodBottom = woodTop + woodHeight;
        DrawWoodBoard(canvas, bounds.Left + padding, woodTop, bounds.Width - 2 * padding, woodHeight,
            markerPosition, isPlaying);

        // Untere Haelfte: Timing-Bar
        float barTop = woodBottom + 20;
        float barHeight = Math.Min(50, barAreaHeight * 0.2f);
        float barLeft = bounds.Left + padding + 8;
        float barWidth = bounds.Width - 2 * padding - 16;
        DrawTimingBar(canvas, barLeft, barTop, barWidth, barHeight,
            markerPosition, perfectStart, perfectWidth, goodStart, goodWidth, okStart, okWidth, isPlaying);

        // Partikel zeichnen (immer, auch nach Spielende fuer Schneide-Animation)
        UpdateAndDrawSawdust(canvas, bounds, padding, woodTop, woodHeight, deltaTime, isPlaying);
        UpdateAndDrawWoodChips(canvas, deltaTime);
    }

    /// <summary>
    /// Zeichnet das Holzbrett mit Bezier-Maserung, Jahresring-Astloechern,
    /// Rinden-Textur und optionaler Schneide/Spalt-Animation.
    /// </summary>
    private void DrawWoodBoard(SKCanvas canvas, float x, float y, float width, float height,
        double markerPos, bool isPlaying)
    {
        // Schneide-Fortschritt berechnen
        float cutProgress = 0; // 0-1: Saege faehrt durch Holz
        float splitProgress = 0; // 0-1: Holz spaltet sich
        if (_cutStarted)
        {
            cutProgress = Math.Clamp(_cutAnimTime / 1.2f, 0, 1); // 1.2s Schnitt
            if (cutProgress >= 1.0f)
            {
                float splitTime = _cutAnimTime - 1.2f;
                splitProgress = Math.Clamp(splitTime / 0.6f, 0, 1); // 0.6s Spaltung
                // EaseOutBack fuer federnden Effekt
                splitProgress = EaseOutBack(splitProgress);
            }
        }

        float cutX = x + width * 0.5f;
        float splitGap = splitProgress * width * 0.06f; // Max 6% Gap

        // Saegemehl-Burst beim Schneiden (einmalig)
        if (_cutStarted && cutProgress > 0.1f && !_cutBurstDone)
        {
            SpawnCutBurst(cutX, y + cutProgress * height, height);
            _cutBurstDone = true;
        }

        // Kontinuierliches Saegemehl waehrend des Schnitts
        if (_cutStarted && cutProgress > 0 && cutProgress < 1)
        {
            SpawnCuttingSawdust(cutX, y + cutProgress * height);
        }

        // === Linke Holzhaelfte ===
        float leftW = width * 0.5f - splitGap;
        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + leftW, y + height));
        DrawWoodSurface(canvas, x, y, width, height);
        canvas.Restore();

        // === Rechte Holzhaelfte ===
        float rightX = cutX + splitGap;
        float rightW = width * 0.5f - splitGap;
        canvas.Save();
        canvas.ClipRect(new SKRect(rightX, y, rightX + rightW, y + height));
        DrawWoodSurface(canvas, x, y, width, height);
        canvas.Restore();

        // === Stirnholz im Spalt (Jahresringe sichtbar) ===
        if (splitGap > 1.5f)
        {
            DrawEndGrain(canvas, cutX - splitGap, y, splitGap * 2, height);
        }

        // === Schnittlinie ===
        if (_cutStarted && cutProgress < 1.0f)
        {
            float cutDepth = cutProgress * height;
            _strokeAAPaint.Color = new SKColor(0x00, 0x00, 0x00, 120);
            _strokeAAPaint.StrokeWidth = 2;
            canvas.DrawLine(cutX, y, cutX, y + cutDepth, _strokeAAPaint);

            // Saegeriss-Splitter an der Schnittkante (vorberechnetes RNG, kein new Random pro Frame)
            _cachedPath.Rewind();
            int ri = 0;
            for (int i = 0; i < 6; i++)
            {
                float ry = y + cutDepth - 4 + s_woodRng[ri++ % s_woodRng.Length] % 8;
                float rx = cutX + (s_woodRng[ri++ % s_woodRng.Length] % 2 == 0 ? -1 : 1) * (1 + s_woodRng[ri++ % s_woodRng.Length] % 3);
                _cachedPath.MoveTo(cutX, ry);
                // rng.Next(-2, 3) → Wertebereich -2..2: (Next()%5) - 2
                _cachedPath.LineTo(rx, ry + s_woodRng[ri++ % s_woodRng.Length] % 5 - 2);
            }
            _strokePaint.Color = WoodDark.WithAlpha(80);
            _strokePaint.StrokeWidth = 1;
            canvas.DrawPath(_cachedPath, _strokePaint);
        }
        else if (!_cutStarted && isPlaying)
        {
            // Markierungslinie nur waehrend des Spielens anzeigen
            _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 50);
            _strokePaint.StrokeWidth = 1;
            _strokePaint.PathEffect?.Dispose();
            _strokePaint.PathEffect = SKPathEffect.CreateDash([6, 4], 0);
            canvas.DrawLine(cutX, y + 2, cutX, y + height - 2, _strokePaint);
            _strokePaint.PathEffect?.Dispose(); // Native-Speicher freigeben
            _strokePaint.PathEffect = null;
        }

        // === Saege ===
        if (isPlaying)
        {
            float sawY = y + height + 4;
            float sawBounce = MathF.Sin(_sawAnimTime * 8) * 3;
            DrawSaw(canvas, cutX, sawY + sawBounce, width * 0.35f);
        }
        else if (_cutStarted && cutProgress < 1.0f)
        {
            // Saege faehrt durch das Holz
            float sawY = y + cutProgress * height;
            float sawBounce = MathF.Sin(_sawAnimTime * 14) * 1.5f; // Schnellere Vibration
            DrawSaw(canvas, cutX, sawY + sawBounce, width * 0.35f);
        }
    }

    /// <summary>
    /// Zeichnet die komplette Holzoberflaeche mit Bezier-Maserung,
    /// Astloechern mit Jahresringen und Rinden-Textur.
    /// </summary>
    private void DrawWoodSurface(SKCanvas canvas, float x, float y, float width, float height)
    {
        // Hintergrund-Farbe (Brett-Koerper)
        _fillPaint.Color = WoodMedium;
        canvas.DrawRect(x, y, width, height, _fillPaint);

        // Subtiler Farbverlauf (Mitte heller, Raender dunkler, gecacht)
        _shaderPaint.Shader = _woodGradientShader;
        canvas.DrawRect(x, y, width, height, _shaderPaint);
        _shaderPaint.Shader = null;

        // === Bezier-Maserung (geschwungene Linien) ===
        DrawWoodGrainCurved(canvas, x, y, width, height);

        // === Astloecher mit Jahresringen ===
        foreach (var (relX, relY, radius) in KnotPositions)
        {
            float kx = x + relX * width;
            float ky = y + relY * height;
            float scaledRadius = radius * Math.Min(width, height) / 200f;
            scaledRadius = Math.Max(scaledRadius, 3);
            DrawKnot(canvas, kx, ky, scaledRadius);
        }

        // === Rinden-Textur oben/unten ===
        DrawBark(canvas, x, y, width, 5, true);
        DrawBark(canvas, x, y + height - 5, width, 5, false);

        // === Seitenkanten (dunkler) ===
        _fillPaint.Color = WoodDark;
        canvas.DrawRect(x, y, 3, height, _fillPaint);
        canvas.DrawRect(x + width - 3, y, 3, height, _fillPaint);

        // === Glanzkante oben (3D-Effekt) ===
        _fillPaint.Color = WoodHighlight.WithAlpha(60);
        canvas.DrawRect(x + 3, y + 5, width - 6, 2, _fillPaint);
    }

    /// <summary>
    /// Zeichnet geschwungene Holzmaserung mit Bezier-Kurven die um Astloecher fliessen.
    /// </summary>
    private void DrawWoodGrainCurved(SKCanvas canvas, float x, float y, float width, float height)
    {
        _grainPaint1.Color = WoodGrain.WithAlpha(90);
        _grainPaint2.Color = WoodLight.WithAlpha(50);
        _grainPaint3.Color = WoodDark.WithAlpha(40);

        // Hauptmaserung (dickere Linien)
        for (float gy = y + 8; gy < y + height - 5; gy += 9)
        {
            _cachedPath.Rewind();
            float waveOffset = MathF.Sin(gy * 0.15f) * 6;
            _cachedPath.MoveTo(x + 2, gy + waveOffset);

            float segW = width / 5f;
            for (int seg = 0; seg < 5; seg++)
            {
                float x1 = x + seg * segW;
                float x2 = x + (seg + 1) * segW;
                float midX = (x1 + x2) * 0.5f;
                float ctrlY = gy + MathF.Sin((x1 + gy) * 0.06f) * 5 + waveOffset;
                float endY = gy + MathF.Sin((x2 + gy * 0.8f) * 0.05f) * 3 + waveOffset;
                _cachedPath.QuadTo(midX, ctrlY, x2, endY);
            }

            var paint = ((int)(gy * 3) % 3) switch
            {
                0 => _grainPaint1,
                1 => _grainPaint2,
                _ => _grainPaint3
            };
            canvas.DrawPath(_cachedPath, paint);
        }

        // Feine Zwischen-Maserung (duennere Linien, versetzt)
        _fineGrainPaint.Color = WoodGrain.WithAlpha(35);

        for (float gy = y + 12; gy < y + height - 5; gy += 9)
        {
            _cachedPath.Rewind();
            float waveOffset = MathF.Sin(gy * 0.12f + 1.5f) * 4;
            _cachedPath.MoveTo(x + 4, gy + waveOffset);

            float segW = width / 4f;
            for (int seg = 0; seg < 4; seg++)
            {
                float x1 = x + seg * segW;
                float x2 = x + (seg + 1) * segW;
                float midX = (x1 + x2) * 0.5f;
                float ctrlY = gy + MathF.Sin((x1 + gy) * 0.08f + 2) * 3 + waveOffset;
                _cachedPath.QuadTo(midX, ctrlY, x2, gy + waveOffset);
            }

            canvas.DrawPath(_cachedPath, _fineGrainPaint);
        }
    }

    /// <summary>
    /// Zeichnet ein Astloch mit konzentrischen Jahresringen und 3D-Tiefe.
    /// </summary>
    private void DrawKnot(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Aeusserer Schatten (Vertiefung)
        _blurPaint.Color = WoodDark.WithAlpha(100);
        _blurPaint.MaskFilter = _knotBlurFilter;
        canvas.DrawCircle(cx + 1, cy + 1, radius + 1, _blurPaint);
        _blurPaint.MaskFilter = null;

        // Hintergrund (dunkleres Holz)
        _fillAAPaint.Color = WoodDark;
        canvas.DrawCircle(cx, cy, radius, _fillAAPaint);

        // Jahresringe (konzentrische Kreise mit abwechselnden Farben)
        int ringCount = Math.Max(2, (int)(radius / 2));
        for (int i = ringCount; i >= 0; i--)
        {
            float r = radius * (i / (float)ringCount);
            byte alpha = (byte)(120 + (ringCount - i) * 15);
            alpha = Math.Min(alpha, (byte)220);

            var color = (i % 2 == 0) ? WoodMedium.WithAlpha(alpha) : WoodDark.WithAlpha(alpha);
            _strokeAAPaint.Color = color;
            _strokeAAPaint.StrokeWidth = Math.Max(1, radius / ringCount * 0.8f);
            canvas.DrawCircle(cx, cy, r, _strokeAAPaint);
        }

        // Zentrum (dunkler Kern)
        _fillAAPaint.Color = new SKColor(0x3A, 0x22, 0x10, 200);
        canvas.DrawCircle(cx, cy, radius * 0.2f, _fillAAPaint);

        // Glanz-Punkt (Lichtreflexion oben links)
        _fillAAPaint.Color = WoodHighlight.WithAlpha(60);
        canvas.DrawCircle(cx - radius * 0.25f, cy - radius * 0.25f, radius * 0.15f, _fillAAPaint);
    }

    /// <summary>
    /// Zeichnet Rinden-Textur an den Brettkanten.
    /// </summary>
    private void DrawBark(SKCanvas canvas, float x, float y, float width, float barkHeight, bool isTop)
    {
        // Rinden-Hintergrund
        _fillPaint.Color = BarkDark;
        canvas.DrawRect(x, y, width, barkHeight, _fillPaint);

        // Raue Rinden-Textur (unregelmaeige Pixel-Bloecke, vorberechnetes RNG)
        _fillPaint.Color = BarkMedium;
        var barkRng = isTop ? s_barkRngTop : s_barkRngBottom;
        int bi = 0;
        int barkH = Math.Max(1, (int)barkHeight - 1);
        for (float bx = x + 1; bx < x + width - 2; bx += 4 + barkRng[bi++ % barkRng.Length] % 3)
        {
            float by = y + barkRng[bi++ % barkRng.Length] % barkH;
            float bw = 2 + barkRng[bi++ % barkRng.Length] % 3;
            float bh = 1 + barkRng[bi++ % barkRng.Length] % 2;
            canvas.DrawRect(bx, by, bw, bh, _fillPaint);
        }

        // Heller Streifen (Rinden-Kante zum Holz)
        _fillPaint.Color = WoodDark.WithAlpha(180);
        if (isTop)
        {
            canvas.DrawRect(x, y + barkHeight - 1, width, 1, _fillPaint);
        }
        else
        {
            canvas.DrawRect(x, y, width, 1, _fillPaint);
        }
    }

    /// <summary>
    /// Zeichnet Stirnholz (Querschnitt) im Spalt mit sichtbaren Jahresringen.
    /// </summary>
    private void DrawEndGrain(SKCanvas canvas, float x, float y, float gapWidth, float height)
    {
        // Hintergrund (helles Stirnholz)
        _fillPaint.Color = EndGrainLight;
        canvas.DrawRect(x, y, gapWidth, height, _fillPaint);

        // Jahresringe (horizontale gebogene Linien)
        _strokeAAPaint.Color = EndGrainRing.WithAlpha(120);
        _strokeAAPaint.StrokeWidth = 1;

        float centerY = y + height * 0.45f; // Leicht nach oben versetzt
        float centerX = x + gapWidth * 0.5f;
        for (float r = 5; r < height * 0.6f; r += 4)
        {
            _cachedPath.Rewind();
            _cachedPath.AddArc(new SKRect(centerX - gapWidth * 0.4f, centerY - r, centerX + gapWidth * 0.4f, centerY + r),
                0, 360);
            canvas.DrawPath(_cachedPath, _strokeAAPaint);
        }

        // Dunklere Raender
        _fillPaint.Color = EndGrainDark.WithAlpha(100);
        canvas.DrawRect(x, y, 1, height, _fillPaint);
        canvas.DrawRect(x + gapWidth - 1, y, 1, height, _fillPaint);
    }

    /// <summary>
    /// Zeichnet die Saege mit detailliertem Blatt, Zaehnen und Griff.
    /// </summary>
    private void DrawSaw(SKCanvas canvas, float x, float y, float sawHalfWidth)
    {
        float bladeH = 5;
        float toothH = 4;

        // Saegeblatt (metallischer Gradient, Position dynamisch durch Animation)
        var bladeShader = SKShader.CreateLinearGradient(
            new SKPoint(x - sawHalfWidth, y),
            new SKPoint(x - sawHalfWidth, y + bladeH),
            [SawBladeShine, SawBlade, new SKColor(0x90, 0x90, 0x90)],
            [0, 0.4f, 1.0f],
            SKShaderTileMode.Clamp);
        _shaderPaint.Shader = bladeShader;
        canvas.DrawRect(x - sawHalfWidth, y, sawHalfWidth * 2, bladeH, _shaderPaint);
        _shaderPaint.Shader = null;
        bladeShader.Dispose();

        // Saegezaehne (alternierend gross/klein)
        _fillPaint.Color = SawTooth;
        bool bigTooth = true;
        for (float tx = x - sawHalfWidth + 2; tx < x + sawHalfWidth - 2; tx += 5)
        {
            float th = bigTooth ? toothH : toothH * 0.6f;
            canvas.DrawRect(tx, y + bladeH, 2, th, _fillPaint);
            // Zahnspitze (dunklerer Punkt)
            _fillPaint.Color = new SKColor(0x70, 0x70, 0x70);
            canvas.DrawRect(tx, y + bladeH + th, 2, 1, _fillPaint);
            _fillPaint.Color = SawTooth;
            bigTooth = !bigTooth;
        }

        // Griff (ergonomisch mit Holz-Optik)
        float handleW = 20;
        float handleH = 16;
        float handleX = x - handleW / 2;
        float handleY = y - handleH;

        var handleShader = SKShader.CreateLinearGradient(
            new SKPoint(handleX, handleY),
            new SKPoint(handleX, handleY + handleH),
            [SawHandleLight, SawHandle, SawHandleDark],
            [0, 0.5f, 1.0f],
            SKShaderTileMode.Clamp);
        _shaderPaint.Shader = handleShader;
        canvas.DrawRect(handleX, handleY, handleW, handleH, _shaderPaint);
        _shaderPaint.Shader = null;
        handleShader.Dispose();

        // Griff-Akzent (Metallring oben/unten)
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90);
        canvas.DrawRect(handleX, handleY, handleW, 2, _fillPaint);
        canvas.DrawRect(handleX, handleY + handleH - 2, handleW, 2, _fillPaint);

        // Griff-Niete (zwei kleine Punkte)
        _fillPaint.Color = new SKColor(0xC0, 0xC0, 0xC0);
        canvas.DrawRect(x - 3, handleY + handleH * 0.35f, 2, 2, _fillPaint);
        canvas.DrawRect(x + 1, handleY + handleH * 0.35f, 2, 2, _fillPaint);
    }

    /// <summary>
    /// Zeichnet die Timing-Bar mit Zonen und Marker.
    /// </summary>
    private void DrawTimingBar(SKCanvas canvas, float x, float y, float width, float height,
        double markerPos, double pStart, double pWidth, double gStart, double gWidth,
        double oStart, double oWidth, bool isPlaying)
    {
        // Bar-Hintergrund (dunkle Holz-Optik)
        _fillPaint.Color = new SKColor(0x33, 0x2B, 0x20);
        canvas.DrawRect(x, y, width, height, _fillPaint);

        // Rahmen (Holzoptik)
        _strokePaint.Color = new SKColor(0x5D, 0x40, 0x37);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawRect(x, y, width, height, _strokePaint);

        // Miss-Zone (gesamte Bar)
        _fillPaint.Color = MissZone.WithAlpha(60);
        canvas.DrawRect(x + 2, y + 2, width - 4, height - 4, _fillPaint);

        // OK-Zone
        float okLeft = x + (float)(oStart * width);
        float okW = (float)(oWidth * width);
        _fillPaint.Color = OkZone.WithAlpha(100);
        canvas.DrawRect(okLeft, y + 2, okW, height - 4, _fillPaint);

        // Good-Zone
        float goodLeft = x + (float)(gStart * width);
        float goodW = (float)(gWidth * width);
        _fillPaint.Color = GoodZone.WithAlpha(140);
        canvas.DrawRect(goodLeft, y + 2, goodW, height - 4, _fillPaint);

        // Perfect-Zone
        float perfLeft = x + (float)(pStart * width);
        float perfW = (float)(pWidth * width);
        _fillPaint.Color = PerfectZone.WithAlpha(200);
        canvas.DrawRect(perfLeft, y + 2, perfW, height - 4, _fillPaint);

        // Perfect-Zone Glow-Puls
        if (isPlaying)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(_sawAnimTime * 4);
            _fillPaint.Color = PerfectZone.WithAlpha((byte)(40 * pulse));
            canvas.DrawRect(perfLeft - 2, y - 2, perfW + 4, height + 4, _fillPaint);
        }

        // Tick-Markierungen
        _strokePaint.Color = new SKColor(255, 255, 255, 40);
        _strokePaint.StrokeWidth = 1;
        for (float t = 0.1f; t < 1.0f; t += 0.1f)
        {
            float tickX = x + t * width;
            canvas.DrawLine(tickX, y, tickX, y + 4, _strokePaint);
            canvas.DrawLine(tickX, y + height - 4, tickX, y + height, _strokePaint);
        }

        // Marker
        float markerX = x + (float)(markerPos * width);

        _fillPaint.Color = new SKColor(0, 0, 0, 80);
        canvas.DrawRect(markerX - 2, y - 3, 6, height + 6, _fillPaint);

        _fillPaint.Color = MarkerColor;
        canvas.DrawRect(markerX - 3, y - 4, 6, height + 8, _fillPaint);

        _fillPaint.Color = new SKColor(255, 255, 255, 200);
        canvas.DrawRect(markerX - 1, y - 3, 2, 4, _fillPaint);

        // Marker-Spitze
        _fillPaint.Color = MarkerColor;
        canvas.DrawRect(markerX - 4, y - 7, 8, 3, _fillPaint);
        canvas.DrawRect(markerX - 2, y - 9, 4, 2, _fillPaint);
    }

    /// <summary>
    /// Erzeugt Saegemehl-Explosion beim Schnitt-Start.
    /// </summary>
    private void SpawnCutBurst(float cutX, float cutY, float woodHeight)
    {
        var random = Random.Shared;

        // Grosse Saegemehl-Wolke
        for (int i = 0; i < 30 && _sawdustCount < MaxSawdust; i++)
        {
            _sawdust[_sawdustCount++] = new SawdustParticle
            {
                X = cutX + random.Next(-12, 13),
                Y = cutY + random.Next(-8, 8),
                VelocityX = (float)(random.NextDouble() - 0.5) * 120,
                VelocityY = -40 - random.Next(0, 80),
                Life = 0,
                MaxLife = 0.8f + (float)random.NextDouble() * 0.8f,
                Size = 1 + random.Next(0, 4)
            };
        }

        // Holzsplitter (groesser, drehen sich)
        for (int i = 0; i < 8 && _woodChipCount < MaxWoodChips; i++)
        {
            _woodChips[_woodChipCount++] = new WoodChipParticle
            {
                X = cutX + random.Next(-6, 7),
                Y = cutY + random.Next(-4, 4),
                VelocityX = (float)(random.NextDouble() - 0.5) * 80,
                VelocityY = -50 - random.Next(0, 60),
                Life = 0,
                MaxLife = 1.0f + (float)random.NextDouble() * 0.5f,
                Width = 3 + random.Next(0, 4),
                Height = 1 + random.Next(0, 2),
                Rotation = (float)random.NextDouble() * 360,
                RotSpeed = (float)(random.NextDouble() - 0.5) * 400,
                Color = random.Next(3) switch
                {
                    0 => WoodLight,
                    1 => WoodMedium,
                    _ => WoodGrain
                }
            };
        }
    }

    /// <summary>
    /// Erzeugt kontinuierliches Saegemehl waehrend des Schnitts.
    /// </summary>
    private void SpawnCuttingSawdust(float cutX, float cutY)
    {
        if (_sawdustCount >= MaxSawdust - 10) return;
        var random = Random.Shared;

        _sawdust[_sawdustCount++] = new SawdustParticle
        {
            X = cutX + random.Next(-4, 5),
            Y = cutY + random.Next(-2, 3),
            VelocityX = (float)(random.NextDouble() - 0.5) * 50,
            VelocityY = -15 - random.Next(0, 25),
            Life = 0,
            MaxLife = 0.4f + (float)random.NextDouble() * 0.4f,
            Size = 1 + random.Next(0, 3)
        };
    }

    /// <summary>
    /// Aktualisiert und zeichnet Saegemehl-Partikel.
    /// </summary>
    private void UpdateAndDrawSawdust(SKCanvas canvas, SKRect bounds, float padding,
        float woodTop, float woodHeight, float deltaTime, bool isPlaying)
    {
        var random = Random.Shared;
        float cutX = bounds.Left + padding + (bounds.Width - 2 * padding) * 0.5f;
        float woodBottom = woodTop + woodHeight;

        // Waehrend des Spiels: Standard-Saegemehl an der Schnittstelle
        if (isPlaying && _sawdustCount < 20)
        {
            _sawdust[_sawdustCount++] = new SawdustParticle
            {
                X = cutX + random.Next(-8, 9),
                Y = woodBottom - random.Next(0, 10),
                VelocityX = (float)(random.NextDouble() - 0.5) * 40,
                VelocityY = -20 - random.Next(0, 30),
                Life = 0,
                MaxLife = 0.5f + (float)random.NextDouble() * 0.5f,
                Size = 1 + random.Next(0, 3)
            };
        }

        // Partikel aktualisieren und zeichnen (Compact-Loop: lebende Partikel nach vorne schieben)
        int aliveCount = 0;
        for (int i = 0; i < _sawdustCount; i++)
        {
            var p = _sawdust[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 60 * deltaTime; // Schwerkraft

            if (p.Life >= p.MaxLife) continue;

            _sawdust[aliveCount++] = p;

            float alpha = 1 - (p.Life / p.MaxLife);
            _fillPaint.Color = new SKColor(0xD2, 0xB4, 0x8C, (byte)(alpha * 220));
            canvas.DrawRect(p.X, p.Y, p.Size, p.Size, _fillPaint);
        }
        _sawdustCount = aliveCount;
    }

    /// <summary>
    /// Aktualisiert und zeichnet Holzsplitter-Partikel (rotierende Stuecke).
    /// </summary>
    private void UpdateAndDrawWoodChips(SKCanvas canvas, float deltaTime)
    {
        int aliveCount = 0;
        for (int i = 0; i < _woodChipCount; i++)
        {
            var p = _woodChips[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 80 * deltaTime; // Schwerkraft
            p.Rotation += p.RotSpeed * deltaTime;

            if (p.Life >= p.MaxLife) continue;

            _woodChips[aliveCount++] = p;

            float alpha = 1 - (p.Life / p.MaxLife);
            _fillPaint.Color = p.Color.WithAlpha((byte)(alpha * 255));

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateDegrees(p.Rotation);
            canvas.DrawRect(-p.Width / 2, -p.Height / 2, p.Width, p.Height, _fillPaint);
            canvas.Restore();
        }
        _woodChipCount = aliveCount;
    }

    /// <summary>
    /// EaseOutBack-Kurve fuer federnden Spaltungs-Effekt.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedPath?.Dispose();
        _fillPaint?.Dispose();
        _fillAAPaint?.Dispose();
        _strokePaint?.Dispose();
        _strokeAAPaint?.Dispose();
        _shaderPaint?.Dispose();
        _blurPaint?.Dispose();
        _grainPaint1?.Dispose();
        _grainPaint2?.Dispose();
        _grainPaint3?.Dispose();
        _fineGrainPaint?.Dispose();
        _knotBlurFilter?.Dispose();

        // Gecachter statischer Shader
        _woodGradientShader?.Dispose();
    }
}
