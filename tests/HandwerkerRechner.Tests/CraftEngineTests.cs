using FluentAssertions;
using Xunit;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.Tests;

/// <summary>
/// Tests für die CraftEngine - die 19 Rechner-Funktionen.
/// Prüft Korrektheit, Grenzfälle und Fehlerbehandlung.
/// </summary>
public class CraftEngineTests
{
    private readonly CraftEngine _sut = new();

    #region Fliesen (CalculateTiles)

    [Fact]
    public void CalculateTiles_StandardRaum_GibtKorrekteFliesen()
    {
        // Vorbereitung: 4x5m Raum, 30x30cm Fliesen, 10% Verschnitt
        // Raumfläche: 20m², Fliesenfläche: 0.09m², Fliesen ohne Verschnitt: 223 (Ceiling)
        // Mit 10% Verschnitt: 245 (Ceiling)
        var ergebnis = _sut.CalculateTiles(4, 5, 30, 30, 10);

        ergebnis.RoomArea.Should().BeApproximately(20, 0.001);
        ergebnis.TileArea.Should().BeApproximately(0.09, 0.001);
        ergebnis.TilesNeeded.Should().Be((int)Math.Ceiling(20.0 / 0.09));
        ergebnis.TilesWithWaste.Should().BeGreaterThan(ergebnis.TilesNeeded);
        ergebnis.WastePercent.Should().Be(10);
    }

    [Fact]
    public void CalculateTiles_NullVerschnitt_TilesWithWasteGleichTilesNeeded()
    {
        // Vorbereitung: 0% Verschnitt → TilesWithWaste = TilesNeeded
        var ergebnis = _sut.CalculateTiles(3, 3, 50, 50, 0);

        ergebnis.TilesWithWaste.Should().Be(ergebnis.TilesNeeded);
    }

    [Fact]
    public void CalculateTiles_NegativerVerschnitt_WirdAufNullKorrigiert()
    {
        // Vorbereitung: Negativer Verschnitt darf kein negatives Ergebnis liefern
        var ergebnis = _sut.CalculateTiles(3, 3, 30, 30, -10);

        ergebnis.WastePercent.Should().Be(0);
        ergebnis.TilesWithWaste.Should().Be(ergebnis.TilesNeeded);
    }

    [Fact]
    public void CalculateTiles_SehrKleineFliesen_KeineNullDivision()
    {
        // Grenzfall: Extrem kleine Fliesen (1x1mm → 0.01x0.01cm)
        // CraftEngine schützt gegen Division durch 0
        var aktion = () => _sut.CalculateTiles(5, 5, 0.01, 0.01, 10);
        aktion.Should().NotThrow();
    }

    #endregion

    #region Tapete (CalculateWallpaper)

