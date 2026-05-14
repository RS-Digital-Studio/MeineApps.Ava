using BomberBlast.Core.Audio;
using BomberBlast.Services;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Core;

/// <summary>
/// Verwaltet Spiel-Audio (Soundeffekte und Musik). Phase 16 () erweitert um:
/// <list type="bullet">
/// <item>7-Kanal Bus-System (Master/Music/Ambient/SFX/UI/Voice/Cinematic) via <see cref="AudioBusMixer"/>.</item>
/// <item>Multi-Sample-Pool mit Anti-Repeat (Brawl-Stars-Pattern) via <see cref="SoundVariationPool"/>.</item>
/// <item>Equal-Power-Crossfade (sin/cos statt linear) — Studio-Standard.</item>
/// <item>Spatial-Audio: Distance-Falloff + Stereo-Pan aus Grid-Koordinaten.</item>
/// <item>Sidechain-Ducking: Music duckt automatisch bei Cinematic/Voice-Stinger.</item>
/// </list>
/// Backward-Compat: Alle alten Public-Methoden (PlaySound/PlayMusic/PlayBombExplosion/...) bleiben unverändert.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly ISoundService _soundService;
    private readonly IPreferencesService _preferences;
    private readonly AudioBusMixer _busMixer;
    private readonly SoundVariationPool _pool = new();

    // Aktueller Musik-Zustand
    private string? _currentMusic;

    // Lautstärke-Einstellungen (Backward-Compat: weiterhin per-Property steuerbar,
    // intern werden sie an Bus-Volumes gespiegelt sodass Bus-Mixer stets die Wahrheit hält).
    private float _sfxVolume = 1.0f;
    private float _musicVolume = 0.7f;
    private bool _sfxEnabled = true;
    private bool _musicEnabled = true;

    // Crossfade-Felder — Equal-Power-Kurve (sin/cos)
    private float _fadeOutTimer;
    private float _fadeOutDuration;
    private string? _nextMusicKey;
    private float _currentFadeVolume = 1f;

    // Welt-Reverb-Hint (informativ — wird vom Plattform-Service ausgewertet, falls unterstützt)
    private ReverbPreset _currentReverbPreset = ReverbPreset.None;

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

    // Stinger-Library (Cinematic-Bus, Phase 17 fügt Files hinzu)
    public const string STINGER_COMBO_MEGA = "stinger_combo_mega";
    public const string STINGER_COMBO_ULTRA = "stinger_combo_ultra";
    public const string STINGER_BOSS_REVEAL = "stinger_boss_reveal";
    public const string STINGER_VICTORY = "stinger_victory";
    public const string STINGER_DEFEAT = "stinger_defeat";

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

    /// <summary>Bus-Mixer für direkten Settings-View-Zugriff (Phase 25 erweitert die UI).</summary>
    public AudioBusMixer BusMixer => _busMixer;

    /// <summary>Aktuelle Welt-Reverb-Hint (read-only).</summary>
    public ReverbPreset CurrentReverbPreset => _currentReverbPreset;

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Math.Clamp(value, 0f, 1f);
            // Spiegele in den SFX-Bus damit alle gerouten Sounds (auch Pool/Spatial) konsistent sind.
            _busMixer.SetBusVolume(AudioBus.Sfx, _sfxVolume);
        }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0f, 1f);
            _busMixer.SetBusVolume(AudioBus.Music, _musicVolume);
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
        _busMixer = new AudioBusMixer(preferences);
        LoadSettings();
        RegisterDefaultPools();

        // Bus-Mixer aktualisiert die laufende Musik-Lautstärke wenn der User in den Settings reglt.
        _busMixer.BusVolumeChanged += OnBusVolumeChanged;
    }

    /// <summary>
    /// Registriert die Standard-Pools (Brawl-Stars-Pattern). Variant-Files müssen in Phase 17
    /// als <c>{key}_a.ogg / _b.ogg / _c.ogg</c> bereitgestellt werden. Wenn nur der Basis-Key
    /// als File existiert, fällt der Pool transparent auf diesen zurück (siehe <see cref="PlayPooled"/>).
    /// </summary>
    private void RegisterDefaultPools()
    {
        // Hochfrequente Sounds bekommen die größten Pools (Spam-Resistenz).
        _pool.RegisterPool(SFX_PLACE_BOMB, "a", "b", "c", "d");
        _pool.RegisterPool(SFX_EXPLOSION, "a", "b", "c", "d");
        _pool.RegisterPool(SFX_FUSE, "a", "b", "c");
        _pool.RegisterPool(SFX_POWERUP, "a", "b", "c");
        _pool.RegisterPool(SFX_ENEMY_DEATH, "a", "b", "c");
        _pool.RegisterPool(SFX_PLAYER_DEATH, "a", "b");
        _pool.RegisterPool(SFX_MENU_SELECT, "a", "b");
        _pool.RegisterPool(SFX_MENU_CONFIRM, "a", "b");
        _pool.RegisterPool(SFX_LEVEL_COMPLETE, "a", "b");
        _pool.RegisterPool(SFX_GAME_OVER, "a", "b");
    }

    /// <summary>
    /// Audio-Einstellungen aus Preferences laden + Bus-Mixer synchronisieren.
    /// </summary>
    private void LoadSettings()
    {
        _sfxVolume = (float)_preferences.Get("SfxVolume", 1.0);
        _musicVolume = (float)_preferences.Get("MusicVolume", 0.7);
        _sfxEnabled = _preferences.Get("SfxEnabled", true);
        _musicEnabled = _preferences.Get("MusicEnabled", true);

        // Bus-Mixer übernimmt die Werte als Source-of-Truth.
        _busMixer.SetBusVolume(AudioBus.Sfx, _sfxVolume);
        _busMixer.SetBusVolume(AudioBus.Music, _musicVolume);
    }

    /// <summary>
    /// Audio-Einstellungen in Preferences speichern.
    /// </summary>
    public void SaveSettings()
    {
        _preferences.Set("SfxVolume", (double)_sfxVolume);
        _preferences.Set("MusicVolume", (double)_musicVolume);
        _preferences.Set("SfxEnabled", _sfxEnabled);
        _preferences.Set("MusicEnabled", _musicEnabled);
    }

    /// <summary>
    /// Alle Soundeffekte vorladen für sofortige Wiedergabe.
    /// </summary>
    public Task PreloadSoundsAsync() => _soundService.PreloadSoundsAsync();

    // Pitch-Variation: ±5% für wiederholte SFX, vermeidet akustisches Stutter.
    private static readonly Random _pitchRandom = new();
    private const float DEFAULT_PITCH_VARIATION = 0.05f;
    private static readonly HashSet<string> _pitchVariedSounds = new()
    {
        SFX_PLACE_BOMB,
        SFX_POWERUP,
        SFX_EXPLOSION,
        SFX_FUSE,
        SFX_ENEMY_DEATH
    };

    /// <summary>
    /// Soundeffekt abspielen. Bei häufig wiederholten Sounds (Bomben/PowerUps) wird automatisch
    /// ±5% Pitch-Random angewendet, damit das Spiel akustisch nicht repetitiv wirkt.
    /// Routing: SFX-Bus.
    /// </summary>
    public void PlaySound(string soundKey)
    {
        PlaySoundOnBus(soundKey, AudioBus.Sfx, 1f, 0f, autoVariation: true);
    }

    /// <summary>
    /// Soundeffekt mit explizitem Pan abspielen (Stereo-Positionierung).
    /// Pan -1 = links, 0 = mittig, 1 = rechts. Routing: SFX-Bus.
    /// </summary>
    public void PlaySoundPanned(string soundKey, float pan)
    {
        PlaySoundOnBus(soundKey, AudioBus.Sfx, 1f, pan, autoVariation: true);
    }

    /// <summary>
    /// Spielt einen Sound aus dem Multi-Sample-Pool (zufällige Variant + Anti-Repeat).
    /// Wenn kein Pool registriert ist oder die Variant-Files nicht geladen sind, fällt
    /// die Methode transparent auf den Basis-Key zurück (TryPlaySound-Pfad).
    /// </summary>
    /// <param name="baseKey">Basis-Key (z.B. <c>SFX_PLACE_BOMB</c>).</param>
    /// <param name="bus">Bus-Routing (Default: SFX).</param>
    /// <param name="volumeMultiplier">Zusätzlicher Volume-Faktor [0,1] für Layering.</param>
    /// <param name="pan">Stereo-Pan [-1, 1].</param>
    public void PlayPooled(string baseKey, AudioBus bus = AudioBus.Sfx, float volumeMultiplier = 1f, float pan = 0f)
    {
        if (!IsBusEnabled(bus)) return;

        var pickedKey = _pool.PickVariant(baseKey);
        var pitch = GetVariationPitch(baseKey);
        var volVar = GetVariationVolume(baseKey);
        var effectiveVol = _busMixer.GetEffectiveVolume(bus, volumeMultiplier * volVar);

        // Erst die Variant probieren — wenn nicht geladen, Fallback auf Basis-Key.
        if (!_soundService.TryPlaySound(pickedKey, effectiveVol, pitch, pan))
        {
            _soundService.PlaySound(baseKey, effectiveVol, pitch, pan);
        }
    }

    /// <summary>
    /// Spielt einen Sound an einer Spielfeld-Position. Volume + Pan werden automatisch
    /// aus der Grid-Distanz zum Spieler berechnet (3D-Distance-Falloff + Stereo-Pan).
    /// </summary>
    public void PlayAt(string baseKey, int gridX, int gridY, int playerGridX, int playerGridY,
        AudioBus bus = AudioBus.Sfx, int gridWidth = 15, int fullVolumeRadius = 3, int silenceRadius = 12)
    {
        if (!IsBusEnabled(bus)) return;

        var distVol = AudioSpatial.CalculateDistanceVolume(
            gridX, gridY, playerGridX, playerGridY, fullVolumeRadius, silenceRadius);
        if (distVol <= 0f) return;

        var pan = AudioSpatial.CalculatePan(gridX, playerGridX, gridWidth);
        PlayPooled(baseKey, bus, distVol, pan);
    }

    /// <summary>
    /// Universeller Pfad: Sound mit explizitem Bus-Routing + optionaler Auto-Variation.
    /// </summary>
    public void PlaySoundOnBus(string soundKey, AudioBus bus, float volumeMultiplier = 1f, float pan = 0f, bool autoVariation = false)
    {
        if (!IsBusEnabled(bus)) return;

        var pitch = autoVariation && _pitchVariedSounds.Contains(soundKey)
            ? 1.0f + ((float)_pitchRandom.NextDouble() * 2f - 1f) * DEFAULT_PITCH_VARIATION
            : 1.0f;
        var volVar = autoVariation && _pitchVariedSounds.Contains(soundKey)
            ? 1.0f + ((float)_pitchRandom.NextDouble() * 2f - 1f) * 0.1f
            : 1.0f;

        var eff = _busMixer.GetEffectiveVolume(bus, volumeMultiplier * volVar);
        _soundService.PlaySound(soundKey, eff, pitch, pan);
    }

    /// <summary>
    /// Spielt einen Cinematic-Stinger und duckt automatisch Music+Ambient für 1.5s auf 30%.
    /// </summary>
    public void PlayStinger(string stingerKey, float volumeMultiplier = 1f)
    {
        if (!_sfxEnabled) return;
        var eff = _busMixer.GetEffectiveVolume(AudioBus.Cinematic, volumeMultiplier);
        if (!_soundService.TryPlaySound(stingerKey, eff))
        {
            // Wenn der Stinger-File nicht da ist (Phase 17 noch nicht ausgeliefert), no-op.
            return;
        }
        _busMixer.DuckForCinematic();
    }

    /// <summary>
    /// Voice-Line oder Announcer-Sample. Duckt Music+Ambient für 0.8s auf 50%.
    /// </summary>
    public void PlayVoice(string voiceKey, float volumeMultiplier = 1f)
    {
        if (!_sfxEnabled) return;
        var eff = _busMixer.GetEffectiveVolume(AudioBus.Voice, volumeMultiplier);
        if (!_soundService.TryPlaySound(voiceKey, eff)) return;
        _busMixer.DuckForCinematic(durationSeconds: 0.8f, multiplier: 0.5f);
    }

    /// <summary>UI-Sounds (Menu-Tap, Confirm, Hover) — Bus.UI.</summary>
    public void PlayUi(string uiKey, float volumeMultiplier = 1f)
        => PlaySoundOnBus(uiKey, AudioBus.Ui, volumeMultiplier);

    private bool IsBusEnabled(AudioBus bus) => bus switch
    {
        AudioBus.Music or AudioBus.Ambient => _musicEnabled,
        _ => _sfxEnabled,
    };

    private float GetVariationPitch(string baseKey)
        => _pitchVariedSounds.Contains(baseKey)
            ? 1.0f + ((float)_pitchRandom.NextDouble() * 2f - 1f) * DEFAULT_PITCH_VARIATION
            : 1.0f;

    private float GetVariationVolume(string baseKey)
        => _pitchVariedSounds.Contains(baseKey)
            ? 1.0f + ((float)_pitchRandom.NextDouble() * 2f - 1f) * 0.1f
            : 1.0f;

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
    /// Setzt den Welt-Reverb-Hint für nachfolgende SFX. Plattform-Services können den Wert
    /// auswerten (z.B. Android AudioFx PresetReverb). SoundPool hat keinen nativen Reverb,
    /// aber der Wert wird für Phase 17+ Audio-FX-Pipeline vorbereitet.
    /// </summary>
    public void ApplyWorldReverbHint(int worldIndex)
    {
        _currentReverbPreset = WorldReverbMap.GetPresetForWorld(worldIndex);
    }

    /// <summary>Setzt den Reverb-Hint manuell (z.B. <see cref="ReverbPreset.Cave"/> für Dungeon).</summary>
    public void SetReverbPreset(ReverbPreset preset)
    {
        _currentReverbPreset = preset;
    }

    /// <summary>
    /// Spezial-Bomben-Explosion: Spielt den Basis-Explosionssound plus einen sekundären SFX-Layer
    /// je nach BombType. Beide laufen über den SFX-Bus, Pool wird genutzt wenn vorhanden.
    /// </summary>
    public void PlayBombExplosion(BomberBlast.Models.Entities.BombType bombType)
    {
        if (!_sfxEnabled) return;

        // Basis-Explosionssound für alle Bomben (Pool-Variation wenn vorhanden)
        PlayPooled(SFX_EXPLOSION, AudioBus.Sfx);

        // Dedizierter Bomben-Sound ODER Layering-Fallback
        switch (bombType)
        {
            case BomberBlast.Models.Entities.BombType.Ice:
                if (!TryPlayLayered(SFX_BOMB_ICE, 0.6f))
                    PlayLayered(SFX_POWERUP, 0.4f);
                break;

            case BomberBlast.Models.Entities.BombType.Gravity:
            case BomberBlast.Models.Entities.BombType.TimeWarp:
                if (!TryPlayLayered(SFX_BOMB_GRAVITY, 0.6f))
                    PlayLayered(SFX_POWERUP, 0.4f);
                break;

            case BomberBlast.Models.Entities.BombType.Fire:
            case BomberBlast.Models.Entities.BombType.Nova:
                if (!TryPlayLayered(SFX_BOMB_FIRE, 0.6f))
                    PlayLayered(SFX_EXPLOSION, 0.5f);
                break;

            case BomberBlast.Models.Entities.BombType.Vortex:
                if (!TryPlayLayered(SFX_BOMB_VORTEX, 0.6f))
                    PlayLayered(SFX_EXPLOSION, 0.5f);
                break;

            case BomberBlast.Models.Entities.BombType.Lightning:
            case BomberBlast.Models.Entities.BombType.Mirror:
                if (!TryPlayLayered(SFX_BOMB_LIGHTNING, 0.6f))
                    PlayLayered(SFX_FUSE, 0.5f);
                break;

            case BomberBlast.Models.Entities.BombType.BlackHole:
                if (!TryPlayLayered(SFX_BOMB_BLACKHOLE, 0.6f))
                    PlayLayered(SFX_TIME_WARNING, 0.3f);
                break;

            // Normal, Sticky, Smoke, Poison, Phantom: Nur Basis-Explosion
        }
    }

    private bool TryPlayLayered(string key, float volMul)
    {
        var eff = _busMixer.GetEffectiveVolume(AudioBus.Sfx, volMul);
        return _soundService.TryPlaySound(key, eff);
    }

    private void PlayLayered(string key, float volMul)
    {
        var eff = _busMixer.GetEffectiveVolume(AudioBus.Sfx, volMul);
        _soundService.PlaySound(key, eff);
    }

    /// <summary>
    /// Pro-Frame-Update: Crossfade-Hüllkurve (Equal-Power) + Bus-Ducking-Hüllkurven tickern.
    /// </summary>
    public void Update(float deltaTime)
    {
        // 1. Bus-Ducking: lässt Music wieder hochkommen wenn Stinger/Voice vorbei.
        _busMixer.Update(deltaTime);

        // 2. Music-Crossfade
        if (_fadeOutTimer > 0 && _nextMusicKey != null)
        {
            _fadeOutTimer -= deltaTime;
            if (_fadeOutTimer <= 0)
            {
                // Fade-Out abgeschlossen → alten Track stoppen, neuen starten
                _soundService.StopMusic();
                _currentMusic = null;
                _currentFadeVolume = 1f;

                var startVol = _busMixer.GetEffectiveVolume(AudioBus.Music, 1f);
                _soundService.PlayMusic(_nextMusicKey, startVol);
                _soundService.SetMusicVolume(startVol);
                _currentMusic = _nextMusicKey;
                _nextMusicKey = null;
            }
            else
            {
                // Equal-Power-Crossfade: Phase t [0..1] für den NEUEN Track,
                // 1-t für den alten. Wir spielen aktuell noch den alten ab, also `oldVolume` regeln.
                var t = 1f - (_fadeOutTimer / _fadeOutDuration);
                var (oldVol, _) = AudioSpatial.EqualPowerCrossfade(t);
                _currentFadeVolume = oldVol;
                _soundService.SetMusicVolume(_busMixer.GetEffectiveVolume(AudioBus.Music, oldVol));
            }
            return;
        }

        // 3. Auch ohne aktiven Crossfade muss die laufende Music auf Bus-Duck-Updates reagieren.
        if (_currentMusic != null)
        {
            var liveVol = _busMixer.GetEffectiveVolume(AudioBus.Music, _currentFadeVolume);
            _soundService.SetMusicVolume(liveVol);
        }
    }

    /// <summary>
    /// Spielt Musik ab, mit Fallback auf einen zweiten Key wenn der erste nicht verfügbar ist.
    /// </summary>
    public void PlayMusicWithFallback(string primaryKey, string fallbackKey)
    {
        // Versuche primären Key — wenn der Track nicht existiert, spielt ISoundService nichts.
        // Phase 17 sollte alle Welt-Tracks bereitstellen, dann wird der Fallback obsolet.
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

        // Crossfade wenn bereits andere Musik läuft (Equal-Power, 0.5s)
        if (_currentMusic != null)
        {
            _nextMusicKey = musicKey;
            _fadeOutDuration = 0.5f;
            _fadeOutTimer = _fadeOutDuration;
            return;
        }

        var startVol = _busMixer.GetEffectiveVolume(AudioBus.Music, 1f);
        _soundService.PlayMusic(musicKey, startVol);
        _currentMusic = musicKey;
        _currentFadeVolume = 1f;
    }

    /// <summary>Hintergrundmusik stoppen und Zustand zurücksetzen.</summary>
    public void StopMusic()
    {
        _soundService.StopMusic();
        _currentMusic = null;
        _nextMusicKey = null;
        _fadeOutTimer = 0f;
    }

    /// <summary>Hintergrundmusik pausieren.</summary>
    public void PauseMusic() => _soundService.PauseMusic();

    /// <summary>Hintergrundmusik fortsetzen.</summary>
    public void ResumeMusic()
    {
        if (_musicEnabled)
            _soundService.ResumeMusic();
    }

    private void OnBusVolumeChanged(AudioBus bus, float effectiveVolume)
    {
        // Nur live anpassen wenn der Bus die laufende Musik betrifft.
        if (_currentMusic != null && (bus == AudioBus.Music || bus == AudioBus.Master))
        {
            _soundService.SetMusicVolume(_busMixer.GetEffectiveVolume(AudioBus.Music, _currentFadeVolume));
        }
    }

    public void Dispose()
    {
        _busMixer.BusVolumeChanged -= OnBusVolumeChanged;
        StopMusic();
        _soundService.Dispose();
    }
}
