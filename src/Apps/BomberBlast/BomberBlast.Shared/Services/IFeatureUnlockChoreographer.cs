namespace BomberBlast.Services;

/// <summary>
/// Feature-Unlock-Choreographer (.4 .
///
/// <para>
/// Spielt eine kurze, prominente Animation ab wenn der Spieler ein neues Feature
/// freischaltet (z.B. Dungeon-Mode bei L20, Card-Crafting bei L40, Master-Mode bei L100).
/// Statt im Hintergrund leise freizuschalten — der User soll wissen, dass es etwas
/// Neues gibt.
/// </para>
///
/// <para>
/// Trigger-Quellen:
/// <list type="bullet">
/// <item>OnLevelComplete: Welt-/Level-Schwellen freischalten Modi (Dungeon, BossRush, Master)</item>
/// <item>OnAchievementUnlocked: Achievement-bedingte Features (Champion-Skin nach 100 Master-3-Sternen)</item>
/// </list>
/// </para>
///
/// <para>
/// Pattern: Vollbild-Overlay "NEU: Dungeon Mode freigeschaltet!" + grosses Icon +
/// Stinger-SFX + 1500ms Auto-Close + "Jetzt entdecken"-CTA. Queue (wenn 2 Features
/// gleichzeitig freischalten, hintereinander zeigen).
/// </para>
///
/// <para>
/// Pref-Flag: jedes Feature-Unlock-Overlay wird NUR EINMAL gezeigt — beim ersten
/// Trigger nach Freischaltung. Re-Spiel des gleichen Levels triggert nichts.
/// </para>
/// </summary>
public interface IFeatureUnlockChoreographer
{
    /// <summary>
    /// Wird vom GameEngine/MainMenu nach einem Level-Complete aufgerufen.
    /// Choreographer prueft welche Features durch dieses Level neu freigeschaltet wurden.
    /// </summary>
    /// <param name="completedLevel">Soeben abgeschlossenes Level (1-100).</param>
    void OnLevelComplete(int completedLevel);

    /// <summary>
    /// Wird vom AchievementService nach einem Achievement-Unlock aufgerufen.
    /// </summary>
    /// <param name="achievementId">ID des freigeschalteten Achievements.</param>
    void OnAchievementUnlocked(string achievementId);

    /// <summary>
    /// Wird gefeuert wenn ein neues Feature freigeschaltet wurde und der Choreographer
    /// das Overlay anzeigen will. Der View-Layer (MainViewModel) subscribed darauf
    /// und navigiert zum Overlay-Screen.
    /// </summary>
    event Action<FeatureUnlockEvent>? FeatureUnlocked;

    /// <summary>
    /// Markiert das aktuelle Feature als abgeschlossen — Choreographer kann das naechste
    /// in der Queue verarbeiten. Wird vom Overlay-VM nach "OK"/Auto-Close aufgerufen.
    /// </summary>
    void DismissCurrent();
}

/// <summary>Daten fuer ein einzelnes Feature-Unlock-Event.</summary>
public sealed class FeatureUnlockEvent
{
    /// <summary>Stable ID — wird als Pref-Key verwendet (nur einmal anzeigen).</summary>
    public required string FeatureId { get; init; }
    /// <summary>RESX-Key fuer den Headline-Text ("NEU: Dungeon Mode!").</summary>
    public required string TitleKey { get; init; }
    /// <summary>RESX-Key fuer den Beschreibungstext.</summary>
    public required string DescriptionKey { get; init; }
    /// <summary>Optionaler Asset-Pfad fuer Hero-Image (z.B. AI-Bitmap).</summary>
    public string? HeroAssetPath { get; init; }
    /// <summary>Optionaler Navigation-Ziel ("DungeonView", "BossRushView" etc.) fuer den CTA-Button.</summary>
    public string? CtaNavTarget { get; init; }
    /// <summary>RESX-Key fuer den CTA-Button-Text ("Jetzt entdecken").</summary>
    public string? CtaTextKey { get; init; }
}
