using FluentAssertions;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using NSubstitute;
using RebornSaga.Models;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für GoldService: AddGold, SpendGold, MaxGold-Clamp, Video-Reward,
/// täglicher Cooldown und Event-Auslösung.
/// Der GoldService ist das kritischste Wirtschafts-System – Overflow oder
/// negative Werte würden die gesamte Gold-Economy kaputt machen.
/// </summary>
public class GoldServiceTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    /// <summary>
    /// Einfacher Preferences-Stub der echte Dictionary-Persistenz simuliert.
    /// NSubstitute-Mocks sind statisch und können den Zustand nicht korrekt verändern,
    /// da IPreferencesService generische Get/Set-Methoden hat.
    /// </summary>
    private class PreferencesStub : IPreferencesService
    {
        private readonly Dictionary<string, object> _store = new();

        public T Get<T>(string key, T defaultValue)
            => _store.TryGetValue(key, out var v) ? (T)v : defaultValue;

        public void Set<T>(string key, T value) => _store[key] = value!;

        public bool ContainsKey(string key) => _store.ContainsKey(key);
        public void Remove(string key) => _store.Remove(key);
        public void Clear() => _store.Clear();
    }

    /// <summary>
    /// Erstellt einen GoldService mit einem echten Preferences-Stub.
    /// lastWatchDate="" simuliert neuen Tag (Zähler werden zurückgesetzt).
    /// </summary>
    private static (GoldService service, PreferencesStub prefs, IRewardedAdService ads)
        ErstelleGoldService(string lastWatchDate = "")
    {
        var prefs = new PreferencesStub();
        var ads = Substitute.For<IRewardedAdService>();

        // Optionales Vorbelegen des Datums (leer = neuer Tag)
        if (!string.IsNullOrEmpty(lastWatchDate))
            prefs.Set("gold_last_watch_date", lastWatchDate);

        var service = new GoldService(prefs, ads);
        return (service, prefs, ads);
    }

    private static Player ErstelleSpieler(int gold = 0)
    {
        var p = Player.Create(Models.Enums.ClassName.Swordmaster);
        p.Gold = gold;
        return p;
    }

    // ─── AddGold ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddGold_NormalesMenge_ErhoehtGold()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 100);

        // Ausführung
        service.AddGold(spieler, 250);

        // Prüfung
        spieler.Gold.Should().Be(350, "100 + 250 = 350 Gold");
    }

    [Fact]
    public void AddGold_NullOderNegativ_IgnoriertAufruf()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 500);

        // Ausführung: Null und negative Beträge
        service.AddGold(spieler, 0);
        service.AddGold(spieler, -100);

        // Prüfung: Gold bleibt unverändert
        spieler.Gold.Should().Be(500, "AddGold mit <= 0 darf nichts tun");
    }

    [Fact]
    public void AddGold_UeberMaxGold_ClamptAufMaxGold()
    {
        // Vorbereitung: Fast am Limit
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: GoldService.MaxGold - 1);

        // Ausführung: +1.000.000 würde weit über das Limit gehen
        service.AddGold(spieler, 1_000_000);

        // Prüfung: Genau am Limit gestoppt
        spieler.Gold.Should().Be(GoldService.MaxGold,
            "Gold darf MaxGold (9.999.999) nicht überschreiten");
    }

    [Fact]
    public void AddGold_ExaktMaxGold_BleibtBeiMaxGold()
    {
        // Grenzfall: Exakt auf MaxGold setzen
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 0);

        service.AddGold(spieler, GoldService.MaxGold);

        spieler.Gold.Should().Be(GoldService.MaxGold,
            "exakt MaxGold hinzufügen muss erlaubt sein");
    }

    [Fact]
    public void AddGold_FeuertGoldChangedEvent()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 100);
        int? eventAlt = null, eventNeu = null;
        service.GoldChanged += (alt, neu) => { eventAlt = alt; eventNeu = neu; };

        // Ausführung
        service.AddGold(spieler, 200);

        // Prüfung
        eventAlt.Should().Be(100, "GoldChanged liefert den alten Wert");
        eventNeu.Should().Be(300, "GoldChanged liefert den neuen Wert");
    }

    [Fact]
    public void AddGold_NullMenge_FeuertKeinEvent()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 100);
        bool eventAusgeloest = false;
        service.GoldChanged += (_, _) => eventAusgeloest = true;

        // Ausführung
        service.AddGold(spieler, 0);

        // Prüfung
        eventAusgeloest.Should().BeFalse("kein Event bei AddGold(0)");
    }

    // ─── SpendGold ────────────────────────────────────────────────────────────

    [Fact]
    public void SpendGold_AusreichendGold_GibtTrueZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 500);

        // Ausführung
        bool erfolg = service.SpendGold(spieler, 200);

        // Prüfung
        erfolg.Should().BeTrue("500 Gold reichen für 200");
        spieler.Gold.Should().Be(300, "500 - 200 = 300");
    }

    [Fact]
    public void SpendGold_NichtGenugGold_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 100);

        // Ausführung
        bool erfolg = service.SpendGold(spieler, 200);

        // Prüfung
        erfolg.Should().BeFalse("100 Gold reichen nicht für 200");
        spieler.Gold.Should().Be(100, "Gold darf sich bei fehlgeschlagenem Kauf nicht ändern");
    }

    [Fact]
    public void SpendGold_ExaktGoldVorhanden_GibtTrueZurueck()
    {
        // Grenzfall: Genau so viel Gold wie nötig
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 800);

        bool erfolg = service.SpendGold(spieler, 800);

        erfolg.Should().BeTrue("exakt passender Betrag muss funktionieren");
        spieler.Gold.Should().Be(0, "nach exaktem Ausgeben ist Gold = 0");
    }

    [Fact]
    public void SpendGold_NullOderNegativ_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 1000);

        // Prüfung
        service.SpendGold(spieler, 0).Should().BeFalse("0 ausgeben ist ungültig");
        service.SpendGold(spieler, -50).Should().BeFalse("negativer Betrag ist ungültig");
        spieler.Gold.Should().Be(1000, "Gold darf sich nicht geändert haben");
    }

    [Fact]
    public void SpendGold_FeuertGoldChangedEvent()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 1000);
        int? eventAlt = null, eventNeu = null;
        service.GoldChanged += (alt, neu) => { eventAlt = alt; eventNeu = neu; };

        // Ausführung
        service.SpendGold(spieler, 300);

        // Prüfung
        eventAlt.Should().Be(1000);
        eventNeu.Should().Be(700);
    }

    [Fact]
    public void SpendGold_FehlgeschlagenKauf_FeuertKeinEvent()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleGoldService();
        var spieler = ErstelleSpieler(gold: 50);
        bool eventAusgeloest = false;
        service.GoldChanged += (_, _) => eventAusgeloest = true;

        // Ausführung: Zu wenig Gold
        service.SpendGold(spieler, 100);

        // Prüfung
        eventAusgeloest.Should().BeFalse(
            "kein Event wenn SpendGold fehlschlägt");
    }

    // ─── WatchVideoForGoldAsync ───────────────────────────────────────────────

    [Fact]
    public async Task WatchVideoForGoldAsync_AdErfolgreich_GibtTrueUndGold()
    {
        // Vorbereitung: Ad-Mock gibt true zurück
        var (service, _, ads) = ErstelleGoldService();
        ads.ShowAdAsync("gold_bonus").Returns(true);
        var spieler = ErstelleSpieler(gold: 0);

        // Ausführung
        bool ergebnis = await service.WatchVideoForGoldAsync(spieler);

        // Prüfung
        ergebnis.Should().BeTrue("erfolgreiche Ad → Belohnung vergeben");
        spieler.Gold.Should().Be(GoldService.VideoReward,
            "Spieler erhält VideoReward (500 Gold)");
    }

    [Fact]
    public async Task WatchVideoForGoldAsync_AdFehlgeschlagen_GibtFalseUndKeinGold()
    {
        // Vorbereitung: Ad schlägt fehl
        var (service, _, ads) = ErstelleGoldService();
        ads.ShowAdAsync("gold_bonus").Returns(false);
        var spieler = ErstelleSpieler(gold: 0);

        // Ausführung
        bool ergebnis = await service.WatchVideoForGoldAsync(spieler);

        // Prüfung
        ergebnis.Should().BeFalse("fehlgeschlagene Ad → keine Belohnung");
        spieler.Gold.Should().Be(0, "kein Gold bei fehlgeschlagenem Video");
    }

    [Fact]
    public async Task WatchVideoForGoldAsync_DreiMalErfolgreich_VerbrauchtTagesLimit()
    {
        // Vorbereitung: Die 3 erfolgreichen Calls verringern DailyVideoWatchesRemaining intern.
        // ResetDailyCountersIfNeeded() liest das Datum, aber nach WatchVideoForGoldAsync
        // wird DailyVideoWatchesRemaining-- direkt im Objekt gesetzt (ohne Preferences-Round-Trip).
        // Daher reicht es, alle 3 Calls mit einem Mock zu machen und dann das Property zu prüfen.
        var (service, prefs, ads) = ErstelleGoldService();
        ads.ShowAdAsync("gold_bonus").Returns(true);
        var spieler = ErstelleSpieler(gold: 0);

        // Ausführung: 3 Videos schauen (verbraucht intern DailyVideoWatchesRemaining von 3→0)
        await service.WatchVideoForGoldAsync(spieler); // 3→2
        await service.WatchVideoForGoldAsync(spieler); // 2→1
        await service.WatchVideoForGoldAsync(spieler); // 1→0

        // Prüfung: Kein 4. Video möglich (DailyVideoWatchesRemaining=0)
        bool viertes = await service.WatchVideoForGoldAsync(spieler);
        viertes.Should().BeFalse("nach 3 Videos täglich ist kein weiteres möglich");
        spieler.Gold.Should().Be(1500, "3 * 500 Gold wurden vergeben");
    }

    [Fact]
    public async Task WatchVideoForGoldAsync_VideoRewardIst500Gold()
    {
        // Sicherstellt dass die Konstante nicht versehentlich geändert wurde
        GoldService.VideoReward.Should().Be(500,
            "Video-Belohnung muss 500 Gold betragen (gemäß Design-Dokument)");
    }

    // ─── CanWatchVideo ────────────────────────────────────────────────────────

    [Fact]
    public void CanWatchVideo_NochVideosVerfuegbar_GibtTrue()
    {
        // Vorbereitung: Neuer Tag, 3 Videos verfügbar
        var (service, _, _) = ErstelleGoldService();

        service.CanWatchVideo().Should().BeTrue("zu Beginn des Tages sind Videos verfügbar");
    }

    [Fact]
    public void CanWatchVideo_AlleVideosVerbraucht_GibtFalse()
    {
        // Vorbereitung: 3 Videos bereits heute geschaut
        var today = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var prefs = new PreferencesStub();
        prefs.Set("gold_last_watch_date", today);
        prefs.Set("gold_daily_watches", 3); // alle 3 verbraucht
        var ads = Substitute.For<IRewardedAdService>();

        var service = new GoldService(prefs, ads);

        service.CanWatchVideo().Should().BeFalse("nach 3 Videos pro Tag: false");
    }

    // ─── DailyVideoWatchesRemaining ───────────────────────────────────────────

    [Fact]
    public void DailyVideoWatchesRemaining_NeuerTag_IstDrei()
    {
        // Vorbereitung: letzter Watch-Tag ist gestern → neuer Tag
        var (service, _, _) = ErstelleGoldService(lastWatchDate: "");

        service.DailyVideoWatchesRemaining.Should().Be(3,
            "zu Beginn eines neuen Tages stehen 3 Videos zur Verfügung");
    }

    [Fact]
    public void DailyVideoWatchesRemaining_EinVideoGeschauts_IstZwei()
    {
        // Vorbereitung: Heute schon 1 Video geschaut
        var today = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var prefs = new PreferencesStub();
        prefs.Set("gold_last_watch_date", today);
        prefs.Set("gold_daily_watches", 1);
        var ads = Substitute.For<IRewardedAdService>();

        var service = new GoldService(prefs, ads);

        service.DailyVideoWatchesRemaining.Should().Be(2,
            "nach 1 von 3 Videos: noch 2 verbleibend");
    }

    // ─── Kombinierter Fluss ───────────────────────────────────────────────────

    [Fact]
    public async Task WatchVideoForGoldAsync_NachAddGold_SummiertsichKorrekt()
    {
        // Vorbereitung: Spieler hat schon Gold
        var (service, _, ads) = ErstelleGoldService();
        ads.ShowAdAsync("gold_bonus").Returns(true);
        var spieler = ErstelleSpieler(gold: 1000);

        // Ausführung
        await service.WatchVideoForGoldAsync(spieler);

        // Prüfung
        spieler.Gold.Should().Be(1500,
            "1000 Basis + 500 Video-Belohnung = 1500");
    }
}
