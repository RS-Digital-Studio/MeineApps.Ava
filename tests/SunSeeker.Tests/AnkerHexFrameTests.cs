using System.Buffers.Binary;
using FluentAssertions;
using SunSeeker.Shared.Services.Anker;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>
/// Verifiziert den Anker-Binär-Frame: Encode des Realtime-Triggers (Präfix, Längenfeld,
/// XOR-Checksumme) und Decode einer C2000-Gen-2-Telemetrie (A1783, Feld a6/04 = DC-Watt, a5/02 = SoC).
/// </summary>
public class AnkerHexFrameTests
{
    [Fact]
    public void BuildRealtimeTrigger_HatGueltigesPraefixLaengeUndChecksumme()
    {
        var frame = AnkerHexFrame.BuildRealtimeTrigger(unixSeconds: 1_700_000_000u);

        frame[0].Should().Be(0xFF);
        frame[1].Should().Be(0x09);

        // Längenfeld (LE) = Gesamtlänge inkl. Checksumme.
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2)).Should().Be((ushort)frame.Length);

        // Pattern (send) 03 00 0f, msgtype 0057.
        frame[4..7].Should().Equal((byte)0x03, (byte)0x00, (byte)0x0F);

        // XOR über ALLE Bytes (inkl. Checksumme) muss 0 ergeben.
        byte xor = 0;
        foreach (var b in frame) xor ^= b;
        xor.Should().Be(0);
    }

    [Fact]
    public void BuildRealtimeTrigger_DecodetZuMsgtype0057MitErwartetenFeldern()
    {
        var frame = AnkerHexFrame.BuildRealtimeTrigger(unixSeconds: 1_700_000_000u, timeoutSeconds: 60);
        var decoded = AnkerHexFrame.TryDecode(frame);

        decoded.Should().NotBeNull();
        decoded!.MsgType.Should().Be(0x0057);
        decoded.Fields.Keys.Should().Contain([(byte)0xA1, (byte)0xA2, (byte)0xA3, (byte)0xFE]);
    }

    [Fact]
    public void TryDecode_A1783Telemetrie_LiestDcEingangUndBatterieSoc()
    {
        // Synthetischer 0421-Frame: a6 (DC-Eingang an Offset 04 = 150 W), a5 (SoC an Offset 02 = 80 %).
        byte[] frame =
        [
            0xFF, 0x09, 0x19, 0x00, 0x03, 0x01, 0x0F, 0x04, 0x21, // Header (len 0x19 = 25), msgtype 0421
            0xA6, 0x07, 0x06, 0x00, 0x00, 0x00, 0x00, 0x96, 0x00, // a6: type strb, value[4..6] = 0x0096 = 150
            0xA5, 0x04, 0x06, 0x19, 0x00, 0x50,                   // a5: value[2] = 0x50 = 80
            0x00,                                                  // Checksumme (Decode ignoriert sie)
        ];

        var decoded = AnkerHexFrame.TryDecode(frame);
        decoded.Should().NotBeNull();
        decoded!.MsgType.Should().Be(0x0421);

        AnkerHexFrame.A1783DcInputWatts(decoded).Should().Be(150);
        AnkerHexFrame.A1783BatterySoc(decoded).Should().Be(80);
    }

    [Fact]
    public void A1783DcInputWatts_LiestNegativeWerteAlsSignedLittleEndian()
    {
        // dc an Offset 04 = 0xFFFE (LE FE FF) = -2 (z.B. minimaler Rückfluss).
        byte[] frame =
        [
            0xFF, 0x09, 0x13, 0x00, 0x03, 0x01, 0x0F, 0x09, 0x00, // msgtype 0900
            0xA6, 0x07, 0x06, 0x00, 0x00, 0x00, 0x00, 0xFE, 0xFF,
            0x00,
        ];
        var decoded = AnkerHexFrame.TryDecode(frame);
        AnkerHexFrame.A1783DcInputWatts(decoded!).Should().Be(-2);
    }

    [Fact]
    public void A1783DcInputWatts_FalscherMsgtype_GibtNull()
    {
        // Gleicher a6-Inhalt, aber msgtype 0830 (Versions-Nachricht) → kein Leistungswert.
        byte[] frame =
        [
            0xFF, 0x09, 0x13, 0x00, 0x03, 0x01, 0x0F, 0x08, 0x30,
            0xA6, 0x07, 0x06, 0x00, 0x00, 0x00, 0x00, 0x96, 0x00,
            0x00,
        ];
        var decoded = AnkerHexFrame.TryDecode(frame);
        decoded.Should().NotBeNull();
        AnkerHexFrame.A1783DcInputWatts(decoded!).Should().BeNull();
    }

    [Fact]
    public void TryDecode_UngueltigesPraefix_GibtNull()
    {
        byte[] frame = [0x12, 0x34, 0x00, 0x00, 0x03, 0x01, 0x0F, 0x04, 0x21, 0x00];
        AnkerHexFrame.TryDecode(frame).Should().BeNull();
    }
}
