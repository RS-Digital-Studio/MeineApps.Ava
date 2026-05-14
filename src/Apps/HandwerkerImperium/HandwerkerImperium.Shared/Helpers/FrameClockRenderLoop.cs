using System;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// (12.05.2026): Wiederverwendbare Render-Loop-Subscription auf den zentralen
/// <see cref="IFrameClock"/>. Ersetzt das gleiche DispatcherTimer-Pattern in 12+ MiniGame-
/// und Renderer-Views.
///
/// Pattern:
/// <code>
/// private readonly FrameClockRenderLoop _renderLoop;
/// public XyzView() {
///     _renderLoop = new FrameClockRenderLoop(() => _canvas?.InvalidateSurface(), FpsProfile.MiniGame());
/// }
/// // Im IsVisible-Handler:
/// if (IsVisible) _renderLoop.Start(); else _renderLoop.Stop();
/// // Statt "_renderTimer != null":
/// if (_renderLoop.IsActive) ...
/// </code>
///
/// Lazy-Init des FrameClock-Service via App.Services — kein Constructor-Injection erforderlich
/// (View-Layer-Konvention).
/// </summary>
public sealed class FrameClockRenderLoop : IDisposable
{
    private readonly IFrameClock? _clock;
    private readonly EventHandler<FrameTickEventArgs> _handler;
    private TimeSpan _interval;
    private bool _active;
    private bool _disposed;

    /// <summary>True wenn die Subscription aktiv ist (Renderer wird vom FrameClock invalidiert).</summary>
    public bool IsActive => _active;

    /// <summary>Aktuelles Render-Intervall.</summary>
    public TimeSpan Interval => _interval;

    public FrameClockRenderLoop(Action invalidate, TimeSpan interval)
    {
        if (invalidate == null) throw new ArgumentNullException(nameof(invalidate));
        _clock = HandwerkerImperium.App.Services?.GetService(typeof(IFrameClock)) as IFrameClock;
        _interval = interval;
        _handler = (_, _) => invalidate();
    }

    /// <summary>Startet die Subscription (idempotent — doppelter Aufruf ist Noop).</summary>
    public void Start()
    {
        if (_disposed || _active) return;
        _clock?.Subscribe(_handler, _interval);
        _active = true;
    }

    /// <summary>Stoppt die Subscription (idempotent).</summary>
    public void Stop()
    {
        if (_disposed || !_active) return;
        _clock?.Unsubscribe(_handler);
        _active = false;
    }

    /// <summary>
    /// Aendert das Render-Intervall zur Laufzeit (z.B. Dashboard Idle 10Hz ↔ Active 30Hz).
    /// </summary>
    public void SetInterval(TimeSpan interval)
    {
        _interval = interval;
        if (_active) _clock?.UpdateInterval(_handler, interval);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_active)
        {
            _clock?.Unsubscribe(_handler);
            _active = false;
        }
    }
}
