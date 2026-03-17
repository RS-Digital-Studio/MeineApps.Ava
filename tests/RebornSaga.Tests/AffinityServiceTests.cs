using FluentAssertions;
using RebornSaga.Models;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für AffinityService und AffinityData:
/// Punkte hinzufügen, Bond-Level-Up, Bond-Szenen, Restore.
/// Das Bond-System beeinflusst Story-Verzweigungen und Endings –
/// fehlerhafte Level-Berechnung würde falsche Dialog-Optionen sperren.
/// </summary>
public class AffinityServiceTests
{
    // ─── AffinityData.CalculateLevel ─────────────────────────────────────────

    [Theory]
    [InlineData(0, 1, "0 Punkte → Level 1 (Bekannter)")]
    [InlineData(49, 1, "49 Punkte → noch Level 1")]
    [InlineData(50, 2, "50 Punkte → Level 2 (Verbündeter)")]
    [InlineData(149, 2, "149 Punkte → noch Level 2")]
    [InlineData(150, 3, "150 Punkte → Level 3 (Freund)")]
    [InlineData(299, 3, "299 Punkte → noch Level 3")]
    [InlineData(300, 4, "300 Punkte → Level 4 (Vertrauter)")]
    [InlineData(499, 4, "499 Punkte → noch Level 4")]
    [InlineData(500, 5, "500 Punkte → Level 5 (Seelenverwandt)")]
    [InlineData(9999, 5, "9999 Punkte → immer noch Level 5 (Maximum)")]
    public void CalculateLevel_GrenzwertTabelle_GibtKorrektesLevel(
        int punkte, int erwartetesLevel, string beschreibung)
    {
        AffinityData.CalculateLevel(punkte).Should().Be(erwartetesLevel, beschreibung);
    }

    [Theory]
    [InlineData(2, 50, "Level 2 braucht 50 Punkte")]
    [InlineData(3, 150, "Level 3 braucht 150 Punkte")]
    [InlineData(4, 300, "Level 4 braucht 300 Punkte")]
    [InlineData(5, 500, "Level 5 braucht 500 Punkte")]
    public void PointsForLevel_GibtKorrekteSchwelle(int level, int erwartet, string beschreibung)
    {
        AffinityData.PointsForLevel(level).Should().Be(erwartet, beschreibung);
    }

    // ─── AffinityService.Initialize ──────────────────────────────────────────

    [Fact]
    public void Initialize_AlleNPCsVorhanden()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();

