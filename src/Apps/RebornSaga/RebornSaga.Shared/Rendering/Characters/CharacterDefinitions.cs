namespace RebornSaga.Rendering.Characters;

using SkiaSharp;

/// <summary>
/// Definition eines Charakters: Farben, Haare, Körper, Accessoires.
/// Wird vom Charakter-Rendering-System verwendet.
/// </summary>
public class CharacterDefinition
{
    public string Id { get; init; } = "";
    public SKColor SkinColor { get; init; }
    public SKColor HairColor { get; init; }
    public SKColor EyeColor { get; init; }
    public SKColor OutfitColor { get; init; }
    public SKColor OutfitAccent { get; init; }
    public float HairLength { get; init; }     // 0-1 (0=kurz, 1=sehr lang)
    public int HairStyle { get; init; }        // 0=kurz, 1=lang-glatt, 2=Zopf, 3=wild/stachelig
    public int BodyType { get; init; }         // 0=schlank, 1=muskulös, 2=Robe/Umhang
    public int AccessoryType { get; init; }    // 0=Schwert, 1=Stab, 2=Dolche, 3=keine
    public bool HasGlowingEyes { get; init; }  // Leuchtende Augen (z.B. Nihilus, aufgeladene Zustände)
    public bool IsHolographic { get; init; }   // Für ARIA System-Geist (blaue Transparenz)
    public bool HasBangs { get; init; }        // Pony ja/nein
    public int BangStyle { get; init; }        // 0=gerade, 1=seitlich, 2=strähnenartig
    public SKColor? AuraColor { get; init; }   // Optionale Aura-Farbe (Bosse, Power-Ups)
}

/// <summary>
/// Statische Definitionen aller Charaktere im Spiel.
/// Protagonist hat 3 Varianten (je nach Klasse).
/// </summary>
public static class CharacterDefinitions
{
    // --- Protagonist (3 Klassen) ---

    public static readonly CharacterDefinition Protagonist_Sword = new()
    {
        Id = "protag_sword",
        SkinColor = new SKColor(0xFF, 0xDB, 0xAC),
        HairColor = new SKColor(0x2C, 0x3E, 0x50),  // Dunkelblau
        EyeColor = new SKColor(0x4A, 0x90, 0xD9),   // System-Blau
        OutfitColor = new SKColor(0x34, 0x49, 0x5E), // Dunkelblau-Grau
        OutfitAccent = new SKColor(0xC0, 0x39, 0x2B), // Rot-Akzent
        HairLength = 0.3f,
        HairStyle = 3, // Wild/stachelig
        BodyType = 1,  // Muskulös
        AccessoryType = 0, // Schwert
        HasBangs = true,
        BangStyle = 2  // Lose Strähnen
    };

    public static readonly CharacterDefinition Protagonist_Mage = new()
    {
        Id = "protag_mage",
        SkinColor = new SKColor(0xFF, 0xDB, 0xAC),
        HairColor = new SKColor(0x6C, 0x3C, 0x97),  // Lila
        EyeColor = new SKColor(0x9B, 0x59, 0xB6),   // Mystisch-Lila
        OutfitColor = new SKColor(0x1A, 0x1A, 0x3E), // Dunkelblau
        OutfitAccent = new SKColor(0x9B, 0x59, 0xB6), // Lila-Akzent
        HairLength = 0.6f,
        HairStyle = 1, // Lang-glatt
        BodyType = 2,  // Robe
        AccessoryType = 1, // Stab
        HasBangs = true,
        BangStyle = 1  // Seitlich
    };

    public static readonly CharacterDefinition Protagonist_Assassin = new()
    {
        Id = "protag_assassin",
        SkinColor = new SKColor(0xF0, 0xCE, 0xA0),
        HairColor = new SKColor(0x1C, 0x1C, 0x1C),  // Schwarz
        EyeColor = new SKColor(0x2E, 0xCC, 0x71),   // Grün
        OutfitColor = new SKColor(0x1C, 0x1C, 0x28), // Sehr dunkel
        OutfitAccent = new SKColor(0x2E, 0xCC, 0x71), // Grün-Akzent
        HairLength = 0.4f,
        HairStyle = 0, // Kurz
        BodyType = 0,  // Schlank
        AccessoryType = 2 // Dolche
        // HasBangs = false (default)
    };

    // --- NPCs ---

