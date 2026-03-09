namespace RebornSaga.Services;

using RebornSaga.Models;
using RebornSaga.Models.Enums;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// SQLite-basiertes Save-System. 3 Slots + Auto-Save (Slot 0).
/// ChapterUnlocks gelten global für alle Save-Slots.
/// </summary>
public class SaveGameService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private SQLiteAsyncConnection? _db;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1); // Verhindert parallele Saves (Auto-Save + manuell)

    /// <summary>
    /// Gibt die Datenbank-Verbindung zurück (lazy init, thread-safe).
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_db != null) return _db;

        await _initSemaphore.WaitAsync();
        try
        {
            // Double-Check nach Lock
            if (_db != null) return _db;

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RebornSaga");
            Directory.CreateDirectory(folder);

            var dbPath = Path.Combine(folder, "savegame.db3");
            var connection = new SQLiteAsyncConnection(dbPath);

            // Tabellen erstellen
            await connection.CreateTableAsync<SaveSlotEntity>();
            await connection.CreateTableAsync<PlayerDataEntity>();
            await connection.CreateTableAsync<InventoryEntity>();
            await connection.CreateTableAsync<SkillDataEntity>();
            await connection.CreateTableAsync<AffinityEntity>();
            await connection.CreateTableAsync<StoryProgressEntity>();
            await connection.CreateTableAsync<CodexEntity>();
            await connection.CreateTableAsync<ChapterUnlockEntity>();

            // Erst NACH allen CreateTable-Aufrufen zuweisen
            _db = connection;
            return _db;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// Speichert den kompletten Spielstand in einen Slot.
    /// Slot 0 = Auto-Save, Slot 1-3 = manuelle Saves.
    /// Alles in einer Transaktion (kein Datenverlust bei Crash).
    /// SemaphoreSlim verhindert parallele Save-Operationen (z.B. Auto-Save + manuelles Speichern gleichzeitig).
    /// </summary>
    public async Task SaveGameAsync(
        int slotNumber,
        Player player,
        SkillService skillService,
        InventoryService inventoryService,
        AffinityService affinityService,
        FateTrackingService fateTrackingService,
        CodexService codexService,
        int playTimeSeconds,
        string? slotName = null)
    {
        await _saveSemaphore.WaitAsync();
        try
        {
            await SaveGameInternalAsync(slotNumber, player, skillService, inventoryService,
                affinityService, fateTrackingService, codexService, playTimeSeconds, slotName);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    /// <summary>
    /// Interne Save-Implementierung (wird unter Semaphore-Schutz aufgerufen).
    /// </summary>
    private async Task SaveGameInternalAsync(
        int slotNumber,
        Player player,
        SkillService skillService,
        InventoryService inventoryService,
        AffinityService affinityService,
        FateTrackingService fateTrackingService,
        CodexService codexService,
        int playTimeSeconds,
        string? slotName = null)
    {
        var db = await GetDatabaseAsync();

        // Daten VOR der Transaktion vorbereiten (keine async Calls im sync RunInTransaction)
        var existingSlot = await db.Table<SaveSlotEntity>()
            .Where(s => s.SlotNumber == slotNumber)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow.ToString("O");
        var flagsJson = JsonSerializer.Serialize(player.Flags, JsonOptions);
        var completedChaptersJson = JsonSerializer.Serialize(player.CompletedChapters, JsonOptions);
        var rawInventory = inventoryService.GetRawInventory();
        var rawEquipment = inventoryService.GetRawEquipment();
        var playerSkills = skillService.GetAllPlayerSkills();
        var affinities = affinityService.GetAllAffinities();
        var decisions = fateTrackingService.Decisions.ToList();
        var unlockedCodex = codexService.GetUnlockedIds();

        // Alles in einer Transaktion (Crash-sicher)
        await db.RunInTransactionAsync(conn =>
        {
            // Alten Slot-Inhalt löschen falls vorhanden
            int slotId;

            if (existingSlot != null)
            {
                DeleteSlotDataSync(conn, existingSlot.Id);

                existingSlot.SlotName = slotName ?? existingSlot.SlotName;
                existingSlot.LastPlayedAt = now;
                existingSlot.PlayTimeSeconds = playTimeSeconds;
                existingSlot.ChapterId = player.CurrentChapterId;
                existingSlot.ClassName = player.Class.ToString();
                existingSlot.Level = player.Level;
                conn.Update(existingSlot);
                slotId = existingSlot.Id;
            }
            else
            {
                var newSlot = new SaveSlotEntity
                {
                    SlotNumber = slotNumber,
                    SlotName = slotName ?? (slotNumber == 0 ? "Auto-Save" : $"Slot {slotNumber}"),
                    CreatedAt = now,
                    LastPlayedAt = now,
                    PlayTimeSeconds = playTimeSeconds,
                    ChapterId = player.CurrentChapterId,
                    ClassName = player.Class.ToString(),
                    Level = player.Level
                };
                conn.Insert(newSlot);
                slotId = newSlot.Id;
            }

            // PlayerData
            conn.Insert(new PlayerDataEntity
            {
                SaveSlotId = slotId,
                Name = player.Name,
                ClassName = player.Class.ToString(),
                Level = player.Level,
                Exp = player.Exp,
                Hp = player.Hp,
                MaxHp = player.MaxHp,
                Mp = player.Mp,
                MaxMp = player.MaxMp,
                Atk = player.Atk,
                Def = player.Def,
                Int = player.Int,
                Spd = player.Spd,
                Luk = player.Luk,
                Gold = player.Gold,
                Karma = player.Karma,
                FreeStatPoints = player.FreeStatPoints,
                CurrentChapterId = player.CurrentChapterId,
                CurrentNodeId = player.CurrentNodeId,
                FlagsJson = flagsJson,
                CompletedChaptersJson = completedChaptersJson
            });

            // Inventar (nicht-ausgerüstete Items)
            foreach (var (itemId, quantity) in rawInventory)
            {
                conn.Insert(new InventoryEntity
                {
                    SaveSlotId = slotId,
                    ItemId = itemId,
                    Quantity = quantity,
                    IsEquipped = false,
                    EquipSlot = null
                });
            }

            // Ausgerüstete Items
            foreach (var (slot, itemId) in rawEquipment)
            {
                conn.Insert(new InventoryEntity
                {
                    SaveSlotId = slotId,
                    ItemId = itemId,
                    Quantity = 1,
                    IsEquipped = true,
                    EquipSlot = slot.ToString()
                });
            }

            // Skills
            foreach (var (skillId, ps) in playerSkills)
            {
                conn.Insert(new SkillDataEntity
                {
                    SaveSlotId = slotId,
                    SkillId = skillId,
                    MasteryCount = ps.Mastery,
                    IsUnlocked = ps.IsUnlocked
                });
            }

            // Affinitäten
            foreach (var (npcId, data) in affinities)
            {
                conn.Insert(new AffinityEntity
                {
                    SaveSlotId = slotId,
                    NpcId = npcId,
                    Points = data.Points,
                    BondLevel = data.BondLevel,
                    SeenScenesStr = string.Join(",", data.SeenScenes)
                });
            }

            // Story-Entscheidungen
            foreach (var d in decisions)
            {
                conn.Insert(new StoryProgressEntity
                {
                    SaveSlotId = slotId,
                    ChapterId = d.ChapterId,
                    NodeId = d.NodeId,
                    ChoiceIndex = d.ChoiceIndex,
                    KarmaChange = d.KarmaChange,
                    DescriptionKey = d.DescriptionKey
                });
            }

            // Kodex
            foreach (var entryId in unlockedCodex)
            {
                conn.Insert(new CodexEntity
                {
                    SaveSlotId = slotId,
                    EntryId = entryId,
                    IsDiscovered = true
                });
            }
        });
    }

    /// <summary>
    /// Lädt einen Spielstand und stellt alle Services wieder her.
    /// Gibt null zurück wenn der Slot leer ist.
    /// WICHTIG: SkillService.LoadSkills() und InventoryService.LoadItems() müssen vorher aufgerufen worden sein.
    /// </summary>
    public async Task<Player?> LoadGameAsync(
        int slotNumber,
        SkillService skillService,
        InventoryService inventoryService,
        AffinityService affinityService,
        FateTrackingService fateTrackingService,
        CodexService codexService)
    {
        var db = await GetDatabaseAsync();

        var slot = await db.Table<SaveSlotEntity>()
            .Where(s => s.SlotNumber == slotNumber)
            .FirstOrDefaultAsync();
        if (slot == null) return null;

        var slotId = slot.Id;

        // PlayerData laden
        var pd = await db.Table<PlayerDataEntity>()
            .Where(p => p.SaveSlotId == slotId)
            .FirstOrDefaultAsync();
        if (pd == null) return null;

        var player = new Player
        {
            Name = pd.Name,
            Class = Enum.TryParse<ClassName>(pd.ClassName, out var cls) ? cls : ClassName.Swordmaster,
            Level = pd.Level,
            Exp = pd.Exp,
            Hp = pd.Hp,
            MaxHp = pd.MaxHp,
            Mp = pd.Mp,
            MaxMp = pd.MaxMp,
            Atk = pd.Atk,
            Def = pd.Def,
            Int = pd.Int,
            Spd = pd.Spd,
            Luk = pd.Luk,
            Gold = pd.Gold,
            Karma = pd.Karma,
            FreeStatPoints = pd.FreeStatPoints,
            CurrentChapterId = pd.CurrentChapterId,
            CurrentNodeId = pd.CurrentNodeId,
            Flags = string.IsNullOrEmpty(pd.FlagsJson)
                ? new HashSet<string>()
                : JsonSerializer.Deserialize<HashSet<string>>(pd.FlagsJson, JsonOptions) ?? new(),
            CompletedChapters = string.IsNullOrEmpty(pd.CompletedChaptersJson)
                ? new HashSet<string>()
                : JsonSerializer.Deserialize<HashSet<string>>(pd.CompletedChaptersJson, JsonOptions) ?? new()
        };

        // Inventar laden
        var invEntities = await db.Table<InventoryEntity>()
            .Where(i => i.SaveSlotId == slotId)
            .ToListAsync();

        var inventory = new Dictionary<string, int>();
        var equipment = new Dictionary<EquipSlot, string>();

        foreach (var ie in invEntities)
        {
            if (ie.IsEquipped && Enum.TryParse<EquipSlot>(ie.EquipSlot, out var equipSlot))
            {
                equipment[equipSlot] = ie.ItemId;
            }
            else
            {
                inventory[ie.ItemId] = ie.Quantity;
            }
        }
        inventoryService.RestoreState(inventory, equipment);

        // Skills laden
        var skillEntities = await db.Table<SkillDataEntity>()
            .Where(s => s.SaveSlotId == slotId)
            .ToListAsync();

        var masteryData = new Dictionary<string, int>();
        var unlockedSkillIds = new HashSet<string>();
        foreach (var se in skillEntities)
        {
            masteryData[se.SkillId] = se.MasteryCount;
            if (se.IsUnlocked)
                unlockedSkillIds.Add(se.SkillId);
        }
        skillService.RestorePlayerSkills(masteryData, unlockedSkillIds);

        // Affinitäten laden
        var affEntities = await db.Table<AffinityEntity>()
            .Where(a => a.SaveSlotId == slotId)
            .ToListAsync();

        var affinityData = new Dictionary<string, AffinityData>();
        foreach (var ae in affEntities)
        {
            var seenScenes = new HashSet<int>();
            if (!string.IsNullOrEmpty(ae.SeenScenesStr))
            {
                foreach (var s in ae.SeenScenesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(s.Trim(), out var level))
                        seenScenes.Add(level);
                }
            }

            affinityData[ae.NpcId] = new AffinityData
            {
                NpcId = ae.NpcId,
                Points = ae.Points,
                BondLevel = ae.BondLevel,
                SeenScenes = seenScenes
            };
        }
        affinityService.RestoreAffinities(affinityData);

        // Story-Entscheidungen laden
        var progressEntities = await db.Table<StoryProgressEntity>()
            .Where(p => p.SaveSlotId == slotId)
            .ToListAsync();

        var decisions = progressEntities.Select(pe => new FateDecision
        {
            ChapterId = pe.ChapterId,
            NodeId = pe.NodeId,
            ChoiceIndex = pe.ChoiceIndex,
            KarmaChange = pe.KarmaChange,
            DescriptionKey = pe.DescriptionKey
        }).ToList();
        fateTrackingService.Restore(player.Karma, decisions, player.Flags);

        // Kodex laden
        var codexEntities = await db.Table<CodexEntity>()
            .Where(c => c.SaveSlotId == slotId)
            .ToListAsync();

        var unlockedCodexIds = new HashSet<string>(codexEntities.Where(c => c.IsDiscovered).Select(c => c.EntryId));
        codexService.RestoreUnlocked(unlockedCodexIds);

        return player;
    }

    /// <summary>
    /// Gibt Metadaten aller Save-Slots zurück (1-3 + Auto-Save).
    /// </summary>
    public async Task<List<SaveSlotEntity>> GetAllSlotsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<SaveSlotEntity>()
            .OrderBy(s => s.SlotNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Gibt die Metadaten eines bestimmten Slots zurück (null wenn leer).
    /// </summary>
    public async Task<SaveSlotEntity?> GetSlotInfoAsync(int slotNumber)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<SaveSlotEntity>()
            .Where(s => s.SlotNumber == slotNumber)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Löscht einen Save-Slot komplett.
    /// </summary>
    public async Task DeleteSlotAsync(int slotNumber)
    {
        var db = await GetDatabaseAsync();
        var slot = await db.Table<SaveSlotEntity>()
            .Where(s => s.SlotNumber == slotNumber)
            .FirstOrDefaultAsync();
        if (slot == null) return;

        await db.RunInTransactionAsync(conn =>
        {
            DeleteSlotDataSync(conn, slot.Id);
            conn.Delete(slot);
        });
    }

    /// <summary>
    /// Prüft ob ein Auto-Save existiert (Slot 0).
    /// </summary>
    public async Task<bool> HasAutoSaveAsync()
    {
        var slot = await GetSlotInfoAsync(0);
        return slot != null;
    }

    // --- Kapitel-Freischaltung (global, nicht pro Slot) ---

    /// <summary>
    /// Schaltet ein Kapitel frei (global für alle Slots).
    /// </summary>
    public async Task UnlockChapterAsync(string chapterId, string method)
    {
        var db = await GetDatabaseAsync();

        var existing = await db.Table<ChapterUnlockEntity>()
            .Where(c => c.ChapterId == chapterId)
            .FirstOrDefaultAsync();
        if (existing != null) return;

        await db.InsertAsync(new ChapterUnlockEntity
        {
            ChapterId = chapterId,
            UnlockMethod = method,
            UnlockedAt = DateTime.UtcNow.ToString("O")
        });
    }

    /// <summary>
    /// Prüft ob ein Kapitel freigeschaltet ist.
    /// </summary>
    public async Task<bool> IsChapterUnlockedAsync(string chapterId)
    {
        var db = await GetDatabaseAsync();
        var entry = await db.Table<ChapterUnlockEntity>()
            .Where(c => c.ChapterId == chapterId)
            .FirstOrDefaultAsync();
        return entry != null;
    }

    /// <summary>
    /// Gibt alle freigeschalteten Kapitel zurück.
    /// </summary>
    public async Task<List<ChapterUnlockEntity>> GetUnlockedChaptersAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<ChapterUnlockEntity>().ToListAsync();
    }

    /// <summary>
    /// Gibt die Spielzeit eines Slots zurück (in Sekunden).
    /// </summary>
    public async Task<int> GetPlayTimeAsync(int slotNumber)
    {
        var slot = await GetSlotInfoAsync(slotNumber);
        return slot?.PlayTimeSeconds ?? 0;
    }

    /// <summary>
    /// Schließt die SQLite-Verbindung und gibt Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _db?.CloseAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Fehler beim Dispose beim Herunterfahren sind unkritisch
        }
        _db = null;
    }

    /// <summary>
    /// Löscht alle Daten eines Slots synchron (für Transaktion).
    /// </summary>
    private static void DeleteSlotDataSync(SQLiteConnection conn, int slotId)
    {
        conn.Execute("DELETE FROM PlayerData WHERE SaveSlotId = ?", slotId);
        conn.Execute("DELETE FROM Inventory WHERE SaveSlotId = ?", slotId);
        conn.Execute("DELETE FROM SkillData WHERE SaveSlotId = ?", slotId);
        conn.Execute("DELETE FROM AffinityData WHERE SaveSlotId = ?", slotId);
        conn.Execute("DELETE FROM StoryProgress WHERE SaveSlotId = ?", slotId);
        conn.Execute("DELETE FROM CodexEntries WHERE SaveSlotId = ?", slotId);
    }
}
