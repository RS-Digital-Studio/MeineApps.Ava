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
    /// Ersetzt bekannte Beleidigungen im Text durch "***".
    /// Prüft ganze Wörter und mehrteilige Phrasen.
    /// </summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Erst mehrteilige Phrasen prüfen (z.B. "hijo de puta")
        foreach (var phrase in BlockedWords)
        {
            if (phrase.Contains(' '))
            {
                var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
                while (index >= 0)
                {
                    text = string.Concat(text.AsSpan(0, index), "***", text.AsSpan(index + phrase.Length));
                    index = text.IndexOf(phrase, index + 3, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // Dann einzelne Wörter prüfen (Wortgrenzen respektieren)
        var words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            // Satzzeichen am Ende entfernen für den Check
            var word = words[i].TrimEnd('.', ',', '!', '?', ':', ';');
            if (BlockedWords.Contains(word))
            {
                // Nur das Wort ersetzen, Satzzeichen behalten
                var suffix = words[i][word.Length..];
                words[i] = "***" + suffix;
            }
        }

        return string.Join(' ', words);
    }
}
