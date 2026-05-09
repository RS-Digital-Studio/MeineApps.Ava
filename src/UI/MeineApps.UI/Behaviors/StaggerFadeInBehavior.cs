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
/// Behavior das Elemente mit versetztem Delay einblendet.
/// Erkennt automatisch den Index im übergeordneten ItemsControl/Panel
/// und berechnet Delay = Index × StaggerDelay.
/// Verwendet DispatcherTimer statt Animation API für Robustheit
/// bei IsVisible-Wechseln durch Data-Binding.
/// </summary>
public class StaggerFadeInBehavior : Behavior<Control>
{
    public static readonly StyledProperty<int> StaggerDelayProperty =
        AvaloniaProperty.Register<StaggerFadeInBehavior, int>(nameof(StaggerDelay), 50);

    public static readonly StyledProperty<int> BaseDurationProperty =
        AvaloniaProperty.Register<StaggerFadeInBehavior, int>(nameof(BaseDuration), 300);

    public static readonly StyledProperty<int> FixedIndexProperty =
        AvaloniaProperty.Register<StaggerFadeInBehavior, int>(nameof(FixedIndex), -1);

    private bool _hasAnimated;
    private DispatcherTimer? _animTimer;
    private double _animProgress;
    private DateTime _animStart;
    private int _animDuration;

    /// <summary>Verzögerung pro Element in Millisekunden (Standard: 50).</summary>
    public int StaggerDelay
    {
        get => GetValue(StaggerDelayProperty);
        set => SetValue(StaggerDelayProperty, value);
    }

    /// <summary>Animationsdauer in Millisekunden (Standard: 300).</summary>
    public int BaseDuration
    {
        get => GetValue(BaseDurationProperty);
        set => SetValue(BaseDurationProperty, value);
    }

    /// <summary>
    /// Fester Index statt Auto-Erkennung (-1 = automatisch).
    /// Nützlich wenn das Element nicht in einem ItemsControl ist.
    /// </summary>
    public int FixedIndex
    {
        get => GetValue(FixedIndexProperty);
        set => SetValue(FixedIndexProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        // Opacity erst auf 0 setzen wenn Element tatsächlich sichtbar wird
        AssociatedObject.Opacity = 0;
        AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
        AssociatedObject.DetachedFromVisualTree += OnDetachedFromVisualTree;

        // Falls bereits im Visual Tree → Animation direkt starten
        if (TopLevel.GetTopLevel(AssociatedObject) != null)
        {
            StartFadeIn();
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject == null) return;

        StopTimer();
        AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
        AssociatedObject.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        AssociatedObject.Opacity = 1;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_hasAnimated)
        {
            // Animation war schon fertig → sofort sichtbar machen
            EnsureVisible();
            return;
        }

        StartFadeIn();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Element wurde aus Visual Tree entfernt (IsVisible=false)
        // Timer stoppen, aber _hasAnimated NICHT zurücksetzen
        StopTimer();

        // Wenn Animation noch nie fertig lief, Opacity=0 beibehalten
        // damit beim nächsten Attach die Animation starten kann
        if (!_hasAnimated && AssociatedObject != null)
        {
            AssociatedObject.Opacity = 0;
        }
    }

    private void StartFadeIn()
    {
        if (_hasAnimated || AssociatedObject == null) return;

        var index = FixedIndex >= 0 ? FixedIndex : DetectIndex();
        var delay = index * StaggerDelay;
        _animDuration = BaseDuration;

        // TranslateTransform setzen falls noch nicht vorhanden
        AssociatedObject.RenderTransform ??= new TranslateTransform();
        AssociatedObject.Opacity = 0;
        if (AssociatedObject.RenderTransform is TranslateTransform tt)
            tt.Y = 15;

        if (delay > 0)
        {
            // Verzögerten Start per Timer
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            delayTimer.Tick += (_, _) =>
            {
                delayTimer.Stop();
                BeginAnimation();
            };
            delayTimer.Start();
        }
        else
        {
            BeginAnimation();
        }
    }

    private void BeginAnimation()
    {
        if (AssociatedObject == null || _hasAnimated)
        {
            EnsureVisible();
            return;
        }

        _animStart = DateTime.UtcNow;
        _animProgress = 0;

        StopTimer();
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (AssociatedObject == null)
        {
            StopTimer();
            _hasAnimated = true;
            return;
        }

        var elapsed = (DateTime.UtcNow - _animStart).TotalMilliseconds;
        _animProgress = Math.Min(elapsed / _animDuration, 1.0);

        // CubicEaseOut: t = 1 - (1-t)^3
        var eased = 1 - Math.Pow(1 - _animProgress, 3);

        AssociatedObject.Opacity = eased;
        if (AssociatedObject.RenderTransform is TranslateTransform tt)
            tt.Y = 15 * (1 - eased);

        if (_animProgress >= 1.0)
        {
            StopTimer();
            _hasAnimated = true;
            EnsureVisible();
        }
    }

    private void StopTimer()
    {
        if (_animTimer != null)
        {
            _animTimer.Stop();
            _animTimer.Tick -= OnAnimTick;
            _animTimer = null;
        }
    }

    private void EnsureVisible()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.Opacity = 1;
            if (AssociatedObject.RenderTransform is TranslateTransform tt)
                tt.Y = 0;
        }
    }

    private int DetectIndex()
    {
        if (AssociatedObject?.Parent is Panel panel)
        {
            return panel.Children.IndexOf(AssociatedObject);
        }
        return 0;
    }
}
