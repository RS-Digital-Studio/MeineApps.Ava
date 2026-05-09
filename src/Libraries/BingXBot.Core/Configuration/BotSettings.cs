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

    /// <summary>
    /// UI-Theme-Vorliebe (Dark/Light/System). System folgt dem Betriebssystem-Theme.
    /// Default Dark — passt zu Trading-Terminal-UI und ist seit v1.0 der Standard.
    /// </summary>
    public ThemePreference ThemePreference { get; set; } = ThemePreference.Dark;

    /// <summary>
    /// v1.5.2 Phase 4 — Decision-Trail / Rejection-Log aktivieren.
    /// Default true — der Trail haelt eine Diagnose-Liste der letzten N Strategy-Evaluations
    /// (warum hat das Setup nicht gefeuert?) im Memory-Ringpuffer (5000 Eintraege).
    /// Bei false: Hot-Path baut keine <c>EvaluationDecision</c>-Records (kein Allocation-Overhead).
    /// </summary>
    public bool EnableDecisionTrail { get; set; } = true;

    /// <summary>
    /// v1.5.5 Phase 9 — Trade-Push-Notifications via FCM (TradeOpened / TradeClosed / SL-Hit).
    /// Default true — Pi-Server pusht Trade-Events an gepairte Mobile-Clients. Bei false
    /// wird der TradePushSubscriber ausgehaengt und feuert nicht mehr.
    /// </summary>
    public bool EnableTradePushNotifications { get; set; } = true;

    /// <summary>
    /// Phase 18 / A7 — Erlaubte Crypto-Trading-Sessions als Bitmask (Asia/EU/EU-US-Overlap/US).
    /// Crypto handelt 24/7, aber Liquiditaet/Setup-Qualitaet schwanken stark zwischen Sessions.
    /// Default: All (= keine zeitliche Einschraenkung). Wirkt zusaetzlich zur TradFi-Stundenpruefung —
    /// Crypto kann nun auch zeitlich beschraenkt werden.
    /// </summary>
    public TradingSessions EnabledSessions { get; set; } = TradingSessions.All;
}

/// <summary>UI-Theme-Optionen fuer die BingXBot-Clients (Desktop + Mobile).</summary>
public enum ThemePreference
{
    Dark = 0,
    Light = 1,
    System = 2
}
