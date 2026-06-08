#nullable enable
using System;
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>EquipmentService</c> (Avalonia-Original): die reine
    /// Drop-Wahrscheinlichkeits-Mathematik und die Shop-Rotations-Generierung. 1:1 zur Vorlage.
    ///
    /// Bewusst NICHT extrahiert (state-/analytics-gekoppelt, gehoeren in den Game-Service):
    /// EquipItem/UnequipItem/BuyEquipment (GameState-Mutation), das EquipmentDropped-Event und Telemetrie.
    /// </summary>
    public static class EquipmentFormulas
    {
        /// <summary>
        /// Basis-Drop-Chance nach einem MiniGame (skaliert nach Schwierigkeit).
        /// Easy=5%, Medium=10%, Hard=15%, Expert=20%. Perfect-Rating: +5%.
        /// </summary>
        public const double BaseDropChance = 0.05;

        /// <summary>Shop-Rotation: 3-4 zufaellige Gegenstaende.</summary>
        public const int MinShopItems = 3;
        public const int MaxShopItems = 4;

        /// <summary>
        /// Drop-Chance nach einem MiniGame: +5% pro Schwierigkeitsstufe, Perfect-Rating zusaetzlich +5%.
        /// </summary>
        public static double CalculateDropChance(int difficulty, bool isPerfect) =>
            BaseDropChance + difficulty * 0.05 + (isPerfect ? 0.05 : 0.0);

        /// <summary>
        /// Wuerfelt einen Equipment-Drop nach einem MiniGame. Liefert <c>null</c>, wenn kein Drop faellt,
        /// sonst ein zufaelliges Equipment passend zur Schwierigkeit. Die Geld-/Inventar-Mutation +
        /// Telemetrie bleiben im Game-Service.
        /// </summary>
        public static Equipment? RollDrop(int difficulty, bool isPerfect, Random rng)
        {
            double dropChance = CalculateDropChance(difficulty, isPerfect);
            if (rng.NextDouble() >= dropChance)
                return null;
            return Equipment.GenerateRandom(difficulty);
        }

        /// <summary>
        /// Generiert die Shop-Rotation (3-4 Gegenstaende, Shop-Qualitaet difficulty 1-3). Die Anzahl- und
        /// Difficulty-Auswahl nutzt das uebergebene <paramref name="rng"/>; die Item-Werte stammen aus
        /// <see cref="Equipment.GenerateRandom"/> (eigener Equipment-Rng — wie im Original).
        /// </summary>
        public static List<Equipment> GenerateShopItems(Random rng)
        {
            int count = rng.Next(MinShopItems, MaxShopItems + 1);
            var items = new List<Equipment>(count);
            for (int i = 0; i < count; i++)
            {
                int shopDifficulty = rng.Next(1, 4);
                items.Add(Equipment.GenerateRandom(shopDifficulty));
            }
            return items;
        }
    }
}
