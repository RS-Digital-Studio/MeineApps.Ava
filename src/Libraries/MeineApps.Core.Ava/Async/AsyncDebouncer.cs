using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MeineApps.Core.Ava.Async;

/// <summary>
/// Saubere Debounce-Klasse für asynchrone Aktionen.
/// Cancellt vorherige Trigger und führt die Aktion erst nach der eingestellten
/// Wartezeit aus. Bei erneutem Trigger wird der Timer zurückgesetzt.
/// </summary>
/// <remarks>
/// <para>Verwendung:</para>
/// <code>
/// private readonly AsyncDebouncer _saveDebouncer = new(TimeSpan.FromMilliseconds(800));
///
/// partial void OnTextChanged(string value)
/// {
///     _saveDebouncer.Trigger(async ct =>
///     {
///         await SaveAsync(value, ct);
///     });
/// }
///
/// // Bei Dispose:
/// _saveDebouncer.Dispose();
/// </code>
/// <para>
/// Das übergebene CancellationToken wird durchgereicht an die Aktion — kommt während
/// der Action ein neuer Trigger, kann die Action darüber abbrechen.
/// </para>
/// <para>
/// Thread-safe für Trigger-Aufrufe (z.B. aus UI- und Background-Thread gemischt).
/// </para>
/// </summary>
public sealed class AsyncDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _paused;

    /// <param name="delay">Wartezeit nach dem letzten Trigger bis zur Ausführung.</param>
    public AsyncDebouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    /// <summary>
    /// Gibt true zurück solange ein Trigger aktiv läuft (Wartezeit + Aktion).
    /// </summary>
    public bool IsPending
    {
        get { lock (_lock) return _cts != null; }
    }

    /// <summary>
    /// Triggert die Aktion. Cancellt eventuell laufende vorherige Trigger.
    /// </summary>
    /// <param name="action">Die nach <see cref="_delay"/> auszuführende Aktion.</param>
    public void Trigger(Func<CancellationToken, Task> action)
    {
        if (action == null) return;

        CancellationTokenSource cts;
        lock (_lock)
        {
            if (_disposed || _paused) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_delay, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { return; }
            catch (OperationCanceledException) { return; }

            if (token.IsCancellationRequested) return;

            try
            {
                await action(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* normaler Abbruch */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"AsyncDebouncer action failed: {ex}");
            }
            finally
            {
                lock (_lock)
                {
                    // Nur freigeben wenn unsere CTS noch die aktuelle ist
                    if (ReferenceEquals(_cts, cts))
                    {
                        _cts.Dispose();
                        _cts = null;
                    }
                }
            }
        });
    }

    /// <summary>
    /// Cancelt einen laufenden Trigger (falls vorhanden) ohne ihn auszuführen.
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Pausiert den Debouncer deterministisch. Solange das zurückgegebene Token
    /// nicht disposed ist, werden <see cref="Trigger"/>-Aufrufe ignoriert.
    /// Ersetzt das fehleranfällige <c>bool _suppress</c>-Flag.
    /// </summary>
    public IDisposable Pause()
    {
        lock (_lock)
        {
            _paused = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        return new Resume(this);
    }

    private sealed class Resume : IDisposable
    {
        private AsyncDebouncer? _owner;
        public Resume(AsyncDebouncer owner) => _owner = owner;
        public void Dispose()
        {
            var o = Interlocked.Exchange(ref _owner, null);
            if (o == null) return;
            lock (o._lock)
            {
                o._paused = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
