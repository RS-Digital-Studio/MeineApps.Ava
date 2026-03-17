using FluentAssertions;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für InventoryService: Items hinzufügen/entfernen, Ausrüsten,
/// Verbrauchsgegenstände benutzen, Klassen-Einschränkungen, Equipment-Stats.
/// </summary>
public class InventoryServiceTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    /// <summary>
    /// Erstellt einen InventoryService mit einer vordefinierten Item-Menge.
    /// Da LoadItems() EmbeddedResources benötigt, werden Items direkt via AddItem(Item) eingefügt.
    /// </summary>
    private static InventoryService ErstelleService(params Item[] items)
    {
        var service = new InventoryService();
        foreach (var item in items)
            service.AddItem(item, 0); // Definition registrieren, Menge 0
        return service;
    }

    private static Item ErstelleWaffe(string id = "W001", int atkBonus = 5, string? classRestriction = null)
        => new()
        {
            Id = id,
            NameKey = $"item_{id}",
            Type = ItemType.Weapon,
            AtkBonus = atkBonus,
            DefBonus = 0,
            BuyPrice = 100,
            SellPrice = 50,
            ClassRestriction = classRestriction
        };

    private static Item ErstelleRuestung(string id = "A001", int defBonus = 3)
        => new()
        {
            Id = id,
            NameKey = $"item_{id}",
            Type = ItemType.Armor,
            DefBonus = defBonus,
            BuyPrice = 80,
            SellPrice = 40
        };

    private static Item ErstelleHeiltrank(string id = "C001", int heilHp = 50)
        => new()
        {
            Id = id,
            NameKey = $"item_{id}",
            Type = ItemType.Consumable,
            HealHp = heilHp,
            BuyPrice = 30,
            SellPrice = 15
        };

    private static Item ErstelleKeyItem(string id = "K001")
        => new()
        {
            Id = id,
            NameKey = $"item_{id}",
            Type = ItemType.KeyItem,
            SellPrice = 0
        };

    private static Player ErstelleSpieler(ClassName klasse = ClassName.Swordmaster)
    {
        var p = Player.Create(klasse);
        return p;
    }

    // ─── AddItem / RemoveItem / HasItem ──────────────────────────────────────

    [Fact]
    public void AddItem_NeuesItem_KannHinterherGefundenWerden()
    {
        // Vorbereitung
        var waffe = ErstelleWaffe();
        var service = ErstelleService(waffe);

        // Ausführung
        service.AddItem(waffe.Id);

        // Prüfung
        service.HasItem(waffe.Id).Should().BeTrue("hinzugefügtes Item muss vorhanden sein");
        service.GetItemCount(waffe.Id).Should().Be(1, "genau 1 wurde hinzugefügt");
    }

    [Fact]
    public void AddItem_MehrfachHinzufuegen_Stapelt()
    {
        // Vorbereitung
        var trank = ErstelleHeiltrank();
        var service = ErstelleService(trank);

        // Ausführung: 3x hinzufügen
        service.AddItem(trank.Id, 3);

        // Prüfung
        service.GetItemCount(trank.Id).Should().Be(3, "3 Tranks müssen gestapelt sein");
    }

    [Fact]
    public void AddItem_UnbekannteId_IgnoriertAufruf()
    {
        // Vorbereitung: Service ohne Items
        var service = new InventoryService();

        // Ausführung: Unbekannte ID hinzufügen
        service.AddItem("nicht_existent");

        // Prüfung
        service.HasItem("nicht_existent").Should().BeFalse(
            "unbekannte Item-ID muss ignoriert werden");
    }

    [Fact]
    public void RemoveItem_VorhandenesMenge_GibtTrueUndVerringert()
    {
        // Vorbereitung
        var trank = ErstelleHeiltrank();
        var service = ErstelleService(trank);
        service.AddItem(trank.Id, 3);

        // Ausführung
        bool erfolg = service.RemoveItem(trank.Id, 2);

        // Prüfung
        erfolg.Should().BeTrue("2 von 3 Tranks entfernen muss klappen");
        service.GetItemCount(trank.Id).Should().Be(1, "1 Trank übrig");
    }

    [Fact]
    public void RemoveItem_ZuWenigVorhanden_GibtFalseZurueck()
    {
        // Vorbereitung
        var trank = ErstelleHeiltrank();
        var service = ErstelleService(trank);
        service.AddItem(trank.Id, 1);

        // Ausführung
        bool erfolg = service.RemoveItem(trank.Id, 5);

        // Prüfung
        erfolg.Should().BeFalse("1 Trank reicht nicht um 5 zu entfernen");
        service.GetItemCount(trank.Id).Should().Be(1, "Menge bleibt unverändert");
    }

    [Fact]
    public void RemoveItem_AlleEntfernen_ItemVerschwindetAusInventar()
    {
        // Grenzfall: Genau alle Items entfernen
        var trank = ErstelleHeiltrank();
        var service = ErstelleService(trank);
        service.AddItem(trank.Id, 2);

        service.RemoveItem(trank.Id, 2);

        service.HasItem(trank.Id).Should().BeFalse("Item muss aus Inventar verschwinden wenn Menge 0");
        service.GetItemCount(trank.Id).Should().Be(0);
    }

    [Fact]
    public void RemoveItem_NichtVorhandenes_GibtFalse()
    {
        var service = new InventoryService();

        service.RemoveItem("nicht_existent").Should().BeFalse(
            "nicht vorhandenes Item kann nicht entfernt werden");
    }

    [Fact]
    public void HasItem_MindestmengeNichtErreicht_GibtFalse()
    {
        // Vorbereitung: Nur 1 vorhanden, aber 2 gefordert
        var trank = ErstelleHeiltrank();
        var service = ErstelleService(trank);
        service.AddItem(trank.Id, 1);

        service.HasItem(trank.Id, 2).Should().BeFalse(
            "1 Trank reicht nicht für HasItem(id, 2)");
        service.HasItem(trank.Id, 1).Should().BeTrue("aber für 1 schon");
    }

    // ─── EquipItem / UnequipSlot ──────────────────────────────────────────────

    [Fact]
    public void EquipItem_GueltigesEquipment_RuestetAnUndEntferntAusInventar()
    {
        // Vorbereitung
        var waffe = ErstelleWaffe();
        var service = ErstelleService(waffe);
        service.AddItem(waffe.Id);
        var spieler = ErstelleSpieler();
        int atkVorher = spieler.Atk;

        // Ausführung
        service.EquipItem(waffe.Id, spieler);

        // Prüfung: Im Inventar nicht mehr vorhanden, aber ausgerüstet
        service.HasItem(waffe.Id).Should().BeFalse("ausgerüstetes Item verlässt Inventar");
        service.IsEquipped(waffe.Id).Should().BeTrue("Item muss als ausgerüstet markiert sein");
        spieler.Atk.Should().Be(atkVorher + waffe.AtkBonus,
            "Equipment-Bonus muss auf Spieler-Stats angewendet werden");
    }

    [Fact]
    public void EquipItem_NichtImInventar_GibtNull()
    {
        // Vorbereitung: Item bekannt aber nicht im Inventar
        var waffe = ErstelleWaffe();
        var service = ErstelleService(waffe);
        // Nicht hinzugefügt!
        var spieler = ErstelleSpieler();

        // Ausführung
        var vorher = service.EquipItem(waffe.Id, spieler);

        // Prüfung
        vorher.Should().BeNull("Item kann nicht ausgerüstet werden wenn es nicht im Inventar ist");
        service.IsEquipped(waffe.Id).Should().BeFalse("nicht im Inventar = nicht ausgerüstet");
    }

    [Fact]
    public void EquipItem_AlteWaffeWirdInsInventarZurueckgelegt()
    {
        // Vorbereitung: Erst Waffe 1 ausrüsten, dann Waffe 2
        var waffe1 = ErstelleWaffe("W001", atkBonus: 5);
        var waffe2 = ErstelleWaffe("W002", atkBonus: 10);
        var service = ErstelleService(waffe1, waffe2);
        service.AddItem(waffe1.Id);
        service.AddItem(waffe2.Id);
        var spieler = ErstelleSpieler();

        service.EquipItem(waffe1.Id, spieler); // W001 anlegen
        var zurueckgelegtes = service.EquipItem(waffe2.Id, spieler); // W002 → W001 zurück

        // Prüfung
        zurueckgelegtes.Should().Be(waffe1.Id, "die alte Waffe muss zurückgegeben werden");
        service.HasItem(waffe1.Id).Should().BeTrue("alte Waffe kommt zurück ins Inventar");
        service.IsEquipped(waffe2.Id).Should().BeTrue("neue Waffe ist jetzt ausgerüstet");
    }

    [Fact]
    public void EquipItem_StatTauschBeimWaffenwechsel_KorrektBerechnet()
    {
        // Vorbereitung
        var waffe1 = ErstelleWaffe("W001", atkBonus: 5);
        var waffe2 = ErstelleWaffe("W002", atkBonus: 8);
        var service = ErstelleService(waffe1, waffe2);
        service.AddItem(waffe1.Id);
        service.AddItem(waffe2.Id);
        var spieler = ErstelleSpieler();
        int atkBasis = spieler.Atk;

        // W001 anlegen (+5)
        service.EquipItem(waffe1.Id, spieler);
        // W002 anlegen (+8, W001 wird abgelegt)
        service.EquipItem(waffe2.Id, spieler);

        spieler.Atk.Should().Be(atkBasis + 8,
            "nach Waffenwechsel muss ATK = Basis + neuer Bonus sein (alter Bonus entfernt)");
    }

    [Fact]
    public void EquipItem_BereitsAusgeruestet_GibtNullUndAendertNichts()
    {
        // Grenzfall: Dasselbe Item nochmal ausrüsten
        var waffe = ErstelleWaffe();
        var service = ErstelleService(waffe);
        service.AddItem(waffe.Id);
        var spieler = ErstelleSpieler();
        service.EquipItem(waffe.Id, spieler);
        int atkNachErstem = spieler.Atk;

        // Ausführung: Nochmal versuchen
        var ergebnis = service.EquipItem(waffe.Id, spieler);

        // Prüfung
        ergebnis.Should().BeNull("bereits ausgerüstetes Item gibt null zurück");
        spieler.Atk.Should().Be(atkNachErstem, "ATK darf sich nicht nochmals erhöhen");
    }

    [Fact]
    public void EquipItem_KlassenEinschraenkung_FalscheKlasse_GibtNull()
    {
        // Vorbereitung: Waffe nur für Arkanisten
        var arkaWaffe = ErstelleWaffe("W_ARC", classRestriction: "Arcanist");
        var service = ErstelleService(arkaWaffe);
        service.AddItem(arkaWaffe.Id);
        var schwertmeister = ErstelleSpieler(ClassName.Swordmaster);

        // Ausführung
        var ergebnis = service.EquipItem(arkaWaffe.Id, schwertmeister);

        // Prüfung
        ergebnis.Should().BeNull("Schwertmeister darf keine Arkanisten-Waffe tragen");
        service.IsEquipped(arkaWaffe.Id).Should().BeFalse("Item nicht ausgerüstet");
        service.HasItem(arkaWaffe.Id).Should().BeTrue("Item bleibt im Inventar");
    }

    [Fact]
    public void EquipItem_KlassenEinschraenkung_RichtigeKlasse_Erfolg()
    {
        // Vorbereitung
        var arkaWaffe = ErstelleWaffe("W_ARC", classRestriction: "Arcanist");
        var service = ErstelleService(arkaWaffe);
        service.AddItem(arkaWaffe.Id);
        var arkanist = ErstelleSpieler(ClassName.Arcanist);

        service.EquipItem(arkaWaffe.Id, arkanist);

        service.IsEquipped(arkaWaffe.Id).Should().BeTrue(
            "Arkanist darf seine Klassenwaffe tragen");
    }

    [Fact]
    public void UnequipSlot_EntferntEquipmentUndStelltStatZurueck()
    {
        // Vorbereitung
        var waffe = ErstelleWaffe(atkBonus: 10);
        var service = ErstelleService(waffe);
        service.AddItem(waffe.Id);
        var spieler = ErstelleSpieler();
        int atkVorher = spieler.Atk;
        service.EquipItem(waffe.Id, spieler);

        // Ausführung
        service.UnequipSlot(EquipSlot.Weapon, spieler);

        // Prüfung
        spieler.Atk.Should().Be(atkVorher, "ATK-Bonus muss nach Ablegen entfernt sein");
        service.HasItem(waffe.Id).Should().BeTrue("abgelegtes Item kommt ins Inventar zurück");
        service.IsEquipped(waffe.Id).Should().BeFalse("Item ist nicht mehr ausgerüstet");
    }

    // ─── UseItem ─────────────────────────────────────────────────────────────

    [Fact]
    public void UseItem_Heiltrank_HeiltspielerHP()
    {
        // Vorbereitung: Spieler mit halbem HP
        var trank = ErstelleHeiltrank(heilHp: 50);
        var service = ErstelleService(trank);
        service.AddItem(trank.Id);
        var spieler = ErstelleSpieler();
        spieler.Hp = spieler.MaxHp / 2;

        // Ausführung
        bool erfolg = service.UseItem(trank.Id, spieler);

        // Prüfung
        erfolg.Should().BeTrue("Trank kann benutzt werden");
        spieler.Hp.Should().BeGreaterThan(spieler.MaxHp / 2, "HP muss nach Heilung gestiegen sein");
        service.HasItem(trank.Id).Should().BeFalse("Trank wird nach Benutzung verbraucht");
    }

    [Fact]
    public void UseItem_HpNieUeberMaxHp()
    {
        // Grenzfall: Riesen-Heiltrank auf vollem Spieler
        var trank = ErstelleHeiltrank(heilHp: 99999);
        var service = ErstelleService(trank);
        service.AddItem(trank.Id);
        var spieler = ErstelleSpieler();
        spieler.Hp = spieler.MaxHp; // Schon voll

        service.UseItem(trank.Id, spieler);

        spieler.Hp.Should().Be(spieler.MaxHp, "HP kann MaxHp nicht überschreiten");
    }

    [Fact]
    public void UseItem_ProzentHeilung_HeilungAnteilig()
    {
        // Elixier: 50% HeilPercent
        var elixier = new Item
        {
            Id = "C_ELIX",
            NameKey = "item_elixier",
            Type = ItemType.Consumable,
            HealPercent = 50
        };
        var service = ErstelleService(elixier);
        service.AddItem(elixier.Id);
        var spieler = ErstelleSpieler();
        spieler.Hp = 0;
        spieler.Hp = 1; // Min 1 (nach Clamp in ApplyItemStats)
        spieler.Hp = spieler.MaxHp / 4; // Auf 25% setzen

        service.UseItem(elixier.Id, spieler);

        // 25% + 50% = 75% von MaxHp
        int erwartet = Math.Min(spieler.MaxHp, spieler.MaxHp / 4 + spieler.MaxHp * 50 / 100);
        spieler.Hp.Should().Be(erwartet, "50% HealPercent heilt 50% von MaxHp");
    }

    [Fact]
    public void UseItem_NichtVorhanden_GibtFalse()
    {
        var service = new InventoryService();
        var spieler = ErstelleSpieler();

        service.UseItem("nicht_existent", spieler).Should().BeFalse(
            "nicht vorhandenes Item kann nicht benutzt werden");
    }

    [Fact]
    public void UseItem_Equipment_GibtFalse()
    {
        // Waffen sind nicht benutzbar (IsUsable = false)
        var waffe = ErstelleWaffe();
        var service = ErstelleService(waffe);
        service.AddItem(waffe.Id);
        var spieler = ErstelleSpieler();

        bool erfolg = service.UseItem(waffe.Id, spieler);

        erfolg.Should().BeFalse("Waffe hat IsUsable=false und kann nicht benutzt werden");
        service.HasItem(waffe.Id).Should().BeTrue("Waffe bleibt im Inventar");
    }

    // ─── GetTotalSellValue ────────────────────────────────────────────────────

    [Fact]
    public void GetTotalSellValue_MehrereItems_SummiertKorrekt()
    {
        // Vorbereitung: 2 Waffen à 50G Verkaufswert, 3 Tranks à 15G
        var waffe = ErstelleWaffe(); // SellPrice = 50
        var trank = ErstelleHeiltrank(); // SellPrice = 15
        var service = ErstelleService(waffe, trank);
        service.AddItem(waffe.Id, 2);
        service.AddItem(trank.Id, 3);

        // Ausführung
        int gesamt = service.GetTotalSellValue();

        // Prüfung: 2*50 + 3*15 = 100 + 45 = 145
        gesamt.Should().Be(145, "Gesamtwert = 2 Waffen (100G) + 3 Tranks (45G) = 145G");
    }

    [Fact]
    public void GetTotalSellValue_KeyItemsNichtMitgezaehlt()
    {
        // Key-Items sind nicht verkäuflich (SellPrice=0 und explizit ausgeschlossen)
        var keyItem = ErstelleKeyItem();
        var service = ErstelleService(keyItem);
        service.AddItem(keyItem.Id, 5);

        int gesamt = service.GetTotalSellValue();

        gesamt.Should().Be(0, "Key-Items dürfen nicht in den Verkaufswert einfließen");
    }

    [Fact]
    public void GetTotalSellValue_LeereInventar_GibtNull()
    {
        var service = new InventoryService();

        service.GetTotalSellValue().Should().Be(0, "leeres Inventar hat Wert 0");
    }

    // ─── RestoreState ─────────────────────────────────────────────────────────

    [Fact]
    public void RestoreState_StelltzustandWiederHer()
    {
        // Vorbereitung
        var waffe = ErstelleWaffe();
        var trank = ErstelleHeiltrank();
        var service = ErstelleService(waffe, trank);

        var inventarVorher = new Dictionary<string, int>
        {
            [waffe.Id] = 1,
            [trank.Id] = 3
        };
        var equipment = new Dictionary<EquipSlot, string>
        {
            [EquipSlot.Weapon] = waffe.Id
        };

        // Ausführung
        service.RestoreState(inventarVorher, equipment);

        // Prüfung
        service.GetItemCount(trank.Id).Should().Be(3, "Trank-Bestand muss wiederhergestellt sein");
        service.GetEquippedId(EquipSlot.Weapon).Should().Be(waffe.Id,
            "ausgerüstete Waffe muss nach Restore korrekt sein");
    }
}
