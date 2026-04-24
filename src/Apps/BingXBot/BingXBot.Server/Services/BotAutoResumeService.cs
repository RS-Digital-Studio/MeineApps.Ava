using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService (24.04.2026): Reaktiviert die Trading-Engine nach einem Server-Restart automatisch,
/// wenn vor dem letzten Shutdown der Bot lief (<see cref="BotSettings.WasRunningOnShutdown"/> = true).
///
/// Hintergrund: Der Pi-Server wurde durch update.sh / systemctl restart / Stromausfall neu gestartet.
/// Vorher lief die Engine. Nach dem Restart blieb die UI im "sucheB"-Cache und niemand merkte,
/// dass der Bot tot war — 3 Tage idle (siehe Diagnose 2026-04-24). Auto-Resume verhindert das.
///
/// Sicherheits-Bedingungen:
/// - <see cref="InitialDelay"/> 15s: Hosting-Setup, DB-Init, NTP-Sync, Tailscale-Connect, BingX-DNS
///   sollen sich nach Pi-Boot setzen koennen. 5s war zu knapp (NTP-Drift-Korrektur dauert 3-10s,
///   Tailscale-Verbindung 5-15s). Bei Server-Restart auf laufendem System wirken die 15s als
///   harmloses Wartefenster — Robert kann waehrenddessen noch manuell intervenieren.
/// - Try-Catch komplett: Resume darf den Server NICHT crashen.
/// - User-bewusste Stops (Stop/EmergencyStop in <see cref="Trading.Local.LocalBotControlService"/>)
///   setzen das Flag auf false → KEIN Re-Start. Auto-Resume greift NUR bei Crash/Reboot.
/// </summary>
public sealed class BotAutoResumeService : IHostedService, IDisposable
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

    private readonly IBotControlService _botControl;
    private readonly BotSettings _botSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly ILogger<BotAutoResumeService> _logger;

    /// <summary>
    /// Eigener Lebenszyklus-CTS (24.04.2026 Debugger-Fix Bug #5):
    /// NICHT den HostedService-CT verwenden — der wuerde Engine-StartAsync abbrechen, sobald
    /// der Server herunterfaehrt. Engine-Start ist nicht trivial atomar (BingX-Connect, Reconcile,
    /// PendingLimitOrder-Restore), Cancellation mitten drin koennte WasRunningOnShutdown in einem
    /// undefinierten Zustand belassen. Wir nutzen unseren eigenen CTS, den nur Dispose() canceled.
    /// </summary>
    private readonly CancellationTokenSource _lifetimeCts = new();

    public BotAutoResumeService(
        IBotControlService botControl,
        BotSettings botSettings,
        ScannerSettings scannerSettings,
        ILogger<BotAutoResumeService> logger)
    {
        _botControl = botControl;
        _botSettings = botSettings;
        _scannerSettings = scannerSettings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Microsoft-Best-Practice: HostedService.StartAsync NICHT blockieren — sonst haengt
        // die ganze Hosting-Pipeline. ConnectAsync (BingX) kann mehrere Sekunden dauern.
        // BEWUSST eigenen CTS uebergeben, NICHT cancellationToken (Bug #5).
        _ = Task.Run(() => ResumeAsync(_lifetimeCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Bewusst NICHT _lifetimeCts.Cancel(): wenn Engine-Start mitten in PendingLimitOrder-Reconcile
        // ist, wuerde Abbruch zu undefinierten Zustaenden fuehren. ResumeAsync ist eh entweder fertig
        // (Engine laeuft) oder blockiert auf einer Microsoft-managed Operation die selbst
        // CT-Aware ist. Beim Prozess-Tod stirbt sowieso alles.
        return Task.CompletedTask;
    }

    private async Task ResumeAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(InitialDelay, ct).ConfigureAwait(false);

            if (!_botSettings.WasRunningOnShutdown)
            {
                _logger.LogInformation(
                    "Auto-Resume: WasRunningOnShutdown=false. Bot bleibt gestoppt — User muss manuell Start druecken.");
                return;
            }

            var tfs = _scannerSettings.ActiveTimeframes?.ToList() ?? new List<TimeFrame>();
            _logger.LogInformation(
                "Auto-Resume: Engine wird im {Mode}-Modus mit Timeframes [{Tfs}] reaktiviert (vor Shutdown lief der Bot).",
                _botSettings.LastMode, string.Join(",", tfs));

            var request = new BotStartRequest(_botSettings.LastMode, InitialBalance: null, ActiveTimeframes: tfs);
            // Bewusst CancellationToken.None weiterreichen — Engine-Start ist atomar zu betrachten.
            var status = await _botControl.StartAsync(request, CancellationToken.None).ConfigureAwait(false);

            if (status.State == BotState.Running)
            {
                _logger.LogInformation("Auto-Resume erfolgreich. State={State}, Mode={Mode}.",
                    status.State, status.Mode);
            }
            else
            {
                _logger.LogWarning("Auto-Resume nicht im Running-State. State={State}, LastError={Error}.",
                    status.State, status.LastError ?? "(kein Fehler)");
            }
        }
        catch (OperationCanceledException)
        {
            // Lifecycle-CTS canceled — Server wird heruntergefahren waehrend wir noch im 15s-Delay warten.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Auto-Resume fehlgeschlagen — Bot bleibt gestoppt. User muss manuell Start druecken.");
        }
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
    }
}
