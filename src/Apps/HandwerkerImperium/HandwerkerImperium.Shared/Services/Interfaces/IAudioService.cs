namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Handles audio playback for sound effects and music.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Whether sound effects are enabled.
    /// </summary>
    bool SoundEnabled { get; set; }

    /// <summary>
    /// Whether music is enabled.
    /// </summary>
    bool MusicEnabled { get; set; }

    /// <summary>F-19: SFX-Volume 0..1 (Default 1.0). Wird auf SoundPool angewandt (Android).</summary>
    float SfxVolume { get; set; }

    /// <summary>F-19: Music-Volume 0..1 (Default 1.0). Wird auf MediaPlayer angewandt (Android).</summary>
    float MusicVolume { get; set; }

    /// <summary>
    /// Plays a sound effect.
    /// </summary>
    Task PlaySoundAsync(GameSound sound);

    /// <summary>
    /// Plays background music (loops). Legacy-Variante mit Asset-Dateinamen.
    /// </summary>
    Task PlayMusicAsync(string musicFile);

    /// <summary>
    /// Plays background music (loops) mit MusicTrack-Enum + optionalem Crossfade (P2.3).
    /// </summary>
    Task PlayMusicAsync(MusicTrack track, bool crossfade = true);

    /// <summary>
    /// Stops the background music. Mit optionalem Fade-Out (P2.3).
    /// </summary>
    void StopMusic(bool fadeOut = false);

    /// <summary>
    /// Pausiert die Musik (z.B. bei AudioFocus-Loss durch Telefonanruf). Resume via <see cref="ResumeMusic"/>.
    /// </summary>
    void PauseMusic();

    /// <summary>
    /// Setzt eine via <see cref="PauseMusic"/> pausierte Musik fort.
    /// </summary>
    void ResumeMusic();

    /// <summary>
    /// Triggers haptic feedback.
    /// </summary>
    void Vibrate(VibrationType type);
}

/// <summary>
/// Stabile Identifier fuer Hintergrund-Musik-Loops (P2.3). Die Implementierung
/// mappt jeden Track auf eine Datei in <c>Assets/Music/</c>. Lizenzpflichtige
/// Asset-Dateien werden separat besorgt (Robert: ArtList/Epidemic Sound).
/// </summary>
public enum MusicTrack
{
    /// <summary>Kein aktiver Track — Aufruf entspricht <see cref="IAudioService.StopMusic"/>.</summary>
    None,

    /// <summary>Default-Loop fuer Werkstatt/Dashboard (warm, akustisch).</summary>
    IdleWorkshop,

    /// <summary>Energischer Loop fuer Mini-Games / Tournament.</summary>
    BossOrTournament,

    /// <summary>Kurzer Triumph-Loop fuer Prestige-Cinematic + Daily Reward.</summary>
    Celebration
}

/// <summary>
/// Sound effect types in the game.
/// </summary>
public enum GameSound
{
    /// <summary>Button tap</summary>
    ButtonTap,

    /// <summary>Money earned (cha-ching!)</summary>
    MoneyEarned,

    /// <summary>Level up fanfare</summary>
    LevelUp,

    /// <summary>Workshop upgraded</summary>
    Upgrade,

    /// <summary>Worker hired</summary>
    WorkerHired,

    /// <summary>Mini-game perfect rating</summary>
    Perfect,

    /// <summary>Mini-game good rating</summary>
    Good,

    /// <summary>Mini-game miss</summary>
    Miss,

    /// <summary>Order completed</summary>
    OrderComplete,

    /// <summary>Sawing sound</summary>
    Sawing,

    /// <summary>Hammering sound</summary>
    Hammering,

    /// <summary>Drilling sound</summary>
    Drilling,

    /// <summary>Countdown-Tick (3-2-1)</summary>
    Countdown,

    /// <summary>Muenze einsammeln</summary>
    CoinCollect,

    /// <summary>Combo-Treffer im MiniGame</summary>
    ComboHit
}

/// <summary>
/// Haptic feedback types.
/// </summary>
public enum VibrationType
{
    /// <summary>Light tap</summary>
    Light,

    /// <summary>Medium impact</summary>
    Medium,

    /// <summary>Heavy impact</summary>
    Heavy,

    /// <summary>Success pattern</summary>
    Success,

    /// <summary>Error pattern</summary>
    Error,

    /// <summary>Level-Up Pattern (laenger)</summary>
    LevelUp,

    /// <summary>MiniGame Treffer (sehr kurz)</summary>
    MiniGameHit
}
