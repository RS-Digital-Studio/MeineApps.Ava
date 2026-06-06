using System.Net.Http.Json;
using System.Text.Json;
using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services.Anker;

/// <summary>
/// HTTPS-Client für die inoffizielle Anker-Power-Cloud (passport/login → Geräteliste → MQTT-Info).
/// Portiert aus thomluther/anker-solix-api (session.py/apitypes.py). Der Host wird regional nach
/// Länder-Code gewählt (EU vs. COM). Authentifizierte Calls senden x-auth-token + gtoken.
/// </summary>
public sealed class AnkerCloudClient(HttpClient http)
{
    private const string LoginPath = "passport/login";
    private const string BindDevicesPath = "power_service/v1/app/get_relate_and_bind_devices";
    private const string MqttInfoPath = "app/devicemanage/get_user_mqtt_info";

    private static readonly HashSet<string> EuCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "DE", "AT", "CH", "FR", "IT", "ES", "NL", "BE", "LU", "GB", "IE", "PT", "PL", "CZ",
        "SK", "HU", "SE", "DK", "FI", "NO", "IS", "SI", "HR", "RO", "BG", "GR", "EE", "LV",
        "LT", "CY", "MT",
    };

    private string _host = "https://ankerpower-api-eu.anker.com";
    private string _country = "DE";

    /// <summary>Meldet sich an und liefert die Session (Token + gtoken). Wirft bei Fehler.</summary>
    public async Task<AnkerSession> LoginAsync(AnkerCredentials creds, CancellationToken ct)
    {
        _country = creds.CountryId.ToUpperInvariant();
        _host = EuCountries.Contains(_country)
            ? "https://ankerpower-api-eu.anker.com"
            : "https://ankerpower-api.anker.com";

        var (pubHex, encPassword) = AnkerCrypto.EncryptPassword(creds.Password);
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);

        var body = new
        {
            ab = _country,
            client_secret_info = new { public_key = pubHex },
            enc = 0,
            email = creds.Email,
            password = encPassword,
            time_zone = (long)offset.TotalMilliseconds,
            transaction = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
        };

        using var data = await PostAsync(LoginPath, body, authToken: null, gToken: null, ct);
        var root = data.RootElement;

        var userId = root.GetProperty("user_id").GetString() ?? throw new InvalidOperationException("user_id fehlt");
        var authToken = root.GetProperty("auth_token").GetString() ?? throw new InvalidOperationException("auth_token fehlt");
        var expires = root.TryGetProperty("token_expires_at", out var exp) && exp.TryGetInt64(out var sec)
            ? DateTimeOffset.FromUnixTimeSeconds(sec).UtcDateTime
            : DateTime.UtcNow.AddHours(1);
        var geoKey = root.TryGetProperty("geo_key", out var gk) ? gk.GetString() ?? "" : "";

        return new AnkerSession(authToken, userId, AnkerCrypto.GToken(userId), geoKey, expires);
    }

    /// <summary>Liefert die mit dem Account verbundenen Geräte (für Topic-Aufbau + Trigger).</summary>
    public async Task<List<AnkerDevice>> GetDevicesAsync(AnkerSession session, CancellationToken ct)
    {
        using var data = await PostAsync(BindDevicesPath, new { }, session.AuthToken, session.GToken, ct);
        var devices = new List<AnkerDevice>();
        CollectDevices(data.RootElement, devices);
        return devices;
    }

    /// <summary>Holt die MQTT-Verbindungs-Bausteine (Zertifikate, Endpoint, Topic-Prefix).</summary>
    public async Task<AnkerMqttInfo> GetMqttInfoAsync(AnkerSession session, CancellationToken ct)
    {
        using var data = await PostAsync(MqttInfoPath, new { }, session.AuthToken, session.GToken, ct);
        var r = data.RootElement;
        string Get(string name) => r.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

        return new AnkerMqttInfo(
            EndpointAddr: Get("endpoint_addr"),
            ThingName: Get("thing_name"),
            AppName: string.IsNullOrWhiteSpace(Get("app_name")) ? "anker_power" : Get("app_name"),
            CertificateId: Get("certificate_id"),
            UserId: string.IsNullOrWhiteSpace(Get("user_id")) ? session.UserId : Get("user_id"),
            CertificatePem: Get("certificate_pem"),
            PrivateKeyPem: Get("private_key"),
            RootCaPem: Get("aws_root_ca1_pem"));
    }

    /// <summary>Durchsucht die Response rekursiv nach Objekten mit device_sn und sammelt sie.</summary>
    private static void CollectDevices(JsonElement element, List<AnkerDevice> devices)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("device_sn", out var snEl) && snEl.ValueKind == JsonValueKind.String)
                {
                    var sn = snEl.GetString() ?? "";
                    var pn = element.TryGetProperty("device_pn", out var pnEl) ? pnEl.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(pn) && element.TryGetProperty("product_code", out var pcEl))
                        pn = pcEl.GetString() ?? "";
                    var name = element.TryGetProperty("device_name", out var nmEl) ? nmEl.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(sn) && devices.All(d => d.DeviceSn != sn))
                        devices.Add(new AnkerDevice(sn, pn, name));
                }
                foreach (var prop in element.EnumerateObject())
                    CollectDevices(prop.Value, devices);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectDevices(item, devices);
                break;
        }
    }

    /// <summary>
    /// Sendet einen POST, prüft den Anker-Umschlag (code == 0) und gibt das geklonte data-Objekt
    /// zurück. Die Anker-Header (model-type/app-name/os-type/country/timezone + optional Auth) werden
    /// pro Request gesetzt.
    /// </summary>
    private async Task<JsonDocument> PostAsync(string path, object body, string? authToken, string? gToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_host}/{path}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.TryAddWithoutValidation("model-type", "DESKTOP");
        req.Headers.TryAddWithoutValidation("app-name", "anker_power");
        req.Headers.TryAddWithoutValidation("os-type", "android");
        req.Headers.TryAddWithoutValidation("country", _country);
        req.Headers.TryAddWithoutValidation("timezone", AnkerCrypto.TimezoneHeader(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)));
        if (!string.IsNullOrEmpty(authToken)) req.Headers.TryAddWithoutValidation("x-auth-token", authToken);
        if (!string.IsNullOrEmpty(gToken)) req.Headers.TryAddWithoutValidation("gtoken", gToken);

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci) ? ci : -1;
        if (code != 0)
        {
            var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : null;
            throw new AnkerCloudException(code, msg ?? "Unbekannter Anker-Cloud-Fehler");
        }
        if (!root.TryGetProperty("data", out var dataEl))
            throw new AnkerCloudException(code, "Antwort ohne data-Feld");

        // data herauslösen und als eigenständiges Dokument zurückgeben (doc wird disposed).
        return JsonDocument.Parse(dataEl.GetRawText());
    }
}

/// <summary>Fehler der Anker-Cloud (code != 0 im Antwort-Umschlag).</summary>
public sealed class AnkerCloudException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
