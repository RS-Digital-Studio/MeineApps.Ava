namespace BomberBlast.Core.Multiplayer;

/// <summary>
/// Gameplay-State-Snapshot (Phase 30 — Multiplayer-Foundation).
///
/// <para>Schlanker State-Hash + Schlüssel-Werte für:</para>
/// <list type="bullet">
///   <item><b>Anti-Cheat-Validation</b>: Server vergleicht Client-Snapshot-Hash gegen Server-Sim-Hash.</item>
///   <item><b>Replay-Sync-Marker</b>: Periodische Snapshots im Replay zum schnellen Vorspulen.</item>
///   <item><b>Rollback-Restore</b>: Bei Mismatch wird auf den letzten konsistenten Snapshot zurückgesetzt.</item>
/// </list>
///
/// <para>Bewusst minimal: Nur das was über die Sim-Ticks-Sequenz nicht reproduzierbar ist
/// (Score, Player-Position, Lebens-Count, RNG-State). Das gesamte Grid lässt sich durch
/// Re-Simulation aus den Inputs herleiten.</para>
/// </summary>
public readonly struct GameStateSnapshot
{
    public GameStateSnapshot(
        int tickNumber,
        int player1Score,
        int player1GridX,
        int player1GridY,
        int player1Lives,
        int player2Score,
        int player2GridX,
        int player2GridY,
        int player2Lives,
        ulong rngState0,
        ulong rngState1,
        ulong rngState2,
        ulong rngState3)
    {
        TickNumber = tickNumber;
        P1Score = player1Score;
        P1GridX = (sbyte)player1GridX;
        P1GridY = (sbyte)player1GridY;
        P1Lives = (sbyte)player1Lives;
        P2Score = player2Score;
        P2GridX = (sbyte)player2GridX;
        P2GridY = (sbyte)player2GridY;
        P2Lives = (sbyte)player2Lives;
        RngS0 = rngState0;
        RngS1 = rngState1;
        RngS2 = rngState2;
        RngS3 = rngState3;
    }

    public int TickNumber { get; }
    public int P1Score { get; }
    public sbyte P1GridX { get; }
    public sbyte P1GridY { get; }
    public sbyte P1Lives { get; }
    public int P2Score { get; }
    public sbyte P2GridX { get; }
    public sbyte P2GridY { get; }
    public sbyte P2Lives { get; }
    public ulong RngS0 { get; }
    public ulong RngS1 { get; }
    public ulong RngS2 { get; }
    public ulong RngS3 { get; }

    /// <summary>
    /// Berechnet einen 64-Bit-Hash des Snapshots (FNV-1a).
    /// Wird vom Anti-Cheat-Server für Vergleich genutzt — Mismatch = Manipulation oder Desync.
    /// </summary>
    public ulong ComputeHash()
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        ulong hash = fnvOffset;
        unchecked
        {
            hash = (hash ^ (ulong)TickNumber) * fnvPrime;
            hash = (hash ^ (ulong)P1Score) * fnvPrime;
            hash = (hash ^ (uint)((P1GridX << 24) | (P1GridY << 16) | (P1Lives << 8))) * fnvPrime;
            hash = (hash ^ (ulong)P2Score) * fnvPrime;
            hash = (hash ^ (uint)((P2GridX << 24) | (P2GridY << 16) | (P2Lives << 8))) * fnvPrime;
            hash = (hash ^ RngS0) * fnvPrime;
            hash = (hash ^ RngS1) * fnvPrime;
            hash = (hash ^ RngS2) * fnvPrime;
            hash = (hash ^ RngS3) * fnvPrime;
        }
        return hash;
    }

    /// <summary>True wenn beide Snapshots denselben Hash haben (= identischer Sim-State).</summary>
    public bool IsIdenticalTo(GameStateSnapshot other)
        => ComputeHash() == other.ComputeHash();
}
