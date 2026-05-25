#nullable enable
using UnityEngine;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Runes
{
    /// <summary>
    /// Statische Runen-Daten als ScriptableObject. Effekt-Magnitude skaliert
    /// mit Rune-Level (siehe RuneInstance).
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Runes/Rune", fileName = "Rune_")]
    public sealed class RuneDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField, TextArea(2, 4)] private string descriptionKey = string.Empty;

        [Header("Mechanik")]
        [SerializeField] private RuneType type = RuneType.Angriff;
        [SerializeField] private Rarity rarity = Rarity.Gewoehnlich;

        /// <summary>Effekt-Magnitude bei LV 1.</summary>
        [SerializeField, Min(0)] private float baseMagnitude = 5f;

        /// <summary>Magnitude-Zuwachs pro Level (linear).</summary>
        [SerializeField, Min(0)] private float magnitudePerLevel = 1f;

        /// <summary>Nur für Element-Runen: das Ziel-Element.</summary>
        [SerializeField] private Element elementTarget = Element.Natur;

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public string DescriptionKey => descriptionKey;
        public RuneType Type => type;
        public Rarity Rarity => rarity;
        public float BaseMagnitude => baseMagnitude;
        public float MagnitudePerLevel => magnitudePerLevel;
        public Element ElementTarget => elementTarget;

        public float CalculateMagnitudeAtLevel(int level)
        {
            level = Mathf.Clamp(level, 1, 10);
            return baseMagnitude + magnitudePerLevel * (level - 1);
        }
    }
}
