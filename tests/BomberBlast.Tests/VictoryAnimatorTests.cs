using BomberBlast.Graphics;
using BomberBlast.Models.Cosmetics;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für VictoryAnimator (Phase 29c).
/// Validiert dass alle 33 VictoryAnimationType-Werte gültige Frame-Daten liefern und
/// dass Skala/Rotation in plausiblen Grenzen bleiben.
/// </summary>
public class VictoryAnimatorTests
{
    [Fact]
    public void Identity_LiefertNeutraleTransformation()
    {
        var f = VictoryAnimator.VictoryFrame.Identity;
        f.ScaleX.Should().Be(1f);
        f.ScaleY.Should().Be(1f);
        f.Rotation.Should().Be(0f);
        f.OffsetY.Should().Be(0f);
        f.ParticleTrigger.Should().BeFalse();
    }

    [Fact]
    public void GetFrame_AlleTypen_LiefernPlausibleTransformation()
    {
        foreach (VictoryAnimationType type in Enum.GetValues<VictoryAnimationType>())
        {
            for (float t = 0; t <= 1f; t += 0.1f)
            {
                var f = VictoryAnimator.GetFrame(type, t);
                f.ScaleX.Should().BeInRange(0.5f, 2f, $"{type} @ t={t}");
                f.ScaleY.Should().BeInRange(0.5f, 2f, $"{type} @ t={t}");
                MathF.Abs(f.Rotation).Should().BeLessThan(1500f, $"{type} @ t={t}");
                MathF.Abs(f.OffsetY).Should().BeLessThan(60f, $"{type} @ t={t}");
            }
        }
    }

    [Fact]
    public void GetFrame_TimeAusserhalb01_WirdGeclamped()
    {
        var fNeg = VictoryAnimator.GetFrame(VictoryAnimationType.Wave, -0.5f);
        var fPos = VictoryAnimator.GetFrame(VictoryAnimationType.Wave, 1.5f);
        // Bei t=0 und t=1 sollten ähnliche Werte rauskommen
        fNeg.ScaleX.Should().BeApproximately(1f, 0.1f);
        fPos.ScaleX.Should().BeApproximately(1f, 0.1f);
    }

    [Fact]
    public void Spin_HatVollstaendigeRotation()
    {
        var f0 = VictoryAnimator.GetFrame(VictoryAnimationType.Spin, 0f);
        var f1 = VictoryAnimator.GetFrame(VictoryAnimationType.Spin, 1f);
        f0.Rotation.Should().Be(0f);
        f1.Rotation.Should().Be(360f);
    }

    [Fact]
    public void Backflip_RotiertRueckwaerts()
    {
        var f1 = VictoryAnimator.GetFrame(VictoryAnimationType.Backflip, 1f);
        f1.Rotation.Should().Be(-360f);
    }

    [Fact]
    public void ParticleTrigger_BeiStingerTypen_KannAusgeloestWerden()
    {
        // Bei FireDance, Tornado etc. sollte irgendwo ein Particle-Trigger feuern
        var anyTrigger = false;
        for (float t = 0; t <= 1f; t += 0.05f)
        {
            if (VictoryAnimator.GetFrame(VictoryAnimationType.FireDance, t).ParticleTrigger)
            {
                anyTrigger = true;
                break;
            }
        }
        anyTrigger.Should().BeTrue("FireDance muss Particle-Trigger über Animation hinweg feuern");
    }

    [Fact]
    public void GetFrame_AlleTypen_KeinNaN()
    {
        foreach (VictoryAnimationType type in Enum.GetValues<VictoryAnimationType>())
        {
            var f = VictoryAnimator.GetFrame(type, 0.5f);
            float.IsNaN(f.ScaleX).Should().BeFalse($"{type}: ScaleX ist NaN");
            float.IsNaN(f.ScaleY).Should().BeFalse($"{type}: ScaleY ist NaN");
            float.IsNaN(f.Rotation).Should().BeFalse($"{type}: Rotation ist NaN");
            float.IsNaN(f.OffsetY).Should().BeFalse($"{type}: OffsetY ist NaN");
        }
    }
}
