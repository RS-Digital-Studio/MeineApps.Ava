using System.Security.Cryptography;
using System.Text;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// HMAC-SHA256-Signierung mit ZWEI Schluesseln fuer zwei verschiedene Vertrauensgrenzen:
///
/// 1. <b>Lokaler Save-Key</b> (geraete-einzigartig, PackageSalt + Installations-GUID):
///    Fuer die eigene <see cref="GameState.IntegritySignature"/>. Wird NUR auf demselben Geraet
///    signiert und verifiziert (Schutz gegen lokales Save-Editing). Felder: PlayerLevel,
///    TotalPrestigeCount, Money, GoldenScrews, TotalOrdersCompleted.
///
/// 2. <b>Geteilter Multiplayer-Key</b> (geraete-uebergreifend identisch, nur PackageSalt):
///    Fuer <see cref="ComputeStringHmac"/> — Co-op-Auftraege, Auktionen, Mega-Projekte.
///    Diese Objekte werden von einem Spieler signiert und von ANDEREN Spielern validiert.
///    Ein geraete-lokaler Key wuerde hier IMMER fehlschlagen (jeder Client haette einen anderen
///    Key) und damit Co-op/Auktionen/Mega-Projekte zwischen echten Gildenmitgliedern komplett
///    brechen. Der geteilte Key dient daher als geraete-uebergreifend konsistente Tamper-Evidence-
///    Pruefsumme; die echte serverseitige Manipulations-Abwehr leisten die Firebase-Rules
///    (Score-Range, Bid-Monotonie, write-once Claims).
/// </summary>
public sealed class GameIntegrityService : IGameIntegrityService
{
    private const string PrefKeyInstallationId = "game_integrity_install_id";

    /// <summary>
    /// Fester Bestandteil des HMAC-Schluessels (Package-Name).
    /// Zusammen mit der Installations-GUID ergibt sich ein geraete-einzigartiger Schluessel.
    /// </summary>
    private const string PackageSalt = "com.meineapps.handwerkerimperium";

    private readonly byte[] _hmacKey;
    private readonly byte[] _sharedHmacKey;

    public GameIntegrityService(IPreferencesService preferences)
    {
        // Installations-GUID laden oder erstmalig generieren
        var installId = preferences.Get<string?>(PrefKeyInstallationId, null);
        if (string.IsNullOrEmpty(installId))
        {
            installId = Guid.NewGuid().ToString("N");
            preferences.Set(PrefKeyInstallationId, installId);
        }

        // Lokaler Save-Key: SHA256(PackageSalt + InstallId) — geraete-einzigartig.
        // Ergibt 32 Byte (256 Bit) — optimale Laenge fuer HMAC-SHA256.
        _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(PackageSalt + installId));

        // Geteilter Multiplayer-Key: nur aus PackageSalt abgeleitet -> auf allen Geraeten identisch,
        // sodass per HMAC signierte geteilte Firebase-Objekte cross-client validierbar bleiben.
        _sharedHmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(PackageSalt + "|shared-guild-hmac-v1"));
    }

    /// <inheritdoc/>
    public void ComputeSignature(GameState state)
    {
        state.IntegritySignature = CalculateHmac(state);
    }

    /// <inheritdoc/>
    public bool VerifySignature(GameState state)
    {
        if (string.IsNullOrEmpty(state.IntegritySignature))
            return false;

        // Die rohen HMAC-Bytes timing-sicher vergleichen — unabhaengig von der
        // Hex-String-Repraesentation (Lower/Upper-Case, Trimming). Frueher wurden die
        // UTF-8-Bytes der Hex-STRINGS verglichen, was bei einer Format-Migration
        // (z.B. Upper-Case oder Base64) still gebrochen waere.
        byte[] storedBytes;
        try
        {
            storedBytes = Convert.FromHexString(state.IntegritySignature);
        }
        catch (FormatException)
        {
            // Ungueltiges Hex-Format (manipuliert / fremdes Format) → Signatur ungueltig.
            return false;
        }

        var expectedBytes = CalculateHmacBytes(state);
        return CryptographicOperations.FixedTimeEquals(storedBytes, expectedBytes);
    }

    /// <inheritdoc/>
    public string ComputeStringHmac(string payload)
    {
        // GETEILTER Key — siehe Klassen-Doku: geteilte Multiplayer-Objekte werden von einem Spieler
        // signiert und von anderen validiert, daher muss der Key geraete-uebergreifend identisch sein.
        var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
        using var hmac = new HMACSHA256(_sharedHmacKey);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Berechnet den HMAC-SHA256 ueber die Gilden-relevanten Werte als Hex-String
    /// (fuer die Persistenz in <see cref="GameState.IntegritySignature"/>).
    /// Format: "Level|PrestigeCount|Money|GoldenScrews|OrdersCompleted"
    /// </summary>
    private string CalculateHmac(GameState state)
        => Convert.ToHexStringLower(CalculateHmacBytes(state));

    /// <summary>
    /// FB-H05: Berechnet die rohen HMAC-SHA256-Bytes — Basis fuer den timing-sicheren
    /// Vergleich in <see cref="VerifySignature"/>, unabhaengig von der String-Kodierung.
    /// </summary>
    private byte[] CalculateHmacBytes(GameState state)
    {
        // Deterministische String-Repraesentation der signierten Werte.
        // Dezimalzahlen mit festem Format (keine Kultur-Abhaengigkeit).
        var payload = string.Create(null, stackalloc char[128],
            $"{state.PlayerLevel}|{state.Prestige.TotalPrestigeCount}|{state.Money:F2}|{state.GoldenScrews}|{state.Statistics.TotalOrdersCompleted}");

        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(_hmacKey);
        return hmac.ComputeHash(payloadBytes);
    }
}
