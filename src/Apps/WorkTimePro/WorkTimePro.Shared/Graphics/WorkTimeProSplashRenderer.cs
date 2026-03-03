using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace WorkTimePro.Graphics;

/// <summary>
/// App-spezifischer Splash-Renderer für WorkTimePro: "Die Stechuhr".
/// Professioneller Dunkelgrau-Hintergrund mit Kalender-Grid,
/// animierte Stechuhr mit Karten-Stempelzyklus, Business-Partikel.
/// Alle Paints/Fonts sind gecacht (keine per-frame Allokation).
/// </summary>
public sealed class WorkTimeProSplashRenderer : SplashRendererBase
{
    public WorkTimeProSplashRenderer()
    {
        _titleFont = new SKFont(_titleTypeface) { Size = 26f };
    }

    // --- Farb-Konstanten ---
    private static readonly SKColor BgTop = SKColor.Parse("#141820");
    private static readonly SKColor BgBottom = SKColor.Parse("#0A0D12");
    private static readonly SKColor GridLineColor = new(0xFF, 0xFF, 0xFF, 15); // Alpha ~6%
    private static readonly SKColor HousingColor = SKColor.Parse("#2A2E3A");
    private static readonly SKColor HousingBorderColor = SKColor.Parse("#3B4050");
    private static readonly SKColor SlotColor = new(0x15, 0x18, 0x22);
    private static readonly SKColor CardColor = new(0xE8, 0xE8, 0xEC);
    private static readonly SKColor CardLineColor = new(0xB0, 0xB0, 0xB8);
    private static readonly SKColor StampColor = SKColor.Parse("#EF4444");
    private static readonly SKColor ProgressStart = SKColor.Parse("#3B82F6");
    private static readonly SKColor ProgressEnd = SKColor.Parse("#60A5FA");
    private static readonly SKColor ProgressBg = SKColor.Parse("#1A1E28");
    private static readonly SKColor ParticleBlue = new(0x3B, 0x82, 0xF6);

    // --- Stechuhr-Animation ---
    private const float CycleDuration = 3.0f;

    // --- Business-Partikel ---
    private const int MaxParticles = 10;

    private struct BusinessParticle
    {
        public float X, Y, Alpha, Phase, Speed, Size;
        public bool IsSquare;
    }

    private readonly BusinessParticle[] _particles = new BusinessParticle[MaxParticles];

