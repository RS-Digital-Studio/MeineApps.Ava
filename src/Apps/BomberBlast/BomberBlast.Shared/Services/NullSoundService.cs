namespace BomberBlast.Services;

/// <summary>
/// No-op sound service for platforms without audio support.
/// Can be replaced with platform-specific implementations later.
/// </summary>
public class NullSoundService : ISoundService
{
    public Task PreloadSoundsAsync() => Task.CompletedTask;

    public void PlaySound(string soundKey, float volume) { }

    public void PlayMusic(string musicKey, float volume) { }

    public void StopMusic() { }

    public void PauseMusic() { }

    public void ResumeMusic() { }

    public void SetMusicVolume(float volume) { }

    public void Dispose() { }
}
