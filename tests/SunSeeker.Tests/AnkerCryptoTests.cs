using FluentAssertions;
using SunSeeker.Shared.Services.Anker;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>
/// Verifiziert die Krypto-Bausteine des Anker-Logins (gtoken, Timezone-Header, Passwort-
/// Verschluesselung). Die Passwort-Verschluesselung kann ohne Server-Private-Key nicht
/// zurueckentschluesselt werden — daher werden Struktur-Invarianten geprueft (Public-Key-Format,
/// AES-Blockgroesse, Ephemeral-Key je Aufruf).
/// </summary>
public class AnkerCryptoTests
{
    [Fact]
    public void GToken_IstMd5HexDesUserId()
    {
        // MD5("test") = 098f6bcd4621d373cade4e832627b4f6
        AnkerCrypto.GToken("test").Should().Be("098f6bcd4621d373cade4e832627b4f6");
    }

    [Theory]
    [InlineData(0, 0, "GMT+00:00")]
    [InlineData(1, 0, "GMT+01:00")]
    [InlineData(-5, 0, "GMT-05:00")]
    [InlineData(5, 30, "GMT+05:30")]
    [InlineData(-3, -30, "GMT-03:30")]
    public void TimezoneHeader_FormatiertOffsetKorrekt(int hours, int minutes, string expected)
    {
        var offset = new TimeSpan(hours, minutes, 0);
        AnkerCrypto.TimezoneHeader(offset).Should().Be(expected);
    }

    [Fact]
    public void EncryptPassword_LiefertUnkomprimiertenPublicKeyUndAesBlock()
    {
        var (pubHex, encrypted) = AnkerCrypto.EncryptPassword("geheim123");

        // Unkomprimierter P-256-Punkt: 1 + 32 + 32 = 65 Byte = 130 Hex-Zeichen, beginnt mit "04".
        pubHex.Should().HaveLength(130);
        pubHex.Should().StartWith("04");

        // AES-CBC-Ausgabe ist Base64 und ein Vielfaches der Blockgroesse (16 Byte).
        var bytes = Convert.FromBase64String(encrypted);
        bytes.Length.Should().BeGreaterThan(0);
        (bytes.Length % 16).Should().Be(0);
    }

    [Fact]
    public void EncryptPassword_NutztProAufrufEinNeuesEphemeresSchluesselpaar()
    {
        var (pub1, enc1) = AnkerCrypto.EncryptPassword("same");
        var (pub2, enc2) = AnkerCrypto.EncryptPassword("same");

        pub1.Should().NotBe(pub2);
        enc1.Should().NotBe(enc2);
    }
}
