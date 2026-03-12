using FluentAssertions;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using NSubstitute;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für das Spieler-Modell: Stat-Berechnung, Level-Up, Klassen-Unterschiede.
/// </summary>
public class PlayerModelTests
{
    // ─── Klassen-Initialwerte ─────────────────────────────────────────────────

    [Fact]
    public void Create_Schwertmeister_HatKorrekteBasisstats()
    {
        // Ausführung
        var spieler = Player.Create(ClassName.Swordmaster, "Held");

        // Prüfung: Klassen-Werte aus PlayerClass.Swordmaster
        spieler.Class.Should().Be(ClassName.Swordmaster);
        spieler.Level.Should().Be(1, "neuer Spieler startet auf Level 1");
        spieler.Hp.Should().Be(120, "Schwertmeister Basis-HP = 120");
        spieler.MaxHp.Should().Be(120, "Basis MaxHp");
        spieler.Atk.Should().Be(15, "Schwertmeister Basis-ATK = 15");
        spieler.Def.Should().Be(12, "Schwertmeister Basis-DEF = 12");
        spieler.Exp.Should().Be(0, "Startwert EXP = 0");
        spieler.FreeStatPoints.Should().Be(0, "keine freien Punkte am Start");
    }

    [Fact]
    public void Create_Arkanist_HatMehrManaAlsSchwertmeister()
    {
        var arkanist = Player.Create(ClassName.Arcanist);
        var schwertmeister = Player.Create(ClassName.Swordmaster);

        arkanist.MaxMp.Should().BeGreaterThan(schwertmeister.MaxMp,
            "Arkanist hat mehr MP als Schwertmeister");
    }

    [Fact]
    public void Create_Schattenklinke_HatHöhereGeschwindigkeit()
    {
        var schattenklinke = Player.Create(ClassName.Shadowblade);
        var schwertmeister = Player.Create(ClassName.Swordmaster);

        schattenklinke.Spd.Should().BeGreaterThan(schwertmeister.Spd,
            "Schattenklinke ist schneller als Schwertmeister");
    }

    [Fact]
    public void Create_SpielerName_WirdKorrektGesetzt()
    {
        var spieler = Player.Create(ClassName.Swordmaster, "Aria");

        spieler.Name.Should().Be("Aria", "Name muss korrekt gesetzt werden");
    }

    [Fact]
    public void Create_HpGleichMaxHp_NachErstellung()
    {
        var spieler = Player.Create(ClassName.Arcanist);

        spieler.Hp.Should().Be(spieler.MaxHp,
            "bei Erstellung muss HP = MaxHP sein (volle Gesundheit)");
        spieler.Mp.Should().Be(spieler.MaxMp,
            "bei Erstellung muss MP = MaxMP sein (volle Mana)");
    }

    // ─── Level-Up ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddExp_ExaktEXPZurmNaechstenLevel_LevelUpErfolgt()
    {
        // Vorbereitung
        var spieler = Player.Create(ClassName.Swordmaster);
        int benoetigteExp = spieler.ExpToNextLevel;
        int levelVorher = spieler.Level;

        // Ausführung: Genau die nötige EXP hinzufügen
        int levelUps = spieler.AddExp(benoetigteExp);

        // Prüfung
        levelUps.Should().Be(1, "genau 1 Level-Up muss stattgefunden haben");
        spieler.Level.Should().Be(levelVorher + 1, "Level muss um 1 gestiegen sein");
    }

    [Fact]
    public void AddExp_HeilungBeimLevelUp()
    {
        // Vorbereitung: Spieler beschädigt
        var spieler = Player.Create(ClassName.Swordmaster);
        spieler.Hp = spieler.MaxHp / 2; // Halb-HP

        // Ausführung: Level-Up
        spieler.AddExp(spieler.ExpToNextLevel);

        // Prüfung: Bei Level-Up wird HP vollständig geheilt
        spieler.Hp.Should().Be(spieler.MaxHp,
            "Level-Up muss HP vollständig heilen");
    }

    [Fact]
    public void AddExp_FreiePunkteNachLevelUp()
    {
        // Vorbereitung
        var spieler = Player.Create(ClassName.Swordmaster);
        int punkteVorher = spieler.FreeStatPoints;

        // Ausführung
        spieler.AddExp(spieler.ExpToNextLevel);

        // Prüfung: 3 Punkte pro Level-Up
        spieler.FreeStatPoints.Should().Be(punkteVorher + 3,
            "3 freie Stat-Punkte werden pro Level-Up vergeben");
    }

    [Fact]
    public void AddExp_MehrAlsEinLevel_MehrfachesLevelUp()
    {
        // Vorbereitung: Sehr viel EXP auf einmal
        var spieler = Player.Create(ClassName.Swordmaster);
        // EXP für mindestens 2 Level-Ups
        int vielExp = spieler.ExpToNextLevel * 5;

        // Ausführung
        int levelUps = spieler.AddExp(vielExp);

        // Prüfung
        levelUps.Should().BeGreaterThanOrEqualTo(2,
            "viel EXP auf einmal kann zu mehreren Level-Ups führen");
        spieler.Level.Should().BeGreaterThanOrEqualTo(3,
            "Level muss entsprechend gestiegen sein");
    }

