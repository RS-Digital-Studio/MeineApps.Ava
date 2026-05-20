using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace MeineApps.UI.Behaviors;

/// <summary>
/// Loest <see cref="Command"/> aus, wenn der Spieler den Pointer fuer mindestens
/// <see cref="DurationMs"/> Millisekunden auf dem Control haelt (Default 500 ms).
/// Bei kuerzerem Tap wird KEIN Command gefeuert — das normale Click-Verhalten des
/// Controls bleibt unberuehrt (z. B. ein Button kann zusaetzlich Click+Long-Press haben).
///
/// Verwendung:
/// <code>
/// &lt;Button Command="{Binding StartCommand}"&gt;
///   &lt;i:Interaction.Behaviors&gt;
///     &lt;behaviors:LongPressBehavior Command="{Binding PinCommand}" CommandParameter="..." /&gt;
///   &lt;/i:Interaction.Behaviors&gt;
/// &lt;/Button&gt;
/// </code>
///
/// Wenn der Long-Press triggert, wird das nachfolgende <see cref="Control.PointerReleased"/>
/// als <c>Handled = true</c> markiert, damit der normale Click NICHT zusaetzlich feuert.
/// </summary>
public class LongPressBehavior : Behavior<Control>
{
    public static readonly StyledProperty<int> DurationMsProperty =
        AvaloniaProperty.Register<LongPressBehavior, int>(nameof(DurationMs), 500);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<LongPressBehavior, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<LongPressBehavior, object?>(nameof(CommandParameter));

    /// <summary>Haltedauer in Millisekunden bis der Long-Press triggert.</summary>
    public int DurationMs
    {
        get => GetValue(DurationMsProperty);
        set => SetValue(DurationMsProperty, value);
    }

    /// <summary>Command das beim Long-Press ausgefuehrt wird.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Parameter, mit dem das Command aufgerufen wird.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private DispatcherTimer? _timer;
    private bool _triggered;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is null) return;

        AssociatedObject.PointerPressed += OnPointerPressed;
        AssociatedObject.PointerReleased += OnPointerReleased;
        AssociatedObject.PointerCaptureLost += OnPointerCaptureLost;
        AssociatedObject.PointerExited += OnPointerExited;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.PointerPressed -= OnPointerPressed;
            AssociatedObject.PointerReleased -= OnPointerReleased;
            AssociatedObject.PointerCaptureLost -= OnPointerCaptureLost;
            AssociatedObject.PointerExited -= OnPointerExited;
        }
        StopTimer();
        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _triggered = false;
        StopTimer();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, DurationMs)) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        StopTimer();
        _triggered = true;
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopTimer();
        // Wenn Long-Press getriggert hat, normalen Click unterdruecken — sonst wuerde
        // Button.Click zusaetzlich feuern und z. B. den Auftrag starten obwohl der Spieler
        // nur die Strategie pinnen wollte.
        if (_triggered)
        {
            e.Handled = true;
            _triggered = false;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => StopTimer();
    private void OnPointerExited(object? sender, PointerEventArgs e) => StopTimer();

    private void StopTimer()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }
}
