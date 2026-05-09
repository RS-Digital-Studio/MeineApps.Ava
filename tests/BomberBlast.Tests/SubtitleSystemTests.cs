using BomberBlast.Graphics;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für SubtitleSystem (v2.0.46 — AAA-Audit Phase 3).
/// Validiert Show/Update-Lifecycle, Pool-Overflow-Verhalten (max 4 Captions),
/// Empty-String-Schutz, Clear-Funktion.
/// </summary>
public class SubtitleSystemTests
{
    [Fact]
    public void Initial_KeineAktivenCaptions()
    {
        using var sys = new SubtitleSystem();
        sys.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Show_ErhoehtActiveCount()
    {
        using var sys = new SubtitleSystem();
        sys.Show("[BOSS BRÜLLT]");

        sys.ActiveCount.Should().Be(1);
    }

    [Fact]
    public void Show_LeerString_WirdIgnoriert()
    {
        using var sys = new SubtitleSystem();
        sys.Show("");
        sys.Show("   ");
        sys.Show(null!);

        sys.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_DurationAbgelaufen_RemovedCaption()
    {
        using var sys = new SubtitleSystem();
        sys.Show("[TEST]", duration: 1.0f);
        sys.Update(0.5f);

        sys.ActiveCount.Should().Be(1);

        sys.Update(0.6f); // Total 1.1s — über Duration
        sys.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Show_VierCaptions_AlleAktiv()
    {
        using var sys = new SubtitleSystem();
        sys.Show("A");
        sys.Show("B");
        sys.Show("C");
        sys.Show("D");

        sys.ActiveCount.Should().Be(4);
    }

    [Fact]
    public void Show_FuenfteCaption_UeberschreibtAeltesteSlot()
    {
        using var sys = new SubtitleSystem();
        sys.Show("A", duration: 5.0f);
        sys.Show("B", duration: 5.0f);
        sys.Show("C", duration: 5.0f);
        sys.Show("D", duration: 5.0f);

        // Update senkt Lifetime aller, A am längsten existing → kürzeste Lifetime
        sys.Update(1.0f);
        sys.Show("E", duration: 5.0f); // sollte ältesten überschreiben

        sys.ActiveCount.Should().Be(4, "Pool bleibt bei max 4");
    }

    [Fact]
    public void Clear_EntferntAlleCaptions()
    {
        using var sys = new SubtitleSystem();
        sys.Show("A");
        sys.Show("B");
        sys.Show("C");

        sys.Clear();

        sys.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_MultipleFrames_LifecyclePerSlot()
    {
        using var sys = new SubtitleSystem();
        sys.Show("A", duration: 1.0f);
        sys.Show("B", duration: 2.0f);

        sys.Update(0.5f);
        sys.ActiveCount.Should().Be(2);

        sys.Update(0.6f); // A bei 1.1s — abgelaufen, B bei 1.1s — noch aktiv
        sys.ActiveCount.Should().Be(1);

        sys.Update(1.5f); // B bei 2.6s — abgelaufen
        sys.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_KannMehrfachAufgerufen()
    {
        var sys = new SubtitleSystem();
        sys.Dispose();
        var act = () => sys.Dispose();
        act.Should().NotThrow();
    }
}
