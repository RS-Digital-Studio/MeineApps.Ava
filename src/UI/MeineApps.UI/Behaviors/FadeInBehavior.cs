using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Xaml.Interactivity;

namespace MeineApps.UI.Behaviors;

/// <summary>
/// Animiert ein Control beim Laden mit Fade-In und optionalem Slide von unten.
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
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
        base.OnDetaching();
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (AssociatedObject is null) return;

        try
        {
            // Verzögerung: Delay-Property oder minimal 16ms für Layout
            var delay = Delay > 0 ? Delay : 16;
            await Task.Delay(delay);

            if (AssociatedObject is null) return;

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
            AssociatedObject.Opacity = 1;

            if (SlideFromBottom && AssociatedObject.RenderTransform is TranslateTransform tt)
            {
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
}
