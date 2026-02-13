using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace MeineApps.UI.Controls;

/// <summary>
/// Runder Badge-Punkt fuer Benachrichtigungen auf Icons.
/// Count=0 → unsichtbar, Count>0 → Zahl, Count=-1 → nur Punkt.
/// Bounce-Animation bei Count-Aenderung.
/// </summary>
public class NotificationBadge : Border
{
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<NotificationBadge, int>(nameof(Count), 0);

    public static readonly StyledProperty<IBrush?> BadgeColorProperty =
        AvaloniaProperty.Register<NotificationBadge, IBrush?>(nameof(BadgeColor),
            new SolidColorBrush(Color.Parse("#DC2626")));

    /// <summary>
    /// Anzahl der Benachrichtigungen.
    /// 0 = unsichtbar, -1 = nur Punkt ohne Zahl, >0 = Zahl anzeigen.
    /// </summary>
    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <summary>Hintergrundfarbe des Badges (Default: Rot).</summary>
    public IBrush? BadgeColor
    {
        get => GetValue(BadgeColorProperty);
        set => SetValue(BadgeColorProperty, value);
    }

    private readonly TextBlock _label;
    private DispatcherTimer? _bounceTimer;

    static NotificationBadge()
    {
        CountProperty.Changed.AddClassHandler<NotificationBadge>(
            (ctrl, _) => ctrl.OnCountChanged());
        BadgeColorProperty.Changed.AddClassHandler<NotificationBadge>(
            (ctrl, _) => ctrl.UpdateAppearance());
    }

    public NotificationBadge()
    {
        MinWidth = 18;
        MinHeight = 18;
        CornerRadius = new CornerRadius(9);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        IsHitTestVisible = false;

        _label = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        Padding = new Thickness(4, 1);
        Child = _label;
        IsVisible = false;
        UpdateAppearance();
    }

    private void OnCountChanged()
    {
        var count = Count;
        if (count == 0)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;

        if (count == -1)
        {
            // Nur Punkt, keine Zahl
            _label.Text = "";
            MinWidth = 10;
            MinHeight = 10;
            CornerRadius = new CornerRadius(5);
            Padding = new Thickness(0);
        }
        else
        {
            _label.Text = count > 99 ? "99+" : count.ToString();
            MinWidth = 18;
            MinHeight = 18;
            CornerRadius = new CornerRadius(9);
            Padding = new Thickness(4, 1);
        }

        // Bounce-Animation
        PlayBounceAnimation();
    }

    private void UpdateAppearance()
    {
        Background = BadgeColor ?? new SolidColorBrush(Color.Parse("#DC2626"));
    }

    /// <summary>
    /// DispatcherTimer-basierte Bounce-Animation.
    /// NICHT Animation.RunAsync() verwenden - crasht mit ScaleTransform
    /// (InvalidCastException in TransformAnimator.Apply).
    /// </summary>
    private void PlayBounceAnimation()
    {
        // Laufende Animation abbrechen
        _bounceTimer?.Stop();

        var scaleTransform = new ScaleTransform(0, 0);
        RenderTransform = scaleTransform;

        // ~300ms bei ~60fps = 18 Frames
        const int totalFrames = 18;
        const int phase1End = 11; // 60% → Scale 0→1.2
        int frame = 0;

        _bounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _bounceTimer.Tick += (_, _) =>
        {
            frame++;
            double scale;

            if (frame <= phase1End)
            {
                // Phase 1: 0 → 1.2 (CubicEaseOut)
                double t = (double)frame / phase1End;
                t = 1.0 - Math.Pow(1.0 - t, 3);
                scale = t * 1.2;
            }
            else if (frame <= totalFrames)
            {
                // Phase 2: 1.2 → 1.0 (linear settle)
                double t = (double)(frame - phase1End) / (totalFrames - phase1End);
                scale = 1.2 - (0.2 * t);
            }
            else
            {
                scale = 1.0;
            }

            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;

            if (frame >= totalFrames)
            {
                _bounceTimer?.Stop();
                _bounceTimer = null;
                RenderTransform = null;
            }
        };
        _bounceTimer.Start();
    }
}
