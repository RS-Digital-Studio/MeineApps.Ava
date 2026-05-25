#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Kompakte JSON-Serialisierung des BattleStates für ReplayService.
    /// Deterministisch: Roundtrip ergibt strukturgleichen State.
    /// </summary>
    public static class BattleStateSerializer
    {
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
                Seed = state.Seed,
                CurrentTurn = state.CurrentTurn,
                PlayerHeroHp = state.PlayerHeroHp,
                EnemyHeroHp = state.EnemyHeroHp,
                PlayerMana = state.PlayerMana,
                EnemyMana = state.EnemyMana,
                PlayerMaxMana = state.PlayerMaxMana,
                EnemyMaxMana = state.EnemyMaxMana,
                Phase = state.Phase,
                Result = state.Result,
                PlayerField = ConvertField(state.PlayerField),
                EnemyField = ConvertField(state.EnemyField),
                PlayerHand = new List<string>(state.PlayerHand),
                EnemyHand = new List<string>(state.EnemyHand),
                PlayerDeck = new List<string>(state.PlayerDeckQueue),
                EnemyDeck = new List<string>(state.EnemyDeckQueue)
            };
            return JsonConvert.SerializeObject(dto, Settings);
        }

        public static BattleState Deserialize(string json)
        {
            var dto = JsonConvert.DeserializeObject<BattleStateDto>(json, Settings) ?? throw new System.InvalidOperationException("Empty BattleStateDto.");
            var state = new BattleState(dto.Seed, dto.PlayerHeroHp, dto.EnemyHeroHp)
            {
                CurrentTurn = dto.CurrentTurn,
                PlayerMana = dto.PlayerMana,
                EnemyMana = dto.EnemyMana,
                PlayerMaxMana = dto.PlayerMaxMana,
                EnemyMaxMana = dto.EnemyMaxMana,
                Phase = dto.Phase,
                Result = dto.Result
            };
            foreach (var s in dto.PlayerField) state.PlayerField.Add(new CardFieldSlot(s.CardInstanceId, s.CurrentAttack, s.CurrentHealth, s.TurnsUntilSpecial));
            foreach (var s in dto.EnemyField) state.EnemyField.Add(new CardFieldSlot(s.CardInstanceId, s.CurrentAttack, s.CurrentHealth, s.TurnsUntilSpecial));
            foreach (var h in dto.PlayerHand) state.PlayerHand.Add(h);
            foreach (var h in dto.EnemyHand) state.EnemyHand.Add(h);
            foreach (var d in dto.PlayerDeck) state.PlayerDeckQueue.Enqueue(d);
            foreach (var d in dto.EnemyDeck) state.EnemyDeckQueue.Enqueue(d);
            return state;
        }

        private static List<FieldSlotDto> ConvertField(IEnumerable<CardFieldSlot> field)
        {
            var list = new List<FieldSlotDto>();
            foreach (var s in field) list.Add(new FieldSlotDto
            {
                CardInstanceId = s.CardInstanceId,
                CurrentAttack = s.CurrentAttack,
                CurrentHealth = s.CurrentHealth,
                TurnsUntilSpecial = s.TurnsUntilSpecial
            });
            return list;
        }

        [System.Serializable]
        private sealed class BattleStateDto
        {
            public int Seed { get; set; }
            public int CurrentTurn { get; set; }
            public int PlayerHeroHp { get; set; }
            public int EnemyHeroHp { get; set; }
            public int PlayerMana { get; set; }
            public int EnemyMana { get; set; }
            public int PlayerMaxMana { get; set; }
            public int EnemyMaxMana { get; set; }
            public BattlePhase Phase { get; set; }
            public BattleResult Result { get; set; }
            public List<FieldSlotDto> PlayerField { get; set; } = new();
            public List<FieldSlotDto> EnemyField { get; set; } = new();
            public List<string> PlayerHand { get; set; } = new();
            public List<string> EnemyHand { get; set; } = new();
            public List<string> PlayerDeck { get; set; } = new();
            public List<string> EnemyDeck { get; set; } = new();
        }

        [System.Serializable]
        private sealed class FieldSlotDto
        {
            public string CardInstanceId { get; set; } = string.Empty;
            public int CurrentAttack { get; set; }
            public int CurrentHealth { get; set; }
            public int TurnsUntilSpecial { get; set; }
        }
    }
}
