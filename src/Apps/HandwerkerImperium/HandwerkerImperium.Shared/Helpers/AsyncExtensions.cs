using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// Erweiterungsmethoden für sichere asynchrone Operationen.
/// Nutzt ILogService (falls gesetzt) für Release-sicheres Logging,
/// mit Console.WriteLine als Fallback.
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// Statische Logger-Referenz. Wird in App.axaml.cs nach DI-Aufbau gesetzt.
    /// </summary>
    internal static ILogService? Logger { get; set; }

    /// <summary>
    /// Führt einen Task im Hintergrund aus und loggt Exceptions statt sie zu verschlucken.
    /// Nutzt CallerMemberName für bessere Fehlerlokalisierung.
    /// </summary>
    public static void SafeFireAndForget(this Task task, [CallerMemberName] string? caller = null)
    {
        task.ContinueWith(t =>
        {
            var ex = t.Exception?.GetBaseException();
            if (ex != null)
            {
                var msg = $"Fire-and-forget Fehler in {caller}: {ex.Message}";
                if (Logger != null)
                    Logger.Error(msg, ex);
                else
                    Console.WriteLine($"[HandwerkerImperium] {msg}");
            }
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>
    /// Führt einen Task im Hintergrund aus und loggt Exceptions mit optionalem Callback.
    /// </summary>
    public static void FireAndForget(this Task task, Action<Exception>? onError = null)
    {
        task.ContinueWith(t =>
        {
            var exception = t.Exception?.Flatten().InnerException ?? t.Exception;
            if (exception != null)
            {
                var msg = $"FireAndForget Fehler: {exception.GetType().Name}: {exception.Message}";
                if (Logger != null)
                    Logger.Error(msg, exception);
                else
                    Console.WriteLine($"[HandwerkerImperium] {msg}");

                onError?.Invoke(exception);
            }
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>
    /// Führt einen ValueTask im Hintergrund aus und loggt Exceptions mit optionalem Callback.
    /// </summary>
    public static void FireAndForget(this ValueTask task, Action<Exception>? onError = null)
    {
        task.AsTask().FireAndForget(onError);
    }
}
