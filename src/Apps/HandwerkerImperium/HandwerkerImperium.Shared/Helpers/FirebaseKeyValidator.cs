namespace HandwerkerImperium.Helpers;

/// <summary>
/// Validierung von Firebase-Realtime-Database-Pfad-Schluesseln.
/// Firebase verbietet folgende Zeichen in Path-Keys: '.', '$', '#', '[', ']', '/'
/// und leere Strings. Geteilt zwischen GuildService und GuildInviteService.
/// </summary>
public static class FirebaseKeyValidator
{
    /// <summary>
    /// Prueft ob ein Key fuer Firebase-Pfade gueltig ist.
    /// Firebase verbietet: '.', '$', '#', '[', ']', '/' und leere Keys.
    /// </summary>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        for (int i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (c == '.' || c == '$' || c == '#' || c == '[' || c == ']' || c == '/')
                return false;
        }

        return true;
    }
}
