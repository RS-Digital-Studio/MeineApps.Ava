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
    ///   - Atomic-Write über Temp-Datei (kein halbgeschriebenes Save bei App-Crash)
    ///   - .bak-Datei mit dem letzten erfolgreichen Save (Restore bei Korruption)
    ///   - SchemaVersion-Check nach Load + Migration-Versuch via SaveMigrator
    ///
    /// Firebase-Erweiterung (siehe SETUP.md "Firebase-Integration"):
    ///   - Im LoadAsync nach erfolgreichem Local-Read mit Server-Snapshot vergleichen
    ///     (LastSavedAtUtc > Local -> Server-Version übernehmen)
    ///   - Im SaveAsync zusätzlich an Firebase-Realtime-DB pushen
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
        // Serialisiert den kompletten Read-Modify-Write von MutateAsync (sowie Load/Save) gegen
        // Nebenlaeufigkeit — sonst koennen ueberlappende Mutationen (Hub-Tick + Settlement, Doppel-Kauf)
        // Buchungen verlieren/duplizieren (Last-Write-Wins auf dem geteilten _cache).
        private readonly SemaphoreSlim _gate = new(1, 1);

        public FirebaseSaveService(IAuthService auth)
        {
            _auth = auth;
            _jsonSettings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                // Verhindert, dass Newtonsoft beim Deserialisieren Elemente an bereits vorbefuellte
                // read-only-Collections ANHAENGT (z.B. Deck.RuneInstanceIds) — sonst wachsen solche
                // Listen bei jedem Laden. Replace = Inhalt komplett ersetzen.
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
        }

        public async UniTask<Result<PlayerSave>> LoadAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return await LoadInternalAsync(ct); }
            finally { _gate.Release(); }
        }

        public async UniTask<Result> SaveAsync(PlayerSave save, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try { return await SaveInternalAsync(save, ct); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Atomare Read-Modify-Write-Operation: Load + mutation + Save laufen unter EINEM Lock,
        /// sodass nebenlaeufige Mutationen sich nicht verschachteln und keine Buchung verloren geht.
        /// Die mutation-Lambda sollte ihre Vorbedingungen selbst pruefen (z.B. Gold-Deckung) und bei
        /// Fehlschlag den State unveraendert zurueckgeben.
        /// </summary>
        public async UniTask<Result<PlayerSave>> MutateAsync(Func<PlayerSave, PlayerSave> mutation,
                                                             CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var loadResult = await LoadInternalAsync(ct);
                if (!loadResult.IsSuccess) return loadResult;
                var mutated = mutation(loadResult.Value!);
                var saveResult = await SaveInternalAsync(mutated, ct);
                return saveResult.IsSuccess
                    ? Result<PlayerSave>.Success(mutated)
                    : Result<PlayerSave>.Failure(saveResult.ErrorMessage ?? "Save failed");
            }
            finally { _gate.Release(); }
        }

        // -------------------------------------------------------------------- Interne (ohne Lock)

        private async UniTask<Result<PlayerSave>> LoadInternalAsync(CancellationToken ct)
        {
            if (_cache != null) return Result<PlayerSave>.Success(_cache);

            // TODO Firebase: Realtime-DB Pull (benoetigt das Firebase Unity SDK, noch nicht installiert —
            //                siehe FIREBASE_SETUP.md). Konflikt-Aufloesung dann ueber LastSavedAtUtc
            //                (Server-Wins bei neuerer Server-Version). Bis dahin: rein lokaler Save.

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

        private async UniTask<Result> SaveInternalAsync(PlayerSave save, CancellationToken ct)
        {
            try
            {
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

                // _cache erst NACH erfolgreichem Write aktualisieren (Konsistenz mit Persistenz).
                _cache = save;
                GameLogger.Verbose("Save", $"Lokal gesichert ({json.Length} bytes).");

                // TODO Firebase: parallel an Realtime-DB pushen (benoetigt Firebase Unity SDK).
                // (await FirebaseDatabase.DefaultInstance.GetReference($"users/{userId}/save").SetRawJsonValueAsync(json);)
                return Result.Success();
            }
            catch (Exception ex)
            {
                // Cache invalidieren: der mutierte In-Memory-Stand ist NICHT persistiert. Naechster
                // LoadAsync liest den letzten konsistenten Stand von Platte statt einen Phantom-Cache.
                _cache = null;
                GameLogger.Error("Save", "SaveAsync fehlgeschlagen — Cache invalidiert", ex);
                return Result.Failure(ex);
            }
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
                var current = ArcaneKingdom.Domain.Save.SaveMigrator.CurrentSchemaVersion;
                if (save.SchemaVersion < current)
                {
                    save = ArcaneKingdom.Domain.Save.SaveMigrator.MigrateToCurrent(save);
                    GameLogger.Info("Save", $"Migriert {Path.GetFileName(path)} auf Schema v{save.SchemaVersion}.");
                }
                else if (save.SchemaVersion > current)
                {
                    // Save stammt aus einer neueren App-Version (Downgrade-Szenario). Felder, die diese
                    // Version nicht kennt, gingen beim naechsten Speichern verloren. Laut warnen statt
                    // still zu degradieren — der Aufrufer/QA sieht das im Log.
                    GameLogger.Error("Save",
                        $"{Path.GetFileName(path)} hat Schema v{save.SchemaVersion} > Client v{current} " +
                        "(Save aus neuerer App-Version). Moeglicher Daten-Downgrade beim naechsten Speichern.");
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
