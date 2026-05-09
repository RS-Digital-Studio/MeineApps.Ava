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
    /// Spielt einen Sound mit Pitch-Variation und optionalem Stereo-Pan.
    /// Pitch 1.0 = Original, 0.5-2.0 sind sinnvolle Bereiche (SoundPool-Limits).
    /// Pan -1.0 = links, 0.0 = mittig, 1.0 = rechts.
    /// Default-Implementation delegiert an PlaySound (kein Pitch/Pan).
    /// </summary>
    void PlaySound(string soundKey, float volume, float pitch, float pan = 0f) => PlaySound(soundKey, volume);

    /// <summary>
    /// Versucht einen Sound abzuspielen. Gibt false zurück wenn der Sound nicht geladen ist.
    /// Ermöglicht Fallback-Logik für optionale Sounds (z.B. dedizierte Bomben-SFX).
    /// </summary>
    bool TryPlaySound(string soundKey, float volume) => false;

    /// <summary>
    /// Versucht einen Sound mit Pitch + Pan abzuspielen. Gibt false zurück wenn der Sound nicht geladen ist.
    /// Default-Implementation delegiert an TryPlaySound (kein Pitch/Pan).
    /// </summary>
    bool TryPlaySound(string soundKey, float volume, float pitch, float pan = 0f) => TryPlaySound(soundKey, volume);

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
