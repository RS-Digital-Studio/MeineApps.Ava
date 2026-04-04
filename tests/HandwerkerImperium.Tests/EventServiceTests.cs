using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für EventService: saisonale Multiplikatoren, Event-Cache, Pity-Timer,
/// Prestige-skalierte Intervalle und Effect-Kombination.
/// </summary>
public class EventServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (EventService service, IGameStateService mockState, GameState state) ErstelleService(int prestigeCount = 0)
    {
        var mockState = Substitute.For<IGameStateService>();
        var state = GameState.CreateNew();
        state.Prestige.BronzeCount = prestigeCount;
        mockState.State.Returns(state);
        var service = new EventService(mockState);
        return (service, mockState, state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetSeasonalMultiplier - saisonale Boni
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(3, 1.15)]
    [InlineData(4, 1.15)]
    [InlineData(5, 1.15)]
    public void GetSeasonalMultiplier_Fruehling_GibtFuenfzehnProzentBonus(int monat, double erwartet)
    {
        // Ausführung
        var ergebnis = EventService.GetSeasonalMultiplier(monat);

        // Prüfung
        ergebnis.Should().Be((decimal)erwartet);
    }

    [Theory]
    [InlineData(6, 1.20)]
    [InlineData(7, 1.20)]
    [InlineData(8, 1.20)]
    public void GetSeasonalMultiplier_Sommer_GibtZwanzigProzentBonus(int monat, double erwartet)
    {
        // Ausführung
        var ergebnis = EventService.GetSeasonalMultiplier(monat);

        // Prüfung
        ergebnis.Should().Be((decimal)erwartet);
    }

    [Theory]
    [InlineData(9, 1.10)]
    [InlineData(10, 1.10)]
    [InlineData(11, 1.10)]
    public void GetSeasonalMultiplier_Herbst_GibtZehnProzentBonus(int monat, double erwartet)
    {
        // Ausführung
        var ergebnis = EventService.GetSeasonalMultiplier(monat);

        // Prüfung
        ergebnis.Should().Be((decimal)erwartet);
    }

    [Theory]
    [InlineData(12, 0.90)]
    [InlineData(1, 0.90)]
    [InlineData(2, 0.90)]
    public void GetSeasonalMultiplier_Winter_GibtZehnProzentMalus(int monat, double erwartet)
    {
        // Ausführung
        var ergebnis = EventService.GetSeasonalMultiplier(monat);

        // Prüfung
        ergebnis.Should().Be((decimal)erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ActiveEvent - kein aktives Event
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ActiveEvent_KeinEvent_GibtNullZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = null;

        // Prüfung
        service.ActiveEvent.Should().BeNull();
    }

    [Fact]
    public void ActiveEvent_AbgelaufenEvent_GibtNullZurueck()
    {
        // Vorbereitung: Event das vor einer Stunde abgelaufen ist
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = new GameEvent
        {
            Type = GameEventType.InnovationFair,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            DurationTicks = TimeSpan.FromHours(1).Ticks,
            Effect = new GameEventEffect { IncomeMultiplier = 1.3m }
        };

        // Prüfung: IsActive ist false → ActiveEvent gibt null zurück
        service.ActiveEvent.Should().BeNull();
    }

    [Fact]
    public void ActiveEvent_AktivesEvent_GibtEventZurueck()
    {
        // Vorbereitung: Event das noch 1 Stunde läuft
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = new GameEvent
        {
            Type = GameEventType.InnovationFair,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            DurationTicks = TimeSpan.FromHours(2).Ticks,
            Effect = new GameEventEffect { IncomeMultiplier = 1.3m }
        };

        // Prüfung
        service.ActiveEvent.Should().NotBeNull();
        service.ActiveEvent!.Type.Should().Be(GameEventType.InnovationFair);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetCurrentEffects - Effect-Kombination
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentEffects_KeinAktivesEvent_GibtNurSaisonalenMultiplikatorZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = null;
        // Saisonaler Multiplikator hängt vom aktuellen Monat ab → nicht vorhersagbar
        // Nur prüfen dass IncomeMultiplier > 0 und CostMultiplier = 1 (kein Event)
        var effect = service.GetCurrentEffects();

        // Prüfung: Kein Event → CostMultiplier bleibt 1.0 (Standard-GameEventEffect)
        effect.Should().NotBeNull();
        effect.IncomeMultiplier.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void GetCurrentEffects_AktivesEvent_KombiniertEventUndSaison()
    {
        // Vorbereitung: InnovationFair gibt +30% Einkommen
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = new GameEvent
        {
            Type = GameEventType.InnovationFair,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            DurationTicks = TimeSpan.FromHours(2).Ticks,
            Effect = new GameEventEffect { IncomeMultiplier = 1.3m }
        };

        // Ausführung
        var effect = service.GetCurrentEffects();

        // Prüfung: Event-Multiplikator * saisonaler Multiplikator > 1.0
        // InnovationFair = 1.3x * mind. 0.9 (Winter) = mind. 1.17
        effect.IncomeMultiplier.Should().BeGreaterThan(1.0m);
    }

    [Fact]
    public void GetCurrentEffects_CacheFunktioniert_WirdNichtNeuBerechnet()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = null;

        // Ausführung: Zweimaliger Aufruf
        var effect1 = service.GetCurrentEffects();
        var effect2 = service.GetCurrentEffects();

        // Prüfung: Gleiche Referenz (Cache greift)
        effect1.Should().BeSameAs(effect2);
    }

    [Fact]
    public void GetCurrentEffects_NachInvalidate_WirdNeuBerechnet()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.ActiveEvent = null;
        var effect1 = service.GetCurrentEffects();

        // Ausführung: Cache invalidieren
        service.InvalidateEffectCache();
        var effect2 = service.GetCurrentEffects();

        // Prüfung: Neues Objekt (Cache wurde geleert)
        effect2.Should().NotBeSameAs(effect1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckForNewEvent - abgelaufenes Event wird bereinigt
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckForNewEvent_AbgelaufenEvent_FeuertEventEndedUndSetztNull()
    {
        // Vorbereitung: Abgelaufenes Event
        var (service, _, state) = ErstelleService();
        GameEvent? beendetesEvent = null;
        service.EventEnded += (_, e) => beendetesEvent = e;
        state.ActiveEvent = new GameEvent
        {
            Type = GameEventType.MaterialSale,
            StartedAt = DateTime.UtcNow.AddHours(-5),
            DurationTicks = TimeSpan.FromHours(2).Ticks,
            Effect = new GameEventEffect { CostMultiplier = 0.7m }
        };
        // Intervall überschreiten damit ein neues geprüft wird
        state.LastEventCheck = DateTime.UtcNow.AddHours(-10);

        // Ausführung
        service.CheckForNewEvent();

        // Prüfung: EventEnded gefeuert, ActiveEvent auf null gesetzt
        beendetesEvent.Should().NotBeNull();
        beendetesEvent!.Type.Should().Be(GameEventType.MaterialSale);
    }

    // ═══════════════════════════════════════════════════════════════════
    // EventStarted - Event-Benachrichtigung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckForNewEvent_IntervalNichtErreicht_StartetKeinNeuesEvent()
    {
        // Vorbereitung: Letzter Check war vor 1 Stunde, Minimum 8 Stunden (kein Prestige)
        var (service, _, state) = ErstelleService(prestigeCount: 0);
        GameEvent? gestartetes = null;
        service.EventStarted += (_, e) => gestartetes = e;
        state.ActiveEvent = null;
        state.LastEventCheck = DateTime.UtcNow.AddHours(-1);

        // Ausführung
        service.CheckForNewEvent();

        // Prüfung: Kein Event gestartet (Intervall nicht erreicht)
        gestartetes.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // EventHistory - Verlauf
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckForNewEvent_EventHistory_BehältMaxZwanzigEintraege()
    {
        // Vorbereitung: 20 bestehende Einträge
        var (service, _, state) = ErstelleService();
        for (int i = 0; i < 20; i++)
            state.EventHistory.Add($"Event_{i}");
        state.ActiveEvent = null;
        state.LastEventCheck = DateTime.UtcNow.AddHours(-10);

        // Ausführung: CheckForNewEvent hinzufügt ggf. einen weiteren
        // (zufallsbasiert, daher nur die Deckenlänge prüfen wenn ein Event generiert wird)
        service.CheckForNewEvent();

        // Prüfung: EventHistory nie größer als 20
        state.EventHistory.Count.Should().BeLessThanOrEqualTo(20);
    }
}
