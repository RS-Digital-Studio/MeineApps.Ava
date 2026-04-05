using System.Security.Cryptography;
using System.Text;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// HMAC-SHA256-Signierung von Gilden-relevanten GameState-Werten.
///
/// Signierte Felder: PlayerLevel, TotalPrestigeCount, Money, GoldenScrews, TotalOrdersCompleted.
/// Diese Werte fliessen in Gilden-Leaderboards und Wochenziele ein.
///
/// Schluessel-Ableitung: HMAC-Key wird pro Geraet aus dem Package-Namen
/// und einer persistierten Installations-GUID kombiniert. Kein hardcodierter Schluessel.
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

    public GameIntegrityService(IPreferencesService preferences)
    {
        // Installations-GUID laden oder erstmalig generieren
        var installId = preferences.Get<string?>(PrefKeyInstallationId, null);
        if (string.IsNullOrEmpty(installId))
        {
            installId = Guid.NewGuid().ToString("N");
            preferences.Set(PrefKeyInstallationId, installId);
        }

        // HMAC-Schluessel: SHA256(PackageSalt + InstallId)
        // Ergibt 32 Byte (256 Bit) — optimale Laenge fuer HMAC-SHA256.
        _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(PackageSalt + installId));
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

        var expected = CalculateHmac(state);

        // Timing-sicherer Vergleich gegen Timing-Attacken
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state.IntegritySignature),
            Encoding.UTF8.GetBytes(expected));
    }

    /// <summary>
    /// Berechnet den HMAC-SHA256 ueber die Gilden-relevanten Werte.
    /// Format: "Level|PrestigeCount|Money|GoldenScrews|OrdersCompleted"
    /// </summary>
    private string CalculateHmac(GameState state)
    {
        // Deterministische String-Repraesentation der signierten Werte.
        // Dezimalzahlen mit festem Format (keine Kultur-Abhaengigkeit).
        var payload = string.Create(null, stackalloc char[128],
            $"{state.PlayerLevel}|{state.Prestige.TotalPrestigeCount}|{state.Money:F2}|{state.GoldenScrews}|{state.Statistics.TotalOrdersCompleted}");

        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }
}
