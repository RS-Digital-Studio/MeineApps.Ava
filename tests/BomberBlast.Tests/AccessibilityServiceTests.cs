using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für AccessibilityService (v2.0.44 — AAA-Audit).
/// Validiert Persistenz-Roundtrip, Event-Firing bei Property-Change,
/// ColorblindMatrix-Validation für alle 4 Modi, UiScale-Clamping.
/// </summary>
public class AccessibilityServiceTests
{
    [Fact]
    public void Initial_DefaultsSindKonservativ()
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);

        svc.ColorblindMode.Should().Be("Off");
        svc.HighContrast.Should().BeFalse();
        svc.UiScale.Should().Be(1.0);
        svc.SubtitlesEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetColorblindMode_FeuertEvent()
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);
        bool fired = false;
        svc.AccessibilityChanged += (_, _) => fired = true;

        svc.ColorblindMode = "Deuteranopia";

        fired.Should().BeTrue();
        svc.ColorblindMode.Should().Be("Deuteranopia");
    }

    [Fact]
    public void SetSelberWert_FeuertKeinEvent()
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);
        svc.HighContrast = true;
        bool fired = false;
        svc.AccessibilityChanged += (_, _) => fired = true;

        svc.HighContrast = true;  // Selbe Wert nochmal

        fired.Should().BeFalse("identischer Wert darf kein Event feuern (PropertyChanged-Convention)");
    }

    [Fact]
    public void Persistenz_ZweiteInstanzLiestVorherigeWerte()
    {
        var prefs = new InMemoryPreferences();
        var svc1 = new AccessibilityService(prefs);
        svc1.ColorblindMode = "Protanopia";
        svc1.HighContrast = true;
        svc1.UiScale = 1.25;
        svc1.SubtitlesEnabled = true;

        var svc2 = new AccessibilityService(prefs);

        svc2.ColorblindMode.Should().Be("Protanopia");
        svc2.HighContrast.Should().BeTrue();
        svc2.UiScale.Should().Be(1.25);
        svc2.SubtitlesEnabled.Should().BeTrue();
    }

    [Fact]
    public void UiScale_AusserhalbBereich_WirdGeclamped()
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);

        svc.UiScale = 0.1;
        svc.UiScale.Should().Be(0.75, "Min ist 0.75");

        svc.UiScale = 5.0;
        svc.UiScale.Should().Be(1.5, "Max ist 1.5");
    }

    [Theory]
    [InlineData("Deuteranopia")]
    [InlineData("Protanopia")]
    [InlineData("Tritanopia")]
    public void GetColorblindMatrix_AktivesModus_LiefertMatrix4x5(string mode)
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);
        svc.ColorblindMode = mode;

        var matrix = svc.GetColorblindMatrix();

        matrix.Should().NotBeNull();
        matrix!.Length.Should().Be(20, "4 Zeilen × 5 Spalten = ColorMatrix für SKColorFilter");
    }

    [Fact]
    public void GetColorblindMatrix_Off_LiefertNull()
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);

        svc.GetColorblindMatrix().Should().BeNull();
    }

    [Fact]
    public void GetColorblindMatrix_AlphaZeile_IstIdentitaet()
    {
        var prefs = new InMemoryPreferences();
        var svc = new AccessibilityService(prefs);
        svc.ColorblindMode = "Deuteranopia";

        var m = svc.GetColorblindMatrix()!;
        // Alpha-Row ist Index 15-19
        m[15].Should().Be(0f);
        m[16].Should().Be(0f);
        m[17].Should().Be(0f);
        m[18].Should().Be(1f, "Alpha pass-through");
        m[19].Should().Be(0f);
    }
}
