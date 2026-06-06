namespace SunSeeker.Shared.Models;

/// <summary>
/// Vom Nutzer eingegebene Anker-Cloud-Zugangsdaten. Hinweis: Ein per "Mit Google anmelden"
/// erstellter Anker-Account hat kein API-taugliches Passwort — der Nutzer muss zuerst über
/// "Passwort vergessen" mit der Gmail-Adresse ein Anker-Passwort setzen (oder ein dediziertes
/// E-Mail-Konto anlegen und das System teilen).
/// </summary>
public sealed record AnkerCredentials(string Email, string Password, string CountryId);

/// <summary>Ergebnis des Anker-Logins (passport/login).</summary>
public sealed record AnkerSession(
    string AuthToken,
    string UserId,
    string GToken,
    string GeoKey,
    DateTime ExpiresUtc);

/// <summary>Ein mit dem Account verbundenes Gerät (für Topic-Aufbau + Realtime-Trigger).</summary>
public sealed record AnkerDevice(string DeviceSn, string DevicePn, string DeviceName);

/// <summary>
/// MQTT-Verbindungs-Bausteine aus app/devicemanage/get_user_mqtt_info. Anker frontet AWS-IoT-Core
/// per eigener DNS und authentifiziert über client-zertifikat-basiertes mTLS (Port 8883) — KEIN
/// SigV4/WebSocket. Die drei PEM-Strings sind Root-CA, Client-Zertifikat und privater Schlüssel.
/// </summary>
public sealed record AnkerMqttInfo(
    string EndpointAddr,
    string ThingName,
    string AppName,
    string CertificateId,
    string UserId,
    string CertificatePem,
    string PrivateKeyPem,
    string RootCaPem);
