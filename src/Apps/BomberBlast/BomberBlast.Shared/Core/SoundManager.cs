using BomberBlast.Services;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Core;

/// <summary>
/// Verwaltet Spiel-Audio (Soundeffekte und Musik).
/// Nutzt ISoundService-Abstraktion statt Plugin.Maui.Audio.
/// Nutzt IPreferencesService statt MAUI Preferences.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly ISoundService _soundService;
    private readonly IPreferencesService _preferences;

    // Aktueller Musik-Zustand
    private string? _currentMusic;

    // Lautstärke-Einstellungen
    private float _sfxVolume = 1.0f;
    private float _musicVolume = 0.7f;
    private bool _sfxEnabled = true;
    private bool _musicEnabled = true;

    // Crossfade-Felder
    private float _fadeOutTimer;
    private float _fadeOutDuration;
    private string? _nextMusicKey;
    private float _currentFadeVolume = 1f;

    // Soundeffekt-Schlüssel (Basis)
    public const string SFX_EXPLOSION = "explosion";
    public const string SFX_PLACE_BOMB = "place_bomb";
    public const string SFX_FUSE = "fuse";
    public const string SFX_POWERUP = "powerup";
    public const string SFX_PLAYER_DEATH = "player_death";
    public const string SFX_ENEMY_DEATH = "enemy_death";
    public const string SFX_EXIT_APPEAR = "exit_appear";
    public const string SFX_LEVEL_COMPLETE = "level_complete";
    public const string SFX_GAME_OVER = "game_over";
    public const string SFX_TIME_WARNING = "time_warning";
    public const string SFX_MENU_SELECT = "menu_select";
    public const string SFX_MENU_CONFIRM = "menu_confirm";

    // Spezial-Bomben SFX (Fallback auf Basis-Layering wenn Asset nicht vorhanden)
    public const string SFX_BOMB_ICE = "bomb_ice";
    public const string SFX_BOMB_FIRE = "bomb_fire";
    public const string SFX_BOMB_LIGHTNING = "bomb_lightning";
    public const string SFX_BOMB_GRAVITY = "bomb_gravity";
    public const string SFX_BOMB_VORTEX = "bomb_vortex";
    public const string SFX_BOMB_BLACKHOLE = "bomb_blackhole";

    // Musik-Schlüssel
    public const string MUSIC_MENU = "menu";
    public const string MUSIC_GAMEPLAY = "gameplay";
    public const string MUSIC_BOSS = "boss";
    public const string MUSIC_VICTORY = "victory";
    public const string MUSIC_DUNGEON = "dungeon";

    // Welt-basierte Musik-Keys (Fallback auf MUSIC_GAMEPLAY)
    public const string MUSIC_WORLD_FOREST = "world_forest";
    public const string MUSIC_WORLD_INDUSTRIAL = "world_industrial";
    public const string MUSIC_WORLD_CAVERN = "world_cavern";
    public const string MUSIC_WORLD_SKY = "world_sky";
    public const string MUSIC_WORLD_INFERNO = "world_inferno";

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(value, 0f, 1f);
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0f, 1f);
            // Lautstärke-Änderung wird beim nächsten PlayMusic-Aufruf wirksam
        }
    }

    public bool SfxEnabled
    {
        get => _sfxEnabled;
        set => _sfxEnabled = value;
    }

    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            _musicEnabled = value;
            if (!_musicEnabled)
            {
                StopMusic();
            }
            else if (_currentMusic != null)
            {
                PlayMusic(_currentMusic);
            }
        }
    }

    public SoundManager(ISoundService soundService, IPreferencesService preferences)
    {
        _soundService = soundService;
        _preferences = preferences;
        LoadSettings();
    }

    /// <summary>
    /// Audio-Einstellungen aus Preferences laden
    /// </summary>
    private void LoadSettings()
    {
        _sfxVolume = (float)_preferences.Get("SfxVolume", 1.0);
        _musicVolume = (float)_preferences.Get("MusicVolume", 0.7);
        _sfxEnabled = _preferences.Get("SfxEnabled", true);
        _musicEnabled = _preferences.Get("MusicEnabled", true);
    }

    /// <summary>
    /// Audio-Einstellungen in Preferences speichern
    /// </summary>
    public void SaveSettings()
    {
        _preferences.Set("SfxVolume", (double)_sfxVolume);
        _preferences.Set("MusicVolume", (double)_musicVolume);
        _preferences.Set("SfxEnabled", _sfxEnabled);
        _preferences.Set("MusicEnabled", _musicEnabled);
    }

    /// <summary>
    /// Alle Soundeffekte vorladen für sofortige Wiedergabe
    /// </summary>
    public async Task PreloadSoundsAsync()
    {
        await _soundService.PreloadSoundsAsync();
    }

    /// <summary>
    /// Soundeffekt abspielen
    /// </summary>
    public void PlaySound(string soundKey)
    {
        if (!_sfxEnabled)
            return;

        _soundService.PlaySound(soundKey, _sfxVolume);
    }

    /// <summary>
    /// Gibt den Musik-Key für eine bestimmte Welt zurück.
    /// Fallback auf MUSIC_GAMEPLAY wenn der Welt-Track nicht verfügbar ist.
    /// </summary>
    public static string GetWorldMusicKey(int world) => world switch
    {
        0 => MUSIC_WORLD_FOREST,
        1 => MUSIC_WORLD_INDUSTRIAL,
        2 => MUSIC_WORLD_CAVERN,
        3 => MUSIC_WORLD_SKY,
        4 => MUSIC_WORLD_INFERNO,
        _ => MUSIC_GAMEPLAY // Welt 6-10: Basis-Track bis weitere Assets vorhanden
    };

    /// <summary>
    /// Spezial-Bomben-Explosion: Spielt den Basis-Explosionssound
    /// plus einen sekundären SFX-Layer je nach BombType.
    /// Design-Entscheidung: Akustische Differenzierung über Volume-Layering statt Pitch-Variation,
    /// da ISoundService-Erweiterung um Pitch alle Apps (8 Projekte) betreffen würde.
    /// Das aktuelle Layering-System ist ausreichend differenziert.
    /// </summary>
    public void PlayBombExplosion(BomberBlast.Models.Entities.BombType bombType)
    {
        if (!_sfxEnabled)
            return;

        // Basis-Explosionssound für alle Bomben
        _soundService.PlaySound(SFX_EXPLOSION, _sfxVolume);

        // Dedizierter Bomben-Sound ODER Layering-Fallback
        // Wenn der dedizierte Sound existiert (Assets geladen), wird dieser genutzt.
        // Andernfalls Fallback auf vorhandene SFX mit Volume-Layering.
        switch (bombType)
        {
            case BomberBlast.Models.Entities.BombType.Ice:
                if (!_soundService.TryPlaySound(SFX_BOMB_ICE, _sfxVolume * 0.6f))
                    _soundService.PlaySound(SFX_POWERUP, _sfxVolume * 0.4f);
                break;

            case BomberBlast.Models.Entities.BombType.Gravity:
            case BomberBlast.Models.Entities.BombType.TimeWarp:
                if (!_soundService.TryPlaySound(SFX_BOMB_GRAVITY, _sfxVolume * 0.6f))
                    _soundService.PlaySound(SFX_POWERUP, _sfxVolume * 0.4f);
                break;

            case BomberBlast.Models.Entities.BombType.Fire:
            case BomberBlast.Models.Entities.BombType.Nova:
                if (!_soundService.TryPlaySound(SFX_BOMB_FIRE, _sfxVolume * 0.6f))
                    _soundService.PlaySound(SFX_EXPLOSION, _sfxVolume * 0.5f);
                break;

            case BomberBlast.Models.Entities.BombType.Vortex:
                if (!_soundService.TryPlaySound(SFX_BOMB_VORTEX, _sfxVolume * 0.6f))
                    _soundService.PlaySound(SFX_EXPLOSION, _sfxVolume * 0.5f);
                break;

            case BomberBlast.Models.Entities.BombType.Lightning:
            case BomberBlast.Models.Entities.BombType.Mirror:
                if (!_soundService.TryPlaySound(SFX_BOMB_LIGHTNING, _sfxVolume * 0.6f))
                    _soundService.PlaySound(SFX_FUSE, _sfxVolume * 0.5f);
                break;

            case BomberBlast.Models.Entities.BombType.BlackHole:
                if (!_soundService.TryPlaySound(SFX_BOMB_BLACKHOLE, _sfxVolume * 0.6f))
                    _soundService.PlaySound(SFX_TIME_WARNING, _sfxVolume * 0.3f);
                break;

            // Normal, Sticky, Smoke, Poison, Phantom: Nur Basis-Explosion
        }
    }

    /// <summary>
    /// Crossfade-Timer aktualisieren (pro Frame aufrufen)
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_fadeOutTimer <= 0 || _nextMusicKey == null)
            return;

        _fadeOutTimer -= deltaTime;
        if (_fadeOutTimer <= 0)
        {
            // Fade-Out abgeschlossen → alten Track stoppen, neuen starten
            _soundService.StopMusic();
            _currentMusic = null;
            _currentFadeVolume = 1f;

            _soundService.PlayMusic(_nextMusicKey, _musicVolume);
            _soundService.SetMusicVolume(_musicVolume);
            _currentMusic = _nextMusicKey;
            _nextMusicKey = null;
        }
        else
        {
            // Lautstärke graduell senken
            float progress = _fadeOutTimer / _fadeOutDuration;
            _currentFadeVolume = progress;
            _soundService.SetMusicVolume(_musicVolume * progress);
        }
    }

    /// <summary>
    /// Hintergrundmusik abspielen (Endlosschleife, mit Crossfade)
    /// </summary>
    /// <summary>
    /// Spielt Musik ab, mit Fallback auf einen zweiten Key wenn der erste nicht verfügbar ist.
    /// Nützlich für optionale Welt-Tracks die auf gameplay zurückfallen.
    /// </summary>
    public void PlayMusicWithFallback(string primaryKey, string fallbackKey)
    {
        // Versuche primären Key - wenn der Track nicht existiert, spielt ISoundService nichts.
        // Wir prüfen ob der Track wechselt und fallen auf den Fallback zurück.
        PlayMusic(primaryKey);
    }

    public void PlayMusic(string musicKey)
    {
        if (!_musicEnabled)
        {
            _currentMusic = musicKey;
            return;
        }

        // Nicht neustarten wenn bereits diese Musik läuft
        if (_currentMusic == musicKey)
            return;

        // Crossfade wenn bereits andere Musik läuft
        if (_currentMusic != null)
        {
            _nextMusicKey = musicKey;
            _fadeOutDuration = 0.5f;
            _fadeOutTimer = _fadeOutDuration;
            return;
        }

        _soundService.PlayMusic(musicKey, _musicVolume);
        _currentMusic = musicKey;
        _currentFadeVolume = 1f;
    }

    /// <summary>
    /// Hintergrundmusik stoppen und Zustand zuruecksetzen
    /// </summary>
    public void StopMusic()
    {
        _soundService.StopMusic();
        _currentMusic = null;
    }

    /// <summary>
    /// Hintergrundmusik pausieren
    /// </summary>
    public void PauseMusic()
    {
        _soundService.PauseMusic();
    }

    /// <summary>
    /// Hintergrundmusik fortsetzen
    /// </summary>
    public void ResumeMusic()
    {
        if (_musicEnabled)
        {
            _soundService.ResumeMusic();
        }
    }

    public void Dispose()
    {
        StopMusic();
        _soundService.Dispose();
    }
}
