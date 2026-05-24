#nullable enable
using System;
using System.IO;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Services
{
    /// <summary>
    /// PlayerSave-Service mit Cloud-First-Strategy + lokalem JSON-Fallback.
    ///
    /// Aktuell Stub — Firebase Realtime Database ist noch nicht im manifest.json.
    /// Lokaler Fallback (JSON unter <c>Application.persistentDataPath</c>) ist funktional
    /// und dient als Bruecke bis Firebase verdrahtet ist.
    /// </summary>
    public sealed class FirebaseSaveService : ISaveService<PlayerSave>
    {
        private const string LocalFileName = "player_save.json";

        private readonly IAuthService _auth;
        private readonly JsonSerializerSettings _jsonSettings;
        private PlayerSave? _cache;

        public FirebaseSaveService(IAuthService auth)
        {
            _auth = auth;
            _jsonSettings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public async UniTask<Result<PlayerSave>> LoadAsync(CancellationToken ct = default)
        {
            if (_cache != null) return Result<PlayerSave>.Success(_cache);

            // TODO: Firebase Realtime DB Read. Aktuell nur lokaler Read.
            var path = LocalPath();
            if (File.Exists(path))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, ct);
                    var save = JsonConvert.DeserializeObject<PlayerSave>(json, _jsonSettings);
                    if (save != null)
                    {
                        _cache = save;
                        GameLogger.Info("Save", $"Lokaler Save geladen ({json.Length} bytes).");
                        return Result<PlayerSave>.Success(save);
                    }
                }
                catch (Exception ex)
                {
                    GameLogger.Error("Save", "Local-Read fehlgeschlagen", ex);
                }
            }

            // Erst-Initialisierung
            var userId = _auth.CurrentUserId ?? "unknown";
            var displayName = _auth.CurrentUserDisplayName ?? "Gast";
            var fresh = new PlayerSave(new PlayerProfile(userId, displayName, "Poseidon", DateTime.UtcNow));
            _cache = fresh;
            GameLogger.Info("Save", "Neuer Save initialisiert.");
            return Result<PlayerSave>.Success(fresh);
        }

        public async UniTask<Result> SaveAsync(PlayerSave save, CancellationToken ct = default)
        {
            try
            {
                _cache = save;
                save.LastSavedAtUtc = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(save, Formatting.Indented, _jsonSettings);
                await File.WriteAllTextAsync(LocalPath(), json, ct);
                GameLogger.Verbose("Save", $"Lokal gesichert ({json.Length} bytes).");
                // TODO: Async Firebase-Write
                return Result.Success();
            }
            catch (Exception ex)
            {
                GameLogger.Error("Save", "SaveAsync fehlgeschlagen", ex);
                return Result.Failure(ex);
            }
        }

        public async UniTask<Result<PlayerSave>> MutateAsync(Func<PlayerSave, PlayerSave> mutation, CancellationToken ct = default)
        {
            var loadResult = await LoadAsync(ct);
            if (!loadResult.IsSuccess) return loadResult;
            var mutated = mutation(loadResult.Value!);
            var saveResult = await SaveAsync(mutated, ct);
            return saveResult.IsSuccess ? Result<PlayerSave>.Success(mutated) : Result<PlayerSave>.Failure(saveResult.ErrorMessage ?? "Save failed");
        }

        private static string LocalPath() => Path.Combine(Application.persistentDataPath, LocalFileName);
    }
}
