using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für den Spieler: Statistiken, PowerUp-Aufnahme, Leben, Respawn.
/// </summary>
public class PlayerTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    private static Player ErstelleSpieler(int gridX = 1, int gridY = 1)
    {
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        return new Player(x, y);
    }

    private static PowerUp ErstellePowerUp(PowerUpType type, int gridX = 5, int gridY = 5)
    {
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        return new PowerUp(x, y, type);
    }

    // ─── Basis-Stats ─────────────────────────────────────────────────────────

    [Fact]
    public void NeuerSpieler_HatBasisstats()
    {
        // Vorbereitung + Ausführung
        var spieler = ErstelleSpieler();

        // Prüfung
        spieler.MaxBombs.Should().Be(1, "Startwert: 1 Bombe");
        spieler.FireRange.Should().Be(1, "Startwert: Reichweite 1");
        spieler.SpeedLevel.Should().Be(0, "Startwert: keine Speed-PowerUps");
        spieler.Lives.Should().Be(3, "Startwert: 3 Leben");
        spieler.Score.Should().Be(0, "Startwert: 0 Punkte");
        spieler.IsActive.Should().BeTrue("Spieler ist beim Start aktiv");
    }

    [Fact]
    public void Speed_OhneSpeedLevel_GibtBasisgeschwindigkeit()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();

        // Prüfung: BASE_SPEED = 80
        spieler.Speed.Should().Be(80f,
            "ohne Speed-PowerUp muss die Basis-Geschwindigkeit 80 betragen");
    }

    [Fact]
    public void Speed_MaxSpeedLevel_IsHoeher()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.SpeedLevel = 3; // Maximum

        // Prüfung: 80 + 3 * 20 = 140
        spieler.Speed.Should().Be(140f,
            "mit MaxSpeedLevel (3) muss Geschwindigkeit 140 betragen");
    }

    [Fact]
    public void Speed_MitSlowCurse_HalbierteGeschwindigkeit()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.SpeedLevel = 2; // 80 + 2*20 = 120 normal
        spieler.ActiveCurse = CurseType.Slow;

        // Prüfung: 120 * 0.5 = 60
        spieler.Speed.Should().Be(60f,
            "Slow-Fluch halbiert die Geschwindigkeit");
    }

    // ─── PowerUp-Sammeln ─────────────────────────────────────────────────────

    [Fact]
    public void CollectPowerUp_BombUp_ErhoehtMaxBombs()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        var powerUp = ErstellePowerUp(PowerUpType.BombUp);
        int vorher = spieler.MaxBombs;

        // Ausführung
        spieler.CollectPowerUp(powerUp);

        // Prüfung
        spieler.MaxBombs.Should().Be(vorher + 1, "BombUp erhöht MaxBombs um 1");
    }

    [Fact]
    public void CollectPowerUp_BombUpMaximum_LimitiertAuf10()
    {
        // Vorbereitung: Bereits am Maximum
        var spieler = ErstelleSpieler();
        spieler.MaxBombs = 10;

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.BombUp));

        // Prüfung: Kein Overflow über Maximum
        spieler.MaxBombs.Should().Be(10,
            "MaxBombs darf nicht über das Maximum von 10 steigen");
    }

    [Fact]
    public void CollectPowerUp_Fire_ErhoehtFireRange()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        int vorher = spieler.FireRange;

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.Fire));

        // Prüfung
        spieler.FireRange.Should().Be(vorher + 1, "Fire-PowerUp erhöht FireRange um 1");
    }

    [Fact]
    public void CollectPowerUp_FireMaximum_LimitiertAuf10()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.FireRange = 10;

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.Fire));

        // Prüfung
        spieler.FireRange.Should().Be(10, "FireRange darf nicht über 10 steigen");
    }

    [Fact]
    public void CollectPowerUp_Speed_ErhoehtSpeedLevel()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.Speed));

        // Prüfung
        spieler.SpeedLevel.Should().Be(1, "Speed-PowerUp erhöht SpeedLevel um 1");
    }

    [Fact]
    public void CollectPowerUp_Flamepass_SetztFlamepassFähigkeit()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.HasFlamepass.Should().BeFalse("vor PowerUp kein Flamepass");

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.Flamepass));

        // Prüfung
        spieler.HasFlamepass.Should().BeTrue("Flamepass-PowerUp aktiviert Flamepass");
    }

    [Fact]
    public void CollectPowerUp_Mystery_AktiviertUnverwundbarkeit()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.IsInvincible.Should().BeFalse("zu Beginn keine Unverwundbarkeit");

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.Mystery));

        // Prüfung
        spieler.IsInvincible.Should().BeTrue("Mystery-PowerUp aktiviert Unverwundbarkeit");
    }

    [Fact]
    public void CollectPowerUp_Skull_SetzteFluch()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.IsCursed.Should().BeFalse("vor PowerUp kein Fluch");

        // Ausführung
        spieler.CollectPowerUp(ErstellePowerUp(PowerUpType.Skull));

        // Prüfung
        spieler.IsCursed.Should().BeTrue("Skull aktiviert einen Fluch");
        spieler.ActiveCurse.Should().NotBe(CurseType.None, "Fluch muss gesetzt sein");
    }

    // ─── Bombe platzieren ────────────────────────────────────────────────────

    [Fact]
    public void CanPlaceBomb_NochSlotsFrei_GibtTrue()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.ActiveBombs = 0;

        // Prüfung
        spieler.CanPlaceBomb().Should().BeTrue("1 Slot frei → Bombe platzierbar");
    }

    [Fact]
    public void CanPlaceBomb_AlleSlotsBelegt_GibtFalse()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.ActiveBombs = spieler.MaxBombs; // Keine freien Slots

        // Prüfung
        spieler.CanPlaceBomb().Should().BeFalse("alle Slots belegt → keine Bombe platzierbar");
    }

    [Fact]
    public void CanPlaceBomb_ConstipationFluch_GibtFalse()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.ActiveCurse = CurseType.Constipation;

        // Prüfung
        spieler.CanPlaceBomb().Should().BeFalse(
            "Constipation-Fluch verhindert Bombenplatzierung");
    }

    [Fact]
    public void CanPlaceBomb_SpielerStirbt_GibtFalse()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.Kill();

        // Prüfung
        spieler.CanPlaceBomb().Should().BeFalse(
            "sterbender Spieler kann keine Bomben platzieren");
    }

    // ─── Tod und Respawn ─────────────────────────────────────────────────────

    [Fact]
    public void Kill_NormalesBedingungen_SetztIsDying()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();

        // Ausführung
        spieler.Kill();

        // Prüfung
        spieler.IsDying.Should().BeTrue("Kill() setzt IsDying auf true");
        spieler.IsActive.Should().BeFalse("sterbender Spieler ist nicht mehr aktiv");
    }

    [Fact]
    public void Kill_BereitsSterbend_IgnoriertZweitenKill()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.Kill();

        // Ausführung: Zweiter Kill-Aufruf
        spieler.Kill(); // Darf keinen Crash oder doppelten Tod auslösen

        // Prüfung: Zustand bleibt konsistent
        spieler.IsDying.Should().BeTrue("IsDying bleibt true nach Doppel-Kill");
    }

    [Fact]
    public void Kill_MitInvincibility_IgnoriertTod()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.ActivateInvincibility(5f);

        // Ausführung
        spieler.Kill();

        // Prüfung: Unverwundbar → kein Tod
        spieler.IsDying.Should().BeFalse(
            "unverwundbarer Spieler stirbt nicht");
        spieler.IsActive.Should().BeTrue("unverwundbarer Spieler bleibt aktiv");
    }

    [Fact]
    public void Respawn_SetztZustandZurueck()
    {
        // Vorbereitung: Spieler getötet
        var spieler = ErstelleSpieler();
        spieler.Kill();

        // Ausführung: Respawn an neuer Position
        spieler.Respawn(48f, 48f);

        // Prüfung
        spieler.IsDying.Should().BeFalse("nach Respawn nicht mehr sterbend");
        spieler.IsActive.Should().BeTrue("nach Respawn wieder aktiv");
        spieler.HasSpawnProtection.Should().BeTrue(
            "nach Respawn muss Spawn-Schutz aktiv sein");
        spieler.X.Should().Be(48f, "X-Position muss auf Respawn-Wert gesetzt sein");
    }

    [Fact]
    public void Respawn_VerliertNichtPermanentePowerUps()
    {
        // Vorbereitung: Permanente Stats gesetzt
        var spieler = ErstelleSpieler();
        spieler.MaxBombs = 5;
        spieler.FireRange = 4;
        spieler.Kill();

        // Ausführung
        spieler.Respawn(48f, 48f);

        // Prüfung: Permanente Stats bleiben erhalten
        spieler.MaxBombs.Should().Be(5, "MaxBombs bleibt nach Respawn erhalten");
        spieler.FireRange.Should().Be(4, "FireRange bleibt nach Respawn erhalten");
    }

    [Fact]
    public void Respawn_VerliertTemporaerePowerUps()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.HasFlamepass = true;
        spieler.HasWallpass = true;
        spieler.SpeedLevel = 2;
        spieler.Kill();

        // Ausführung
        spieler.Respawn(48f, 48f);

        // Prüfung: Temporäre PowerUps verloren
        spieler.HasFlamepass.Should().BeFalse("Flamepass geht beim Tod verloren");
        spieler.HasWallpass.Should().BeFalse("Wallpass geht beim Tod verloren");
        spieler.SpeedLevel.Should().Be(0, "SpeedLevel geht beim Tod verloren");
    }

    // ─── Invincibility-Timer ─────────────────────────────────────────────────

    [Fact]
    public void Update_InvincibilityLaeuftAb_SetztIsInvincibleZurueck()
    {
        // Vorbereitung
        var spieler = ErstelleSpieler();
        spieler.ActivateInvincibility(1f);

        // Ausführung: Timer ablaufen lassen
        spieler.Update(1.1f);

        // Prüfung
        spieler.IsInvincible.Should().BeFalse(
            "Unverwundbarkeit endet nach Ablauf des Timers");
    }

    [Fact]
    public void GridX_BerechnungKorrekt()
    {
        // Prüfung: Pixel → Grid-Koordinate
        var spieler = new Player(3 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f, 32f);
        spieler.GridX.Should().Be(3, "GridX berechnet sich aus X / CELL_SIZE (Floor)");
    }
}
