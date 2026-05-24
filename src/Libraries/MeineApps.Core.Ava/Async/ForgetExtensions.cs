using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MeineApps.Core.Ava.Async;

/// <summary>
/// Helfer für Fire-and-Forget-Tasks mit einheitlicher Fehlerbehandlung.
/// Ersetzt das Boilerplate-Muster aus 13+ Stellen in WorkTimePro
/// (<c>_ = SomeAsync().ContinueWith(..., OnlyOnFaulted)</c>) durch eine zentrale API.
/// </summary>
/// <remarks>
/// <para>Verwendung:</para>
/// <code>
/// // Statt:
/// _ = SaveAsync().ContinueWith(t => Debug.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
///
/// // Lieber:
/// SaveAsync().Forget();
/// SaveAsync().Forget(ex => MessageRequested?.Invoke("Fehler", ex.Message));
/// </code>
/// </remarks>
public static class ForgetExtensions
{
    /// <summary>
    /// Startet einen Task „verloren" — Exceptions werden nicht verschluckt, sondern via
    /// <paramref name="onError"/> oder per <see cref="Debug.WriteLine(string)"/> gemeldet.
    /// Liefert sofort zurück, ohne den aufrufenden Thread zu blockieren.
    /// </summary>
    /// <param name="task">Der zu vergessende Task.</param>
    /// <param name="onError">Optionaler Exception-Handler. Wenn null, wird per Debug.WriteLine geloggt.</param>
    /// <param name="caller">Wird vom Compiler gefüllt — landet in der Debug-Meldung.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Forget(this Task task, Action<Exception>? onError = null,
        [CallerMemberName] string caller = "")
    {
        if (task == null) return;

        // ContinueWith mit OnlyOnFaulted + ExecuteSynchronously: minimaler Overhead,
        // keine zusätzlichen Task-Allokationen wenn kein Fehler auftritt.
        task.ContinueWith(
            static (t, state) =>
            {
                var tuple = ((Action<Exception>? onError, string caller))state!;
                var ex = t.Exception?.Flatten().InnerException ?? t.Exception;
                if (ex == null) return;

                if (tuple.onError != null)
                {
                    try { tuple.onError(ex); }
                    catch (Exception handlerEx)
                    {
                        Debug.WriteLine($"[Forget:{tuple.caller}] onError handler threw: {handlerEx}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[Forget:{tuple.caller}] Unhandled task exception: {ex}");
                }
            },
            (onError, caller),
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// Erstellt einen verlorenen Task aus einer Async-Methode. Convenience-Wrapper für
    /// Eventhandler die als <c>async void</c> deklariert wären — hier wird der Task
    /// kontrolliert verschluckt mit einheitlichem Logging.
    /// </summary>
    /// <param name="work">Die auszuführende Async-Methode.</param>
    /// <param name="onError">Optionaler Exception-Handler.</param>
    /// <param name="caller">Wird vom Compiler gefüllt.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RunForget(Func<Task> work, Action<Exception>? onError = null,
        [CallerMemberName] string caller = "")
    {
        if (work == null) return;
        try
        {
            work().Forget(onError, caller);
        }
        catch (Exception ex)
        {
            // work() selbst kann synchron werfen (vor dem ersten await)
            if (onError != null)
            {
                try { onError(ex); }
                catch (Exception handlerEx)
                {
                    Debug.WriteLine($"[RunForget:{caller}] onError handler threw: {handlerEx}");
                }
            }
            else
            {
                Debug.WriteLine($"[RunForget:{caller}] Sync exception in work: {ex}");
            }
        }
    }
}
