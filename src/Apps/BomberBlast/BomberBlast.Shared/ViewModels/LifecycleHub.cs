namespace BomberBlast.ViewModels;

/// <summary>
/// Default-Implementation von <see cref="ILifecycleHub"/>.
///
/// <para>
/// Leeres Geruest. <c>CloudSaveInitTask</c> ist erstmal <c>Task.CompletedTask</c>
/// damit Subscriber waehrend der Migration nicht haengen bleiben. Die echte Init-Logik wird
/// noch aus <see cref="MainViewModel"/> hier hin verschoben.
/// </para>
/// </summary>
public sealed class LifecycleHub : ILifecycleHub
{
    public Task CloudSaveInitTask => Task.CompletedTask;

    public event Action<string>? ExitHintRequested;

    public bool HandleBackPressed()
        => throw new NotImplementedException("Migration aus MainViewModel ausstehend.");

    /// <summary>Helper damit Event-Subscriber waehrend der Migration nicht crashen.</summary>
    internal void RaiseExitHint(string message) => ExitHintRequested?.Invoke(message);
}
