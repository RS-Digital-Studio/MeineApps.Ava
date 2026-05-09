namespace BomberBlast.Core.Audio;

/// <summary>
/// 7-Kanal Audio-Bus-System nach Studio-Vorbild (Brawl Stars / Genshin).
/// Jeder Sound durchläuft genau einen Bus. Das finale Volume ist:
/// <c>busVolume[bus] * masterVolume * playVolume * busDuckMultiplier</c>.
/// Ducking erlaubt automatisches Absenken anderer Buses bei wichtigen Cues
/// (z.B. Music duckt auf 30% bei aktiver Voice-Line oder Cinematic-Stinger).
/// </summary>
public enum AudioBus
{
    /// <summary>Master-Mix — alle anderen Buses laufen hier hindurch.</summary>
    Master = 0,

    /// <summary>Hintergrund-Musik (Welt-Tracks, Boss-Music, Menu-Theme).</summary>
    Music = 1,

    /// <summary>Atmosphärische Loops (Wind, Wasser, Höhlen-Hall, Lava-Brodeln).</summary>
    Ambient = 2,

    /// <summary>Gameplay-SFX (Explosionen, Bomben, PowerUps, Gegner-Tod).</summary>
    Sfx = 3,

    /// <summary>UI-Sounds (Menu-Tap, Confirm, Hover, Currency-Pop).</summary>
    Ui = 4,

    /// <summary>Voice-Lines (Boss-Roar, Player-Reactions, Announcer).</summary>
    Voice = 5,

    /// <summary>Cinematic-Stinger (Boss-Reveal, Victory-Sting, Ultra-Combo).</summary>
    Cinematic = 6,
}
