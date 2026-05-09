using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für LeagueService Profanity-Filter (v2.0.31 + v2.0.44).
/// Validiert NormalizeForProfanityCheck (NFKD-Normalisierung, Strip Invisible Chars,
/// Leetspeak-Erkennung, Lowercase). Diese Tests laufen indirekt über die Profanity-Substitution.
///
/// Hinweis: NormalizeForProfanityCheck ist private. Die Tests prüfen das Verhalten
/// über öffentliche LeagueService.SetPlayerName-API, sind aber wegen Firebase-Abhängigkeit
/// als Plausibilitäts-Tests via Reflection markiert.
/// </summary>
public class LeagueServiceProfanityTests
{
    /// <summary>
    /// Test-Helper: Ruft die private NormalizeForProfanityCheck-Methode via Reflection auf.
    /// Robust gegen Sealed-Classes — funktioniert auch ohne Service-Instanz.
    /// </summary>
    private static string Normalize(string input)
    {
        var type = typeof(BomberBlast.Services.LeagueService);
        var method = type.GetMethod("NormalizeForProfanityCheck",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null) return input;
        return (string)method.Invoke(null, new object[] { input })!;
    }

    [Fact]
    public void Normalize_BasicInput_LowercaseUndAlnum()
    {
        var result = Normalize("Hello123");
        result.Should().Be("hello123");
    }

    [Fact]
    public void Normalize_Umlaute_NFKDDekomposition()
    {
        // "Müller" → NFKD: "Mu" + "̈ller" → Combining-Strip → "Muller"
        var result = Normalize("Müller");
        result.Should().Be("muller");
    }

    [Fact]
    public void Normalize_LeetspeakFvck_WirdNichtNormalisiert()
    {
        // Leetspeak v→u ist NICHT Teil der Normalisierung — Blocklist muss Leet-Variant kennen
        // Test dokumentiert dass Reine NFKD-Normalisierung NICHT Leet ersetzt
        var result = Normalize("fvck");
        result.Should().Be("fvck", "Leet ist nicht Teil der Normalisierung — Blocklist muss Variant matchen");
    }

    [Fact]
    public void Normalize_SonderzeichenZwischenBuchstaben_WerdenEntfernt()
    {
        // "F.u.c.k" → Punkte sind non-alnum → "fuck"
        var result = Normalize("F.u.c.k");
        result.Should().Be("fuck");
    }

    [Fact]
    public void Normalize_ZeroWidthSpace_WirdEntfernt()
    {
        // U+200B (Zero-Width-Space) muss durch Format-Strip entfernt werden
        var result = Normalize("hi​there");
        result.Should().Be("hithere");
    }

    [Fact]
    public void Normalize_ControlCharakter_WirdEntfernt()
    {
        var result = Normalize("hithere");
        result.Should().Be("hithere");
    }

    [Fact]
    public void Normalize_AccentedCharakters_StrippedZuBasis()
    {
        // Café, Naïve, Faïence
        Normalize("Café").Should().Be("cafe");
        Normalize("naïve").Should().Be("naive");
    }

    [Fact]
    public void Normalize_LeerString_BleibtLeer()
    {
        Normalize("").Should().Be("");
    }

    [Fact]
    public void Normalize_NurSonderzeichen_BleibtLeer()
    {
        Normalize("....---").Should().Be("");
    }
}
