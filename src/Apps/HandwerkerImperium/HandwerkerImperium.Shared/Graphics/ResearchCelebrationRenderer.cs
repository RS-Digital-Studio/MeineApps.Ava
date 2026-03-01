using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert Feier-Effekte bei Abschluss einer Forschung:
/// - Goldener Glow-Ring der sich ausbreitet
/// - Branch-farbige Confetti-Explosion
/// - Aufsteigende Bonus-Texte ("+5% Effizienz")
/// - Leuchtende Verbindungslinie zum nächsten Item
/// </summary>
public class ResearchCelebrationRenderer : IDisposable
{
    private bool _disposed;
    // Celebration-State
    private bool _isActive;
    private float _celebrationTime;
    private float _celebrationDuration = 3.5f;
    private ResearchBranch _celebrationBranch;
    private string _bonusText = string.Empty;

    // Partikel (Fixed-Size struct-Pools, 0 GC)
    private const int MaxConfetti = 50;
    private readonly ConfettiParticle[] _confetti = new ConfettiParticle[MaxConfetti];
    private int _confettiCount;
    private const int MaxGlowRings = 5;
    private readonly GlowRing[] _glowRings = new GlowRing[MaxGlowRings];
    private int _glowRingCount;

    // Farben
    private static readonly SKColor GoldGlow = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor TextWhite = new(0xFF, 0xFF, 0xFF);

