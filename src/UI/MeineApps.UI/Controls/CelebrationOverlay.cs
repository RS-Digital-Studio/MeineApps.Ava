using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace MeineApps.UI.Controls;

/// <summary>
/// Confetti-Overlay das bei Erfolgen abgefeuert wird.
/// Reine Avalonia-Implementierung mit Canvas + Border-Controls als Partikel.
/// Kein SkiaSharp benoetigt.
/// </summary>
public class CelebrationOverlay : Canvas
{
    // Partikel-Pool (feste Groesse, keine GC-Allokationen pro Frame)
    private const int MaxParticles = 50;
    private readonly ParticleData[] _particles = new ParticleData[MaxParticles];
    private readonly Border[] _particleControls = new Border[MaxParticles];

    private DispatcherTimer? _timer;
    private DateTime _animationStart;
    private bool _isAnimating;

    // Konfetti-Farben: Gold, Amber, Rot, Gruen, Blau
    private static readonly Color[] ConfettiColors =
    [
        Color.Parse("#FFD700"),
        Color.Parse("#D97706"),
        Color.Parse("#DC2626"),
        Color.Parse("#22C55E"),
        Color.Parse("#2563EB")
    ];

    // Gecachte Brushes (vermeidet Allokation pro Partikel)
    private static readonly SolidColorBrush[] ConfettiBrushes;

    private readonly Random _rng = new();

    // Animationsdauer in Sekunden
    private const double AnimationDuration = 2.5;

    static CelebrationOverlay()
    {
        ConfettiBrushes = new SolidColorBrush[ConfettiColors.Length];
        for (int i = 0; i < ConfettiColors.Length; i++)
        {
            ConfettiBrushes[i] = new SolidColorBrush(ConfettiColors[i]);
        }
    }

    public CelebrationOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;

        // Partikel-Controls vorab erstellen (Pool)
        for (int i = 0; i < MaxParticles; i++)
        {
            var border = new Border
            {
                IsHitTestVisible = false,
                IsVisible = false,
                CornerRadius = new CornerRadius(1),
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };
            _particleControls[i] = border;
            Children.Add(border);
        }
    }

    /// <summary>
    /// Startet die Confetti-Animation. Kann mehrfach aufgerufen werden.
    /// </summary>
    public void ShowConfetti()
    {
        // Laufende Animation stoppen
        if (_isAnimating)
        {
            _timer?.Stop();
        }

        var width = Bounds.Width > 0 ? Bounds.Width : 400;
        var height = Bounds.Height > 0 ? Bounds.Height : 800;

        // 40 Partikel aktivieren
        var count = Math.Min(40, MaxParticles);

        for (int i = 0; i < MaxParticles; i++)
        {
            if (i < count)
            {
                var size = 6 + _rng.NextDouble() * 4; // 6-10px
                var colorIndex = _rng.Next(ConfettiBrushes.Length);

                _particles[i] = new ParticleData
                {
                    X = _rng.NextDouble() * width,
                    Y = -10 - _rng.NextDouble() * 60, // Oberhalb des Views starten
                    VelocityX = (_rng.NextDouble() - 0.5) * 120, // Horizontale Streuung
                    VelocityY = 80 + _rng.NextDouble() * 160, // Nach unten fallend
                    Size = size,
                    Rotation = _rng.NextDouble() * 360,
                    RotationSpeed = (_rng.NextDouble() - 0.5) * 720, // Grad/Sek
                    ColorIndex = colorIndex,
                    PhaseOffset = _rng.NextDouble() * Math.PI * 2, // Fuer sin-Schwankung
                    Active = true
                };

                var border = _particleControls[i];
                border.Width = size;
                border.Height = size;
                border.Background = ConfettiBrushes[colorIndex];
                border.IsVisible = true;
                border.Opacity = 1.0;
                SetLeft(border, _particles[i].X);
                SetTop(border, _particles[i].Y);
            }
            else
            {
                _particles[i].Active = false;
                _particleControls[i].IsVisible = false;
            }
        }

        _animationStart = DateTime.UtcNow;
        _isAnimating = true;
        IsVisible = true;

        // ~60fps Timer fuer Animation
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick -= OnTimerTick;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _animationStart).TotalSeconds;
        var dt = 0.016; // ~16ms pro Frame
        var height = Bounds.Height > 0 ? Bounds.Height : 800;

        if (elapsed >= AnimationDuration)
        {
            StopAnimation();
            return;
        }

        // Fortschritt fuer Fade-Out (letzte 30% der Animation)
        var fadeStart = AnimationDuration * 0.7;
        var globalOpacity = elapsed > fadeStart
            ? 1.0 - (elapsed - fadeStart) / (AnimationDuration - fadeStart)
            : 1.0;

        var anyActive = false;

        for (int i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.Active) continue;

            // Schwerkraft (beschleunigt nach unten)
            p.VelocityY += 180 * dt;

            // Horizontale sin-Schwankung
            var sway = Math.Sin(elapsed * 3.0 + p.PhaseOffset) * 30 * dt;

            // Position aktualisieren
            p.X += (p.VelocityX * dt) + sway;
            p.Y += p.VelocityY * dt;

            // Rotation aktualisieren
            p.Rotation += p.RotationSpeed * dt;

            // Partikel deaktivieren wenn ausserhalb des Views
            if (p.Y > height + 20)
            {
                p.Active = false;
                _particleControls[i].IsVisible = false;
                continue;
            }

            anyActive = true;

            // Control aktualisieren
            var border = _particleControls[i];
            SetLeft(border, p.X);
            SetTop(border, p.Y);
            border.Opacity = Math.Clamp(globalOpacity, 0, 1);
            border.RenderTransform = new RotateTransform(p.Rotation);
        }

        // Alle Partikel ausserhalb → Animation beenden
        if (!anyActive)
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        _timer?.Stop();
        _isAnimating = false;

        // Alle Partikel verstecken
        for (int i = 0; i < MaxParticles; i++)
        {
            _particles[i].Active = false;
            _particleControls[i].IsVisible = false;
        }

        IsVisible = false;
    }

    /// <summary>
    /// Partikel-Daten (Struct, kein Heap-Allokation pro Partikel).
    /// </summary>
    private struct ParticleData
    {
        public double X;
        public double Y;
        public double VelocityX;
        public double VelocityY;
        public double Size;
        public double Rotation;
        public double RotationSpeed;
        public int ColorIndex;
        public double PhaseOffset;
        public bool Active;
    }
}
