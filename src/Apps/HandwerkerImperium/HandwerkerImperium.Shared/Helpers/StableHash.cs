namespace HandwerkerImperium.Helpers;

/// <summary>
/// Prozess-stabiler String-Hash (FNV-artiges Polynom). <see cref="string.GetHashCode()"/> ist seit
/// .NET Core pro Prozess randomisiert und damit ungeeignet fuer Seeds, die ueber App-Neustarts hinweg
/// deterministisch sein muessen (z.B. tagesbasierte Markt-Preise pro Spieler/Material).
/// </summary>
public static class StableHash
{
    public static int Compute(string? input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        unchecked
        {
            int hash = 17;
            foreach (char c in input)
                hash = hash * 31 + c;
            return hash;
        }
    }
}
