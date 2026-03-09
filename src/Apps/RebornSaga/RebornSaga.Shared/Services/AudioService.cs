namespace RebornSaga.Services;

using MeineApps.Core.Ava.Services;

/// <summary>
/// Zentrale Audio-Verwaltung für RebornSaga.
/// Desktop-Stub: Kein Sound. Android: Wird durch AndroidAudioService ersetzt (Factory-Pattern).
/// Speichert SFX/BGM/Vibrations-Einstellungen in Preferences.
/// </summary>
public class AudioService : IAudioService
{
    private readonly IPreferencesService _preferences;

    private bool _sfxEnabled;
    private bool _bgmEnabled;
    private bool _vibrationEnabled;
    private float _sfxVolume;
    private float _bgmVolume;

    public bool SfxEnabled
    {
        get => _sfxEnabled;
        set
        {
            _sfxEnabled = value;
            _preferences.Set("audio_sfx_enabled", value);
        }
    }

    public bool BgmEnabled
    {
        get => _bgmEnabled;
        set
        {
            _bgmEnabled = value;
            _preferences.Set("audio_bgm_enabled", value);
            if (!value) StopBgm();
        }
    }

    public bool VibrationEnabled
    {
        get => _vibrationEnabled;
        set
        {
            _vibrationEnabled = value;
            _preferences.Set("audio_vibration_enabled", value);
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Math.Clamp(value, 0f, 1f);
            _preferences.Set("audio_sfx_volume", _sfxVolume);
        }
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Math.Clamp(value, 0f, 1f);
            _preferences.Set("audio_bgm_volume", _bgmVolume);
            OnBgmVolumeChanged(_bgmVolume);
        }
    }

    public string? CurrentBgm { get; protected set; }

    public AudioService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _sfxEnabled = _preferences.Get("audio_sfx_enabled", true);
        _bgmEnabled = _preferences.Get("audio_bgm_enabled", true);
        _vibrationEnabled = _preferences.Get("audio_vibration_enabled", true);
        _sfxVolume = _preferences.Get("audio_sfx_volume", 0.8f);
        _bgmVolume = _preferences.Get("audio_bgm_volume", 0.5f);
    }

    /// <summary>Spielt einen Soundeffekt (Desktop-Stub: no-op).</summary>
    public virtual void PlaySfx(GameSfx sfx) { }

    /// <summary>Startet Hintergrundmusik im Loop (Desktop-Stub: no-op).</summary>
    public virtual void PlayBgm(string musicId)
    {
        CurrentBgm = musicId;
    }

    /// <summary>Stoppt die aktuelle Hintergrundmusik (Desktop-Stub: no-op).</summary>
    public virtual void StopBgm()
    {
        CurrentBgm = null;
    }

    /// <summary>Haptisches Feedback (Desktop-Stub: no-op).</summary>
    public virtual void Vibrate(int durationMs = 30) { }

    /// <summary>Wird aufgerufen wenn sich die BGM-Lautstärke ändert. Override in Android.</summary>
    protected virtual void OnBgmVolumeChanged(float volume) { }

    /// <summary>Gibt native Ressourcen frei (Desktop: nichts).</summary>
    public virtual void Dispose() { }
}

/// <summary>
/// Interface für Audio-Service (DI + Testbarkeit).
/// </summary>
public interface IAudioService : IDisposable
{
    /// <summary>SFX an/aus.</summary>
    bool SfxEnabled { get; set; }

    /// <summary>BGM an/aus.</summary>
    bool BgmEnabled { get; set; }

    /// <summary>Vibration an/aus.</summary>
    bool VibrationEnabled { get; set; }

    /// <summary>SFX-Lautstärke (0..1).</summary>
    float SfxVolume { get; set; }

    /// <summary>BGM-Lautstärke (0..1).</summary>
    float BgmVolume { get; set; }

    /// <summary>Aktuell laufende BGM-ID (null = keine).</summary>
    string? CurrentBgm { get; }

    /// <summary>Spielt einen Soundeffekt.</summary>
    void PlaySfx(GameSfx sfx);

    /// <summary>Startet Hintergrundmusik (loop). Stoppt vorherige BGM automatisch.</summary>
    void PlayBgm(string musicId);

    /// <summary>Stoppt die aktuelle Hintergrundmusik.</summary>
    void StopBgm();

    /// <summary>Haptisches Feedback (Kampf-Treffer etc.).</summary>
    void Vibrate(int durationMs = 30);
}

/// <summary>
/// Alle Soundeffekte im Spiel.
/// Mapping zu Dateien in AndroidAudioService.SoundFileMap.
/// </summary>
public enum GameSfx
{
    // --- UI ---
    /// <summary>Button-Tap / Navigation</summary>
    ButtonTap,
    /// <summary>Menü/Fenster öffnen</summary>
    MenuOpen,
    /// <summary>Menü/Fenster schließen</summary>
    MenuClose,
    /// <summary>Bestätigung (OK)</summary>
    Confirm,
    /// <summary>Fehler / ungültige Aktion</summary>
    Error,

    // --- Dialog ---
    /// <summary>Textzeichen-Tick (Typewriter)</summary>
    TextTick,
    /// <summary>Entscheidungs-Auswahl</summary>
    ChoiceSelect,

    // --- Kampf ---
    /// <summary>Schwert-Slash (physisch)</summary>
    SwordSlash,
    /// <summary>Magie wirken</summary>
    MagicCast,
    /// <summary>Treffer (normal)</summary>
    HitImpact,
    /// <summary>Kritischer Treffer</summary>
    CriticalHit,
    /// <summary>Ausweichen / Miss</summary>
    Dodge,
    /// <summary>Gegner besiegt</summary>
    EnemyDefeat,
    /// <summary>Verteidigung / Block</summary>
    Block,
    /// <summary>Heilung</summary>
    Heal,
    /// <summary>Buff anwenden</summary>
    BuffApply,
    /// <summary>Debuff anwenden</summary>
    DebuffApply,

    // --- Fortschritt ---
    /// <summary>Level Up Fanfare</summary>
    LevelUp,
    /// <summary>Skill freigeschaltet</summary>
    SkillUnlock,
    /// <summary>Item erhalten</summary>
    ItemPickup,
    /// <summary>Gold erhalten</summary>
    GoldCollect,
    /// <summary>Kodex-Eintrag entdeckt</summary>
    CodexDiscover,
    /// <summary>Bond-Level gestiegen</summary>
    BondUp,
    /// <summary>Kapitel abgeschlossen</summary>
    ChapterComplete,

    // --- Spezial ---
    /// <summary>ARIA Glitch / System-Störung</summary>
    GlitchSound,
    /// <summary>Zeitriss-Effekt</summary>
    TimeRift,
    /// <summary>Karma-Verschiebung</summary>
    KarmaShift,
    /// <summary>Ultimate-Skill aktiviert</summary>
    UltimateActivate
}

/// <summary>
/// Vordefinierte BGM-IDs für Szenen.
/// Mapping zu Asset-Pfaden in AndroidAudioService.
/// </summary>
public static class BgmTracks
{
    public const string TitleScreen = "bgm_title";
    public const string Village = "bgm_village";
    public const string Dungeon = "bgm_dungeon";
    public const string BossBattle = "bgm_boss_battle";
    public const string NormalBattle = "bgm_normal_battle";
    public const string Emotional = "bgm_emotional";
    public const string AriaSystem = "bgm_aria_system";
    public const string OverworldMap = "bgm_overworld";
    public const string Dreamworld = "bgm_dreamworld";
    public const string PrologBattle = "bgm_prolog_battle";
}
