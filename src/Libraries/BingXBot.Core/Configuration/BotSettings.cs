using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class BotSettings
{
    public RiskSettings Risk { get; set; } = new();
    public ScannerSettings Scanner { get; set; } = new();
    public BacktestSettings Backtest { get; set; } = new();
    public TradingMode LastMode { get; set; } = TradingMode.Paper;
    public string? LastStrategyName { get; set; }
    public Dictionary<string, string> StrategyParameters { get; set; } = new();
    public bool ShowBtcTicker { get; set; } = true;

    /// <summary>Startkapital für Paper-Trading in USDT.</summary>
    public decimal PaperInitialBalance { get; set; } = 10_000m;

    // === Benachrichtigungen ===
    /// <summary>Desktop-Benachrichtigungen bei Trade-Eröffnung/-Schließung.</summary>
    public bool EnableDesktopNotifications { get; set; } = true;

    // === Funding-Rate für Simulation ===
    /// <summary>Simulierte Funding-Rate pro 8h für Paper/Backtest (in %, z.B. 0.01 = 0.01%).</summary>
    public decimal SimulatedFundingRatePercent { get; set; } = 0.01m;

    // === Remote-Modus (Client verbindet zu Pi-Server via HTTP + SignalR) ===
    /// <summary>Wenn true: Client nutzt Remote-Services (HTTP/SignalR). Wenn false: Engine laeuft lokal.</summary>
    public bool UseRemoteMode { get; set; }

    /// <summary>Server-URL (z.B. http://bingxbot.local:5050 oder Tailscale-IP:5050).</summary>
    public string? ServerUrl { get; set; }

    // === Auto-Resume nach Server-Restart (24.04.2026) ===
    /// <summary>
    /// Wird vom Server gesetzt: true sobald der Bot via <see cref="Contracts.Services.IBotControlService.StartAsync"/>
    /// laeuft, false bei Stop/EmergencyStop. Ueberlebt Pi-Reboots/systemd-Restarts.
    /// Beim naechsten Server-Start liest <c>BotAutoResumeService</c> dieses Flag und reaktiviert
    /// die Trading-Engine automatisch im zuletzt aktiven <see cref="LastMode"/> mit den persistierten
    /// <see cref="ScannerSettings.ActiveTimeframes"/>.
    ///
    /// WICHTIG: Server-Authority — der Client darf dieses Flag NICHT veraendern (siehe LocalSettingsService.SaveBotAsync,
    /// gleiche Schutz-Logik wie LastMode). Sonst kippt jeder Client-Save den Auto-Resume-Wunsch zurueck auf Default false.
    /// </summary>
    public bool WasRunningOnShutdown { get; set; }
}
