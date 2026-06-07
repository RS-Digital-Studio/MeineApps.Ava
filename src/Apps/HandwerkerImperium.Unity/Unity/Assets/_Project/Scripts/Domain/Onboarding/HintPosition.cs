namespace HandwerkerImperium.Domain.Onboarding
{
    /// <summary>
    /// Position eines kontextuellen Hints relativ zum Ziel-Element.
    /// 1:1-Port aus dem Avalonia-Original (Models/ContextualHint.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// </summary>
    public enum HintPosition
    {
        Above,
        Below
    }
}
