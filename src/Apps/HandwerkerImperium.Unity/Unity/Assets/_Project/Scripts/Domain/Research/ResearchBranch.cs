namespace HandwerkerImperium.Domain.Research
{
    /// <summary>
    /// Forschungs-Branches im Skill-Tree. Jeder Branch hat bis zu 20 Forschungs-Level.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/ResearchBranch.cs). Die
    /// Extension-Methoden des Originals (Icon, Farb-Key, Lokalisierungs-Keys) sind
    /// ausschließlich UI und wandern in die Unity-Präsentationsschicht.
    /// </summary>
    public enum ResearchBranch
    {
        /// <summary>Verbessert Werkzeuge, Effizienz und Mini-Game-Boni.</summary>
        Tools = 0,

        /// <summary>Verbessert Worker-Management, Einstellung und Training.</summary>
        Management = 1,

        /// <summary>Verbessert Marketing, Reputation und Auftragsbelohnungen.</summary>
        Marketing = 2,

        /// <summary>Lager-Slots, Stack-Limit, Markt, Auto-Verkauf.</summary>
        Logistics = 3
    }
}
