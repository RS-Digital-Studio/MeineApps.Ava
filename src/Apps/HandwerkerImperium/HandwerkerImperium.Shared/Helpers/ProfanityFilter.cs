namespace HandwerkerImperium.Helpers;

/// <summary>
/// Minimaler Profanity-Filter für Gilden-Chat und Spielernamen.
/// Ersetzt bekannte Beleidigungen durch "***" (case-insensitive, 6 Sprachen).
/// Kein Anspruch auf Vollständigkeit — Play Store Compliance Basis-Schutz.
/// </summary>
public static class ProfanityFilter
{
    // Gängige Beleidigungen (DE, EN, ES, FR, IT, PT) — Wörter die im Gaming-Kontext
    // am häufigsten für Missbrauch verwendet werden. Bewusst kompakt gehalten.
    private static readonly HashSet<string> BlockedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Deutsch
        "hurensohn", "wichser", "arschloch", "schlampe", "fotze", "missgeburt",
        "spasti", "behindert", "nazi", "heil hitler", "sieg heil",

        // Englisch
        "fuck", "fucking", "fucker", "shit", "asshole", "bitch", "cunt",
        "nigger", "nigga", "faggot", "retard", "retarded",

        // Spanisch
        "puta", "mierda", "hijo de puta", "pendejo", "maricón", "coño",

        // Französisch
        "putain", "merde", "connard", "salope", "enculé", "nique",

        // Italienisch
        "cazzo", "vaffanculo", "stronzo", "puttana", "merda", "coglione",

        // Portugiesisch
        "porra", "caralho", "filho da puta", "merda", "buceta", "viado"
    };

    /// <summary>
    /// v2.1.1 (Audit FB-M04): Leetspeak-Mapping fuer typische Bypass-Versuche
    /// (f@ck, sh1t, b1tch, etc.). Wird vor dem Wort-Vergleich angewendet.
    /// </summary>
    private static readonly (char from, char to)[] LeetMapping =
    {
        ('@', 'a'), ('4', 'a'),
        ('3', 'e'),
        ('1', 'i'), ('!', 'i'), ('|', 'i'),
        ('0', 'o'),
        ('5', 's'), ('$', 's'),
        ('7', 't')
    };

    /// <summary>
    /// v2.1.1 (Audit FB-M04): Normalisiert Text — Unicode-Diakritika (FormD + IsNonSpacingMark)
    /// strippen, dann Leetspeak-Mapping anwenden. Ergebnis ist die Vergleichs-Form, die
    /// gegen die Blockliste geprueft wird.
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // 1. Unicode-Diakritika strippen ("fück" → "fuck")
        var normalized = input.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        // 2. Leetspeak-Mapping ("f@ck" → "fack" mit nochmaligem Match-Versuch)
        for (int i = 0; i < sb.Length; i++)
        {
            char c = sb[i];
            for (int j = 0; j < LeetMapping.Length; j++)
            {
                if (LeetMapping[j].from == c) { sb[i] = LeetMapping[j].to; break; }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Ersetzt bekannte Beleidigungen im Text durch "***".
    /// Prüft ganze Wörter und mehrteilige Phrasen.
    /// v2.1.1 (Audit FB-M04): Vergleich gegen normalisierte Form (Diakritika strippen +
    /// Leetspeak). Replace passiert im Original-Text, sodass die Anzeige Diakritika
    /// behaelt — nur die Match-Logik ist robuster gegen Bypass-Versuche.
    /// </summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // FB-M04: Vergleichs-Form berechnen (normalisiert), Replace im Original.
        var normalized = Normalize(text);

        // Erst mehrteilige Phrasen prüfen (z.B. "hijo de puta")
        foreach (var phrase in BlockedWords)
        {
            if (phrase.Contains(' '))
            {
                // Phrasen in der normalisierten Form suchen, Indizes ueber das Original anwenden.
                var index = normalized.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
                while (index >= 0)
                {
                    text = string.Concat(text.AsSpan(0, index), "***", text.AsSpan(index + phrase.Length));
                    normalized = string.Concat(normalized.AsSpan(0, index), "***", normalized.AsSpan(index + phrase.Length));
                    index = normalized.IndexOf(phrase, index + 3, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // Dann einzelne Wörter prüfen (Wortgrenzen respektieren).
        // FB-M04: Wir splitten BEIDE Strings parallel, damit Index-Alignment stimmt.
        var normWords = normalized.Split(' ');
        var words = text.Split(' ');
        for (int i = 0; i < words.Length && i < normWords.Length; i++)
        {
            var normWord = normWords[i].TrimEnd('.', ',', '!', '?', ':', ';');
            if (BlockedWords.Contains(normWord))
            {
                // Im Original-Wort die gleiche Satzzeichen-Behandlung
                var origWord = words[i].TrimEnd('.', ',', '!', '?', ':', ';');
                var suffix = words[i][origWord.Length..];
                words[i] = "***" + suffix;
            }
        }

        return string.Join(' ', words);
    }
}
