using System;
using System.Diagnostics;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// IFrameClock-Implementation auf Basis von DispatcherTimer.
/// 30 Hz Standard-Frequenz (FpsProfile.DashboardActive), Stopwatch-basierte Delta-Zeit.
/// Auto-Stop bei 0 Subscribern, Pause/Resume fuer App-Lifecycle.
/// </summary>
public sealed class FrameClockService : IFrameClock, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private event EventHandler<FrameTickEventArgs>? _frameTick;
    private float _elapsedSeconds;
    private float _lastTickSeconds;
    private bool _isPaused;
    private bool _disposed;
    private int _subscriberCount;

    public int SubscriberCount => _subscriberCount;

    public bool IsRunning => _timer.IsEnabled && !_isPaused;

    public FrameClockService()
    {
        _timer = new DispatcherTimer
        {
            Interval = FpsProfile.DashboardActive() // 30 Hz Standard
        };
        _timer.Tick += OnTimerTick;
    }

    public void Subscribe(EventHandler<FrameTickEventArgs> handler)
    {
        if (_disposed) return;

        // Idempotenz: doppeltes Subscribe verhindern.
        // Delegate.GetInvocationList ist zuverlaessig fuer Vergleich.
        if (_frameTick != null)
        {
            foreach (var existing in _frameTick.GetInvocationList())
            {
                if (existing == (Delegate)handler) return;
            }
        }

        _frameTick += handler;
        _subscriberCount++;

        if (_subscriberCount == 1 && !_isPaused)
        {
            _stopwatch.Restart();
            _elapsedSeconds = 0f;
            _lastTickSeconds = 0f;
            _timer.Start();
        }
    }

    public void Unsubscribe(EventHandler<FrameTickEventArgs> handler)
    {
        if (_disposed || _frameTick == null) return;

        bool wasSubscribed = false;
        foreach (var existing in _frameTick.GetInvocationList())
        {
            if (existing == (Delegate)handler)
            {
                wasSubscribed = true;
                break;
            }
        }
        if (!wasSubscribed) return;

        _frameTick -= handler;
        _subscriberCount = Math.Max(0, _subscriberCount - 1);

        if (_subscriberCount == 0)
        {
            _timer.Stop();
            _stopwatch.Stop();
        }
    }

    public void Pause()
    {
        if (_disposed || _isPaused) return;
        _isPaused = true;
        _timer.Stop();
        _stopwatch.Stop();
    }

    public void Resume()
    {
        if (_disposed || !_isPaused) return;
        _isPaused = false;
        if (_subscriberCount > 0)
        {
            _stopwatch.Start();
            _timer.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_frameTick == null) return;

        var nowSeconds = (float)_stopwatch.Elapsed.TotalSeconds;
        var delta = nowSeconds - _lastTickSeconds;
        _lastTickSeconds = nowSeconds;
        _elapsedSeconds = nowSeconds;

        var args = new FrameTickEventArgs(delta, _elapsedSeconds);
        _frameTick.Invoke(this, args);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _stopwatch.Stop();
        _frameTick = null;
    }
}
