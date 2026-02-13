namespace ZeitManager.Audio;

/// <summary>
/// Deterministischer Hash für Sound-IDs und andere Strings.
/// Gleicher Algorithmus wie AndroidNotificationService.StableHash.
/// </summary>
public static class HashHelper
{
    public static int StableHash(string input)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
                hash = hash * 31 + c;
            return Math.Abs(hash);
        }
    }
}
