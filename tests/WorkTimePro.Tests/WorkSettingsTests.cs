using WorkTimePro.Models;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für WorkSettings: gesetzliche Pausenstaffel (§4 ArbZG) inkl. konfigurierbarer Schwelle.
/// </summary>
public class WorkSettingsTests
{
    private static WorkSettings ErstelleSettings(
        bool autoPause = true,
        double schwelleStunden = 6.0)
    {
        return new WorkSettings
        {
            AutoPauseEnabled = autoPause,
            AutoPauseAfterHours = schwelleStunden,
            AutoPauseMinutes = 30,
            AutoPauseMinutesOver9Hours = 45
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetRequiredPauseMinutes - Standardschwelle 6h
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetRequiredPauseMinutes_FuenfStunden_KeinePause()
    {
        var settings = ErstelleSettings();

        settings.GetRequiredPauseMinutes(5 * 60).Should().Be(0);
    }

    [Fact]
    public void GetRequiredPauseMinutes_GenauSechsStunden_KeinePause()
    {
        // §4 ArbZG: Pause erst bei MEHR als 6 Stunden
        var settings = ErstelleSettings();

        settings.GetRequiredPauseMinutes(6 * 60).Should().Be(0);
    }

    [Fact]
    public void GetRequiredPauseMinutes_SiebenStunden_DreissigMinuten()
    {
        var settings = ErstelleSettings();

        settings.GetRequiredPauseMinutes(7 * 60).Should().Be(30);
    }

    [Fact]
    public void GetRequiredPauseMinutes_NeuneinhalbStunden_FuenfundvierzigMinuten()
    {
        var settings = ErstelleSettings();

        settings.GetRequiredPauseMinutes((int)(9.5 * 60)).Should().Be(45);
    }

    [Fact]
    public void GetRequiredPauseMinutes_Deaktiviert_KeinePause()
    {
        var settings = ErstelleSettings(autoPause: false);

        settings.GetRequiredPauseMinutes(10 * 60).Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetRequiredPauseMinutes - Konfigurierte Schwelle über 9h
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetRequiredPauseMinutes_SchwelleZehnStunden_NeuneinhalbStundenKeinePause()
    {
        // Nutzer-Schwelle 10h: Unterhalb davon greift auch die 9h-Stufe NICHT
        var settings = ErstelleSettings(schwelleStunden: 10.0);

        settings.GetRequiredPauseMinutes((int)(9.5 * 60)).Should().Be(0);
    }

    [Fact]
    public void GetRequiredPauseMinutes_SchwelleZehnStunden_ZehneinhalbStundenFuenfundvierzig()
    {
        // Über der (höheren) Schwelle gilt direkt die >9h-Stufe (45 min)
        var settings = ErstelleSettings(schwelleStunden: 10.0);

        settings.GetRequiredPauseMinutes((int)(10.5 * 60)).Should().Be(45);
    }

    [Fact]
    public void GetRequiredPauseMinutes_SchwelleSechseinhalb_SiebenStundenDreissig()
    {
        var settings = ErstelleSettings(schwelleStunden: 6.5);

        settings.GetRequiredPauseMinutes(7 * 60).Should().Be(30);
    }
}
