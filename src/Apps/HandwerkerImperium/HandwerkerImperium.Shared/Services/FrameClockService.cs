using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// IFrameClock-Implementation auf Basis eines einzelnen DispatcherTimer.
///
/// Interner Tick laeuft mit 33ms (~30Hz, FpsProfile.DashboardActive). Pro Subscriber wird
/// das eigene Intervall getrackt — der Handler wird nur aufgerufen wenn das Intervall seit
/// dem letzten Aufruf verstrichen ist. So koennen 5fps-Subscriber und 30fps-Subscriber sich
/// denselben Tick teilen.
///
/// Auto-Stop bei 0 Subscribern, Pause/Resume fuer App-Lifecycle.
/// </summary>
public sealed class FrameClockService : IFrameClock, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private readonly List<SubscriberEntry> _subscribers = new();

    // Wiederverwendbarer Snapshot der Subscriber-Liste fuer lock-freies Iterieren im Tick.
    // Frueher wurde pro Tick (30Hz) ein neues Array via ToArray() allokiert — groesster
    // app-weiter GC-Posten. Jetzt nur bei Subscribe/Unsubscribe/UpdateInterval neu befuellt
    // (Dirty-Flag). Da SubscriberEntry eine Referenz-Klasse ist, teilen Liste und Snapshot
    // dieselben Objekte → LastTickSeconds-Mutation im Tick wirkt direkt, kein Rueck-Schreiben.
    private SubscriberEntry[] _snapshot = [];
    private bool _snapshotDirty = true;
    private bool _isPaused;
    private bool _disposed;

    private static readonly TimeSpan DefaultInterval = FpsProfile.DashboardActive(); // ~30Hz

    public int SubscriberCount
    {
        get
        {
            lock (_subscribers) return _subscribers.Count;
        }
    }

    public bool IsRunning => _timer.IsEnabled && !_isPaused;

    public FrameClockService()
    {
        _timer = new DispatcherTimer
        {
            // Interner Master-Tick: 30Hz reicht fuer alle Use-Cases. Subscriber mit
            // niedrigerer Frequenz (z.B. 5Hz Idle) bekommen ihre Ticks gefiltert.
            Interval = FpsProfile.DashboardActive()
        };
        _timer.Tick += OnTimerTick;
    }

    public void Subscribe(EventHandler<FrameTickEventArgs> handler, TimeSpan? interval = null)
    {
        if (_disposed || handler == null) return;

        lock (_subscribers)
        {
            // Idempotenz: doppeltes Subscribe verhindern.
            foreach (var existing in _subscribers)
            {
                if (existing.Handler == handler) return;
            }

            _subscribers.Add(new SubscriberEntry
            {
                Handler = handler,
                Interval = interval ?? DefaultInterval,
                LastTickSeconds = -1f, // Erste Tick fuert sofort
            });
            _snapshotDirty = true;

            if (_subscribers.Count == 1 && !_isPaused)
            {
                _stopwatch.Restart();
                _timer.Start();
            }
        }
    }

    public void Unsubscribe(EventHandler<FrameTickEventArgs> handler)
    {
        if (_disposed || handler == null) return;

        lock (_subscribers)
        {
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (_subscribers[i].Handler == handler)
                {
                    _subscribers.RemoveAt(i);
                    _snapshotDirty = true;
                    break;
                }
            }

            if (_subscribers.Count == 0)
            {
                _timer.Stop();
                _stopwatch.Stop();
            }
        }
    }

    public void UpdateInterval(EventHandler<FrameTickEventArgs> handler, TimeSpan interval)
    {
        if (_disposed || handler == null) return;

        lock (_subscribers)
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                if (_subscribers[i].Handler == handler)
                {
                    // Referenz-Klasse: direkte Property-Mutation, kein Copy-Back noetig.
                    _subscribers[i].Interval = interval;
                    return;
                }
            }
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
        lock (_subscribers)
        {
            if (_subscribers.Count > 0)
            {
                _stopwatch.Start();
                _timer.Start();
            }
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var nowSeconds = (float)_stopwatch.Elapsed.TotalSeconds;

        // Snapshot der Subscriber-Liste (lock-frei iterieren) — Subscribe/Unsubscribe
        // waehrend Iteration ist OK, Aenderungen wirken naechsten Tick.
        // Das Array wird nur bei Aenderung neu befuellt (kein ToArray() pro Tick).
        SubscriberEntry[] snapshot;
        lock (_subscribers)
        {
            if (_snapshotDirty)
            {
                if (_snapshot.Length != _subscribers.Count)
                    _snapshot = new SubscriberEntry[_subscribers.Count];
                _subscribers.CopyTo(_snapshot);
                _snapshotDirty = false;
            }
            snapshot = _snapshot;
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            var entry = snapshot[i];
            var intervalSeconds = (float)entry.Interval.TotalSeconds;

            // Erster Tick fuer diesen Subscriber: sofort feuern (LastTickSeconds=-1)
            if (entry.LastTickSeconds < 0f)
            {
                entry.Args.DeltaSeconds = 0f;
                entry.Args.ElapsedSeconds = nowSeconds;
                entry.Handler.Invoke(this, entry.Args);
                // Referenz-Klasse: Liste + Snapshot teilen das Objekt → direkte Mutation reicht.
                entry.LastTickSeconds = nowSeconds;
                continue;
            }

            var elapsedSinceLast = nowSeconds - entry.LastTickSeconds;
            if (elapsedSinceLast >= intervalSeconds)
            {
                entry.Args.DeltaSeconds = elapsedSinceLast;
                entry.Args.ElapsedSeconds = nowSeconds;
                entry.Handler.Invoke(this, entry.Args);
                entry.LastTickSeconds = nowSeconds;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _stopwatch.Stop();
        lock (_subscribers)
        {
            _subscribers.Clear();
            _snapshot = [];
            _snapshotDirty = true;
        }
    }

    /// <summary>
    /// Interner Subscriber-Eintrag. Referenz-Klasse (nicht Struct), damit Liste und Snapshot-Array
    /// dasselbe Objekt teilen — so wirkt die LastTickSeconds-Mutation im Tick ohne Rueck-Schreiben.
    /// </summary>
    private sealed class SubscriberEntry
    {
        public required EventHandler<FrameTickEventArgs> Handler;
        public TimeSpan Interval;
        public float LastTickSeconds;

        // Pro Subscriber genau EINE wiederverwendete EventArgs-Instanz (statt pro Tick eine neue).
        // Felder werden vor jedem Invoke mutiert. Sicher, weil der Tick synchron laeuft und kein
        // Consumer die Instanz ueber den Handler-Aufruf hinaus festhaelt.
        public readonly FrameTickEventArgs Args = new(0f, 0f);
    }
}
