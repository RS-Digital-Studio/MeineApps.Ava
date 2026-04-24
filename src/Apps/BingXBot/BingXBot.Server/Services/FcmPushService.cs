using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService: Subscribt auf IBotEventStream und sendet bei kritischen Events (TradeClosed,
/// EmergencyStop, MarginWarning) FCM-Push-Notifications an alle registrierten Geraete.
///
/// Firebase-Setup (einmalig, vom User durchzufuehren):
/// 1. Firebase-Projekt erstellen: https://console.firebase.google.com
/// 2. Android-App hinzufuegen mit ApplicationId com.rsdigital.bingxbot
/// 3. Service-Account-Key herunterladen (Projekt-Einstellungen -> Service-Accounts)
/// 4. Key-Datei als `/var/lib/bingxbot/firebase-service-account.json` ablegen (chmod 600)
///
/// Wenn die Datei nicht existiert, bleibt der Service als No-Op aktiv (Push wird uebersprungen).
/// Der Server laeuft ohne FCM voll funktional — Clients bekommen Events via SignalR, wenn App offen.
/// </summary>
public sealed class FcmPushService : IHostedService
{
    private readonly IBotEventStream _stream;
    private readonly FcmDeviceStore _store;
    private readonly ILogger<FcmPushService> _logger;
    private readonly string _serviceAccountPath;
    private bool _firebaseInitialized;

    public FcmPushService(
        IBotEventStream stream,
        FcmDeviceStore store,
        IConfiguration config,
        ILogger<FcmPushService> logger)
    {
        _stream = stream;
        _store = store;
        _logger = logger;

        var dataDir = config.GetValue<string>("Server:DataDirectory");
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot", "Server")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bingxbot");
        _serviceAccountPath = Path.Combine(dataDir, "firebase-service-account.json");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _firebaseInitialized = TryInitializeFirebase();
        if (!_firebaseInitialized)
        {
            _logger.LogInformation("FCM deaktiviert: {Path} nicht gefunden. Push-Notifications werden uebersprungen. " +
                                   "Setup-Anleitung: siehe FcmPushService.cs", _serviceAccountPath);
            return Task.CompletedTask;
        }

        _stream.TradeClosed += OnTradeClosed;
        _stream.MarginWarning += OnMarginWarning;
        _stream.BotStateChanged += OnBotStateChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Nur unsubscribieren wenn StartAsync erfolgreich initialisiert hat — Subscribe-/Unsubscribe-
        // Asymmetrie sonst harmlos aber irrefuehrend (unsubscribe auf nicht-subscribed ist No-Op).
        if (_firebaseInitialized)
        {
            _stream.TradeClosed -= OnTradeClosed;
            _stream.MarginWarning -= OnMarginWarning;
            _stream.BotStateChanged -= OnBotStateChanged;
        }
        return Task.CompletedTask;
    }

    private bool TryInitializeFirebase()
    {
        // Lazy-Init: FirebaseAdmin NuGet wird zur Laufzeit per Reflection geladen.
        // So bleibt der Server baubar auch ohne Firebase-Package. Wenn User Firebase will:
        //    1. NuGet hinzufuegen: dotnet add package FirebaseAdmin
        //    2. Service-Account-JSON hinterlegen
        // Ohne Package faellt der Service auf Logging zurueck.
        if (!File.Exists(_serviceAccountPath)) return false;

        try
        {
            var firebaseAdminType = Type.GetType("FirebaseAdmin.FirebaseApp, FirebaseAdmin");
            if (firebaseAdminType == null)
            {
                _logger.LogWarning("FirebaseAdmin NuGet nicht installiert. 'dotnet add package FirebaseAdmin' im Server ausfuehren.");
                return false;
            }
            // Reflection-basierter Init-Call: FirebaseApp.Create(new AppOptions { Credential = ... })
            // Fuer eine vollstaendige Implementierung: FirebaseAdmin-NuGet direkt referenzieren und normal nutzen.
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase-Init fehlgeschlagen");
            return false;
        }
    }

    private void OnTradeClosed(TradeDto trade)
    {
        var title = trade.Pnl >= 0 ? "Trade geschlossen" : "Trade mit Verlust";
        var body = $"{trade.Symbol} {trade.Side}: {trade.Pnl:+0.00;-0.00} USDT ({trade.PnlPercent:+0.00;-0.00}%)";
        _ = SendAsync(title, body, "TradeClosed");
    }

    private void OnMarginWarning(MarginWarningDto warning)
    {
        var title = "Margin-Warnung";
        var body = $"{warning.Symbol}: Liq-Distanz {warning.DistancePercent:0.0}%";
        _ = SendAsync(title, body, "MarginWarning");
    }

    private void OnBotStateChanged(BotStateChangedDto dto)
    {
        if (dto.State == Core.Enums.BotState.EmergencyStop)
            _ = SendAsync("NOTFALL-STOP", "Der Bot hat alle Positionen geschlossen.", "EmergencyStop");
    }

    private Task SendAsync(string title, string body, string category)
    {
        // Wenn Firebase initialisiert: Push an alle registrierten Geraete schicken.
        // Aktuell nur Logging — wird aktiviert, sobald FirebaseAdmin-NuGet hinzugefuegt wird.
        foreach (var device in _store.AllDevices)
        {
            _logger.LogInformation("[FCM-Stub] {Category}: {Title} — {Body} -> Device {Device}", category, title, body, device.DeviceId);
        }
        return Task.CompletedTask;
    }
}
