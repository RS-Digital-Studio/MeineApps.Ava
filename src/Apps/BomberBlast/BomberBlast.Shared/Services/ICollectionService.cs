using BomberBlast.Models.Collection;
using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet das Sammlungs-Album: Gegner, Bosse, PowerUps, Karten, Kosmetik.
/// Aggregiert Daten aus ICardService, ICustomizationService + eigenes Encounter/Defeat-Tracking.
/// </summary>
public interface ICollectionService
{
    /// <summary>Alle Sammlungs-Einträge (aggregiert, aktuell)</summary>
    IReadOnlyList<CollectionEntry> GetEntries(CollectionCategory category);

    /// <summary>Gesamtfortschritt in Prozent (0-100)</summary>
    int GetTotalProgressPercent();

    /// <summary>Fortschritt pro Kategorie in Prozent (0-100)</summary>
    int GetCategoryProgressPercent(CollectionCategory category);

    /// <summary>Anzahl entdeckter Einträge pro Kategorie</summary>
    int GetDiscoveredCount(CollectionCategory category);

    /// <summary>Gesamtanzahl Einträge pro Kategorie</summary>
    int GetTotalCount(CollectionCategory category);

    /// <summary>Gegner als gesichtet/angetroffen melden</summary>
    void RecordEnemyEncounter(EnemyType type);

    /// <summary>Gegner als besiegt melden</summary>
    void RecordEnemyDefeat(EnemyType type);

    /// <summary>Boss als angetroffen melden</summary>
    void RecordBossEncounter(BossType type);

    /// <summary>Boss als besiegt melden (mit Kampfzeit)</summary>
    void RecordBossDefeat(BossType type, float timeSeconds);

    /// <summary>PowerUp als eingesammelt melden</summary>
    void RecordPowerUpCollected(string powerUpId);

    /// <summary>Meilensteine abrufen</summary>
    IReadOnlyList<CollectionMilestone> GetMilestones();

    /// <summary>Meilenstein beanspruchen (gibt Coins/Gems)</summary>
    bool TryClaimMilestone(int percentRequired);

    /// <summary>Erzwingt Speichern wenn Dirty-Flag gesetzt (z.B. am Level-Ende)</summary>
    void FlushIfDirty();

    /// <summary>Wird ausgelöst wenn sich Daten ändern (Encounter, Defeat, etc.)</summary>
    event EventHandler? CollectionChanged;
}
