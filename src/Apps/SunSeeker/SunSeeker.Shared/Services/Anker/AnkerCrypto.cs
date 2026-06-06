using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SunSeeker.Shared.Services.Anker;

/// <summary>
/// Krypto-Bausteine des Anker-Cloud-Logins (passport/login), 1:1 portiert aus thomluther/anker-solix-api
/// (session.py). Das Passwort wird NICHT im Klartext übertragen, sondern per ECDH (NIST P-256 /
/// SECP256R1) gegen einen fest hinterlegten Server-Public-Key zu einem 32-Byte-Shared-Secret
/// abgeleitet und damit AES-256-CBC verschlüsselt (Key = Shared Secret, IV = die ersten 16 Byte,
/// PKCS7-Padding, Base64-Ausgabe). Der eigene Public Key reist als unkomprimierter Punkt (04|X|Y,
/// Hex) im Login-Body mit.
/// </summary>
public static class AnkerCrypto
{
    /// <summary>Hartkodierter Server-Public-Key (unkomprimierter P-256-Punkt, Hex) aus session.py.</summary>
    private const string ServerPublicKeyHex =
        "04c5c00c4f8d1197cc7c3167c52bf7acb054d722f0ef08dcd7e0883236e0d72a3868d9750cb47fa4619248f3d83f0f662671dadc6e2d31c2f41db0161651c7c076";

    /// <summary>
    /// Erzeugt ein flüchtiges P-256-Schlüsselpaar, verschlüsselt das Passwort gegen den Server-Key
    /// und liefert (eigener Public Key als Hex, Base64-verschlüsseltes Passwort).
    /// </summary>
    public static (string PublicKeyHex, string EncryptedPassword) EncryptPassword(string password)
    {
        using var ownEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var serverBytes = Convert.FromHexString(ServerPublicKeyHex);
        var serverParams = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = serverBytes[1..33],
                Y = serverBytes[33..65],
            },
        };
        using var serverEcdh = ECDiffieHellman.Create(serverParams);

        // Rohes Shared Secret (32-Byte X-Koordinate des gemeinsamen Punkts) — KEINE KDF.
        // Entspricht cryptography.exchange(ec.ECDH(), serverPub) in der Python-Referenz.
        byte[] shared = ownEcdh.DeriveRawSecretAgreement(serverEcdh.PublicKey);

        byte[] iv = shared[..16];
        using var aes = Aes.Create();
        aes.Key = shared; // 32 Byte → AES-256
        byte[] cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(password), iv, PaddingMode.PKCS7);

        return (UncompressedPublicKeyHex(ownEcdh), Convert.ToBase64String(cipher));
    }

    /// <summary>Eigener Public Key als unkomprimierter Punkt 04|X|Y (Hex, kleingeschrieben) — wie _rawPublicKey().</summary>
    private static string UncompressedPublicKeyHex(ECDiffieHellman ecdh)
    {
        var p = ecdh.ExportParameters(includePrivateParameters: false);
        var point = new byte[65];
        point[0] = 0x04;
        p.Q.X!.CopyTo(point, 1);
        p.Q.Y!.CopyTo(point, 33);
        return Convert.ToHexString(point).ToLowerInvariant();
    }

    /// <summary>gtoken = MD5-Hex des rohen user_id-Strings (session.py: md5(data.get("user_id"))).</summary>
    public static string GToken(string userId) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();

    /// <summary>UTC-Offset im Header-Format "GMT+01:00" / "GMT-05:00".</summary>
    public static string TimezoneHeader(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"GMT{sign}{abs.Hours:00}:{abs.Minutes:00}");
    }
}
