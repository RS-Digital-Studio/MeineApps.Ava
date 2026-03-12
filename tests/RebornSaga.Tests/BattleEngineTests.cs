using FluentAssertions;
using RebornSaga.Models.Enums;
using RebornSaga.Services;
using Xunit;

namespace RebornSaga.Tests;

/// <summary>
/// Tests für die BattleEngine: Schadensberechnung, Element-Kreislauf, Crits, Dodge.
/// </summary>
public class BattleEngineTests
{
    private readonly BattleEngine _engine = new();

    // ─── Grundlegende Schadensberechnung ─────────────────────────────────────

    [Fact]
    public void CalculateDamage_NormaleWerte_GibtPositivenSchaden()
    {
        // Ausführung: ATK 20, Multiplikator 1.0, DEF 5, kein Element, kein Crit-LUK
        var (schaden, _) = _engine.CalculateDamage(20, 1.0f, 5, null, null, 0);

        // Prüfung
        schaden.Should().BeGreaterThan(0, "Schaden muss immer positiv sein");
    }

    [Fact]
    public void CalculateDamage_MinimumSchaden_NieUnterEins()
    {
        // Prüfung: Edge Case - extrem hohe Def gegen schwachen Angriff
        // baseDmg = Max(5, (1 * 1.0) - (10000 * 0.5)) → Max(5, -4999.5) = 5
        // Dann * randMod → minimum 5 * 0.9 ≈ 4.5 → (int) = 4, aber Math.Max(1,...) sichert ab
        var (schaden, _) = _engine.CalculateDamage(1, 1.0f, 10000, null, null, 0);

        schaden.Should().BeGreaterThanOrEqualTo(1,
            "Schaden darf niemals unter 1 fallen (Math.Max-Schutz)");
    }

    [Fact]
    public void CalculateDamage_HoherMultiplikator_ErgibtMehrSchaden()
    {
        // Gleiches Setup, verschiedene Multiplikatoren
        // Viele Durchläufe für statistische Stabilität (±10% Zufall)
        int summeNiedrig = 0, summeHoch = 0;
        for (int i = 0; i < 100; i++)
        {
            var (s1, _) = _engine.CalculateDamage(20, 1.0f, 0, null, null, 0);
            var (s2, _) = _engine.CalculateDamage(20, 3.0f, 0, null, null, 0);
            summeNiedrig += s1;
            summeHoch += s2;
        }

        summeHoch.Should().BeGreaterThan(summeNiedrig,
            "höherer Multiplikator muss im Durchschnitt mehr Schaden ergeben");
    }

    // ─── Element-Kreislauf ────────────────────────────────────────────────────

    [Theory]
    [InlineData(Element.Fire, Element.Ice, "Feuer schlägt Eis")]
    [InlineData(Element.Ice, Element.Lightning, "Eis schlägt Blitz")]
    [InlineData(Element.Lightning, Element.Wind, "Blitz schlägt Wind")]
    [InlineData(Element.Wind, Element.Light, "Wind schlägt Licht")]
    [InlineData(Element.Light, Element.Dark, "Licht schlägt Dunkel")]
    [InlineData(Element.Dark, Element.Fire, "Dunkel schlägt Feuer")]
    public void CalculateDamage_ElementSchwaeche_GibtEinhalbfachenSchaden(
        Element angreifer, Element verteidiger, string grund)
    {
        // Statistischer Test über viele Durchläufe (Zufall eliminieren)
        int summeVorteil = 0, summeNeutral = 0;
        int atk = 50, def = 0;

        for (int i = 0; i < 200; i++)
        {
            var (sVorteil, _) = _engine.CalculateDamage(atk, 1f, def, angreifer, verteidiger, 0);
            var (sNeutral, _) = _engine.CalculateDamage(atk, 1f, def, null, null, 0);
            summeVorteil += sVorteil;
            summeNeutral += sNeutral;
        }

        float faktor = (float)summeVorteil / summeNeutral;
        faktor.Should().BeApproximately(1.5f, 0.1f,
            $"{grund}: Element-Schwäche muss ~1.5x Schaden ergeben");
    }

