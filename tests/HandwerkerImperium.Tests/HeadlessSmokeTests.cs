using Xunit;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Minimaler Avalonia-Headless-Smoke-Test — aktuell skip-only.
///
/// Hintergrund: Avalonia 12 hat <c>Avalonia.Headless.XUnit</c> auf xunit.v3 umgestellt,
/// der Rest unserer Test-Suite läuft noch auf xunit 2.x. Dual-Use kollidiert (CS0433).
/// Bis Headless-Critical-Path-Tests in einem eigenen xunit.v3-Test-Projekt extrahiert
/// sind, bleiben diese Smoke-Tests skip-only — Pattern siehe <see cref="CriticalPathHeadlessTests"/>.
/// </summary>
public class HeadlessSmokeTests
{
    [Fact(Skip = "Avalonia.Headless.XUnit braucht xunit.v3 — Migration in eigenem Test-Projekt offen")]
    public void Headless_Window_KannErzeugtUndGerendertWerden()
    {
        Assert.Fail("Headless-Smoke wartet auf xunit.v3-Migration");
    }

    [Fact(Skip = "Avalonia.Headless.XUnit braucht xunit.v3 — Migration in eigenem Test-Projekt offen")]
    public void Headless_StackPanel_LayoutTriggert()
    {
        Assert.Fail("Headless-Smoke wartet auf xunit.v3-Migration");
    }
}
