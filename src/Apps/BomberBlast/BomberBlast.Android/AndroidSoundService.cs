using Android.Content;
using Android.Content.Res;
using Android.Media;
using BomberBlast.Services;

namespace BomberBlast.Droid;

/// <summary>
/// Android-spezifischer Sound-Service mit SoundPool (SFX) und MediaPlayer (Musik).
/// Sounds werden aus Assets/Sounds/ geladen.
/// SFX: sfx_{key}.ogg → SoundPool (low-latency, mehrere gleichzeitig)
/// Musik: music_{key}.ogg → MediaPlayer (Loop, Streaming)
/// </summary>
public class AndroidSoundService : ISoundService
{
    private readonly Context _context;
    private readonly AssetManager _assets;
    private SoundPool? _soundPool;
    private MediaPlayer? _musicPlayer;

    // SoundPool IDs pro SFX-Key
    private readonly Dictionary<string, int> _sfxIds = new();

    // Aktuell laufender Musik-Key
    private string? _currentMusicKey;
    private bool _disposed;

    // Maximale gleichzeitige SFX-Streams
    private const int MaxStreams = 8;

    // Alle bekannten SFX-Keys
    private static readonly string[] SfxKeys =
    [
        "explosion", "place_bomb", "fuse", "powerup",
        "player_death", "enemy_death", "exit_appear",
        "level_complete", "game_over", "time_warning",
        "menu_select", "menu_confirm"
    ];

    // Alle bekannten Musik-Keys
    private static readonly string[] MusicKeys =
    [
        "menu", "gameplay", "boss", "victory"
    ];

    public AndroidSoundService(Context context)
    {
        _context = context;
        _assets = context.Assets!;
    }

    public async Task PreloadSoundsAsync()
    {
        await Task.Run(() =>
        {
            // SoundPool erstellen (AudioAttributes statt deprecated Constructor)
            var audioAttributes = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .Build();

            _soundPool = new SoundPool.Builder()!
                .SetMaxStreams(MaxStreams)!
                .SetAudioAttributes(audioAttributes!)!
                .Build();

            // SFX-Dateien aus Assets laden (versucht .ogg, dann .wav)
            foreach (var key in SfxKeys)
            {
                var loaded = TryLoadSfx($"Sounds/sfx_{key}.ogg", key)
                          || TryLoadSfx($"Sounds/sfx_{key}.wav", key);
            }
        });
    }

    /// <summary>Versucht eine SFX-Datei aus Assets in SoundPool zu laden</summary>
    private bool TryLoadSfx(string filename, string key)
    {
        try
        {
            var afd = _assets.OpenFd(filename);
            if (afd != null)
            {
                var id = _soundPool!.Load(afd, 1);
                _sfxIds[key] = id;
                afd.Close();
                return true;
            }
        }
        catch (Java.IO.IOException) { }
        return false;
    }

    public void PlaySound(string soundKey, float volume)
    {
        if (_soundPool == null || !_sfxIds.TryGetValue(soundKey, out var soundId))
            return;

        var clampedVol = Math.Clamp(volume, 0f, 1f);
        _soundPool.Play(soundId, clampedVol, clampedVol, 1, 0, 1.0f);
    }

    public void PlayMusic(string musicKey, float volume)
    {
        // Gleiche Musik bereits aktiv → nur Lautstärke anpassen
        if (_currentMusicKey == musicKey && _musicPlayer != null)
        {
            var v = Math.Clamp(volume, 0f, 1f);
            _musicPlayer.SetVolume(v, v);
            return;
        }

        StopMusic();

        // Versucht .ogg, dann .wav
        AssetFileDescriptor? afd = null;
        foreach (var ext in new[] { ".ogg", ".wav" })
        {
            try { afd = _assets.OpenFd($"Sounds/music_{musicKey}{ext}"); break; }
            catch (Java.IO.IOException) { }
        }
        if (afd == null) return;

        try
        {

            _musicPlayer = new MediaPlayer();
            _musicPlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
            _musicPlayer.Looping = true;
            var vol = Math.Clamp(volume, 0f, 1f);
            _musicPlayer.SetVolume(vol, vol);
            _musicPlayer.Prepare();
            _musicPlayer.Start();
            _currentMusicKey = musicKey;
            afd.Close();
        }
        catch (Java.IO.IOException)
        {
            // Musik-Datei nicht vorhanden → ignorieren
        }
        catch (Exception)
        {
            // MediaPlayer-Fehler → ignorieren
            _musicPlayer?.Release();
            _musicPlayer = null;
            _currentMusicKey = null;
        }
    }

    public void StopMusic()
    {
        if (_musicPlayer != null)
        {
            try
            {
                if (_musicPlayer.IsPlaying)
                    _musicPlayer.Stop();
            }
            catch { /* Bereits gestoppt */ }

            _musicPlayer.Release();
            _musicPlayer = null;
        }
        _currentMusicKey = null;
    }

    public void PauseMusic()
    {
        try
        {
            if (_musicPlayer is { IsPlaying: true })
                _musicPlayer.Pause();
        }
        catch { /* Bereits pausiert */ }
    }

    public void ResumeMusic()
    {
        try
        {
            if (_musicPlayer != null && !_musicPlayer.IsPlaying)
                _musicPlayer.Start();
        }
        catch { /* Kann nicht fortgesetzt werden */ }
    }

    public void SetMusicVolume(float volume)
    {
        try
        {
            if (_musicPlayer != null)
            {
                var v = Math.Clamp(volume, 0f, 1f);
                _musicPlayer.SetVolume(v, v);
            }
        }
        catch { /* Volume-Änderung fehlgeschlagen */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopMusic();

        _soundPool?.Release();
        _soundPool = null;
        _sfxIds.Clear();
    }
}
