using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Findet hardcodierte User-facing-Strings in AXAML (Text=/Content=/Watermark=/Header=/ToolTip.Tip=)
/// und in C# (Toast/MessageBox/Notification-Strings ohne GetString()).
///
/// Heuristiken:
/// - Mindestlaenge 3 Zeichen
/// - Enthaelt Buchstaben-Sequenzen (≥ 3 Buchstaben)
/// - Keine reinen Zahlen, Hex-Codes, Pfade, URLs
/// - Keine Binding-/Resource-Referenzen ({Binding}, {x:Static}, {StaticResource}, {DynamicResource})
/// - Keine bekannten technischen Werte (Boolean, Enum-Werte, Spacing-Tokens)
///
/// Befunde sind INFO/WARN, KEIN FAIL (Heuristik kann False-Positives liefern).
/// `// AppChecker:ignore` unterdrueckt einzelne Treffer.
/// </summary>
class HardcodedStringChecker : IChecker
{
    public string Category => "Hardcoded-Strings";

    // Properties die User-facing Text enthalten
    static readonly string[] UserFacingProps =
    [
        "Text", "Content", "Watermark", "Header",
        "ToolTip.Tip", "ContentDescription", "PlaceholderText",
        "Title", "Subtitle", "Description"
    ];

    // Werte die KEINE User-facing Strings sind (Enum, Boolean, Spacing, etc.)
    static readonly HashSet<string> AllowedShortValues = new(StringComparer.OrdinalIgnoreCase)
    {
        // Avalonia/XAML Enums
        "True", "False", "None", "Auto", "Stretch", "Center", "Left", "Right",
        "Top", "Bottom", "Visible", "Hidden", "Collapsed", "OK", "Cancel",
        "Yes", "No", "Light", "Dark", "Default", "Normal", "Bold", "Italic",
        "All", "Any", "Both", "Inherit", "Wrap", "NoWrap", "WrapWithOverflow",
        "Horizontal", "Vertical", "Disabled", "Enabled", "Selected", "Unselected",
        // Sprach-Eigenbezeichnungen (NICHT lokalisieren - "Deutsch" heisst auch auf englisch "Deutsch")
        "Deutsch", "English", "Español", "Espanol", "Français", "Francais",
        "Italiano", "Português", "Portugues",
        // Math/Calculator-Notation
        "INV", "Ans", "EXP", "LOG", "LN", "SIN", "COS", "TAN",
        // Brand-/App-Namen (eigene Apps)
        "RechnerPlus", "ZeitManager", "FinanzRechner", "FitnessRechner",
        "HandwerkerRechner", "WorkTimePro", "HandwerkerImperium",
        "BomberBlast", "RebornSaga", "BingXBot", "GardenControl", "SmartMeasure",
        // Soziale/Brand-Begriffe
        "Discord", "Twitter", "Reddit", "GitHub", "GitLab", "AdMob", "AdSense"
    };

