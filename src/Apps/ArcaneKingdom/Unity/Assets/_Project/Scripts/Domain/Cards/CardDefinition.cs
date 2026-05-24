#nullable enable
using UnityEngine;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Statische Karten-Daten als ScriptableObject. Bildet alle Karten-Stammdaten ab
    /// (DESIGN.md Kapitel 5). Runtime-Instanzen (mit Level/EXP) sind in <see cref="CardInstance"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Cards/Card", fileName = "Card_")]
    public sealed class CardDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;                  // Stabiler ID-String, z.B. "card_drachenherrscher"
        [SerializeField] private string displayNameKey = string.Empty;       // Localization-Key
        [SerializeField, TextArea(2, 4)] private string flavorTextKey = string.Empty;

        [Header("Klassifikation")]
        [SerializeField] private Element element = Element.Natur;
        [SerializeField] private Rarity rarity = Rarity.Gewoehnlich;
        [SerializeField] private Race race = Race.Koenigreich;

        [Header("Werte (Basis bei LV 0)")]
        [SerializeField, Range(1, 10)] private int cost = 1;                 // Mana-Kosten
        [SerializeField, Min(0)] private int baseAttack = 100;
        [SerializeField, Min(0)] private int baseHealth = 200;
        [SerializeField, Range(1, 10)] private int turnsToSpecial = 4;      // Rundenwarten bis Spezial

        [Header("Faehigkeiten")]
        [SerializeField] private AbilityDefinition? baseAbility;            // LV 0
        [SerializeField] private AbilityDefinition? secondAbility;          // LV 5+
        [SerializeField] private AbilityDefinition? thirdAbility;           // LV 10+

        [Header("Deck-Konstruktion")]
        [SerializeField] private DeckLimit deckLimit = DeckLimit.Unlimited;
        [SerializeField, Min(1)] private int globalCraftLimit = 90;          // NO. X/Y Saison-Limit

        [Header("Assets")]
        [SerializeField] private string artworkAddressableKey = string.Empty;
        [SerializeField] private string? voiceLineAddressableKey;

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public string FlavorTextKey => flavorTextKey;
        public Element Element => element;
        public Rarity Rarity => rarity;
        public Race Race => race;
        public int Cost => cost;
        public int BaseAttack => baseAttack;
        public int BaseHealth => baseHealth;
        public int TurnsToSpecial => turnsToSpecial;
        public AbilityDefinition? BaseAbility => baseAbility;
        public AbilityDefinition? SecondAbility => secondAbility;
        public AbilityDefinition? ThirdAbility => thirdAbility;
        public DeckLimit DeckLimit => deckLimit;
        public int GlobalCraftLimit => globalCraftLimit;
        public string ArtworkAddressableKey => artworkAddressableKey;
        public string? VoiceLineAddressableKey => voiceLineAddressableKey;
    }
}