    private static readonly SKColor[] ConfettiColors =
    [
        new SKColor(0xFF, 0xD7, 0x00),  // Gold
        new SKColor(0xEA, 0x58, 0x0C),  // Orange
        new SKColor(0x4C, 0xAF, 0x50),  // Grün
        new SKColor(0x21, 0x96, 0xF3),  // Blau
        new SKColor(0xFF, 0x57, 0x22),  // Dunkel-Orange
        new SKColor(0xFF, 0xEB, 0x3B),  // Gelb
        new SKColor(0xE9, 0x1E, 0x63),  // Pink
        new SKColor(0x9C, 0x27, 0xB0)   // Lila
    ];

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    // Gecachte Font-Objekte (vermeidet Allokationen pro Frame)
    private readonly SKFont _titleFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };
    private readonly SKFont _bonusFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };

    /// <summary>
    /// Ob die Celebration-Animation aktiv ist.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Startet eine Celebration-Animation.
    /// </summary>
    /// <param name="branch">Branch der abgeschlossenen Forschung.</param>
    /// <param name="bonusText">Bonus-Text (z.B. "+5% Effizienz").</param>
    public void StartCelebration(ResearchBranch branch, string bonusText)
    {
        _isActive = true;
        _celebrationTime = 0;
        _celebrationBranch = branch;
        _bonusText = bonusText;
        _confettiCount = 0;
        _glowRingCount = 0;

        // Initiale Confetti-Burst
        SpawnConfetti(40);

        // Glow-Ring
        if (_glowRingCount < MaxGlowRings)
        {
            _glowRings[_glowRingCount++] = new GlowRing
            {
                Radius = 10,
                MaxRadius = 300,
                Alpha = 1.0f,
                Speed = 140
            };
        }
    }

    /// <summary>
    /// Aktualisiert und rendert die Celebration-Animation.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Gesamter verfügbarer Bereich (über alles).</param>
    /// <param name="deltaTime">Zeitdelta in Sekunden.</param>
    public void Render(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        if (!_isActive) return;

        _celebrationTime += deltaTime;

        float cx = bounds.MidX;
        float cy = bounds.MidY;
        var branchColor = ResearchItemRenderer.GetBranchColor(_celebrationBranch);

        // Glow-Ringe
        UpdateAndDrawGlowRings(canvas, cx, cy, branchColor, deltaTime);

        // Confetti
        UpdateAndDrawConfetti(canvas, bounds, deltaTime);

        // Bonus-Text (aufsteigend)
        if (_celebrationTime < 3.0f)
        {
            DrawBonusText(canvas, cx, cy, branchColor);
        }

        // Animation beenden
        if (_celebrationTime >= _celebrationDuration && _confettiCount == 0)
        {
            _isActive = false;
            _confettiCount = 0;
            _glowRingCount = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GLOW-RINGE
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawGlowRings(SKCanvas canvas, float cx, float cy,
        SKColor branchColor, float deltaTime)
    {
        int aliveCount = 0;
        for (int i = 0; i < _glowRingCount; i++)
        {
            var ring = _glowRings[i];
            ring.Radius += ring.Speed * deltaTime;
            ring.Alpha -= deltaTime * 0.4f;

            if (ring.Alpha <= 0 || ring.Radius > ring.MaxRadius) continue;

            _glowRings[aliveCount++] = ring;

            // Goldener Glow-Ring
            _stroke.Color = GoldGlow.WithAlpha((byte)(ring.Alpha * 120));
            _stroke.StrokeWidth = 4 * ring.Alpha;
            canvas.DrawCircle(cx, cy, ring.Radius, _stroke);

            // Branch-farbiger innerer Ring
            _stroke.Color = branchColor.WithAlpha((byte)(ring.Alpha * 80));
            _stroke.StrokeWidth = 2 * ring.Alpha;
            canvas.DrawCircle(cx, cy, ring.Radius * 0.8f, _stroke);
        }
        _glowRingCount = aliveCount;

        // Nachfolgende Ringe spawnen
        if (_celebrationTime < 0.6f && _celebrationTime > 0.2f && _glowRingCount < 3)
        {
            if ((_celebrationTime * 10) % 3 < deltaTime * 10 + 0.1f)
            {
                if (_glowRingCount < MaxGlowRings)
                {
                    _glowRings[_glowRingCount++] = new GlowRing
                    {
                        Radius = 5,
                        MaxRadius = 250,
                        Alpha = 0.8f,
                        Speed = 110
                    };
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONFETTI
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnConfetti(int count)
    {
        for (int i = 0; i < count && _confettiCount < MaxConfetti; i++)
        {
            float angle = Random.Shared.NextSingle() * MathF.Tau;
            float speed = 70 + Random.Shared.NextSingle() * 140;

            _confetti[_confettiCount++] = new ConfettiParticle
            {
                X = 0, // Wird beim Rendern auf Mitte des Bounds gesetzt
                Y = 0,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed - 50, // Leicht nach oben
                Rotation = Random.Shared.NextSingle() * 360,
                RotationSpeed = (Random.Shared.NextSingle() - 0.5f) * 300,
                Color = ConfettiColors[Random.Shared.Next(ConfettiColors.Length)],
                Life = 2.0f + Random.Shared.NextSingle() * 1.5f,
                Size = 3 + Random.Shared.NextSingle() * 4,
                NeedsInit = true
            };
        }
    }

    private void UpdateAndDrawConfetti(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        int aliveCount = 0;
        for (int i = 0; i < _confettiCount; i++)
        {
            var p = _confetti[i];

            // Erstmaliges Positionieren auf Mitte
            if (p.NeedsInit)
            {
                p.X = cx + (Random.Shared.NextSingle() - 0.5f) * 20;
                p.Y = cy + (Random.Shared.NextSingle() - 0.5f) * 20;
                p.NeedsInit = false;
            }

            // Physik
            p.X += p.VX * deltaTime;
            p.Y += p.VY * deltaTime;
            p.VY += 120 * deltaTime;  // Gravity
            p.VX *= 0.98f;            // Luftwiderstand
            p.Rotation += p.RotationSpeed * deltaTime;
            p.Life -= deltaTime;

            if (p.Life <= 0 || p.Y > bounds.Bottom + 50) continue;

            _confetti[aliveCount++] = p;

            // Zeichnen (rotiertes Rechteck)
            byte alpha = (byte)(Math.Min(p.Life, 1.0f) * 255);
            _fill.Color = p.Color.WithAlpha(alpha);

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateDegrees(p.Rotation);
            canvas.DrawRect(-p.Size / 2, -p.Size * 0.3f, p.Size, p.Size * 0.6f, _fill);
            canvas.Restore();
        }
        _confettiCount = aliveCount;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BONUS-TEXT
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawBonusText(SKCanvas canvas, float cx, float cy, SKColor branchColor)
    {
        if (string.IsNullOrEmpty(_bonusText)) return;

        // Text steigt auf und wird groesser
        float textProgress = Math.Clamp(_celebrationTime / 2.0f, 0, 1);
        float textY = cy - textProgress * 60;
        float textScale = 0.5f + textProgress * 0.5f;

        // Fade-out ab 70%
        float textAlpha = textProgress < 0.7f ? 1.0f : 1.0f - (textProgress - 0.7f) / 0.3f;

        // "Forschung abgeschlossen!" (oben)
        _titleFont.Size = 18 * textScale;
        _textPaint.Color = GoldGlow.WithAlpha((byte)(textAlpha * 255));

        // Schatten
        _textPaint.Color = new SKColor(0, 0, 0, (byte)(textAlpha * 150));
        canvas.DrawText("Forschung abgeschlossen!", cx + 1, textY - 18 + 1, SKTextAlign.Center, _titleFont, _textPaint);

        // Text
        _textPaint.Color = GoldGlow.WithAlpha((byte)(textAlpha * 255));
        canvas.DrawText("Forschung abgeschlossen!", cx, textY - 18, SKTextAlign.Center, _titleFont, _textPaint);

        // Bonus-Text darunter
        _bonusFont.Size = 14 * textScale;
        _textPaint.Color = branchColor.WithAlpha((byte)(textAlpha * 255));
        canvas.DrawText(_bonusText, cx, textY + 6, SKTextAlign.Center, _bonusFont, _textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-KLASSEN
    // ═══════════════════════════════════════════════════════════════════════

    private struct ConfettiParticle
    {
        public float X, Y, VX, VY, Rotation, RotationSpeed, Life, Size;
        public SKColor Color;
        public bool NeedsInit;
    }

    private struct GlowRing
    {
        public float Radius, MaxRadius, Alpha, Speed;
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _titleFont?.Dispose();
        _bonusFont?.Dispose();
    }
}
