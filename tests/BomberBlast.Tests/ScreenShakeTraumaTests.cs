using BomberBlast.Graphics;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Trauma-Decay-Modell des ScreenShake (v2.0.44 — AAA-Audit).
/// Validiert: Trauma akkumuliert sich, klingt linear ab, Distanz-Skalierung wirkt,
/// Enabled=false ignoriert Trigger.
/// </summary>
public class ScreenShakeTraumaTests
{
    [Fact]
    public void Trigger_VerursachtAktivenShake()
    {
        var shake = new ScreenShake();

        shake.Trigger(intensity: 5f, duration: 0.3f);
        shake.Update(0.01f);

        shake.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Trigger_WhenDisabled_KeinShake()
    {
        var shake = new ScreenShake { Enabled = false };

        shake.Trigger(10f, 0.5f);
        shake.Update(0.01f);

        shake.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Update_TraumaKlingtAb_ShakeStoppt()
    {
        var shake = new ScreenShake();
        shake.Trigger(5f, 0.3f);

        // Decay = 1.5/s, intensity 5f → trauma 0.5 → nach ~0.34s sollte trauma 0 sein
        for (int i = 0; i < 60; i++)
        {
            shake.Update(0.0167f); // 60 fps
        }

        shake.IsActive.Should().BeFalse("Trauma sollte nach ~1s vollständig abgeklungen sein");
    }

    [Fact]
    public void TriggerAt_WeiteEntfernung_KleinererShake()
    {
        var farShake = new ScreenShake();
        var nearShake = new ScreenShake();

        // baseAmount 1.0 — bei distanceCells=0 max, bei distanceCells=8 wenig
        farShake.TriggerAt(1.0f, distanceCells: 8f, falloffCells: 4f);
        nearShake.TriggerAt(1.0f, distanceCells: 0f, falloffCells: 4f);

        farShake.Update(0.01f);
        nearShake.Update(0.01f);

        // Beide aktiv, aber far-shake sollte kleinere Offsets haben (statistisch über mehrere Frames)
        // Wir können nicht zuverlässig OffsetX direkt vergleichen wegen Random — aber Trauma per Reflection
        var traumaField = typeof(ScreenShake).GetField("_trauma",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var farTrauma = (float)traumaField!.GetValue(farShake)!;
        var nearTrauma = (float)traumaField.GetValue(nearShake)!;

        nearTrauma.Should().BeGreaterThan(farTrauma,
            "Distanz-Skalierung sollte den Shake bei großer Distanz reduzieren");
    }

    [Fact]
    public void AddTrauma_KlamptAuf1()
    {
        var shake = new ScreenShake();
        shake.AddTrauma(0.6f);
        shake.AddTrauma(0.6f);

        var traumaField = typeof(ScreenShake).GetField("_trauma",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var trauma = (float)traumaField!.GetValue(shake)!;

        trauma.Should().BeLessThanOrEqualTo(1.0f);
    }

    [Fact]
    public void Reset_StopptAktivenShake()
    {
        var shake = new ScreenShake();
        shake.Trigger(10f, 0.5f);
        shake.Update(0.01f);

        shake.Reset();

        shake.IsActive.Should().BeFalse();
        shake.OffsetX.Should().Be(0);
        shake.OffsetY.Should().Be(0);
    }

    [Fact]
    public void TriggerAt_FalloffNullSafe_KeinDivisionByZero()
    {
        var shake = new ScreenShake();
        var act = () => shake.TriggerAt(1.0f, distanceCells: 5f, falloffCells: 0f);

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_MultipleTriggers_TraumaSammeltSich()
    {
        var shake = new ScreenShake();
        shake.Trigger(2f, 0.1f);
        shake.Trigger(2f, 0.1f);
        shake.Trigger(2f, 0.1f);

        // Trauma sollte > 0.5 sein (jeder Trigger ~0.2 Trauma)
        var traumaField = typeof(ScreenShake).GetField("_trauma",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var trauma = (float)traumaField!.GetValue(shake)!;

        trauma.Should().BeGreaterThan(0.3f);
    }

    // === Phase 21 (V4) — Camera-Pull-Back-Tests =============================

    [Fact]
    public void TriggerPullBack_AktiviertPullBackFactor()
    {
        var shake = new ScreenShake();
        shake.PullBackFactor.Should().Be(1f);

        shake.TriggerPullBack(magnitude: 1.0f, durationSeconds: 0.4f);
        shake.Update(0.05f); // Kurze Zeit ins Smoothstep-In

        shake.PullBackFactor.Should().BeLessThan(1f, "Pull-Back senkt den Faktor unter 1.0");
        shake.PullBackFactor.Should().BeGreaterThan(0.84f, "Max-Reduktion ist 15% bei magnitude=1");
    }

    [Fact]
    public void TriggerPullBack_WhenDisabled_KeinEffekt()
    {
        var shake = new ScreenShake { Enabled = false };
        shake.TriggerPullBack(1.0f);
        shake.Update(0.1f);
        shake.PullBackFactor.Should().Be(1f);
    }

    [Fact]
    public void TriggerPullBack_RecoveryNachAblauf()
    {
        var shake = new ScreenShake();
        shake.TriggerPullBack(magnitude: 1.0f, durationSeconds: 0.3f);

        // Auf 100% Lifetime tickern
        for (int i = 0; i < 30; i++) shake.Update(0.01f);

        shake.PullBackFactor.Should().BeApproximately(1f, 0.05f, "nach Ablauf vollständige Recovery");
    }

    [Fact]
    public void TriggerPullBack_StaerkererGewinnt()
    {
        var shake = new ScreenShake();
        shake.TriggerPullBack(magnitude: 0.3f, durationSeconds: 0.4f);
        shake.TriggerPullBack(magnitude: 1.0f, durationSeconds: 0.5f); // staerker
        shake.Update(0.1f);

        // Magnitude * Duration = 0.5 (vs 0.12) sollte gewinnen
        // Pull-Back-Faktor sollte reduziert sein um etwa magnitude=1.0 entsprechend
        shake.PullBackFactor.Should().BeLessThan(0.97f);
    }

    [Fact]
    public void Reset_NulltAuchPullBack()
    {
        var shake = new ScreenShake();
        shake.TriggerPullBack(1.0f, 0.5f);
        shake.Update(0.05f);

        shake.Reset();

        shake.PullBackFactor.Should().Be(1f);
        shake.IsActive.Should().BeFalse();
    }
}
