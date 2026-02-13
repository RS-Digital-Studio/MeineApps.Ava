using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Xaml.Interactivity;

namespace MeineApps.Core.Ava.Behaviors;

/// <summary>
/// Behavior that fades in a control when it becomes visible
/// </summary>
public class FadeInBehavior : Behavior<Control>
{
    /// <summary>
    /// Animation duration in milliseconds
    /// </summary>
    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<FadeInBehavior, int>(nameof(Duration), 300);

    /// <summary>
    /// Delay before animation starts in milliseconds
    /// </summary>
    public static readonly StyledProperty<int> DelayProperty =
        AvaloniaProperty.Register<FadeInBehavior, int>(nameof(Delay), 0);

    /// <summary>
    /// Whether to also translate from bottom
    /// </summary>
    public static readonly StyledProperty<bool> SlideFromBottomProperty =
        AvaloniaProperty.Register<FadeInBehavior, bool>(nameof(SlideFromBottom), false);

    /// <summary>
    /// Distance to slide from (in pixels)
    /// </summary>
    public static readonly StyledProperty<double> SlideDistanceProperty =
        AvaloniaProperty.Register<FadeInBehavior, double>(nameof(SlideDistance), 20);

    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public int Delay
    {
        get => GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    public bool SlideFromBottom
    {
        get => GetValue(SlideFromBottomProperty);
        set => SetValue(SlideFromBottomProperty, value);
    }

    public double SlideDistance
    {
        get => GetValue(SlideDistanceProperty);
        set => SetValue(SlideDistanceProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject == null) return;

        AssociatedObject.Opacity = 0;
        AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject == null) return;

        AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (AssociatedObject == null) return;

        try
        {
            if (Delay > 0)
                await Task.Delay(Delay);

            if (AssociatedObject == null) return;

            var animation = new Animation
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
        }
        catch
        {
            // Bei Fehler (z.B. Control detached) Element sichtbar machen
            if (AssociatedObject != null)
                AssociatedObject.Opacity = 1;
        }
    }
}
