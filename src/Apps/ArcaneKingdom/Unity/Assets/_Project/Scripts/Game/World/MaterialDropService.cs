#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.World
{
    /// <summary>
    /// Vergibt Material-Karten nach Welt-Node-Sieg. Drop-Tabelle aus
    /// <c>Resources/Data/material_drops.json</c>. Auf Servern würde diese Logik
    /// in einer Cloud Function laufen — der Client-Trigger ist optimistisch.
    /// </summary>
    public sealed class MaterialDropService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly Dictionary<string, NodeMaterialDropTable> _tables = new();
        private readonly System.Random _random = new();

        public MaterialDropService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            LoadTablesFromResources();
        }

        public async Cysharp.Threading.Tasks.UniTask<IReadOnlyList<string>> RollAndAwardAsync(string nodeId, int stars, System.Threading.CancellationToken ct = default)
        {
            if (!_tables.TryGetValue(nodeId, out var table) || table.Drops.Length == 0)
                return Array.Empty<string>();

            var drops = MaterialDropResolver.RollDrops(table, stars, _random);
            if (drops.Count == 0) return drops;

            await _save.MutateAsync(save =>
            {
                foreach (var matId in drops)
                {
                    var instId = Guid.NewGuid().ToString("N");
                    save.CardInventory[instId] = new CardInstance(
                        instanceId: instId,
                        cardDefinitionId: $"material.{matId}",
                        level: 0,
                        expWithinLevel: 0,
                        obtainedAtUtc: DateTime.UtcNow);
                }
                return save;
            }, ct);

            _analytics.Track("material_dropped", new Dictionary<string, object>
            {
                ["node_id"] = nodeId, ["stars"] = stars, ["count"] = drops.Count
            });
            GameLogger.Info("MaterialDrop", $"{nodeId} @ {stars}★ → {drops.Count} Material(ien)");
            return drops;
        }

        private void LoadTablesFromResources()
        {
            var asset = Resources.Load<TextAsset>("Data/material_drops");
            if (asset == null) { GameLogger.Warning("MaterialDrop", "material_drops.json fehlt."); return; }
            try
            {
                var list = JsonConvert.DeserializeObject<List<NodeMaterialDropTable>>(asset.text);
                if (list != null) foreach (var t in list) _tables[t.NodeId] = t;
                GameLogger.Info("MaterialDrop", $"{_tables.Count} Drop-Tabellen geladen.");
            }
            catch (Exception ex)
            {
                GameLogger.Error("MaterialDrop", "Drop-Tabellen-Load fehlgeschlagen", ex);
            }
        }
    }
}
