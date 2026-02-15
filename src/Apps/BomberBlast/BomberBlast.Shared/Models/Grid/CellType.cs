namespace BomberBlast.Models.Grid;

/// <summary>
/// Types of cells in the game grid
/// </summary>
public enum CellType
{
    /// <summary>Empty floor - player and enemies can walk here</summary>
    Empty,

    /// <summary>Indestructible wall - blocks movement and explosions</summary>
    Wall,

    /// <summary>Destructible block - can be destroyed by explosions, may contain power-up</summary>
    Block,

    /// <summary>Exit portal - appears after all enemies are defeated</summary>
    Exit,

    /// <summary>Eis-Boden (Welt 2: Industrial) - Spieler/Gegner rutschen weiter bis Hindernis</summary>
    Ice,

    /// <summary>Förderband (Welt 3: Cavern) - schiebt Entities in eine Richtung</summary>
    Conveyor,

    /// <summary>Teleporter (Welt 4: Sky) - transportiert zum gepaarten Teleporter</summary>
    Teleporter,

    /// <summary>Lava-Riss (Welt 5: Inferno) - pulsiert periodisch, tötet bei aktivem Zustand</summary>
    LavaCrack
}