    [Theory]
    [InlineData(Element.Ice, Element.Fire, "Eis gegen Feuer ist schwach")]
    [InlineData(Element.Lightning, Element.Ice, "Blitz gegen Eis ist schwach")]
    [InlineData(Element.Wind, Element.Lightning, "Wind gegen Blitz ist schwach")]
    public void CalculateDamage_ElementResistenz_GibtHalbenSchaden(
        Element angreifer, Element verteidiger, string grund)
    {
        int summeResistenz = 0, summeNeutral = 0;
        int atk = 50, def = 0;

        for (int i = 0; i < 200; i++)
        {
            var (sResistenz, _) = _engine.CalculateDamage(atk, 1f, def, angreifer, verteidiger, 0);
            var (sNeutral, _) = _engine.CalculateDamage(atk, 1f, def, null, null, 0);
            summeResistenz += sResistenz;
            summeNeutral += sNeutral;
        }

        float faktor = (float)summeResistenz / summeNeutral;
        faktor.Should().BeApproximately(0.5f, 0.1f,
            $"{grund}: Resistenz muss ~0.5x Schaden ergeben");
    }

    [Fact]
    public void CalculateDamage_GleichesElement_GibtNeutralenSchaden()
    {
        // Feuer gegen Feuer: kein Bonus, kein Malus
        int summeGleich = 0, summeNeutral = 0;
        int atk = 50, def = 0;

        for (int i = 0; i < 200; i++)
        {
            var (sGleich, _) = _engine.CalculateDamage(atk, 1f, def, Element.Fire, Element.Fire, 0);
            var (sNeutral, _) = _engine.CalculateDamage(atk, 1f, def, null, null, 0);
            summeGleich += sGleich;
            summeNeutral += sNeutral;
        }

        float faktor = (float)summeGleich / summeNeutral;
        faktor.Should().BeApproximately(1.0f, 0.15f,
            "gleiches Element gegen gleiches Element muss neutralen Schaden ergeben");
    }

    [Fact]
    public void CalculateDamage_NullAngreiferElement_GibtNeutralenSchaden()
    {
        // Elementloser Angriff gegen Feuergegner: kein Bonus
        int summeNull = 0, summeNeutral = 0;

        for (int i = 0; i < 100; i++)
        {
            var (sNull, _) = _engine.CalculateDamage(30, 1f, 0, null, Element.Fire, 0);
            var (sNeutral, _) = _engine.CalculateDamage(30, 1f, 0, null, null, 0);
            summeNull += sNull;
            summeNeutral += sNeutral;
        }

        float faktor = (float)summeNull / summeNeutral;
        faktor.Should().BeApproximately(1.0f, 0.15f,
            "elementloser Angriff hat keinen Element-Modifikator");
    }

    // ─── Kritische Treffer ────────────────────────────────────────────────────

    [Fact]
    public void CalculateDamage_HohesLuk_KritischerTrefferWahrscheinlicher()
    {
        // LUK 200 → 100% Crit-Chance theoretisch
        int critsHochesLuk = 0, critsNiederesLuk = 0;
        int versuche = 200;

        for (int i = 0; i < versuche; i++)
        {
            var (_, critHoch) = _engine.CalculateDamage(20, 1f, 0, null, null, 200);
            var (_, critNiedrig) = _engine.CalculateDamage(20, 1f, 0, null, null, 0);

            if (critHoch) critsHochesLuk++;
            if (critNiedrig) critsNiederesLuk++;
        }

        critsHochesLuk.Should().BeGreaterThan(critsNiederesLuk,
            "hohes LUK muss zu mehr kritischen Treffern führen");
    }

    [Fact]
    public void CalculateDamage_KritischerTreffer_VerdoppeltSchaden()
    {
        // Deterministischer Test: Viele Durchläufe, kritische Treffer separieren
        int normalSchaden = 0, kritischSchaden = 0;
        int normalAnzahl = 0, kritischAnzahl = 0;

        // Sehr hohes LUK für viele Crits (LUK 200 = 100%)
        for (int i = 0; i < 500; i++)
        {
            var (s, isCrit) = _engine.CalculateDamage(50, 1f, 0, null, null, 200);
            if (isCrit)
            {
                kritischSchaden += s;
                kritischAnzahl++;
            }
        }

        // Mit LUK=0: Wenige/keine Crits
        for (int i = 0; i < 500; i++)
        {
            var (s, isCrit) = _engine.CalculateDamage(50, 1f, 0, null, null, 0);
            if (!isCrit)
            {
                normalSchaden += s;
                normalAnzahl++;
            }
        }

        if (kritischAnzahl > 0 && normalAnzahl > 0)
        {
            float kritDurchschnitt = (float)kritischSchaden / kritischAnzahl;
            float normalDurchschnitt = (float)normalSchaden / normalAnzahl;
            kritDurchschnitt.Should().BeApproximately(normalDurchschnitt * 2f, normalDurchschnitt * 0.3f,
                "kritische Treffer müssen ~2x normalen Schaden machen");
        }
    }

