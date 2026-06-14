using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MeineApps.Core.Ava.Services;
using MQTTnet;
using MQTTnet.Formatter;
using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services.Anker;

/// <summary>
/// Echte Live-Watt-Anbindung der Anker-Powerstation über die inoffizielle Anker-Cloud + MQTT (mTLS).
/// Ablauf: Login (passport/login) → Geräteliste → get_user_mqtt_info → mTLS-MQTT (Port 8883) →
/// Subscribe je Gerät → Realtime-Trigger → Telemetrie dekodieren (A1783: Feld a6/04 = DC-Eingang in W).
///
/// Ohne hinterlegte Zugangsdaten läuft der <see cref="MockAnkerMonitorService"/> als Demo
/// (<see cref="IsSimulated"/> = true), damit der Leistungs-Tab nicht leer ist; sobald Zugangsdaten
/// gesetzt sind, wird die echte Verbindung versucht (bei Fehler → <see cref="AnkerConnectionState.Error"/>).
/// MQTTnet + System.Security.Cryptography sind plattformneutral → läuft auf Android und Desktop.
/// </summary>
public sealed class AnkerMonitorService : IAnkerMonitorService, IDisposable
{
    private static readonly HashSet<string> PpsModels = new(StringComparer.OrdinalIgnoreCase) { "A1783", "A1785" };

    private readonly IPreferencesService _prefs;
    private readonly MockAnkerMonitorService _demo;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, int> _dcWattsBySn = [];

    private IMqttClient? _mqtt;
    private List<AnkerDevice> _devices = [];
    private AnkerMqttInfo? _mqttInfo;
    private System.Timers.Timer? _triggerTimer;
    private System.Security.Cryptography.X509Certificates.X509Certificate2? _clientCert;
    private System.Timers.Timer? _reconnectTimer;
    private int _reconnectAttempts;
    private bool _demoActive;
    private bool _wantConnected; // true, solange eine echte Verbindung gehalten/wiederhergestellt werden soll

    public AnkerMonitorService(IPreferencesService prefs, MockAnkerMonitorService demo)
    {
        _prefs = prefs;
        _demo = demo;
        _demo.SampleReceived += OnDemoSample;
    }

    public AnkerConnectionState State { get; private set; } = AnkerConnectionState.Disconnected;

    public double CurrentSolarWatts { get; private set; }

    /// <summary>True, solange der Demo-Generator läuft (keine Zugangsdaten) — die UI weist darauf hin.</summary>
    public bool IsSimulated { get; private set; } = true;

    /// <summary>Letzte Fehlermeldung der echten Verbindung (für die UI). Null bei Erfolg/Demo.</summary>
    public string? LastError { get; private set; }

    public event EventHandler<PowerSample>? SampleReceived;

    public event EventHandler<AnkerConnectionState>? StateChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync();

            var creds = AnkerCredentialStore.Load(_prefs);
            if (creds is null)
            {
                StartDemo();
                return;
            }

