namespace BomberBlast.Core.Multiplayer;

/// <summary>
/// Multiplayer-Modus-Typ (Phase 30 — AAA-Audit C1).
/// Foundation für künftige Co-Op- / PvP- / Async-Replay-Spielformen.
/// </summary>
public enum MultiplayerMode
{
    /// <summary>Single-Player (Default — alle existierenden Modi).</summary>
    Single = 0,

    /// <summary>2P-Lokal-Co-Op: zwei Spieler auf demselben Gerät, geteilter Input.
    /// Input-Source: 2× lokale Joysticks ODER Keyboard+Gamepad-Split.</summary>
    LocalCoop = 1,

    /// <summary>2P-Lokal-Versus: zwei Spieler kämpfen gegeneinander auf demselben Grid.</summary>
    LocalVersus = 2,

    /// <summary>Async-PvP via Ghost-Replay: Spieler-A spielt zuerst, Spieler-B sieht A's Replay live + spielt parallel.
    /// Voraussetzung: deterministische Engine (Phase 18b/18c FixedTimestep).</summary>
    AsyncGhost = 3,

    /// <summary>Real-Time-PvP via Pi-Server (BingXBot.Server-Pattern). 6-10 Wochen Backend-Sprint.</summary>
    RealtimeServer = 4,
}

/// <summary>
/// Spieler-Slot-Typ (Phase 30 — Co-Op + Versus).
/// </summary>
public enum PlayerSlot
{
    Player1 = 0,
    Player2 = 1,
}

/// <summary>
/// Co-Op-Spawn-Position-Konvention. Bei 2P-Co-Op spawnen Spieler in gegenüberliegenden Ecken.
/// </summary>
public static class MultiplayerSpawnPositions
{
    /// <summary>Player 1 — links oben (Standard-Single-Player-Position).</summary>
    public static readonly (int x, int y) Player1 = (1, 1);

    /// <summary>Player 2 — rechts unten (gegenüber).</summary>
    public static readonly (int x, int y) Player2 = (13, 8); // 15x10 Grid, leicht innen

    public static (int x, int y) GetSpawn(PlayerSlot slot) => slot switch
    {
        PlayerSlot.Player1 => Player1,
        PlayerSlot.Player2 => Player2,
        _ => Player1,
    };
}
