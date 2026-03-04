using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Horizontale Prestige-Tier-Roadmap mit 7 Medaillen, Verbindungslinien und Fortschritts-Glow.
/// Zeigt den aktuellen Prestige-Status und den Fortschritt zum nächsten Tier visuell an.
/// </summary>
public sealed class PrestigeRoadmapRenderer : IDisposable
{
    private bool _disposed;

    // Gecachte Paints (Instanz-Felder, werden in Dispose() freigegeben)
    private readonly SKPaint _fillPaint = new() { IsAntialias = true };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f };

    // Gecachter MaskFilter (wird nur bei Radius-Änderung neu erstellt)
    private SKMaskFilter? _glowMaskFilter;
    private float _lastGlowRadius;

    // Alle 7 Tiers in Reihenfolge
    private static readonly PrestigeTier[] AllTiers =
    [
        PrestigeTier.Bronze,
        PrestigeTier.Silver,
        PrestigeTier.Gold,
        PrestigeTier.Platin,
        PrestigeTier.Diamant,
        PrestigeTier.Meister,
        PrestigeTier.Legende,
    ];

    // Tier-Farben gecacht
    private static readonly SKColor[] TierColors =
    [
        SKColor.Parse("#CD7F32"),  // Bronze
        SKColor.Parse("#C0C0C0"),  // Silver
        SKColor.Parse("#FFD700"),  // Gold
        SKColor.Parse("#E5E4E2"),  // Platin
        SKColor.Parse("#B9F2FF"),  // Diamant
        SKColor.Parse("#FF4500"),  // Meister
        SKColor.Parse("#FF69B4"),  // Legende
    ];

    // Tier-Symbole (einfache Unicode-Zeichen)
    private static readonly string[] TierSymbols = ["B", "S", "G", "P", "D", "M", "L"];

    // Gecachte Level-Strings (vermeidet String-Interpolation pro Frame)
    private static readonly string[] TierLevelStrings = AllTiers
        .Select(t => $"Lv.{t.GetRequiredLevel()}")
        .ToArray();

    /// <summary>
    /// Rendert die Prestige-Roadmap.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Verfügbarer Bereich</param>
    /// <param name="currentHighestTier">Aktuell höchster verfügbarer Tier</param>
    /// <param name="tierCounts">Anzahl Prestiges pro Tier [Bronze..Legende]</param>
    /// <param name="nextTierProgress">Fortschritt zum nächsten Tier (0-1)</param>
    /// <param name="animTime">Animationszeit in Sekunden für Glow-Pulsieren</param>
    public void Render(SKCanvas canvas, SKRect bounds, PrestigeTier currentHighestTier,
        int[] tierCounts, double nextTierProgress, float animTime)
    {
        if (_disposed) return;

        var w = bounds.Width;
        var h = bounds.Height;

        // Medaillen-Layout: Gleichmäßig horizontal verteilt
        float padding = 24f;
        float medalRadius = Math.Min(16f, (w - padding * 2) / 14f - 2f);
        float usableWidth = w - padding * 2 - medalRadius * 2;
        float medalY = h * 0.45f;
        float spacing = usableWidth / 6f; // 6 Lücken für 7 Medaillen

        int currentIndex = currentHighestTier != PrestigeTier.None ? (int)currentHighestTier - 1 : -1;
        int nextIndex = currentIndex + 1;

        // 1. Verbindungslinien zeichnen
        for (int i = 0; i < 6; i++)
        {
            float x1 = padding + medalRadius + i * spacing;
            float x2 = x1 + spacing;
            bool isCompleted = i < currentIndex;
            bool isActive = i == currentIndex && currentIndex >= 0;

            if (isCompleted)
            {
                _linePaint.Color = TierColors[i].WithAlpha(200);
                _linePaint.StrokeWidth = 3f;
            }
            else if (isActive && nextIndex < 7)
            {
                // Fortschritts-Linie (teilweise gefüllt)
                // Hintergrund
                _linePaint.Color = new SKColor(80, 80, 80, 100);
                _linePaint.StrokeWidth = 2f;
                canvas.DrawLine(x1, medalY, x2, medalY, _linePaint);

                // Fortschritt
                float progressX = x1 + (x2 - x1) * (float)nextTierProgress;
                _linePaint.Color = TierColors[nextIndex].WithAlpha(180);
                _linePaint.StrokeWidth = 3f;
                canvas.DrawLine(x1, medalY, progressX, medalY, _linePaint);
                continue;
            }
            else
            {
                _linePaint.Color = new SKColor(80, 80, 80, 100);
                _linePaint.StrokeWidth = 2f;
            }

            canvas.DrawLine(x1, medalY, x2, medalY, _linePaint);
        }

        // MaskFilter nur bei Radius-Änderung neu erstellen (statt pro Frame)
        float glowRadius = medalRadius * 0.6f;
        if (Math.Abs(glowRadius - _lastGlowRadius) > 0.01f)
        {
            _glowMaskFilter?.Dispose();
            _glowMaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowRadius);
            _lastGlowRadius = glowRadius;
        }

        // 2. Medaillen zeichnen
        for (int i = 0; i < 7; i++)
        {
            float cx = padding + medalRadius + i * spacing;
            bool isUnlocked = i <= currentIndex;
            bool isCurrent = i == currentIndex;
            bool isNext = i == nextIndex && nextIndex < 7;
            var color = TierColors[i];

            // Glow auf aktuellem Tier (pulsierend, ~1Hz)
            if (isCurrent)
            {
                float pulse = 0.6f + 0.4f * (float)Math.Sin(animTime * 6.0);
                _glowPaint.Color = color.WithAlpha((byte)(60 * pulse));
                _glowPaint.MaskFilter = _glowMaskFilter;
                canvas.DrawCircle(cx, medalY, medalRadius * 1.4f, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Medaillen-Kreis
            if (isUnlocked)
            {
                _fillPaint.Color = color;
                canvas.DrawCircle(cx, medalY, medalRadius, _fillPaint);

                // Innerer Ring (Tiefe)
                _strokePaint.Color = color.WithAlpha(80);
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawCircle(cx, medalY, medalRadius * 0.75f, _strokePaint);
            }
            else
            {
                // Gesperrter Tier: Grauer Ring
                _fillPaint.Color = new SKColor(60, 60, 60, 180);
                canvas.DrawCircle(cx, medalY, medalRadius, _fillPaint);
                _strokePaint.Color = new SKColor(100, 100, 100, 120);
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawCircle(cx, medalY, medalRadius, _strokePaint);

                // Nächster Tier: Farbiger Rand als Teaser
                if (isNext)
                {
                    _strokePaint.Color = color.WithAlpha(120);
                    _strokePaint.StrokeWidth = 2f;
                    canvas.DrawCircle(cx, medalY, medalRadius, _strokePaint);
                }
            }

            // Tier-Buchstabe
            _textPaint.Color = isUnlocked ? SKColors.White : new SKColor(150, 150, 150);
            _textPaint.TextSize = medalRadius * 0.9f;
            _textPaint.TextAlign = SKTextAlign.Center;
            _textPaint.FakeBoldText = isCurrent;
            canvas.DrawText(TierSymbols[i], cx, medalY + medalRadius * 0.3f, _textPaint);

            // Tier-Count unter der Medaille (nur wenn > 0)
            int count = i < tierCounts.Length ? tierCounts[i] : 0;
            if (count > 0)
            {
                _textPaint.Color = color.WithAlpha(200);
                _textPaint.TextSize = medalRadius * 0.55f;
                _textPaint.FakeBoldText = false;
                canvas.DrawText($"x{count}", cx, medalY + medalRadius + medalRadius * 0.7f, _textPaint);
            }

            // Benötigtes Level über der Medaille (gecachter String)
            _textPaint.Color = isUnlocked
                ? color.WithAlpha(150)
                : new SKColor(120, 120, 120, 150);
            _textPaint.TextSize = medalRadius * 0.5f;
            _textPaint.FakeBoldText = false;
            canvas.DrawText(TierLevelStrings[i], cx, medalY - medalRadius - 4f, _textPaint);
        }
    }

    /// <summary>
    /// HitTest: Gibt den Index des Tiers zurück (-1 wenn kein Treffer).
    /// </summary>
    public int HitTest(float x, float y, SKRect bounds)
    {
        float padding = 24f;
        float w = bounds.Width;
        float medalRadius = Math.Min(16f, (w - padding * 2) / 14f - 2f);
        float usableWidth = w - padding * 2 - medalRadius * 2;
        float medalY = bounds.Height * 0.45f;
        float spacing = usableWidth / 6f;
        float hitRadius = medalRadius * 1.8f;

        for (int i = 0; i < 7; i++)
        {
            float cx = padding + medalRadius + i * spacing;
            float dx = x - cx;
            float dy = y - medalY;
            if (dx * dx + dy * dy <= hitRadius * hitRadius)
                return i;
        }

        return -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _textPaint.Dispose();
        _glowPaint.Dispose();
        _linePaint.Dispose();
        _glowMaskFilter?.Dispose();
    }
}
