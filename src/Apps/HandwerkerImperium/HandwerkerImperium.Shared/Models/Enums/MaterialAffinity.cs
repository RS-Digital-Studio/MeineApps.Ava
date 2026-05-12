namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan): Worker-Material-Affinitaet (Plan Section 3.7).
/// Eine von 5 Achsen wird beim Hiring gerollt (gleichverteilt 20%).
/// Match mit Material-Kategorie gibt +20% Crafting-Speed des Workshops.
/// </summary>
public enum MaterialAffinity
{
    /// <summary>Keine Affinitaet (Default fuer alte Saves vor V7 Phase 4).</summary>
    None = 0,

    /// <summary>Holz: Holzbrett, Moebel, Luxusmoebel, Schalung.</summary>
    Wood = 1,

    /// <summary>Metall: Rohrleitung, Beschlag, Stahltraeger, Sanitaer-System.</summary>
    Metal = 2,

    /// <summary>Stein: Beton, Hochhaus-Rohbau, Dachziegel, Dachsystem.</summary>
    Stone = 3,

    /// <summary>Kunst: Farbe, Wand-Design, Kunstwerk, Luxusmoebel-Veredelung.</summary>
    Art = 4,

    /// <summary>Tech: Kabel, Schaltkreis, Smart-Home, Prototyp, Innovation, Patent.</summary>
    Tech = 5
}

public static class MaterialAffinityExtensions
{
    /// <summary>
    /// V7 (Phase 4): Ordnet eine Produkt-ID einer Material-Affinitaet zu.
    /// Wird im CraftingService genutzt um den +20%-Speed-Match zu berechnen.
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

    /// <summary>Lokalisations-Key fuer Affinitaets-Anzeige.</summary>
    public static string GetLocalizationKey(this MaterialAffinity affinity) => affinity switch
    {
        MaterialAffinity.Wood => "AffinityWood",
        MaterialAffinity.Metal => "AffinityMetal",
        MaterialAffinity.Stone => "AffinityStone",
        MaterialAffinity.Art => "AffinityArt",
        MaterialAffinity.Tech => "AffinityTech",
        _ => "AffinityNone"
    };

    /// <summary>Icon-Hinweis fuer UI-Anzeige.</summary>
    public static string GetIcon(this MaterialAffinity affinity) => affinity switch
    {
        MaterialAffinity.Wood => "Forest",
        MaterialAffinity.Metal => "Anvil",
        MaterialAffinity.Stone => "Wall",
        MaterialAffinity.Art => "Palette",
        MaterialAffinity.Tech => "Chip",
        _ => "AccountQuestion"
    };
}
