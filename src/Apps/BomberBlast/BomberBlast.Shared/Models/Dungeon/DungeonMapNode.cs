namespace BomberBlast.Models.Dungeon;

/// <summary>
/// Ein Knoten in der Dungeon-Map (Slay the Spire-Inspiration).
/// Jeder Knoten repräsentiert einen Floor mit Raum-Typ und optionalem Modifikator.
/// </summary>
public class DungeonMapNode
{
    /// <summary>Floor-Nummer (1-basiert)</summary>
    public int Floor { get; set; }

    /// <summary>Spalten-Position (0-2)</summary>
    public int Column { get; set; }

    /// <summary>Raum-Typ dieses Knotens</summary>
    public DungeonRoomType RoomType { get; set; }

    /// <summary>Floor-Modifikator (None = keiner)</summary>
    public DungeonFloorModifier Modifier { get; set; }

    /// <summary>Challenge-Modus (nur relevant wenn RoomType == Challenge)</summary>
    public DungeonChallengeMode ChallengeMode { get; set; }

    /// <summary>Ob dieser Knoten bereits abgeschlossen wurde</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Ob dies der aktuelle Knoten ist</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Ob dieser Knoten vom Spieler erreichbar ist (verbunden mit aktuellem Pfad)</summary>
    public bool IsReachable { get; set; }

    /// <summary>Spalten-Indizes der verbundenen Knoten in der nächsten Reihe</summary>
    public List<int> ConnectsTo { get; set; } = [];
}

/// <summary>
/// Gesamte Map-Daten für einen Dungeon-Run (serialisierbar).
/// </summary>
public class DungeonMapData
{
    /// <summary>Alle Reihen der Map (Index 0 = Floor 1, etc.)</summary>
    public List<List<DungeonMapNode>> Rows { get; set; } = [];

    /// <summary>Gewählter Spalten-Index pro Floor (-1 = nicht gewählt)</summary>
    public List<int> ChosenColumns { get; set; } = [];
}
