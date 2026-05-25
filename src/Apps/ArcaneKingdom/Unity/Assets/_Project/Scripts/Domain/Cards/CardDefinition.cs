#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Statische Karten-Daten als ScriptableObject. Bildet alle Karten-Stammdaten ab
    /// (Designplan v4 Kap. 4 + Kap. 8 Karten-Persönlichkeit).
    /// Runtime-Instanzen (mit Level/EXP) sind in <see cref="CardInstance"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Cards/Card", fileName = "Card_")]
    public sealed class CardDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;                  // Stabiler ID-String, z.B. "drachenherrscher_goldzahn"
        [SerializeField] private string displayNameKey = string.Empty;       // Localization-Key
        [SerializeField, TextArea(2, 4)] private string flavorTextKey = string.Empty;

        [Header("Klassifikation")]
        [SerializeField] private Element element = Element.Natur;
        [SerializeField] private Rarity rarity = Rarity.Gewoehnlich;
        [SerializeField] private Race race = Race.Ritter;

        [Header("Werte (Basis bei LV 0)")]
        [SerializeField, Range(1, 60)] private int cost = 5;                // Mana-Kosten (1★ ~5, 6★ ~45)
        [SerializeField, Min(0)] private int baseAttack = 100;
        [SerializeField, Min(0)] private int baseHealth = 200;
        [SerializeField, Range(1, 10)] private int turnsToSpecial = 3;      // Rundenwarten bis Spezial-Skill

        [Header("Faehigkeiten")]
        [SerializeField] private AbilityDefinition? baseAbility;            // Skill 1 (Awakening, LV 0)
        [SerializeField] private AbilityDefinition? secondAbility;          // Skill 2 (LV 5+)
        [SerializeField] private AbilityDefinition? thirdAbility;           // Skill 3 (LV 10+)
        [SerializeField] private AbilityDefinition? lastWillAbility;        // Letzter Wille (nur 6★ Mythisch, LV 15 MAX)

        [Header("Deck-Konstruktion")]
        [SerializeField] private DeckLimit deckLimit = DeckLimit.Unlimited;
        [SerializeField, Min(1)] private int globalCraftLimit = 90;          // NO. X/Y Saison-Limit

        [Header("Karten-Persoenlichkeit (Designplan v4 Kap. 8)")]
        [Tooltip("Ab 3 Sternen Pflicht. Wird beim Einsetzen der Karte angezeigt/gesprochen.")]
        [SerializeField] private string? onPlayLineKey;
        [Tooltip("Wird nach gewonnenem Kampf angezeigt.")]
        [SerializeField] private string? onVictoryLineKey;
        [Tooltip("Letzter Spruch wenn die Karte stirbt.")]
        [SerializeField] private string? onDeathLineKey;
        [Tooltip("Rivalen-Karten — beim Aufeinandertreffen wird ein spezieller Dialog ausgeloest (Karten-IDs).")]
        [SerializeField] private List<string> rivalCardIds = new();
        [Tooltip("Synergy-Karten — gemeinsam im Deck loesen sie einen kleinen Bonus aus (Karten-IDs).")]
        [SerializeField] private List<string> synergyCardIds = new();

        [Header("Ökosystem-Marker (Designplan v4 Karten-Oekosystem)")]
        [Tooltip("Event-Karten haben einen saisonalen Rahmen und sind nur waehrend eines Events erspielbar.")]
        [SerializeField] private bool isEventCard = false;
        [Tooltip("Premium-Karten kommen nur aus dem Diamanten-Shop. Koennen NICHT fuer Fusion verwendet werden.")]
        [SerializeField] private bool isPremiumCard = false;
        [Tooltip("Prestige-IV-Karten — pro Welt eine, exklusive Endgame-Belohnung.")]
        [SerializeField] private bool isPrestigeCard = false;
        [Tooltip("Sternkarten-Tempel-Exklusive — nur durch Sternpunkte aus Login-Belohnungen.")]
        [SerializeField] private bool isStarTempleCard = false;
        [Tooltip("Saison-Pass-Karten — exklusiv ueber Stufe 15 (Free) oder Stufe 30 (Premium).")]
        [SerializeField] private bool isSaisonPassCard = false;

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
        public AbilityDefinition? LastWillAbility => lastWillAbility;
        public DeckLimit DeckLimit => deckLimit;
        public int GlobalCraftLimit => globalCraftLimit;
        public string? OnPlayLineKey => string.IsNullOrEmpty(onPlayLineKey) ? null : onPlayLineKey;
        public string? OnVictoryLineKey => string.IsNullOrEmpty(onVictoryLineKey) ? null : onVictoryLineKey;
        public string? OnDeathLineKey => string.IsNullOrEmpty(onDeathLineKey) ? null : onDeathLineKey;
        public IReadOnlyList<string> RivalCardIds => rivalCardIds;
        public IReadOnlyList<string> SynergyCardIds => synergyCardIds;
        public bool IsEventCard => isEventCard;
        public bool IsPremiumCard => isPremiumCard;
        public bool IsPrestigeCard => isPrestigeCard;
        public bool IsStarTempleCard => isStarTempleCard;
        public bool IsSaisonPassCard => isSaisonPassCard;
        public string ArtworkAddressableKey => artworkAddressableKey;
        public string? VoiceLineAddressableKey => voiceLineAddressableKey;

        /// <summary>Liefert true, wenn die Karte ueber irgendein exklusives Oekosystem (Event/Premium/Prestige/Sternkarten/Saison) erworben wird.</summary>
        public bool IsExclusive => isEventCard || isPremiumCard || isPrestigeCard || isStarTempleCard || isSaisonPassCard;

        /// <summary>Premium-Karten koennen nicht fuer Fusion verwendet werden (Designplan v4 Kap. 3.2 letzter Hinweis).</summary>
        public bool CanBeUsedInFusion => !isPremiumCard;
    }
}
