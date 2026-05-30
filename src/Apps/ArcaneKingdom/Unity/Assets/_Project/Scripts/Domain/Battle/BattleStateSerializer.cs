#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Hero;
using Newtonsoft.Json;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Kompakte JSON-Serialisierung des BattleStates fuer ReplayService.
    /// Deterministisch und VERLUSTFREI: ein Roundtrip ergibt einen strukturgleichen State
    /// inkl. Status-Effekten, MaxHealth, Helden-Passiv-Kontext (mit mutablen Laufzeitfeldern),
    /// Boss-Phasen-Zustand und der vollstaendigen Event-Spur.
    /// </summary>
    public static class BattleStateSerializer
    {
        /// <summary>Schema-Version des serialisierten Formats (fuer kuenftige Migrationen).</summary>
        public const int SchemaVersion = 1;

        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static string Serialize(BattleState state)
        {
            var dto = new BattleStateDto
            {
                SchemaVersion = SchemaVersion,
                Seed = state.Seed,
                CurrentTurn = state.CurrentTurn,
                PlayerHeroHp = state.PlayerHeroHp,
                EnemyHeroHp = state.EnemyHeroHp,
                PlayerHeroMaxHp = state.PlayerHeroMaxHp,
                EnemyHeroMaxHp = state.EnemyHeroMaxHp,
                PlayerMana = state.PlayerMana,
                EnemyMana = state.EnemyMana,
                PlayerMaxMana = state.PlayerMaxMana,
                EnemyMaxMana = state.EnemyMaxMana,
                PlayerCardsPlayedThisTurn = state.PlayerCardsPlayedThisTurn,
                EnemyCardsPlayedThisTurn = state.EnemyCardsPlayedThisTurn,
                EnemyStatMultiplier = state.EnemyStatMultiplier,
                Phase = state.Phase,
                Result = state.Result,
                IsBossEncounter = state.IsBossEncounter,
                BossPhase2Active = state.BossPhase2Active,
                BossPhase2PassiveKey = state.BossPhase2PassiveKey,
                BossPhase2ReinforcementCardIds = new List<string>(state.BossPhase2ReinforcementCardIds),
                PlayerField = ConvertField(state.PlayerField),
                EnemyField = ConvertField(state.EnemyField),
                PlayerHand = new List<string>(state.PlayerHand),
                EnemyHand = new List<string>(state.EnemyHand),
                PlayerDeck = new List<string>(state.PlayerDeckQueue),
                EnemyDeck = new List<string>(state.EnemyDeckQueue),
                PlayerHeroPassiv = ConvertPassiv(state.PlayerHeroPassiv),
                EnemyHeroPassiv = ConvertPassiv(state.EnemyHeroPassiv),
                Events = ConvertEvents(state.Events)
            };
            return JsonConvert.SerializeObject(dto, Settings);
        }

        public static BattleState Deserialize(string json)
        {
            var dto = JsonConvert.DeserializeObject<BattleStateDto>(json, Settings)
                      ?? throw new System.InvalidOperationException("Empty BattleStateDto.");

            var state = new BattleState(dto.Seed, dto.PlayerHeroHp, dto.EnemyHeroHp)
            {
                CurrentTurn = dto.CurrentTurn,
                PlayerHeroMaxHp = dto.PlayerHeroMaxHp,
                EnemyHeroMaxHp = dto.EnemyHeroMaxHp,
                PlayerMana = dto.PlayerMana,
                EnemyMana = dto.EnemyMana,
                PlayerMaxMana = dto.PlayerMaxMana,
                EnemyMaxMana = dto.EnemyMaxMana,
                PlayerCardsPlayedThisTurn = dto.PlayerCardsPlayedThisTurn,
                EnemyCardsPlayedThisTurn = dto.EnemyCardsPlayedThisTurn,
                EnemyStatMultiplier = dto.EnemyStatMultiplier,
                Phase = dto.Phase,
                Result = dto.Result,
                IsBossEncounter = dto.IsBossEncounter,
                BossPhase2Active = dto.BossPhase2Active,
                BossPhase2PassiveKey = dto.BossPhase2PassiveKey,
                PlayerHeroPassiv = RestorePassiv(dto.PlayerHeroPassiv),
                EnemyHeroPassiv = RestorePassiv(dto.EnemyHeroPassiv)
            };

            foreach (var id in dto.BossPhase2ReinforcementCardIds) state.BossPhase2ReinforcementCardIds.Add(id);
            foreach (var s in dto.PlayerField) state.PlayerField.Add(RestoreSlot(s));
            foreach (var s in dto.EnemyField) state.EnemyField.Add(RestoreSlot(s));
            foreach (var h in dto.PlayerHand) state.PlayerHand.Add(h);
            foreach (var h in dto.EnemyHand) state.EnemyHand.Add(h);
            foreach (var d in dto.PlayerDeck) state.PlayerDeckQueue.Enqueue(d);
            foreach (var d in dto.EnemyDeck) state.EnemyDeckQueue.Enqueue(d);
            foreach (var e in dto.Events) state.Events.Add(RestoreEvent(e));
            return state;
        }

        // ------------------------------------------------------------------ Field

        private static List<FieldSlotDto> ConvertField(IEnumerable<CardFieldSlot> field)
        {
            var list = new List<FieldSlotDto>();
            foreach (var s in field)
            {
                var slot = new FieldSlotDto
                {
                    CardInstanceId = s.CardInstanceId,
                    CurrentAttack = s.CurrentAttack,
                    CurrentHealth = s.CurrentHealth,
                    MaxHealth = s.MaxHealth,
                    TurnsUntilSpecial = s.TurnsUntilSpecial,
                    StatusEffects = new List<StatusEffectDto>()
                };
                foreach (var fx in s.StatusEffects)
                    slot.StatusEffects.Add(new StatusEffectDto
                    {
                        Type = fx.Type,
                        Magnitude = fx.Magnitude,
                        RemainingTurns = fx.RemainingTurns,
                        SourceCardId = fx.SourceCardId
                    });
                list.Add(slot);
            }
            return list;
        }

        private static CardFieldSlot RestoreSlot(FieldSlotDto s)
        {
            var slot = new CardFieldSlot(s.CardInstanceId, s.CurrentAttack, s.CurrentHealth, s.TurnsUntilSpecial)
            {
                MaxHealth = s.MaxHealth   // Ctor setzt MaxHealth=CurrentHealth — hier auf den echten Wert ueberschreiben
            };
            foreach (var fx in s.StatusEffects)
                slot.StatusEffects.Add(new StatusEffect(fx.Type, fx.RemainingTurns, fx.Magnitude, fx.SourceCardId));
            return slot;
        }

        // ------------------------------------------------------------------ Hero-Passiv

        private static HeroPassivDto? ConvertPassiv(HeroPassivContext? passiv)
        {
            if (passiv == null) return null;
            return new HeroPassivDto
            {
                PassivType = passiv.PassivType,
                Magnitude = passiv.Magnitude,
                BeastSpiritCountInDeck = passiv.BeastSpiritCountInDeck,
                FirstCardThisTurnPlayed = passiv.FirstCardThisTurnPlayed,
                DivineBlessingsRemaining = passiv.DivineBlessingsRemaining
            };
        }

        private static HeroPassivContext? RestorePassiv(HeroPassivDto? dto)
        {
            if (dto == null) return null;
            return new HeroPassivContext(dto.PassivType, dto.Magnitude, dto.BeastSpiritCountInDeck)
            {
                FirstCardThisTurnPlayed = dto.FirstCardThisTurnPlayed,
                DivineBlessingsRemaining = dto.DivineBlessingsRemaining
            };
        }

        // ------------------------------------------------------------------ Events

        private static List<BattleEventDto> ConvertEvents(IEnumerable<BattleEvent> events)
        {
            var list = new List<BattleEventDto>();
            foreach (var e in events)
                list.Add(new BattleEventDto
                {
                    EventType = e.EventType,
                    Turn = e.Turn,
                    ForPlayer = e.ForPlayer,
                    CardInstanceId = e.CardInstanceId,
                    CardDefinitionId = e.CardDefinitionId,
                    LocalizationKey = e.LocalizationKey,
                    PartnerCardId = e.PartnerCardId,
                    Magnitude = e.Magnitude
                });
            return list;
        }

        private static BattleEvent RestoreEvent(BattleEventDto e) => new(
            e.EventType, e.Turn, e.ForPlayer,
            cardInstanceId: e.CardInstanceId,
            cardDefinitionId: e.CardDefinitionId,
            localizationKey: e.LocalizationKey,
            partnerCardId: e.PartnerCardId,
            magnitude: e.Magnitude);

        // ------------------------------------------------------------------ DTOs

        [System.Serializable]
        private sealed class BattleStateDto
        {
            public int SchemaVersion { get; set; }
            public int Seed { get; set; }
            public int CurrentTurn { get; set; }
            public int PlayerHeroHp { get; set; }
            public int EnemyHeroHp { get; set; }
            public int PlayerHeroMaxHp { get; set; }
            public int EnemyHeroMaxHp { get; set; }
            public int PlayerMana { get; set; }
            public int EnemyMana { get; set; }
            public int PlayerMaxMana { get; set; }
            public int EnemyMaxMana { get; set; }
            public int PlayerCardsPlayedThisTurn { get; set; }
            public int EnemyCardsPlayedThisTurn { get; set; }
            public float EnemyStatMultiplier { get; set; } = 1.0f;
            public BattlePhase Phase { get; set; }
            public BattleResult Result { get; set; }
            public bool IsBossEncounter { get; set; }
            public bool BossPhase2Active { get; set; }
            public string? BossPhase2PassiveKey { get; set; }
            public List<string> BossPhase2ReinforcementCardIds { get; set; } = new();
            public List<FieldSlotDto> PlayerField { get; set; } = new();
            public List<FieldSlotDto> EnemyField { get; set; } = new();
            public List<string> PlayerHand { get; set; } = new();
            public List<string> EnemyHand { get; set; } = new();
            public List<string> PlayerDeck { get; set; } = new();
            public List<string> EnemyDeck { get; set; } = new();
            public HeroPassivDto? PlayerHeroPassiv { get; set; }
            public HeroPassivDto? EnemyHeroPassiv { get; set; }
            public List<BattleEventDto> Events { get; set; } = new();
        }

        [System.Serializable]
        private sealed class FieldSlotDto
        {
            public string CardInstanceId { get; set; } = string.Empty;
            public int CurrentAttack { get; set; }
            public int CurrentHealth { get; set; }
            public int MaxHealth { get; set; }
            public int TurnsUntilSpecial { get; set; }
            public List<StatusEffectDto> StatusEffects { get; set; } = new();
        }

        [System.Serializable]
        private sealed class StatusEffectDto
        {
            public StatusEffectType Type { get; set; }
            public int Magnitude { get; set; }
            public int RemainingTurns { get; set; }
            public string? SourceCardId { get; set; }
        }

        [System.Serializable]
        private sealed class HeroPassivDto
        {
            public HeroFaehigkeitsTyp PassivType { get; set; }
            public int Magnitude { get; set; }
            public int BeastSpiritCountInDeck { get; set; }
            public bool FirstCardThisTurnPlayed { get; set; }
            public int DivineBlessingsRemaining { get; set; }
        }

        [System.Serializable]
        private sealed class BattleEventDto
        {
            public BattleEventType EventType { get; set; }
            public int Turn { get; set; }
            public bool ForPlayer { get; set; }
            public string? CardInstanceId { get; set; }
            public string? CardDefinitionId { get; set; }
            public string? LocalizationKey { get; set; }
            public string? PartnerCardId { get; set; }
            public int Magnitude { get; set; }
        }
    }
}
