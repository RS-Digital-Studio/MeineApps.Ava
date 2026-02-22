using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Cards;

/// <summary>
/// Statischer Katalog aller 14 Bomben-Karten.
/// Definiert Rarität, Uses und RESX-Keys für jede Karte.
/// </summary>
public static class CardCatalog
{
    /// <summary>Alle verfügbaren Bomben-Karten</summary>
    public static readonly BombCard[] All =
    [
        // === Original-Bomben (Shop-Migration) ===
        new BombCard
        {
            BombType = BombType.Ice,
            Rarity = Rarity.Common,
            NameKey = "UpgradeIceBomb",
            DescriptionKey = "UpgradeIceBombDesc",
            BaseBronzeUses = 3
        },
        new BombCard
        {
            BombType = BombType.Fire,
            Rarity = Rarity.Common,
            NameKey = "UpgradeFireBomb",
            DescriptionKey = "UpgradeFireBombDesc",
            BaseBronzeUses = 3
        },
        new BombCard
        {
            BombType = BombType.Sticky,
            Rarity = Rarity.Common,
            NameKey = "UpgradeStickyBomb",
            DescriptionKey = "UpgradeStickyBombDesc",
            BaseBronzeUses = 3
        },

        // === Neue Rare-Bomben ===
        new BombCard
        {
            BombType = BombType.Smoke,
            Rarity = Rarity.Rare,
            NameKey = "BombSmoke",
            DescriptionKey = "BombSmokeDesc",
            BaseBronzeUses = 2
        },
        new BombCard
        {
            BombType = BombType.Lightning,
            Rarity = Rarity.Rare,
            NameKey = "BombLightning",
            DescriptionKey = "BombLightningDesc",
            BaseBronzeUses = 2
        },
        new BombCard
        {
            BombType = BombType.Gravity,
            Rarity = Rarity.Rare,
            NameKey = "BombGravity",
            DescriptionKey = "BombGravityDesc",
            BaseBronzeUses = 2
        },
        new BombCard
        {
            BombType = BombType.Poison,
            Rarity = Rarity.Rare,
            NameKey = "BombPoison",
            DescriptionKey = "BombPoisonDesc",
            BaseBronzeUses = 2
        },

        // === Neue Epic-Bomben ===
        new BombCard
        {
            BombType = BombType.TimeWarp,
            Rarity = Rarity.Epic,
            NameKey = "BombTimeWarp",
            DescriptionKey = "BombTimeWarpDesc",
            BaseBronzeUses = 2
        },
        new BombCard
        {
            BombType = BombType.Mirror,
            Rarity = Rarity.Epic,
            NameKey = "BombMirror",
            DescriptionKey = "BombMirrorDesc",
            BaseBronzeUses = 2
        },
        new BombCard
        {
            BombType = BombType.Vortex,
            Rarity = Rarity.Epic,
            NameKey = "BombVortex",
            DescriptionKey = "BombVortexDesc",
            BaseBronzeUses = 2
        },
        new BombCard
        {
            BombType = BombType.Phantom,
            Rarity = Rarity.Epic,
            NameKey = "BombPhantom",
            DescriptionKey = "BombPhantomDesc",
            BaseBronzeUses = 2
        },

        // === Neue Legendary-Bomben ===
        new BombCard
        {
            BombType = BombType.Nova,
            Rarity = Rarity.Legendary,
            NameKey = "BombNova",
            DescriptionKey = "BombNovaDesc",
            BaseBronzeUses = 1
        },
        new BombCard
        {
            BombType = BombType.BlackHole,
            Rarity = Rarity.Legendary,
            NameKey = "BombBlackHole",
            DescriptionKey = "BombBlackHoleDesc",
            BaseBronzeUses = 1
        }
    ];

    /// <summary>Maximale Deck-Slots</summary>
    public const int MaxDeckSlots = 4;

    /// <summary>Karten-Definition für einen BombType finden</summary>
    public static BombCard? GetCard(BombType type)
    {
        foreach (var card in All)
        {
            if (card.BombType == type) return card;
        }
        return null;
    }

    /// <summary>Alle Karten einer bestimmten Rarität</summary>
    public static BombCard[] GetByRarity(Rarity rarity)
    {
        var result = new List<BombCard>();
        foreach (var card in All)
        {
            if (card.Rarity == rarity) result.Add(card);
        }
        return result.ToArray();
    }
}