    // Properties die NIE auf "Hardcoded String" hinweisen muessen
    static readonly HashSet<string> AlwaysAllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        // MainWindow.Title ist App-Name (typisch nicht lokalisiert)
        // → kommt vorher als Sonderfall, siehe unten
    };

    // Pattern fuer Hardcoded-Properties in XAML: Property="Wert" (NICHT {Binding}/{StaticResource}/etc.)
    // Dynamisch zusammengebaut weil ToolTip.Tip Punkt enthaelt
    static readonly Regex AttributePattern = new(
        @"(?<prop>[A-Z][A-Za-z]+(?:\.[A-Z][a-z]+)?)\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled);

    // Unicode-Symbole als UI-Text (Icon-Strategie verbietet sie): Arrows (2190-21FF), Technical (2300-23FF),
    // Geometric Shapes (25A0-25FF), Misc Symbols (2600-26FF), Dingbats (2700-27BF), Misc Symbols & Arrows
    // (2B00-2BFF). Bullet U+2022 bewusst NICHT erfasst (Listen-Bullets sind legitim).
    static readonly Regex UiSymbolRegex = new(
        @"[←-⇿⌀-⏿■-◿☀-⛿✀-➿⬀-⯿]",
        RegexOptions.Compiled);

    // C# Toast/MessageBox-Patterns
    static readonly Regex CsToastPattern = new(
        @"(MessageRequested\?\.Invoke|ShowMessage|ShowToast|FloatingTextRequested\?\.Invoke|Toast\.MakeText|NotificationManager\.Show)\s*\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        int xamlHardcoded = 0;
        int csHardcoded = 0;
        int unicodeSymbols = 0;
        var userFacingSet = new HashSet<string>(UserFacingProps, StringComparer.OrdinalIgnoreCase);

        // === 1. AXAML: Hardcoded Text/Content/Watermark/Header/ToolTip.Tip ===
        foreach (var file in ctx.AxamlFiles)
        {
            // App.axaml + AppPalette.axaml ueberspringen (Resources, keine User-Strings)
            var fileName = Path.GetFileName(file.FullPath);
            if (fileName == "App.axaml" || fileName == "AppPalette.axaml") continue;

            var xamlLines = file.Content.Split('\n');
            for (int i = 0; i < xamlLines.Length; i++)
            {
                var line = xamlLines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("<!--")) continue;
                // XAML AppChecker:ignore unterstuetzen
                if (i > 0 && xamlLines[i - 1].TrimStart().StartsWith("<!-- AppChecker:ignore")) continue;

                var matches = AttributePattern.Matches(line);
                foreach (Match m in matches)
                {
                    var prop = m.Groups["prop"].Value;
                    var rawValue = m.Groups["value"].Value;
                    // XAML-Entities dekodieren (&#241; → ñ, &amp; → & etc.)
                    var value = System.Net.WebUtility.HtmlDecode(rawValue);

                    if (!userFacingSet.Contains(prop)) continue;

                    // Unicode-Symbol als UI-Text (Icon-Strategie: SvgIcon/MaterialIcon statt Glyphen)
                    if (UiSymbolRegex.IsMatch(value))
                    {
                        unicodeSymbols++;
                        if (unicodeSymbols <= 10)
                        {
                            var sym = UiSymbolRegex.Match(value).Value;
                            results.Add(new(Severity.Warn, Category,
                                $"Unicode-Symbol '{sym}' als {prop} in {file.RelativePath}:{i + 1} → SvgIcon/MaterialIcon verwenden (Icon-Strategie)"));
                        }
                        continue; // nicht zusaetzlich als Hardcoded-String werten
                    }

                    if (IsTechnicalValue(value)) continue;
                    if (IsBindingOrResource(value)) continue;
                    if (!ContainsWordLikeContent(value)) continue;

                    // MainWindow.Title=AppName ist OK (Brand-Name)
                    if (string.Equals(prop, "Title", StringComparison.OrdinalIgnoreCase)
                        && fileName.Equals("MainWindow.axaml", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Brand-Logo-Komponenten (ALL_CAPS_ein_wort ≤ 8 chars, Buchstaben) sind oft Logo-Text
                    bool brandLikely = value.Length <= 8 && !value.Contains(' ')
                                     && value.All(c => char.IsLetter(c)) && value.All(char.IsUpper);

                    // ToolTip.Tip mit Einzel-Wort → meist allgemeines Aktions-Wort, INFO statt WARN
                    bool isShortTooltip = string.Equals(prop, "ToolTip.Tip", StringComparison.OrdinalIgnoreCase)
                                        && !value.Contains(' ') && value.Length <= 12;

                    xamlHardcoded++;
                    if (xamlHardcoded <= 15)
                    {
                        var preview = value.Length > 40 ? value[..40] + "..." : value;
                        var severity = (brandLikely || isShortTooltip) ? Severity.Info : Severity.Warn;
                        var hint = brandLikely ? " (Brand-Logo? sonst lokalisieren)"
                                 : isShortTooltip ? " (kurzer Tooltip — meist lokalisieren)"
                                 : "";
                        results.Add(new(severity, Category,
                            $"Hardcoded {prop}=\"{preview}\" in {file.RelativePath}:{i + 1}{hint} → {{x:Static strings:AppStrings.Xxx}} oder {{Binding}}"));
                    }
                }
            }
        }

        if (xamlHardcoded > 15)
            results.Add(new(Severity.Warn, Category, $"...und {xamlHardcoded - 15} weitere hardcodierte AXAML-Strings"));

        if (unicodeSymbols == 0)
            results.Add(new(Severity.Pass, Category, "Keine Unicode-Symbole als UI-Text (Icon-Strategie eingehalten)"));
        else if (unicodeSymbols > 10)
            results.Add(new(Severity.Warn, Category, $"...und {unicodeSymbols - 10} weitere Unicode-Symbole als UI-Text"));

        // === 2. C#: Toast/MessageBox/FloatingText-Strings ohne GetString() ===
        foreach (var file in ctx.SharedCsFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                var match = CsToastPattern.Match(trimmed);
                if (!match.Success) continue;

                var value = match.Groups[2].Value;
                if (IsTechnicalValue(value)) continue;
                if (!ContainsWordLikeContent(value)) continue;

                // Wenn Zeile GetString() enthaelt (z.B. Komposition) → ok
                if (trimmed.Contains("GetString(")) continue;

                csHardcoded++;
                if (csHardcoded <= 10)
                {
                    var preview = value.Length > 40 ? value[..40] + "..." : value;
                    results.Add(new(Severity.Info, Category,
                        $"Hardcoded User-String \"{preview}\" in {file.RelativePath}:{i + 1} → GetString(key) verwenden"));
                }
            }
        }

        if (csHardcoded > 10)
            results.Add(new(Severity.Info, Category, $"...und {csHardcoded - 10} weitere hardcodierte C#-Strings"));

        // Zusammenfassungen
        if (xamlHardcoded == 0)
            results.Add(new(Severity.Pass, Category, "Keine hardcodierten User-Strings in AXAML"));
        if (csHardcoded == 0)
            results.Add(new(Severity.Pass, Category, "Keine hardcodierten User-Strings in Toast/MessageBox/FloatingText"));

        return results;
    }

    static bool IsBindingOrResource(string value) =>
        value.StartsWith('{') ||              // {Binding}, {StaticResource}, etc.
        value.StartsWith("avares://") ||      // Asset-Referenz
        value.StartsWith("http://") ||        // URL
        value.StartsWith("https://");

    static bool IsTechnicalValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Length < 3) return true;
        if (AllowedShortValues.Contains(value)) return true;
        // Copyright-Zeile (typisch "© 2026 RS Digital") — nicht lokalisiert
        if (value.Contains('©') || value.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase)) return true;
        // Versions-Strings (v1.0.0, 2.3.4)
        if (Regex.IsMatch(value, @"^v?\d+\.\d+(\.\d+)?(\.\d+)?$")) return true;

        // Reine Zahl (auch mit Komma/Punkt)
        if (Regex.IsMatch(value, @"^-?\d+([.,]\d+)?$")) return true;
        // Mehrere Zahlen kommagetrennt (Spacing, Margin, Padding-Werte)
        if (Regex.IsMatch(value, @"^[\d.,\s-]+$")) return true;
        // Hex-Farbe
        if (Regex.IsMatch(value, @"^#[0-9A-Fa-f]{3,8}$")) return true;
        // CSS-Klassennamen (kleinbuchstaben + dash)
        if (Regex.IsMatch(value, @"^[a-z][a-z0-9-]*$")) return true;
        // Format-String mit Klammer/Platzhalter
        if (value.Contains('{') && value.Contains('}')) return true;
        // Single-Symbol (z.B. •, →, *, +, -)
        if (value.Length <= 3 && !value.Any(char.IsLetter)) return true;
        // DateTime-Format
        if (Regex.IsMatch(value, @"^[ydHhmsfMtT.:/ -]+$") && value.Any(char.IsLetter)) return true;
        // Material-Icon-Kind (Pascal-Case ohne Spaces, kein Vokal-Wort)
        if (Regex.IsMatch(value, @"^[A-Z][a-zA-Z]{2,30}$") && !value.Contains(' '))
        {
            // Heuristik: Material-Icon-Namen sind oft "AlertCircle", "ChevronRight", etc. - keine Spaces
            // Aber auch deutsche Substantive: "Einstellungen", "Speichern", etc.
            // Wir lassen das durch und fragen weiter
        }

        return false;
    }

    /// <summary>Enthaelt der Wert eine erkennbare Wortstruktur (≥ 3 aufeinanderfolgende Buchstaben)?</summary>
    static bool ContainsWordLikeContent(string value)
    {
        int letterRun = 0;
        foreach (var c in value)
        {
            if (char.IsLetter(c))
            {
                letterRun++;
                if (letterRun >= 3) return true;
            }
            else
                letterRun = 0;
        }
        return false;
    }
}
