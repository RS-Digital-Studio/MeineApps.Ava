using BomberBlast.Models.Entities;

namespace BomberBlast.Core;

/// <summary>
/// Replay-Capture-System (Phase 18b — AAA-Audit Replay-Foundation).
///
/// <para>Zeichnet pro Sim-Tick die Spieler-Inputs als kompaktes Frame-Stream auf. Reicht zusammen
/// mit dem RNG-Seed aus, um das gesamte Spielgeschehen deterministisch zu reproduzieren —
/// Voraussetzung für Anti-Cheat-Validation, Replay-Sharing, Async-PvP-Ghost-Replays.</para>
///
/// <para>Format: 1 Byte pro Tick — Bit 0-2 = Direction, Bit 3 = BombPressed, Bit 4 = DetonatePressed.
/// 60 Hz × 600s Survival = 36k Bytes = 36 KB pro 10-Minuten-Run. Komprimierbar via Run-Length-Encoding
/// (~5-10 KB nach RLE für typischen Run).</para>
/// </summary>
public sealed class ReplayCapture
{
    /// <summary>Replay-Seed für deterministisches RNG. Wird beim Run-Start gesetzt.</summary>
    public ulong Seed { get; private set; }

    /// <summary>Level-Nummer / Mode-Tag — fürs Replay-File-Header.</summary>
    public string ModeTag { get; private set; } = string.Empty;
    public int LevelNumber { get; private set; }

    /// <summary>Aktuelle Tick-Anzahl seit Run-Start.</summary>
    public int TickCount => _ticks.Count;

    private readonly List<byte> _ticks = new();

    /// <summary>Schema-Version für Forward-Compatibility (V1 = 1 Byte pro Tick).</summary>
    public const byte SchemaVersion = 1;

    /// <summary>Maximale Replay-Länge (Soft-Cap). 60 Hz × 30 min = 108k Ticks.</summary>
    public const int MaxTicks = 108_000;

    /// <summary>Beginnt eine neue Replay-Aufnahme. Seed sollte deterministisch sein (z.B. UTC-Datum).</summary>
    public void StartCapture(ulong seed, string modeTag, int levelNumber)
    {
        Seed = seed;
        ModeTag = modeTag;
        LevelNumber = levelNumber;
        _ticks.Clear();
    }

    /// <summary>Zeichnet einen Sim-Tick auf. Wird pro deterministischem Sim-Tick aufgerufen.</summary>
    public void RecordTick(Direction direction, bool bombPressed, bool detonatePressed)
    {
        if (_ticks.Count >= MaxTicks) return; // Soft-Cap, ältere Ticks werden NICHT überschrieben
        var packed = PackInput(direction, bombPressed, detonatePressed);
        _ticks.Add(packed);
    }

    /// <summary>Liefert den Tick an Position <paramref name="index"/> für Replay-Playback.</summary>
    public (Direction direction, bool bombPressed, bool detonatePressed) GetTick(int index)
    {
        if (index < 0 || index >= _ticks.Count)
            return (Direction.None, false, false);
        return UnpackInput(_ticks[index]);
    }

    /// <summary>Serialisiert den Replay als Byte-Array (Header + Tick-Stream).</summary>
    public byte[] Serialize()
    {
        // Layout:
        //   [0]   Schema-Version (1 Byte)
        //   [1-8] Seed (UInt64 LE)
        //   [9]   LevelNumber (1 Byte, 0-100)
        //   [10]  ModeTag-Length (1 Byte)
        //   [11-...] ModeTag (UTF-8)
        //   [...]  TickCount (Int32 LE)
        //   [...]  Tick-Bytes (1 Byte pro Tick)
        var modeBytes = System.Text.Encoding.UTF8.GetBytes(ModeTag ?? string.Empty);
        var headerSize = 1 + 8 + 1 + 1 + modeBytes.Length + 4;
        var buf = new byte[headerSize + _ticks.Count];

        int offset = 0;
        buf[offset++] = SchemaVersion;
        BitConverter.GetBytes(Seed).CopyTo(buf, offset); offset += 8;
        buf[offset++] = (byte)Math.Min(LevelNumber, 255);
        buf[offset++] = (byte)Math.Min(modeBytes.Length, 255);
        Array.Copy(modeBytes, 0, buf, offset, modeBytes.Length); offset += modeBytes.Length;
        BitConverter.GetBytes(_ticks.Count).CopyTo(buf, offset); offset += 4;
        for (int i = 0; i < _ticks.Count; i++) buf[offset++] = _ticks[i];

        return buf;
    }

    /// <summary>Liest einen Replay aus Bytes ein. Wirft <see cref="InvalidDataException"/> bei Schema-Mismatch.</summary>
    public static ReplayCapture Deserialize(byte[] data)
    {
        if (data == null || data.Length < 14)
            throw new InvalidDataException("Replay-Daten zu kurz");

        int offset = 0;
        var version = data[offset++];
        if (version != SchemaVersion)
            throw new InvalidDataException($"Unbekannte Replay-Schema-Version {version}");

        var seed = BitConverter.ToUInt64(data, offset); offset += 8;
        var level = data[offset++];
        var modeLen = data[offset++];
        var modeTag = System.Text.Encoding.UTF8.GetString(data, offset, modeLen); offset += modeLen;
        var tickCount = BitConverter.ToInt32(data, offset); offset += 4;

        if (offset + tickCount > data.Length)
            throw new InvalidDataException("Replay-Daten unvollständig");

        var replay = new ReplayCapture();
        replay.StartCapture(seed, modeTag, level);
        for (int i = 0; i < tickCount; i++)
            replay._ticks.Add(data[offset + i]);
        return replay;
    }

    private static byte PackInput(Direction direction, bool bombPressed, bool detonatePressed)
    {
        // Bit 0-2: Direction (0-4: None/Up/Down/Left/Right)
        // Bit 3: BombPressed
        // Bit 4: DetonatePressed
        // Bit 5-7: Reserve
        byte b = (byte)((int)direction & 0b111);
        if (bombPressed) b |= 1 << 3;
        if (detonatePressed) b |= 1 << 4;
        return b;
    }

    private static (Direction direction, bool bombPressed, bool detonatePressed) UnpackInput(byte b)
    {
        var dirRaw = b & 0b111;
        var dir = dirRaw is >= 0 and <= 4 ? (Direction)dirRaw : Direction.None;
        var bomb = (b & (1 << 3)) != 0;
        var det = (b & (1 << 4)) != 0;
        return (dir, bomb, det);
    }
}