    /// <summary>Aria: Rothaarige Schwertkämpferin, temperamentvoll, loyal.</summary>
    public static readonly CharacterDefinition Aria = new()
    {
        Id = "aria",
        SkinColor = new SKColor(0xFF, 0xDB, 0xAC),
        HairColor = new SKColor(0xCC, 0x33, 0x33),  // Feuerrot
        EyeColor = new SKColor(0x2E, 0xCC, 0x71),   // Grün
        OutfitColor = new SKColor(0x8B, 0x45, 0x13), // Braunes Leder
        OutfitAccent = new SKColor(0xD4, 0x8B, 0x2E), // Gold-Akzent
        HairLength = 0.7f,
        HairStyle = 1, // Lang-glatt
        BodyType = 1,  // Muskulös (Kriegerin)
        AccessoryType = 0, // Schwert
        HasBangs = true,
        BangStyle = 1  // Seitlich
    };

    /// <summary>Aldric: Weißhaariger Erzmagier, weise aber mit dunklem Geheimnis.</summary>
    public static readonly CharacterDefinition Aldric = new()
    {
        Id = "aldric",
        SkinColor = new SKColor(0xE8, 0xD0, 0xB5),
        HairColor = new SKColor(0xE0, 0xE0, 0xE8),  // Weiß/Silber
        EyeColor = new SKColor(0x58, 0xA6, 0xFF),   // Leuchtendes Blau
        OutfitColor = new SKColor(0x2C, 0x1A, 0x4A), // Dunkles Lila
        OutfitAccent = new SKColor(0xF3, 0x9C, 0x12), // Gold
        HairLength = 0.8f,
        HairStyle = 1, // Lang
        BodyType = 2,  // Robe
        AccessoryType = 1 // Stab
        // HasBangs = false (default)
    };

    /// <summary>Kael: Rivale/Freund, braunes Haar, sarkastisch, versteckt Angst hinter Humor.</summary>
    public static readonly CharacterDefinition Kael = new()
    {
        Id = "kael",
        SkinColor = new SKColor(0xD4, 0xA5, 0x74),
        HairColor = new SKColor(0x8B, 0x65, 0x14),  // Braun
        EyeColor = new SKColor(0xF3, 0x9C, 0x12),   // Amber
        OutfitColor = new SKColor(0x4A, 0x3D, 0x2E), // Dunkelbraun
        OutfitAccent = new SKColor(0xF3, 0x9C, 0x12), // Gold
        HairLength = 0.35f,
        HairStyle = 3, // Wild
        BodyType = 0,  // Schlank
        AccessoryType = 2, // Dolche
        HasBangs = true,
        BangStyle = 2  // Lose Strähnen
    };

    /// <summary>Luna: Heilerin mit blauem Haar, sanft, verbirgt immense Macht.</summary>
    public static readonly CharacterDefinition Luna = new()
    {
        Id = "luna",
        SkinColor = new SKColor(0xFA, 0xE0, 0xC8),
        HairColor = new SKColor(0x5D, 0xAE, 0xE3),  // Hellblau
        EyeColor = new SKColor(0xAD, 0x8B, 0xFA),   // Lavendel
        OutfitColor = new SKColor(0xE8, 0xE8, 0xF0), // Weiß
        OutfitAccent = new SKColor(0x5D, 0xAE, 0xE3), // Hellblau
        HairLength = 0.9f,
        HairStyle = 2, // Zopf
        BodyType = 2,  // Robe (Heilergewand)
        AccessoryType = 1, // Stab (Heilstab)
        HasBangs = true,
        BangStyle = 0  // Gerade
    };

    /// <summary>Vex: Dieb/Händler, dunklere Haut, verschlagen, moralisch ambivalent.</summary>
    public static readonly CharacterDefinition Vex = new()
    {
        Id = "vex",
        SkinColor = new SKColor(0xC4, 0x8E, 0x60),
        HairColor = new SKColor(0x2C, 0x2C, 0x2C),  // Schwarz
        EyeColor = new SKColor(0xE7, 0x4C, 0x3C),   // Rot
        OutfitColor = new SKColor(0x34, 0x2E, 0x28), // Dunkelbraun
        OutfitAccent = new SKColor(0xF3, 0x9C, 0x12), // Gold
        HairLength = 0.2f,
        HairStyle = 0, // Kurz
        BodyType = 0,  // Schlank
        AccessoryType = 2 // Dolche
        // HasBangs = false (default)
    };

