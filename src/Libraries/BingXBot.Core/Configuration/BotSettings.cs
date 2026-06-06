using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class BotSettings
{
    public RiskSettings Risk { get; set; } = new();
    public ScannerSettings Scanner { get; set; } = new();
    public BacktestSettings Backtest { get; set; } = new();
    public TradingMode LastMode { get; set; } = TradingMode.Paper;
    /// <summary>Welche Engine zuletzt lief (Scalper = per-Symbol-Scanner, CrossSectional = Momentum-Korb).</summary>
    public EngineMode LastEngineMode { get; set; } = EngineMode.Scalper;
    /// <summary>Cross-Sectional-Momentum-Parameter (nur relevant bei LastEngineMode=CrossSectional).</summary>
    public CrossSectionalSettings CrossSectional { get; set; } = new();
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

    /// <summary>
    /// Watchdog: Auto-Restart der Engine wenn der <c>StaleEngineDetector</c> meldet, dass der
    /// Scan-Loop seit &gt;= 6 h keine Aktivitaet mehr zeigt. Schuetzt vor "silent death" der
    /// RunLoopAsync (real beobachtet 09.-15.05.2026: Bot meldete Running, Heartbeat lief, aber
    /// ScanAndTradeAsync wurde nicht mehr aufgerufen). Default true. Opt-out fuer Debugging.
    /// </summary>
    public bool EnableAutoRestartOnStale { get; set; } = true;

    /// <summary>
    /// Watchdog: Nach wie vielen aufeinanderfolgenden Stale-Alerts ohne Recovery (12 h zwischen
    /// Alerts) wird der Auto-Restart ausgeloest. Default 2 = nach 2× Stale-Alert = ~24 h
    /// Bot-Stillstand. Wirkt nur wenn <see cref="EnableAutoRestartOnStale"/>.
    /// </summary>
    public int AutoRestartAfterStaleAlertCount { get; set; } = 2;

    /// <summary>
    /// Snapshot-Report-Fix Befund 1 / A0.3: Server-seitige Log-Persistenz in die DB.
    /// Wenn true (Default), schreibt der <c>DbLogPersistenceService</c> alle Log-Eintraege
    /// (LogLevel &gt;= <see cref="DbLogPersistenceMinLevel"/>) in die <c>LogEntries</c>-Tabelle.
    /// Ohne diese Persistenz kann der Server-/api/v1/logs-Endpoint nur den In-Memory-Ringpuffer
    /// (<c>LogBufferService</c>) ausliefern — alles vor dem letzten Restart ist verloren.
    /// </summary>
    public bool EnableDbLogPersistence { get; set; } = true;

    /// <summary>
    /// Minimum-Level fuer DB-Log-Persistenz. Default Info (Debug/Trace landen nicht in der DB,
    /// sonst flutet ein einzelner Scan-Loop die Tabelle binnen Stunden).
    /// </summary>
    public LogLevel DbLogPersistenceMinLevel { get; set; } = LogLevel.Info;
}

/// <summary>UI-Theme-Optionen fuer die BingXBot-Clients (Desktop + Mobile).</summary>
public enum ThemePreference
{
    Dark = 0,
    Light = 1,
    System = 2
}