        // Prüfung: Alle 5 NPCs initialisiert
        foreach (var npcId in new[] { "aria", "aldric", "kael", "luna", "vex" })
        {
            service.GetAffinity(npcId).Should().NotBeNull(
                $"NPC '{npcId}' muss nach Initialize vorhanden sein");
            service.GetBondLevel(npcId).Should().Be(1,
                $"NPC '{npcId}' startet auf Bond-Level 1");
        }
    }

    [Fact]
    public void Initialize_WiederholtAufgerufen_SetzteZurueckAufLevel1()
    {
        // Vorbereitung: Erst Punkte sammeln, dann neu initialisieren
        var service = new AffinityService();
        service.Initialize();
        service.AddPoints("aria", 500); // Level 5

        // Ausführung
        service.Initialize();

        // Prüfung: Reset auf Level 1
        service.GetBondLevel("aria").Should().Be(1,
            "nach Initialize muss Bond-Level zurückgesetzt sein");
        service.GetAffinity("aria")!.Points.Should().Be(0, "Punkte zurückgesetzt");
    }

    // ─── AddPoints / BondLevelUp ──────────────────────────────────────────────

    [Fact]
    public void AddPoints_PunkteHinzufuegen_ErhoehtPunktstand()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();

        // Ausführung
        service.AddPoints("aria", 30);

        // Prüfung
        service.GetAffinity("aria")!.Points.Should().Be(30, "30 Punkte hinzugefügt");
    }

    [Fact]
    public void AddPoints_SchwelleErreicht_GibtTrueUndErhoehtLevel()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();

        // Ausführung: Exakt auf Level-2-Schwelle
        bool levelUp = service.AddPoints("aria", 50);

        // Prüfung
        levelUp.Should().BeTrue("50 Punkte erreichen Level 2 → true zurück");
        service.GetBondLevel("aria").Should().Be(2, "Bond-Level muss 2 sein");
    }

    [Fact]
    public void AddPoints_UnterSchwelle_GibtFalseZurueck()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();

        // Ausführung: Unter der Schwelle von 50
        bool levelUp = service.AddPoints("aria", 49);

        // Prüfung
        levelUp.Should().BeFalse("49 Punkte reichen nicht für Level 2");
        service.GetBondLevel("aria").Should().Be(1, "Level bleibt 1");
    }

    [Fact]
    public void AddPoints_FeuertBondLevelUpEvent()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();
        string? eventNpcId = null;
        int eventNeuesLevel = 0;
        service.BondLevelUp += (npcId, level) => { eventNpcId = npcId; eventNeuesLevel = level; };

        // Ausführung
        service.AddPoints("aldric", 50);

        // Prüfung
        eventNpcId.Should().Be("aldric", "BondLevelUp liefert die NPC-ID");
        eventNeuesLevel.Should().Be(2, "BondLevelUp liefert das neue Level");
    }

    [Fact]
    public void AddPoints_KeinLevelUp_FeuertKeinEvent()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();
        bool eventAusgeloest = false;
        service.BondLevelUp += (_, _) => eventAusgeloest = true;

        // Ausführung: Unter Schwelle
        service.AddPoints("luna", 30);

        // Prüfung
        eventAusgeloest.Should().BeFalse("kein Event ohne Level-Up");
    }

    [Fact]
    public void AddPoints_UnbekannterNPC_GibtFalseZurueck()
    {
        // Vorbereitung: Service ohne Initialize
        var service = new AffinityService();

        // Ausführung
        bool ergebnis = service.AddPoints("unbekannt_npc", 100);

        // Prüfung
        ergebnis.Should().BeFalse("unbekannte NPC-ID muss false zurückgeben");
    }

    [Fact]
    public void AddPoints_MehrfachAddierenBisLevel5_ProgressionKorrekt()
    {
        // Integration: Von Level 1 auf Level 5 hocharbeiten
        var service = new AffinityService();
        service.Initialize();
        int levelUps = 0;
        service.BondLevelUp += (_, _) => levelUps++;

        service.AddPoints("aria", 50);  // → Level 2
        service.AddPoints("aria", 100); // → Level 3
        service.AddPoints("aria", 150); // → Level 4
        service.AddPoints("aria", 200); // → Level 5

        service.GetBondLevel("aria").Should().Be(5, "nach 500 Punkten ist Level 5 erreicht");
        levelUps.Should().Be(4, "4 Level-Ups von 1→5");
    }

    [Fact]
    public void AddPoints_BeimLevel5Bleiben_KeinWeitererLevelUp()
    {
        // Grenzfall: Über Level 5 hinaus → kein weiterer Level-Up möglich
        var service = new AffinityService();
        service.Initialize();
        service.AddPoints("vex", 500); // Level 5 erreicht
        int extraLevelUps = 0;
        service.BondLevelUp += (_, _) => extraLevelUps++;

        // Ausführung: Noch mehr Punkte
        service.AddPoints("vex", 10000);

        // Prüfung
        service.GetBondLevel("vex").Should().Be(5, "Level 5 ist Maximum, kein Level 6");
        extraLevelUps.Should().Be(0, "kein Level-Up-Event wenn bereits Level 5");
    }

    // ─── NPCs sind unabhängig voneinander ─────────────────────────────────────

    [Fact]
    public void AddPoints_VarianteNPCs_SindUnabhaengig()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();

        // Ausführung: Nur Aria
        service.AddPoints("aria", 500);

        // Prüfung: Aldric unberührt
        service.GetBondLevel("aldric").Should().Be(1,
            "Arias Level-Up darf Aldrics Level nicht beeinflussen");
        service.GetAffinity("aldric")!.Points.Should().Be(0,
            "Aldrics Punkte bleiben 0");
    }

    // ─── Bond-Szenen ─────────────────────────────────────────────────────────

    [Fact]
    public void HasUnseenScene_LevelEinsKeineSzenen_GibtFalse()
    {
        // Vorbereitung: Frisch initialisiert, Level 1
        var service = new AffinityService();
        service.Initialize();

        service.HasUnseenScene("aria").Should().BeFalse(
            "auf Level 1 gibt es keine Bond-Szenen (erst ab Level 2)");
    }

    [Fact]
    public void HasUnseenScene_Level2NochNichtGesehen_GibtTrue()
    {
        // Vorbereitung: Auf Level 2 hocharbeiten, Szene nicht gesehen
        var service = new AffinityService();
        service.Initialize();
        service.AddPoints("aria", 50); // → Level 2

        service.HasUnseenScene("aria").Should().BeTrue(
            "Level 2 Bond-Szene noch nicht gesehen");
    }

    [Fact]
    public void MarkSceneSeen_DanachKeinUngesehenesSzenen()
    {
        // Vorbereitung
        var service = new AffinityService();
        service.Initialize();
        service.AddPoints("aria", 50); // → Level 2

        // Ausführung
        service.MarkSceneSeen("aria", 2);

        // Prüfung
        service.HasUnseenScene("aria").Should().BeFalse(
            "nach MarkSceneSeen(2) keine ungesehene Szene mehr");
    }

    [Fact]
    public void HasUnseenScene_Level3ErreichtAberNurLevel2Gesehen_GibtTrue()
    {
        // Vorbereitung: Level 3, nur Szene 2 gesehen
        var service = new AffinityService();
        service.Initialize();
        service.AddPoints("aria", 150); // → Level 3
        service.MarkSceneSeen("aria", 2);

        // Prüfung: Szene 3 noch ausstehend
        service.HasUnseenScene("aria").Should().BeTrue(
            "Level 3 Szene noch nicht gesehen obwohl Level 2 gesehen");
    }

    // ─── RestoreAffinities ────────────────────────────────────────────────────

    [Fact]
    public void RestoreAffinities_StelltZustandWiederHer()
    {
        // Vorbereitung
        var service = new AffinityService();
        var wiederherstellDaten = new Dictionary<string, AffinityData>
        {
            ["aria"] = new() { NpcId = "aria", Points = 300, BondLevel = 4 },
            ["kael"] = new() { NpcId = "kael", Points = 50, BondLevel = 2 }
        };

        // Ausführung
        service.RestoreAffinities(wiederherstellDaten);

        // Prüfung
        service.GetBondLevel("aria").Should().Be(4, "Arias Level muss wiederhergestellt sein");
        service.GetAffinity("aria")!.Points.Should().Be(300);
        service.GetBondLevel("kael").Should().Be(2);
    }

    [Fact]
    public void GetAllAffinities_GibtNeuesDictionaryZurueck_NichtDieSelbeInstanz()
    {
        // GetAllAffinities() gibt new Dictionary<...>(_affinities) zurück –
        // also eine neue Dictionary-Instanz, aber die AffinityData-Werte
        // sind Referenz-Typen und werden NICHT geklont.
        // Dieser Test dokumentiert das tatsächliche Verhalten.
        var service = new AffinityService();
        service.Initialize();

        var kopie1 = service.GetAllAffinities();
        var kopie2 = service.GetAllAffinities();

        // Prüfung: Zwei verschiedene Dictionary-Instanzen
        kopie1.Should().NotBeSameAs(kopie2,
            "GetAllAffinities muss jedes Mal ein neues Dictionary erstellen");

        // HINWEIS: Die enthaltenen AffinityData-Objekte sind identische Referenzen.
        // Wer Save-Daten manipulieren will, muss AffinityData selbst klonen.
        kopie1["aria"].Should().BeSameAs(kopie2["aria"],
            "die AffinityData-Objekte sind Referenzen (kein Deep-Clone)");
    }
}
