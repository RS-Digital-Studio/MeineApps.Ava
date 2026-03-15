using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// Erweiterungsmethoden für sichere asynchrone Operationen.
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// Führt einen Task im Hintergrund aus und loggt Exceptions statt sie zu verschlucken.
    /// Nutzt CallerMemberName für bessere Fehlerlokalisierung im Debug-Output.
    /// </summary>
    public static void SafeFireAndForget(this Task task, [CallerMemberName] string? caller = null)
    {
        task.ContinueWith(t =>
        {
            var ex = t.Exception?.GetBaseException();
            if (ex != null)
            {
                Debug.WriteLine($"[HandwerkerImperium] Fire-and-forget Fehler in {caller}: {ex.Message}");
#if DEBUG
                Debug.WriteLine(ex.StackTrace);
#endif
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
                Debug.WriteLine(
                    $"[HandwerkerImperium] FireAndForget Fehler: {exception.GetType().Name}: {exception.Message}");
#if DEBUG
                Debug.WriteLine(exception.StackTrace);
#endif
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
