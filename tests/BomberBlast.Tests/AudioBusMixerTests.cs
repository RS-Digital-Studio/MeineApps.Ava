using BomberBlast.Core.Audio;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für AudioBusMixer (Phase 16). Pure-Logic-Klasse — testbar ohne ISoundService.
/// Validiert Bus-Volume-Persistenz, Master-Multiplikation, Sidechain-Ducking + Recovery,
/// Event-Firing bei Volume-Änderungen.
/// </summary>
public class AudioBusMixerTests
{
    [Fact]
    public void DefaultVolumes_SindStudioPreset()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);

        mixer.GetBusVolume(AudioBus.Master).Should().Be(1.0f);
        mixer.GetBusVolume(AudioBus.Music).Should().Be(0.7f);
        mixer.GetBusVolume(AudioBus.Ambient).Should().Be(0.5f);
        mixer.GetBusVolume(AudioBus.Sfx).Should().Be(1.0f);
        mixer.GetBusVolume(AudioBus.Ui).Should().Be(0.85f);
        mixer.GetBusVolume(AudioBus.Voice).Should().Be(1.0f);
        mixer.GetBusVolume(AudioBus.Cinematic).Should().Be(1.0f);
    }

    [Fact]
    public void SetBusVolume_PersistiertInPreferences()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);

        mixer.SetBusVolume(AudioBus.Music, 0.42f);

        prefs.Get("Bus_Vol_Music", 0.0).Should().BeApproximately(0.42, 0.001);

        // Neu erzeugt: Wert wird wieder gelesen
        var mixer2 = new AudioBusMixer(prefs);
        mixer2.GetBusVolume(AudioBus.Music).Should().BeApproximately(0.42f, 0.001f);
    }

    [Fact]
    public void GetEffectiveVolume_MultipliziertBusMasterUndPlayVolume()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);
        mixer.SetBusVolume(AudioBus.Master, 0.5f);
        mixer.SetBusVolume(AudioBus.Music, 0.8f);

        // 0.8 (Music) * 0.5 (Master) * 1.0 (Duck=1) * 0.6 (Play) = 0.24
        mixer.GetEffectiveVolume(AudioBus.Music, 0.6f).Should().BeApproximately(0.24f, 0.001f);
    }

    [Fact]
    public void GetEffectiveVolume_Master_MultipliziertSichNichtSelbst()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);
        mixer.SetBusVolume(AudioBus.Master, 0.5f);

        // Master * 1.0 (kein Doppel-Master) * 1.0 (Duck) * 1.0 (Play) = 0.5
        mixer.GetEffectiveVolume(AudioBus.Master, 1.0f).Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void Duck_ReduziertEffectiveVolumeWaehrendDuration()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);
        // SetBusVolume(Music, 0.7f) ist Default — fixe Vergleichsgrundlage
        var beforeDuck = mixer.GetEffectiveVolume(AudioBus.Music, 1f);

        mixer.Duck(AudioBus.Music, 0.3f, 1.0f);
        // Trigger ohne Update: Multiplier ist noch 1.0 (Update tickert ihn auf Target)
        mixer.Update(0f);
        var afterDuck = mixer.GetEffectiveVolume(AudioBus.Music, 1f);

        afterDuck.Should().BeLessThan(beforeDuck);
        afterDuck.Should().BeApproximately(beforeDuck * 0.3f, 0.001f);
    }

    [Fact]
    public void Duck_RecoveryNachAblauf_LinearZuEins()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);

        mixer.Duck(AudioBus.Music, 0.3f, 0.5f);

        // 0.5s Duck-Phase
        mixer.Update(0.5f);
        // Recovery: 0.5s linear (deltaTime * 2f → 0.3 + 0.5*2 = 1.3 → clamp 1.0)
        mixer.Update(0.5f);

        // Nach kompletter Recovery: full 1.0
        mixer.GetEffectiveVolume(AudioBus.Music, 1f)
            .Should().BeApproximately(0.7f, 0.01f); // = busVolMusic*master*1.0 = 0.7
    }

    [Fact]
    public void DuckForCinematic_AffectsMusicAndAmbient()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);

        var musicBefore = mixer.GetEffectiveVolume(AudioBus.Music, 1f);
        var ambientBefore = mixer.GetEffectiveVolume(AudioBus.Ambient, 1f);

        mixer.DuckForCinematic();
        mixer.Update(0f);

        mixer.GetEffectiveVolume(AudioBus.Music, 1f).Should().BeLessThan(musicBefore);
        mixer.GetEffectiveVolume(AudioBus.Ambient, 1f).Should().BeLessThan(ambientBefore);
        // SFX bleibt unangetastet (Cinematic-Duck zielt nur auf Music+Ambient)
        mixer.GetEffectiveVolume(AudioBus.Sfx, 1f).Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void StaerkererDuck_GewinntGegenSchwaecheren()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);

        mixer.Duck(AudioBus.Music, 0.5f, 0.5f);
        mixer.Duck(AudioBus.Music, 0.2f, 0.5f); // staerker (kleinerer Multiplier)
        mixer.Update(0f);

        var eff = mixer.GetEffectiveVolume(AudioBus.Music, 1f);
        // Music-Default 0.7, Master 1.0, Duck 0.2 → 0.14
        eff.Should().BeApproximately(0.14f, 0.001f);
    }

    [Fact]
    public void BusVolumeChanged_FiredBeiVolumeAenderung()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);
        AudioBus? receivedBus = null;
        float receivedVolume = -1f;
        mixer.BusVolumeChanged += (bus, vol) =>
        {
            receivedBus = bus;
            receivedVolume = vol;
        };

        mixer.SetBusVolume(AudioBus.Sfx, 0.4f);

        receivedBus.Should().Be(AudioBus.Sfx);
        receivedVolume.Should().BeApproximately(0.4f, 0.001f);
    }

    [Fact]
    public void SetBusVolume_KeinDuplikat_FeuertNichtErneut()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);
        mixer.SetBusVolume(AudioBus.Sfx, 0.5f);
        var calls = 0;
        mixer.BusVolumeChanged += (_, _) => calls++;

        mixer.SetBusVolume(AudioBus.Sfx, 0.5f); // gleicher Wert
        calls.Should().Be(0);
    }

    [Fact]
    public void SetBusVolume_ClampedAuf0Bis1()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);

        mixer.SetBusVolume(AudioBus.Master, -0.5f);
        mixer.GetBusVolume(AudioBus.Master).Should().Be(0f);

        mixer.SetBusVolume(AudioBus.Master, 1.5f);
        mixer.GetBusVolume(AudioBus.Master).Should().Be(1f);
    }
}
