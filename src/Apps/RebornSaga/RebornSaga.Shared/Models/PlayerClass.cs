namespace RebornSaga.Models;

using RebornSaga.Models.Enums;

/// <summary>
/// Definition einer Spieler-Klasse mit Basis-Stats und Auto-Bonus pro Level.
/// </summary>
public class PlayerClass
{
    public ClassName Name { get; init; }
    public string NameKey { get; init; } = "";
    public string DescriptionKey { get; init; } = "";

    // Basis-Stats auf Level 1
    public int BaseHp { get; init; }
    public int BaseMp { get; init; }
    public int BaseAtk { get; init; }
    public int BaseDef { get; init; }
    public int BaseInt { get; init; }
    public int BaseSpd { get; init; }
    public int BaseLuk { get; init; }

    // Auto-Bonus pro Level-Up (wird automatisch addiert)
    public int HpPerLevel { get; init; }
    public int MpPerLevel { get; init; }
    public int AtkPerLevel { get; init; }
    public int DefPerLevel { get; init; }
    public int IntPerLevel { get; init; }
    public int SpdPerLevel { get; init; }
    public int LukPerLevel { get; init; }

    /// <summary>Vordefinierte Klassen.</summary>
    public static readonly PlayerClass Swordmaster = new()
    {
        Name = ClassName.Swordmaster,
        NameKey = "class_swordmaster",
        DescriptionKey = "class_swordmaster_desc",
        BaseHp = 120, BaseMp = 30, BaseAtk = 15, BaseDef = 12,
        BaseInt = 5, BaseSpd = 10, BaseLuk = 8,
        HpPerLevel = 12, MpPerLevel = 3, AtkPerLevel = 3, DefPerLevel = 2,
        IntPerLevel = 1, SpdPerLevel = 1, LukPerLevel = 1
    };

    public static readonly PlayerClass Arcanist = new()
    {
        Name = ClassName.Arcanist,
        NameKey = "class_arcanist",
        DescriptionKey = "class_arcanist_desc",
        BaseHp = 80, BaseMp = 80, BaseAtk = 5, BaseDef = 6,
        BaseInt = 18, BaseSpd = 8, BaseLuk = 8,
        HpPerLevel = 6, MpPerLevel = 10, AtkPerLevel = 1, DefPerLevel = 1,
        IntPerLevel = 4, SpdPerLevel = 1, LukPerLevel = 1
    };

    public static readonly PlayerClass Shadowblade = new()
    {
        Name = ClassName.Shadowblade,
        NameKey = "class_shadowblade",
        DescriptionKey = "class_shadowblade_desc",
        BaseHp = 90, BaseMp = 40, BaseAtk = 12, BaseDef = 7,
        BaseInt = 8, BaseSpd = 16, BaseLuk = 14,
        HpPerLevel = 8, MpPerLevel = 4, AtkPerLevel = 2, DefPerLevel = 1,
        IntPerLevel = 1, SpdPerLevel = 3, LukPerLevel = 2
    };

    /// <summary>Gibt die Klassen-Definition für einen Enum-Wert zurück.</summary>
    public static PlayerClass Get(ClassName name) => name switch
    {
        ClassName.Swordmaster => Swordmaster,
        ClassName.Arcanist => Arcanist,
        ClassName.Shadowblade => Shadowblade,
        _ => Swordmaster
    };

    /// <summary>Gibt die Klassen-Definition für den Klassen-Index zurück (0/1/2).</summary>
    public static PlayerClass Get(int classType) => classType switch
    {
        0 => Swordmaster,
        1 => Arcanist,
        2 => Shadowblade,
        _ => Swordmaster
    };
}