    // ─── Dodge-Berechnung ────────────────────────────────────────────────────

    [Fact]
    public void TryDodge_HohesDefenderSpd_HoehereDodgeChance()
    {
        int dodgesSchnell = 0, dodgesLangsam = 0;
        int versuche = 500;

        for (int i = 0; i < versuche; i++)
        {
            if (_engine.TryDodge(100, 5)) dodgesSchnell++;  // Verteidiger viel schneller
            if (_engine.TryDodge(5, 100)) dodgesLangsam++;  // Verteidiger viel langsamer
        }

        dodgesSchnell.Should().BeGreaterThan(dodgesLangsam,
            "hohe Verteidiger-Geschwindigkeit erhöht Dodge-Chance");
    }

    [Fact]
    public void TryDodge_DodgeChanceMaximum_NieÜber50Prozent()
    {
        // Selbst bei extremem Geschwindigkeitsunterschied: max 50%
        int dodges = 0;
        int versuche = 1000;

        for (int i = 0; i < versuche; i++)
        {
            if (_engine.TryDodge(99999, 0)) dodges++;
        }

        float rate = (float)dodges / versuche;
        rate.Should().BeLessThanOrEqualTo(0.55f,
            "Dodge-Chance darf nie signifikant über 50% liegen (Clamp bei 0.5)");
    }

    [Fact]
    public void TryDodge_GleicheGeschwindigkeit_BasischeDodgeChance()
    {
        // Gleiche SPD → dodgeChance = 0 * 0.02 + 0.1 = 10%
        int dodges = 0;
        int versuche = 1000;

        for (int i = 0; i < versuche; i++)
        {
            if (_engine.TryDodge(10, 10)) dodges++;
        }

        float rate = (float)dodges / versuche;
        rate.Should().BeApproximately(0.1f, 0.04f,
            "bei gleicher Geschwindigkeit sollte Dodge-Rate ~10% betragen");
    }

    // ─── CalculateDrops ───────────────────────────────────────────────────────

    [Fact]
    public void CalculateDrops_GegnerOhneDrops_GibtLeereListeZurueck()
    {
        // Vorbereitung
        var gegner = new Models.Enemy
        {
            Id = "test_enemy",
            NameKey = "enemy_test",
            Drops = null
        };

        // Ausführung
        var drops = _engine.CalculateDrops(gegner);

        // Prüfung
        drops.Should().BeEmpty("Gegner ohne Drop-Tabelle gibt leere Liste zurück");
    }

    [Fact]
    public void CalculateDrops_Drop100ProzentChance_ImmerGedroppt()
    {
        // Vorbereitung: Garantierter Drop
        var gegner = new Models.Enemy
        {
            Id = "test_enemy",
            NameKey = "enemy_test",
            Drops = new List<Models.EnemyDrop>
            {
                new() { ItemId = "item_001", Chance = 1.0f } // 100%
            }
        };

        // Ausführung: 10 Versuche - alle müssen droppen
        for (int i = 0; i < 10; i++)
        {
            var drops = _engine.CalculateDrops(gegner);
            drops.Should().Contain("item_001",
                "100%-Drop muss bei jedem Versuch erscheinen");
        }
    }

    [Fact]
    public void CalculateDrops_Drop0ProzentChance_NiemalsGedroppt()
    {
        // Vorbereitung: Unmöglicher Drop
        var gegner = new Models.Enemy
        {
            Id = "test_enemy",
            NameKey = "enemy_test",
            Drops = new List<Models.EnemyDrop>
            {
                new() { ItemId = "item_002", Chance = 0.0f } // 0%
            }
        };

        // Ausführung: 20 Versuche - kein einziger Drop
        for (int i = 0; i < 20; i++)
        {
            var drops = _engine.CalculateDrops(gegner);
            drops.Should().NotContain("item_002",
                "0%-Drop darf niemals erscheinen");
        }
    }
}
