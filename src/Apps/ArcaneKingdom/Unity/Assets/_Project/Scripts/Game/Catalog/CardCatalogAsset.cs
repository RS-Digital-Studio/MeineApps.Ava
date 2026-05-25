#nullable enable
using ArcaneKingdom.Domain.Cards;
using UnityEngine;

namespace ArcaneKingdom.Game.Catalog
{
    /// <summary>
    /// ScriptableObject-Container der alle <see cref="CardDefinition"/>-Assets für
    /// Runtime-Lookup buendelt. Liegt unter Assets/_Project/Resources/CardCatalog.asset
    /// damit Resources.Load es findet.
    ///
    /// Das Field wird per Editor-Menu "ArcaneKingdom > Tools > Sync CardCatalog"
    /// (siehe <c>CardCatalogSyncTool</c>) automatisch mit allen vorhandenen
    /// CardDefinitions gefüllt.
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Catalog/Card Catalog",
                     fileName = "CardCatalog")]
    public sealed class CardCatalogAsset : ScriptableObject
    {
        [SerializeField] private CardDefinition[] cards = System.Array.Empty<CardDefinition>();

        public CardDefinition[] Cards => cards;

#if UNITY_EDITOR
        /// <summary>Wird vom Editor-Sync-Tool aufgerufen.</summary>
        public void SetCards(CardDefinition[] all) => cards = all;
#endif
    }
}
