using Xunit;

namespace HandwerkerImperium.Tests;

/// <summary>
/// P0.1 Layer 3 AAA-Audit (08.05.2026): Skelett für tiefere Avalonia-Headless-Tests.
///
/// Smoke-Tests laufen live (siehe <see cref="HeadlessSmokeTests"/>). Diese Klasse
/// dokumentiert die nächsten Critical-Path-Szenarien, die **App-DI-Mocking** brauchen
/// (~80 Service-Interfaces). Aufwand pro Test: 30-60 Minuten Mock-Setup.
///
/// Bis diese Tests vollständig live sind, decken <see cref="SaveGameMigrationTests"/>
/// + <see cref="PrestigeCinematicRendererTests"/> + <see cref="DailyBundleServiceTests"/>
/// + 996 bestehende Service-Tests die Logik-Pipeline ab.
/// </summary>
public class CriticalPathHeadlessTests
{
    [Fact(Skip = "Skelett — Headless-Setup ist eigener 1W-Sprint (siehe AAA_AUDIT P0.1 Layer 3)")]
    public void AppStart_ShowsSplashThenDashboard()
    {
        // TODO Layer 3: Headless-AppBuilder mit Mock-Container
        Assert.Fail("Headless-Skelett: Implementierung bei Realisierung von Layer 3");
    }

    [Fact(Skip = "Skelett — siehe AAA_AUDIT P0.1 Layer 3")]
    public void FirstWorkshopHint_TriggersOnDashboard()
    {
        Assert.Fail("Headless-Skelett");
    }

    [Fact(Skip = "Skelett — siehe AAA_AUDIT P0.1 Layer 3")]
    public void AcceptOrder_CountdownPlaysMiniGame()
    {
        Assert.Fail("Headless-Skelett");
    }

    [Fact(Skip = "Skelett — siehe AAA_AUDIT P0.1 Layer 3")]
    public void DialogStack_PreventsMoreThanOneVisible()
    {
        Assert.Fail("Headless-Skelett");
    }

    [Fact(Skip = "Skelett — siehe AAA_AUDIT P0.1 Layer 3")]
    public void PrestigeReset_ShowsCinematicNotBottomSheet()
    {
        Assert.Fail("Headless-Skelett");
    }
}
