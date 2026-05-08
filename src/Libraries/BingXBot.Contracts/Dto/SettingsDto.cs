using BingXBot.Core.Configuration;

namespace BingXBot.Contracts.Dto;

/// <summary>
/// Vollstaendiger Settings-Snapshot. Wird per GET /settings geholt und bei Aenderungen
/// per SignalR-Event SettingsChanged gepusht (damit andere Clients synchronisieren).
///
/// Hinweis: Die Core-Klassen (RiskSettings/ScannerSettings/BotSettings/BacktestSettings)
/// sind bereits einfache POCOs mit Public-Properties. Wir uebertragen sie direkt als DTOs,
/// kein separates Mapping noetig.
/// </summary>
public record FullSettingsDto(
    BotSettings Bot,
    RiskSettings Risk,
    ScannerSettings Scanner,
    BacktestSettings Backtest,
    int Revision);

/// <summary>Credentials-Status fuer UI (KEIN API-Key/Secret im Klartext!).</summary>
public record CredentialsStatusDto(
    bool HasCredentials,
    bool IsConnected,
    string? ApiKeyMasked,
    DateTime? LastConnectAttemptUtc,
    string? LastError);

/// <summary>Request: BingX-Credentials auf den Pi uebertragen (HTTPS / Tailnet / LAN).</summary>
public record SetCredentialsRequest(
    string ApiKey,
    string ApiSecret);

/// <summary>v1.6.3 Phase 14 — Settings-Audit-Trail-Eintrag (Wire-DTO).</summary>
public record SettingsChangeDto(
    DateTime Timestamp,
    string Field,
    string? OldValue,
    string? NewValue,
    string Source,
    string? Snapshot);

/// <summary>v1.6.3 Phase 14 — Antwort-DTO fuer GET /settings/history.</summary>
public record SettingsHistoryDto(
    IReadOnlyList<SettingsChangeDto> Changes);

/// <summary>FCM-Device-Registrierung (Phase 5.7).</summary>
public record FcmDeviceRegistrationDto(
    string DeviceId,
    string FcmToken,
    string Platform);
