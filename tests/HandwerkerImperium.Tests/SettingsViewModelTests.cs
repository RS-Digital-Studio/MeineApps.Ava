using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für SettingsViewModel: Initialisierung, GraphicsQuality-Optionen,
/// Automatisierungs-Toggles, Sprach-Auswahl und NavigationRequested-Event.
/// </summary>
public class SettingsViewModelTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt alle Mock-Dependencies und das ViewModel.
    /// </summary>
    private static (SettingsViewModel Vm, IGameStateService StateSvc, IPurchaseService PurchaseSvc) ErstelleSetup(
        GameState? state = null,
        bool isPremium = false,
        bool isSignedIn = false)
    {
        var audioSvc = Substitute.For<IAudioService>();
        var localizationSvc = Substitute.For<ILocalizationService>();
        var saveGameSvc = Substitute.For<ISaveGameService>();
        var stateSvc = Substitute.For<IGameStateService>();
        var purchaseSvc = Substitute.For<IPurchaseService>();
        var playGamesSvc = Substitute.For<IPlayGamesService>();
        var hintSvc = Substitute.For<IContextualHintService>();
        var dialogSvc = Substitute.For<IDialogService>();

        // Lokalisierung: Gibt den Schlüssel als Fallback zurück
        localizationSvc.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        localizationSvc.CurrentLanguage.Returns("de");

        // State konfigurieren
        var spielState = state ?? GameState.CreateNew();
        stateSvc.State.Returns(spielState);
        stateSvc.IsAutoCollectUnlocked.Returns(spielState.PlayerLevel >= 15);
        stateSvc.IsAutoAcceptUnlocked.Returns(spielState.PlayerLevel >= 25);
        stateSvc.IsAutoAssignUnlocked.Returns(spielState.PlayerLevel >= 50);

        // Premium konfigurieren
        purchaseSvc.IsPremium.Returns(isPremium);

        // Play Games konfigurieren
        playGamesSvc.IsSignedIn.Returns(isSignedIn);
        playGamesSvc.SupportsCloudSave.Returns(false);

        // Audio: alle Sounds feuern und vergessen (void-kompatibel)
        audioSvc.PlaySoundAsync(Arg.Any<GameSound>()).Returns(Task.CompletedTask);

        // SaveGame: async void kompatibel
        saveGameSvc.SaveAsync().Returns(Task.CompletedTask);
        saveGameSvc.ExportSaveAsync().Returns(Task.FromResult((string?)null));

        var vm = new SettingsViewModel(
            audioSvc,
            localizationSvc,
            saveGameSvc,
            stateSvc,
            purchaseSvc,
            playGamesSvc,
            hintSvc,
            dialogSvc);

        return (vm, stateSvc, purchaseSvc);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GraphicsQuality - Optionen-Liste
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GraphicsQualities_NachKonstruktor_HatDreiOptionen()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung
        vm.GraphicsQualities.Should().HaveCount(3);
    }

    [Fact]
    public void GraphicsQualities_EnthältLow()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung
        vm.GraphicsQualities.Should().Contain(q => q.Quality == GraphicsQuality.Low);
    }

    [Fact]
    public void GraphicsQualities_EnthältMedium()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung
        vm.GraphicsQualities.Should().Contain(q => q.Quality == GraphicsQuality.Medium);
    }

    [Fact]
    public void GraphicsQualities_EnthältHigh()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung
        vm.GraphicsQualities.Should().Contain(q => q.Quality == GraphicsQuality.High);
    }

    [Fact]
    public void GraphicsQualities_AlleHabenAnzeigenamen()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung: Kein leerer Anzeigename
        vm.GraphicsQualities.Should().AllSatisfy(q =>
            q.DisplayName.Should().NotBeNullOrEmpty());
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprachen-Liste
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Languages_HatSechs()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung: DE, EN, ES, FR, IT, PT
        vm.Languages.Should().HaveCount(6);
    }

    [Fact]
    public void Languages_EnthältDeutsch()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung
        vm.Languages.Should().Contain(l => l.Code == "de");
    }

    [Fact]
    public void Languages_AlleCodesNichtLeer()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup();

        // Prüfung
        vm.Languages.Should().AllSatisfy(l => l.Code.Should().NotBeNullOrEmpty());
    }

    // ═══════════════════════════════════════════════════════════════════
    // ReloadSettings - Initialisierung aus GameState
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ReloadSettings_SoundAktivInState_SetztSoundEnabled()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.SoundEnabled = true;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.SoundEnabled.Should().BeTrue();
    }

    [Fact]
    public void ReloadSettings_SoundDeaktivInState_SetztSoundEnabledFalse()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.SoundEnabled = false;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.SoundEnabled.Should().BeFalse();
    }

    [Fact]
    public void ReloadSettings_HighGraphicsInState_SelectedQualityIstHigh()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.GraphicsQuality = GraphicsQuality.High;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.SelectedGraphicsQuality.Should().NotBeNull();
        vm.SelectedGraphicsQuality!.Quality.Should().Be(GraphicsQuality.High);
    }

    [Fact]
    public void ReloadSettings_LowGraphicsInState_SelectedQualityIstLow()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.GraphicsQuality = GraphicsQuality.Low;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.SelectedGraphicsQuality!.Quality.Should().Be(GraphicsQuality.Low);
    }

    [Fact]
    public void ReloadSettings_AutoCollectInState_SetztAutoCollect()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Automation.AutoCollectDelivery = true;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.AutoCollectDelivery.Should().BeTrue();
    }

    [Fact]
    public void ReloadSettings_AutoAcceptInState_SetztAutoAccept()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Automation.AutoAcceptOrder = true;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.AutoAcceptOrder.Should().BeTrue();
    }

    [Fact]
    public void ReloadSettings_SpracheDeutschInState_SelectedLanguageIstDeutsch()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Language = "de";
        var (vm, _, _) = ErstelleSetup(state: state);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.SelectedLanguage.Should().NotBeNull();
        vm.SelectedLanguage!.Code.Should().Be("de");
    }

    [Fact]
    public void ReloadSettings_PremiumInState_SetztIsPremium()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.IsPremium = true;
        var (vm, _, _) = ErstelleSetup(state: state, isPremium: true);

        // Ausführung
        vm.ReloadSettings();

        // Prüfung
        vm.IsPremium.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Level-Gates für Automatisierung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsAutoCollectUnlocked_Level1_IstFalse()
    {
        // Vorbereitung: Level 1 (unter Schwelle 15)
        var state = GameState.CreateNew();
        state.PlayerLevel = 1;
        var (vm, _, _) = ErstelleSetup(state: state);

        // Prüfung
        vm.IsAutoCollectUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsAutoClaimUnlocked_NurFuerPremium()
    {
        // Vorbereitung: Nicht-Premium
        var (vmFree, _, _) = ErstelleSetup(isPremium: false);
        var (vmPremium, _, _) = ErstelleSetup(isPremium: true);

        // Prüfung: Auto-Claim nur für Premium-Nutzer
        vmFree.IsAutoClaimUnlocked.Should().BeFalse();
        vmPremium.IsAutoClaimUnlocked.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ShowAds
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ShowAds_NichtPremium_IstTrue()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup(isPremium: false);

        // Prüfung
        vm.ShowAds.Should().BeTrue();
    }

    [Fact]
    public void ShowAds_Premium_IstFalse()
    {
        // Ausführung
        var (vm, _, _) = ErstelleSetup(isPremium: true);

        // Prüfung
        vm.ShowAds.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Navigation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GoBackCommand_FeuertNavigationRequestedMitDoubleDot()
    {
        // Vorbereitung
        var (vm, _, _) = ErstelleSetup();
        string? navigiertZu = null;
        vm.NavigationRequested += route => navigiertZu = route;

        // Ausführung
        vm.GoBackCommand.Execute(null);

        // Prüfung
        navigiertZu.Should().Be("..");
    }

    [Fact]
    public void NavigateToStatisticsCommand_FeuertKorrekteRoute()
    {
        // Vorbereitung
        var (vm, _, _) = ErstelleSetup();
        string? navigiertZu = null;
        vm.NavigationRequested += route => navigiertZu = route;

        // Ausführung
        vm.NavigateToStatisticsCommand.Execute(null);

        // Prüfung
        navigiertZu.Should().Be("../statistics");
    }
}
