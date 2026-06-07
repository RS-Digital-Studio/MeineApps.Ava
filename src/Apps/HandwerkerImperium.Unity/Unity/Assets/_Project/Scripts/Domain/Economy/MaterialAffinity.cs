namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Worker-Material-Affinität. Eine von 5 Achsen wird beim Hiring gerollt (gleichverteilt 20%).
    /// Match mit der Material-Kategorie gibt +20% Crafting-Speed des Workshops.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/MaterialAffinity.cs). Numerische Werte
    /// save-relevant. Die Produkt→Affinität-Zuordnung ist Crafting-Spiellogik (kein UI).
    /// </summary>
    public enum MaterialAffinity
    {
        /// <summary>Keine Affinität (Default für alte Saves vor V7).</summary>
        None = 0,

        /// <summary>Holz: Holzbrett, Möbel, Luxusmöbel, Schalung.</summary>
        Wood = 1,

        /// <summary>Metall: Rohrleitung, Beschlag, Stahlträger, Sanitär-System.</summary>
        Metal = 2,

        /// <summary>Stein: Beton, Hochhaus-Rohbau, Dachziegel, Dachsystem.</summary>
        Stone = 3,

        /// <summary>Kunst: Farbe, Wand-Design, Kunstwerk, Luxusmöbel-Veredelung.</summary>
        Art = 4,

        /// <summary>Tech: Kabel, Schaltkreis, Smart-Home, Prototyp, Innovation, Patent.</summary>
        Tech = 5
    }

    /// <summary>
    /// Extension-Methoden für <see cref="MaterialAffinity"/> (Crafting-Zuordnung, reine Spiellogik).
    /// </summary>
    public static class MaterialAffinityExtensions
    {
        /// <summary>
        /// Ordnet eine Produkt-ID einer Material-Affinität zu (für den +20%-Speed-Match im Crafting).
        /// </summary>
        public static MaterialAffinity GetMaterialAffinity(string productId) => productId switch
        {
            // Holz-Kategorie
            "planks" or "furniture" or "luxury_furniture" or "concrete_foundation"
                => MaterialAffinity.Wood,

            // Metall-Kategorie
            "pipes" or "plumbing_system" or "bathroom_installation"
                or "fittings" or "master_fittings" or "masterpiece_fittings"
                => MaterialAffinity.Metal,

            // Stein-Kategorie
            "concrete" or "skyscraper_frame"
                or "roof_tiles" or "roofing_system" or "roof_structure"
                => MaterialAffinity.Stone,

            // Kunst-Kategorie
            "paint_mix" or "wall_design" or "artwork"
                or "blueprint" or "framework" or "master_blueprint"
                or "contract" or "contract_complex" or "general_contract"
                => MaterialAffinity.Art,

            // Tech-Kategorie
            "cables" or "circuit" or "smart_home"
                or "prototype" or "innovation" or "patent"
                => MaterialAffinity.Tech,

            // T4-Items: gemischt (Imperiums-Manufaktur), kein einzelner Match
            _ => MaterialAffinity.None
        };
    }
}
