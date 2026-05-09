using BomberBlast.Models.Entities;

namespace BomberBlast.Core.Multiplayer;

/// <summary>
/// Pro-Tick-Input-Snapshot eines Spielers (Phase 30 — Multiplayer-Foundation).
///
/// <para>Wird verwendet für:</para>
/// <list type="bullet">
///   <item>Co-Op: Beide Spieler-Snapshots werden im Sim-Tick aufgenommen + auf jeweiligen Player angewandt.</item>
///   <item>Async-Ghost: Spieler-B's Replay-Stream liefert P1-Snapshots aus dem A-Run.</item>
///   <item>Real-Time-PvP: Wire-Format für Pi-Server-SignalR (12 Bytes pro Tick × 60 Hz × 2 Spieler = 1.4 KB/s).</item>
/// </list>
/// </summary>
public readonly struct PlayerInputSnapshot
{
    public PlayerInputSnapshot(PlayerSlot slot, Direction direction, bool bombPressed, bool detonatePressed, bool toggleSpecialBomb = false)
    {
        Slot = slot;
        Direction = direction;
        BombPressed = bombPressed;
        DetonatePressed = detonatePressed;
        ToggleSpecialBomb = toggleSpecialBomb;
    }

    public PlayerSlot Slot { get; }
    public Direction Direction { get; }
    public bool BombPressed { get; }
    public bool DetonatePressed { get; }
    public bool ToggleSpecialBomb { get; }

    /// <summary>Liefert einen leeren Snapshot (kein Input).</summary>
    public static PlayerInputSnapshot Empty(PlayerSlot slot = PlayerSlot.Player1)
        => new(slot, Direction.None, false, false, false);

    /// <summary>
    /// Packt den Snapshot in 2 Bytes für Wire-Format.
    /// Byte 0: Slot (Bit 7) + Direction (Bit 0-2) + BombPressed (Bit 3) + DetonatePressed (Bit 4) + ToggleSpecial (Bit 5)
    /// Byte 1: Reserve (Future: PowerUp-Switch, Card-Cycle, etc.)
    /// </summary>
    public ushort ToWireFormat()
    {
        ushort packed = 0;
        packed |= (ushort)((int)Direction & 0b111);
        if (BombPressed) packed |= 1 << 3;
        if (DetonatePressed) packed |= 1 << 4;
        if (ToggleSpecialBomb) packed |= 1 << 5;
        if (Slot == PlayerSlot.Player2) packed |= 1 << 7;
        return packed;
    }

    /// <summary>Liest einen Snapshot aus dem Wire-Format.</summary>
    public static PlayerInputSnapshot FromWireFormat(ushort packed)
    {
        var slot = (packed & (1 << 7)) != 0 ? PlayerSlot.Player2 : PlayerSlot.Player1;
        var dirRaw = packed & 0b111;
        var dir = dirRaw is >= 0 and <= 4 ? (Direction)dirRaw : Direction.None;
        return new PlayerInputSnapshot(
            slot,
            dir,
            (packed & (1 << 3)) != 0,
            (packed & (1 << 4)) != 0,
            (packed & (1 << 5)) != 0
        );
    }
}
