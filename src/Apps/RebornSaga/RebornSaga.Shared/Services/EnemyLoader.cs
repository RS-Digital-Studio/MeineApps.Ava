namespace RebornSaga.Services;

using RebornSaga.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Lädt Gegner-Definitionen aus der eingebetteten enemies.json.
/// Statischer Zugriff per GetById() — Daten werden beim ersten Aufruf geladen.
/// </summary>
public static class EnemyLoader
{
    private static Dictionary<string, Enemy>? _enemies;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Lädt einen Gegner per ID aus enemies.json. Gibt null zurück wenn nicht gefunden.</summary>
    public static Enemy? GetById(string enemyId)
    {
        EnsureLoaded();
        return _enemies!.TryGetValue(enemyId, out var enemy) ? CloneEnemy(enemy) : null;
    }

    /// <summary>Erstellt eine Kopie (Kampf-HP darf pro Kampf variieren).</summary>
    private static Enemy CloneEnemy(Enemy template) => new()
    {
        Id = template.Id,
        NameKey = template.NameKey,
        Level = template.Level,
        Hp = template.Hp,
        Atk = template.Atk,
        Def = template.Def,
        Spd = template.Spd,
        ElementStr = template.ElementStr,
        WeaknessStr = template.WeaknessStr,
        Exp = template.Exp,
        Gold = template.Gold,
        Phases = template.Phases,
        Drops = template.Drops,
        IsProlog = template.IsProlog,
        IsScripted = template.IsScripted,
        ScriptedRounds = template.ScriptedRounds,
        IsCutsceneOnly = template.IsCutsceneOnly
    };

    private static void EnsureLoaded()
    {
        if (_enemies != null) return;

        _enemies = new Dictionary<string, Enemy>();

        try
        {
            var resourceName = "RebornSaga.Data.Enemies.enemies.json";
            using var stream = typeof(EnemyLoader).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var wrapper = JsonSerializer.Deserialize<EnemyWrapper>(json, JsonOptions);

            // Normale Gegner laden
            if (wrapper?.Enemies != null)
                foreach (var enemy in wrapper.Enemies)
                    _enemies[enemy.Id] = enemy;

            // Bosse laden (gleiche Struktur, selbes Dictionary)
            if (wrapper?.Bosses != null)
                foreach (var boss in wrapper.Bosses)
                    _enemies[boss.Id] = boss;
        }
        catch
        {
            // Fallback: leeres Dictionary
        }
    }

    private class EnemyWrapper
    {
        [JsonPropertyName("enemies")]
        public List<Enemy>? Enemies { get; set; }

        [JsonPropertyName("bosses")]
        public List<Enemy>? Bosses { get; set; }
    }
}
