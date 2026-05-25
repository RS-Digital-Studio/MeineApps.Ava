#nullable enable
using System;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Trigger fuer Karten-Persoenlichkeit (Designplan v4 Kap. 8).
    /// </summary>
    public enum BattleEventType
    {
        /// <summary>Karte wurde ausgespielt. Triggert <c>OnPlayLineKey</c>.</summary>
        CardPlayed = 0,

        /// <summary>Karte ueberlebt das Kampfende auf eigener Seite. Triggert <c>OnVictoryLineKey</c>.</summary>
        CardVictory = 1,

        /// <summary>Karte stirbt im Kampf. Triggert <c>OnDeathLineKey</c>.</summary>
        CardDied = 2,

        /// <summary>Synergy-Bonus zwischen Karten ausgeloest. Magnitude = HP-Bonus%, BonusCardId = Partner.</summary>
        SynergyActivated = 3,

        /// <summary>Rivalen-Karten treffen aufeinander. Trigger Kampf-Dialog.</summary>
        RivalryClashed = 4,

        /// <summary>Helden-Passiv hat Effekt ausgeloest (z.B. GoettlicherSegen rettet Karte).</summary>
        HeroPassivTriggered = 5
    }

    /// <summary>
    /// Aufzeichnung eines Battle-Events fuer UI/Animation/Sound + Replay-Reconstruction.
    /// Wird vom BattleEngine pro Aktion erzeugt und in <see cref="BattleState.Events"/> persistiert.
    /// </summary>
    [Serializable]
    public sealed class BattleEvent
    {
        public BattleEventType EventType { get; }
        public int Turn { get; }
        public bool ForPlayer { get; }
        public string? CardInstanceId { get; }
        public string? CardDefinitionId { get; }
        public string? LocalizationKey { get; }     // z.B. card.aetherius_allschoepfer.play
        public string? PartnerCardId { get; }       // bei Synergy/Rivalry
        public int Magnitude { get; }               // z.B. Synergy-Bonus-Prozent

        public BattleEvent(BattleEventType eventType, int turn, bool forPlayer,
                            string? cardInstanceId = null,
                            string? cardDefinitionId = null,
                            string? localizationKey = null,
                            string? partnerCardId = null,
                            int magnitude = 0)
        {
            EventType = eventType;
            Turn = turn;
            ForPlayer = forPlayer;
            CardInstanceId = cardInstanceId;
            CardDefinitionId = cardDefinitionId;
            LocalizationKey = localizationKey;
            PartnerCardId = partnerCardId;
            Magnitude = magnitude;
        }
    }
}
