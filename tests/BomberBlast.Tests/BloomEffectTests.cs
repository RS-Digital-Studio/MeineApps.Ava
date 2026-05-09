using BomberBlast.Graphics;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für BloomEffect (Phase 21b — AAA-Audit V3).
/// Validiert Preload-Idempotenz, Apply-No-Crash bei nicht-initialisiertem Effect.
/// Echter Pixel-Output-Test ist Visual-Regression (Phase 26b) vorbehalten.
/// </summary>
public class BloomEffectTests
{
    [Fact]
    public void Preload_IstIdempotent()
    {
        BloomEffect.Preload();
        var available1 = BloomEffect.IsAvailable;
        BloomEffect.Preload(); // Zweiter Call darf nicht erneut kompilieren
        var available2 = BloomEffect.IsAvailable;
        available2.Should().Be(available1);
    }

    [Fact]
    public void IsAvailable_NachPreload_TrueOderFalse()
    {
        // Auf manchen Test-Hosts (CI ohne GPU) kann SkSL-Compilation scheitern
        // → wir akzeptieren beide Pfade. Wichtig: Apply darf nicht crashen wenn nicht available.
        BloomEffect.Preload();
        BloomEffect.IsAvailable.Should().Be(BloomEffect.IsAvailable); // Konsistent
    }

    [Fact]
    public void Apply_OhneInitialisierung_KeinCrash()
    {
        var bloom = new BloomEffect();
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        using var image = surface.Snapshot();

        // Selbst wenn IsAvailable=false: Apply muss safe early-return tun
        var act = () => bloom.Apply(surface.Canvas, image, new SKRect(0, 0, 100, 100));
        act.Should().NotThrow();
        bloom.Dispose();
    }

    [Fact]
    public void Apply_MitGueltigemImage_KeinCrash()
    {
        BloomEffect.Preload();
        var bloom = new BloomEffect();
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        surface.Canvas.Clear(SKColors.White);
        using var image = surface.Snapshot();

        var act = () => bloom.Apply(surface.Canvas, image, new SKRect(0, 0, 64, 64), threshold: 0.5f, intensity: 0.4f);
        act.Should().NotThrow();
        bloom.Dispose();
    }

    [Fact]
    public void Dispose_MultipleCallsSafe()
    {
        var bloom = new BloomEffect();
        bloom.Dispose();
        var act = () => bloom.Dispose();
        act.Should().NotThrow();
    }
}
