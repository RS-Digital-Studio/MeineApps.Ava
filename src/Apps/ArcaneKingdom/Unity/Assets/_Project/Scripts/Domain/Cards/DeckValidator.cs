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
        public enum ValidationCode
        {
            Valid = 0,
            TooManyCards = 1,
            CardLimitExceeded = 2,
            UniqueCardDuplicated = 3,
            UnknownCardInstance = 4,
            UnknownCardDefinition = 5,
            CopyLimitExceeded = 6,
            EmptyDeck = 7
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

            foreach (var instanceId in deck.CardInstanceIds)
            {
                if (!instances.TryGetValue(instanceId, out var inst))
                    return new ValidationResult { Code = ValidationCode.UnknownCardInstance, OffendingCardId = instanceId };

                if (!definitions.TryGetValue(inst.CardDefinitionId, out var def))
                    return new ValidationResult { Code = ValidationCode.UnknownCardDefinition, OffendingCardId = inst.CardDefinitionId };

                if (!perDefinitionCount.ContainsKey(def.Id)) perDefinitionCount[def.Id] = 0;
                perDefinitionCount[def.Id]++;

                totalCost += def.Cost;

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

            return new ValidationResult
            {
                Code = ValidationCode.Valid,
                CardCount = deck.CardInstanceIds.Count,
                TotalCost = totalCost
            };
        }
    }
}
