namespace BomberBlast.Services;

/// <summary>
/// No-Op Implementierung für Desktop (kein Cloud Save).
/// </summary>
public class NullCloudSaveService : ICloudSaveService
{
    public bool IsEnabled => false;
    public bool IsSyncing => false;
    public string? LastSyncTimeUtc => null;

#pragma warning disable CS0067 // Event wird im Null-Service nie gefeuert
    public event EventHandler? SyncStatusChanged;
#pragma warning restore CS0067

    public Task<bool> TryLoadFromCloudAsync() => Task.FromResult(false);
    public Task SchedulePushAsync() => Task.CompletedTask;
    public Task ForceUploadAsync() => Task.CompletedTask;
    public Task<bool> ForceDownloadAsync() => Task.FromResult(false);
    public void SetEnabled(bool enabled) { }
}
