namespace HandwerkerImperium.Domain.Buildings
{
    /// <summary>
    /// Typen von Hilfsgebäuden, die passive Boni geben. Jedes Gebäude kann von Level 1 bis 5
    /// ausgebaut werden.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/BuildingType.cs). Die UI-Extensions
    /// (Icon, Lokalisierungs-/Beschreibungs-/Effekt-Keys) wandern in die Präsentationsschicht.
    /// </summary>
    public enum BuildingType
    {
        /// <summary>Verbessert Worker-Mood-Erholung und reduziert Ruhezeit.</summary>
        Canteen = 0,

        /// <summary>Reduziert Materialkosten.</summary>
        Storage = 1,

        /// <summary>Erhöht verfügbare Auftrags-Slots.</summary>
        Office = 2,

        /// <summary>Erhöht passiv die Reputation.</summary>
        Showroom = 3,

        /// <summary>Beschleunigt Worker-Training.</summary>
        TrainingCenter = 4,

        /// <summary>Erhöht Auftragsbelohnungen.</summary>
        VehicleFleet = 5,

        /// <summary>Fügt zusätzliche Worker-Slots pro Workshop hinzu.</summary>
        WorkshopExtension = 6
    }

    /// <summary>Gameplay-Extensions für <see cref="BuildingType"/>.</summary>
    public static class BuildingTypeExtensions
    {
        /// <summary>
        /// Basis-Kaufkosten für diesen Gebäude-Typ (Level 1).
        /// Jedes weitere Level kostet BaseCost * 2^(Level) (siehe Building.NextLevelCost).
        /// </summary>
        public static decimal GetBaseCost(this BuildingType type) => type switch
        {
            BuildingType.Canteen => 10_000m,
            BuildingType.Storage => 15_000m,
            BuildingType.Office => 20_000m,
            BuildingType.Showroom => 25_000m,
            BuildingType.TrainingCenter => 50_000m,
            BuildingType.VehicleFleet => 75_000m,
            BuildingType.WorkshopExtension => 100_000m,
            _ => 10_000m
        };

        /// <summary>Spieler-Level zum Freischalten dieses Gebäudes.</summary>
        public static int GetUnlockLevel(this BuildingType type) => type switch
        {
            BuildingType.Canteen => 5,
            BuildingType.Storage => 8,
            BuildingType.Office => 10,
            BuildingType.Showroom => 15,
            BuildingType.TrainingCenter => 20,
            BuildingType.VehicleFleet => 25,
            BuildingType.WorkshopExtension => 30,
            _ => 5
        };

        /// <summary>Maximales Level für diesen Gebäude-Typ.</summary>
        public static int GetMaxLevel(this BuildingType type) => 5;
    }
}
