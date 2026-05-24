#nullable enable
using ArcaneKingdom.Domain.Cards;
using UnityEngine;

namespace ArcaneKingdom.Domain.Hero
{
    /// <summary>
    /// Statische Helden-Daten als ScriptableObject (DESIGN.md Kap. 9.6).
    /// Eine Faehigkeit pro Held, mit Cooldown in Runden. Faehigkeit ist mana-frei.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Hero/Hero", fileName = "Hero_")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField, TextArea(2, 4)] private string flavorTextKey = string.Empty;

        [Header("Element")]
        [SerializeField] private Element element = Element.Licht;

        [Header("Faehigkeit")]
        [SerializeField] private string faehigkeitNameKey = string.Empty;
        [SerializeField, TextArea(2, 4)] private string faehigkeitDescKey = string.Empty;
        [SerializeField] private HeroFaehigkeitsTyp faehigkeitsTyp = HeroFaehigkeitsTyp.AllyHeal;
        [SerializeField, Min(1)] private int cooldownRunden = 5;
        [SerializeField, Min(0)] private int magnitude = 0;
        [SerializeField, Min(0)] private int durationTurns = 0;

        [Header("Assets")]
        [SerializeField] private string portraitAddressableKey = string.Empty;
        [SerializeField] private string? voiceLineAddressableKey;

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public string FlavorTextKey => flavorTextKey;
        public Element Element => element;
        public string FaehigkeitNameKey => faehigkeitNameKey;
        public string FaehigkeitDescKey => faehigkeitDescKey;
        public HeroFaehigkeitsTyp FaehigkeitsTyp => faehigkeitsTyp;
        public int CooldownRunden => cooldownRunden;
        public int Magnitude => magnitude;
        public int DurationTurns => durationTurns;
        public string PortraitAddressableKey => portraitAddressableKey;
        public string? VoiceLineAddressableKey => voiceLineAddressableKey;
    }
}
