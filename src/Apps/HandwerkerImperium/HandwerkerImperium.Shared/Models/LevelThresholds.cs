namespace HandwerkerImperium.Models;

/// <summary>
/// Zentrale Konstanten für alle Level-basierten Schwellenwerte.
/// Verhindert Magic Numbers und Duplikation über Services/ViewModels/Renderer hinweg.
/// </summary>
public static class LevelThresholds
{
    // ═══════════════════════════════════════════════════════════════════════
    // PROGRESSIVE DISCLOSURE (UI-Sichtbarkeit)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>BannerStrip (Events/Boosts) im Dashboard anzeigen.</summary>
    public const int BannerStrip = 3;

    /// <summary>QuickJobs-Tab im Dashboard freischalten.</summary>
    public const int QuickJobs = 5;

    /// <summary>Crafting + Forschung im Imperium-Tab anzeigen.</summary>
    public const int CraftingResearch = 8;

    /// <summary>Vorarbeiter Quick-Access im Imperium-Tab anzeigen.</summary>
    public const int ManagerSection = 10;

    /// <summary>Meisterwerkzeuge Quick-Access im Imperium-Tab anzeigen.</summary>
    public const int MasterToolsSection = 20;

    // ═══════════════════════════════════════════════════════════════════════
    // AUTOMATISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Auto-Collect: Lieferungen automatisch einsammeln.</summary>
    public const int AutoCollect = 15;

    /// <summary>Auto-Accept: Besten Auftrag automatisch annehmen.</summary>
    public const int AutoAccept = 25;

    /// <summary>Auto-Assign: Idle Worker automatisch zuweisen (inkl. Auto-Rest bei Fatigue > 80%).</summary>
    public const int AutoAssign = 20;

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-FREISCHALTUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Tab-Unlock-Level in Tab-Reihenfolge: Werkstatt(0), Imperium(1), Missionen(2), Gilde(3), Shop(4).</summary>
    public static readonly int[] TabUnlockLevels = [1, 5, 8, 15, 3];

    // ═══════════════════════════════════════════════════════════════════════
    // KONTEXTUELLE HINTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Hint: Arbeiter einstellen.</summary>
    public const int HintWorkerUnlock = 3;

    /// <summary>Hint: QuickJobs verfügbar.</summary>
    public const int HintQuickJobs = 5;

    /// <summary>Hint: Crafting verfügbar.</summary>
    public const int HintCrafting = 8;

    /// <summary>Hint: Vorarbeiter verfügbar.</summary>
    public const int HintManagerUnlock = 10;

    /// <summary>Hint: Automatisierung verfügbar.</summary>
    public const int HintAutomation = 15;

    /// <summary>Hint: Meisterwerkzeuge verfügbar.</summary>
    public const int HintMasterTools = 20;

    /// <summary>Hint: Prestige verfügbar.</summary>
    public const int HintPrestige = 30;

    // ═══════════════════════════════════════════════════════════════════════
    // FEATURE-GATES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Prestige-Shop freischalten (oder wenn bereits prestigiert).</summary>
    public const int PrestigeShopUnlock = 500;

    /// <summary>Tutorial-Hint (pulsierender Rahmen) nur unter diesem Level anzeigen.</summary>
    public const int TutorialHintMaxLevel = 3;

    /// <summary>Workshop-Level-Milestone ab dem Zeremonien gezeigt werden.</summary>
    public const int WorkshopCeremonyThreshold = 50;

    // ═══════════════════════════════════════════════════════════════════════
    // REPUTATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Reputation-Badge anzeigen wenn unter diesem Wert (Warnung).</summary>
    public const int ReputationWarningThreshold = 50;

    /// <summary>Reputation-Badge anzeigen wenn über diesem Wert (Highlight).</summary>
    public const int ReputationHighlightThreshold = 80;

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN-VALIDIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Minimales Spieler-Level (Exploit-Schutz).</summary>
    public const int MinPlayerLevel = 1;

    /// <summary>Maximales Spieler-Level (Hard-Cap).</summary>
    public const int MaxPlayerLevel = 1500;
}
