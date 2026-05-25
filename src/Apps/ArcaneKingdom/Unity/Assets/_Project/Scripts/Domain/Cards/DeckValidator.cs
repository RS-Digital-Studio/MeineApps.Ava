#nullable enable
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Prüft Decks gegen die Konstruktions-Regeln (DESIGN.md Kap. 5.5):
    /// max 10 Karten, Deck-Limits (1/deck, MaxTwo, Unlimited), max 3 Kopien pro Karte.
    /// Optional: COST-Budget-Prüfung.
    /// </summary>
    public static class DeckValidator
    {
        /// <summary>
        /// Maximales Gesamt-COST-Budget pro Deck (Arcane_Legends_Designplan Kap. 9).
        /// </summary>
        public const int MaxDeckCost = 200;

        public enum ValidationCode
        {
            Valid = 0,
            TooManyCards = 1,
            CardLimitExceeded = 2,
            UniqueCardDuplicated = 3,
            UnknownCardInstance = 4,
            UnknownCardDefinition = 5,
            CopyLimitExceeded = 6,
            EmptyDeck = 7,
            /// <summary>Gesamt-COST des Decks ueberschreitet MaxDeckCost (200).</summary>
            CostBudgetExceeded = 8,
            /// <summary>Zu viele Legendaere Karten im Deck (max 2 laut V1-Plan).</summary>
            TooManyLegendaries = 9,
            /// <summary>Zu viele Epic-Karten im Deck (max 3).</summary>
            TooManyEpics = 10
        }

        public sealed class ValidationResult
        {
            public ValidationCode Code { get; init; }
            public string? OffendingCardId { get; init; }
            public int CardCount { get; init; }
            public int TotalCost { get; init; }
            public bool IsValid => Code == ValidationCode.Valid;
        }

        public const int MaxCopiesFarmable = 3;

        public static ValidationResult Validate(
            Deck deck,
            IReadOnlyDictionary<string, CardInstance> instances,
            IReadOnlyDictionary<string, CardDefinition> definitions)
        {
            if (deck.CardInstanceIds.Count == 0)
                return new ValidationResult { Code = ValidationCode.EmptyDeck };

            if (deck.CardInstanceIds.Count > Deck.MaxCards)
                return new ValidationResult { Code = ValidationCode.TooManyCards, CardCount = deck.CardInstanceIds.Count };

            var perDefinitionCount = new Dictionary<string, int>();
            var totalCost = 0;
            var legendaryCount = 0;
            var epicCount = 0;

            foreach (var instanceId in deck.CardInstanceIds)
            {
                if (!instances.TryGetValue(instanceId, out var inst))
                    return new ValidationResult { Code = ValidationCode.UnknownCardInstance, OffendingCardId = instanceId };

                if (!definitions.TryGetValue(inst.CardDefinitionId, out var def))
                    return new ValidationResult { Code = ValidationCode.UnknownCardDefinition, OffendingCardId = inst.CardDefinitionId };

                if (!perDefinitionCount.ContainsKey(def.Id)) perDefinitionCount[def.Id] = 0;
                perDefinitionCount[def.Id]++;

                totalCost += def.Cost;

                // Seltenheits-Limits laut V1-Plan Kap. 9
                if (def.Rarity == Rarity.Legendaer || def.Rarity == Rarity.Mythisch) legendaryCount++;
                else if (def.Rarity == Rarity.Epic) epicCount++;

                var limit = def.DeckLimit switch
                {
                    DeckLimit.OneOnly => 1,
                    DeckLimit.MaxTwo => 2,
                    DeckLimit.Unlimited => MaxCopiesFarmable,
                    _ => MaxCopiesFarmable
                };

                if (perDefinitionCount[def.Id] > limit)
                {
                    var code = def.DeckLimit switch
                    {
                        DeckLimit.OneOnly => ValidationCode.UniqueCardDuplicated,
                        DeckLimit.MaxTwo => ValidationCode.CardLimitExceeded,
                        _ => ValidationCode.CopyLimitExceeded
                    };
                    return new ValidationResult { Code = code, OffendingCardId = def.Id, CardCount = deck.CardInstanceIds.Count, TotalCost = totalCost };
                }
            }

            // COST-Budget pruefen
            if (totalCost > MaxDeckCost)
                return new ValidationResult
                {
                    Code = ValidationCode.CostBudgetExceeded,
                    CardCount = deck.CardInstanceIds.Count,
                    TotalCost = totalCost
                };

            // Legendary-Limit pruefen (max 2)
            if (legendaryCount > 2)
                return new ValidationResult
                {
                    Code = ValidationCode.TooManyLegendaries,
                    CardCount = deck.CardInstanceIds.Count,
                    TotalCost = totalCost
                };

            // Epic-Limit pruefen (max 3)
            if (epicCount > 3)
                return new ValidationResult
                {
                    Code = ValidationCode.TooManyEpics,
                    CardCount = deck.CardInstanceIds.Count,
                    TotalCost = totalCost
                };

            return new ValidationResult
            {
                Code = ValidationCode.Valid,
                CardCount = deck.CardInstanceIds.Count,
                TotalCost = totalCost
            };
        }
    }
}
