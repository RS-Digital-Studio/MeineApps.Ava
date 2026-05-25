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
    /// PlayerSave-Service mit lokalem JSON-Backend + Backup. Bereitet sich auf Firebase-
    /// Realtime-Database-Anbindung vor.
    ///
    /// Verbesserungen vs Stufe-1-Stub:
    ///   - Atomic-Write ueber Temp-Datei (kein halbgeschriebenes Save bei App-Crash)
    ///   - .bak-Datei mit dem letzten erfolgreichen Save (Restore bei Korruption)
    ///   - SchemaVersion-Check nach Load + Migration-Versuch via SaveMigrator
    ///
    /// Firebase-Erweiterung (siehe SETUP.md "Firebase-Integration"):
    ///   - Im LoadAsync nach erfolgreichem Local-Read mit Server-Snapshot vergleichen
    ///     (LastSavedAtUtc > Local -> Server-Version uebernehmen)
    ///   - Im SaveAsync zusaetzlich an Firebase-Realtime-DB pushen
    ///   - Conflict-Resolution: Server-Wins bei Differenz &gt; 60s
    /// </summary>
    public sealed class FirebaseSaveService : ISaveService<PlayerSave>
    {
        private const string LocalFileName = "player_save.json";
        private const string BackupFileName = "player_save.bak.json";
        private const string TempFileName = "player_save.tmp.json";

        private readonly IAuthService _auth;
        private readonly JsonSerializerSettings _jsonSettings;
        private PlayerSave? _cache;
        private readonly object _ioLock = new();

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

            // TODO Firebase: Realtime-DB Pull. Wenn Server-Daten vorhanden und neuer
            //                als Local -> Local ueberschreiben, sonst Server-Push.

            var save = await TryLoadFromAsync(LocalPath(), ct)
                       ?? await TryLoadFromAsync(BackupPath(), ct);

            if (save != null)
            {
                _cache = save;
                return Result<PlayerSave>.Success(save);
            }

            // Erst-Initialisierung
            var userId = _auth.CurrentUserId ?? "unknown";
            var displayName = _auth.CurrentUserDisplayName ?? "Gast";
            var fresh = new PlayerSave(new PlayerProfile(userId, displayName, "Poseidon", DateTime.UtcNow));
            _cache = fresh;
            GameLogger.Info("Save", "Neuer Save initialisiert (kein local + kein backup gefunden).");
            return Result<PlayerSave>.Success(fresh);
        }

        public async UniTask<Result> SaveAsync(PlayerSave save, CancellationToken ct = default)
        {
            try
            {
                _cache = save;
                save.LastSavedAtUtc = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(save, Formatting.Indented, _jsonSettings);

                // Atomic-Write: Temp -> Rename. Bei Crash bleibt entweder altes File oder
                // Temp existiert; nie ein halbgeschriebenes Live-File.
                var tempPath = TempPath();
                var livePath = LocalPath();
                var backupPath = BackupPath();

                await File.WriteAllTextAsync(tempPath, json, ct);

                lock (_ioLock)
                {
                    // Aktuelle Live -> Backup (rotiert)
                    if (File.Exists(livePath))
                    {
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                        File.Move(livePath, backupPath);
                    }
                    // Temp -> Live
                    File.Move(tempPath, livePath);
                }

                GameLogger.Verbose("Save", $"Lokal gesichert ({json.Length} bytes).");

                // TODO Firebase: parallel an Realtime-DB pushen
                // (await FirebaseDatabase.DefaultInstance.GetReference($"users/{userId}/save").SetRawJsonValueAsync(json);)
                return Result.Success();
            }
            catch (Exception ex)
            {
                GameLogger.Error("Save", "SaveAsync fehlgeschlagen", ex);
                return Result.Failure(ex);
            }
        }

        public async UniTask<Result<PlayerSave>> MutateAsync(Func<PlayerSave, PlayerSave> mutation,
                                                             CancellationToken ct = default)
        {
            var loadResult = await LoadAsync(ct);
            if (!loadResult.IsSuccess) return loadResult;
            var mutated = mutation(loadResult.Value!);
            var saveResult = await SaveAsync(mutated, ct);
            return saveResult.IsSuccess
                ? Result<PlayerSave>.Success(mutated)
                : Result<PlayerSave>.Failure(saveResult.ErrorMessage ?? "Save failed");
        }

        // -------------------------------------------------------------------- Intern

        private async UniTask<PlayerSave?> TryLoadFromAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(path, ct);
                var save = JsonConvert.DeserializeObject<PlayerSave>(json, _jsonSettings);
                if (save == null)
                {
                    GameLogger.Warning("Save", $"Deserialisierung null fuer {Path.GetFileName(path)}.");
                    return null;
                }

                // SchemaVersion-Check & Migration
                if (save.SchemaVersion < ArcaneKingdom.Domain.Save.SaveMigrator.CurrentSchemaVersion)
                {
                    save = ArcaneKingdom.Domain.Save.SaveMigrator.MigrateToCurrent(save);
                    GameLogger.Info("Save", $"Migriert {Path.GetFileName(path)} auf Schema v{save.SchemaVersion}.");
                }

                GameLogger.Info("Save", $"Geladen aus {Path.GetFileName(path)} ({json.Length} bytes).");
                return save;
            }
            catch (Exception ex)
            {
                GameLogger.Error("Save", $"Load-Fehler bei {Path.GetFileName(path)} — Backup wird probiert", ex);
                return null;
            }
        }

        private static string LocalPath()  => Path.Combine(Application.persistentDataPath, LocalFileName);
        private static string BackupPath() => Path.Combine(Application.persistentDataPath, BackupFileName);
        private static string TempPath()   => Path.Combine(Application.persistentDataPath, TempFileName);
    }
}
