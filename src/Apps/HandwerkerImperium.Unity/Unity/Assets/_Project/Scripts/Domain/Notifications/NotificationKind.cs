namespace HandwerkerImperium.Domain.Notifications
{
    /// <summary>
    /// Art der Benachrichtigung. Steuert Icon, Sortier-Priorität und Klick-Aktion.
    /// 1:1-Port aus dem Avalonia-Original (Models/NotificationItem.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// Der NotificationItem-Datenteil reist später mit dem GameState; Icon-/Priority-Auflösung ist UI.
    /// </summary>
    public enum NotificationKind
    {
        /// <summary>Offline-Einkommen verfügbar — wird IMMER als Modal angezeigt, nicht in der Bell.</summary>
        OfflineEarnings,
        /// <summary>Tagesbelohnung wartet — sammelbar in der Bell.</summary>
        DailyReward,
        /// <summary>Willkommen-Zurück-Angebot (Premium-Bundle) — sammelbar in der Bell.</summary>
        WelcomeBackOffer,
        /// <summary>Erfolg freigeschaltet — sammelbar in der Bell.</summary>
        AchievementUnlocked,
        /// <summary>Login-Streak erfolgreich gerettet — sammelbar in der Bell.</summary>
        StreakSaved,
        /// <summary>Neues Story-Kapitel verfügbar — sammelbar in der Bell, mit Pulse-Akzent.</summary>
        NewStoryChapter,
        /// <summary>Live-/Premium-Auftrag verfügbar.</summary>
        LiveOrderAvailable
    }
}
