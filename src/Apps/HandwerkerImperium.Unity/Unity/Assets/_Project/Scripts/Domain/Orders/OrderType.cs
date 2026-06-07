using System;
using HandwerkerImperium.Domain;

namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Auftragstypen mit unterschiedlicher Komplexität und Belohnung.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/OrderType.cs). Reine Spiellogik —
    /// Icon/Lokalisierungs-Methoden leben in der Unity-UI-Schicht. Numerische Werte save-relevant.
    /// </summary>
    public enum OrderType
    {
        /// <summary>1 Mini-Game, schnelle Belohnung, kein Cooldown</summary>
        Quick = 0,

        /// <summary>2-3 Mini-Games, Standard-Belohnung</summary>
        Standard = 1,

        /// <summary>4-6 Mini-Games, hohe Belohnung</summary>
        Large = 2,

        /// <summary>10 Mini-Games, 7-Tage-Deadline, sehr hohe Belohnung</summary>
        Weekly = 3,

        /// <summary>3 Mini-Games über 2+ Workshop-Typen, Bonus-Belohnung</summary>
        Cooperation = 4,

        /// <summary>Kein Mini-Game — Crafting-Items liefern für sofortige Belohnung</summary>
        MaterialOrder = 5
    }

    /// <summary>
    /// Extension-Methoden für <see cref="OrderType"/> (reine Spiellogik-Werte).
    /// </summary>
    public static class OrderTypeExtensions
    {
        /// <summary>Anzahl Tasks (Mini-Games) für diesen Auftragstyp als (Min, Max)-Spanne.</summary>
        public static (int Min, int Max) GetTaskCount(this OrderType type) => type switch
        {
            OrderType.Quick => (1, 1),
            OrderType.Standard => (2, 3),
            OrderType.Large => (4, 6),
            OrderType.Weekly => (10, 10),
            OrderType.Cooperation => (3, 3),
            OrderType.MaterialOrder => (0, 0), // Kein Mini-Game
            _ => (2, 3)
        };

        /// <summary>Belohnungs-Multiplikator verglichen mit einem Standard-Auftrag.</summary>
        public static decimal GetRewardMultiplier(this OrderType type) => type switch
        {
            OrderType.Quick => 0.6m,
            OrderType.Standard => 1.0m,
            OrderType.Large => 1.8m,
            OrderType.Weekly => 3.0m,
            OrderType.Cooperation => 2.5m,
            OrderType.MaterialOrder => GameBalanceConstants.MaterialOrderRewardMultiplier,
            _ => 1.0m
        };

        /// <summary>XP-Multiplikator verglichen mit einem Standard-Auftrag.</summary>
        public static decimal GetXpMultiplier(this OrderType type) => type switch
        {
            OrderType.Quick => 0.5m,
            OrderType.Standard => 1.0m,
            OrderType.Large => 2.0m,
            OrderType.Weekly => 3.0m,
            OrderType.Cooperation => 3.0m,
            OrderType.MaterialOrder => GameBalanceConstants.MaterialOrderXpMultiplier,
            _ => 1.0m
        };

        /// <summary>Mindest-Spielerlevel für diesen Auftragstyp.</summary>
        public static int GetUnlockLevel(this OrderType type) => type switch
        {
            OrderType.Quick => 1,
            OrderType.Standard => 1,
            OrderType.Large => 10,
            OrderType.Weekly => 20,
            OrderType.Cooperation => 15,
            OrderType.MaterialOrder => GameBalanceConstants.AutoProductionUnlockLevel,
            _ => 1
        };

        /// <summary>Ob dieser Auftragstyp ein Zeitlimit hat.</summary>
        public static bool HasDeadline(this OrderType type) => type switch
        {
            OrderType.Weekly => true,
            OrderType.MaterialOrder => true,
            _ => false
        };

        /// <summary>Standard-Deadline-Dauer (nur für zeitlich begrenzte Aufträge).</summary>
        public static TimeSpan? GetDeadline(this OrderType type) => type switch
        {
            OrderType.Weekly => TimeSpan.FromDays(7),
            OrderType.MaterialOrder => TimeSpan.FromHours(GameBalanceConstants.MaterialOrderDeadlineHours),
            _ => null
        };

        /// <summary>Ob dieser Auftragstyp mehrere Workshop-Typen benötigt.</summary>
        public static bool RequiresMultipleWorkshops(this OrderType type) =>
            type == OrderType.Cooperation;

        /// <summary>Ob dieser Auftragstyp ein Lieferauftrag ist (kein Mini-Game, Items liefern).</summary>
        public static bool IsMaterialOrder(this OrderType type) => type == OrderType.MaterialOrder;
    }
}
