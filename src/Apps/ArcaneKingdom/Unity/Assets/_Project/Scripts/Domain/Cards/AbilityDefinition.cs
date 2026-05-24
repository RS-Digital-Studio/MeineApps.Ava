#nullable enable
using UnityEngine;

namespace ArcaneKingdom.Domain.Cards
{
    public enum AbilityType
    {
        Passive = 0,
        ActiveOnSpecial = 1
    }

    public enum AbilityCategory
    {
        Damage = 0,
        Defense = 1,
        Control = 2,
        Buff = 3,
        Debuff = 4,
        Synergy = 5
    }

    /// <summary>
    /// Faehigkeit einer Karte als ScriptableObject. Jede Karte hat bis zu 3 Faehigkeiten,
    /// die ab Level 0, 5 bzw. 10 freigeschaltet werden (DESIGN.md Kapitel 5.4).
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Cards/Ability", fileName = "Ability_")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Header("Identitaet")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayNameKey = string.Empty;  // Localization-Key
        [SerializeField, TextArea(3, 6)] private string descriptionKey = string.Empty;

        [Header("Mechanik")]
        [SerializeField] private AbilityType type = AbilityType.Passive;
        [SerializeField] private AbilityCategory category = AbilityCategory.Damage;
        [SerializeField, Min(0)] private int magnitude = 0;          // z.B. Schaden, Heilung, %-Wert
        [SerializeField, Min(0)] private int durationTurns = 0;      // Buff/Debuff Dauer
        [SerializeField] private bool targetsAllAllies = false;
        [SerializeField] private bool targetsAllEnemies = false;

        public string Id => id;
        public string DisplayNameKey => displayNameKey;
        public string DescriptionKey => descriptionKey;
        public AbilityType Type => type;
        public AbilityCategory Category => category;
        public int Magnitude => magnitude;
        public int DurationTurns => durationTurns;
        public bool TargetsAllAllies => targetsAllAllies;
        public bool TargetsAllEnemies => targetsAllEnemies;
    }
}
