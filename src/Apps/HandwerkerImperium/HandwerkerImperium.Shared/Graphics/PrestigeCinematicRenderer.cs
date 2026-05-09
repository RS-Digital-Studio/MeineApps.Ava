using System;
using System.Globalization;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// 4-Phasen Prestige-Cinematic (P0.3 — AAA-Audit 2026-05-08).
/// Verwandelt den Prestige-Reset von einer Tabellen-Ansicht in einen 14-Sekunden Wow-Moment.
///
/// Phase 1 (0-3s):  Money-Reverse-Counter — Geld rollt von <see cref="PrestigeCinematicData.MoneyAtPrestige"/> auf 0
/// Phase 2 (3-6s):  Tier-Badge mit Glow-Pulse + Confetti-Burst
/// Phase 3 (6-11s): Staggered Multiplier-Aufbau (Tier-Bonus → Diminishing → Bonus-PP → Final-Score)
/// Phase 4 (11-14s): Reward-Card "Tap to Continue"
///
/// Skip-Button wird vom ViewModel bei <see cref="IsSkipEnabled"/> sichtbar geschaltet.
/// </summary>
public sealed class PrestigeCinematicRenderer : IDisposable
{
    // --- Phasen-Zeiten (Sekunden) ---
    private const float PhaseMoneyEnd = 3.0f;
    private const float PhaseBadgeEnd = 6.0f;
    private const float PhaseMultiEnd = 11.0f;
    private const float PhaseRewardEnd = 14.0f;
    private const float SkipEnabledAfter = 2.0f;
    private const int MaxConfetti = 80;

    // --- State ---
    private float _elapsed;
    private bool _isActive;
    private bool _disposed;
    private PrestigeCinematicData? _data;
    private SKColor _accent;
    private bool _confettiSpawned;

    // --- Subsysteme ---
    private readonly FireworksRenderer _fireworks = new();

    // --- Confetti ---
    private struct Confetti
    {
        public float X, Y, Vx, Vy;
        public float Rotation, RotSpeed;
        public float Width, Height;
        public float Life;
        public SKColor Color;
    }
    private readonly Confetti[] _confetti = new Confetti[MaxConfetti];
    private int _confettiCount;
    private static readonly SKColor[] s_confettiColors =
    {
        new(0xFF, 0xD7, 0x00),  // Gold
        new(0xFF, 0x9F, 0x40),  // Orange
        new(0xEC, 0x4D, 0x4D),  // Rot
        new(0x60, 0xC8, 0xFF),  // Hellblau
        new(0xA8, 0x55, 0xF7),  // Lila
    };

