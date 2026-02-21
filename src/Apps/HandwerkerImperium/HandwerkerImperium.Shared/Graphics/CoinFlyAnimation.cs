using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Eigenständige Münzen-Flug-Animation (unabhängig von GameJuiceEngine).
/// Rendert Münzen die von einer Quelle zum Währungs-HUD fliegen.
/// Kann direkt in Views eingebettet werden.
/// </summary>
public class CoinFlyAnimation
{
    private const int MaxCoins = 16;
    private readonly FlyingCoin[] _coins = new FlyingCoin[MaxCoins];
    private int _coinCount;

    // HUD-Pulse-Effekt (wenn Münze ankommt)
    private float _hudPulseScale = 1f;
    private float _hudPulseTimer;
    private const float HudPulseDuration = 0.2f;

    // Gecachte Paints
    private readonly SKPaint _coinPaint = new() { IsAntialias = true };
    private readonly SKPaint _highlightPaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0xFF, 0xFF, 0xFF, 80)
    };
    private readonly SKPaint _edgePaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0xDA, 0xA5, 0x20) // Dunkles Gold
    };
    private readonly SKPaint _trailPaint = new() { IsAntialias = true };

    /// <summary>
    /// Callback wenn eine Münze am Ziel ankommt (für Sound-Trigger).
    /// </summary>
    public event Action? CoinArrived;

    /// <summary>
    /// Aktuelle HUD-Pulse-Skalierung (1.0 = normal, >1.0 = vergrößert).
    /// Views können dies auf ihr Währungs-Icon anwenden.
    /// </summary>
    public float HudPulseScale => _hudPulseScale;

    /// <summary>
    /// Gibt true zurück wenn Münzen aktiv fliegen.
    /// </summary>
    public bool IsActive => _coinCount > 0 || _hudPulseTimer > 0;

    /// <summary>
    /// Münzen-Flug starten.
    /// </summary>
    /// <param name="fromX">Start X (z.B. Upgrade-Button).</param>
    /// <param name="fromY">Start Y.</param>
    /// <param name="toX">Ziel X (z.B. Währungs-Anzeige im HUD).</param>
    /// <param name="toY">Ziel Y.</param>
    /// <param name="count">Anzahl Münzen (8-12 empfohlen).</param>
    /// <param name="coinSize">Münz-Durchmesser in dp (6-10 empfohlen).</param>
    public void Start(float fromX, float fromY, float toX, float toY, int count = 10, float coinSize = 8f)
    {
        count = Math.Clamp(count, 4, MaxCoins);
        uint rng = (uint)(fromX * 31 + fromY * 17 + count * 7);

        for (int i = 0; i < count; i++)
        {
            if (_coinCount >= MaxCoins) break;

            // Pseudo-Random für Kontrollpunkt-Variation
            rng = rng * 1664525 + 1013904223;
            float randX = ((rng >> 16) / 65535f - 0.5f) * 2f; // -1 bis 1
            rng = rng * 1664525 + 1013904223;
            float randY = (rng >> 16) / 65535f; // 0 bis 1

            float delay = i * 0.04f; // 40ms versetzt
            float midX = (fromX + toX) * 0.5f + randX * 50f;
            float midY = Math.Min(fromY, toY) - 40f - randY * 40f;

            _coins[_coinCount++] = new FlyingCoin
            {
                StartX = fromX,
                StartY = fromY,
                TargetX = toX,
                TargetY = toY,
                ControlX = midX,
                ControlY = midY,
                Duration = 0.55f,
                Timer = 0.55f + delay,
                Delay = delay,
                Size = coinSize,
                IsActive = true
            };
        }
    }

    /// <summary>
    /// Animation updaten. Jeden Frame aufrufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        // HUD-Pulse abklingen
        if (_hudPulseTimer > 0)
        {
            _hudPulseTimer -= deltaTime;
            float pulseProgress = 1f - Math.Max(0, _hudPulseTimer) / HudPulseDuration;
            _hudPulseScale = 1f + 0.25f * (1f - EasingFunctions.EaseOutCubic(pulseProgress));
        }
        else
        {
            _hudPulseScale = 1f;
        }

        // Münzen updaten (rückwärts für Swap-Remove)
        for (int i = _coinCount - 1; i >= 0; i--)
        {
            ref var coin = ref _coins[i];
            coin.Timer -= deltaTime;

            if (coin.Timer <= 0)
            {
                // Münze angekommen
                _hudPulseTimer = HudPulseDuration;
                _hudPulseScale = 1.25f;
                CoinArrived?.Invoke();

                // Swap-Remove
                _coins[i] = _coins[_coinCount - 1];
                _coinCount--;
            }
        }
    }

    /// <summary>
    /// Fliegende Münzen rendern.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        for (int i = 0; i < _coinCount; i++)
        {
            ref var coin = ref _coins[i];

            // Delay-Phase: Noch nicht rendern
            float elapsed = (coin.Duration + coin.Delay) - coin.Timer;
            if (elapsed < coin.Delay) continue;

            float progress = (elapsed - coin.Delay) / coin.Duration;
            progress = Math.Clamp(progress, 0f, 1f);
            float easedT = EasingFunctions.EaseInOutQuint(progress);

            // Quadratische Bezier-Kurve
            float oneMinusT = 1f - easedT;
            float cx = oneMinusT * oneMinusT * coin.StartX
                      + 2f * oneMinusT * easedT * coin.ControlX
                      + easedT * easedT * coin.TargetX;
            float cy = oneMinusT * oneMinusT * coin.StartY
                      + 2f * oneMinusT * easedT * coin.ControlY
                      + easedT * easedT * coin.TargetY;

            // Skalierung (schrumpft beim Ziel)
            float scale = EasingFunctions.LerpClamped(1f, 0.5f, progress);
            float size = coin.Size * scale;

            // Trail (leichte Spur hinter der Münze)
            if (progress > 0.1f && progress < 0.9f)
            {
                float trailT = easedT - 0.05f;
                float trailX = (1f - trailT) * (1f - trailT) * coin.StartX
                              + 2f * (1f - trailT) * trailT * coin.ControlX
                              + trailT * trailT * coin.TargetX;
                float trailY = (1f - trailT) * (1f - trailT) * coin.StartY
                              + 2f * (1f - trailT) * trailT * coin.ControlY
                              + trailT * trailT * coin.TargetY;

                _trailPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 40);
                canvas.DrawCircle(trailX, trailY, size * 0.6f, _trailPaint);
            }

            // Münz-Körper (Gold)
            _coinPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
            canvas.DrawCircle(cx, cy, size, _coinPaint);

            // Rand (dunkleres Gold)
            _edgePaint.StrokeWidth = Math.Max(0.5f, size * 0.15f);
            _edgePaint.Style = SKPaintStyle.Stroke;
            canvas.DrawCircle(cx, cy, size * 0.85f, _edgePaint);
            _edgePaint.Style = SKPaintStyle.Fill;

            // Glanz-Highlight (oben links)
            canvas.DrawCircle(cx - size * 0.25f, cy - size * 0.25f, size * 0.3f, _highlightPaint);

            // Euro-Markierung (E in der Mitte)
            if (size > 4f)
            {
                _edgePaint.Style = SKPaintStyle.Fill;
                float markSize = size * 0.3f;
                // Einfaches "E" als drei horizontale Striche
                _edgePaint.StrokeWidth = Math.Max(0.5f, size * 0.12f);
                _edgePaint.Style = SKPaintStyle.Stroke;
                canvas.DrawLine(cx - markSize, cy - markSize * 0.5f, cx + markSize * 0.3f, cy - markSize * 0.5f, _edgePaint);
                canvas.DrawLine(cx - markSize, cy, cx + markSize * 0.1f, cy, _edgePaint);
                canvas.DrawLine(cx - markSize, cy + markSize * 0.5f, cx + markSize * 0.3f, cy + markSize * 0.5f, _edgePaint);
            }
        }
    }

    /// <summary>
    /// Alle Münzen sofort entfernen.
    /// </summary>
    public void Clear()
    {
        _coinCount = 0;
        _hudPulseTimer = 0;
        _hudPulseScale = 1f;
    }

    private struct FlyingCoin
    {
        public float StartX, StartY;
        public float TargetX, TargetY;
        public float ControlX, ControlY;
        public float Duration;
        public float Timer;
        public float Delay;
        public float Size;
        public bool IsActive;
    }
}
