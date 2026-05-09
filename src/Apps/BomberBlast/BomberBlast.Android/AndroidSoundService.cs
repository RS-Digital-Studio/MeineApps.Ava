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
public sealed class AndroidSoundService : ISoundService
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
    private readonly object _musicLock = new();

    // Maximale gleichzeitige SFX-Streams
    private const int MaxStreams = 8;

    // Alle bekannten SFX-Keys (Basis-Keys — werden auch als Single-Sample-Fallback geladen)
    private static readonly string[] SfxKeys =
    [
        "explosion", "place_bomb", "fuse", "powerup",
        "player_death", "enemy_death", "exit_appear",
        "level_complete", "game_over", "time_warning",
        "menu_select", "menu_confirm",
        // Spezial-Bomben SFX (optional, Fallback im SoundManager)
        "bomb_ice", "bomb_fire", "bomb_lightning",
        "bomb_gravity", "bomb_vortex", "bomb_blackhole"
    ];

    // Multi-Sample-Pool-Keys (Phase 16). Werden zusätzlich zu den Basis-Keys geladen,
    // jeweils nur wenn die Datei existiert (Hot-Path: TryLoadSfx mit silent fail).
    // Basis-Key ohne Pool-Variant: SoundManager.PlayPooled fällt automatisch zurück.
    private static readonly string[] PoolVariantKeys =
    [
        // 4-Pool für Hochfrequenz-SFX
        "place_bomb_a", "place_bomb_b", "place_bomb_c", "place_bomb_d",
        "explosion_a", "explosion_b", "explosion_c", "explosion_d",
        // 3-Pool für mittelfrequente
        "fuse_a", "fuse_b", "fuse_c",
        "powerup_a", "powerup_b", "powerup_c",
        "enemy_death_a", "enemy_death_b", "enemy_death_c",
        // 2-Pool für seltene
        "player_death_a", "player_death_b",
        "menu_select_a", "menu_select_b",
        "menu_confirm_a", "menu_confirm_b",
        "level_complete_a", "level_complete_b",
        "game_over_a", "game_over_b",
    ];

    // Cinematic-Stinger-Library (Phase 16/17)
    private static readonly string[] StingerKeys =
    [
        "stinger_combo_mega",
        "stinger_combo_ultra",
        "stinger_boss_reveal",
        "stinger_victory",
        "stinger_defeat",
    ];

    // Alle bekannten Musik-Keys
    private static readonly string[] MusicKeys =
    [
        "menu", "gameplay", "boss", "victory",
        // Welt-Musik + Dungeon
        "world_forest", "world_industrial", "world_cavern",
        "world_sky", "world_inferno", "dungeon"
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

            // Multi-Sample-Pool-Variants laden (silent fail, Phase 17 stellt Files bereit)
            foreach (var key in PoolVariantKeys)
            {
                _ = TryLoadSfx($"Sounds/sfx_{key}.ogg", key)
                 || TryLoadSfx($"Sounds/sfx_{key}.wav", key);
            }

            // Cinematic-Stinger laden (silent fail bis Phase 17)
            foreach (var key in StingerKeys)
            {
                _ = TryLoadSfx($"Sounds/sfx_{key}.ogg", key)
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
        PlaySound(soundKey, volume, 1.0f, 0f);
    }

    /// <summary>
    /// Spielt einen Sound mit Pitch und Stereo-Pan über SoundPool.Play.
    /// Pitch wird auf [0.5, 2.0] geclamped (SoundPool-Limit).
    /// Pan wird in linkes/rechtes Volume umgesetzt (-1=links, 0=mittig, 1=rechts).
    /// </summary>
    public void PlaySound(string soundKey, float volume, float pitch, float pan = 0f)
    {
        if (_soundPool == null || !_sfxIds.TryGetValue(soundKey, out var soundId))
            return;

        var clampedVol = Math.Clamp(volume, 0f, 1f);
        var clampedPitch = Math.Clamp(pitch, 0.5f, 2.0f);
        var clampedPan = Math.Clamp(pan, -1f, 1f);

        // Stereo-Pan: links bei pan<0, rechts bei pan>0
        var leftVol = clampedPan <= 0 ? clampedVol : clampedVol * (1f - clampedPan);
        var rightVol = clampedPan >= 0 ? clampedVol : clampedVol * (1f + clampedPan);

        _soundPool.Play(soundId, leftVol, rightVol, 1, 0, clampedPitch);
    }

    /// <summary>Versucht Sound abzuspielen. Gibt false zurück wenn der Sound nicht geladen ist.</summary>
    public bool TryPlaySound(string soundKey, float volume)
    {
        return TryPlaySound(soundKey, volume, 1.0f, 0f);
    }

    /// <summary>Versucht Sound mit Pitch + Pan abzuspielen.</summary>
    public bool TryPlaySound(string soundKey, float volume, float pitch, float pan = 0f)
    {
        if (_soundPool == null || !_sfxIds.TryGetValue(soundKey, out var soundId))
            return false;

        var clampedVol = Math.Clamp(volume, 0f, 1f);
        var clampedPitch = Math.Clamp(pitch, 0.5f, 2.0f);
        var clampedPan = Math.Clamp(pan, -1f, 1f);

        var leftVol = clampedPan <= 0 ? clampedVol : clampedVol * (1f - clampedPan);
        var rightVol = clampedPan >= 0 ? clampedVol : clampedVol * (1f + clampedPan);

        _soundPool.Play(soundId, leftVol, rightVol, 1, 0, clampedPitch);
        return true;
    }

    public void PlayMusic(string musicKey, float volume)
    {
        lock (_musicLock)
        {
            // Gleiche Musik bereits aktiv → nur Lautstärke anpassen
            if (_currentMusicKey == musicKey && _musicPlayer != null)
            {
                try
                {
                    var v = Math.Clamp(volume, 0f, 1f);
                    _musicPlayer.SetVolume(v, v);
                }
                catch { /* MediaPlayer im ungültigen State */ }
                return;
            }

            StopMusicInternal();

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
                var player = new MediaPlayer();
                player.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                player.Looping = true;
                var vol = Math.Clamp(volume, 0f, 1f);
                player.SetVolume(vol, vol);

                // PrepareAsync statt Prepare → blockiert UI-Thread nicht (ANR-Fix)
                player.Prepared += (_, _) =>
                {
                    try { player.Start(); }
                    catch { /* Player bereits disposed/gestoppt */ }
                };
                player.PrepareAsync();

                _musicPlayer = player;
                _currentMusicKey = musicKey;
                afd.Close();
            }
            catch (Java.IO.IOException)
            {
                // Musik-Datei nicht vorhanden → ignorieren
            }
            catch (Exception)
            {
                // MediaPlayer-Fehler → aufräumen
                _musicPlayer?.Release();
                _musicPlayer = null;
                _currentMusicKey = null;
            }
        }
    }

    public void StopMusic()
    {
        lock (_musicLock)
        {
            StopMusicInternal();
        }
    }

    /// <summary>
    /// Interne StopMusic-Logik ohne Lock (wird von PlayMusic innerhalb des Locks aufgerufen).
    /// </summary>
    private void StopMusicInternal()
    {
        if (_musicPlayer != null)
        {
            try
            {
                if (_musicPlayer.IsPlaying)
                    _musicPlayer.Stop();
            }
            catch { /* Bereits gestoppt */ }

            try { _musicPlayer.Release(); }
            catch { /* Bereits released */ }

            _musicPlayer = null;
        }
        _currentMusicKey = null;
    }

    public void PauseMusic()
    {
        lock (_musicLock)
        {
            try
            {
                if (_musicPlayer is { IsPlaying: true })
                    _musicPlayer.Pause();
            }
            catch { /* Bereits pausiert */ }
        }
    }

    public void ResumeMusic()
    {
        lock (_musicLock)
        {
            try
            {
                if (_musicPlayer != null && !_musicPlayer.IsPlaying)
                    _musicPlayer.Start();
            }
            catch { /* Kann nicht fortgesetzt werden */ }
        }
    }

    public void SetMusicVolume(float volume)
    {
        lock (_musicLock)
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
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_musicLock)
        {
            StopMusicInternal();
        }

        _soundPool?.Release();
        _soundPool = null;
        _sfxIds.Clear();
    }
}
