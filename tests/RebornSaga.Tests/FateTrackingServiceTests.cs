using FluentAssertions;
using RebornSaga.Models;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für FateTrackingService: Karma-Berechnung, Clamp, Flags, Tendenz.
/// Das Karma-System ist unsichtbar für den Spieler, bestimmt aber das Ending.
/// Fehler hier (z.B. fehlender Clamp) würden Ending-Logik silent kaputt machen.
/// </summary>
public class FateTrackingServiceTests
{
    // ─── Startzustand ─────────────────────────────────────────────────────────

    [Fact]
    public void NeuerstService_KarmaIstNull()
    {
        var service = new FateTrackingService();

        service.Karma.Should().Be(0, "Karma startet bei 0 (neutral)");
    }

    [Fact]
    public void NeuesterService_KeineEntscheidungenUndFlags()
    {
        var service = new FateTrackingService();

        service.Decisions.Should().BeEmpty("keine Entscheidungen am Start");
        service.FateFlags.Should().BeEmpty("keine Flags am Start");
    }

    // ─── ModifyKarma ─────────────────────────────────────────────────────────

    [Fact]
    public void ModifyKarma_PositiverWert_ErhoehtKarma()
    {
        var service = new FateTrackingService();

        service.ModifyKarma(20);

        service.Karma.Should().Be(20, "positives Karma erhöht den Wert");
    }

    [Fact]
    public void ModifyKarma_NegativerWert_VerringertKarma()
    {
        var service = new FateTrackingService();

        service.ModifyKarma(-30);

        service.Karma.Should().Be(-30, "negatives Karma verringert den Wert");
    }

    [Fact]
    public void ModifyKarma_UeberMaximum_ClamptAuf100()
    {
        var service = new FateTrackingService();

        service.ModifyKarma(999);

        service.Karma.Should().Be(100, "Karma darf nicht über 100 gehen");
    }

    [Fact]
    public void ModifyKarma_UnterMinimum_ClamptAufMinus100()
    {
        var service = new FateTrackingService();

        service.ModifyKarma(-999);

        service.Karma.Should().Be(-100, "Karma darf nicht unter -100 fallen");
    }

    [Fact]
    public void ModifyKarma_GenauAnGrenze_BleibtBeiGrenze()
    {
        var service = new FateTrackingService();
        service.ModifyKarma(100); // Exakt Maximum

        service.Karma.Should().Be(100, "Karma exakt bei 100 erlaubt");
    }

    [Fact]
    public void ModifyKarma_NullWert_AendertNichtsUndFeuertKeinEvent()
    {
        var service = new FateTrackingService();
        bool eventAusgeloest = false;
        service.KarmaChanged += (_, _) => eventAusgeloest = true;

        service.ModifyKarma(0);

        service.Karma.Should().Be(0, "ModifyKarma(0) ändert nichts");
        eventAusgeloest.Should().BeFalse("kein KarmaChanged-Event bei Änderung um 0");
    }

    [Fact]
    public void ModifyKarma_FeuertKarmaChangedEvent()
    {
        var service = new FateTrackingService();
        int? altWert = null, neuWert = null;
        service.KarmaChanged += (alt, neu) => { altWert = alt; neuWert = neu; };

        service.ModifyKarma(15);

        altWert.Should().Be(0, "alter Wert war 0");
        neuWert.Should().Be(15, "neuer Wert ist 15");
    }

    // ─── RecordDecision ───────────────────────────────────────────────────────

    [Fact]
    public void RecordDecision_LoggtEntscheidung()
    {
        var service = new FateTrackingService();

        service.RecordDecision("k1", "node_001", choiceIndex: 0, karmaChange: 10, "save_the_child");

        service.Decisions.Should().HaveCount(1, "eine Entscheidung wurde geloggt");
        service.Decisions[0].ChapterId.Should().Be("k1");
        service.Decisions[0].NodeId.Should().Be("node_001");
        service.Decisions[0].ChoiceIndex.Should().Be(0);
        service.Decisions[0].KarmaChange.Should().Be(10);
    }

    [Fact]
    public void RecordDecision_MitKarmaChange_AentertKarma()
    {
        var service = new FateTrackingService();

        service.RecordDecision("k1", "node_001", 0, karmaChange: 25, "heldentat");

        service.Karma.Should().Be(25, "Karma muss durch Entscheidung geändert werden");
    }

