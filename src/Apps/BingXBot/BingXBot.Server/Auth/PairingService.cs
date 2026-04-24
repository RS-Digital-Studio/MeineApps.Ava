using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace BingXBot.Server.Auth;

/// <summary>
/// Pairing-Service: Generiert 6-stellige Einmal-Codes fuer die Erstverbindung eines Clients.
///
/// Ablauf (analog Chromecast/Steam Link):
/// 1. Client ruft POST /api/v1/pair/init auf (ohne Auth).
/// 2. Server generiert einen kryptographisch sicheren 6-stelligen Code (000000-999999).
/// 3. Code wird in stdout/systemd-journal geloggt und in /var/lib/bingxbot/pairing-code.txt geschrieben.
///    Nutzer liest den Code direkt vom Pi (SSH oder Bildschirm) und tippt ihn im Client ein.
/// 4. Client ruft POST /api/v1/pair/complete mit Code + DeviceId auf.
/// 5. Server verifiziert den Code und gibt einen Bearer-Token + Refresh-Token zurueck.
/// 6. Der Token wird im AuthTokenStore persistiert und ist in Folgerequests gueltig.
///
/// Codes sind max. 5 Min. gueltig, werden nach Verwendung geloescht und erlauben max. 5 Versuche.
/// </summary>
public sealed class PairingService
{
    private readonly ConcurrentDictionary<string, PendingPairing> _pending = new();
    private readonly string _codeFilePath;
    private readonly ILogger<PairingService> _logger;
    private readonly int _codeLength;
    private readonly int _lifetimeSeconds;

