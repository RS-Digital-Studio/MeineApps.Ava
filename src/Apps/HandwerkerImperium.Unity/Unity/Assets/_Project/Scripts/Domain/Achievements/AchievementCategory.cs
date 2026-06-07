namespace HandwerkerImperium.Domain.Achievements
{
    /// <summary>
    /// Kategorien für (Spieler-)Achievements.
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/AchievementCategory.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// </summary>
    public enum AchievementCategory
    {
        Orders,
        Workshops,
        MiniGames,
        Money,
        Time,
        Special,
        Workers,
        Buildings,
        Research,
        Reputation,
        Prestige,
        Guilds,
        Crafting,
        Tournaments,
        Collection,
        Ascension,
        Rebirth
    }
}
