namespace BingXBot.Contracts.Dto;

/// <summary>
/// Pairing-Ablauf:
/// 1. Client ruft /pair/init mit seinem Geraetenamen auf.
/// 2. Server generiert 6-stelligen Code, zeigt im Terminal + Datei ~/.config/bingxbot/pairing-code.txt an.
///    Codes sind 5 Minuten gueltig.
/// 3. Nutzer liest den Code vom Pi und gibt ihn im Client ein.
/// 4. Client ruft /pair/complete mit Code + DeviceId auf.
/// 5. Server verifiziert Code -> gibt Token + RefreshToken zurueck.
/// 6. Client persistiert Token (SecureStorage) und nutzt ihn als Bearer.
/// </summary>
public record PairInitRequest(string DeviceName);

public record PairCancelRequest(string PairingId);

public record PairInitResponse(
    string PairingId,
    int CodeLengthDigits,
    int ExpiresInSeconds);

public record PairCompleteRequest(
    string PairingId,
    string Code,
    string DeviceId);

public record PairCompleteResponse(
    string Token,
    string RefreshToken,
    int TokenLifetimeSeconds,
    int SchemaVersion);

public record TokenRefreshRequest(string RefreshToken);

public record TokenRefreshResponse(
    string Token,
    string RefreshToken,
    int TokenLifetimeSeconds);

public record HealthResponse(
    string Status,
    int SchemaVersion,
    long UptimeSeconds);

public record ErrorResponse(
    string Error,
    string? Detail = null,
    string? CorrelationId = null);
