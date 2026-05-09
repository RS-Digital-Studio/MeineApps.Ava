using FluentAssertions;
using HandwerkerImperium.Graphics;

namespace HandwerkerImperium.Tests;

/// <summary>
/// P1.2 AAA-Audit Phase 1 (08.05.2026): CJK-Font-Resolver-Tests.
///
/// Diese Tests laufen auf dem CI-Runner — sie verifizieren NICHT, ob die System-Fonts
/// vorhanden sind (das hängt vom Runner ab), sondern dass der Resolver mindestens
/// einen Fallback liefert (kein <c>null</c>) und die Sprach-Erkennung sauber arbeitet.
/// </summary>
public class CjkFontResolverTests
{
    [Theory]
    [InlineData("zh-CN", true)]
    [InlineData("zh-Hans-CN", true)]
    [InlineData("zh-TW", true)]
    [InlineData("zh-Hant", true)]
    [InlineData("ja", true)]
    [InlineData("ja-JP", true)]
    [InlineData("ko", true)]
    [InlineData("ko-KR", true)]
    [InlineData("de", false)]
    [InlineData("en-US", false)]
    [InlineData("fr", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCjkLanguage_ErkenntNurCjkSprachen(string? culture, bool expected)
    {
        CjkFontResolver.IsCjkLanguage(culture).Should().Be(expected);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    [InlineData("ja-JP")]
    [InlineData("ko-KR")]
    public void Resolve_LiefertFallback_FuerCjkSprachen(string culture)
    {
        // Property: kein null bei CJK-Sprachen — minimum Default-Typeface als Fallback.
        var face = CjkFontResolver.Resolve(culture);
        face.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_LiefertNullFuerNonCjk()
    {
        CjkFontResolver.Resolve("de").Should().BeNull();
        CjkFontResolver.Resolve("en-US").Should().BeNull();
        CjkFontResolver.Resolve(null).Should().BeNull();
    }

    [Fact]
    public void AuditAvailableFaces_LiefertViereSprachen()
    {
        var audit = CjkFontResolver.AuditAvailableFaces();
        audit.Should().HaveCount(4);
        audit.Select(a => a.Lang).Should().Contain(["zh-CN", "zh-TW", "ja", "ko"]);
    }
}
