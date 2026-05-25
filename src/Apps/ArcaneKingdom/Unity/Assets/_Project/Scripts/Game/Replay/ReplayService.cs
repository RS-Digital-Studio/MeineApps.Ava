#nullable enable
using System;
using System.IO;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Replay;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Replay
{
    /// <summary>
    /// Schreibt Replay-Snapshots zu einem Kampf in den persistentDataPath.
    /// Async-Upload zum Backend folgt in MVP-Phase (Firebase Storage).
    /// </summary>
    public sealed class ReplayService
    {
        private readonly JsonSerializerSettings _jsonSettings = new()
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        public ReplayFile StartRecording(int seed, string matchType, string playerAId, string playerBId)
        {
            var file = new ReplayFile
            {
                ReplayId = Guid.NewGuid().ToString("N").Substring(0, 16),
                Seed = seed,
                MatchType = matchType,
                PlayerAId = playerAId,
                PlayerBId = playerBId,
                StartedAtUtc = DateTime.UtcNow
            };
            GameLogger.Verbose("Replay", $"Start {file.ReplayId} ({matchType})");
            return file;
        }

        public void CaptureSnapshot(ReplayFile file, BattleState state)
        {
            var snapshot = new ReplaySnapshot
            {
                Turn = state.CurrentTurn,
                PlayerHeroHp = state.PlayerHeroHp,
                EnemyHeroHp = state.EnemyHeroHp,
                PlayerMana = state.PlayerMana,
                EnemyMana = state.EnemyMana,
                Phase = state.Phase,
                CapturedAtUtc = DateTime.UtcNow
            };
            foreach (var s in state.PlayerField)
                snapshot.PlayerField.Add(new ReplayFieldSnapshot { CardInstanceId = s.CardInstanceId, CurrentAttack = s.CurrentAttack, CurrentHealth = s.CurrentHealth, TurnsUntilSpecial = s.TurnsUntilSpecial });
            foreach (var s in state.EnemyField)
                snapshot.EnemyField.Add(new ReplayFieldSnapshot { CardInstanceId = s.CardInstanceId, CurrentAttack = s.CurrentAttack, CurrentHealth = s.CurrentHealth, TurnsUntilSpecial = s.TurnsUntilSpecial });
            file.Snapshots.Add(snapshot);
        }

        public void FinishRecording(ReplayFile file, BattleResult result)
        {
            file.EndedAtUtc = DateTime.UtcNow;
            file.Result = result;
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "Replays");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{file.ReplayId}.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(file, _jsonSettings));
                GameLogger.Info("Replay", $"Gespeichert: {path} ({file.Snapshots.Count} Runden)");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Replay", $"Speichern fehlgeschlagen ({file.ReplayId})", ex);
            }
        }

        public ReplayFile? Load(string replayId)
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "Replays", $"{replayId}.json");
                if (!File.Exists(path)) return null;
                return JsonConvert.DeserializeObject<ReplayFile>(File.ReadAllText(path), _jsonSettings);
            }
            catch (Exception ex)
            {
                GameLogger.Error("Replay", $"Laden fehlgeschlagen ({replayId})", ex);
                return null;
            }
        }

        public int PurgeExpired()
        {
            var dir = Path.Combine(Application.persistentDataPath, "Replays");
            if (!Directory.Exists(dir)) return 0;
            var purged = 0;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var replay = JsonConvert.DeserializeObject<ReplayFile>(File.ReadAllText(file), _jsonSettings);
                    if (replay != null && replay.IsExpired) { File.Delete(file); purged++; }
                }
                catch { /* defekte Datei löschen */ File.Delete(file); purged++; }
            }
            if (purged > 0) GameLogger.Info("Replay", $"{purged} abgelaufene Replays geloescht.");
            return purged;
        }
    }
}