    [Fact]
    public void AddExp_SchwertmeisterLevel2_ErhoehtATKPerLevel()
    {
        // Vorbereitung
        var spieler = Player.Create(ClassName.Swordmaster);
        int atkVorher = spieler.Atk;

        // Ausführung
        spieler.AddExp(spieler.ExpToNextLevel);

        // Prüfung: Schwertmeister +3 ATK/Level laut PlayerClass.Swordmaster
        spieler.Atk.Should().Be(atkVorher + 3,
            "Schwertmeister bekommt +3 ATK pro Level (AtkPerLevel=3)");
    }

    [Fact]
    public void AddExp_ArkanistLevel2_ErhoehtINTMehrAlsATK()
    {
        // Vorbereitung
        var arkanist = Player.Create(ClassName.Arcanist);
        int intVorher = arkanist.Int;
        int atkVorher = arkanist.Atk;

        // Ausführung
        arkanist.AddExp(arkanist.ExpToNextLevel);

        // Prüfung: Arkanist +4 INT/Level aber nur +1 ATK/Level
        (arkanist.Int - intVorher).Should().BeGreaterThan(arkanist.Atk - atkVorher,
            "Arkanist gewinnt pro Level mehr INT als ATK");
    }

    // ─── ExpToNextLevel-Formel ───────────────────────────────────────────────

    [Fact]
    public void ExpToNextLevel_Level1_Berechnet50()
    {
        // Formel: 50 * Level^1.5 = 50 * 1^1.5 = 50
        var spieler = Player.Create(ClassName.Swordmaster);

        spieler.ExpToNextLevel.Should().Be(50,
            "EXP für Level 2: 50 * 1^1.5 = 50");
    }

    [Fact]
    public void ExpToNextLevel_HöheresLevel_BrauchtMehrExp()
    {
        var spieler = Player.Create(ClassName.Swordmaster);
        spieler.Level = 10;
        int expFuerLevel10 = spieler.ExpToNextLevel;

        spieler.Level = 1;
        int expFuerLevel1 = spieler.ExpToNextLevel;

        expFuerLevel10.Should().BeGreaterThan(expFuerLevel1,
            "höheres Level benötigt mehr EXP für nächsten Aufstieg");
    }

    // ─── Stat-Allocation ─────────────────────────────────────────────────────

    // ─── Hilfsmethode für ProgressionService ─────────────────────────────────

    private static ProgressionService ErstelleProgressionService()
    {
        // GoldService benötigt IPreferencesService + IRewardedAdService → per Mock bereitstellen
        var mockPrefs = Substitute.For<IPreferencesService>();
        var mockAds = Substitute.For<IRewardedAdService>();
        return new ProgressionService(new SkillService(), new GoldService(mockPrefs, mockAds));
    }

    [Fact]
    public void AllocateStatPoint_KeineFreienPunkte_GibtFalse()
    {
        // Vorbereitung
        var spieler = Player.Create(ClassName.Swordmaster);
        var service = ErstelleProgressionService();

        spieler.FreeStatPoints.Should().Be(0, "frischer Spieler hat keine Punkte");

        // Ausführung
        bool erfolg = service.AllocateStatPoint(spieler, StatType.Atk);

        // Prüfung
        erfolg.Should().BeFalse("ohne freie Punkte schlägt Zuweisung fehl");
    }

    [Fact]
    public void AllocateStatPoint_FreiePunkte_ErhoehtStat()
    {
        // Vorbereitung
        var spieler = Player.Create(ClassName.Swordmaster);
        spieler.FreeStatPoints = 3;
        var service = ErstelleProgressionService();
        int atkVorher = spieler.Atk;

        // Ausführung
        bool erfolg = service.AllocateStatPoint(spieler, StatType.Atk);

        // Prüfung
        erfolg.Should().BeTrue("mit freien Punkten erfolgreich");
        spieler.Atk.Should().Be(atkVorher + 1, "ATK steigt um 1 pro Punkt");
        spieler.FreeStatPoints.Should().Be(2, "ein Punkt wurde verbraucht");
    }

    [Fact]
    public void AllocateStatPoint_HP_ErhoehtMaxHp()
    {
        // Vorbereitung
        var spieler = Player.Create(ClassName.Swordmaster);
        spieler.FreeStatPoints = 1;
        var service = ErstelleProgressionService();
        int maxHpVorher = spieler.MaxHp;

        // Ausführung
        service.AllocateStatPoint(spieler, StatType.Hp);

        // Prüfung: +5 MaxHp, Punkt verbraucht
        spieler.MaxHp.Should().Be(maxHpVorher + 5, "HP-Invest: +5 MaxHp");
        spieler.FreeStatPoints.Should().Be(0, "Punkt wurde verbraucht");
    }

    // ─── FullHeal ─────────────────────────────────────────────────────────────

    [Fact]
    public void FullHeal_HeilkomplettHPundMP()
    {
        // Vorbereitung: Spieler beschädigt
        var spieler = Player.Create(ClassName.Arcanist);
        spieler.Hp = 1;
        spieler.Mp = 0;

        // Ausführung
        spieler.FullHeal();

        // Prüfung
        spieler.Hp.Should().Be(spieler.MaxHp, "FullHeal heilt HP auf Maximum");
        spieler.Mp.Should().Be(spieler.MaxMp, "FullHeal stellt MP auf Maximum wieder her");
    }

    // ─── PrologHero ───────────────────────────────────────────────────────────

    [Fact]
    public void CreatePrologHero_IstLevel50()
    {
        var held = Player.CreatePrologHero();

        held.Level.Should().Be(50, "Prolog-Held ist Level 50");
        held.MaxHp.Should().BeGreaterThan(120, "Level 50 hat mehr HP als Level 1");
    }
}
