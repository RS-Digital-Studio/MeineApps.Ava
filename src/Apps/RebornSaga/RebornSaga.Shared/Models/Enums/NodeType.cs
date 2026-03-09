namespace RebornSaga.Models.Enums;

/// <summary>
/// Typ eines Story-Knotens. Bestimmt wie der Knoten verarbeitet und dargestellt wird.
/// </summary>
public enum NodeType
{
    Dialogue,       // Dialog-Szene mit Sprecher-Lines
    Choice,         // Auswahl-Knoten mit Verzweigungen
    Battle,         // Kampf gegen Gegner
    ClassSelect,    // Klassenwahl (Prolog)
    Shop,           // Händler-Interaktion
    Cutscene,       // Nicht-interaktive Zwischensequenz
    Overworld,      // Wechsel zur Overworld-Map
    BondScene,      // Charakter-Beziehungs-Szene
    FateChange,     // Schicksals-Wendepunkt (permanente Story-Änderung)
    SystemMessage,  // ARIA System-Nachricht
    ChapterEnd      // Kapitel-Ende
}

/// <summary>
/// Typ eines Map-Knotens auf der Overworld. Bestimmt Farbe und Verhalten.
/// </summary>
public enum MapNodeType
{
    Story,      // Gold - Hauptquest, muss besucht werden
    SideQuest,  // Silber - Optional, Bonus-EXP/Items
    Boss,       // Rot - Kampf-Knoten
    Npc,        // Blau - NPC/Shop, Bond-Szenen
    Dungeon,    // Lila - Mehrere Kämpfe hintereinander
    Rest,       // Grün - HP/MP regenerieren, Speichern
    Locked      // Grau - Kapitel nicht freigeschaltet
}