    [Fact]
    public void CalculateWallpaper_StandardRaum_GibtKorrekteRollen()
    {
        // Vorbereitung: 4x5m Raum, 2.5m hoch, Standard-Rolle (10.05m x 53cm)
        var ergebnis = _sut.CalculateWallpaper(4, 5, 2.5);

        // Prüfung: Umfang = 18m, Bahnen = Ceiling(18 / 0.53)
        ergebnis.Perimeter.Should().BeApproximately(18, 0.001);
        ergebnis.WallArea.Should().BeApproximately(18 * 2.5, 0.001);
        ergebnis.StripsNeeded.Should().Be((int)Math.Ceiling(18.0 / 0.53));
        ergebnis.RollsNeeded.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateWallpaper_MitRapport_BrauchtMehrRollen()
    {
        // Vorbereitung: Rapport erhöht den Materialverbrauch
        var ohneRapport = _sut.CalculateWallpaper(4, 4, 2.5, 10.05, 53, 0);
        var mitRapport = _sut.CalculateWallpaper(4, 4, 2.5, 10.05, 53, 64);

        mitRapport.RollsNeeded.Should().BeGreaterThanOrEqualTo(ohneRapport.RollsNeeded);
    }

    [Fact]
    public void CalculateWallpaper_OhneRapport_BeschnittZugabeReduziertBahnenProRolle()
    {
        // Handrechnung: Raumhöhe 2.50m + 0.10m Beschnitt-Zugabe = 2.60m effektive Bahnlänge
        // Bahnen/Rolle = Floor(10.05 / 2.60) = 3 (ohne Zugabe wären es fälschlich 4)
        // Raum 4x5m: Umfang 18m → Bahnen = Ceiling(18 / 0.53) = 34 → Rollen = Ceiling(34/3) = 12
        var ergebnis = _sut.CalculateWallpaper(4, 5, 2.5, 10.05, 53, 0);

        ergebnis.StripsPerRoll.Should().Be(3);
        ergebnis.StripsNeeded.Should().Be(34);
        ergebnis.RollsNeeded.Should().Be(12);
    }

    [Fact]
    public void CalculateWallpaper_RaumhoeheUeberRollenlaenge_EineRolleProBahn()
    {
        // Grenzfall: Raumhöhe 12m > Rollenlänge 10.05m → keine ganze Bahn pro Rolle.
        // Konservativ: eine Rolle pro Bahn. Raum 2x2m: Umfang 8m → Ceiling(8/0.53) = 16 Bahnen → 16 Rollen
        var ergebnis = _sut.CalculateWallpaper(2, 2, 12, 10.05, 53, 0);

        ergebnis.StripsNeeded.Should().Be(16);
        ergebnis.RollsNeeded.Should().Be(ergebnis.StripsNeeded);
    }

    [Fact]
    public void CalculateWallpaper_WandflaeacheIstPerimeterTimesHoehe()
    {
        // Invariante: WallArea = Perimeter * RoomHeight
        var ergebnis = _sut.CalculateWallpaper(5, 6, 3.0);

        ergebnis.WallArea.Should().BeApproximately(ergebnis.Perimeter * 3.0, 0.001);
    }

    #endregion

    #region Farbe (CalculatePaint)

    [Fact]
    public void CalculatePaint_ZweiAnstriche_DoppelteLiterMenge()
    {
        // Vorbereitung: 2 Anstriche bei 10m²/L → 20m² / 10 = 2L
        var ergebnis = _sut.CalculatePaint(10, 10, 2);

        ergebnis.LitersNeeded.Should().BeApproximately(2.0, 0.001);
        ergebnis.TotalArea.Should().BeApproximately(20, 0.001);
    }

    [Fact]
    public void CalculatePaint_EinAnstrich_HalbeSoVieLiterWieZwei()
    {
        var einAnstrich = _sut.CalculatePaint(20, 8, 1);
        var zweiAnstriche = _sut.CalculatePaint(20, 8, 2);

        zweiAnstriche.LitersNeeded.Should().BeApproximately(einAnstrich.LitersNeeded * 2, 0.01);
    }

    [Fact]
    public void CalculatePaint_ErgebnisBisAufEinZehntelGerundet()
    {
        // Vorbereitung: 15m² / 7m²/L = 2.142... → aufgerundet auf 0.1 = 2.2L
        var ergebnis = _sut.CalculatePaint(15, 7, 1);

        // Prüfung: Ergebnis ist ein Vielfaches von 0.1
        var rest = ergebnis.LitersNeeded % 0.1;
        rest.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculatePaint_NullDeckfaehigkeit_KeineNullDivision()
    {
        // Grenzfall: Engine schützt gegen Division durch 0
        var aktion = () => _sut.CalculatePaint(10, 0, 2);
        aktion.Should().NotThrow();
    }

    #endregion

    #region Dielen (CalculateFlooring)

    [Fact]
    public void CalculateFlooring_StandardRaum_GibtKorrekteDielen()
    {
        // Vorbereitung: 5x4m Raum, 2m lange / 12cm breite Dielen, 10% Verschnitt
        // Raumfläche: 20m², Dielenfläche: 2 * 0.12 = 0.24m²
        var ergebnis = _sut.CalculateFlooring(5, 4, 2, 12, 10);

        ergebnis.RoomArea.Should().BeApproximately(20, 0.001);
        ergebnis.BoardArea.Should().BeApproximately(0.24, 0.001);
        ergebnis.BoardsNeeded.Should().Be((int)Math.Ceiling(20.0 / 0.24));
        ergebnis.BoardsWithWaste.Should().BeGreaterThan(ergebnis.BoardsNeeded);
    }

    [Fact]
    public void CalculateFlooring_NullVerschnitt_BoardsWithWasteGleichBoardsNeeded()
    {
        var ergebnis = _sut.CalculateFlooring(4, 4, 1.5, 10, 0);

        ergebnis.BoardsWithWaste.Should().Be(ergebnis.BoardsNeeded);
    }

    #endregion

    #region Trockenbau (CalculateDrywall)

    [Fact]
    public void CalculateDrywall_StandardWand_GibtKorrekteWerte()
    {
        // Vorbereitung: 5m lange, 2.5m hohe Wand, einfach beplankt
        var ergebnis = _sut.CalculateDrywall(5, 2.5, false);

        ergebnis.WallArea.Should().BeApproximately(12.5, 0.001);
        ergebnis.Plates.Should().BeGreaterThan(0);
        ergebnis.CwProfiles.Should().BeGreaterThan(0);
        ergebnis.Screws.Should().Be(ergebnis.Plates * 25);
        ergebnis.IsDoublePlated.Should().BeFalse();
    }

    [Fact]
    public void CalculateDrywall_DoppeltBeplankt_ZweiMalSoVieleSchrauben()
    {
        var einfach = _sut.CalculateDrywall(4, 2.5, false);
        var doppelt = _sut.CalculateDrywall(4, 2.5, true);

        doppelt.Plates.Should().Be(einfach.Plates * 2);
        doppelt.Screws.Should().Be(doppelt.Plates * 25);
    }

    [Fact]
    public void CalculateBaseboard_MitTueren_ZiehtTuerbreitenAb()
    {
        // Vorbereitung: 20m Umfang, 2 Türen à 0.9m → 20 - 1.8 = 18.2m
        var ergebnis = _sut.CalculateBaseboard(20, 0.9, 2);

        ergebnis.Should().BeApproximately(18.2, 0.001);
    }

    [Fact]
    public void CalculateBaseboard_MehrTuerenAlsUmfang_GibtNull()
    {
        // Grenzfall: Summe der Türbreiten > Umfang → 0, kein negativer Wert
        var ergebnis = _sut.CalculateBaseboard(5, 3, 3);

        ergebnis.Should().Be(0);
    }

    #endregion

    #region Spannungsabfall (CalculateVoltageDrop)

    [Fact]
    public void CalculateVoltageDrop_KupferKabel_GibtKorrektenSpannungsabfall()
    {
        // Vorbereitung: 230V, 10A, 20m Kabel, 2.5mm² Kupfer
        // Formel: U = 2 * 10 * 20 * 0.0178 / 2.5 = 2.848V
        var ergebnis = _sut.CalculateVoltageDrop(230, 10, 20, 2.5, true);

        ergebnis.VoltageDrop.Should().BeApproximately(2.848, 0.001);
        ergebnis.PercentDrop.Should().BeApproximately((2.848 / 230) * 100, 0.001);
    }

    [Fact]
    public void CalculateVoltageDrop_AluKabelHoeherenWiderstand()
    {
        // Vorbereitung: Aluminium hat höheren Widerstand als Kupfer
        var kupfer = _sut.CalculateVoltageDrop(230, 16, 30, 4, true);
        var alu = _sut.CalculateVoltageDrop(230, 16, 30, 4, false);

        alu.VoltageDrop.Should().BeGreaterThan(kupfer.VoltageDrop);
    }

    [Fact]
    public void CalculateVoltageDrop_UnterDreiProzent_IsAcceptableTrue()
    {
        // Vorbereitung: Kurzes Kabel mit großem Querschnitt → unter 3% Abfall
        var ergebnis = _sut.CalculateVoltageDrop(230, 5, 5, 4, true);

        ergebnis.IsAcceptable.Should().BeTrue();
        ergebnis.PercentDrop.Should().BeLessThan(3);
    }

    [Fact]
    public void CalculateVoltageDrop_UeberDreiProzent_IsAcceptableFalse()
    {
        // Vorbereitung: Langes Kabel mit kleinem Querschnitt → über 3% Abfall
        var ergebnis = _sut.CalculateVoltageDrop(230, 20, 100, 1.5, true);

        ergebnis.IsAcceptable.Should().BeFalse();
        ergebnis.PercentDrop.Should().BeGreaterThan(3);
    }

    #endregion

    #region Stromkosten (CalculatePowerCost)

    [Fact]
    public void CalculatePowerCost_HerkoemmlicheLampe_BerechnetKosten()
    {
        // Vorbereitung: 60W, 8h/Tag, 0.30€/kWh
        // kWh/Tag = 0.06 * 8 = 0.48 → Kosten/Tag = 0.144€
        var ergebnis = _sut.CalculatePowerCost(60, 8, 0.30);

        ergebnis.KwhPerDay.Should().BeApproximately(0.48, 0.001);
        ergebnis.CostPerDay.Should().BeApproximately(0.144, 0.001);
        // Monat = Jahr/12 (konsistent: 12 × Monat == Jahr), nicht Tag × 30
        ergebnis.CostPerMonth.Should().BeApproximately(0.144 * 365 / 12, 0.001);
        ergebnis.CostPerYear.Should().BeApproximately(0.144 * 365, 0.001);
        // Konsistenz-Garantie: 12 Monate ergeben exakt ein Jahr
        (ergebnis.CostPerMonth * 12).Should().BeApproximately(ergebnis.CostPerYear, 0.001);
    }

    [Fact]
    public void CalculatePowerCost_NullWatt_AlleKostenNull()
    {
        var ergebnis = _sut.CalculatePowerCost(0, 24, 0.35);

        ergebnis.KwhPerDay.Should().Be(0);
        ergebnis.CostPerDay.Should().Be(0);
    }

    #endregion

    #region Ohmsches Gesetz (CalculateOhmsLaw)

    [Fact]
    public void CalculateOhmsLaw_SpannungUndStrom_BerechnetWiderstandUndLeistung()
    {
        // Vorbereitung: 12V, 2A → R = 6Ω, P = 24W
        var ergebnis = _sut.CalculateOhmsLaw(12, 2, null, null);

        ergebnis.Resistance.Should().BeApproximately(6, 0.001);
        ergebnis.Power.Should().BeApproximately(24, 0.001);
    }

    [Fact]
    public void CalculateOhmsLaw_SpannungUndWiderstand_BerechnetStromUndLeistung()
    {
        // Vorbereitung: 230V, 46Ω → I = 5A, P = 1150W
        var ergebnis = _sut.CalculateOhmsLaw(230, null, 46, null);

        ergebnis.Current.Should().BeApproximately(5, 0.001);
        ergebnis.Power.Should().BeApproximately(1150, 0.001);
    }

    [Fact]
    public void CalculateOhmsLaw_StromUndWiderstand_BerechnetSpannungUndLeistung()
    {
        // Vorbereitung: 3A, 10Ω → U = 30V, P = 90W
        var ergebnis = _sut.CalculateOhmsLaw(null, 3, 10, null);

        ergebnis.Voltage.Should().BeApproximately(30, 0.001);
        ergebnis.Power.Should().BeApproximately(90, 0.001);
    }

    #endregion

    #region Metallgewicht (CalculateMetalWeight)

    [Fact]
    public void CalculateMetalWeight_StahlRundstueck_GibtKorrektesMasse()
    {
        // Vorbereitung: Stahl, Rundstueck, 20mm Durchmesser, 1m Länge
        // Volumen = π * (0.01)² * 1 ≈ 0.000314m³
        // Gewicht = 0.000314 * 7850 ≈ 2.466kg
        var ergebnis = _sut.CalculateMetalWeight(MetalType.Steel, ProfileType.RoundBar, 1.0, 20);

        ergebnis.Weight.Should().BeApproximately(2.466, 0.01);
    }

    [Fact]
    public void CalculateMetalWeight_Kupfer_SchwerAlsAluminium()
    {
        // Vorbereitung: Gleiches Profil, aber unterschiedliches Material
        var aluminium = _sut.CalculateMetalWeight(MetalType.Aluminum, ProfileType.FlatBar, 1.0, 50, 10);
        var kupfer = _sut.CalculateMetalWeight(MetalType.Copper, ProfileType.FlatBar, 1.0, 50, 10);

        kupfer.Weight.Should().BeGreaterThan(aluminium.Weight);
    }

    #endregion

    #region Gewindekerndurchmesser (GetThreadDrill)

    [Fact]
    public void GetThreadDrill_BekanntesGewinde_GibtKorrektesKernloch()
    {
        // Vorbereitung: M6 → 5.0mm Kernlochbohrung
        var ergebnis = _sut.GetThreadDrill("M6");

        ergebnis.Found.Should().BeTrue();
        ergebnis.DrillSize.Should().Be(5.0);
        ergebnis.ThreadSize.Should().Be("M6");
    }

    [Fact]
    public void GetThreadDrill_UnbekanntesGewinde_GibtNichtGefunden()
    {
        var ergebnis = _sut.GetThreadDrill("M99");

        ergebnis.Found.Should().BeFalse();
        ergebnis.DrillSize.Should().Be(0);
    }

    [Fact]
    public void GetThreadDrill_Kleinbuchstabeneingabe_WirdGrossgeschrieben()
    {
        // Vorbereitung: Eingabe in Kleinbuchstaben muss trotzdem funktionieren
        var ergebnis = _sut.GetThreadDrill("m10");

        ergebnis.Found.Should().BeTrue();
        ergebnis.DrillSize.Should().Be(8.5);
    }

    #endregion

    #region Pflastersteine (CalculatePaving)

    [Fact]
    public void CalculatePaving_StandardPflaster_GibtKorrekteSteineAnzahl()
    {
        // Vorbereitung: 10m² Fläche, 20x10cm Steine, 3mm Fuge (= 0.3cm!)
        // SteinMitFuge: (20+0.3)/100 * (10+0.3)/100 = 0.203 * 0.103 = 0.020909m²
        var ergebnis = _sut.CalculatePaving(10, 20, 10, 3);

        ergebnis.Area.Should().Be(10);
        ergebnis.StonesNeeded.Should().Be((int)Math.Ceiling(10.0 / (0.203 * 0.103)));
        ergebnis.StonesWithReserve.Should().BeGreaterThan(ergebnis.StonesNeeded);
    }

    [Fact]
    public void CalculatePaving_FugeInMillimetern_HandgerechneterWert()
    {
        // Handrechnung: 20m², Stein 20x10cm, Fuge 3mm
        // Modul: 20.3 x 10.3 cm = 209.09 cm² = 0.020909 m²
        // 20 / 0.020909 = 956.52 → 957 Steine vor Reserve
        // Mit 5% Reserve: 956.52 * 1.05 = 1004.35 → 1005 Steine
        var ergebnis = _sut.CalculatePaving(20, 20, 10, 3);

        ergebnis.StonesNeeded.Should().Be(957);
        ergebnis.StonesWithReserve.Should().Be(1005);
    }

    [Fact]
    public void CalculatePaving_NegativeFugenbreite_WirdAufNullKorrigiert()
    {
        var aktion = () => _sut.CalculatePaving(10, 20, 10, -5);
        aktion.Should().NotThrow();

        var ergebnis = _sut.CalculatePaving(10, 20, 10, -5);
        ergebnis.StonesNeeded.Should().BeGreaterThan(0);
    }

    #endregion

    #region Erde/Mulch (CalculateSoil)

    [Fact]
    public void CalculateSoil_StandardBeet_GibtKorrektesVolumenUndSaecke()
    {
        // Vorbereitung: 5m² Beet, 20cm Tiefe, 40L Säcke
        // Volumen = 5 * 0.20 * 1000 = 1000L → 25 Säcke
        var ergebnis = _sut.CalculateSoil(5, 20, 40);

        ergebnis.VolumeLiters.Should().BeApproximately(1000, 0.001);
        ergebnis.BagsNeeded.Should().Be(25);
    }

    [Fact]
    public void CalculateSoil_GrossereSaecke_WenigerSaeckeBenoetigt()
    {
        var kleine = _sut.CalculateSoil(10, 15, 20);
        var grosse = _sut.CalculateSoil(10, 15, 40);

        grosse.BagsNeeded.Should().BeLessThanOrEqualTo(kleine.BagsNeeded);
    }

    #endregion

    #region Teichfolie (CalculatePondLiner)

    [Fact]
    public void CalculatePondLiner_StandardTeich_GibtKorrekteAbmessungen()
    {
        // Vorbereitung: 3x2m Teich, 1m tief, 0.5m Überstand
        // LinerLaenge = 3 + 2*1 + 2*0.5 = 6m
        // LinerBreite = 2 + 2*1 + 2*0.5 = 5m
        var ergebnis = _sut.CalculatePondLiner(3, 2, 1, 0.5);

        ergebnis.LinerLength.Should().BeApproximately(6, 0.001);
        ergebnis.LinerWidth.Should().BeApproximately(5, 0.001);
        ergebnis.LinerArea.Should().BeApproximately(30, 0.001);
    }

    #endregion

    #region Dachneigung (CalculateRoofPitch)

    [Fact]
    public void CalculateRoofPitch_45Grad_SteigeUndSpanngleich()
    {
        // Vorbereitung: Lauf = Steigung → 45 Grad
        var ergebnis = _sut.CalculateRoofPitch(1, 1);

        ergebnis.PitchDegrees.Should().BeApproximately(45, 0.001);
        ergebnis.PitchPercent.Should().BeApproximately(100, 0.001);
    }

    [Fact]
    public void CalculateRoofPitch_FlaeresDach_WenigerAlsSteileresDach()
    {
        var flach = _sut.CalculateRoofPitch(10, 1);
        var steil = _sut.CalculateRoofPitch(10, 5);

        steil.PitchDegrees.Should().BeGreaterThan(flach.PitchDegrees);
    }

    #endregion

    #region Dachziegel (CalculateRoofTiles)

    [Fact]
    public void CalculateRoofTiles_StandardDach_GibtKorrekteZiegel()
    {
        // Vorbereitung: 50m² Dach, 10 Ziegel/m²
        var ergebnis = _sut.CalculateRoofTiles(50, 10);

        ergebnis.TilesNeeded.Should().Be(500);
        ergebnis.TilesWithReserve.Should().Be((int)Math.Ceiling(500 * 1.05));
    }

    [Fact]
    public void CalculateRoofTiles_WithReserveIstFuenfProzentMehr()
    {
        var ergebnis = _sut.CalculateRoofTiles(100, 12);

        // Prüfung: Reserve ist mindestens 5% mehr
        ergebnis.TilesWithReserve.Should().BeGreaterThanOrEqualTo((int)Math.Ceiling(ergebnis.TilesNeeded * 1.05));
    }

    #endregion

    #region Beton (CalculateConcrete)

    [Fact]
    public void CalculateConcrete_Platte_BerechnetKorrektesMischverhaeltnis()
    {
        // Vorbereitung: 5x4m Platte, 10cm dick → 2m³ Beton
        var ergebnis = _sut.CalculateConcrete(0, 5, 4, 10, 25);

        ergebnis.VolumeM3.Should().BeApproximately(2, 0.001);
        ergebnis.CementKg.Should().BeApproximately(600, 0.001);    // 300kg/m³ * 2m³
        ergebnis.SandKg.Should().BeApproximately(1400, 0.001);     // 700kg/m³ * 2m³
        ergebnis.GravelKg.Should().BeApproximately(2200, 0.001);   // 1100kg/m³ * 2m³
        ergebnis.WaterLiters.Should().BeApproximately(300, 0.001); // 150L/m³ * 2m³
        // Trockenmischung 2100kg/m³: 2m³ * 2100 = 4200kg → 4200/25 = 168 Säcke à 25kg
        ergebnis.BagsNeeded.Should().Be(168);
    }

    [Fact]
    public void CalculateConcrete_Rundsaeule_NutztKreisformel()
    {
        // Vorbereitung: 30cm Durchmesser, 2m Höhe → π * 0.15² * 2 ≈ 0.1414m³
        var ergebnis = _sut.CalculateConcrete(2, 30, 0, 200, 25);

        var erwartetVolumen = Math.PI * 0.15 * 0.15 * 2;
        ergebnis.VolumeM3.Should().BeApproximately(erwartetVolumen, 0.001);
    }

    [Fact]
    public void CalculateConcrete_GroessereSaecke_WenigerSaecke()
    {
        var mit25kg = _sut.CalculateConcrete(0, 3, 3, 15, 25);
        var mit40kg = _sut.CalculateConcrete(0, 3, 3, 15, 40);

        mit40kg.BagsNeeded.Should().BeLessThanOrEqualTo(mit25kg.BagsNeeded);
    }

    #endregion

    #region Treppen (CalculateStairs)

    [Fact]
    public void CalculateStairs_StandardGeschosshoehe_DINKonform()
    {
        // Vorbereitung: 270cm Geschosshöhe (typisches Einfamilienhaus)
        var ergebnis = _sut.CalculateStairs(270, 100);

        ergebnis.StepCount.Should().BeGreaterThan(0);
        ergebnis.StepHeight.Should().BeApproximately(270.0 / ergebnis.StepCount, 0.001);
        // DIN 18065: Stufenhöhe 14-21cm
        ergebnis.StepHeight.Should().BeInRange(14, 21);
    }

    [Fact]
    public void CalculateStairs_ManuelleStufenanzahl_IgnoriertAutomatischeBerechnung()
    {
        // Vorbereitung: Explizit 18 Stufen vorgeben
        var ergebnis = _sut.CalculateStairs(300, 100, 18);

        ergebnis.StepCount.Should().Be(18);
    }

    [Fact]
    public void CalculateStairs_SchrittmassInKomfortablemBereich_IsComfortableTrue()
    {
        // Vorbereitung: 275cm Geschosshöhe → typisch 16 Stufen à 17.19cm
        // Schrittmaß ≈ 2*17.19 + Auftritt ≈ 62-63cm → komfortabel
        var ergebnis = _sut.CalculateStairs(275, 100);

        // Prüfung: Schrittmaß 59-65cm ist komfortabel laut DIN
        ergebnis.StepMeasure.Should().BeInRange(59, 65);
        ergebnis.IsComfortable.Should().BeTrue();
    }

    #endregion

    #region Putz (CalculatePlaster)

    [Fact]
    public void CalculatePlaster_Innenputz_BerechnetKorrektesMenge()
    {
        // Vorbereitung: 20m² Fläche, 15mm dick, Kalk-Zement-Innenputz (1.5 kg/m²/mm)
        // Gesamt = 20 * 15 * 1.5 = 450kg → 15 Säcke (à 30kg)
        var ergebnis = _sut.CalculatePlaster(20, 15, PlasterType.Interior);

        ergebnis.PlasterKg.Should().BeApproximately(450, 0.001);
        ergebnis.BagsNeeded.Should().Be(15);
    }

    [Fact]
    public void CalculatePlaster_Gipsputz_HandgerechneterWert()
    {
        // Handrechnung: 10m², 10mm dick, Gipsputz (0.85 kg/m²/mm, Knauf MP 75)
        // Gesamt = 10 * 10 * 0.85 = 85kg → Ceiling(85/30) = 3 Säcke
        var ergebnis = _sut.CalculatePlaster(10, 10, PlasterType.Gypsum);

        ergebnis.PlasterKg.Should().BeApproximately(85, 0.001);
        ergebnis.BagsNeeded.Should().Be(3);
    }

    [Fact]
    public void CalculatePlaster_Aussenputz_SchwerAlsInnenputz()
    {
        // Vorbereitung: Zementputz außen (1.7 kg/m²/mm) vs Innenputz (1.5 kg/m²/mm)
        var innen = _sut.CalculatePlaster(10, 10, PlasterType.Interior);
        var aussen = _sut.CalculatePlaster(10, 10, PlasterType.Exterior);

        aussen.PlasterKg.Should().BeGreaterThan(innen.PlasterKg);
    }

    [Fact]
    public void CalculatePlaster_GipsputzLeichtesterTyp()
    {
        // Gipsputz (0.85) ist leichter als Kalkputz (1.4) und Innenputz (1.5)
        var kalk = _sut.CalculatePlaster(10, 10, PlasterType.Lime);
        var gips = _sut.CalculatePlaster(10, 10, PlasterType.Gypsum);

        gips.PlasterKg.Should().BeLessThan(kalk.PlasterKg);
    }

    #endregion

    #region Estrich (CalculateScreed)

    [Fact]
    public void CalculateScreed_Zementestrich_BerechnetVolumenUndGewicht()
    {
        // Vorbereitung: 20m², 5cm dick, Zementestrich (2100 kg/m³)
        // Volumen = 20 * 0.05 = 1m³, Gewicht = 2100kg → 53 Säcke à 40kg
        var ergebnis = _sut.CalculateScreed(20, 5, ScreedType.Cement);

        ergebnis.VolumeM3.Should().BeApproximately(1.0, 0.001);
        ergebnis.WeightKg.Should().BeApproximately(2100, 0.001);
        ergebnis.BagsNeeded.Should().Be((int)Math.Ceiling(2100.0 / 40));
    }

    [Fact]
    public void CalculateScreed_TrocknungszeitBis40mm_EinTagProMm()
    {
        // Vorbereitung: 3cm = 30mm → 30 Tage Trocknungszeit
        var ergebnis = _sut.CalculateScreed(10, 3, ScreedType.Cement);

        ergebnis.DryingDays.Should().Be(30);
    }

    [Fact]
    public void CalculateScreed_TrocknungszeitUeber40mm_ZweiTageProMm()
    {
        // Vorbereitung: 5cm = 50mm → 40 + (10mm * 2) = 60 Tage
        var ergebnis = _sut.CalculateScreed(10, 5, ScreedType.Cement);

        ergebnis.DryingDays.Should().Be(60);
    }

    [Fact]
    public void CalculateScreed_Anhydrit_SchwererAlsZement()
    {
        // Vorbereitung: Anhydrit (2200 kg/m³) > Zement (2100 kg/m³)
        var zement = _sut.CalculateScreed(10, 4, ScreedType.Cement);
        var anhydrit = _sut.CalculateScreed(10, 4, ScreedType.Anhydrite);

        anhydrit.WeightKg.Should().BeGreaterThan(zement.WeightKg);
    }

    #endregion

    #region Dämmung (CalculateInsulation)

    [Fact]
    public void CalculateInsulation_EPS_BerechnetDaemmdickeUndPlatten()
    {
        // Vorbereitung: 20m², Ist-U=0.8, Soll-U=0.2, EPS (lambda=0.032)
        // Dicke = 0.032 * (1/0.2 - 1/0.8) = 0.032 * (5 - 1.25) = 0.032 * 3.75 = 0.12m = 12cm
        var ergebnis = _sut.CalculateInsulation(20, 0.8, 0.2, 0);

        ergebnis.Lambda.Should().Be(0.032);
        ergebnis.ThicknessCm.Should().BeApproximately(12, 1); // auf cm aufgerundet
        ergebnis.PiecesNeeded.Should().Be((int)Math.Ceiling(20.0 / 0.72));
    }

    [Fact]
    public void CalculateInsulation_XPSTeuerAlsEPS()
    {
        // Vorbereitung: XPS (15€/m²) teurer als EPS (8€/m²)
        var eps = _sut.CalculateInsulation(30, 0.8, 0.2, 0);
        var xps = _sut.CalculateInsulation(30, 0.8, 0.2, 1);

        xps.EstimatedTotalCost.Should().BeGreaterThan(eps.EstimatedTotalCost);
    }

    #endregion

    #region Leitungsquerschnitt (CalculateCableSize)

    [Fact]
    public void CalculateCableSize_Standardszenario_EmpfiehltKorrektenQuerschnitt()
    {
        // Vorbereitung: 16A, 25m Kabel, 230V, Kupfer, max. 3% Abfall
        var ergebnis = _sut.CalculateCableSize(16, 25, 230, 0, 3.0);

        ergebnis.RecommendedCrossSection.Should().BeGreaterThan(0);
        ergebnis.IsVdeCompliant.Should().BeTrue();
        // Empfohlener Querschnitt muss mindestens so groß wie Minimum sein
        ergebnis.RecommendedCrossSection.Should().BeGreaterThanOrEqualTo(ergebnis.MinCrossSection);
    }

    [Fact]
    public void CalculateCableSize_LaengererWegBrauchtGroesserenQuerschnitt()
    {
        // Vorbereitung: Kurze vs. lange Leitung bei gleichem Strom
        var kurz = _sut.CalculateCableSize(10, 10, 230, 0, 3.0);
        var lang = _sut.CalculateCableSize(10, 100, 230, 0, 3.0);

        lang.RecommendedCrossSection.Should().BeGreaterThanOrEqualTo(kurz.RecommendedCrossSection);
    }

    [Fact]
    public void CalculateCableSize_AluHatHoeherenWiderstand()
    {
        // Vorbereitung: Aluminium hat höheren spezifischen Widerstand als Kupfer
        var kupfer = _sut.CalculateCableSize(10, 20, 230, 0, 3.0);
        var alu = _sut.CalculateCableSize(10, 20, 230, 1, 3.0);

        alu.Resistivity.Should().BeGreaterThan(kupfer.Resistivity);
    }

    #endregion

    #region Plausibilitäts-Clamp (Infinity/NaN)

    [Fact]
    public void CalculateMethods_NaNUndInfinity_KeinCrashUndEndlicheErgebnisse()
    {
        // Projektregel (Models-CLAUDE.md): Infinity/NaN-Clamp IMMER vor Berechnungen.
        // Repräsentative Methoden mit Extrem-Inputs — kein Throw, keine Infinity/NaN-Ergebnisse.
        var nan = double.NaN;
        var inf = double.PositiveInfinity;

        var tapete = _sut.CalculateWallpaper(nan, inf, nan, inf, nan, inf);
        double.IsFinite(tapete.WallArea).Should().BeTrue();
        tapete.RollsNeeded.Should().BeGreaterThanOrEqualTo(0);

        var pflaster = _sut.CalculatePaving(inf, nan, inf, nan);
        pflaster.StonesNeeded.Should().BeGreaterThanOrEqualTo(0);

        var putz = _sut.CalculatePlaster(inf, nan, PlasterType.Interior);
        double.IsFinite(putz.PlasterKg).Should().BeTrue();

        var beton = _sut.CalculateConcrete(0, inf, nan, inf);
        double.IsFinite(beton.VolumeM3).Should().BeTrue();

        var strom = _sut.CalculatePowerCost(inf, nan, inf);
        double.IsFinite(strom.CostPerYear).Should().BeTrue();

        var fuge = CraftEngine.CalculateGrout(nan, inf, nan, inf, nan);
        fuge.TotalKg.Should().Be(0); // NaN → 0 → Ungültig-Guard → leeres Ergebnis
    }

    #endregion

    #region Fugenmasse (CalculateGrout)

    [Fact]
    public void CalculateGrout_StandardFliesen_GibtKorrektenFugenmassebedarf()
    {
        // Vorbereitung: 10m², 30x30cm Fliesen, 5mm Fugenbreite, 10mm Fugentiefe
        var ergebnis = CraftEngine.CalculateGrout(10, 30, 30, 5, 10, 2.5);

        ergebnis.AreaSqm.Should().Be(10);
        // Magnituden-Check gegen physikalischen Sollwert (deckt Einheiten-Fehler auf):
        // ((300+300)/(300*300)) * 5 * 10 * 1.6 = 0,533 kg/m²  →  10 m² * 0,533 = 5,33 kg
        ergebnis.ConsumptionPerSqm.Should().BeApproximately(0.533, 0.01);
        ergebnis.TotalKg.Should().BeApproximately(5.333, 0.05);
        // 10% Reserve muss eingerechnet sein
        ergebnis.TotalWithReserveKg.Should().BeApproximately(ergebnis.TotalKg * 1.1, 0.001);
        // 5,33 kg + 10% = 5,87 kg → 2 Eimer à 5 kg
        ergebnis.BucketsNeeded.Should().Be(2);
    }

    [Fact]
    public void CalculateGrout_NullWerte_GibtLeereResultat()
    {
        // Grenzfall: Ungültige Eingaben → leeres Ergebnis (kein Crash)
        var ergebnis = CraftEngine.CalculateGrout(0, 30, 30, 5, 10);

        ergebnis.TotalKg.Should().Be(0);
        ergebnis.BucketsNeeded.Should().Be(0);
    }

    [Fact]
    public void CalculateGrout_BrockenereFugen_MehrFugenmasse()
    {
        // Vorbereitung: Breitere Fugen → mehr Fugenmasse
        var schmal = CraftEngine.CalculateGrout(10, 30, 30, 3, 10);
        var breit = CraftEngine.CalculateGrout(10, 30, 30, 8, 10);

        breit.TotalKg.Should().BeGreaterThan(schmal.TotalKg);
    }

    [Fact]
    public void CalculateGrout_KleinereFliesenMehrFugenmasse()
    {
        // Vorbereitung: Kleinere Fliesen = mehr Fugenlänge = mehr Masse
        var gross = CraftEngine.CalculateGrout(10, 60, 60, 5, 10);
        var klein = CraftEngine.CalculateGrout(10, 20, 20, 5, 10);

        klein.TotalKg.Should().BeGreaterThan(gross.TotalKg);
    }

    #endregion

    #region Stundenrechner (CalculateHourlyRate)

    [Fact]
    public void CalculateHourlyRate_StandardTag_HandgerechneteWerte()
    {
        // Vorbereitung: 45 €/h, 8 h, 30 min Pause, 20% Aufschlag, 19% MwSt, 1 Mitarbeiter
        // Netto-Zeit: 8 - 0.5 = 7.5 h → Lohn: 337.50 € → Aufschlag: 67.50 €
        // Netto: 405.00 € → MwSt: 76.95 € → Brutto: 481.95 €
        var ergebnis = _sut.CalculateHourlyRate(45, 8, 30, 20, 19, 1);

        ergebnis.NetWorkHours.Should().BeApproximately(7.5, 0.001);
        ergebnis.NetLaborCost.Should().BeApproximately(337.50, 0.001);
        ergebnis.OverheadAmount.Should().BeApproximately(67.50, 0.001);
        ergebnis.TotalNet.Should().BeApproximately(405.00, 0.001);
        ergebnis.VatAmount.Should().BeApproximately(76.95, 0.001);
        ergebnis.TotalGross.Should().BeApproximately(481.95, 0.001);
    }

    [Fact]
    public void CalculateHourlyRate_ReihenfolgeAufschlagVorMwSt()
    {
        // Der Aufschlag wird auf die Lohnkosten gerechnet, die MwSt erst auf Netto+Aufschlag —
        // NICHT MwSt auf Lohnkosten und dann Aufschlag (ergäbe denselben Brutto-Betrag,
        // aber falsche Zwischensummen für Netto/MwSt-Ausweis).
        var ergebnis = _sut.CalculateHourlyRate(100, 10, 0, 10, 19, 1);

        ergebnis.NetLaborCost.Should().BeApproximately(1000, 0.001);
        ergebnis.TotalNet.Should().BeApproximately(ergebnis.NetLaborCost + ergebnis.OverheadAmount, 0.001);
        ergebnis.VatAmount.Should().BeApproximately(ergebnis.TotalNet * 0.19, 0.001);
        ergebnis.TotalGross.Should().BeApproximately(ergebnis.TotalNet + ergebnis.VatAmount, 0.001);
        // 1000 → +10% = 1100 → +19% = 1309
        ergebnis.TotalGross.Should().BeApproximately(1309.00, 0.001);
    }

    [Fact]
    public void CalculateHourlyRate_MehrereMitarbeiter_MultipliziertNettoZeit()
    {
        // 3 Mitarbeiter à (8 - 0.5) h = 22.5 h
        var ergebnis = _sut.CalculateHourlyRate(45, 8, 30, 0, 0, 3);

        ergebnis.NetWorkHours.Should().BeApproximately(22.5, 0.001);
        ergebnis.NetLaborCost.Should().BeApproximately(1012.50, 0.001);
    }

    [Fact]
    public void CalculateHourlyRate_PauseLaengerAlsArbeitszeit_AllesNull()
    {
        // Grenzfall: 2 h Arbeit, 180 min Pause → Netto-Zeit auf 0 begrenzt
        var ergebnis = _sut.CalculateHourlyRate(45, 2, 180, 20, 19, 1);

        ergebnis.NetWorkHours.Should().Be(0);
        ergebnis.TotalGross.Should().Be(0);
    }

    [Fact]
    public void CalculateHourlyRate_NaNUndInfinity_EndlicheErgebnisse()
    {
        // Projektregel: Infinity/NaN-Clamp vor Berechnungen
        var ergebnis = _sut.CalculateHourlyRate(double.NaN, double.PositiveInfinity, double.NaN, double.PositiveInfinity, double.NaN, 1);

        double.IsFinite(ergebnis.TotalGross).Should().BeTrue();
        double.IsFinite(ergebnis.NetWorkHours).Should().BeTrue();
    }

    #endregion

    #region Aufmaß-Flächen (CalculateShapeArea)

    [Fact]
    public void CalculateShapeArea_Rechteck_LaengeMalBreite()
    {
        // 5 × 4 = 20 m²
        _sut.CalculateShapeArea(AreaShape.Rectangle, 5, 4).Should().BeApproximately(20, 0.001);
    }

    [Fact]
    public void CalculateShapeArea_LForm_GesamtMinusAusschnitt()
    {
        // 5 × 4 = 20 minus Ausschnitt 2 × 1.5 = 3 → 17 m²
        _sut.CalculateShapeArea(AreaShape.LShape, 5, 4, 2, 1.5).Should().BeApproximately(17, 0.001);
    }

    [Fact]
    public void CalculateShapeArea_LForm_AusschnittGroesserAlsRechteck_GibtNull()
    {
        // Ausschnitt 10 × 10 = 100 > Rechteck 20 → Max(0, …) = 0
        _sut.CalculateShapeArea(AreaShape.LShape, 5, 4, 10, 10).Should().Be(0);
    }

    [Fact]
    public void CalculateShapeArea_TForm_MittelteilPlusQuerbalken()
    {
        // 5 × 4 = 20 plus Querbalken 2 × 1.5 = 3 → 23 m²
        _sut.CalculateShapeArea(AreaShape.TShape, 5, 4, 2, 1.5).Should().BeApproximately(23, 0.001);
    }

    [Fact]
    public void CalculateShapeArea_Trapez_MittelwertDerParallelseiten()
    {
        // (5 + 3) / 2 × 4 = 16 m²
        _sut.CalculateShapeArea(AreaShape.Trapezoid, 5, 4, dimension5: 3).Should().BeApproximately(16, 0.001);
    }

    [Fact]
    public void CalculateShapeArea_Dreieck_BasisMalHoeheHalbe()
    {
        // 5 × 4 / 2 = 10 m²
        _sut.CalculateShapeArea(AreaShape.Triangle, 5, 4).Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void CalculateShapeArea_Kreis_PiRQuadrat()
    {
        // Durchmesser 5 → r = 2.5 → π × 6.25 ≈ 19.635 m²
        _sut.CalculateShapeArea(AreaShape.Circle, 5).Should().BeApproximately(Math.PI * 2.5 * 2.5, 0.001);
    }

    [Theory]
    [InlineData(AreaShape.Rectangle, -5, 4, 0, 0, 0)]
    [InlineData(AreaShape.Rectangle, -5, -4, 0, 0, 0)] // zwei Negative dürfen keine positive Fläche ergeben
    [InlineData(AreaShape.LShape, 5, -4, 2, 1.5, 0)]
    [InlineData(AreaShape.LShape, 5, 4, -2, 1.5, 0)]   // negativer Ausschnitt → ungültig
    [InlineData(AreaShape.TShape, -5, 4, 2, 1.5, 0)]
    [InlineData(AreaShape.TShape, 5, 4, 2, -1.5, 0)]   // negativer Querbalken → ungültig
    [InlineData(AreaShape.Trapezoid, 5, 4, 0, 0, -3)]  // negative Parallelseite → ungültig
    [InlineData(AreaShape.Trapezoid, 0, 4, 0, 0, 3)]
    [InlineData(AreaShape.Triangle, 5, -4, 0, 0, 0)]
    [InlineData(AreaShape.Circle, -5, 0, 0, 0, 0)]
    [InlineData(AreaShape.Circle, 0, 0, 0, 0, 0)]
    public void CalculateShapeArea_NegativeOderNullEingaben_GibtNull(
        AreaShape form, double d1, double d2, double d3, double d4, double d5)
    {
        _sut.CalculateShapeArea(form, d1, d2, d3, d4, d5).Should().Be(0);
    }

    [Fact]
    public void CalculateShapeArea_NullAusschnitt_WieRechteck()
    {
        // Ausschnitt 0 × 0 ist gültig (L-Form ohne Ausschnitt = Rechteck)
        _sut.CalculateShapeArea(AreaShape.LShape, 5, 4, 0, 0).Should().BeApproximately(20, 0.001);
    }

    [Fact]
    public void CalculateShapeArea_NaNEingabe_GibtNull()
    {
        // NaN → Clamp auf 0 → Ungültig-Guard
        _sut.CalculateShapeArea(AreaShape.Rectangle, double.NaN, 4).Should().Be(0);
        _sut.CalculateShapeArea(AreaShape.Circle, double.NaN).Should().Be(0);
    }

    #endregion

    #region Material-Vergleich (CompareMaterialCosts)

    [Fact]
    public void CompareMaterialCosts_StandardVergleich_HandgerechneteWerte()
    {
        // A: 20 m² × 1.0/m² × 1.10 × 25 € = 550 €
        // B: 20 m² × 0.8/m² × 1.10 × 35 € = 616 €
        // Ersparnis: 66 € → 66/616 = 10.714 % — A günstiger
        var ergebnis = _sut.CompareMaterialCosts(20, 25, 1.0, 10, 35, 0.8, 10);

        ergebnis.TotalCostA.Should().BeApproximately(550, 0.001);
        ergebnis.TotalCostB.Should().BeApproximately(616, 0.001);
        ergebnis.SavingsAmount.Should().BeApproximately(66, 0.001);
        ergebnis.SavingsPercent.Should().BeApproximately(66.0 / 616.0 * 100, 0.001);
        ergebnis.IsACheaper.Should().BeTrue();
    }

    [Fact]
    public void CompareMaterialCosts_BGuenstiger_IsACheaperFalse()
    {
        var ergebnis = _sut.CompareMaterialCosts(20, 35, 1.0, 10, 25, 1.0, 10);

        ergebnis.IsACheaper.Should().BeFalse();
        ergebnis.TotalCostB.Should().BeLessThan(ergebnis.TotalCostA);
    }

    [Fact]
    public void CompareMaterialCosts_GleicheKosten_AGewinntErsparnisNull()
    {
        // Gleiche Kosten → A gilt als günstiger (<=), Ersparnis 0
        var ergebnis = _sut.CompareMaterialCosts(20, 25, 1.0, 10, 25, 1.0, 10);

        ergebnis.IsACheaper.Should().BeTrue();
        ergebnis.SavingsAmount.Should().Be(0);
        ergebnis.SavingsPercent.Should().Be(0);
    }

    [Fact]
    public void CompareMaterialCosts_VerschnittErhoehtKosten()
    {
        var ohne = _sut.CompareMaterialCosts(20, 25, 1.0, 0, 35, 0.8, 0);
        var mit = _sut.CompareMaterialCosts(20, 25, 1.0, 50, 35, 0.8, 50);

        mit.TotalCostA.Should().BeApproximately(ohne.TotalCostA * 1.5, 0.001);
        mit.TotalCostB.Should().BeApproximately(ohne.TotalCostB * 1.5, 0.001);
    }

    [Fact]
    public void CompareMaterialCosts_NaNUndInfinity_EndlicheErgebnisse()
    {
        var ergebnis = _sut.CompareMaterialCosts(double.NaN, double.PositiveInfinity, double.NaN,
            double.PositiveInfinity, double.NaN, double.PositiveInfinity, double.NaN);

        double.IsFinite(ergebnis.TotalCostA).Should().BeTrue();
        double.IsFinite(ergebnis.TotalCostB).Should().BeTrue();
        double.IsFinite(ergebnis.SavingsPercent).Should().BeTrue();
    }

    #endregion

    #region Abzugsflächen (CalculateOpeningsDeduction)

    [Fact]
    public void CalculateOpeningsDeduction_TuerenUndFenster_HandgerechneterWert()
    {
        // 1 Tür 0.8 × 2.0 = 1.6 m² + 2 Fenster 1.2 × 1.0 = 2.4 m² → 4.0 m²
        var ergebnis = _sut.CalculateOpeningsDeduction(1, 0.8, 2.0, 2, 1.2, 1.0);

        ergebnis.Should().BeApproximately(4.0, 0.001);
    }

    [Fact]
    public void CalculateOpeningsDeduction_KeineOeffnungen_GibtNull()
    {
        _sut.CalculateOpeningsDeduction(0, 0.8, 2.0, 0, 1.2, 1.0).Should().Be(0);
    }

    [Fact]
    public void CalculateOpeningsDeduction_NegativeMasse_ZaehlenAlsNull()
    {
        // Negative Breite/Höhe → 0 (kein negativer Abzug, der die Fläche vergrößern würde)
        _sut.CalculateOpeningsDeduction(1, -0.8, 2.0, 2, 1.2, 1.0).Should().BeApproximately(2.4, 0.001);
        // Negative Anzahl → 0
        _sut.CalculateOpeningsDeduction(-1, 0.8, 2.0, 0, 1.2, 1.0).Should().Be(0);
    }

    [Fact]
    public void CalculateOpeningsDeduction_NaNUndInfinity_EndlichesErgebnis()
    {
        var ergebnis = _sut.CalculateOpeningsDeduction(1, double.NaN, double.PositiveInfinity, 1, double.NaN, double.NaN);

        double.IsFinite(ergebnis).Should().BeTrue();
        ergebnis.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion
}
