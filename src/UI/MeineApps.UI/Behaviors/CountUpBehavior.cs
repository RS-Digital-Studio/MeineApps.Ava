using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace MeineApps.UI.Behaviors;

/// <summary>
/// Behavior das einen TextBlock-Wert von 0 zum Zielwert hochzählt (CountUp-Animation).
/// Wird an Ergebnis-TextBlocks in Rechner-Views gebunden.
/// Unterstützt Prefix (z.B. "+"), Suffix (z.B. " €") und CultureInfo (z.B. "de-DE").
/// </summary>
public class CountUpBehavior : Behavior<TextBlock>
{
    private DispatcherTimer? _timer;
    private double _currentValue;
    private double _targetValue;
    private int _frameCount;
    private int _currentFrame;
    private CultureInfo? _cultureInfoCache;

    public static readonly StyledProperty<double> TargetValueProperty =
        AvaloniaProperty.Register<CountUpBehavior, double>(nameof(TargetValue));

    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<CountUpBehavior, int>(nameof(Duration), 500);

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<CountUpBehavior, string>(nameof(Format), "F1");

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<CountUpBehavior, string>(nameof(Suffix), "");

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<CountUpBehavior, string>(nameof(Prefix), "");

    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<CountUpBehavior, string>(nameof(CultureName), "");

    public static readonly StyledProperty<bool> UseSignedPrefixProperty =
        AvaloniaProperty.Register<CountUpBehavior, bool>(nameof(UseSignedPrefix));

    /// <summary>Zielwert zu dem hochgezählt wird.</summary>
    public double TargetValue
    {
        get => GetValue(TargetValueProperty);
        set => SetValue(TargetValueProperty, value);
    }

    /// <summary>Animationsdauer in Millisekunden (Standard: 500).</summary>
    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Format-String für die Zahl (Standard: "F1").</summary>
    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Optionaler Suffix nach der Zahl (z.B. " kg").</summary>
    public string Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    /// <summary>Optionaler Prefix vor der Zahl (z.B. "+" für Vorzeichen).</summary>
    public string Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    /// <summary>
    /// CultureInfo-Name für Zahlenformatierung (z.B. "de-DE").
    /// Leer = InvariantCulture.
    /// </summary>
    public string CultureName
    {
        get => GetValue(CultureNameProperty);
        set => SetValue(CultureNameProperty, value);
    }

    /// <summary>
    /// Wenn true, wird automatisch "+" bei positiven und "-" bei negativen Werten vorangestellt.
    /// Überschreibt die Prefix-Property.
    /// </summary>
    public bool UseSignedPrefix
    {
        get => GetValue(UseSignedPrefixProperty);
        set => SetValue(UseSignedPrefixProperty, value);
    }

    private CultureInfo GetCulture()
    {
        if (_cultureInfoCache != null) return _cultureInfoCache;
        _cultureInfoCache = string.IsNullOrEmpty(CultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(CultureName);
        return _cultureInfoCache;
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        // TargetValue-Änderung überwachen
        var observable = this.GetObservable(TargetValueProperty);
        observable.Subscribe(new ValueObserver(this));
    }

    private class ValueObserver(CountUpBehavior behavior) : IObserver<double>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(double value) => behavior.OnTargetValueChanged(value);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        StopAnimation();
    }

    private string FormatValue(double value)
    {
        var prefix = UseSignedPrefix
            ? (value >= 0 ? "+" : "")  // Negatives Vorzeichen kommt vom ToString
            : Prefix;
        return prefix + value.ToString(Format, GetCulture()) + Suffix;
    }

    private void OnTargetValueChanged(double newValue)
    {
        if (AssociatedObject == null) return;

        // CultureInfo-Cache invalidieren bei Property-Änderung
        _cultureInfoCache = null;

        // Bei 0 oder NaN direkt setzen
        if (double.IsNaN(newValue) || newValue == 0)
        {
            AssociatedObject.Text = FormatValue(newValue);
            return;
        }

        StartAnimation(newValue);
    }

    private void StartAnimation(double target)
    {
        StopAnimation();

        _targetValue = target;
        _currentValue = 0;
        _frameCount = 30; // 30 Frames
        _currentFrame = 0;

        var intervalMs = Math.Max(16, Duration / _frameCount);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (AssociatedObject == null)
        {
            StopAnimation();
            return;
        }

        _currentFrame++;

        if (_currentFrame >= _frameCount)
        {
            // Letzter Frame → exakten Zielwert setzen
            AssociatedObject.Text = FormatValue(_targetValue);
            StopAnimation();
            return;
        }

        // EaseOut-Kurve: schnell am Anfang, langsam am Ende
        var t = (double)_currentFrame / _frameCount;
        var eased = 1 - Math.Pow(1 - t, 3); // CubicEaseOut
        _currentValue = _targetValue * eased;

        AssociatedObject.Text = FormatValue(_currentValue);
    }

    private void StopAnimation()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }
    }
}
