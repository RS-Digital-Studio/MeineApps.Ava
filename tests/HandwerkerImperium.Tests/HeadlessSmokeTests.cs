using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HandwerkerImperium.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestAppBuilder))]

namespace HandwerkerImperium.Tests;

/// <summary>
/// P0.1 Layer 3 AAA-Audit (08.05.2026): Minimaler Avalonia-Headless-Smoke-Test.
///
/// Beweist, dass das Headless-Setup funktioniert. Zeigt Pattern für
/// zukünftige Critical-Path-Tests (siehe <see cref="CriticalPathHeadlessTests"/>):
/// - <see cref="AvaloniaTestApplicationAttribute"/> auf Assembly-Ebene
/// - <see cref="AvaloniaFactAttribute"/> statt <see cref="Xunit.FactAttribute"/>
/// - Ein simples <see cref="Window"/> rendern und auf Properties testen
///
/// Spätere Tests können zusätzlich App-DI mocken und MainWindow + Views laden.
/// </summary>
public class HeadlessSmokeTests
{
    [AvaloniaFact]
    public void Headless_Window_KannErzeugtUndGerendertWerden()
    {
        // Arrange + Act
        var window = new Window
        {
            Width = 320,
            Height = 240,
            Content = new TextBlock { Text = "HandwerkerImperium Headless Test" }
        };
        window.Show();

        // Assert
        window.IsVisible.Should().BeTrue();
        window.Content.Should().BeOfType<TextBlock>()
            .Which.Text.Should().Be("HandwerkerImperium Headless Test");

        // Cleanup
        window.Close();
    }

    [AvaloniaFact]
    public void Headless_StackPanel_LayoutTriggert()
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Children =
            {
                new TextBlock { Text = "Zeile 1" },
                new TextBlock { Text = "Zeile 2" },
            }
        };
        var window = new Window { Width = 320, Height = 240, Content = panel };
        window.Show();

        // Layout-Pass triggern
        window.UpdateLayout();

        panel.Children.Should().HaveCount(2);
        // Bounds sind nach Layout > 0
        panel.Bounds.Width.Should().BeGreaterThan(0);

        window.Close();
    }
}

/// <summary>
/// AppBuilder fuer Avalonia-Headless. Konfiguriert nur was fuer die Smoke-Tests
/// gebraucht wird. <c>UseHeadlessDrawing = true</c> nutzt den eingebauten Skia-loesen
/// Render-Stub (keine GPU, keine Bitmaps — perfekt fuer CI ohne Display-Server).
/// </summary>
public class HeadlessTestAppBuilder : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestAppBuilder>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    public override void Initialize()
    {
        // Minimal-Setup: kein FluentTheme noetig fuer Smoke-Tests.
        // Bei Critical-Path-Tests mit echten Views: Styles.Add(new FluentTheme());
    }
}
