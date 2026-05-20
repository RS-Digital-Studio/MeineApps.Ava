using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Spielstart-Sequenz, aus MainViewModel.Init.cs extrahiert. MainViewModel.InitializeAsync()
/// ist nur noch ein dünner Forwarder auf <see cref="RunAsync"/>; die EconomyVM-Refreshes und
/// der Loading-State laufen über die schmale <see cref="IStartupHost"/>-Bruecke.
/// Singleton im DI.
/// </summary>
public sealed class GameStartupCoordinator : IGameStartupCoordinator
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly ILocalizationService _localizationService;
    private readonly IOrderGeneratorService _orderGeneratorService;
    private readonly IQuickJobService _quickJobService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IWeeklyMissionService _weeklyMissionService;
    private readonly ILuckySpinService _luckySpinService;
    private readonly IGameLoopService _gameLoopService;
    private readonly DialogViewModel _dialogVm;
    private readonly WelcomeFlowViewModel _welcomeFlowVm;
    private readonly MissionsFeatureViewModel _missionsVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly IWhatsNewService? _whatsNewService;
    private readonly IAnalyticsService? _analyticsService;
    private readonly ICloudSaveService? _cloudSaveService;
    private readonly FtueProgressTracker? _ftueProgressTracker;
    private readonly LiveEventScoreTracker? _liveEventScoreTracker;
    private IStartupHost? _host;

    public GameStartupCoordinator(
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        ILocalizationService localizationService,
        IOrderGeneratorService orderGeneratorService,
        IQuickJobService quickJobService,
        IDailyChallengeService dailyChallengeService,
        IWeeklyMissionService weeklyMissionService,
        ILuckySpinService luckySpinService,
        IGameLoopService gameLoopService,
        DialogViewModel dialogVm,
        WelcomeFlowViewModel welcomeFlowVm,
        MissionsFeatureViewModel missionsVm,
        SettingsViewModel settingsVm,
        IWhatsNewService? whatsNewService = null,
        IAnalyticsService? analyticsService = null,
        ICloudSaveService? cloudSaveService = null,
        FtueProgressTracker? ftueProgressTracker = null,
        LiveEventScoreTracker? liveEventScoreTracker = null)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _localizationService = localizationService;
        _orderGeneratorService = orderGeneratorService;
        _quickJobService = quickJobService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyMissionService = weeklyMissionService;
        _luckySpinService = luckySpinService;
        _gameLoopService = gameLoopService;
        _dialogVm = dialogVm;
        _welcomeFlowVm = welcomeFlowVm;
        _missionsVm = missionsVm;
        _settingsVm = settingsVm;
        _whatsNewService = whatsNewService;
        _analyticsService = analyticsService;
        _cloudSaveService = cloudSaveService;
        _ftueProgressTracker = ftueProgressTracker;
        _liveEventScoreTracker = liveEventScoreTracker;
        // Tracker werden hier referenziert, damit DI sie eager instanziiert (Event-Abos im Ctor).
        _ = _liveEventScoreTracker;
    }

    public void AttachHost(IStartupHost host) => _host = host;

    public async Task RunAsync()
    {
        try
        {
            // ShaderPreloader.PreloadAll() wird bereits in HandwerkerImperiumLoadingPipeline (Schritt 1) aufgerufen

            // Spielstand laden
            if (!_gameStateService.IsInitialized)
            {
                await _saveGameService.LoadAsync();

                // If LoadAsync didn't initialize (no save file), create new state
                if (!_gameStateService.IsInitialized)
                {
                    _gameStateService.Initialize();
                }
            }

            // Cloud-Save prüfen (wenn Play Games angemeldet)
            await CheckCloudSaveAsync();

            // FpsProfile an die vom Spieler gewaehlte Grafikqualitaet binden. Aktualisiert laufende
            // Render-Timer (WorkerAvatar via Event, andere Views lesen neu bei Tab-Wechsel).
            Graphics.FpsProfile.SetCurrent(_gameStateService.Settings.GraphicsQuality);

            // Sprache synchronisieren: gespeicherte Sprache laden oder Gerätesprache übernehmen
            var savedLang = _gameStateService.Settings.Language;
            if (!string.IsNullOrEmpty(savedLang))
            {
                _localizationService.SetLanguage(savedLang);
            }
            else
            {
                // Neues Spiel: Gerätesprache in GameState übernehmen
                _gameStateService.Settings.Language = _localizationService.CurrentLanguage;
            }

            // Reload settings in SettingsVM now that game state is loaded
            _settingsVm.ReloadSettings();

            // Recover stuck active order from previous session
            // (mini-game state is not saved, so it cannot be resumed)
            if (_gameStateService.State.ActiveOrder != null)
            {
                _gameStateService.CancelActiveOrder();
            }

            _host?.RefreshFromState();

            // Generate orders if none or too few exist
            if (_gameStateService.State.AvailableOrders.Count < 3)
            {
                _orderGeneratorService.RefreshOrders();
                _host?.RefreshOrders();
            }

            // Quick Jobs initialisieren
            if (_gameStateService.State.QuickJobs.Count == 0)
                _quickJobService.GenerateJobs();
            _missionsVm.RefreshQuickJobs();

            // Daily Challenges initialisieren
            _dailyChallengeService.CheckAndResetIfNewDay();
            _missionsVm.MarkChallengesDirty();
            _missionsVm.RefreshChallenges();

            // Weekly Missions initialisieren
            _weeklyMissionService.CheckAndResetIfNewWeek();
            _missionsVm.RefreshWeeklyMissions();

            // Lucky Spin Status
            _missionsVm.HasFreeSpin = _luckySpinService.HasFreeSpin;

            if (_host != null) _host.IsLoading = false;

            // Welcome-Flow: Offline-Earnings, Daily-Reward, Starter-Offer + verzoegerte
            // Dialog-Kaskade. Vollstaendig in WelcomeFlowViewModel gekapselt.
            _welcomeFlowVm.RunStartupDialogSequence();

            // FTUE-Sequenz initialisieren (F-03): Tracker verdrahtet die Game-Events,
            // StartIfNeeded() startet die 10-Step-Sequenz wenn der Spieler sie nie
            // begonnen oder uebersprungen hat. Idempotent.
            _ftueProgressTracker?.StartIfNeeded();

            // Start the game loop for idle earnings
            _gameLoopService.Start();

            // WhatsNew-Dialog fuer Bestandsspieler nach App-Update.
            // Wird verzoegert ausgespielt, damit Offline-Earnings/Daily-Reward/Story zuerst durchgehen.
            // Fire-and-forget — Spielstart darf darauf nicht warten.
            if (_whatsNewService != null)
                ShowWhatsNewDeferredAsync().SafeFireAndForget();

            // Telemetrie: Analytics + Session-Start (nur wenn Consent gegeben oder noch nie gefragt).
            // ShowAnalyticsConsentIfNeededAsync laeuft nicht-blockierend — der Spieler kann schon spielen.
            if (_analyticsService != null)
            {
                if (_gameStateService.Settings.AnalyticsConsentShown && _gameStateService.Settings.AnalyticsEnabled)
                {
                    await _analyticsService.InitializeAsync();
                }
                else if (!_gameStateService.Settings.AnalyticsConsentShown)
                {
                    ShowAnalyticsConsentIfNeededAsync().SafeFireAndForget();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] GameStartupCoordinator.RunAsync fehlgeschlagen: {ex}");
            if (_host != null) _host.IsLoading = false;

            // Fehlerdialog anzeigen (DialogVM ist per DI injiziert, immer verfügbar)
            try
            {
                _dialogVm.ShowAlertDialog(
                    _localizationService?.GetString("Error") ?? "Error",
                    _localizationService?.GetString("InitError") ?? "An error occurred while loading. Please restart the app.",
                    "OK");
            }
            catch
            {
                // Wenn selbst der Dialog fehlschlägt, still ignorieren
            }
        }
    }

    /// <summary>
    /// Vergleicht Cloud-Spielstand mit lokalem und fragt Benutzer bei neuerem Cloud-Save.
    /// Nutzt <see cref="ICloudSaveService"/> (Firebase-REST), ersetzt den nicht-funktionalen Play-Games-Stub.
    /// </summary>
    private async Task CheckCloudSaveAsync()
    {
        if (_cloudSaveService?.IsAvailable != true || !_gameStateService.Settings.CloudSaveEnabled)
            return;

        try
        {
            var metadata = await _cloudSaveService.GetMetadataAsync();
            if (metadata == null) return;

            // App-Outdated-Schutz. Wenn der Cloud-Save mit einer
            // neueren App-Version geschrieben wurde (z.B. Spieler hat 2 Geraete, aktuelles
            // Geraet ist alte App-Version), KEIN Download — sonst wuerde Migration auf
            // bereits-aktuelle Daten den State korrumpieren. Nutzer sieht stattdessen
            // den Hinweis dass er die App aktualisieren muss.
            if (metadata.StateVersion > GameState.CurrentStateVersion)
            {
                var outdatedTitle = _localizationService.GetString("CloudSaveTooNewTitle")
                    ?? "App update required";
                var outdatedBody = _localizationService.GetString("CloudSaveTooNewBody")
                    ?? "Your cloud save was created with a newer app version. Please update the app in the Play Store.";
                _dialogVm.ShowAlertDialog(outdatedTitle, outdatedBody, _localizationService.GetString("Confirm") ?? "OK");
                return;
            }

            // Cloud neuer als lokal? Toleranz 5s gegen Clock-Skew.
            // H-H09: Wenn der lokale Save beschaedigt war (LastLoadFailedCorrupt → CreateNew lief),
            // die SavedAt-Heuristik ueberspringen — der Cloud-Stand ist IMMER besser als der
            // frische Leer-State, auch wenn sein Zeitstempel aelter aussieht.
            var localSavedAt = _gameStateService.State.LastSavedAt;
            var cloudSavedAt = metadata.SavedAtUtc;
            bool localWasCorrupt = _saveGameService.LastLoadFailedCorrupt;
            if (!localWasCorrupt && cloudSavedAt <= localSavedAt.AddSeconds(5))
                return;

            // Konflikt-Dialog: zeigt Level + Money beider Stände
            var title = _localizationService.GetString("CloudSaveNewer") ?? "A newer cloud save was found (Level {0}). Use cloud save?";
            var localLbl = string.Format(
                _localizationService.GetString("CloudSaveLocalSummary") ?? "Local: Level {0} ({1})",
                _gameStateService.State.PlayerLevel,
                MoneyFormatter.FormatCompact(_gameStateService.State.Money));
            var cloudLbl = string.Format(
                _localizationService.GetString("CloudSaveCloudSummary") ?? "Cloud: Level {0} ({1})",
                metadata.PlayerLevel,
                MoneyFormatter.FormatCompact(metadata.Money));
            var message = $"{localLbl}\n{cloudLbl}";

            var useCloud = _localizationService.GetString("UseCloudSave") ?? "Use Cloud";
            var useLocal = _localizationService.GetString("UseLocalSave") ?? "Keep Local";

            var confirmed = await _dialogVm.ShowConfirmDialog(title, message, useCloud, useLocal);
            if (!confirmed) return;

            var cloudState = await _cloudSaveService.DownloadAsync();
            if (cloudState == null) return;

            // Cloud-State via ImportSaveAsync einspielen (ruft SanitizeState + SaveInternalAsync)
            var cloudJson = System.Text.Json.JsonSerializer.Serialize(cloudState);
            await _saveGameService.ImportSaveAsync(cloudJson);
            _host?.RefreshFromState();

            _analyticsService?.TrackEvent(AnalyticsEvents.CloudSaveDownloaded, new Dictionary<string, object?>
            {
                ["level"] = cloudState.PlayerLevel,
                ["money"] = (double)cloudState.Money
            });
        }
        catch (Exception ex)
        {
            // Cloud-Sync-Fehler still ignorieren (lokaler Save funktioniert)
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] CheckCloudSaveAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Wartet kurz und zeigt dann den WhatsNew-Dialog wenn er
    /// gebraucht wird. Wartet zusaetzlich falls beim Start andere Dialoge offen sind
    /// (Offline/DailyReward/Story/Welcome/Starter-Offer) — Bestandsspieler haben nach
    /// einem Update meist mehrere Dialog-Kandidaten.
    /// </summary>
    private async Task ShowWhatsNewDeferredAsync()
    {
        if (_whatsNewService == null) return;

        // Erste Verzoegerung: andere Startup-Dialoge zuerst durchlassen.
        await Task.Delay(2500);

        // Maximal 4 Sekunden zusaetzlich warten falls Dialoge offen sind.
        for (int i = 0; i < 8 && _welcomeFlowVm.IsAnyDialogVisible; i++)
            await Task.Delay(500);

        if (_welcomeFlowVm.IsAnyDialogVisible) return; // ergibt sich beim naechsten Start nochmal

        await _whatsNewService.ShowWhatsNewIfNeededAsync();
    }

    /// <summary>
    /// Zeigt den DSGVO-Consent-Dialog fuer Analytics, wenn er noch nie gezeigt wurde.
    /// Wird nicht-blockierend aufgerufen (fire-and-forget) damit der Spielstart nicht wartet.
    /// </summary>
    private async Task ShowAnalyticsConsentIfNeededAsync()
    {
        if (_analyticsService == null) return;
        if (_gameStateService.Settings.AnalyticsConsentShown) return;

        // Kleines Delay, damit der Consent-Dialog nicht mit Offline-Earnings/Welcome-Dialog kollidiert
        await Task.Delay(1500);
        if (_welcomeFlowVm.IsOfflineEarningsDialogVisible ||
            _welcomeFlowVm.IsCombinedWelcomeDialogVisible ||
            _welcomeFlowVm.IsDailyRewardDialogVisible)
        {
            // Warten bis der erste Dialog geschlossen ist
            await Task.Delay(2500);
        }

        var title = _localizationService.GetString("AnalyticsConsentTitle") ?? "Help us improve?";
        var message = _localizationService.GetString("AnalyticsConsentMessage")
                      ?? "Anonymous usage data helps us improve the game. No personal data, no third-party tracking. You can change this in settings at any time.";
        var accept = _localizationService.GetString("AnalyticsConsentAccept") ?? "Yes, help";
        var decline = _localizationService.GetString("AnalyticsConsentDecline") ?? "No, thanks";

        var consent = await _dialogVm.ShowConfirmDialog(title, message, accept, decline);

        _gameStateService.Settings.AnalyticsConsentShown = true;
        _analyticsService.IsEnabled = consent;

        if (consent)
        {
            await _analyticsService.InitializeAsync();
        }

        // Settings speichern damit der Dialog nicht nochmal auftaucht
        await _saveGameService.SaveAsync();
    }
}
