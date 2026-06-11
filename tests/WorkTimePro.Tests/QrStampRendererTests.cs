using WorkTimePro.Graphics;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für den Stempel-QR-Code (Deep-Link-Inhalt + PNG-Export).
/// </summary>
public class QrStampRendererTests
{
    [Fact]
    public void StampUri_HatErwartetesSchemaUndHost()
    {
        // Der Intent-Filter in MainActivity.cs (DataScheme/DataHost) ist auf
        // genau diese URI verdrahtet — Abweichung würde den Scan-Flow brechen.
        QrStampRenderer.StampUri.Should().Be("worktimepro://stamp");
    }

    [Fact]
    public void CreatePngBytes_LiefertGueltigesPng()
    {
        var bytes = QrStampRenderer.CreatePngBytes(256);

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);
        // PNG-Signatur: 89 50 4E 47 0D 0A 1A 0A
        bytes[..8].Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }
}
