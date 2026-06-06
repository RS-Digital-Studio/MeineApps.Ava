using System.Buffers.Binary;

namespace SunSeeker.Shared.Services.Anker;

/// <summary>
/// Encoder/Decoder des proprietären Anker-Binär-Frames (gleiche Struktur wie das Anker-BLE-Protokoll;
/// in der MQTT-Nachricht base64-codiert, NICHT verschlüsselt). Portiert aus thomluther/anker-solix-api
/// (mqtttypes.py).
///
/// Frame-Layout: <c>FF 09 | len(2B LE, inkl. Checksumme) | 03 00 0f (send) / 03 01 0f (recv) |
/// msgtype(2B) | [increment(1B)] | Felder… | XOR-Checksumme(1B)</c>.
/// Ein Feld: <c>name(1B) | length(1B) | [type(1B)] | value</c>, wobei length = Anzahl der Bytes
/// nach dem Längen-Byte (also type + value).
/// </summary>
public static class AnkerHexFrame
{
    // Feld-Datentypen (DeviceHexDataTypes).
    private const byte TypeVar = 0x03; // 4-Byte-Rohwert (u.a. Timeout, Timestamp)

    /// <summary>Dekodiertes Frame: Nachrichtentyp + flache Feld-Tabelle (Tag → Roh-Value ohne Typ-Byte).</summary>
    public sealed record DecodedFrame(int MsgType, IReadOnlyDictionary<byte, byte[]> Fields);

    /// <summary>
    /// Zerlegt einen empfangenen (base64-dekodierten) Frame in Nachrichtentyp + Felder. Liefert
    /// null bei ungültigem Präfix oder zu kurzem Frame. Defensiv: bricht beim ersten unplausiblen
    /// Feld sauber ab und gibt das bis dahin Geparste zurück.
    /// </summary>
    public static DecodedFrame? TryDecode(byte[] frame)
    {
        if (frame.Length < 10 || frame[0] != 0xFF || frame[1] != 0x09)
            return null;

        int msgType = (frame[7] << 8) | frame[8];

        // Header = 9 Byte; ein optionales increment-Byte liegt vor, wenn Byte[9] KEIN Feld-Tag a0..a9 ist.
        int idx = 9;
        if (frame[9] is < 0xA0 or > 0xA9)
            idx = 10;

        var fields = new Dictionary<byte, byte[]>();
        // Letztes Byte ist die Checksumme — nicht als Feld interpretieren.
        int end = frame.Length - 1;
        while (idx + 2 <= end)
        {
            byte name = frame[idx];
            int length = frame[idx + 1]; // 1-Byte-Länge (Power-/Status-Felder sind nie str/bin)
            if (length < 1 || idx + 2 + length > frame.Length)
                break;

            // value = Bytes nach dem (vorhandenen) Typ-Byte. Typ-Byte liegt vor, wenn das Byte
            // direkt nach der Länge < 0x10 ist (so unterscheidet die Referenz Typ von Daten).
            int valueStart = idx + 2;
            int valueLen = length;
            if (frame[idx + 2] < 0x10)
            {
                valueStart = idx + 3;
                valueLen = length - 1;
            }
            if (valueLen > 0 && valueStart + valueLen <= frame.Length)
                fields[name] = frame[valueStart..(valueStart + valueLen)];

            idx += 2 + length;
        }

        return new DecodedFrame(msgType, fields);
    }

    /// <summary>
    /// Liest die DC-Eingangsleistung (Solar + ggf. 12V-Auto-Laden) der C2000 Gen 2 (A1783) aus
    /// einem Telemetrie-Frame: Feld <c>a6</c>, Sub-Offset 04, int16 signed Little-Endian, Watt.
    /// Nur für Status-Nachrichten 0x0421/0x0900 gültig. Liefert null, wenn nicht enthalten.
    /// </summary>
    public static int? A1783DcInputWatts(DecodedFrame frame)
    {
        if (frame.MsgType is not (0x0421 or 0x0900)) return null;
        if (!frame.Fields.TryGetValue(0xA6, out var a6) || a6.Length < 6) return null;
        return BinaryPrimitives.ReadInt16LittleEndian(a6.AsSpan(4, 2));
    }

    /// <summary>Batterie-Ladestand (%) aus Feld <c>a5</c>, Sub-Offset 02 (1 Byte). Null wenn nicht enthalten.</summary>
    public static int? A1783BatterySoc(DecodedFrame frame)
    {
        if (frame.MsgType is not (0x0421 or 0x0900)) return null;
        if (!frame.Fields.TryGetValue(0xA5, out var a5) || a5.Length < 3) return null;
        return a5[2];
    }

    /// <summary>
    /// Baut den Realtime-Trigger-Frame (msgtype 0057, CMD_REALTIME_TRIGGER) — veranlasst das Gerät,
    /// regelmäßig (~3-5 s) Status-Nachrichten 0421 zu senden. Aufbau wie in mqtt.py:
    /// feste Felder a1 01 22, a2 02 01 01, a3 (var, 4B Timeout LE) und ein Zeitstempel-Feld fe.
    /// </summary>
    public static byte[] BuildRealtimeTrigger(uint unixSeconds, int timeoutSeconds = 60)
    {
        var fields = new List<byte>();
        fields.AddRange([0xA1, 0x01, 0x22]);             // vorformatiert
        fields.AddRange([0xA2, 0x02, 0x01, 0x01]);        // vorformatiert
        fields.AddRange(BuildVarField(0xA3, (uint)timeoutSeconds)); // a3 | 05 | 03 | timeout LE
        fields.AddRange(BuildVarField(0xFE, unixSeconds));          // fe | 05 | 03 | timestamp LE

        int totalLen = 9 + fields.Count + 1; // Header(9) + Felder + Checksumme(1)

        var frame = new List<byte>(totalLen);
        frame.AddRange([0xFF, 0x09]);                     // Präfix
        var len = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(len, (ushort)totalLen);
        frame.AddRange(len);
        frame.AddRange([0x03, 0x00, 0x0F]);               // pattern (send)
        frame.AddRange([0x00, 0x57]);                     // msgtype 0057
        frame.AddRange(fields);

        byte checksum = 0;
        foreach (var b in frame) checksum ^= b;
        frame.Add(checksum);

        return [.. frame];
    }

    /// <summary>Ein var-Feld (4-Byte-Wert LE): name | length(=5) | 03 | value(4B LE).</summary>
    private static byte[] BuildVarField(byte name, uint value)
    {
        var v = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(v, value);
        return [name, 0x05, TypeVar, v[0], v[1], v[2], v[3]];
    }
}