    [Fact]
    public void RecordDecision_KarmaChangeNull_FeuertKeinKarmaEvent()
    {
        var service = new FateTrackingService();
        bool eventAusgeloest = false;
        service.KarmaChanged += (_, _) => eventAusgeloest = true;

        service.RecordDecision("k1", "node_001", 0, karmaChange: 0, "neutral_choice");

        eventAusgeloest.Should().BeFalse("Entscheidung ohne Karma-Änderung feuert kein Event");
    }

    [Fact]
    public void GetDecisionsForChapter_NurKapitelEntscheidungen()
    {
        // Vorbereitung: Entscheidungen aus verschiedenen Kapiteln
        var service = new FateTrackingService();
        service.RecordDecision("k1", "node_001", 0, 10, "");
        service.RecordDecision("k2", "node_001", 1, -10, "");
        service.RecordDecision("k1", "node_002", 0, 5, "");

        // Ausführung
        var k1Entscheidungen = service.GetDecisionsForChapter("k1");

        // Prüfung
        k1Entscheidungen.Should().HaveCount(2, "K1 hat 2 Entscheidungen");
        k1Entscheidungen.Should().AllSatisfy(d =>
            d.ChapterId.Should().Be("k1", "nur K1-Entscheidungen"));
    }

    // ─── GetTendency ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, KarmaTendency.Hero, "maximales Karma → Wahrer Held")]
    [InlineData(60, KarmaTendency.Hero, "60 Karma → Wahrer Held (Grenzwert)")]
    [InlineData(59, KarmaTendency.Good, "59 Karma → Gutherzig")]
    [InlineData(20, KarmaTendency.Good, "20 Karma → Gutherzig (Grenzwert)")]
    [InlineData(19, KarmaTendency.Neutral, "19 Karma → Pragmatiker")]
    [InlineData(0, KarmaTendency.Neutral, "0 Karma → Pragmatiker (Mitte)")]
    [InlineData(-19, KarmaTendency.Neutral, "-19 Karma → Pragmatiker (Grenzwert)")]
    [InlineData(-20, KarmaTendency.Dark, "-20 Karma → Dunkel (Grenzwert)")]
    [InlineData(-59, KarmaTendency.Dark, "-59 Karma → Dunkel")]
    [InlineData(-60, KarmaTendency.Fallen, "-60 Karma → Gefallen (Grenzwert)")]
    [InlineData(-100, KarmaTendency.Fallen, "minimales Karma → Gefallen")]
    public void GetTendency_KarmaWert_GibtKorrekTendenz(
        int karma, KarmaTendency erwartet, string beschreibung)
    {
        // Vorbereitung
        var service = new FateTrackingService();
        service.Restore(karma, new List<FateDecision>());

        // Prüfung
        service.GetTendency().Should().Be(erwartet, beschreibung);
    }

    // ─── Flags ───────────────────────────────────────────────────────────────

    [Fact]
    public void AddFlag_NeuesFlag_IstDanachVorhanden()
    {
        var service = new FateTrackingService();

        service.AddFlag("betrayed_aldric");

        service.HasFlag("betrayed_aldric").Should().BeTrue("gesetztes Flag muss vorhanden sein");
    }

    [Fact]
    public void AddFlag_FeuertFateFlagChangedEvent()
    {
        var service = new FateTrackingService();
        string? eventFlag = null;
        bool? eventGesetzt = null;
        service.FateFlagChanged += (flag, gesetzt) => { eventFlag = flag; eventGesetzt = gesetzt; };

        service.AddFlag("saved_luna");

        eventFlag.Should().Be("saved_luna");
        eventGesetzt.Should().BeTrue("true = Flag wurde gesetzt");
    }

    [Fact]
    public void AddFlag_BereitsVorhanden_FeuertKeinZweitesEvent()
    {
        var service = new FateTrackingService();
        service.AddFlag("test_flag");
        int eventAnzahl = 0;
        service.FateFlagChanged += (_, _) => eventAnzahl++;

        service.AddFlag("test_flag"); // Nochmal setzen

        eventAnzahl.Should().Be(0, "bereits vorhandenes Flag feuert kein zweites Event");
    }

    [Fact]
    public void RemoveFlag_VorhandeneFlag_EntferntEs()
    {
        var service = new FateTrackingService();
        service.AddFlag("betrayed_aldric");

        service.RemoveFlag("betrayed_aldric");

        service.HasFlag("betrayed_aldric").Should().BeFalse("entferntes Flag darf nicht mehr vorhanden sein");
    }

