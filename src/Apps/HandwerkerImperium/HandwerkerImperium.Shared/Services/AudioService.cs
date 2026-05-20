using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Stub audio service for Avalonia.
/// Audio playback and haptic feedback are not available on desktop platforms.
/// Sound/music settings are still persisted via game state.
/// </summary>
public sealed class AudioService : IAudioService
{
    private readonly IGameStateService _gameStateService;

    public bool SoundEnabled
    {
        get => _gameStateService.Settings.SoundEnabled;
        set
        {
            _gameStateService.Settings.SoundEnabled = value;
        }
    }

    public bool MusicEnabled
    {
        get => _gameStateService.Settings.MusicEnabled;
        set
        {
            _gameStateService.Settings.MusicEnabled = value;
            if (!value) StopMusic();
        }
    }

    /// <summary>F-19: Stub-Implementierung — Desktop hat kein Audio.</summary>
    public float SfxVolume
    {
        get => _gameStateService.Settings.SfxVolume;
        set => _gameStateService.Settings.SfxVolume = System.Math.Clamp(value, 0f, 1f);
    }

    /// <summary>F-19: Stub-Implementierung — Desktop hat kein Audio.</summary>
    public float MusicVolume
    {
        get => _gameStateService.Settings.MusicVolume;
        set => _gameStateService.Settings.MusicVolume = System.Math.Clamp(value, 0f, 1f);
    }

    public AudioService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public Task PlaySoundAsync(GameSound sound)
    {
        // Desktop-Stub: Audio nur auf Android via AndroidAudioService
        return Task.CompletedTask;
    }

    public Task PlayMusicAsync(string musicFile)
    {
        // Stub: No music playback on desktop/Avalonia
        return Task.CompletedTask;
    }

    public Task PlayMusicAsync(MusicTrack track, bool crossfade = true)
    {
        // Stub: Desktop hat keine Musik-Wiedergabe
        return Task.CompletedTask;
    }

    public void StopMusic(bool fadeOut = false)
    {
        // Stub: No music to stop
    }

    public void PauseMusic() { /* Stub */ }

    public void ResumeMusic() { /* Stub */ }

    public void Vibrate(VibrationType type)
    {
        // Stub: No haptic feedback on desktop/Avalonia
    }
}
