namespace BomberBlast.ViewModels;

/// <summary>
/// Default-Implementation von <see cref="ILifecycleHub"/> (Welle 6 MainViewModel-Refactor).
///
/// <para>
/// Phase 1: Leeres Geruest. <c>CloudSaveInitTask</c> ist erstmal <c>Task.CompletedTask</c>
/// damit Subscriber waehrend der Migration nicht haengen bleiben. Die echte Init-Logik wird
/// in Phase 6 aus <see cref="MainViewModel"/> hier hin verschoben.
/// </para>
/// </summary>
public sealed class LifecycleHub : ILifecycleHub
{
    public Task CloudSaveInitTask => Task.CompletedTask;

    public event Action<string>? ExitHintRequested;

    public bool HandleBackPressed()
        => throw new NotImplementedException("Wird in Phase 6 gefuellt.");

    /// <summary>Helper damit Event-Subscriber waehrend der Migration nicht crashen.</summary>
    internal void RaiseExitHint(string message) => ExitHintRequested?.Invoke(message);
}
