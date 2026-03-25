using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace MeineApps.UI.Behaviors;

/// <summary>
/// Animiert ein Control beim Laden mit Fade-In und optionalem Slide von unten.
/// Slide-Animation wird per DispatcherTimer interpoliert (CubicEaseOut),
/// da Avalonia KeyFrame-Animationen auf TranslateTransform.Y nicht direkt funktionieren.
/// </summary>
public class FadeInBehavior : Behavior<Control>
{
    public static readonly StyledProperty<int> DelayProperty =
        AvaloniaProperty.Register<FadeInBehavior, int>(nameof(Delay), 0);

    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<FadeInBehavior, int>(nameof(Duration), 250);

    public static readonly StyledProperty<bool> SlideFromBottomProperty =
        AvaloniaProperty.Register<FadeInBehavior, bool>(nameof(SlideFromBottom), false);

    public static readonly StyledProperty<double> SlideDistanceProperty =
        AvaloniaProperty.Register<FadeInBehavior, double>(nameof(SlideDistance), 12);

    /// <summary>Verzögerung in Millisekunden bevor die Animation startet (für Stagger-Effekt).</summary>
    public int Delay
    {
        get => GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    /// <summary>Animationsdauer in Millisekunden.</summary>
    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Ob das Element von unten einsliden soll.</summary>
    public bool SlideFromBottom
    {
        get => GetValue(SlideFromBottomProperty);
        set => SetValue(SlideFromBottomProperty, value);
    }

    /// <summary>Slide-Distanz in Pixeln.</summary>
    public double SlideDistance
    {
        get => GetValue(SlideDistanceProperty);
        set => SetValue(SlideDistanceProperty, value);
    }

    // Slide-Animation State
    private DispatcherTimer? _slideTimer;
    private int _slideFrame;
    private int _slideTotalFrames;
    private double _slideFrom;
    private const int FrameIntervalMs = 16;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is null) return;

        AssociatedObject.Opacity = 0;

        if (SlideFromBottom)
        {
            AssociatedObject.RenderTransform = new TranslateTransform(0, SlideDistance);
        }

        AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;

        // Fallback: Falls Control bereits im Visual Tree ist, wird AttachedToVisualTree
        // nicht mehr gefeuert → Animation direkt starten
        if (AssociatedObject.GetVisualRoot() != null)
        {
            _ = RunFadeInAsync();
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
            // Sicherstellen dass Element sichtbar bleibt
            AssociatedObject.Opacity = 1;
            if (SlideFromBottom && AssociatedObject.RenderTransform is TranslateTransform tt)
                tt.Y = 0;
        }
        StopSlideAnimation();
        base.OnDetaching();
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        await RunFadeInAsync();
    }

    /// <summary>
    /// Führt die Fade-In-Animation aus und stellt sicher, dass Opacity am Ende IMMER 1 ist.
    /// Slide-Animation läuft parallel per DispatcherTimer mit CubicEaseOut.
    /// </summary>
    private async Task RunFadeInAsync()
    {
        if (AssociatedObject is null) return;

        try
        {
            // Verzögerung: Delay-Property oder minimal 16ms für Layout
            var delay = Delay > 0 ? Delay : 16;
            await Task.Delay(delay);

            if (AssociatedObject is null) return;

            // Slide-Animation parallel starten (per DispatcherTimer)
            if (SlideFromBottom && AssociatedObject.RenderTransform is TranslateTransform)
            {
                StartSlideAnimation();
            }

            // Opacity-Animation per KeyFrame (funktioniert zuverlässig)
            var animation = new Avalonia.Animation.Animation
            {
                Duration = TimeSpan.FromMilliseconds(Duration),
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters = { new Setter(Visual.OpacityProperty, 0.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    }
                }
            };

            await animation.RunAsync(AssociatedObject);

            // Sicherheits-Fallback: Opacity IMMER auf 1 setzen nach Animation
            if (AssociatedObject is not null)
            {
                AssociatedObject.Opacity = 1;
                if (SlideFromBottom && AssociatedObject.RenderTransform is TranslateTransform tt)
                    tt.Y = 0;
            }
        }
        catch
        {
            // Bei Fehler (z.B. Control detached) Element sichtbar machen
            if (AssociatedObject is not null)
            {
                AssociatedObject.Opacity = 1;
                if (SlideFromBottom && AssociatedObject.RenderTransform is TranslateTransform tt)
                    tt.Y = 0;
            }
        }
    }

    /// <summary>
    /// Startet die Slide-Animation per DispatcherTimer (TranslateTransform.Y interpolieren).
    /// </summary>
    private void StartSlideAnimation()
    {
        StopSlideAnimation();
        _slideFrom = SlideDistance;
        _slideFrame = 0;
        _slideTotalFrames = Math.Max(1, Duration / FrameIntervalMs);

        _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameIntervalMs) };
        _slideTimer.Tick += OnSlideTick;
        _slideTimer.Start();
    }

    private void OnSlideTick(object? sender, EventArgs e)
    {
        if (AssociatedObject?.RenderTransform is not TranslateTransform tt)
        {
            StopSlideAnimation();
            return;
        }

        _slideFrame++;

        if (_slideFrame >= _slideTotalFrames)
        {
            tt.Y = 0;
            StopSlideAnimation();
            return;
        }

        // CubicEaseOut: 1 - (1 - t)^3
        var t = (double)_slideFrame / _slideTotalFrames;
        var eased = 1.0 - Math.Pow(1.0 - t, 3);
        tt.Y = _slideFrom * (1.0 - eased);
    }

    private void StopSlideAnimation()
    {
        if (_slideTimer == null) return;
        _slideTimer.Stop();
        _slideTimer.Tick -= OnSlideTick;
        _slideTimer = null;
    }
}
