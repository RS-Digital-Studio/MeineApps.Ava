namespace BomberBlast.Services;

/// <summary>
/// Abstraction over platform-specific audio playback.
/// Replaces Plugin.Maui.Audio.
/// </summary>
public interface ISoundService : IDisposable
{
    /// <summary>
    /// Preload all sound effects for instant playback
    /// </summary>
    Task PreloadSoundsAsync();

    /// <summary>
    /// Play a sound effect by key
    /// </summary>
    void PlaySound(string soundKey, float volume);

    /// <summary>
    /// Play background music (loops continuously)
    /// </summary>
    void PlayMusic(string musicKey, float volume);

    /// <summary>
    /// Stop background music
    /// </summary>
    void StopMusic();

    /// <summary>
    /// Pause background music
    /// </summary>
    void PauseMusic();

    /// <summary>
    /// Resume background music
    /// </summary>
    void ResumeMusic();

    /// <summary>
    /// Musik-Lautstärke setzen (für Crossfade)
    /// </summary>
    void SetMusicVolume(float volume);
}
