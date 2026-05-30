using Avalonia.Controls;
using Avalonia.Threading;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests.Ui;

/// <summary>
/// Tests zum Compiled-Binding-DataContext-Verhalten des WhatsNewOverlay.
///
/// <para><b>Befund (per Headless-Test bewiesen):</b> Das WhatsNewOverlay-Muster — DataContext
/// und x:DataType auf GETRENNTE, verschachtelte Ebenen verteilt (aeusserer Border setzt
/// <c>DataContext="{Binding WhatsNewVm}"</c>, innerer Border setzt
/// <c>x:DataType="vm:WhatsNewViewModel"</c>) — loest in Avalonia 12 zur Laufzeit KORREKT auf.
/// Commands und Texte im inneren Bereich greifen, sowohl bei explizit gesetztem als auch bei
/// geerbtem DataContext. Der vermutete DataContext-Bug existiert NICHT — der eigentliche
/// WhatsNew-Anzeige-Defekt lag an fehlenden RESX-Keys (siehe WhatsNewServiceTests).</para>
///
/// <para>Diese Tests sichern das Verhalten ab, damit das funktionierende Muster nicht erneut
/// faelschlich "gefixt" wird (vgl. wirkungsloser Commit e89d38ee).</para>
/// </summary>
[Collection(HeadlessUiCollection.Name)]
public class DataContextPatternTests(HeadlessUiSession ui)
{
    private static Button LoadAndFindInnerButton(Control control)
    {
        var window = new Window { Content = control, Width = 240, Height = 120 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var button = control.FindControl<Button>("InnerButton");
        button.Should().NotBeNull("der innere Button muss im Visual-/Logical-Tree existieren");
        return button!;
    }

    [Fact]
    public void SplitDataContextAndDataType_InnerBindingsResolveCorrectly()
    {
        ui.OnUiThread(() =>
        {
            // Bildet das WhatsNewOverlay-Muster nach (getrennte DataContext/x:DataType-Ebenen).
            var outer = new OuterTestViewModel();
            var button = LoadAndFindInnerButton(new BrokenContextControl { DataContext = outer });

            // Avalonia 12 loest den inneren Sub-VM-Kontext korrekt auf → Command + Text greifen.
            button.Command.Should().NotBeNull(
                "Avalonia 12 loest das getrennte DataContext/x:DataType-Muster korrekt auf");
            (button.Content as string).Should().Be("Verstanden");

            button.Command!.Execute(null);
            outer.Inner.CommandExecuted.Should().BeTrue();
        });
    }

    [Fact]
    public void FixedPattern_RootDataTypeWithExternalDataContext_InnerBindingsResolve()
    {
        ui.OnUiThread(() =>
        {
            // Reproduziert das korrekte BottomTabBar-Muster (Fix-Vorlage).
            var outer = new OuterTestViewModel();
            var button = LoadAndFindInnerButton(new FixedContextControl { DataContext = outer.Inner });

            button.Command.Should().NotBeNull("der Control-Root-x:DataType + externer DataContext loest korrekt auf");
            (button.Content as string).Should().Be("Verstanden");

            button.Command!.Execute(null);
            outer.Inner.CommandExecuted.Should().BeTrue("der Button muss das Sub-VM-Command tatsaechlich ausloesen");
        });
    }

    [Fact]
    public void EmbeddedBrokenPattern_InheritedDataContext_InnerBindingsResolve()
    {
        ui.OnUiThread(() =>
        {
            // Repliziert den ECHTEN Einbettungsfall: BrokenContextControl erbt den DataContext
            // vom Host (analog WhatsNewOverlay, das MainViewModel von MainView erbt) — KEIN
            // explizit gesetzter DataContext am Overlay.
            var outer = new OuterTestViewModel();
            var host = new HostControl { DataContext = outer };
            var window = new Window { Content = host, Width = 240, Height = 120 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var button = host.FindControl<BrokenContextControl>("EmbeddedBroken")!
                .FindControl<Button>("InnerButton");
            button.Should().NotBeNull();
            button!.Command.Should().NotBeNull("auch bei geerbtem DataContext muss der Sub-VM-Wechsel greifen");
            (button.Content as string).Should().Be("Verstanden");
        });
    }
}
