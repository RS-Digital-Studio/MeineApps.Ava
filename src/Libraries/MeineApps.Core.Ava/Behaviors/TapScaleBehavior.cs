using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace MeineApps.Core.Ava.Behaviors;

/// <summary>
/// Skaliert ein Control beim Drücken herunter mit Bounce-Effekt beim Loslassen.
/// Nutzt direkte Property-Setzung statt Animation API (TransformAnimator crasht auf ScaleTransform).
/// </summary>
public class TapScaleBehavior : Behavior<Control>
{
    public static readonly StyledProperty<double> PressedScaleProperty =
        AvaloniaProperty.Register<TapScaleBehavior, double>(nameof(PressedScale), 0.95);

    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<TapScaleBehavior, int>(nameof(Duration), 100);

    public double PressedScale
    {
        get => GetValue(PressedScaleProperty);
        set => SetValue(PressedScaleProperty, value);
    }

    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    private ScaleTransform? _scaleTransform;
    private DispatcherTimer? _animTimer;
    private double _animFrom;
    private int _animFrame;
    private int _totalFrames;
    private const int FrameIntervalMs = 16;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        _scaleTransform = new ScaleTransform(1, 1);
        AssociatedObject.RenderTransform = _scaleTransform;
        AssociatedObject.RenderTransformOrigin = RelativePoint.Center;

        AssociatedObject.PointerPressed += OnPointerPressed;
        AssociatedObject.PointerReleased += OnPointerReleased;
        AssociatedObject.PointerCaptureLost += OnPointerCaptureLost;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject == null) return;

        AssociatedObject.PointerPressed -= OnPointerPressed;
        AssociatedObject.PointerReleased -= OnPointerReleased;
        AssociatedObject.PointerCaptureLost -= OnPointerCaptureLost;
        StopAnimation();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        StopAnimation();
        SetScale(PressedScale);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => AnimateBack();
    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => AnimateBack();

    private void SetScale(double scale)
    {
        if (_scaleTransform == null) return;
        _scaleTransform.ScaleX = scale;
        _scaleTransform.ScaleY = scale;
    }

    /// <summary>Bounce-Animation zurück auf Scale 1.0 via DispatcherTimer.</summary>
    private void AnimateBack()
    {
        if (_scaleTransform == null) return;

        StopAnimation();
        _animFrom = _scaleTransform.ScaleX;
        _animFrame = 0;
        _totalFrames = Math.Max(1, Duration / FrameIntervalMs);

        if (Math.Abs(_animFrom - 1.0) < 0.001)
        {
            SetScale(1.0);
            return;
        }

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameIntervalMs) };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (_scaleTransform == null) { StopAnimation(); return; }

        _animFrame++;

        if (_animFrame >= _totalFrames)
        {
            SetScale(1.0);
            StopAnimation();
            return;
        }

        // ElasticEaseOut fuer Bounce-Effekt
        var t = (double)_animFrame / _totalFrames;
        var eased = ElasticEaseOut(t);
        var scale = _animFrom + (1.0 - _animFrom) * eased;
        SetScale(scale);
    }

    /// <summary>Elastische Ease-Out Kurve fuer natuerlichen Bounce.</summary>
    private static double ElasticEaseOut(double t)
    {
        if (t is <= 0 or >= 1) return t;
        const double c4 = 2 * Math.PI / 3;
        return Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
    }

    private void StopAnimation()
    {
        if (_animTimer == null) return;
        _animTimer.Stop();
        _animTimer.Tick -= OnAnimTick;
        _animTimer = null;
    }
}