    public PairingService(IConfiguration config, ILogger<PairingService> logger)
    {
        _logger = logger;
        _codeLength = config.GetValue<int>("Server:PairingCodeLengthDigits", 6);
        _lifetimeSeconds = config.GetValue<int>("Server:PairingCodeLifetimeSeconds", 300);

        var dataDir = config.GetValue<string>("Server:DataDirectory");
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            dataDir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot", "Server")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bingxbot");
        }
        Directory.CreateDirectory(dataDir);
        _codeFilePath = Path.Combine(dataDir, "pairing-code.txt");
    }

    /// <summary>Startet einen neuen Pairing-Vorgang: Code generieren, persistieren, loggen.</summary>
    /// <exception cref="InvalidOperationException">Wenn bereits ein aktiver Pairing-Vorgang lauft
    /// (verhindert Spam-Angriffe, die die Code-Datei ueberschreiben wuerden).</exception>
    public (string PairingId, int LifetimeSeconds) StartPairing(string deviceName)
    {
        CleanupExpired();

        // Spam-Schutz: Nur ein aktives Pair zur Zeit. Angreifer kann nicht den legitimen
        // Code in pairing-code.txt ueberschreiben. Der legitime User muss seinen laufenden
        // Pair-Vorgang erst abschliessen oder abbrechen (Code laeuft nach 5 Min ab).
        if (!_pending.IsEmpty)
        {
            var active = _pending.Values.First();
            var secondsLeft = Math.Max(0, (int)(active.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);
            throw new InvalidOperationException(
                $"Pairing bereits aktiv fuer Geraet '{active.DeviceName}'. Noch {secondsLeft}s gueltig.");
        }

        var pairingId = Guid.NewGuid().ToString("N");
        var code = GenerateCode();
        var expiresAt = DateTime.UtcNow.AddSeconds(_lifetimeSeconds);

        _pending[pairingId] = new PendingPairing(pairingId, code, deviceName, expiresAt, 0);

        // Dauerhaft in Datei schreiben (ueberschreibt alten Code) — Nutzer liest direkt vom Pi.
        // chmod 600 auf Linux: Nur bingxbot-User darf lesen (Rootless-Multi-User-Schutz).
        try
        {
            File.WriteAllText(_codeFilePath,
                $"BingXBot Pairing-Code fuer Geraet '{deviceName}'{Environment.NewLine}" +
                $"Code: {code}{Environment.NewLine}" +
                $"Gueltig bis: {expiresAt:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try { File.SetUnixFileMode(_codeFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                catch { /* best effort */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pairing-Code-Datei konnte nicht geschrieben werden");
        }

        // Ausserdem in systemd-journal: 'journalctl -u bingxbot -f' zeigt den Code
        _logger.LogWarning("=== BINGXBOT PAIRING-CODE: {Code} fuer Geraet '{Device}' (gueltig {Lifetime}s) ===",
            code, deviceName, _lifetimeSeconds);

        return (pairingId, _lifetimeSeconds);
    }

    /// <summary>
    /// Versucht, einen Pairing-Vorgang abzuschließen. Bei Erfolg wird die PendingPairing entfernt.
    /// Rückgabe differenziert zwischen Tippfehler und Session-Tod (abgelaufen / zu viele Fehlversuche /
    /// unbekannte PairingId), damit der Client den User klar anleiten kann.
    /// </summary>
    public PairCompleteOutcome TryComplete(string pairingId, string code, out string? deviceName)
    {
        deviceName = null;

        if (!_pending.TryGetValue(pairingId, out var pending))
        {
            _logger.LogInformation(
                "Pairing {PairingId} complete-fehlgeschlagen: unbekannte PairingId (Server-Neustart oder bereits abgeschlossen)",
                pairingId);
            return PairCompleteOutcome.UnknownPairingId;
        }

        if (pending.ExpiresAtUtc < DateTime.UtcNow)
        {
            _pending.TryRemove(pairingId, out _);
            _logger.LogInformation(
                "Pairing {PairingId} complete-fehlgeschlagen: Code abgelaufen ({Device})",
                pairingId, pending.DeviceName);
            return PairCompleteOutcome.Expired;
        }

        pending.Attempts++;
        if (pending.Attempts > 5)
        {
            _pending.TryRemove(pairingId, out _);
            _logger.LogWarning(
                "Pairing {PairingId} abgebrochen: zu viele Fehlversuche (>5) ({Device})",
                pairingId, pending.DeviceName);
            return PairCompleteOutcome.TooManyAttempts;
        }

        // Timing-safe Code-Vergleich. FixedTimeEquals verlangt gleiche Länge, sonst wirft es
        // ArgumentException → 500 statt 401. Länge ist nicht geheim (6 Ziffern), darf upfront geprüft werden.
        var candidateCode = code ?? string.Empty;
        var codeMatch = candidateCode.Length == pending.Code.Length
            && CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(pending.Code),
                System.Text.Encoding.ASCII.GetBytes(candidateCode));

        if (!codeMatch)
        {
            _logger.LogInformation(
                "Pairing {PairingId} complete-fehlgeschlagen: Code falsch (Versuch {Attempts}/5, {Device})",
                pairingId, pending.Attempts, pending.DeviceName);
            return PairCompleteOutcome.InvalidCode;
        }

        _pending.TryRemove(pairingId, out _);
        deviceName = pending.DeviceName;

        // Code-Datei löschen — nicht mehr gültig
        try { if (File.Exists(_codeFilePath)) File.Delete(_codeFilePath); } catch { }

        _logger.LogInformation(
            "Pairing {PairingId} erfolgreich abgeschlossen für '{Device}'",
            pairingId, deviceName);
        return PairCompleteOutcome.Success;
    }

    /// <summary>Bricht ein aktives Pairing ab (z.B. wenn Nutzer den Code verlegt hat).</summary>
    public bool CancelPairing(string pairingId)
    {
        var removed = _pending.TryRemove(pairingId, out _);
        if (removed)
        {
            try { if (File.Exists(_codeFilePath)) File.Delete(_codeFilePath); } catch { }
            _logger.LogInformation("Pairing {PairingId} vom Client abgebrochen", pairingId);
        }
        return removed;
    }

    /// <summary>Ob gerade ein Pairing laeuft (Lese-only, fuer Client-Status).</summary>
    public bool HasActivePairing => !_pending.IsEmpty;

    private string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var num = BitConverter.ToUInt32(bytes);
        var modulo = (int)Math.Pow(10, _codeLength);
        var code = (num % modulo).ToString().PadLeft(_codeLength, '0');
        return code;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _pending)
        {
            if (kvp.Value.ExpiresAtUtc < now)
                _pending.TryRemove(kvp.Key, out _);
        }
    }

    private sealed class PendingPairing(string pairingId, string code, string deviceName, DateTime expiresAtUtc, int attempts)
    {
        public string PairingId { get; } = pairingId;
        public string Code { get; } = code;
        public string DeviceName { get; } = deviceName;
        public DateTime ExpiresAtUtc { get; } = expiresAtUtc;
        public int Attempts { get; set; } = attempts;
    }
}

/// <summary>
/// Ergebnis eines PairComplete-Versuchs. Wird auf HTTP-ErrorCodes gemappt, damit der Client
/// zwischen "Tippfehler" (Session bleibt offen, User tippt neu) und "Session tot" (Client muss
/// zurück in Schritt 1) unterscheiden kann.
/// </summary>
public enum PairCompleteOutcome
{
    /// <summary>Code stimmt, Token wird ausgestellt.</summary>
    Success,

    /// <summary>Code ist falsch, aber die Session lebt noch — User darf erneut tippen.</summary>
    InvalidCode,

    /// <summary>PairingId nicht bekannt (Server-Neustart oder andere Client-Instanz hat bereits abgeschlossen).</summary>
    UnknownPairingId,

    /// <summary>Pairing-Lifetime abgelaufen (Default 5 min).</summary>
    Expired,

    /// <summary>Mehr als 5 Fehlversuche — Pairing wurde serverseitig abgebrochen.</summary>
    TooManyAttempts
}
