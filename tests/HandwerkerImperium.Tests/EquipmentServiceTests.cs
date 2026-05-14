using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für EquipmentService: Ausrüsten, Ablegen, Kaufen, Drop-Chance, Shop-Rotation.
/// </summary>
public class EquipmentServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (IGameStateService gameState, GameState state) ErstelleMock()
    {
        var state = new GameState();
        // v2.1.1 (Audit C-C03): Echte GameStateService-Instanz statt Mock — EquipmentService
        // mutiert jetzt unter ExecuteWithLock, das ein NSubstitute-Mock nicht ausfuehren wuerde.
        var gameState = GameStateTestFactory.Create(state);
        return (gameState, state);
    }

    private static Worker ErstelleArbeiter(string id = "worker-1")
    {
        return new Worker { Id = id };
    }

    private static Workshop ErstelleWerkstatt(Worker? worker = null)
    {
        var ws = new Workshop { Type = WorkshopType.Carpenter, IsUnlocked = true };
        if (worker != null)
            ws.Workers.Add(worker);
        return ws;
    }

    // ═══════════════════════════════════════════════════════════════════
    // EquipItem
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void EquipItem_ItemImInventarArbeiterVorhanden_AusruestungWirdZugewiesen()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var arbeiter = ErstelleArbeiter("w1");
        state.Workshops.Add(ErstelleWerkstatt(arbeiter));

        var item = new Equipment { Id = "eq-1" };
        state.EquipmentInventory.Add(item);
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.EquipItem("w1", item);

        // Prüfung
        arbeiter.EquippedItem.Should().NotBeNull();
        arbeiter.EquippedItem!.Id.Should().Be("eq-1");
    }

    [Fact]
    public void EquipItem_ItemImInventarArbeiterVorhanden_EntferntItemAusInventar()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var arbeiter = ErstelleArbeiter("w1");
        state.Workshops.Add(ErstelleWerkstatt(arbeiter));

        var item = new Equipment { Id = "eq-1" };
        state.EquipmentInventory.Add(item);
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.EquipItem("w1", item);

        // Prüfung: Item muss aus Inventar entfernt worden sein
        state.EquipmentInventory.Should().BeEmpty();
    }

    [Fact]
    public void EquipItem_ArbeiterHatBereitsAusruestung_AltesItemKommentInsInventar()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var alteAusruestung = new Equipment { Id = "eq-alt" };
        var arbeiter = ErstelleArbeiter("w1");
        arbeiter.EquippedItem = alteAusruestung;
        state.Workshops.Add(ErstelleWerkstatt(arbeiter));

        var neuesItem = new Equipment { Id = "eq-neu" };
        state.EquipmentInventory.Add(neuesItem);
        var sut = new EquipmentService(gameState);

        // Ausführung: Altes Item wird zurück ins Inventar gelegt
        sut.EquipItem("w1", neuesItem);

        // Prüfung
        state.EquipmentInventory.Should().Contain(i => i.Id == "eq-alt");
    }

    [Fact]
    public void EquipItem_ArbeiterNichtVorhanden_AenderungKeineWirkung()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        // Kein Arbeiter in Workshops
        var item = new Equipment { Id = "eq-1" };
        state.EquipmentInventory.Add(item);
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.EquipItem("nicht-vorhanden", item);

        // Prüfung: Item bleibt im Inventar
        state.EquipmentInventory.Should().Contain(i => i.Id == "eq-1");
    }

    [Fact]
    public void EquipItem_ItemNichtImInventar_KeineAusruestungZugewiesen()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var arbeiter = ErstelleArbeiter("w1");
        state.Workshops.Add(ErstelleWerkstatt(arbeiter));
        // Item NICHT im Inventar
        var item = new Equipment { Id = "eq-unbekannt" };
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.EquipItem("w1", item);

        // Prüfung
        arbeiter.EquippedItem.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // UnequipItem
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void UnequipItem_ArbeiterHatAusruestung_ItemKommentInsInventar()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var item = new Equipment { Id = "eq-1" };
        var arbeiter = ErstelleArbeiter("w1");
        arbeiter.EquippedItem = item;
        state.Workshops.Add(ErstelleWerkstatt(arbeiter));
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.UnequipItem("w1");

        // Prüfung
        arbeiter.EquippedItem.Should().BeNull();
        state.EquipmentInventory.Should().Contain(i => i.Id == "eq-1");
    }

    [Fact]
    public void UnequipItem_ArbeiterHatKeineAusruestung_KeineFehler()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var arbeiter = ErstelleArbeiter("w1");
        // EquippedItem = null
        state.Workshops.Add(ErstelleWerkstatt(arbeiter));
        var sut = new EquipmentService(gameState);

        // Ausführung & Prüfung: Kein Crash
        var act = () => sut.UnequipItem("w1");
        act.Should().NotThrow();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuyEquipment
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BuyEquipment_GenugGoldschrauben_FuegtItemInsInventarHinzu()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        state.GoldenScrews = 50; // genug fuer Common-Equipment (ShopPrice 3)
        var item = new Equipment { Id = "shop-eq" };
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.BuyEquipment(item);

        // Prüfung
        state.EquipmentInventory.Should().Contain(i => i.Id == "shop-eq");
    }

    [Fact]
    public void BuyEquipment_NichtGenugGoldschrauben_ItemNichtImInventar()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        state.GoldenScrews = 0; // zu wenig fuer Common-Equipment (ShopPrice 3)
        var item = new Equipment { Id = "shop-eq" };
        var sut = new EquipmentService(gameState);

        // Ausführung
        sut.BuyEquipment(item);

        // Prüfung
        state.EquipmentInventory.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetShopItems
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetShopItems_IstAufrufbar_GibtItemsZurueck()
    {
        // Vorbereitung
        var (gameState, _) = ErstelleMock();
        var sut = new EquipmentService(gameState);

        // Ausführung
        var items = sut.GetShopItems();

        // Prüfung: Shop hat 3-4 Items
        items.Should().HaveCountGreaterThanOrEqualTo(3).And.HaveCountLessThanOrEqualTo(4);
    }

    // ═══════════════════════════════════════════════════════════════════
    // EquipmentDropped Event
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TryGenerateDrop_DropErfolgt_FeuertEquipmentDroppedEvent()
    {
        // Vorbereitung
        var (gameState, state) = ErstelleMock();
        var sut = new EquipmentService(gameState);
        bool eventFired = false;
        sut.EquipmentDropped += () => eventFired = true;

        // Ausführung: Mit difficulty=4 und perfect=true ist Drop-Chance 45%, viele Versuche
        bool dropped = false;
        for (int i = 0; i < 200 && !dropped; i++)
        {
            var item = sut.TryGenerateDrop(difficulty: 4, isPerfect: true);
            if (item != null) dropped = true;
        }

        // Prüfung: Nach 200 Versuchen bei 45% Chance muss mindestens 1 Drop passiert sein
        dropped.Should().BeTrue("nach 200 Versuchen mit 45% Chance muss ein Drop passieren");
        eventFired.Should().BeTrue();
    }
}
