#nullable enable
using ArcaneKingdom.Domain.Cards;
using UnityEngine;

namespace ArcaneKingdom.Domain.Hero
{
    /// <summary>
    /// Statische Helden-Daten als ScriptableObject (Designplan v4 Kap. 2.1).
    /// Anders als in v5.x sind Helden-Fähigkeiten in v4 PASSIVE Skills, die für den gesamten Kampf gelten.
    /// Sie sind direkt an die gewählte Rasse gekoppelt — kein Cooldown, kein manuelles Auslösen.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Hero/Hero", fileName = "Hero_")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField, TextArea(2, 4)] private string flavorTextKey = string.Empty;

        [Header("Rassen-Kopplung")]
        [SerializeField] private Race race = Race.Ritter;

        [Header("Passiv-Faehigkeit")]
        [SerializeField] private string faehigkeitNameKey = string.Empty;
        [SerializeField, TextArea(2, 4)] private string faehigkeitDescKey = string.Empty;
        [SerializeField] private HeroFaehigkeitsTyp faehigkeitsTyp = HeroFaehigkeitsTyp.KoeniglicheAura;
        [SerializeField, Min(0)] private int magnitude = 5;     // Effekt-Stärke (z.B. 5% HP, 20% Lebensraub)

        [Header("Assets")]
        [SerializeField] private string portraitAddressableKey = string.Empty;
        [SerializeField] private string? voiceLineAddressableKey;

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public string FlavorTextKey => flavorTextKey;
        public Race Race => race;
        public string FaehigkeitNameKey => faehigkeitNameKey;
        public string FaehigkeitDescKey => faehigkeitDescKey;
        public HeroFaehigkeitsTyp FaehigkeitsTyp => faehigkeitsTyp;
        public int Magnitude => magnitude;
        public string PortraitAddressableKey => portraitAddressableKey;
        public string? VoiceLineAddressableKey => voiceLineAddressableKey;
    }
}