    [Fact]
    public void RemoveFlag_FeuertFateFlagChangedEvent()
    {
        var service = new FateTrackingService();
        service.AddFlag("test_flag");
        bool? eventGesetzt = null;
        service.FateFlagChanged += (_, gesetzt) => eventGesetzt = gesetzt;

        service.RemoveFlag("test_flag");

        eventGesetzt.Should().BeFalse("false = Flag wurde entfernt");
    }

    [Fact]
    public void RemoveFlag_NichtVorhanden_FeuertKeinEvent()
    {
        var service = new FateTrackingService();
        bool eventAusgeloest = false;
        service.FateFlagChanged += (_, _) => eventAusgeloest = true;

        service.RemoveFlag("nicht_vorhanden");

        eventAusgeloest.Should().BeFalse("nicht vorhandenes Flag feuert kein Event");
    }

    [Fact]
    public void AddFlag_LeerString_IgnoriertAufruf()
    {
        var service = new FateTrackingService();

        // Kein Crash erwartet
        service.AddFlag("");
        service.AddFlag(null!);

        service.FateFlags.Should().BeEmpty("leere/null Flags werden ignoriert");
    }

    // ─── RecordFateChange ─────────────────────────────────────────────────────

    [Fact]
    public void RecordFateChange_SpeichertFlagMitPräfix()
    {
        var service = new FateTrackingService();

        service.RecordFateChange("dark_pact");

        service.HasFlag("fate_dark_pact").Should().BeTrue(
            "RecordFateChange speichert Flag mit 'fate_'-Präfix");
    }

    [Fact]
    public void RecordFateChange_FeuertFateChangedEvent()
    {
        var service = new FateTrackingService();
        string? eventKey = null;
        service.FateChanged += key => eventKey = key;

        service.RecordFateChange("dark_pact");

        eventKey.Should().Be("dark_pact", "FateChanged liefert den Fate-Key ohne Präfix");
    }

    [Fact]
    public void RecordFateChange_LeerKey_IgnoriertAufruf()
    {
        var service = new FateTrackingService();
        bool eventAusgeloest = false;
        service.FateChanged += _ => eventAusgeloest = true;

        service.RecordFateChange("");

        eventAusgeloest.Should().BeFalse("leerer Fate-Key wird ignoriert");
        service.FateFlags.Should().BeEmpty();
    }

    // ─── Restore ─────────────────────────────────────────────────────────────

    [Fact]
    public void Restore_StelltKarmaWiederHer()
    {
        var service = new FateTrackingService();

        service.Restore(75, new List<FateDecision>());

        service.Karma.Should().Be(75, "Karma muss wiederhergestellt werden");
    }

    [Fact]
    public void Restore_ClamptKarmaAufGueltigeGrenzen()
    {
        var service = new FateTrackingService();

        service.Restore(9999, new List<FateDecision>());

        service.Karma.Should().Be(100, "Restore clampt ungültiges Karma auf Maximum");
    }

    [Fact]
    public void Restore_StelltEntscheidungenWiederHer()
    {
        var service = new FateTrackingService();
        var entscheidungen = new List<FateDecision>
        {
            new() { ChapterId = "k1", NodeId = "n1", KarmaChange = 10 },
            new() { ChapterId = "k2", NodeId = "n2", KarmaChange = -5 }
        };

        service.Restore(50, entscheidungen);

        service.Decisions.Should().HaveCount(2, "2 Entscheidungen wiederhergestellt");
    }

    [Fact]
    public void Restore_StelltFateFlagsWiederHer()
    {
        var service = new FateTrackingService();
        var flags = new HashSet<string> { "betrayed_aldric", "saved_luna" };

        service.Restore(0, new List<FateDecision>(), flags);

        service.HasFlag("betrayed_aldric").Should().BeTrue();
        service.HasFlag("saved_luna").Should().BeTrue();
    }

    [Fact]
    public void Restore_OhneFateFlags_LeertFlags()
    {
        // Vorbereitung: Erst Flag setzen
        var service = new FateTrackingService();
        service.AddFlag("alter_flag");

        // Ausführung: Restore ohne Flags
        service.Restore(0, new List<FateDecision>(), null);

        service.FateFlags.Should().BeEmpty(
            "Restore mit null-Flags muss bestehende Flags löschen");
    }
}
