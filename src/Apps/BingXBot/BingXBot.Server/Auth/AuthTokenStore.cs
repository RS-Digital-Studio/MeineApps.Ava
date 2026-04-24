using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace BingXBot.Server.Auth;

/// <summary>
/// Persistiert Bearer-Tokens + Refresh-Tokens in einer JSON-Datei im DataDirectory.
///
/// Sicherheits-Hinweis: Die Datei liegt unter `DataDirectory/tokens.json` und sollte per
/// chmod 600 geschuetzt sein (wird beim Schreiben auf Linux gesetzt).
///
/// Token-Format: 32 zufaellige Bytes, Base64Url-kodiert (~43 Zeichen).
/// </summary>
public sealed class AuthTokenStore
{
    private readonly string _path;
    private readonly ILogger<AuthTokenStore> _logger;
    private readonly int _tokenLifetimeSeconds;
    private readonly int _refreshLifetimeSeconds;
    private ConcurrentDictionary<string, TokenRecord> _tokens = new();
    private readonly Lock _saveLock = new();

    public AuthTokenStore(IConfiguration config, ILogger<AuthTokenStore> logger)
    {
        _logger = logger;
        _tokenLifetimeSeconds = config.GetValue<int>("Server:TokenLifetimeSeconds", 7 * 24 * 3600);
        // Refresh-Token-Lebensdauer (Default 30 Tage) — separat vom Access-Token, sodass Refresh
        // auch nach Ablauf des Access-Tokens noch funktioniert. Ohne diese Trennung wurde bei
        // Server-Restart zwischen Access-Ablauf und Refresh-Attempt der Record rausgefiltert
        // und der Client musste neu pairen.
        _refreshLifetimeSeconds = config.GetValue<int>("Server:RefreshTokenLifetimeSeconds", 30 * 24 * 3600);

        var dataDir = config.GetValue<string>("Server:DataDirectory");
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            dataDir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot", "Server")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bingxbot");
        }
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "tokens.json");
        Load();
    }

    public TokenRecord IssueToken(string deviceId, string deviceName)
    {
        var token = NewRandomToken();
        var refreshToken = NewRandomToken();
        var now = DateTime.UtcNow;
        var rec = new TokenRecord(
            Token: token,
            RefreshToken: refreshToken,
            DeviceId: deviceId,
            DeviceName: deviceName,
            IssuedAtUtc: now,
            ExpiresAtUtc: now.AddSeconds(_tokenLifetimeSeconds),
            RefreshExpiresAtUtc: now.AddSeconds(_refreshLifetimeSeconds));
        _tokens[token] = rec;
        Save();
        return rec;
    }

    public bool Validate(string token, out TokenRecord? record)
    {
        record = null;
        if (!_tokens.TryGetValue(token, out var rec)) return false;
        if (rec.ExpiresAtUtc < DateTime.UtcNow)
        {
            // Access-Token abgelaufen — Record NICHT entfernen, damit Refresh noch funktioniert.
            // Der Record verschwindet erst wenn auch RefreshExpiresAtUtc ueberschritten ist
            // (siehe PurgeExpired / Save).
            return false;
        }
        record = rec;
        return true;
    }

    public TokenRecord? Refresh(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) return null;
        // Konstant-zeitiger Vergleich ueber ALLE Tokens — verhindert Timing-Side-Channel
        // zur Enumeration der RefreshToken-Menge.
        var tokenBytes = System.Text.Encoding.ASCII.GetBytes(refreshToken);
        TokenRecord? existing = null;
        foreach (var rec in _tokens.Values)
        {
            var candidateBytes = System.Text.Encoding.ASCII.GetBytes(rec.RefreshToken);
            if (candidateBytes.Length == tokenBytes.Length
                && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(candidateBytes, tokenBytes))
            {
                existing = rec;
                // Absichtlich kein break — weiterlaufen um Timing identisch zu halten.
            }
        }
        if (existing == null) return null;

        // Refresh-Token-Lebensdauer separat pruefen. Legacy-Records (vor v1.3.0) haben keine
        // RefreshExpiresAtUtc; dort faellt der Check auf das alte ExpiresAtUtc zurueck.
        var refreshDeadline = existing.RefreshExpiresAtUtc ?? existing.ExpiresAtUtc;
        if (refreshDeadline < DateTime.UtcNow)
        {
            _tokens.TryRemove(existing.Token, out _);
            Save();
            return null;
        }
        _tokens.TryRemove(existing.Token, out _);
        return IssueToken(existing.DeviceId, existing.DeviceName);
    }

    public void Revoke(string token)
    {
        _tokens.TryRemove(token, out _);
        Save();
    }

    /// <summary>Revoked ALLE Tokens ausser dem angegebenen (z.B. nach Credentials-Aenderung).</summary>
    public int RevokeAllExcept(string keepToken)
    {
        var removed = 0;
        foreach (var key in _tokens.Keys.ToList())
        {
            if (!string.Equals(key, keepToken, StringComparison.Ordinal))
            {
                if (_tokens.TryRemove(key, out _)) removed++;
            }
        }
        if (removed > 0) Save();
        return removed;
    }

    public int TokenLifetimeSeconds => _tokenLifetimeSeconds;

    private static string NewRandomToken()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var records = JsonSerializer.Deserialize<List<TokenRecord>>(json) ?? new();
            // Nur echt-ueberfaellige Records (Refresh-Token ebenfalls abgelaufen) entfernen.
            // Access-Token-Ablauf allein reicht nicht — sonst kann der Client nach Restart nicht
            // mehr refreshen. Legacy-Records ohne RefreshExpiresAtUtc fallen auf ExpiresAtUtc zurueck,
            // behalten aber bis zum Refresh den Record im Store.
            var now = DateTime.UtcNow;
            _tokens = new ConcurrentDictionary<string, TokenRecord>(
                records.Where(r => (r.RefreshExpiresAtUtc ?? r.ExpiresAtUtc) > now)
                       .ToDictionary(r => r.Token));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tokens-Datei konnte nicht geladen werden");
        }
    }

    /// <summary>
    /// Raeumt alle Records weg, deren Refresh-Token-Lebensdauer abgelaufen ist. Kann periodisch
    /// oder nach Save-Operationen aufgerufen werden, um die tokens.json klein zu halten.
    /// </summary>
    public int PurgeExpired()
    {
        var now = DateTime.UtcNow;
        var removed = 0;
        foreach (var kvp in _tokens)
        {
            var deadline = kvp.Value.RefreshExpiresAtUtc ?? kvp.Value.ExpiresAtUtc;
            if (deadline < now)
            {
                if (_tokens.TryRemove(kvp.Key, out _)) removed++;
            }
        }
        if (removed > 0) Save();
        return removed;
    }

    private void Save()
    {
        lock (_saveLock)
        {
            try
            {
                // Atomic write via Tmp-File + Move. File.WriteAllText truncated sofort und schreibt
                // dann — Power-Loss/SIGKILL mid-write → 0-byte-Datei → beim naechsten Start wirft
                // Load() JsonException, der catch loggt nur, und ALLE Tokens sind weg. Tmp+Move
                // verhindert das (Move ist atomar auf NTFS/ext4). Analog zu PiCredentialStore.
                var tmp = _path + ".tmp";
                var json = JsonSerializer.Serialize(_tokens.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tmp, json);
                if (!OperatingSystem.IsWindows())
                {
                    try { File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
                }
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tokens-Datei konnte nicht geschrieben werden");
            }
        }
    }
}

public sealed record TokenRecord(
    string Token,
    string RefreshToken,
    string DeviceId,
    string DeviceName,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    // Nullable fuer Migration von Legacy-Records (vor v1.3.0, hatten keine separate Refresh-Expiry).
    // Bei null faellt der Refresh-Check auf ExpiresAtUtc zurueck.
    DateTime? RefreshExpiresAtUtc = null);
