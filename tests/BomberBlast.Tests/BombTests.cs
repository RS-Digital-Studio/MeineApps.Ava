using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Bomben-Verhalten: Countdown, Explosion, Kettenreaktionen.
/// </summary>
public class BombTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    private static Player ErstelleTestSpieler(int gridX = 1, int gridY = 1)
    {
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        return new Player(x, y);
    }

    private static Bomb ErstelleBombe(Player spieler, int gridX = 3, int gridY = 3)
    {
        return Bomb.CreateAtGrid(gridX, gridY, spieler);
    }

    // ─── Countdown-Verhalten ─────────────────────────────────────────────────

    [Fact]
    public void Update_NachDefaultFuseTime_SollteExplosionAusloesen()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Ausführung: Genau die Zündschnur-Zeit abwarten
        bombe.Update(Bomb.DEFAULT_FUSE_TIME);

        // Prüfung
        bombe.ShouldExplode.Should().BeTrue(
            "nach Ablauf der Zündschnur muss die Bombe zünden");
    }

    [Fact]
    public void Update_NochNichtAbgelaufen_SollteNichtExplodieren()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Ausführung: Weniger als Zündschnur-Zeit
        bombe.Update(Bomb.DEFAULT_FUSE_TIME - 0.1f);

        // Prüfung
        bombe.ShouldExplode.Should().BeFalse(
            "Bombe sollte noch nicht explodieren wenn Timer nicht abgelaufen");
    }

    [Fact]
    public void Update_ManuelleDetonation_LäuftNichtRunter()
    {
        // Vorbereitung: Spieler mit Detonator-PowerUp
        var spieler = ErstelleTestSpieler();
        spieler.HasDetonator = true;
        var bombe = Bomb.CreateAtGrid(3, 3, spieler);

        // Ausführung: Weit mehr als normale Zündzeit
        bombe.Update(Bomb.DEFAULT_FUSE_TIME * 10f);

        // Prüfung: Manuelle Bombe zündet NICHT von selbst
        bombe.ShouldExplode.Should().BeFalse(
            "manuelle Bomben explodieren nur durch expliziten Trigger, nicht automatisch");
    }

    [Fact]
    public void IsAboutToExplode_BeiWenigerAlsHalberSekunde_GibtTrueZurueck()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Ausführung: Fast abgelaufen (0.4s verbleibend → < 0.5s Schwelle)
        bombe.Update(Bomb.DEFAULT_FUSE_TIME - 0.4f);

        // Prüfung
        bombe.IsAboutToExplode.Should().BeTrue(
            "Warnsignal muss bei weniger als 0.5s Restzeit aktiv sein");
    }

    [Fact]
    public void IsAboutToExplode_AmAnfang_GibtFalseZurueck()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Prüfung: Frisch platzierte Bombe nicht "about to explode"
        bombe.IsAboutToExplode.Should().BeFalse(
            "neue Bombe hat viel Restzeit und gibt kein Warnsignal");
    }

    // ─── Explode()-Methode ───────────────────────────────────────────────────

    [Fact]
    public void Explode_ErstesAufrufen_SetztHasExplodedUndDeaktiviertBombe()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        spieler.ActiveBombs = 1;
        var bombe = ErstelleBombe(spieler);

        // Ausführung
        bombe.Explode();

        // Prüfung
        bombe.HasExploded.Should().BeTrue("nach Explode() muss HasExploded true sein");
        bombe.IsActive.Should().BeFalse("explodierte Bombe ist nicht mehr aktiv");
        bombe.IsMarkedForRemoval.Should().BeTrue("explodierte Bombe wird aus der Szene entfernt");
    }

    [Fact]
    public void Explode_VerringertAktiveBombenDesEigentuemers()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        spieler.ActiveBombs = 3;
        var bombe = ErstelleBombe(spieler);

        // Ausführung
        bombe.Explode();

        // Prüfung: Eigentümer bekommt Slot zurück
        spieler.ActiveBombs.Should().Be(2,
            "nach Explosion wird ein Bombe-Slot des Eigentümers freigegeben");
    }

    [Fact]
    public void Explode_MehrfachAufgerufen_ExplodiertNurEinmal()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        spieler.ActiveBombs = 2;
        var bombe = ErstelleBombe(spieler);

        // Ausführung: Doppelter Aufruf
        bombe.Explode();
        bombe.Explode();

        // Prüfung: ActiveBombs nur einmal reduziert
        spieler.ActiveBombs.Should().Be(1,
            "Doppel-Explosion darf ActiveBombs nicht zweimal reduzieren");
    }

    [Fact]
    public void Explode_ActiveBombsNullKorruption_NieUnterNull()
    {
        // Vorbereitung: Edge Case - ActiveBombs bereits 0
        var spieler = ErstelleTestSpieler();
        spieler.ActiveBombs = 0;
        var bombe = ErstelleBombe(spieler);

        // Ausführung
        bombe.Explode();

        // Prüfung: Math.Max(0,...) schützt vor negativem Wert
        spieler.ActiveBombs.Should().Be(0,
            "ActiveBombs darf nie negativ werden (Math.Max-Schutz)");
    }

    // ─── Kettenreaktion ───────────────────────────────────────────────────────

    [Fact]
    public void TriggerChainReaction_SetztShouldExplodeAufTrue()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Ausführung
        bombe.TriggerChainReaction();

        // Prüfung
        bombe.ShouldExplode.Should().BeTrue(
            "TriggerChainReaction muss sofortige Explosion markieren");
    }

    // ─── Kick-Mechanik ───────────────────────────────────────────────────────

    [Fact]
    public void Kick_NormaleRichtung_SetztIsSliding()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Ausführung
        bombe.Kick(Direction.Right);

        // Prüfung
        bombe.IsSliding.Should().BeTrue("Kick startet Gleit-Bewegung");
        bombe.SlideDirection.Should().Be(Direction.Right, "Gleitrichtung muss korrekt sein");
        bombe.PlayerOnTop.Should().BeFalse("Kick hebt PlayerOnTop sofort auf");
    }

    [Fact]
    public void Kick_BereitsGleitend_IgnoriertZweitenKick()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);
        bombe.Kick(Direction.Right);

        // Ausführung: Zweiter Kick in andere Richtung
        bombe.Kick(Direction.Left);

        // Prüfung: Richtung bleibt beim ersten Kick
        bombe.SlideDirection.Should().Be(Direction.Right,
            "zweiter Kick wird ignoriert solange Bombe noch gleitet");
    }

    [Fact]
    public void StopSlide_SnapptAnZellenmitte()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler, 3, 3);
        bombe.Kick(Direction.Right);

        // Ausführung: Bombe stoppen
        bombe.StopSlide();

        // Prüfung: Position am Zellzentrum (Grid 3,3 → Pixel 3*32+16=112)
        float erwartetX = 3 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float erwartetY = 3 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        bombe.IsSliding.Should().BeFalse("StopSlide beendet Gleit-Bewegung");
        bombe.X.Should().BeApproximately(erwartetX, 0.01f, "X muss an Zellenmitte einrasten");
        bombe.Y.Should().BeApproximately(erwartetY, 0.01f, "Y muss an Zellenmitte einrasten");
    }

    // ─── ReduceFuse ──────────────────────────────────────────────────────────

    [Fact]
    public void ReduceFuse_NieUnterHalbeSekunde()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();
        var bombe = ErstelleBombe(spieler);

        // Ausführung: Extreme Reduzierung
        bombe.ReduceFuse(999f);

        // Prüfung: Minimum 0.5s (Sicherheits-Clamp)
        bombe.FuseTimer.Should().BeGreaterThanOrEqualTo(0.5f,
            "FuseTimer darf durch ReduceFuse nie unter 0.5s fallen");
    }

    // ─── Grid-Position ───────────────────────────────────────────────────────

    [Fact]
    public void CreateAtGrid_PositionEntsprichtZellzentrum()
    {
        // Vorbereitung
        var spieler = ErstelleTestSpieler();

        // Ausführung
        var bombe = Bomb.CreateAtGrid(5, 7, spieler);

        // Prüfung: Bombe zentriert in Zelle (5,7)
        float erwartetX = 5 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float erwartetY = 7 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        bombe.X.Should().BeApproximately(erwartetX, 0.01f);
        bombe.Y.Should().BeApproximately(erwartetY, 0.01f);
        bombe.GridX.Should().Be(5);
        bombe.GridY.Should().Be(7);
    }
}
