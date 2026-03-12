using FluentAssertions;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für SkillService: Skill-Initialisierung, Mastery, Evolution.
/// Da die JSON-Embedded-Resources in Tests nicht verfügbar sind, werden
/// Skills manuell in _allSkills injiziert via Reflection, oder es wird
/// direkt mit PlayerSkill-Objekten gearbeitet.
/// </summary>
public class SkillTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    /// <summary>
    /// Injiziert Skills direkt in den SkillService via Reflection,
    /// da die Embedded-Resources im Test-Kontext nicht verfügbar sind.
    /// </summary>
    private static SkillService ErstelleServiceMitSkills(params Skill[] skills)
    {
        var service = new SkillService();

        // Via Reflection: _allSkills direkt setzen
        var allSkillsField = typeof(SkillService)
            .GetField("_allSkills",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        allSkillsField.Should().NotBeNull("_allSkills-Feld muss über Reflection zugänglich sein");

        var allSkills = (Dictionary<string, Skill>)allSkillsField!.GetValue(service)!;
        foreach (var skill in skills)
            allSkills[skill.Id] = skill;

        return service;
    }

    private static Skill ErstelleSkill(
        string id,
        ClassName klasse,
        int tier = 1,
        int masteryRequired = 0,
        string? nextTierId = null,
        int mpKosten = 10,
        float multiplier = 1.5f)
    {
        return new Skill
        {
            Id = id,
            NameKey = $"skill_{id}",
            Class = klasse,
            Tier = tier,
            MasteryRequired = masteryRequired,
            NextTierId = nextTierId,
            MpCost = mpKosten,
            Multiplier = multiplier
        };
    }

    // ─── InitializeForClass ───────────────────────────────────────────────────

    [Fact]
    public void InitializeForClass_Schwertmeister_SchaltetTier1SkillsFrei()
    {
        // Vorbereitung: Skills injizieren
        var tier1Skill = ErstelleSkill("sm_slash_1", ClassName.Swordmaster, tier: 1);
        var tier2Skill = ErstelleSkill("sm_slash_2", ClassName.Swordmaster, tier: 2);
        var arkanistSkill = ErstelleSkill("arc_fireball_1", ClassName.Arcanist, tier: 1);
        var service = ErstelleServiceMitSkills(tier1Skill, tier2Skill, arkanistSkill);

        // Ausführung
        service.InitializeForClass(ClassName.Swordmaster);

        // Prüfung
        var freigeschaltet = service.GetUnlockedSkills();
        freigeschaltet.Should().HaveCount(1,
            "nur Tier-1-Skills der Klasse Schwertmeister werden freigeschaltet");
        freigeschaltet[0].Definition.Id.Should().Be("sm_slash_1",
            "der Tier-1-Skill muss freigeschaltet sein");
    }

    [Fact]
    public void InitializeForClass_AndereKlasse_SchaltetNurEigeneSkillsFrei()
    {
        // Vorbereitung
        var schwertSkill = ErstelleSkill("sm_skill", ClassName.Swordmaster, tier: 1);
        var arkaSkill = ErstelleSkill("arc_skill", ClassName.Arcanist, tier: 1);
        var service = ErstelleServiceMitSkills(schwertSkill, arkaSkill);

        // Ausführung: Arkanist auswählen
        service.InitializeForClass(ClassName.Arcanist);

        // Prüfung: Nur Arkanist-Skills freigeschaltet
        var freigeschaltet = service.GetUnlockedSkills();
        freigeschaltet.Should().AllSatisfy(s =>
            s.Definition.Class.Should().Be(ClassName.Arcanist,
                "nur Arkanist-Skills dürfen freigeschaltet sein"));
    }

    [Fact]
    public void InitializeForClass_NachWiederholung_LoeschtAltesSetup()
    {
        // Vorbereitung
        var schwertSkill = ErstelleSkill("sm_skill", ClassName.Swordmaster, tier: 1);
        var arkaSkill = ErstelleSkill("arc_skill", ClassName.Arcanist, tier: 1);
        var service = ErstelleServiceMitSkills(schwertSkill, arkaSkill);

        // Ausführung: Erst Schwertmeister, dann Arkanist
        service.InitializeForClass(ClassName.Swordmaster);
        service.InitializeForClass(ClassName.Arcanist);

        // Prüfung: Schwertmeister-Skills nicht mehr vorhanden
        var freigeschaltet = service.GetUnlockedSkills();
        freigeschaltet.Should().NotContain(s => s.Definition.Id == "sm_skill",
            "Klassen-Wechsel muss alte Skills zurücksetzen");
    }

    // ─── UseSkill / Mastery ───────────────────────────────────────────────────

    [Fact]
    public void UseSkill_BekannnterSkill_ErhoehtMastery()
    {
        // Vorbereitung
        var skill = ErstelleSkill("sm_slash_1", ClassName.Swordmaster, tier: 1, masteryRequired: 5);
        var service = ErstelleServiceMitSkills(skill);
        service.InitializeForClass(ClassName.Swordmaster);

        // Ausführung
        service.UseSkill("sm_slash_1");

        // Prüfung
        var ps = service.GetPlayerSkill("sm_slash_1");
        ps.Should().NotBeNull();
        ps!.Mastery.Should().Be(1, "erste Benutzung erhöht Mastery auf 1");
    }

    [Fact]
    public void UseSkill_UnbekannterSkill_GibtNullZurueck()
    {
        // Vorbereitung
        var service = new SkillService();

        // Ausführung
        var ergebnis = service.UseSkill("nicht_vorhanden");

        // Prüfung
        ergebnis.Should().BeNull("unbekannte Skill-ID gibt null zurück");
    }

    [Fact]
    public void UseSkill_GesperrterSkill_GibtNullZurueck()
    {
        // Vorbereitung: Skill manuell als gesperrt einrichten
        var skill = ErstelleSkill("sm_slash_1", ClassName.Swordmaster, tier: 1);
        var service = ErstelleServiceMitSkills(skill);
        service.InitializeForClass(ClassName.Swordmaster);

        // Skill sperren via Reflection
        var playerSkillsField = typeof(SkillService)
            .GetField("_playerSkills",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var playerSkills = (Dictionary<string, PlayerSkill>)playerSkillsField!.GetValue(service)!;
        playerSkills["sm_slash_1"].IsUnlocked = false;

        // Ausführung
        var ergebnis = service.UseSkill("sm_slash_1");

        // Prüfung
        ergebnis.Should().BeNull("gesperrter Skill kann nicht benutzt werden");
    }

    // ─── Evolution ───────────────────────────────────────────────────────────

    [Fact]
    public void UseSkill_MasteryErreicht_EvolviertzuNächstemTier()
    {
        // Vorbereitung: Tier-1 mit masteryRequired=3 → evolves zu Tier-2
        var tier1 = ErstelleSkill("sm_slash_1", ClassName.Swordmaster,
            tier: 1, masteryRequired: 3, nextTierId: "sm_slash_2");
        var tier2 = ErstelleSkill("sm_slash_2", ClassName.Swordmaster, tier: 2);
        var service = ErstelleServiceMitSkills(tier1, tier2);
        service.InitializeForClass(ClassName.Swordmaster);

        // Ausführung: 3x benutzen → Evolution bei 3. Benutzung
        service.UseSkill("sm_slash_1");
        service.UseSkill("sm_slash_1");
        var neueSkillId = service.UseSkill("sm_slash_1"); // Mastery = 3 → Evolution

        // Prüfung
        neueSkillId.Should().Be("sm_slash_2",
            "nach Erreichen der Mastery-Schwelle muss Evolution stattfinden");
    }

    [Fact]
    public void UseSkill_NachEvolution_NächsTermSkilllstFreigeschaltet()
    {
        // Vorbereitung
        var tier1 = ErstelleSkill("sm_slash_1", ClassName.Swordmaster,
            tier: 1, masteryRequired: 1, nextTierId: "sm_slash_2");
        var tier2 = ErstelleSkill("sm_slash_2", ClassName.Swordmaster, tier: 2);
        var service = ErstelleServiceMitSkills(tier1, tier2);
        service.InitializeForClass(ClassName.Swordmaster);

        // Ausführung: Evolution auslösen
        service.UseSkill("sm_slash_1"); // Mastery = 1 → Evolution

        // Prüfung: Tier-2 ist jetzt freigeschaltet
        var tier2Skill = service.GetPlayerSkill("sm_slash_2");
        tier2Skill.Should().NotBeNull("Tier-2-Skill muss nach Evolution existieren");
        tier2Skill!.IsUnlocked.Should().BeTrue("evolvierter Skill muss freigeschaltet sein");
    }

    [Fact]
    public void UseSkill_NachEvolution_AltesSkillGesperrt()
    {
        // Vorbereitung
        var tier1 = ErstelleSkill("sm_slash_1", ClassName.Swordmaster,
            tier: 1, masteryRequired: 1, nextTierId: "sm_slash_2");
        var tier2 = ErstelleSkill("sm_slash_2", ClassName.Swordmaster, tier: 2);
        var service = ErstelleServiceMitSkills(tier1, tier2);
        service.InitializeForClass(ClassName.Swordmaster);

        // Ausführung
        service.UseSkill("sm_slash_1"); // Evolution

        // Prüfung: Alter Skill gesperrt
        var tier1Skill = service.GetPlayerSkill("sm_slash_1");
        tier1Skill?.IsUnlocked.Should().BeFalse(
            "der evolvierte Skill muss nach Evolution gesperrt sein");
    }

    [Fact]
    public void UseSkill_OhneMasteryRequired_KeinEvolution()
    {
        // Vorbereitung: MasteryRequired = 0 → keine Evolution
        var skill = ErstelleSkill("sm_basic", ClassName.Swordmaster, masteryRequired: 0);
        var service = ErstelleServiceMitSkills(skill);
        service.InitializeForClass(ClassName.Swordmaster);

        // Ausführung: Viele Benutzungen
        for (int i = 0; i < 100; i++)
        {
            var result = service.UseSkill("sm_basic");
            result.Should().BeNull("Skill ohne MasteryRequired evolviert nicht");
        }
    }

    // ─── PlayerSkill-Modell ───────────────────────────────────────────────────

    [Fact]
    public void PlayerSkill_CanEvolve_FalschOhneNextTierId()
    {
        // Vorbereitung
        var skill = new Skill
        {
            Id = "test",
            MasteryRequired = 3,
            NextTierId = null // keine nächste Stufe
        };
        var playerSkill = new PlayerSkill
        {
            Definition = skill,
            Mastery = 10, // über Schwelle
            IsUnlocked = true
        };

        // Prüfung
        playerSkill.CanEvolve.Should().BeFalse(
            "ohne NextTierId ist Evolution nicht möglich");
    }

    [Fact]
    public void PlayerSkill_CanEvolve_TrueWennAllesErfuellt()
    {
        // Vorbereitung
        var skill = new Skill
        {
            Id = "test",
            MasteryRequired = 5,
            NextTierId = "test_v2"
        };
        var playerSkill = new PlayerSkill
        {
            Definition = skill,
            Mastery = 5, // genau an der Schwelle
            IsUnlocked = true
        };

        // Prüfung
        playerSkill.CanEvolve.Should().BeTrue(
            "CanEvolve muss true sein wenn alle Bedingungen erfüllt sind");
    }

    // ─── GetUnlockedSkills ────────────────────────────────────────────────────

    [Fact]
    public void GetUnlockedSkills_OhneInitialisierung_GibtLeereListe()
    {
        // Vorbereitung: Frischer Service ohne Klassen-Init
        var service = new SkillService();

        // Prüfung
        service.GetUnlockedSkills().Should().BeEmpty(
            "ohne InitializeForClass gibt es keine freigeschalteten Skills");
    }

    // ─── RestorePlayerSkills ─────────────────────────────────────────────────

    [Fact]
    public void RestorePlayerSkills_SetztMasteryStat()
    {
        // Vorbereitung: Skill im Service registrieren
        var skill = ErstelleSkill("sm_slash_1", ClassName.Swordmaster, masteryRequired: 10);
        var service = ErstelleServiceMitSkills(skill);

        // Ausführung: Laden aus Save-Daten
        service.RestorePlayerSkills(
            new Dictionary<string, int> { { "sm_slash_1", 7 } },
            new HashSet<string> { "sm_slash_1" });

        // Prüfung
        var ps = service.GetPlayerSkill("sm_slash_1");
        ps.Should().NotBeNull();
        ps!.Mastery.Should().Be(7, "gespeicherter Mastery-Wert muss wiederhergestellt werden");
        ps.IsUnlocked.Should().BeTrue("freigeschalteter Skill bleibt freigeschaltet");
    }
}