            IsSimulated = false;
            _wantConnected = true;
            LastError = null;
            SetState(AnkerConnectionState.Connecting);
            await ConnectRealAsync(creds, cancellationToken);
        }
        catch (Exception ex)
        {
            LastError = ex is AnkerCloudException ace ? ace.Message : DescribeException(ex);
            SetState(AnkerConnectionState.Error);
            // Vorübergehende (Netzwerk-/TLS-)Fehler mit Backoff erneut versuchen; echte Cloud-Fehler
            // (falsches Passwort, Konto) NICHT — die muss der Nutzer beheben.
            if (_wantConnected && ex is not AnkerCloudException)
                ScheduleReconnect();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Disconnect() => _ = DisconnectAsync();

    private async Task DisconnectAsync()
    {
        _wantConnected = false; // vor StopInternalAsync setzen → der MQTT-DisconnectedAsync-Handler reconnectet nicht
        await _gate.WaitAsync();
        try
        {
            await StopInternalAsync();
            SetState(AnkerConnectionState.Disconnected);
        }
        finally { _gate.Release(); }
    }

    // ── Demo-Modus ────────────────────────────────────────────────────────────

    private void StartDemo()
    {
        _wantConnected = false;
        _demoActive = true;
        IsSimulated = true;
        _ = _demo.ConnectAsync();
        SetState(AnkerConnectionState.Connected);
    }

    private void OnDemoSample(object? sender, PowerSample sample)
    {
        if (!_demoActive) return;
        CurrentSolarWatts = sample.SolarWatts;
        SampleReceived?.Invoke(this, sample);
    }

    // ── Echte Verbindung ──────────────────────────────────────────────────────

    private async Task ConnectRealAsync(AnkerCredentials creds, CancellationToken ct)
    {
        var cloud = new AnkerCloudClient(_http);
        var session = await cloud.LoginAsync(creds, ct);
        _devices = await cloud.GetDevicesAsync(session, ct);
        _mqttInfo = await cloud.GetMqttInfoAsync(session, ct);

        if (string.IsNullOrWhiteSpace(_mqttInfo.EndpointAddr) || string.IsNullOrWhiteSpace(_mqttInfo.CertificatePem))
            throw new AnkerCloudException(0, "Keine MQTT-Zugangsdaten erhalten (get_user_mqtt_info leer)");

        // Altes Client-Zertifikat (vorheriger Connect/Reconnect) freigeben — natives Krypto-Handle,
        // das sonst erst beim GC-Finalizer schliesst und ueber haeufige Reconnects akkumuliert.
        _clientCert?.Dispose();
        var clientCert = LoadClientCertificate(_mqttInfo);
        _clientCert = clientCert;

        var clientId = $"{_mqttInfo.ThingName}_{Random.Shared.Next(0, 99999):D5}";
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttInfo.EndpointAddr, 8883)
            .WithClientId(clientId)
            .WithCleanSession(true)
            .WithProtocolVersion(MqttProtocolVersion.V311);

        var nativeTls = App.AnkerSecureStreamFactory;
        if (nativeTls is not null)
        {
            // Android: SslStream beherrscht kein Client-Zertifikat-mTLS (Interop+AndroidCrypto+SslException)
            // → TLS plattform-nativ aufbauen (SSLContext/KeyManager) und MQTTnet nur den fertigen Stream
            // durchreichen. Client-Cert als PKCS12 (zufälliges Passwort) an den nativen Aufbau übergeben.
            var pfxPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
            var pkcs12 = clientCert.Export(X509ContentType.Pkcs12, pfxPassword);
            var tlsStream = await nativeTls(
                new AnkerTlsParams(_mqttInfo.EndpointAddr, 8883, pkcs12, pfxPassword, _mqttInfo.RootCaPem), ct);
            _mqtt = new MqttClientFactory().CreateMqttClient(
                new PreConnectedMqttAdapterFactory(tlsStream, new DnsEndPoint(_mqttInfo.EndpointAddr, 8883), clientCert));
        }
        else
        {
            // Desktop: MQTTnet baut TCP+TLS selbst — Client-Zertifikat funktioniert dort.
            optionsBuilder.WithTlsOptions(o =>
            {
                o.UseTls(true);
                o.WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12);
                o.WithClientCertificates([clientCert]);
                o.WithCertificateValidationHandler(ctx => ValidateServerCert(ctx, _mqttInfo.RootCaPem));
            });
            _mqtt = new MqttClientFactory().CreateMqttClient();
        }

        _mqtt.ApplicationMessageReceivedAsync += OnMqttMessageAsync;
        _mqtt.DisconnectedAsync += _ =>
        {
            // Unerwarteter Abbruch (Token abgelaufen, Netzwerk, Broker): mit Backoff neu verbinden —
            // der frische Login erneuert zugleich das abgelaufene Token. Bei gewolltem Trennen ist
            // _wantConnected bereits false.
            if (_wantConnected && !_demoActive)
            {
                SetState(AnkerConnectionState.Connecting);
                ScheduleReconnect();
            }
            return Task.CompletedTask;
        };

        await _mqtt.ConnectAsync(optionsBuilder.Build(), ct);

        foreach (var dev in _devices)
        {
            var pn = string.IsNullOrWhiteSpace(dev.DevicePn) ? "" : dev.DevicePn;
            if (string.IsNullOrWhiteSpace(pn)) continue;
            var topic = $"dt/{_mqttInfo.AppName}/{pn}/{dev.DeviceSn}/#";
            await _mqtt.SubscribeAsync(topic, cancellationToken: ct);
        }

        SetState(AnkerConnectionState.Connected);
        _reconnectAttempts = 0; // erfolgreicher Connect → Backoff zurücksetzen
        await PublishTriggerAsync(ct);

        _triggerTimer = new System.Timers.Timer(30_000) { AutoReset = true };
        _triggerTimer.Elapsed += (_, _) => _ = PublishTriggerAsync(CancellationToken.None);
        _triggerTimer.Start();
    }

    private async Task PublishTriggerAsync(CancellationToken ct)
    {
        if (_mqtt is not { IsConnected: true } client || _mqttInfo is null) return;
        var trigger = AnkerHexFrame.BuildRealtimeTrigger((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var triggerB64 = Convert.ToBase64String(trigger);

        foreach (var dev in _devices)
        {
            if (string.IsNullOrWhiteSpace(dev.DevicePn) || !PpsModels.Contains(dev.DevicePn)) continue;
            try
            {
                var inner = JsonSerializer.Serialize(new
                {
                    device_sn = dev.DeviceSn,
                    account_id = _mqttInfo.UserId,
                    data = triggerB64,
                });
                var message = new
                {
                    head = new
                    {
                        version = "1.0.0.1",
                        client_id = $"android-{_mqttInfo.AppName}-{_mqttInfo.UserId}-{_mqttInfo.CertificateId}",
                        sess_id = "1234-5678",
                        msg_seq = 1,
                        seed = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        cmd_status = 2,
                        cmd = 17,
                        sign_code = 1,
                        device_pn = dev.DevicePn,
                        device_sn = dev.DeviceSn,
                    },
                    payload = inner,
                };
                var topic = $"cmd/{_mqttInfo.AppName}/{dev.DevicePn}/{dev.DeviceSn}/req";
                await client.PublishStringAsync(topic, JsonSerializer.Serialize(message), cancellationToken: ct);
            }
            catch
            {
                // Trigger ist Best-Effort: schlägt er fehl, kommen weiterhin (langsamere) 0900-Updates.
            }
        }
    }

    private Task OnMqttMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var parts = e.ApplicationMessage.Topic.Split('/');
            var pn = parts.Length > 2 ? parts[2] : "";
            var sn = parts.Length > 3 ? parts[3] : "";
            if (!PpsModels.Contains(pn)) return Task.CompletedTask;

            var outerJson = e.ApplicationMessage.ConvertPayloadToString();
            if (string.IsNullOrEmpty(outerJson)) return Task.CompletedTask;
            using var outer = JsonDocument.Parse(outerJson);
            if (!outer.RootElement.TryGetProperty("payload", out var payloadEl)) return Task.CompletedTask;

            using var inner = JsonDocument.Parse(payloadEl.GetString() ?? "{}");
            var dataB64 = inner.RootElement.TryGetProperty("data", out var d) ? d.GetString()
                : inner.RootElement.TryGetProperty("trans", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(dataB64)) return Task.CompletedTask;

            var frame = AnkerHexFrame.TryDecode(Convert.FromBase64String(dataB64));
            if (frame is null) return Task.CompletedTask;

            var dc = AnkerHexFrame.A1783DcInputWatts(frame);
            if (dc is null) return Task.CompletedTask;

            _dcWattsBySn[sn] = Math.Max(0, dc.Value);
            CurrentSolarWatts = _dcWattsBySn.Values.Sum();
            IsSimulated = false;
            SampleReceived?.Invoke(this, new PowerSample(DateTime.UtcNow, CurrentSolarWatts));
        }
        catch
        {
            // Defekte/fremde Nachricht ignorieren — nächste Telemetrie kommt gleich.
        }
        return Task.CompletedTask;
    }

    // ── TLS-Helfer ──────────────────────────────────────────────────────────

    /// <summary>Client-Zertifikat aus PEM-Cert + PEM-Key; Pkcs12-Roundtrip macht den privaten Schlüssel beim TLS-Handshake nutzbar (Windows-Schannel/Android).</summary>
    private static X509Certificate2 LoadClientCertificate(AnkerMqttInfo info)
    {
        using var fromPem = X509Certificate2.CreateFromPem(info.CertificatePem, info.PrivateKeyPem);
        return X509CertificateLoader.LoadPkcs12(fromPem.Export(X509ContentType.Pkcs12), null);
    }

    private static bool ValidateServerCert(MqttClientCertificateValidationEventArgs ctx, string rootCaPem)
    {
        if (ctx.SslPolicyErrors == SslPolicyErrors.None) return true; // Amazon Root CA 1 i.d.R. im System-Trust
        if (ctx.Certificate is null) return false;
        try
        {
            using var rootCa = X509Certificate2.CreateFromPem(rootCaPem);
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(rootCa);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            var cert2 = X509CertificateLoader.LoadCertificate(ctx.Certificate.Export(X509ContentType.Cert));
            return chain.Build(cert2);
        }
        catch
        {
            return false;
        }
    }

    // ── Aufräumen ─────────────────────────────────────────────────────────────

    private async Task StopInternalAsync()
    {
        CancelReconnect();
        _demoActive = false;
        _demo.Disconnect();

        if (_triggerTimer is { } timer)
        {
            timer.Stop();
            timer.Dispose();
            _triggerTimer = null;
        }

        if (_mqtt is { } client)
        {
            client.ApplicationMessageReceivedAsync -= OnMqttMessageAsync;
            try { if (client.IsConnected) await client.DisconnectAsync(); } catch { /* ignore */ }
            client.Dispose();
            _mqtt = null;
        }

        // Client-Zertifikat (natives Handle) nach dem MQTT-Client freigeben.
        _clientCert?.Dispose();
        _clientCert = null;

        _dcWattsBySn.Clear();
        CurrentSolarWatts = 0;
    }

    // ── Reconnect mit exponentiellem Backoff ──────────────────────────────────

    /// <summary>Plant einen Wiederverbindungsversuch (5/10/20/40/60 s Backoff). Ein erneuter
    /// <see cref="ConnectAsync"/> macht einen frischen Login (erneuert das Token) und MQTT-Connect.</summary>
    private void ScheduleReconnect()
    {
        if (!_wantConnected) return;
        var attempt = Interlocked.Increment(ref _reconnectAttempts);
        var delayMs = Math.Min(60_000d, 5_000d * Math.Pow(2, Math.Min(attempt - 1, 4)));
        _reconnectTimer?.Stop();
        _reconnectTimer?.Dispose();
        _reconnectTimer = new System.Timers.Timer(delayMs) { AutoReset = false };
        _reconnectTimer.Elapsed += (_, _) =>
        {
            if (_wantConnected && !_demoActive) _ = ConnectAsync();
        };
        _reconnectTimer.Start();
    }

    private void CancelReconnect()
    {
        _reconnectTimer?.Stop();
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
    }

    /// <summary>Vollständige Exception-Kette (Typ + Message je Ebene) — macht die eigentliche Ursache
    /// hinter generischen Meldungen wie "The SSL connection could not be established" sichtbar.</summary>
    private static string DescribeException(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? e = ex; e is not null; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");
        return string.Join(" -> ", parts);
    }

    private void SetState(AnkerConnectionState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        _wantConnected = false;
        _demo.SampleReceived -= OnDemoSample;
        _reconnectTimer?.Dispose();
        _triggerTimer?.Dispose();
        _mqtt?.Dispose();
        _http.Dispose();
        _gate.Dispose();
    }
}
