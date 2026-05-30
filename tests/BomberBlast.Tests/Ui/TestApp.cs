using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace BomberBlast.Tests.Ui;

/// <summary>
/// Minimale Avalonia-Application fuer Headless-UI-Tests. Laedt nur das Fluent-Theme,
/// damit Controls (Button etc.) ein Default-Template bekommen. App-spezifische
/// DynamicResources (SurfaceBrush, AccentBrush) sind fuer die Binding-Tests irrelevant
/// (fehlende Ressourcen → unset, kein Crash).
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}

/// <summary>
/// Entry-Point-Typ fuer <see cref="HeadlessUnitTestSession"/>. Die Session sucht per
/// Konvention die statische <c>BuildAvaloniaApp()</c>-Methode. Wir nutzen die Core-Headless-API
/// (nicht Avalonia.Headless.XUnit), weil dieses Paket xunit.v3 zieht und damit mit der
/// bestehenden xunit-v2-Suite kollidieren wuerde.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