    // --- Gecachte Paints ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = GridLineColor };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private readonly SKPaint _housingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _housingBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = HousingBorderColor };
    private readonly SKPaint _slotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SlotColor };
    private readonly SKPaint _cardPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = CardColor };
    private readonly SKPaint _cardLinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = CardLineColor };
    private readonly SKPaint _stampPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = StampColor };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts + Typefaces ---
    private readonly SKTypeface _titleTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
    private readonly SKFont _titleFont;

    protected override void OnUpdate(float deltaTime)
    {
        // Business-Partikel bewegen (langsam schwebend)
        for (var i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            p.Phase += deltaTime * 0.6f;
            p.Y -= p.Speed * deltaTime;
            p.X += MathF.Sin(p.Phase) * 0.1f * deltaTime;

            // Wrap wenn oben raus
            if (p.Y < -0.05f)
            {
                p.Y = 1.05f;
                p.X = (float)Rng.NextDouble();
                p.IsSquare = Rng.NextDouble() > 0.5;
            }
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeIfNeeded();

        RenderBackground(canvas, bounds);
        RenderCalendarGrid(canvas, w, h);
        RenderParticles(canvas, w, h);
        RenderAppName(canvas, w, h);
        RenderTimeClock(canvas, w, h);

        // Progress-Balken bei y ~ 75%
        var barWidth = MathF.Min(240f, w * 0.6f);
        DrawProgressBar(canvas, w, h * 0.75f, barWidth, 6f, 3f, ProgressStart, ProgressEnd, ProgressBg);

        DrawStatusText(canvas, w, h * 0.80f);
        DrawVersion(canvas, w, h * 0.92f);
    }

    private void InitializeIfNeeded()
    {
        if (IsInitialized) return;
        IsInitialized = true;

        for (var i = 0; i < MaxParticles; i++)
        {
            _particles[i] = new BusinessParticle
            {
                X = (float)Rng.NextDouble(),
                Y = (float)Rng.NextDouble(),
                Alpha = 30f + (float)Rng.NextDouble() * 20f,
                Phase = (float)(Rng.NextDouble() * Math.PI * 2),
                Speed = 0.01f + (float)Rng.NextDouble() * 0.015f,
                Size = 3f + (float)Rng.NextDouble() * 2f,
                IsSquare = Rng.NextDouble() > 0.5
            };
        }
    }

    /// <summary>Professionelles Dunkelgrau Gradient.</summary>
    private void RenderBackground(SKCanvas canvas, SKRect bounds)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.MidX, bounds.Top),
            new SKPoint(bounds.MidX, bounds.Bottom),
            new[] { BgTop, BgBottom },
            null,
            SKShaderTileMode.Clamp);

        _bgPaint.Shader = shader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;
    }

    /// <summary>Dezentes Kalender-Grid (vertikale + horizontale Linien).</summary>
    private void RenderCalendarGrid(SKCanvas canvas, float w, float h)
    {
        const float vSpacing = 50f;
        const float hSpacing = 30f;

        // Vertikale Linien (Tage)
        var vCount = (int)(w / vSpacing) + 1;
        for (var i = 0; i <= vCount; i++)
        {
            var x = i * vSpacing;
            canvas.DrawLine(x, 0, x, h, _gridPaint);
        }

        // Horizontale Linien (Stunden)
        var hCount = (int)(h / hSpacing) + 1;
        for (var i = 0; i <= hCount; i++)
        {
            var y = i * hSpacing;
            canvas.DrawLine(0, y, w, y, _gridPaint);
        }
    }

    /// <summary>Business-Partikel (dezente geometrische Formen).</summary>
    private void RenderParticles(SKCanvas canvas, float w, float h)
    {
        for (var i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            var px = p.X * w;
            var py = p.Y * h;

            // Alpha pulsiert leicht
            var alpha = (byte)Math.Clamp(p.Alpha + 10f * MathF.Sin(p.Phase * 0.4f), 20, 50);
            _particlePaint.Color = ParticleBlue.WithAlpha(alpha);

            if (p.IsSquare)
            {
                var half = p.Size / 2f;
                canvas.DrawRect(px - half, py - half, p.Size, p.Size, _particlePaint);
            }
            else
            {
                // Rechteck (breiter als hoch)
                canvas.DrawRect(px - p.Size * 0.7f, py - p.Size * 0.35f, p.Size * 1.4f, p.Size * 0.7f, _particlePaint);
            }
        }
    }

    /// <summary>App-Name oben bei y ~ 12%, weiß, bold.</summary>
    private void RenderAppName(SKCanvas canvas, float w, float h)
    {
        _titleFont.Size = MathF.Min(26f, w * 0.065f);
        DrawCenteredText(canvas, AppName, h * 0.12f, _titleFont, _titlePaint, w);
    }

    /// <summary>
    /// Stechuhr-Gehäuse mit animierter Zeitkarte (y ~ 30-60%).
    /// Zyklisch: Karte gleitet rein, Stempel drückt, Tinte erscheint, Karte gleitet raus.
    /// </summary>
    private void RenderTimeClock(SKCanvas canvas, float w, float h)
    {
        var centerX = w / 2f;
        var centerY = h * 0.45f;
        var scale = MathF.Min(1f, w / 400f);

        // Gehäuse-Dimensionen (skaliert)
        var housingW = 120f * scale;
        var housingH = 80f * scale;
        var housingRadius = 8f * scale;
        var housingRect = new SKRect(
            centerX - housingW / 2f, centerY - housingH / 2f,
            centerX + housingW / 2f, centerY + housingH / 2f);

        // --- Gehäuse zeichnen (metallischer vertikaler Gradient) ---
        using var housingShader = SKShader.CreateLinearGradient(
            new SKPoint(centerX, housingRect.Top),
            new SKPoint(centerX, housingRect.Bottom),
            new[] { HousingBorderColor, HousingColor, HousingColor, HousingBorderColor },
            new[] { 0f, 0.15f, 0.85f, 1f },
            SKShaderTileMode.Clamp);

        _housingPaint.Shader = housingShader;
        canvas.DrawRoundRect(housingRect, housingRadius, housingRadius, _housingPaint);
        _housingPaint.Shader = null;

        // Gehäuse-Rand
        canvas.DrawRoundRect(housingRect, housingRadius, housingRadius, _housingBorderPaint);

        // --- Schlitz oben ---
        var slotW = 60f * scale;
        var slotH = 6f * scale;
        var slotRect = new SKRect(
            centerX - slotW / 2f, housingRect.Top - slotH / 2f,
            centerX + slotW / 2f, housingRect.Top + slotH / 2f);
        canvas.DrawRoundRect(slotRect, 2f, 2f, _slotPaint);

        // --- Stechuhr-Animation (zyklisch) ---
        var cycleTime = Time % CycleDuration;
        var phase = cycleTime / CycleDuration; // 0-1

        // Karten-Dimensionen
        var cardW = 50f * scale;
        var cardH = 35f * scale;
        var cardCorner = 3f * scale;

        // Karten-Position berechnen (Y-Offset basierend auf Phase)
        float cardOffsetY;
        if (phase < 0.3f)
        {
            // Phase 0-0.3: Karte gleitet von oben in den Schlitz
            var t = phase / 0.3f;
            // EaseOut
            t = 1f - (1f - t) * (1f - t);
            cardOffsetY = MathF.FusedMultiplyAdd(-cardH * 1.5f, 1f - t, 0f);
        }
        else if (phase < 0.6f)
        {
            // Phase 0.3-0.6: Karte ist drin (leichte Vibration beim Stempeln)
            cardOffsetY = 0f;
        }
        else
        {
            // Phase 0.6-1.0: Karte gleitet wieder raus
            var t = (phase - 0.6f) / 0.4f;
            // EaseIn
            t = t * t;
            cardOffsetY = -cardH * 1.5f * t;
        }

        var cardCenterY = housingRect.Top + cardOffsetY;
        var cardRect = new SKRect(
            centerX - cardW / 2f, cardCenterY - cardH / 2f,
            centerX + cardW / 2f, cardCenterY + cardH / 2f);

        // Karte clippen (nur innerhalb des Gehäuses + etwas darüber sichtbar)
        canvas.Save();
        var clipRect = new SKRect(
            housingRect.Left - 5f, housingRect.Top - cardH * 1.8f,
            housingRect.Right + 5f, housingRect.Bottom + 5f);
        canvas.ClipRect(clipRect);

        // --- Zeitkarte zeichnen ---
        canvas.DrawRoundRect(cardRect, cardCorner, cardCorner, _cardPaint);

        // Zeitraster-Linien auf der Karte
        var lineSpacing = cardH / 6f;
        for (var i = 1; i < 6; i++)
        {
            var ly = cardRect.Top + i * lineSpacing;
            canvas.DrawLine(cardRect.Left + 4f * scale, ly, cardRect.Right - 4f * scale, ly, _cardLinePaint);
        }

        // --- Stempel-Animation ---
        if (phase >= 0.3f && phase < 0.6f)
        {
            var stampPhase = (phase - 0.3f) / 0.3f;

            // Stempel bewegt sich nach unten (Phase 0.3-0.5)
            if (stampPhase < 0.67f)
            {
                var stampT = stampPhase / 0.67f;
                // Bump-Effekt (schnell runter, bounce zurück)
                float stampY;
                if (stampT < 0.5f)
                {
                    // Runter
                    stampY = stampT * 2f;
                }
                else
                {
                    // Bounce zurück
                    stampY = 1f - (stampT - 0.5f) * 2f;
                }

                var stampSize = 8f * scale;
                var stampX = centerX;
                var stampBaseY = housingRect.Top + 10f * scale;
                var stampYPos = stampBaseY + stampY * 12f * scale;

                // Stempel-Mechanismus (kleines rotes Rechteck)
                var stampRect = new SKRect(
                    stampX - stampSize / 2f, stampYPos - stampSize / 3f,
                    stampX + stampSize / 2f, stampYPos + stampSize / 3f);
                canvas.DrawRoundRect(stampRect, 1f, 1f, _stampPaint);
            }

            // Tinte erscheint auf der Karte (Phase 0.5-0.6 im Gesamtzyklus)
            if (stampPhase > 0.5f)
            {
                var inkAlpha = (byte)Math.Clamp((stampPhase - 0.5f) / 0.5f * 200f, 0, 200);
                _stampPaint.Color = StampColor.WithAlpha(inkAlpha);

                // Tinten-Punkt auf der Karte
                var inkX = centerX + 6f * scale;
                var inkY = cardRect.Top + cardH * 0.35f;
                canvas.DrawCircle(inkX, inkY, 2.5f * scale, _stampPaint);

                // Tinten-Linie (Zeitstempel)
                canvas.DrawLine(inkX - 4f * scale, inkY, inkX + 4f * scale, inkY, _stampPaint);

                // Farbe zurücksetzen
                _stampPaint.Color = StampColor;
            }
        }
        // Tinte bleibt sichtbar während Karte rausgleitet
        else if (phase >= 0.6f)
        {
            var fadeAlpha = (byte)Math.Clamp(200f * (1f - (phase - 0.6f) / 0.4f), 0, 200);
            if (fadeAlpha > 10)
            {
                _stampPaint.Color = StampColor.WithAlpha(fadeAlpha);
                var inkX = centerX + 6f * scale;
                var inkY = cardRect.Top + cardH * 0.35f;
                canvas.DrawCircle(inkX, inkY, 2.5f * scale, _stampPaint);
                canvas.DrawLine(inkX - 4f * scale, inkY, inkX + 4f * scale, inkY, _stampPaint);
                _stampPaint.Color = StampColor;
            }
        }

        canvas.Restore();

        // --- Schlitz nochmal drüber zeichnen (Karte verschwindet dahinter) ---
        canvas.DrawRoundRect(slotRect, 2f, 2f, _slotPaint);
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _gridPaint.Dispose();
        _titlePaint.Dispose();
        _housingPaint.Dispose();
        _housingBorderPaint.Dispose();
        _slotPaint.Dispose();
        _cardPaint.Dispose();
        _cardLinePaint.Dispose();
        _stampPaint.Dispose();
        _particlePaint.Dispose();
        _titleFont.Dispose();
        _titleTypeface.Dispose();
    }
}
