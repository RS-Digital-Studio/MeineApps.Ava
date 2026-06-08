namespace HandwerkerImperium.Domain.Settings
{
    /// <summary>
    /// Grafik-Qualitätsstufen. 1:1-Port aus dem Avalonia-Original (Models/Enums/GraphicsQuality.cs).
    /// Persistiert in SettingsData → Enum-Integer-Werte stabil halten. Unity wendet die Stufe auf
    /// sein eigenes Quality-System an (Präsentationsschicht).
    /// </summary>
    public enum GraphicsQuality
    {
        /// <summary>Niedrig: Keine Partikel, kein Wetter, keine Schatten.</summary>
        Low = 0,
        /// <summary>Mittel: Reduzierte Partikel, einfaches Wetter.</summary>
        Medium = 1,
        /// <summary>Hoch: Alle Effekte aktiv (Standard).</summary>
        High = 2
    }
}
