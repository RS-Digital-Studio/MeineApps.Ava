namespace BomberBlast.Services;

/// <summary>
/// Master Mode (New Game+): Erweiterter Story-Modus ab L100-Abschluss.
/// Nutzt die existierenden 100 Story-Level mit verschärften Parametern:
/// <list type="bullet">
///   <item>Gegner-Geschwindigkeit × 1.5 (gleich wie DoubleSpeed-Mutator)</item>
///   <item>Gegner-Typ-Upgrade (Ballom→Minvo, Doll→Pass, Kondoria→Pontan etc.)</item>
///   <item>+1 Gem pro erstmaligem Master-3-Sterne-Clear (100 Levels × 1G = 100G Endgame-Reward)</item>
///   <item>Unlock "master_champion"-Skin nach 100 Master-3-Stars</item>
/// </list>
///
/// <para>Per-Level-Status wird separat von Normal-Mode in Preferences persistiert
/// (JSON). IsUnlocked ist eine computed Property basierend auf
/// <see cref="IProgressService.HighestCompletedLevel"/> &gt;= 100.</para>
/// </summary>
public interface IMasterModeService : IDisposable
{
    /// <summary>Ob Master Mode freigeschaltet ist (nach L100-Abschluss im Normal-Modus).</summary>
    bool IsUnlocked { get; }

    /// <summary>Ob der User aktuell Master Mode aktiviert hat (LevelSelect-Toggle).</summary>
    bool IsActive { get; set; }

    /// <summary>Gesamt-Anzahl der im Master Mode abgeschlossenen Level (distinct Levels).</summary>
    int TotalMasterClears { get; }

    /// <summary>Gesamt-Anzahl der im Master Mode mit 3 Sternen abgeschlossenen Level (distinct).</summary>
    int TotalMaster3Stars { get; }

    /// <summary>
    /// Gibt den Master-Mode-Status für ein bestimmtes Level zurück.
    /// </summary>
    /// <param name="level">1-basierter Story-Level-Index (1-100).</param>
    /// <returns>Stars: 0-3 (0 = noch nicht im Master-Modus abgeschlossen).</returns>
    int GetMasterStars(int level);

    /// <summary>
    /// Registriert einen Level-Abschluss im Master Mode. Nur höhere Sterne werden
    /// gespeichert (keine Regression bei späterem 2-Stern-Clear nach bereits 3-Stern).
    /// Feuert <see cref="MasterLevelCleared"/>.
    /// </summary>
    /// <returns>true wenn Erst-Clear oder Stern-Verbesserung.</returns>
    bool RecordLevelCompleted(int level, int stars);

    /// <summary>Setzt alle Master-Mode-Daten zurück (z.B. für New-Game-Plus-Plus in v2.0.36).</summary>
    void Reset();

    /// <summary>
    /// Wird bei jedem Master-Level-Abschluss gefeuert (auch ohne Stern-Verbesserung).
    /// Payload: (level, stars, wasFirstClear, wasStarImprovement).
    /// </summary>
    event EventHandler<MasterLevelClearedEventArgs>? MasterLevelCleared;
}

public sealed class MasterLevelClearedEventArgs : EventArgs
{
    public int Level { get; init; }
    public int Stars { get; init; }
    public bool WasFirstClear { get; init; }
    public bool WasStarImprovement { get; init; }
}
