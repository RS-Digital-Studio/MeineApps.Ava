namespace BomberBlast.Core.Multiplayer;

/// <summary>
/// Ring-Buffer für Multiplayer-Input-Snapshots (Phase 30 — Multiplayer-Foundation).
///
/// <para>Speichert die letzten N Sim-Ticks an Inputs pro Spieler. Wird verwendet für:</para>
/// <list type="bullet">
///   <item><b>Lag-Compensation</b>: Server kann den Input mit Tick-Stempel rückwirkend anwenden.</item>
///   <item><b>Replay-Live-Capture</b>: Während des Live-Spiels werden Inputs gepuffert für ReplayCapture-Export.</item>
///   <item><b>Rollback-Netcode</b> (Future): Bei Server-Snapshot-Mismatch lässt sich der lokale State per Re-Sim
///       der gepufferten Inputs reproduzieren.</item>
/// </list>
///
/// <para>Default-Buffer-Size: 120 Ticks (= 2s @ 60 Hz). Reicht für typische Mobile-Network-Latenzen.</para>
/// </summary>
public sealed class InputBuffer
{
    private readonly PlayerInputSnapshot[] _buffer;
    private readonly int _capacity;
    private int _writeIndex;
    private int _count;

    /// <summary>Anzahl gepufferter Snapshots (max <see cref="Capacity"/>).</summary>
    public int Count => _count;

    /// <summary>Maximale Buffer-Größe.</summary>
    public int Capacity => _capacity;

    public InputBuffer(int capacity = 120)
    {
        _capacity = Math.Max(1, capacity);
        _buffer = new PlayerInputSnapshot[_capacity];
    }

    /// <summary>Schreibt einen neuen Snapshot in den Buffer (überschreibt ältesten bei Voll).</summary>
    public void Push(PlayerInputSnapshot snapshot)
    {
        _buffer[_writeIndex] = snapshot;
        _writeIndex = (_writeIndex + 1) % _capacity;
        if (_count < _capacity) _count++;
    }

    /// <summary>
    /// Liest den jüngsten Snapshot. Liefert Empty wenn Buffer leer.
    /// </summary>
    public PlayerInputSnapshot PeekLatest()
    {
        if (_count == 0) return PlayerInputSnapshot.Empty();
        var idx = (_writeIndex - 1 + _capacity) % _capacity;
        return _buffer[idx];
    }

    /// <summary>
    /// Liest einen Snapshot aus der Vergangenheit. <paramref name="ticksAgo"/>=0 ist der jüngste.
    /// Liefert Empty wenn nicht genug Historie vorhanden.
    /// </summary>
    public PlayerInputSnapshot PeekHistorical(int ticksAgo)
    {
        if (ticksAgo < 0 || ticksAgo >= _count) return PlayerInputSnapshot.Empty();
        var idx = (_writeIndex - 1 - ticksAgo + _capacity) % _capacity;
        return _buffer[idx];
    }

    /// <summary>Setzt den Buffer zurück.</summary>
    public void Clear()
    {
        _writeIndex = 0;
        _count = 0;
    }
}
