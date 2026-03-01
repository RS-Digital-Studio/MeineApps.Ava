using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Art der Belohnungs-Zeremonie (bestimmt Farbe, Feuerwerk-Intensität, Dauer).
/// </summary>
public enum CeremonyType
{
    /// <summary>Level-Meilenstein (10/25/50/100/250/500/1000).</summary>
    LevelMilestone,
    /// <summary>Workshop-Meilenstein (50/100/250/500/1000).</summary>
    WorkshopMilestone,
    /// <summary>Prestige-Aufstieg.</summary>
    Prestige,
    /// <summary>Achievement freigeschaltet.</summary>
    Achievement,
    /// <summary>Meisterwerkzeug gesammelt.</summary>
    MasterTool,
}

/// <summary>
/// Full-Screen Belohnungs-Zeremonie mit Feuerwerk, Confetti, Scale-In und Text-Animation.
/// Eigene Instanz (kein Singleton), wird beim Auslösen erstellt und nach Ablauf entsorgt.
/// Gecachte SKPaint-Objekte, struct-basiertes Confetti.
/// </summary>
public class RewardCeremonyRenderer
{
    // --- Konfig ---
    private const float TotalDuration = 4.0f;       // Gesamtdauer in Sekunden
    private const float FadeInDuration = 0.4f;       // Backdrop fade-in
    private const float ScaleInDuration = 0.6f;      // Zentrales Element scale-in
    private const float TextDelay = 0.5f;             // Text erscheint nach X Sekunden
    private const float FadeOutStart = 3.2f;          // Ausblenden beginnt
    private const int MaxConfetti = 120;

    // --- State ---
    private float _elapsed;
    private bool _isActive;
    private CeremonyType _type;
    private string _title = "";
    private string _subtitle = "";
    private SKColor _accentColor;
    private readonly FireworksRenderer _fireworks = new();

    // --- Confetti ---
    private readonly ConfettiParticle[] _confetti = new ConfettiParticle[MaxConfetti];
    private int _confettiCount;
    private uint _rngState = 271;