    // --- Spezial ---

    /// <summary>System-ARIA: Holographischer KI-Geist, blau-transparent, schwebt.</summary>
    public static readonly CharacterDefinition SystemAria = new()
    {
        Id = "system_aria",
        SkinColor = new SKColor(0x4A, 0x90, 0xD9, 180), // Blau-transparent
        HairColor = new SKColor(0x58, 0xA6, 0xFF, 150),
        EyeColor = new SKColor(0x58, 0xA6, 0xFF),
        OutfitColor = new SKColor(0x4A, 0x90, 0xD9, 100),
        OutfitAccent = new SKColor(0x58, 0xA6, 0xFF),
        HairLength = 0.5f,
        HairStyle = 1,
        BodyType = 2,
        AccessoryType = 3, // Keine Waffe
        IsHolographic = true,
        HasBangs = true,
        BangStyle = 1  // Seitlich
    };

    /// <summary>Nihilus: Endgegner, dunkle Haut, schwarze Haare, rot-violett leuchtende Augen, dunkle Robe.</summary>
    public static readonly CharacterDefinition Nihilus = new()
    {
        Id = "nihilus",
        SkinColor = new SKColor(0x2C, 0x2C, 0x3A),  // Dunkle Hautfarbe
        HairColor = new SKColor(0x0A, 0x0A, 0x0A),  // Schwarz
        EyeColor = new SKColor(0x8B, 0x00, 0x00),    // Dunkelrot/violett
        OutfitColor = new SKColor(0x1A, 0x0A, 0x2E), // Dunkle Robe
        OutfitAccent = new SKColor(0xE7, 0x4C, 0x3C), // Blutrot
        HairLength = 0.6f,
        HairStyle = 3, // Wild
        BodyType = 2,  // Robe
        AccessoryType = 3, // Keine Waffe (pure Macht)
        HasGlowingEyes = true,
        AuraColor = new SKColor(0x8B, 0x00, 0x00) // Dunkelrot
    };

    /// <summary>Xaroth: Dunkler Magier, blasse Haut, graue Haare, rote Augen, dunkle Robe mit roten Akzenten.</summary>
    public static readonly CharacterDefinition Xaroth = new()
    {
        Id = "xaroth",
        SkinColor = new SKColor(0xD4, 0xC5, 0xA9),  // Blasse Haut
        HairColor = new SKColor(0x4A, 0x4A, 0x5A),  // Grau
        EyeColor = new SKColor(0xCC, 0x00, 0x00),    // Rot
        OutfitColor = new SKColor(0x2D, 0x1B, 0x4E), // Dunkle Robe
        OutfitAccent = new SKColor(0xCC, 0x33, 0x33), // Rote Akzente
        HairLength = 0.8f,
        HairStyle = 1, // Lang
        BodyType = 2,  // Robe
        AccessoryType = 1, // Stab
        HasBangs = true,
        BangStyle = 1, // Seitlich
        AuraColor = new SKColor(0xCC, 0x00, 0x00) // Rot
    };

    /// <summary>
    /// Alle NPC/Spezial-Definitionen per ID, für Lookup aus Story-JSON.
    /// Protagonist wird separat über GetProtagonist() aufgelöst.
    /// </summary>
    private static readonly Dictionary<string, CharacterDefinition> _byId = new()
    {
        ["aria"] = Aria,
        ["aldric"] = Aldric,
        ["kael"] = Kael,
        ["luna"] = Luna,
        ["vex"] = Vex,
        ["system_aria"] = SystemAria,
        ["nihilus"] = Nihilus,
        ["xaroth"] = Xaroth
    };

    /// <summary>
    /// Gibt die passende Protagonist-Definition für den Klassen-Typ zurück.
    /// </summary>
    public static CharacterDefinition GetProtagonist(int classType) => classType switch
    {
        0 => Protagonist_Sword,
        1 => Protagonist_Mage,
        2 => Protagonist_Assassin,
        _ => Protagonist_Sword
    };

    /// <summary>
    /// Gibt die CharacterDefinition für eine character-ID aus dem Story-JSON zurück.
    /// Für "protagonist" muss GetProtagonist() mit dem Klassen-Index verwendet werden.
    /// </summary>
    public static CharacterDefinition? GetById(string characterId)
    {
        return _byId.TryGetValue(characterId, out var def) ? def : null;
    }
}
