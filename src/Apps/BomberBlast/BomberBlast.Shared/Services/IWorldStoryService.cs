namespace BomberBlast.Services;

/// <summary>
/// Mini-Story-Beats pro Welt (Sprint 6.2 AAA-Audit #16).
///
/// <para>
/// Liefert Cutscene-Daten fuer Welt-Start (Welcome) und Welt-Ende (Outro mit Cliffhanger).
/// Texte sind RESX-lokalisiert in 6 Sprachen.
/// </para>
///
/// <para>
/// Verwendung:
/// <list type="bullet">
/// <item>Welt-Start (LevelN1): GameEngine ruft GetIntro(world) auf, zeigt 2-Saetze-Text + Standbild + Stinger</item>
/// <item>Welt-Boss-Sieg (LevelN10 + Boss): GameEngine ruft GetOutro(world) auf, zeigt Cliffhanger</item>
/// </list>
/// </para>
///
/// <para>
/// "Skip"-Button respektiert Hardcore-Spieler — sie koennen die Cutscene ueberspringen.
/// HasSeenIntro/HasSeenOutro Pref-Flags verhindern doppelte Anzeige bei Re-Spielen.
/// </para>
/// </summary>
public interface IWorldStoryService
{
    /// <summary>
    /// Liefert die Intro-Cutscene fuer eine Welt (1-10) oder null wenn nicht definiert.
    /// </summary>
    StoryCutscene? GetIntro(int worldId);

    /// <summary>
    /// Liefert die Outro-Cutscene mit Cliffhanger zur naechsten Welt (1-9, fuer 10 nicht definiert).
    /// </summary>
    StoryCutscene? GetOutro(int worldId);

    /// <summary>True wenn der User die Intro-Cutscene fuer diese Welt schon gesehen hat.</summary>
    bool HasSeenIntro(int worldId);

    /// <summary>True wenn der User die Outro-Cutscene fuer diese Welt schon gesehen hat.</summary>
    bool HasSeenOutro(int worldId);

    /// <summary>Markiert die Intro als gesehen (one-shot pro Welt).</summary>
    void MarkIntroSeen(int worldId);

    /// <summary>Markiert die Outro als gesehen (one-shot pro Welt).</summary>
    void MarkOutroSeen(int worldId);
}

/// <summary>Daten einer Story-Cutscene (Intro oder Outro).</summary>
public sealed class StoryCutscene
{
    /// <summary>Welt-ID 1-10 (kontext fuer Standbild-Asset-Auswahl).</summary>
    public required int WorldId { get; init; }
    /// <summary>RESX-Key fuer den 2-Satz-Text. Wird im Cutscene-Renderer aus AppStrings geholt.</summary>
    public required string TextKey { get; init; }
    /// <summary>SoundManager-Stinger-Konstante (z.B. STINGER_BOSS_REVEAL fuer dramatische Welt-Intros).</summary>
    public string? StingerKey { get; init; }
    /// <summary>Optional: Pfad zu einem Standbild-Asset (z.B. AI-WebP).</summary>
    public string? StillImagePath { get; init; }
}
