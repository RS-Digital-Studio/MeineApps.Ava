#nullable enable
using System.Collections.Generic;
using UnityEngine;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Welt-Definition als ScriptableObject (Designplan v4 Kap. 3.5 + Story v4).
    /// Jede Welt hat 10 Nodes, ein dominantes Element, eine Säule, einen Story-Boss
    /// und endet mit einem Erinnerungs-Fragment für den Spieler.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/World/World", fileName = "World_")]
    public sealed class WorldDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField] private int index = 1;                 // 1–10

        [Header("Thema (Designplan v4 Kap. 3.5)")]
        [SerializeField] private Element themeElement = Element.Natur;
        [SerializeField] private Element recommendedCounterElement = Element.Feuer;
        [SerializeField, Min(1)] private int recommendedPlayerLevel = 1;
        [SerializeField] private string backgroundAddressableKey = string.Empty;
        [SerializeField] private string musicAddressableKey = string.Empty;

        [Header("Story (Story v4)")]
        [Tooltip("Name der elementaren Säule die in dieser Welt korrumpiert ist (Lebensbaum, Urkern, etc.).")]
        [SerializeField] private string saeuleNameKey = string.Empty;
        [Tooltip("Story-Boss-Karte (Karten-ID).")]
        [SerializeField] private string bossCardId = string.Empty;
        [Tooltip("Story-Zusammenfassung der Welt (Localization-Key).")]
        [SerializeField, TextArea(2, 5)] private string storySummaryKey = string.Empty;
        [Tooltip("Erinnerungs-Fragment das beim Welt-Abschluss freigeschaltet wird (Localization-Key).")]
        [SerializeField, TextArea(2, 5)] private string memoryFragmentKey = string.Empty;
        [Tooltip("Rassen-Mentor-NPC (Localization-Key fuer Name).")]
        [SerializeField] private string mentorNpcKey = string.Empty;

        [Header("Prestige (Designplan v4 Oeko-Kap. 6)")]
        [Tooltip("Basis-Tagesgold das diese Welt im Idle-Income generiert (Normal-Stufe). Skaliert mit Prestige.")]
        [SerializeField, Min(0)] private int baseGoldPerDay = 100;
        [Tooltip("Karten-ID der exklusiven Prestige-IV-Karte dieser Welt (3–4★).")]
        [SerializeField] private string prestige4CardId = string.Empty;

        [Header("Nodes")]
        [SerializeField] private List<NodeDefinition> nodes = new(10);

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public int Index => index;
        public Element ThemeElement => themeElement;
        public Element RecommendedCounterElement => recommendedCounterElement;
        public int RecommendedPlayerLevel => recommendedPlayerLevel;
        public string BackgroundAddressableKey => backgroundAddressableKey;
        public string MusicAddressableKey => musicAddressableKey;
        public string SaeuleNameKey => saeuleNameKey;
        public string BossCardId => bossCardId;
        public string StorySummaryKey => storySummaryKey;
        public string MemoryFragmentKey => memoryFragmentKey;
        public string MentorNpcKey => mentorNpcKey;
        public int BaseGoldPerDay => baseGoldPerDay;
        public string Prestige4CardId => prestige4CardId;
        public IReadOnlyList<NodeDefinition> Nodes => nodes;
    }
}
