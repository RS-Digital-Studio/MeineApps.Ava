using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für GameStateService: Geldoperationen, XP/Level-Up, Workshop-Kauf,
/// Goldschrauben, Initialisierung, Automation-Level-Gates, Events.
/// </summary>
public class GameStateServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt einen frischen GameStateService mit neuen Spieldaten.
    /// </summary>
    private static GameStateService ErstelleService(GameState? state = null)
    {
        var svc = new GameStateService();
        svc.Initialize(state ?? GameState.CreateNew());
        return svc;
    }

    // ═══════════════════════════════════════════════════════════════════
    // INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Initialize_OhneState_Setzt_IsInitialized()
    {
        // Vorbereitung
        var svc = new GameStateService();

        // Ausführung
        svc.Initialize();

        // Prüfung
        svc.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Initialize_MitLadezustand_UebernimmtGeld()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.Money = 99999m;
        var svc = new GameStateService();

        // Ausführung
        svc.Initialize(state);

        // Prüfung
        svc.State.Money.Should().Be(99999m);
    }

    [Fact]
    public void Initialize_FeuertStateLabelEvent()
    {
        // Vorbereitung
        var svc = new GameStateService();
        bool feuerte = false;
        svc.StateLoaded += (_, _) => feuerte = true;

        // Ausführung
        svc.Initialize();

        // Prüfung
        feuerte.Should().BeTrue();
    }

    [Fact]
    public void Reset_ErsetztStateUndFeuertStateLoaded()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 999999m;
        bool feuerte = false;
        svc.StateLoaded += (_, _) => feuerte = true;

        // Ausführung
        svc.Reset();

        // Prüfung: Geld zurück auf Startwert, Event gefeuert
        svc.State.Money.Should().Be(1000m);
        feuerte.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GELD - AddMoney
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddMoney_PositiverBetrag_ErhoehteKontostand()
    {
        // Vorbereitung
        var svc = ErstelleService();
        var vorher = svc.State.Money;

        // Ausführung
        svc.AddMoney(500m);

        // Prüfung
        svc.State.Money.Should().Be(vorher + 500m);
    }

    [Fact]
    public void AddMoney_ErhoeheTotalMoneyEarned()
    {
        // Vorbereitung
        var svc = ErstelleService();
        var vorher = svc.State.TotalMoneyEarned;

        // Ausführung
        svc.AddMoney(200m);

        // Prüfung
        svc.State.TotalMoneyEarned.Should().Be(vorher + 200m);
    }

    [Fact]
    public void AddMoney_FeuertMoneyChangedMitKorrektenWerten()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 100m;
        MoneyChangedEventArgs? args = null;
        svc.MoneyChanged += (_, e) => args = e;

        // Ausführung
        svc.AddMoney(50m);

        // Prüfung
        args.Should().NotBeNull();
        args!.OldAmount.Should().Be(100m);
        args.NewAmount.Should().Be(150m);
        args.Delta.Should().Be(50m);
    }

    [Fact]
    public void AddMoney_NullOderNegativ_IgnoriertOhneEvent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 1000m;
        int ereignisse = 0;
        svc.MoneyChanged += (_, _) => ereignisse++;

        // Ausführung: Null und negativer Betrag
        svc.AddMoney(0m);
        svc.AddMoney(-100m);

        // Prüfung: Kein Event, kein Kontostandwechsel
        svc.State.Money.Should().Be(1000m);
        ereignisse.Should().Be(0);
    }

    [Fact]
    public void AddMoney_ErhoehteCurrentRunMoney()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.CurrentRunMoney = 0m;

        // Ausführung
        svc.AddMoney(300m);

        // Prüfung: CurrentRunMoney für Prestige-Berechnung relevant
        svc.State.CurrentRunMoney.Should().Be(300m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GELD - TrySpendMoney
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TrySpendMoney_GenugGeld_GibtTrueUndReduziert()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 1000m;

        // Ausführung
        var ergebnis = svc.TrySpendMoney(400m);

        // Prüfung
        ergebnis.Should().BeTrue();
        svc.State.Money.Should().Be(600m);
    }

    [Fact]
    public void TrySpendMoney_NichtGenugGeld_GibtFalseUndBehaeltGeld()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 100m;

        // Ausführung
        var ergebnis = svc.TrySpendMoney(500m);

        // Prüfung
        ergebnis.Should().BeFalse();
        svc.State.Money.Should().Be(100m);
    }

    [Fact]
    public void TrySpendMoney_GenauGleicherBetrag_Erfolgreich()
    {
        // Vorbereitung: Genau der Kontostand wird ausgegeben
        var svc = ErstelleService();
        svc.State.Money = 250m;

        // Ausführung
        var ergebnis = svc.TrySpendMoney(250m);

        // Prüfung: Grenzfall → genau 0 übrig
        ergebnis.Should().BeTrue();
        svc.State.Money.Should().Be(0m);
    }

    [Fact]
    public void TrySpendMoney_ErhoehtTotalMoneySpent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 500m;

        // Ausführung
        svc.TrySpendMoney(200m);

        // Prüfung
        svc.State.TotalMoneySpent.Should().Be(200m);
    }

    [Fact]
    public void TrySpendMoney_NullOderNegativ_GibtFalse()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 1000m;

        // Prüfung
        svc.TrySpendMoney(0m).Should().BeFalse();
        svc.TrySpendMoney(-10m).Should().BeFalse();
        svc.State.Money.Should().Be(1000m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GELD - CanAfford
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CanAfford_GenugGeld_IstTrue()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 500m;

        // Prüfung
        svc.CanAfford(500m).Should().BeTrue();
    }

    [Fact]
    public void CanAfford_NichtGenugGeld_IstFalse()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 100m;

        // Prüfung
        svc.CanAfford(101m).Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GOLDSCHRAUBEN
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddGoldenScrews_NormalZugabe_ErhoehteKontostand()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.GoldenScrews = 10;

        // Ausführung: fromPurchase=true → kein Premium-Multiplier
        svc.AddGoldenScrews(5, fromPurchase: true);

        // Prüfung
        svc.State.GoldenScrews.Should().Be(15);
    }

    [Fact]
    public void AddGoldenScrews_PremiumSpieler_VerdoppeltGameplayQuellen()
    {
        // Vorbereitung: Premium-Spieler bekommt 2x aus Gameplay-Quellen
        var state = GameState.CreateNew();
        state.IsPremium = true;
        state.GoldenScrews = 0;
        var svc = ErstelleService(state);

        // Ausführung: fromPurchase=false = Gameplay-Quelle
        svc.AddGoldenScrews(10, fromPurchase: false);

        // Prüfung: 10 * 2 = 20
        svc.State.GoldenScrews.Should().Be(20);
    }

    [Fact]
    public void AddGoldenScrews_PremiumSpieler_VerdoppeltNichtBeiKauf()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.IsPremium = true;
        state.GoldenScrews = 0;
        var svc = ErstelleService(state);

        // Ausführung: fromPurchase=true → IAP wird nicht verdoppelt
        svc.AddGoldenScrews(50, fromPurchase: true);

        // Prüfung: Exakt 50, kein Doppel
        svc.State.GoldenScrews.Should().Be(50);
    }

    [Fact]
    public void AddGoldenScrews_NullOderNegativ_IgnorierAufruf()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.GoldenScrews = 10;

        // Ausführung
        svc.AddGoldenScrews(0);
        svc.AddGoldenScrews(-5);

        // Prüfung: Keine Änderung
        svc.State.GoldenScrews.Should().Be(10);
    }

    [Fact]
    public void TrySpendGoldenScrews_GenugVorhanden_ErfolgreichUndReduziert()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.GoldenScrews = 20;

        // Ausführung
        var ergebnis = svc.TrySpendGoldenScrews(10);

        // Prüfung
        ergebnis.Should().BeTrue();
        svc.State.GoldenScrews.Should().Be(10);
    }

    [Fact]
    public void TrySpendGoldenScrews_NichtGenug_GibtFalseOhneAenderung()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.GoldenScrews = 5;

        // Ausführung
        var ergebnis = svc.TrySpendGoldenScrews(10);

        // Prüfung
        ergebnis.Should().BeFalse();
        svc.State.GoldenScrews.Should().Be(5);
    }

    // ═══════════════════════════════════════════════════════════════════
    // XP / LEVEL-UP
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddXp_PositiverBetrag_ErhoehteCurrentXp()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.PlayerLevel = 1;
        svc.State.CurrentXp = 0;

        // Ausführung
        svc.AddXp(50);

        // Prüfung
        svc.State.CurrentXp.Should().Be(50);
    }

    [Fact]
    public void AddXp_FeuertXpGainedEvent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        XpGainedEventArgs? args = null;
        svc.XpGained += (_, e) => args = e;

        // Ausführung
        svc.AddXp(30);

        // Prüfung
        args.Should().NotBeNull();
        args!.Amount.Should().Be(30);
    }

    [Fact]
    public void AddXp_GenugFuerLevelUp_ErhoehtePlayerLevel()
    {
        // Vorbereitung: Level 1 braucht 100 XP für Level 2
        var svc = ErstelleService();
        svc.State.PlayerLevel = 1;
        svc.State.CurrentXp = 0;

        // Ausführung: Mehr als 100 XP auf einmal
        svc.AddXp(150);

        // Prüfung: Level-Up passiert
        svc.State.PlayerLevel.Should().BeGreaterThan(1);
    }

    [Fact]
    public void AddXp_LevelUp_FeuertLevelUpEvent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.PlayerLevel = 1;
        svc.State.CurrentXp = 0;
        LevelUpEventArgs? args = null;
        svc.LevelUp += (_, e) => args = e;

        // Ausführung
        svc.AddXp(200);

        // Prüfung
        args.Should().NotBeNull();
        args!.OldLevel.Should().Be(1);
        args.NewLevel.Should().BeGreaterThan(1);
    }

    [Fact]
    public void AddXp_NullOderNegativ_IgnoriertOhneEvent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.PlayerLevel = 1;
        svc.State.CurrentXp = 0;
        int xpEvents = 0;
        svc.XpGained += (_, _) => xpEvents++;

        // Ausführung
        svc.AddXp(0);
        svc.AddXp(-10);

        // Prüfung
        xpEvents.Should().Be(0);
        svc.State.CurrentXp.Should().Be(0);
    }

    [Fact]
    public void AddXp_MehrmaligenLevelUp_KorrektesEndlevel()
    {
        // Vorbereitung: Mehrere Level auf einmal überspringen
        var svc = ErstelleService();
        svc.State.PlayerLevel = 1;
        svc.State.CurrentXp = 0;

        // Ausführung: Sehr viel XP auf einmal
        svc.AddXp(10000);

        // Prüfung: Level deutlich über 1
        svc.State.PlayerLevel.Should().BeGreaterThan(5);
    }

    [Fact]
    public void AddXp_BeimMaxLevel_KeineWeitereZugabe()
    {
        // Vorbereitung: Auf Max-Level setzen
        var svc = ErstelleService();
        svc.State.PlayerLevel = 1000; // LevelThresholds.MaxPlayerLevel
        var vorherXp = svc.State.CurrentXp;

        // Ausführung: XP-Versuch am Cap
        svc.AddXp(999);

        // Prüfung: Kein Overflow
        svc.State.PlayerLevel.Should().Be(1000);
    }

    [Fact]
    public void AddXp_MitXpBoostAktiv_VerdoppeltBetrag()
    {
        // Vorbereitung: XP-Boost aktiv (endet in der Zukunft)
        var state = GameState.CreateNew();
        state.XpBoostEndTime = DateTime.UtcNow.AddHours(1);
        state.PlayerLevel = 1;
        state.CurrentXp = 0;
        var svc = ErstelleService(state);
        int empfangenerBetrag = 0;
        svc.XpGained += (_, e) => empfangenerBetrag = e.Amount;

        // Ausführung
        svc.AddXp(10);

        // Prüfung: 10 * 2 = 20 durch XP-Boost
        empfangenerBetrag.Should().Be(20);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AUTOMATION LEVEL-GATES
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsAutoCollectUnlocked_UnterLevel15_IstFalse()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 14;
        var svc = ErstelleService(state);

        // Prüfung
        svc.IsAutoCollectUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsAutoCollectUnlocked_AbLevel15_IstTrue()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 15;
        var svc = ErstelleService(state);

        // Prüfung: Grenzwert Level 15
        svc.IsAutoCollectUnlocked.Should().BeTrue();
    }

    [Fact]
    public void IsAutoAcceptUnlocked_UnterLevel25_IstFalse()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 24;
        var svc = ErstelleService(state);

        // Prüfung
        svc.IsAutoAcceptUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsAutoAcceptUnlocked_AbLevel25_IstTrue()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 25;
        var svc = ErstelleService(state);

        // Prüfung
        svc.IsAutoAcceptUnlocked.Should().BeTrue();
    }

    [Fact]
    public void IsAutoAssignUnlocked_UnterLevel20_IstFalse()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 19;
        var svc = ErstelleService(state);

        // Prüfung
        svc.IsAutoAssignUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsAutoAssignUnlocked_AbLevel20_IstTrue()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 20;
        var svc = ErstelleService(state);

        // Prüfung
        svc.IsAutoAssignUnlocked.Should().BeTrue();
    }

    [Fact]
    public void AlleAutomationen_NachErstemPrestige_FreigeschaltetUnabhaengigVomLevel()
    {
        // Vorbereitung: Level 1, aber 1 Prestige abgeschlossen
        var state = GameState.CreateNew();
        state.PlayerLevel = 1;
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1
        var svc = ErstelleService(state);

        // Prüfung: Nach erstem Prestige alles permanent freigeschaltet (laut CLAUDE.md)
        svc.IsAutoCollectUnlocked.Should().BeTrue();
        svc.IsAutoAcceptUnlocked.Should().BeTrue();
        svc.IsAutoAssignUnlocked.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // WORKSHOP-KAUF
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TryPurchaseWorkshop_GenugGeldUndLevel_ErfolgreichUndGeldAbgezogen()
    {
        // Vorbereitung: Klempner braucht Level 5, Kosten ~1.000€
        var state = GameState.CreateNew();
        state.PlayerLevel = 10;
        state.Money = 999999m;
        var svc = ErstelleService(state);

        // Ausführung
        var ergebnis = svc.TryPurchaseWorkshop(WorkshopType.Plumber, costOverride: 100m);

        // Prüfung
        ergebnis.Should().BeTrue();
        state.UnlockedWorkshopTypes.Should().Contain(WorkshopType.Plumber);
        state.Money.Should().Be(999999m - 100m);
    }

    [Fact]
    public void TryPurchaseWorkshop_NichtGenugGeld_GibtFalse()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 10;
        state.Money = 0m;
        var svc = ErstelleService(state);

        // Ausführung
        var ergebnis = svc.TryPurchaseWorkshop(WorkshopType.Plumber, costOverride: 500m);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void TryPurchaseWorkshop_LevelNichtErreicht_GibtFalse()
    {
        // Vorbereitung: Klempner braucht Level 5, Spieler auf Level 1
        var state = GameState.CreateNew();
        state.PlayerLevel = 1;
        state.Money = 999999m;
        var svc = ErstelleService(state);

        // Ausführung
        var ergebnis = svc.TryPurchaseWorkshop(WorkshopType.Plumber, costOverride: 1m);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void TryPurchaseWorkshop_BereitsFreigeschaltet_GibtFalse()
    {
        // Vorbereitung: Schreinerei ist bereits freigeschaltet
        var state = GameState.CreateNew();
        state.Money = 999999m;
        var svc = ErstelleService(state);

        // Ausführung
        var ergebnis = svc.TryPurchaseWorkshop(WorkshopType.Carpenter, costOverride: 0m);

        // Prüfung: Bereits freigeschaltet → nein
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void TryPurchaseWorkshop_Erfolg_GibtXpFuerFreischaltung()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.PlayerLevel = 10;
        state.Money = 999999m;
        var svc = ErstelleService(state);
        bool xpErhalten = false;
        svc.XpGained += (_, _) => xpErhalten = true;

        // Ausführung
        svc.TryPurchaseWorkshop(WorkshopType.Plumber, costOverride: 1m);

        // Prüfung: Workshop-Freischaltung gibt XP
        xpErhalten.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // WORKSHOP-UPGRADE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TryUpgradeWorkshop_GenugGeld_ErfolgreichUndLevelErhoehen()
    {
        // Vorbereitung
        var svc = ErstelleService();
        var ws = svc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.IsUnlocked = true;
        svc.State.Money = 999999m;
        var altesLevel = ws.Level;

        // Ausführung
        var ergebnis = svc.TryUpgradeWorkshop(WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeTrue();
        ws.Level.Should().Be(altesLevel + 1);
    }

    [Fact]
    public void TryUpgradeWorkshop_NichtGenugGeld_GibtFalse()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 0m;

        // Ausführung
        var ergebnis = svc.TryUpgradeWorkshop(WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void TryUpgradeWorkshop_FeuertWorkshopUpgradedEvent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 999999m;
        WorkshopUpgradedEventArgs? args = null;
        svc.WorkshopUpgraded += (_, e) => args = e;

        // Ausführung
        svc.TryUpgradeWorkshop(WorkshopType.Carpenter);

        // Prüfung
        args.Should().NotBeNull();
        args!.WorkshopType.Should().Be(WorkshopType.Carpenter);
        args.NewLevel.Should().Be(args.OldLevel + 1);
    }

    [Fact]
    public void TryUpgradeWorkshop_GibtXpFuerUpgrade()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.Money = 999999m;
        bool xpErhalten = false;
        svc.XpGained += (_, _) => xpErhalten = true;

        // Ausführung
        svc.TryUpgradeWorkshop(WorkshopType.Carpenter);

        // Prüfung
        xpErhalten.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AUFTRÄGE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void StartOrder_SchiebteAusVerfuegbarenInAktiven()
    {
        // Vorbereitung
        var svc = ErstelleService();
        var auftrag = new Order { Id = "test-order-1" };
        svc.State.AvailableOrders.Add(auftrag);

        // Ausführung
        svc.StartOrder(auftrag);

        // Prüfung
        svc.State.AvailableOrders.Should().NotContain(auftrag);
        svc.State.ActiveOrder.Should().Be(auftrag);
    }

    [Fact]
    public void GetActiveOrder_KeinAktiverAuftrag_GibtNull()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.ActiveOrder = null;

        // Prüfung
        svc.GetActiveOrder().Should().BeNull();
    }

    [Fact]
    public void CancelActiveOrder_EntferntAktivenAuftrag()
    {
        // Vorbereitung
        var svc = ErstelleService();
        svc.State.ActiveOrder = new Order { Id = "zu-stornieren" };

        // Ausführung
        svc.CancelActiveOrder();

        // Prüfung
        svc.State.ActiveOrder.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRESTIGE-BONUS-CACHE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void InvalidatePrestigeBonusCache_FeuertPrestigeShopPurchasedEvent()
    {
        // Vorbereitung
        var svc = ErstelleService();
        bool feuerte = false;
        svc.PrestigeShopPurchased += (_, _) => feuerte = true;

        // Ausführung
        svc.InvalidatePrestigeBonusCache();

        // Prüfung
        feuerte.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // LOCK-DELEGATION
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ExecuteWithLock_AktionWirdAusgeführt()
    {
        // Vorbereitung
        var svc = ErstelleService();
        bool ausgeführt = false;

        // Ausführung
        svc.ExecuteWithLock(() => ausgeführt = true);

        // Prüfung
        ausgeführt.Should().BeTrue();
    }

    [Fact]
    public void ExecuteWithLockGeneric_GibtRückgabewertZurueck()
    {
        // Vorbereitung
        var svc = ErstelleService();

        // Ausführung
        var ergebnis = svc.ExecuteWithLock(() => 42);

        // Prüfung
        ergebnis.Should().Be(42);
    }
}
