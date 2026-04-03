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
        get => _gameStateService.State.Settings.SoundEnabled;
        set
        {
            _gameStateService.State.Settings.SoundEnabled = value;
            _gameStateService.MarkDirty();
        }
    }

    public bool MusicEnabled
    {
        get => _gameStateService.State.Settings.MusicEnabled;
        set
        {
            _gameStateService.State.Settings.MusicEnabled = value;
            _gameStateService.MarkDirty();
            if (!value) StopMusic();
        }
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

    public void StopMusic()
    {
        // Stub: No music to stop
    }

    public void Vibrate(VibrationType type)
    {
        // Stub: No haptic feedback on desktop/Avalonia
    }
}