    // --- Gecachte Paints ---
    private static readonly SKPaint _backdropPaint = new() { IsAntialias = true };
    private static readonly SKPaint _circlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12f)
    };
    private static readonly SKPaint _ringPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f
    };
    private static readonly SKPaint _textPaint = new()
    {
        IsAntialias = true,
        TextAlign = SKTextAlign.Center,
        FakeBoldText = true
    };
    private static readonly SKPaint _subtitlePaint = new()
    {
        IsAntialias = true,
        TextAlign = SKTextAlign.Center
    };
    private static readonly SKPaint _confettiPaint = new() { IsAntialias = true };
    private static readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gecachter SKPath fuer Icon-Zeichnung (vermeidet GC-Allokationen pro Frame)
    private readonly SKPath _iconPath = new();

    /// <summary>
    /// Ob die Zeremonie aktiv ist.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Startet eine neue Belohnungs-Zeremonie.
    /// </summary>
    /// <param name="type">Art der Zeremonie</param>
    /// <param name="title">Haupttext (z.B. "Level 100!")</param>
    /// <param name="subtitle">Untertext (z.B. "+50 Goldschrauben")</param>
    public void Start(CeremonyType type, string title, string subtitle)
    {
        _type = type;
        _title = title;
        _subtitle = subtitle;
        _elapsed = 0f;
        _isActive = true;
        _confettiCount = 0;

        _accentColor = type switch
        {
            CeremonyType.LevelMilestone => new SKColor(0xF5, 0x9E, 0x0B),     // Amber
            CeremonyType.WorkshopMilestone => new SKColor(0xD9, 0x77, 0x06),   // Craft-Orange
            CeremonyType.Prestige => new SKColor(0xFF, 0xD7, 0x00),            // Gold
            CeremonyType.Achievement => new SKColor(0x22, 0xC5, 0x5E),         // Grün
            CeremonyType.MasterTool => new SKColor(0xA8, 0x55, 0xF7),          // Lila
            _ => new SKColor(0xFF, 0xD7, 0x00)
        };

        _fireworks.Clear();
    }

    /// <summary>
    /// Vorzeitig beenden (Tap-to-Close).
    /// </summary>
    public void Dismiss()
    {
        // Schnell ausblenden statt sofort weg
        if (_elapsed < FadeOutStart) _elapsed = FadeOutStart;
    }

    /// <summary>
    /// Update-Tick (typisch deltaTime = 0.05 bei 20fps).
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_isActive) return;

        _elapsed += deltaTime;

        if (_elapsed >= TotalDuration)
        {
            _isActive = false;
            _fireworks.Clear();
            return;
        }

        // Feuerwerk-Raketen zeitversetzt starten
        if (_elapsed > 0.2f && _elapsed < 2.5f)
        {
            float rocketChance = _type == CeremonyType.Prestige ? 0.15f : 0.08f;
            if (NextRandom() < rocketChance)
            {
                _fireworks.LaunchRocket(
                    50f + NextRandom() * 300f,
                    400f, // Wird beim Render mit echten Bounds angepasst
                    40f + NextRandom() * 120f);
            }
        }

        _fireworks.Update(deltaTime);

        // Confetti spawnen in den ersten 2 Sekunden
        if (_elapsed < 2.0f && _confettiCount < MaxConfetti)
        {
            int toSpawn = Math.Min(3, MaxConfetti - _confettiCount);
            for (int i = 0; i < toSpawn; i++)
            {
                _confetti[_confettiCount++] = new ConfettiParticle
                {
                    X = NextRandom() * 400f, // Wird beim Render skaliert
                    Y = -10f - NextRandom() * 30f,
                    VX = NextRandom(-30f, 30f),
                    VY = 60f + NextRandom() * 80f,
                    Rotation = NextRandom() * 360f,
                    RotationSpeed = NextRandom(-180f, 180f),
                    Width = 4f + NextRandom() * 4f,
                    Height = 2f + NextRandom() * 3f,
                    Life = 3f + NextRandom(),
                    Color = ConfettiColors[(int)(NextRandom() * ConfettiColors.Length) % ConfettiColors.Length]
                };
            }
        }

        // Confetti aktualisieren
        for (int i = 0; i < _confettiCount; i++)
        {
            ref var c = ref _confetti[i];
            c.Life -= deltaTime;
            c.X += c.VX * deltaTime;
            c.Y += c.VY * deltaTime;
            c.VX += MathF.Sin(c.Y * 0.02f) * 20f * deltaTime; // Seitliches Flattern
            c.Rotation += c.RotationSpeed * deltaTime;
        }
    }

    /// <summary>
    /// Rendert die komplette Zeremonie als Fullscreen-Overlay.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (!_isActive) return;

        float t = _elapsed / TotalDuration;

        // --- 1. Dunkler Backdrop (fade-in/out) ---
        float backdropAlpha;
        if (_elapsed < FadeInDuration)
            backdropAlpha = _elapsed / FadeInDuration;
        else if (_elapsed > FadeOutStart)
            backdropAlpha = 1f - (_elapsed - FadeOutStart) / (TotalDuration - FadeOutStart);
        else
            backdropAlpha = 1f;

        _backdropPaint.Color = new SKColor(0, 0, 0, (byte)(180 * Math.Clamp(backdropAlpha, 0f, 1f)));
        canvas.DrawRect(bounds, _backdropPaint);

        float centerX = bounds.MidX;
        float centerY = bounds.MidY - bounds.Height * 0.05f; // Leicht oberhalb der Mitte

        // --- 2. Feuerwerk im Hintergrund ---
        _fireworks.Render(canvas, bounds);

        // --- 3. Confetti ---
        float scaleX = bounds.Width / 400f;
        for (int i = 0; i < _confettiCount; i++)
        {
            ref readonly var c = ref _confetti[i];
            if (c.Life <= 0f) continue;

            float alpha = Math.Clamp(c.Life / 1f, 0f, 1f); // Fade-Out in letzter Sekunde
            _confettiPaint.Color = c.Color.WithAlpha((byte)(200 * alpha * backdropAlpha));

            canvas.Save();
            canvas.Translate(bounds.Left + c.X * scaleX, bounds.Top + c.Y);
            canvas.RotateDegrees(c.Rotation);
            canvas.DrawRect(-c.Width / 2f, -c.Height / 2f, c.Width, c.Height, _confettiPaint);
            canvas.Restore();
        }

        // --- 4. Zentraler Glow-Kreis (scale-in) ---
        if (_elapsed > 0.1f)
        {
            float scaleT = Math.Clamp((_elapsed - 0.1f) / ScaleInDuration, 0f, 1f);
            float scale = EasingFunctions.EaseOutBack(scaleT, 1.5f);
            float circleRadius = 50f * scale;

            // Äußerer Glow
            _glowPaint.Color = _accentColor.WithAlpha((byte)(60 * backdropAlpha));
            canvas.DrawCircle(centerX, centerY, circleRadius + 20f, _glowPaint);

            // Innerer Kreis
            _circlePaint.Color = _accentColor.WithAlpha((byte)(200 * backdropAlpha));
            canvas.DrawCircle(centerX, centerY, circleRadius, _circlePaint);

            // Pulsierender Ring
            float ringPulse = 1f + MathF.Sin(_elapsed * 4f) * 0.08f;
            _ringPaint.Color = SKColors.White.WithAlpha((byte)(120 * backdropAlpha));
            _ringPaint.StrokeWidth = 2f;
            canvas.DrawCircle(centerX, centerY, circleRadius * ringPulse + 4f, _ringPaint);

            // Icon im Kreis
            DrawCeremonyIcon(canvas, centerX, centerY, circleRadius * 0.5f, backdropAlpha);

            // Expandierender Ring (einmalig beim Scale-In)
            if (scaleT < 1f)
            {
                float expandT = EasingFunctions.EaseOutCubic(scaleT);
                float expandRadius = circleRadius + 40f * expandT;
                byte expandAlpha = (byte)(150 * (1f - expandT) * backdropAlpha);
                _ringPaint.Color = _accentColor.WithAlpha(expandAlpha);
                _ringPaint.StrokeWidth = 3f * (1f - expandT) + 0.5f;
                canvas.DrawCircle(centerX, centerY, expandRadius, _ringPaint);
            }
        }

        // --- 5. Titel-Text (slide-in von unten) ---
        if (_elapsed > TextDelay)
        {
            float textT = Math.Clamp((_elapsed - TextDelay) / 0.4f, 0f, 1f);
            float textEased = EasingFunctions.EaseOutBack(textT, 1.2f);
            float titleY = centerY + 80f + (1f - textEased) * 30f;

            _textPaint.TextSize = 24f;
            _textPaint.Color = SKColors.White.WithAlpha((byte)(255 * Math.Min(textEased, backdropAlpha)));
            canvas.DrawText(_title, centerX, titleY, _textPaint);

            // Untertitel
            if (!string.IsNullOrEmpty(_subtitle) && _elapsed > TextDelay + 0.2f)
            {
                float subT = Math.Clamp((_elapsed - TextDelay - 0.2f) / 0.3f, 0f, 1f);
                float subEased = EasingFunctions.EaseOutCubic(subT);
                float subtitleY = titleY + 28f + (1f - subEased) * 15f;

                _subtitlePaint.TextSize = 16f;
                _subtitlePaint.Color = _accentColor.WithAlpha((byte)(230 * Math.Min(subEased, backdropAlpha)));
                canvas.DrawText(_subtitle, centerX, subtitleY, _subtitlePaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE: Icon je nach CeremonyType
    // ═══════════════════════════════════════════════════════════════════

    private void DrawCeremonyIcon(SKCanvas canvas, float cx, float cy, float size, float alpha)
    {
        _iconPaint.Color = SKColors.White.WithAlpha((byte)(240 * alpha));

        switch (_type)
        {
            case CeremonyType.LevelMilestone:
                // Aufwärtspfeil (Level-Up)
                DrawArrowUp(canvas, cx, cy, size);
                break;

            case CeremonyType.WorkshopMilestone:
                // Stern
                DrawStar(canvas, cx, cy, size);
                break;

            case CeremonyType.Prestige:
                // Krone
                DrawCrown(canvas, cx, cy, size);
                break;

            case CeremonyType.Achievement:
                // Trophäe
                DrawTrophy(canvas, cx, cy, size);
                break;

            case CeremonyType.MasterTool:
                // Hammer
                DrawHammer(canvas, cx, cy, size);
                break;
        }
    }

    private void DrawArrowUp(SKCanvas canvas, float cx, float cy, float s)
    {
        _iconPath.Reset();
        _iconPath.MoveTo(cx, cy - s);            // Spitze
        _iconPath.LineTo(cx + s * 0.6f, cy);     // Rechts
        _iconPath.LineTo(cx + s * 0.2f, cy);
        _iconPath.LineTo(cx + s * 0.2f, cy + s * 0.7f);
        _iconPath.LineTo(cx - s * 0.2f, cy + s * 0.7f);
        _iconPath.LineTo(cx - s * 0.2f, cy);
        _iconPath.LineTo(cx - s * 0.6f, cy);     // Links
        _iconPath.Close();
        canvas.DrawPath(_iconPath, _iconPaint);
    }

    private void DrawStar(SKCanvas canvas, float cx, float cy, float s)
    {
        _iconPath.Reset();
        for (int i = 0; i < 10; i++)
        {
            float angle = (i * 36f - 90f) * MathF.PI / 180f;
            float r = (i % 2 == 0) ? s : s * 0.4f;
            float px = cx + MathF.Cos(angle) * r;
            float py = cy + MathF.Sin(angle) * r;
            if (i == 0) _iconPath.MoveTo(px, py); else _iconPath.LineTo(px, py);
        }
        _iconPath.Close();
        canvas.DrawPath(_iconPath, _iconPaint);
    }

    private void DrawCrown(SKCanvas canvas, float cx, float cy, float s)
    {
        _iconPath.Reset();
        float w = s * 0.9f, h = s * 0.7f;
        // Basis
        _iconPath.MoveTo(cx - w, cy + h * 0.4f);
        // 3 Zacken nach oben
        _iconPath.LineTo(cx - w * 0.6f, cy - h * 0.3f);
        _iconPath.LineTo(cx - w * 0.3f, cy + h * 0.1f);
        _iconPath.LineTo(cx, cy - h);
        _iconPath.LineTo(cx + w * 0.3f, cy + h * 0.1f);
        _iconPath.LineTo(cx + w * 0.6f, cy - h * 0.3f);
        _iconPath.LineTo(cx + w, cy + h * 0.4f);
        _iconPath.Close();
        canvas.DrawPath(_iconPath, _iconPaint);

        // Juwelen auf Spitzen
        _iconPaint.Color = _accentColor;
        canvas.DrawCircle(cx, cy - h + 2f, 2.5f, _iconPaint);
        canvas.DrawCircle(cx - w * 0.6f, cy - h * 0.3f + 2f, 2f, _iconPaint);
        canvas.DrawCircle(cx + w * 0.6f, cy - h * 0.3f + 2f, 2f, _iconPaint);
        _iconPaint.Color = SKColors.White;
    }

    private void DrawTrophy(SKCanvas canvas, float cx, float cy, float s)
    {
        // Kelch-Körper
        float w = s * 0.7f, h = s * 0.6f;
        _iconPath.Reset();
        _iconPath.MoveTo(cx - w, cy - h);
        _iconPath.LineTo(cx + w, cy - h);
        _iconPath.LineTo(cx + w * 0.6f, cy + h * 0.3f);
        _iconPath.LineTo(cx - w * 0.6f, cy + h * 0.3f);
        _iconPath.Close();
        canvas.DrawPath(_iconPath, _iconPaint);

        // Fuß
        canvas.DrawRect(cx - s * 0.15f, cy + h * 0.3f, s * 0.3f, s * 0.3f, _iconPaint);
        canvas.DrawRect(cx - s * 0.4f, cy + h * 0.6f, s * 0.8f, s * 0.15f, _iconPaint);

        // Henkel
        _ringPaint.Color = _iconPaint.Color;
        _ringPaint.StrokeWidth = 2.5f;
        canvas.DrawArc(new SKRect(cx - w - 6f, cy - h * 0.5f, cx - w + 4f, cy + h * 0.1f), -90f, 180f, false, _ringPaint);
        canvas.DrawArc(new SKRect(cx + w - 4f, cy - h * 0.5f, cx + w + 6f, cy + h * 0.1f), 90f, -180f, false, _ringPaint);
    }

    private void DrawHammer(SKCanvas canvas, float cx, float cy, float s)
    {
        // Stiel
        canvas.Save();
        canvas.RotateDegrees(-30f, cx, cy);

        var stielColor = new SKColor(0x8B, 0x69, 0x14);
        _iconPaint.Color = stielColor;
        canvas.DrawRect(cx - s * 0.08f, cy - s * 0.1f, s * 0.16f, s * 1.1f, _iconPaint);

        // Kopf
        _iconPaint.Color = new SKColor(0x78, 0x71, 0x6C); // Metall
        canvas.DrawRoundRect(cx - s * 0.45f, cy - s * 0.35f, s * 0.9f, s * 0.35f, 3f, 3f, _iconPaint);

        // Metall-Glanz
        _iconPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 60);
        canvas.DrawRect(cx - s * 0.4f, cy - s * 0.33f, s * 0.8f, 2f, _iconPaint);

        canvas.Restore();
        _iconPaint.Color = SKColors.White;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE: Random + Confetti
    // ═══════════════════════════════════════════════════════════════════

    private static readonly SKColor[] ConfettiColors =
    [
        new(0xFF, 0xD7, 0x00), // Gold
        new(0xFF, 0x45, 0x00), // Orange-Rot
        new(0x00, 0xBF, 0xFF), // Blau
        new(0x22, 0xC5, 0x5E), // Grün
        new(0xFF, 0x69, 0xB4), // Pink
        new(0xBF, 0x40, 0xFF), // Lila
        new(0xFF, 0xFF, 0xFF), // Weiß
        new(0xF5, 0x9E, 0x0B), // Amber
    ];

    private float NextRandom()
    {
        _rngState = _rngState * 1103515245 + 12345;
        return (_rngState >> 16 & 0x7FFF) / (float)0x7FFF;
    }

    private float NextRandom(float min, float max) => min + NextRandom() * (max - min);

    private struct ConfettiParticle
    {
        public float X, Y;
        public float VX, VY;
        public float Rotation, RotationSpeed;
        public float Width, Height;
        public float Life;
        public SKColor Color;
    }
}