    // --- Cached Paints / Fonts ---
    private static readonly SKPaint s_backdropPaint = new() { IsAntialias = true };
    private static readonly SKPaint s_glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18f),
    };
    private static readonly SKPaint s_circlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint s_strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f };
    private static readonly SKPaint s_textPaint = new() { IsAntialias = true };
    private static readonly SKPaint s_confettiPaint = new() { IsAntialias = true };
    private static readonly SKFont s_titleFont = new() { Size = 36f, Embolden = true };
    private static readonly SKFont s_bigFont = new() { Size = 64f, Embolden = true };
    private static readonly SKFont s_mediumFont = new() { Size = 22f, Embolden = true };
    private static readonly SKFont s_smallFont = new() { Size = 16f };

    /// <summary>Cinematic läuft.</summary>
    public bool IsActive => _isActive;

    /// <summary>Der Skip-Button darf jetzt angezeigt werden (~2s nach Start).</summary>
    public bool IsSkipEnabled => _isActive && _elapsed >= SkipEnabledAfter;

    /// <summary>
    /// Skip-Button-Hit-Box in Canvas-Koordinaten. Vom View fuer Pointer-Hit-Test.
    /// <c>null</c> wenn Skip aktuell nicht sichtbar.
    /// </summary>
    public SKRect? SkipButtonBounds { get; private set; }

    /// <summary>Aktuelle Phase (0=Money, 1=Badge, 2=Multi, 3=Reward).</summary>
    public int CurrentPhase => _elapsed < PhaseMoneyEnd ? 0
                             : _elapsed < PhaseBadgeEnd ? 1
                             : _elapsed < PhaseMultiEnd ? 2
                             : 3;

    /// <summary>Cinematic ist in der Reward-Phase und kann durch Tap beendet werden.</summary>
    public bool IsReadyForDismiss => _isActive && _elapsed >= PhaseMultiEnd;

    /// <summary>Cinematic-Animation natürlich abgeschlossen (Tap-zu-Continue erforderlich).</summary>
    public bool IsAnimationComplete => _isActive && _elapsed >= PhaseRewardEnd;

    /// <summary>Startet die Cinematic mit den gegebenen Daten.</summary>
    public void Start(PrestigeCinematicData data)
    {
        _data = data;
        _elapsed = 0f;
        _isActive = true;
        _confettiSpawned = false;
        _confettiCount = 0;
        _fireworks.Clear();
        _accent = TierAccent(data.Tier);
        // Code-Review-Fix [Finding 3]: SkipButtonBounds beim Start zuruecksetzen,
        // damit ein Tap-Handler nicht alte Hit-Box aus vorherigem Cinematic-Run kennt.
        SkipButtonBounds = null;
    }

    /// <summary>Skip-Button gedrückt — direkt in die Reward-Phase springen.</summary>
    public void Skip()
    {
        if (_elapsed < PhaseMultiEnd) _elapsed = PhaseMultiEnd + 0.1f;
    }

    /// <summary>Komplett beenden (Tap-to-Continue).</summary>
    public void Dismiss()
    {
        _isActive = false;
        _fireworks.Clear();
    }

    public void Update(float deltaTime)
    {
        if (!_isActive || _data == null) return;
        _elapsed += deltaTime;

        // Auto-Dismiss: Wenn Spieler nach 8 s in der Reward-Phase nicht tippt, automatisch ausblenden.
        // Verhindert dauerhafte Cinematic-Anzeige bei Hintergrund-Apps oder zerstreuten Spielern.
        const float autoDismissAfterReward = 8.0f;
        if (_elapsed >= PhaseRewardEnd + autoDismissAfterReward)
        {
            Dismiss();
            return;
        }

        // Code-Review-Fix [Finding 3]: SkipButtonBounds nullen sobald wir die Skip-Phase verlassen
        // — auch wenn der Render-Loop pausiert ist (z.B. App-Pause). Verhindert dass Tap-Handler
        // mit stale Bounds einen Skip triggert nachdem Phase 4 begonnen hat.
        if (!IsSkipEnabled || _elapsed >= PhaseMultiEnd)
            SkipButtonBounds = null;

        // Confetti-Burst beim Eintritt in Phase 2 (Badge-Reveal)
        if (!_confettiSpawned && _elapsed >= PhaseMoneyEnd)
        {
            SpawnConfettiBurst();
            _confettiSpawned = true;
        }

        // Feuerwerk waehrend Badge + Multiplier-Phase
        if (_elapsed > PhaseMoneyEnd && _elapsed < PhaseMultiEnd)
        {
            if (NextRandom() < 0.05f)
                _fireworks.LaunchRocket(50f + NextRandom() * 300f, 600f, 80f + NextRandom() * 200f);
        }
        _fireworks.Update(deltaTime);

        // Confetti-Update
        for (int i = 0; i < _confettiCount; i++)
        {
            ref var c = ref _confetti[i];
            c.Life -= deltaTime;
            c.X += c.Vx * deltaTime;
            c.Y += c.Vy * deltaTime;
            c.Vx += MathF.Sin(c.Y * 0.02f) * 20f * deltaTime;
            c.Vy += 30f * deltaTime; // Schwerkraft
            c.Rotation += c.RotSpeed * deltaTime;
        }
    }

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (!_isActive || _data == null) return;

        // Backdrop fade-in
        var backdropAlpha = (byte)(220 * Math.Clamp(_elapsed / 0.4f, 0f, 1f));
        if (_elapsed > PhaseRewardEnd)
        {
            var fadeOut = 1f - Math.Clamp((_elapsed - PhaseRewardEnd) / 0.5f, 0f, 1f);
            backdropAlpha = (byte)(220 * fadeOut);
        }
        s_backdropPaint.Color = new SKColor(8, 6, 18, backdropAlpha);
        canvas.DrawRect(bounds, s_backdropPaint);

        var cx = bounds.MidX;
        var cy = bounds.MidY;

        if (_elapsed < PhaseMoneyEnd)
            RenderPhaseMoney(canvas, cx, cy);
        else if (_elapsed < PhaseBadgeEnd)
            RenderPhaseBadge(canvas, cx, cy);
        else if (_elapsed < PhaseMultiEnd)
            RenderPhaseMultiplier(canvas, cx, cy, bounds);
        else
            RenderPhaseReward(canvas, cx, cy, bounds);

        // Feuerwerk + Confetti immer im Vordergrund (waehrend Badge/Multi)
        if (_elapsed > PhaseMoneyEnd)
        {
            _fireworks.Render(canvas, bounds);
            RenderConfetti(canvas);
        }

        // Skip-Button oben rechts ab 2s — verschwindet in Phase 4 (Reward-Phase, dort gilt Tap=Dismiss)
        if (IsSkipEnabled && _elapsed < PhaseMultiEnd)
            RenderSkipButton(canvas, bounds);
        else
            SkipButtonBounds = null;
    }

    private void RenderSkipButton(SKCanvas canvas, SKRect bounds)
    {
        const float padding = 16f;
        const float btnW = 96f;
        const float btnH = 40f;
        var rect = new SKRect(
            bounds.Right - btnW - padding,
            bounds.Top + padding,
            bounds.Right - padding,
            bounds.Top + padding + btnH);

        // Hintergrund
        s_circlePaint.Color = new SKColor(40, 30, 70, 200);
        canvas.DrawRoundRect(rect, 12f, 12f, s_circlePaint);
        // Akzent-Border
        s_strokePaint.Color = _accent.WithAlpha(200);
        s_strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(rect, 12f, 12f, s_strokePaint);
        // Text
        s_textPaint.Color = SKColors.White;
        canvas.DrawText("SKIP ▶", rect.MidX, rect.MidY + 5f, SKTextAlign.Center, s_smallFont, s_textPaint);

        SkipButtonBounds = rect;
    }

    // ----------------- Phase 1: Reverse-Money-Counter -----------------
    private void RenderPhaseMoney(SKCanvas canvas, float cx, float cy)
    {
        // Rückwärts-Roll: 0..1 → Money..0
        var t = Math.Clamp(_elapsed / PhaseMoneyEnd, 0f, 1f);
        // Ease-Out: am Ende langsamer
        var eased = 1f - (1f - t) * (1f - t);
        var current = (decimal)((double)_data!.MoneyAtPrestige * (1.0 - eased));

        // Pulse-Effekt: zoom subtil
        var pulse = 1.0f + 0.05f * MathF.Sin(_elapsed * 6f);
        canvas.Save();
        canvas.Scale(pulse, pulse, cx, cy);

        // "Geld wird verbucht..." Untertitel
        s_textPaint.Color = new SKColor(180, 180, 200);
        canvas.DrawText("Prestige-Reset", cx, cy - 60f, SKTextAlign.Center, s_mediumFont, s_textPaint);

        // Money-Display gross
        s_textPaint.Color = new SKColor(255, 220, 100);
        var moneyText = FormatMoney(current);
        canvas.DrawText(moneyText, cx, cy + 30f, SKTextAlign.Center, s_bigFont, s_textPaint);

        canvas.Restore();
    }

    // ----------------- Phase 2: Tier-Badge mit Glow -----------------
    private void RenderPhaseBadge(SKCanvas canvas, float cx, float cy)
    {
        var local = _elapsed - PhaseMoneyEnd;
        var phaseT = Math.Clamp(local / (PhaseBadgeEnd - PhaseMoneyEnd), 0f, 1f);

        // Scale-In Ease-Out (1.5 → 1.0)
        var scale = 1.5f - 0.5f * (1f - (1f - phaseT) * (1f - phaseT));
        var pulse = 1.0f + 0.08f * MathF.Sin(local * 4f);

        // Glow hinter dem Badge
        s_glowPaint.Color = _accent.WithAlpha((byte)(220 * Math.Clamp(local / 0.3f, 0f, 1f)));
        canvas.DrawCircle(cx, cy, 90f * scale * pulse, s_glowPaint);

        // Badge-Hintergrund
        s_circlePaint.Color = new SKColor(40, 30, 70, 240);
        canvas.DrawCircle(cx, cy, 70f * scale, s_circlePaint);

        // Akzent-Ring
        s_strokePaint.Color = _accent;
        s_strokePaint.StrokeWidth = 3f * scale;
        canvas.DrawCircle(cx, cy, 70f * scale, s_strokePaint);

        // Tier-Name im Badge
        s_textPaint.Color = SKColors.White;
        canvas.DrawText(_data!.TierDisplayName, cx, cy + 8f, SKTextAlign.Center, s_titleFont, s_textPaint);

        // Subtitle "Aufgestiegen!" (fade-in nach 0.5s)
        if (local > 0.5f)
        {
            var alpha = (byte)(255 * Math.Clamp((local - 0.5f) / 0.4f, 0f, 1f));
            s_textPaint.Color = new SKColor(255, 215, 100, alpha);
            canvas.DrawText("AUFGESTIEGEN", cx, cy + 130f, SKTextAlign.Center, s_mediumFont, s_textPaint);
        }
    }

    // ----------------- Phase 3: Staggered Multiplier-Stack -----------------
    private void RenderPhaseMultiplier(SKCanvas canvas, float cx, float cy, SKRect bounds)
    {
        var local = _elapsed - PhaseBadgeEnd;

        // 4 Zeilen, jede pop-in nach 1.0s offset
        // Zeile 1: Tier-Bonus (z.B. "+35%")
        // Zeile 2: Diminishing (z.B. "× 0.83")
        // Zeile 3: Bonus-PP (z.B. "+5 PP")
        // Zeile 4: Final-Score (gross)

        var top = cy - 130f;
        var lineH = 50f;

        DrawStaggeredLine(canvas, cx, top + 0 * lineH, local, 0.0f, $"Tier-Bonus: +{_data!.TierMultiplierRaw * 100:F0}%", _accent);
        DrawStaggeredLine(canvas, cx, top + 1 * lineH, local, 0.7f, $"Diminishing: × {_data!.DiminishingReturnsFactor:F2}", new SKColor(180, 180, 200));
        if (_data.BonusPrestigePoints > 0)
            DrawStaggeredLine(canvas, cx, top + 2 * lineH, local, 1.4f, $"Bonus: +{_data.BonusPrestigePoints} PP", new SKColor(150, 220, 100));
        if (_data.ActiveChallengeCount > 0)
            DrawStaggeredLine(canvas, cx, top + 3 * lineH, local, 2.1f, $"{_data.ActiveChallengeCount} Challenges", new SKColor(255, 140, 80));

        // Final-Score Pop-In nach 2.8s gross
        if (local > 2.8f)
        {
            var fT = Math.Clamp((local - 2.8f) / 0.5f, 0f, 1f);
            var scale = 0.5f + 0.5f * fT;
            canvas.Save();
            canvas.Scale(scale, scale, cx, cy + 90f);

            s_textPaint.Color = new SKColor(255, 220, 100, (byte)(255 * fT));
            var totalPP = _data.BasePrestigePoints + _data.BonusPrestigePoints;
            canvas.DrawText($"+{totalPP} PP", cx, cy + 100f, SKTextAlign.Center, s_bigFont, s_textPaint);
            canvas.Restore();
        }
    }

    private static void DrawStaggeredLine(SKCanvas canvas, float x, float y, float local, float delay, string text, SKColor color)
    {
        if (local < delay) return;
        var t = Math.Clamp((local - delay) / 0.4f, 0f, 1f);
        var alpha = (byte)(255 * t);
        var slide = (1f - t) * 30f; // von rechts einrutschen
        s_textPaint.Color = color.WithAlpha(alpha);
        canvas.DrawText(text, x + slide, y, SKTextAlign.Center, s_mediumFont, s_textPaint);
    }

    // ----------------- Phase 4: Reward-Card -----------------
    private void RenderPhaseReward(SKCanvas canvas, float cx, float cy, SKRect bounds)
    {
        var local = _elapsed - PhaseMultiEnd;
        var fadeIn = Math.Clamp(local / 0.4f, 0f, 1f);

        // Reward-Card-Hintergrund
        var cardW = Math.Min(360f, bounds.Width - 32f);
        var cardH = 240f;
        var rect = new SKRect(cx - cardW / 2, cy - cardH / 2, cx + cardW / 2, cy + cardH / 2);
        s_circlePaint.Color = new SKColor(40, 30, 70, (byte)(240 * fadeIn));
        canvas.DrawRoundRect(rect, 16f, 16f, s_circlePaint);
        s_strokePaint.Color = _accent.WithAlpha((byte)(255 * fadeIn));
        s_strokePaint.StrokeWidth = 2f;
        canvas.DrawRoundRect(rect, 16f, 16f, s_strokePaint);

        // Titel
        s_textPaint.Color = SKColors.White.WithAlpha((byte)(255 * fadeIn));
        canvas.DrawText("Belohnung", cx, cy - 80f, SKTextAlign.Center, s_titleFont, s_textPaint);

        // Werte
        var totalPP = _data!.BasePrestigePoints + _data.BonusPrestigePoints;
        s_textPaint.Color = new SKColor(255, 220, 100, (byte)(255 * fadeIn));
        canvas.DrawText($"+{totalPP} Prestige-Punkte", cx, cy - 28f, SKTextAlign.Center, s_mediumFont, s_textPaint);

        s_textPaint.Color = new SKColor(180, 180, 200, (byte)(255 * fadeIn));
        var dur = TimeSpan.FromSeconds(_data.RunDurationSeconds);
        canvas.DrawText($"Run: {dur:hh\\:mm\\:ss}", cx, cy + 10f, SKTextAlign.Center, s_smallFont, s_textPaint);
        canvas.DrawText($"{_data.TierDisplayName} ×{_data.TierMultiplierEffective:F2}", cx, cy + 38f, SKTextAlign.Center, s_smallFont, s_textPaint);

        // "Tap to Continue" pulsiert
        if (local > 0.6f)
        {
            var pulse = 0.7f + 0.3f * MathF.Sin(local * 4f);
            s_textPaint.Color = SKColors.White.WithAlpha((byte)(220 * pulse));
            canvas.DrawText("Tippen zum Fortfahren", cx, cy + 90f, SKTextAlign.Center, s_smallFont, s_textPaint);
        }
    }

    // ----------------- Helpers -----------------
    private void SpawnConfettiBurst()
    {
        for (int i = 0; i < MaxConfetti && _confettiCount < MaxConfetti; i++)
        {
            _confetti[_confettiCount++] = new Confetti
            {
                X = 200f + NextRandom() * 200f, // Zentral starten
                Y = 100f + NextRandom() * 100f,
                Vx = (NextRandom() - 0.5f) * 400f,
                Vy = -100f - NextRandom() * 200f,
                Rotation = NextRandom() * 360f,
                RotSpeed = (NextRandom() - 0.5f) * 720f,
                Width = 5f + NextRandom() * 5f,
                Height = 3f + NextRandom() * 4f,
                Life = 4f + NextRandom() * 2f,
                Color = s_confettiColors[(int)(NextRandom() * s_confettiColors.Length) % s_confettiColors.Length]
            };
        }
    }

    private void RenderConfetti(SKCanvas canvas)
    {
        for (int i = 0; i < _confettiCount; i++)
        {
            ref var c = ref _confetti[i];
            if (c.Life <= 0) continue;
            var alpha = (byte)Math.Clamp(c.Life * 60, 0, 255);
            s_confettiPaint.Color = c.Color.WithAlpha(alpha);
            canvas.Save();
            canvas.Translate(c.X, c.Y);
            canvas.RotateDegrees(c.Rotation);
            canvas.DrawRect(-c.Width / 2, -c.Height / 2, c.Width, c.Height, s_confettiPaint);
            canvas.Restore();
        }
    }

    private static SKColor TierAccent(PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => new SKColor(0xCD, 0x7F, 0x32),
        PrestigeTier.Silver => new SKColor(0xC0, 0xC0, 0xC0),
        PrestigeTier.Gold => new SKColor(0xFF, 0xD7, 0x00),
        PrestigeTier.Platin => new SKColor(0xE5, 0xE4, 0xE2),
        PrestigeTier.Diamant => new SKColor(0x60, 0xC8, 0xFF),
        PrestigeTier.Meister => new SKColor(0xA8, 0x55, 0xF7),
        PrestigeTier.Legende => new SKColor(0xFF, 0x44, 0x88),
        _ => new SKColor(0xFF, 0xD7, 0x00),
    };

    private static string FormatMoney(decimal value)
    {
        if (value >= 1_000_000_000_000m) return (value / 1_000_000_000_000m).ToString("F2", CultureInfo.InvariantCulture) + "T";
        if (value >= 1_000_000_000m) return (value / 1_000_000_000m).ToString("F2", CultureInfo.InvariantCulture) + "B";
        if (value >= 1_000_000m) return (value / 1_000_000m).ToString("F2", CultureInfo.InvariantCulture) + "M";
        if (value >= 1_000m) return (value / 1_000m).ToString("F1", CultureInfo.InvariantCulture) + "K";
        return value.ToString("F0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Thread-safe Random-Quelle. Code-Review-Fix [Finding 6]: Vorher xorshift32
    /// mit eigener mutable State-Variable — bei zukuenftiger Background-Animation waere
    /// das ein Race. <see cref="Random.Shared"/> ist seit .NET 6 thread-safe und
    /// allokationsfrei (statisch).
    /// </summary>
    private static float NextRandom() => (float)Random.Shared.NextDouble();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fireworks.Clear();
        // Static Paints/Fonts werden NICHT disposed (Process-Lifetime)
    }
}
