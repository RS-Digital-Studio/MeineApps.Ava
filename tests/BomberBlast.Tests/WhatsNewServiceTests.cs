using BomberBlast.Services;
using FluentAssertions;
using MeineApps.Core.Ava.Localization;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Regressionstests fuer den WhatsNew-Anzeige-Bug: Fehlende RESX-Keys duerfen NICHT als rohe
/// Key-IDs im Dialog erscheinen (LocalizationService.GetString liefert bei Miss den Key-Namen,
/// nicht null → das alte <c>?? "default"</c> war toter Code). Der Helper <c>L(key, default)</c>
/// faengt das ab. Zusaetzlich: Eintraege mit leeren Bullets werden nie ausgespielt.
/// </summary>
public class WhatsNewServiceTests
{
    /// <summary>
    /// Lokalisierungs-Mock, der das echte LocalizationService-Miss-Verhalten nachbildet:
    /// bei fehlendem Key wird der Key-NAME zurueckgegeben (nicht null).
    /// </summary>
    private static ILocalizationService MissingKeysLocalization()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.GetString(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
        return loc;
    }

    private static WhatsNewService Create(ILocalizationService loc, string lastSeen)
    {
        var prefs = new InMemoryPreferences();
        if (!string.IsNullOrEmpty(lastSeen))
            prefs.Set("WhatsNew_LastSeenVersion", lastSeen);
        return new WhatsNewService(prefs, loc);
    }

    [Fact]
    public void GetEntries_MissingResxKeys_FallsBackToDefaultText_NotRawKeyIds()
    {
        var svc = Create(MissingKeysLocalization(), lastSeen: "2.0.56");

        var entries = svc.GetEntries();

        entries.Should().NotBeEmpty();
        foreach (var entry in entries)
        {
            entry.Title.Should().NotStartWith("WhatsNew_",
                "bei fehlendem RESX-Key muss der Default-Text greifen, nicht die rohe Key-ID");
            entry.Bullets.Should().NotBeEmpty("Eintraege ohne Inhalt duerfen nicht ausgespielt werden");
            foreach (var bullet in entry.Bullets)
                bullet.Should().NotStartWith("WhatsNew_", "kein Bullet darf eine rohe Key-ID sein");
        }
    }

    [Fact]
    public void GetEntries_KeyPresentInResx_UsesLocalizedValue()
    {
        var loc = MissingKeysLocalization();
        // CurrentVersion stammt aus der Shared-Assembly (aktuell 2.0.62) — der zugehoerige
        // Titel-Key wird "getroffen" und muss den RESX-Wert (nicht den Default) verwenden.
        loc.GetString("WhatsNew_2_0_62_Title").Returns("Lokalisierter Titel");
        var svc = Create(loc, lastSeen: "2.0.56");

        svc.GetEntries().Should().Contain(e => e.Title == "Lokalisierter Titel");
    }

    [Fact]
    public void GetEntries_NeverContainsEntriesWithEmptyBullets()
    {
        var svc = Create(MissingKeysLocalization(), lastSeen: "2.0.10");

        svc.GetEntries().Should().OnlyContain(e => e.Bullets.Count > 0,
            "der offene Entwicklungs-Eintrag (leere Bullets) darf nie als leerer Dialog erscheinen");
    }

    [Fact]
    public void ShouldShow_FirstInstall_IsFalse()
    {
        var svc = Create(MissingKeysLocalization(), lastSeen: "");

        svc.ShouldShow.Should().BeFalse("bei Erstinstall (kein LastSeenVersion) wird kein Modal gezeigt");
    }

    [Fact]
    public void ShouldShow_UpdatingPlayer_IsTrue()
    {
        var svc = Create(MissingKeysLocalization(), lastSeen: "2.0.10");

        svc.ShouldShow.Should().BeTrue("ein Bestandsspieler mit aelterer Version muss das Modal sehen");
    }

    [Theory]
    [InlineData("2.0.62", "2.0.62", 0)]
    [InlineData("2.0.63", "2.0.62", 1)]
    [InlineData("2.0.57", "2.0.62", -1)]
    public void CompareVersions_OrdersCorrectly(string a, string b, int expectedSign)
    {
        Math.Sign(WhatsNewService.CompareVersions(a, b)).Should().Be(expectedSign);
    }
}
