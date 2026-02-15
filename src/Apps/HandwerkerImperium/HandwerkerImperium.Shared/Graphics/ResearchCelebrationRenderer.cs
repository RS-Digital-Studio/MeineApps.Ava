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
public class ResearchCelebrationRenderer
{
    // Celebration-State
    private bool _isActive;
    private float _celebrationTime;
    private float _celebrationDuration = 2.5f;
    private ResearchBranch _celebrationBranch;
    private string _bonusText = string.Empty;

    // Partikel
    private readonly List<ConfettiParticle> _confetti = [];
    private readonly List<GlowRing> _glowRings = [];

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
        _confetti.Clear();
        _glowRings.Clear();

        // Initiale Confetti-Burst
        SpawnConfetti(40);

        // Glow-Ring
        _glowRings.Add(new GlowRing
        {
            Radius = 10,
            MaxRadius = 300,
            Alpha = 1.0f,
            Speed = 200
        });
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
        if (_celebrationTime < 2.0f)
        {
            DrawBonusText(canvas, cx, cy, branchColor);
        }

        // Animation beenden
        if (_celebrationTime >= _celebrationDuration && _confetti.Count == 0)
        {
            _isActive = false;
            _confetti.Clear();
            _glowRings.Clear();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GLOW-RINGE
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawGlowRings(SKCanvas canvas, float cx, float cy,
        SKColor branchColor, float deltaTime)
    {
        for (int i = _glowRings.Count - 1; i >= 0; i--)
        {
            var ring = _glowRings[i];
            ring.Radius += ring.Speed * deltaTime;
            ring.Alpha -= deltaTime * 0.6f;

            if (ring.Alpha <= 0 || ring.Radius > ring.MaxRadius)
            {
                _glowRings.RemoveAt(i);
                continue;
            }

            // Goldener Glow-Ring
            _stroke.Color = GoldGlow.WithAlpha((byte)(ring.Alpha * 120));
            _stroke.StrokeWidth = 4 * ring.Alpha;
            canvas.DrawCircle(cx, cy, ring.Radius, _stroke);

            // Branch-farbiger innerer Ring
            _stroke.Color = branchColor.WithAlpha((byte)(ring.Alpha * 80));
            _stroke.StrokeWidth = 2 * ring.Alpha;
            canvas.DrawCircle(cx, cy, ring.Radius * 0.8f, _stroke);
        }

        // Nachfolgende Ringe spawnen
        if (_celebrationTime < 0.6f && _celebrationTime > 0.2f && _glowRings.Count < 3)
        {
            if ((_celebrationTime * 10) % 3 < deltaTime * 10 + 0.1f)
            {
                _glowRings.Add(new GlowRing
                {
                    Radius = 5,
                    MaxRadius = 250,
                    Alpha = 0.8f,
                    Speed = 160
                });
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONFETTI
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnConfetti(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Shared.NextSingle() * MathF.Tau;
            float speed = 100 + Random.Shared.NextSingle() * 200;

            _confetti.Add(new ConfettiParticle
            {
                X = 0, // Wird beim Rendern auf Mitte des Bounds gesetzt
                Y = 0,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed - 50, // Leicht nach oben
                Rotation = Random.Shared.NextSingle() * 360,
                RotationSpeed = (Random.Shared.NextSingle() - 0.5f) * 500,
                Color = ConfettiColors[Random.Shared.Next(ConfettiColors.Length)],
                Life = 1.5f + Random.Shared.NextSingle() * 1.0f,
                Size = 3 + Random.Shared.NextSingle() * 4,
                NeedsInit = true
            });
        }
    }

    private void UpdateAndDrawConfetti(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        for (int i = _confetti.Count - 1; i >= 0; i--)
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
            p.VY += 180 * deltaTime;  // Gravity
            p.VX *= 0.98f;            // Luftwiderstand
            p.Rotation += p.RotationSpeed * deltaTime;
            p.Life -= deltaTime;

            if (p.Life <= 0 || p.Y > bounds.Bottom + 50)
            {
                _confetti.RemoveAt(i);
                continue;
            }

            // Zeichnen (rotiertes Rechteck)
            byte alpha = (byte)(Math.Min(p.Life, 1.0f) * 255);
            _fill.Color = p.Color.WithAlpha(alpha);

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateDegrees(p.Rotation);
            canvas.DrawRect(-p.Size / 2, -p.Size * 0.3f, p.Size, p.Size * 0.6f, _fill);
            canvas.Restore();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BONUS-TEXT
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawBonusText(SKCanvas canvas, float cx, float cy, SKColor branchColor)
    {
        if (string.IsNullOrEmpty(_bonusText)) return;

        // Text steigt auf und wird groesser
        float textProgress = Math.Clamp(_celebrationTime / 1.5f, 0, 1);
        float textY = cy - textProgress * 60;
        float textScale = 0.5f + textProgress * 0.5f;

        // Fade-out ab 70%
        float textAlpha = textProgress < 0.7f ? 1.0f : 1.0f - (textProgress - 0.7f) / 0.3f;

        // "Forschung abgeschlossen!" (oben)
        using var titleFont = new SKFont { Size = 18 * textScale, Embolden = true };
        _textPaint.Color = GoldGlow.WithAlpha((byte)(textAlpha * 255));

        // Schatten
        _textPaint.Color = new SKColor(0, 0, 0, (byte)(textAlpha * 150));
        canvas.DrawText("Forschung abgeschlossen!", cx + 1, textY - 18 + 1, SKTextAlign.Center, titleFont, _textPaint);

        // Text
        _textPaint.Color = GoldGlow.WithAlpha((byte)(textAlpha * 255));
        canvas.DrawText("Forschung abgeschlossen!", cx, textY - 18, SKTextAlign.Center, titleFont, _textPaint);

        // Bonus-Text darunter
        using var bonusFont = new SKFont { Size = 14 * textScale, Embolden = true };
        _textPaint.Color = branchColor.WithAlpha((byte)(textAlpha * 255));
        canvas.DrawText(_bonusText, cx, textY + 6, SKTextAlign.Center, bonusFont, _textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-KLASSEN
    // ═══════════════════════════════════════════════════════════════════════

    private class ConfettiParticle
    {
        public float X, Y, VX, VY, Rotation, RotationSpeed, Life, Size;
        public SKColor Color;
        public bool NeedsInit;
    }

    private class GlowRing
    {
        public float Radius, MaxRadius, Alpha, Speed;
    }
}
