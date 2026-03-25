namespace RebornSaga.Services;

using RebornSaga.Models;
using RebornSaga.Models.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Kern-Service der Story-Logik. Lädt Kapitel aus JSON, navigiert durch Knoten,
/// wertet Bedingungen aus und verwaltet Dialog-Texte sprachabhängig.
/// </summary>
public class StoryEngine
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
    };

    private readonly Dictionary<string, StoryNode> _nodeMap = new();
    private Dictionary<string, string> _dialogueTexts = new();

    // Injizierte Services für Effekt-Verarbeitung
    private readonly ProgressionService _progressionService;
    private readonly FateTrackingService _fateTrackingService;
    private readonly GoldService _goldService;
    private readonly InventoryService _inventoryService;

    /// <summary>Aktueller Spieler (per SetPlayer() gesetzt nach SaveGame-Load oder Neues-Spiel).</summary>
    private Player? _player;

    /// <summary>Aktuell geladenes Kapitel.</summary>
    public Chapter? CurrentChapter { get; private set; }

    /// <summary>Aktueller Story-Knoten.</summary>
    public StoryNode? CurrentNode { get; private set; }

    /// <summary>Index der aktuellen Sprecher-Zeile innerhalb eines Dialog-Knotens.</summary>
    public int CurrentLineIndex { get; private set; }

    /// <summary>Spieler-Karma (beeinflusst Entscheidungen und Story-Verlauf).</summary>
    public int Karma { get; set; }

    /// <summary>Charakter-Affinitäten (z.B. "aria" → 15).</summary>
    public Dictionary<string, int> Affinities { get; } = new();

    /// <summary>Spieler-Items (IDs).</summary>
    public HashSet<string> Items { get; } = new();

    /// <summary>Story-Flags (z.B. "alliance_aria", "betrayed_aldric").</summary>
    public HashSet<string> Flags { get; } = new();

    /// <summary>Spieler-Klasse (0=Schwert, 1=Magier, 2=Assassine).</summary>
    public int PlayerClassType { get; set; }

    /// <summary>Event wenn Effekte angewendet werden (für UI-Feedback).</summary>
    public event Action<StoryEffects>? EffectsApplied;

    /// <summary>Event wenn ein Kapitel abgeschlossen wird.</summary>
    public event Action<string>? ChapterCompleted;

    /// <summary>Event wenn ein Schicksals-Wendepunkt eintritt (für FateChangedOverlay).</summary>
    public event Action<string>? FateChangeTriggered;

    public StoryEngine(
        ProgressionService progressionService,
        FateTrackingService fateTrackingService,
        GoldService goldService,
        InventoryService inventoryService)
    {
        _progressionService = progressionService;
        _fateTrackingService = fateTrackingService;
        _goldService = goldService;
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Setzt den aktiven Spieler für Effekt-Anwendung (EXP, Gold, Flags).
    /// Muss nach SaveGame-Load oder Neues-Spiel aufgerufen werden.
    /// Gibt den aktuellen Player zurück (oder null wenn noch nicht gesetzt).
    /// </summary>
    public Player? GetPlayer() => _player;

    /// <summary>
    /// Synchronisiert die Engine-Properties (Karma, Items, Flags, Affinities) mit dem Player.
    /// </summary>
    public void SetPlayer(Player player)
    {
        _player = player;

        // Engine-State mit Player synchronisieren
        Karma = player.Karma;
        PlayerClassType = (int)player.Class;

        Items.Clear();
        foreach (var item in player.Inventory)
            Items.Add(item);

        Flags.Clear();
        foreach (var flag in player.Flags)
            Flags.Add(flag);

        Affinities.Clear();
        foreach (var (key, value) in player.Affinities)
            Affinities[key] = value;
    }

    /// <summary>
    /// Lädt ein Kapitel aus der EmbeddedResource JSON-Datei.
    /// </summary>
    public async Task LoadChapterAsync(string chapterId)
    {
        var assembly = typeof(StoryEngine).Assembly;
        var resourceName = $"RebornSaga.Data.Chapters.chapter_{chapterId}.json";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Kapitel-Daten nicht gefunden: {resourceName}");

        var chapter = await JsonSerializer.DeserializeAsync<Chapter>(stream, _jsonOptions);
        if (chapter == null)
            throw new InvalidOperationException($"Kapitel konnte nicht deserialisiert werden: {chapterId}");

        CurrentChapter = chapter;

        // Knoten-Map aufbauen für schnellen Zugriff per ID
        _nodeMap.Clear();
        foreach (var node in chapter.Nodes)
            _nodeMap[node.Id] = node;

        // Dialog-Texte laden (sprachabhängig)
        await LoadDialogueTextsAsync(chapterId);

        // Zum ersten Knoten navigieren
        if (chapter.Nodes.Count > 0)
        {
            CurrentNode = chapter.Nodes[0];
            CurrentLineIndex = 0;
        }
    }

    /// <summary>
    /// Lädt die lokalisierten Dialog-Texte für ein Kapitel.
    /// Fallback: Deutsch wenn gewählte Sprache nicht verfügbar.
    /// </summary>
    private async Task LoadDialogueTextsAsync(string chapterId)
    {
        var assembly = typeof(StoryEngine).Assembly;
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        // Versuch die gewählte Sprache, Fallback auf Deutsch
        var resourceName = $"RebornSaga.Data.Dialogue.{lang}.chapter_{chapterId}.json";
        var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            resourceName = $"RebornSaga.Data.Dialogue.de.chapter_{chapterId}.json";
            stream = assembly.GetManifestResourceStream(resourceName);
        }

        if (stream == null)
        {
            _dialogueTexts = new Dictionary<string, string>();
            return;
        }

        await using (stream)
        {
            _dialogueTexts = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, _jsonOptions)
                ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Gibt einen Knoten per ID zurück.
    /// </summary>
    public StoryNode? GetNode(string nodeId)
    {
        return _nodeMap.TryGetValue(nodeId, out var node) ? node : null;
    }

    /// <summary>
    /// Navigiert zu einem bestimmten Knoten. Prüft node.Condition -
    /// wenn nicht erfüllt, wird der Knoten übersprungen (folgt node.Next).
    /// Iterativ mit Limit (max 100 Knoten), verhindert StackOverflow bei Ketten
    /// wo alle Conditions fehlschlagen.
    /// </summary>
    public void AdvanceToNode(string nodeId)
    {
        const int maxIterations = 100;
        var currentId = nodeId;

        for (int i = 0; i < maxIterations; i++)
        {
            if (!_nodeMap.TryGetValue(currentId, out var node))
                return;

            // Knoten-Bedingung prüfen: wenn nicht erfüllt, überspringen
            if (!EvaluateCondition(node.Condition))
            {
                // Zum nächsten Knoten springen wenn verfügbar, sonst Knoten ignorieren
                if (!string.IsNullOrEmpty(node.Next))
                {
                    currentId = node.Next;
                    continue;
                }
                return;
            }

            CurrentNode = node;
            CurrentLineIndex = 0;

            // Knoten-Effekte anwenden
            if (node.Effects != null)
                ApplyEffects(node.Effects);

            // Kapitel-Ende erkennen
            if (node.Type == NodeType.ChapterEnd && CurrentChapter != null)
                ChapterCompleted?.Invoke(CurrentChapter.Id);

            return;
        }

        // Sicherheitslimit erreicht - am letzten gültigen Knoten stoppen
        System.Diagnostics.Debug.WriteLine(
            $"StoryEngine: Knoten-Skip-Limit erreicht (100) ab '{nodeId}'. Möglicher Endlos-Loop in Kapitel-Daten.");
    }

    /// <summary>
    /// Navigiert automatisch zum nächsten Knoten (folgt CurrentNode.Next).
    /// Gibt true zurück wenn ein nächster Knoten existiert.
    /// </summary>
    public bool AdvanceToNext()
    {
        if (CurrentNode?.Next == null) return false;
        AdvanceToNode(CurrentNode.Next);
        return true;
    }

    /// <summary>
    /// Geht zur nächsten Sprecher-Zeile im aktuellen Knoten.
    /// Gibt true zurück wenn es noch Zeilen gibt, false wenn der Knoten fertig ist.
    /// </summary>
    public bool AdvanceToNextLine()
    {
        if (CurrentNode?.Speakers == null) return false;

        CurrentLineIndex++;
        return CurrentLineIndex < CurrentNode.Speakers.Count;
    }

    /// <summary>
    /// Gibt die aktuelle Sprecher-Zeile zurück.
    /// </summary>
    public SpeakerLine? GetCurrentLine()
    {
        if (CurrentNode?.Speakers == null || CurrentLineIndex >= CurrentNode.Speakers.Count)
            return null;

        return CurrentNode.Speakers[CurrentLineIndex];
    }

    /// <summary>
    /// Verarbeitet eine Spieler-Auswahl bei Choice-Knoten.
    /// </summary>
    public void MakeChoice(int optionIndex)
    {
        if (CurrentNode?.Options == null || optionIndex < 0 || optionIndex >= CurrentNode.Options.Count)
            return;

        var option = CurrentNode.Options[optionIndex];

        // Effekte der Wahl anwenden
        if (option.Effects != null)
            ApplyEffects(option.Effects);

        // Zum nächsten Knoten navigieren
        if (!string.IsNullOrEmpty(option.Next))
            AdvanceToNode(option.Next);
    }

    /// <summary>
    /// Gibt den lokalisierten Text für einen Key zurück.
    /// Fallback: Key selbst (für Debugging sichtbar).
    /// </summary>
    public string GetLocalizedText(string key)
    {
        return _dialogueTexts.TryGetValue(key, out var text) ? text : $"[{key}]";
    }

    /// <summary>
    /// Wertet eine Bedingung aus (z.B. "karma > 50", "has_item:M001", "affinity:aria >= 10").
    /// </summary>
    public bool EvaluateCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;

        // Einfacher Condition-Parser
        var parts = condition.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 3)
        {
            var variable = parts[0].ToLowerInvariant();
            var op = parts[1];
            if (!int.TryParse(parts[2], out var value)) return true;

            int actual = variable switch
            {
                "karma" => Karma,
                _ when variable.StartsWith("affinity:") =>
                    Affinities.TryGetValue(variable[9..], out var aff) ? aff : 0,
                "class" => PlayerClassType,
                _ => 0
            };

            return op switch
            {
                ">" => actual > value,
                ">=" => actual >= value,
                "<" => actual < value,
                "<=" => actual <= value,
                "==" => actual == value,
                "!=" => actual != value,
                _ => true
            };
        }

        // Item-Besitz prüfen: "has_item:M001" — über InventoryService (Single Source of Truth)
        if (parts.Length == 1 && parts[0].StartsWith("has_item:"))
            return _inventoryService.HasItem(parts[0][9..]);

        // Negation: "!has_item:M001"
        if (parts.Length == 1 && parts[0].StartsWith("!has_item:"))
            return !_inventoryService.HasItem(parts[0][10..]);

        // Flag-Besitz prüfen: "has_flag:betrayed_aldric" — über FateTrackingService (Single Source of Truth)
        if (parts.Length == 1 && parts[0].StartsWith("has_flag:"))
            return _fateTrackingService.HasFlag(parts[0][9..]) || Flags.Contains(parts[0][9..]);

        // Negation: "!has_flag:betrayed_aldric"
        if (parts.Length == 1 && parts[0].StartsWith("!has_flag:"))
            return !_fateTrackingService.HasFlag(parts[0][10..]) && !Flags.Contains(parts[0][10..]);

        // Einwort-Condition ohne Operator → als Flag-Check behandeln
        // z.B. "alliance_aria", "is_hero", "betrayed_aldric"
        if (parts.Length == 1)
        {
            var word = parts[0];
            // Negation: "!alliance_aria"
            if (word.StartsWith('!'))
                return !_fateTrackingService.HasFlag(word[1..]) && !Flags.Contains(word[1..]);
            return _fateTrackingService.HasFlag(word) || Flags.Contains(word);
        }

        return true;
    }

    // Kapitel-Reihenfolge: Prolog P1-P3, dann Arc 1 K1-K10
    private static readonly string[] ChapterOrder =
    {
        "p1", "p2", "p3", "k1", "k2", "k3", "k4", "k5", "k6", "k7", "k8", "k9", "k10"
    };

    /// <summary>
    /// Gibt die nächste Kapitel-ID zurück, oder null wenn das letzte Kapitel erreicht ist.
    /// </summary>
    public static string? GetNextChapterId(string currentChapterId)
    {
        var index = Array.IndexOf(ChapterOrder, currentChapterId);
        if (index < 0 || index >= ChapterOrder.Length - 1) return null;
        return ChapterOrder[index + 1];
    }

    /// <summary>
    /// Gibt die verfügbaren (nicht-deaktivierten) Choice-Optionen zurück.
    /// Deaktivierte Optionen haben eine Condition die false ergibt.
    /// </summary>
    public HashSet<int> GetDisabledChoices()
    {
        var disabled = new HashSet<int>();
        if (CurrentNode?.Options == null) return disabled;

        for (int i = 0; i < CurrentNode.Options.Count; i++)
        {
            if (!EvaluateCondition(CurrentNode.Options[i].Condition))
                disabled.Add(i);
        }

        return disabled;
    }

    /// <summary>
    /// Wendet Story-Effekte an (Karma, EXP, Gold, Affinität, Items, Flags, FateChanged).
    /// Delegiert an die entsprechenden Services und synchronisiert den Player-Zustand.
    /// </summary>
    private void ApplyEffects(StoryEffects effects)
    {
        // --- Karma ---
        // Karma an FateTrackingService delegieren (Clamp -100 bis +100 + Event)
        if (effects.Karma != 0)
        {
            _fateTrackingService.ModifyKarma(effects.Karma);

            // Engine-Wert mit dem geclampten Wert synchronisieren
            Karma = _fateTrackingService.Karma;

            if (_player != null)
                _player.Karma = _fateTrackingService.Karma;
        }

        // --- EXP ---
        if (effects.Exp != 0 && _player != null)
            _progressionService.AwardExp(_player, effects.Exp);

        // --- Gold (mit Minimum-0-Clamp) ---
        if (effects.Gold != 0 && _player != null)
        {
            if (effects.Gold > 0)
                _goldService.AddGold(_player, effects.Gold);
            else
            {
                // Negativer Gold-Betrag: abziehen, aber nicht unter 0
                var absAmount = Math.Abs(effects.Gold);
                if (_player.Gold >= absAmount)
                    _goldService.SpendGold(_player, absAmount);
                else
                {
                    // Nicht genug Gold → auf 0 setzen
                    _goldService.SpendGold(_player, _player.Gold);
                }
            }
        }

        // --- Affinitäten ---
        if (effects.Affinity != null)
        {
            foreach (var (charId, delta) in effects.Affinity)
            {
                if (!Affinities.ContainsKey(charId))
                    Affinities[charId] = 0;
                Affinities[charId] += delta;

                // Player-Affinitäten synchronisieren
                if (_player != null)
                    _player.Affinities[charId] = Affinities[charId];
            }
        }

        // --- Items (ueber InventoryService, synchronisiert Engine + Player) ---
        if (effects.AddItems != null)
        {
            foreach (var item in effects.AddItems)
            {
                Items.Add(item);
                _inventoryService.AddItem(item);
                _player?.Inventory.Add(item);
            }
        }

        if (effects.RemoveItems != null)
        {
            foreach (var item in effects.RemoveItems)
            {
                Items.Remove(item);
                _inventoryService.RemoveItem(item);
                _player?.Inventory.Remove(item);
            }
        }

        // --- Flags setzen ---
        if (effects.SetFlags != null)
        {
            foreach (var flag in effects.SetFlags)
            {
                Flags.Add(flag);
                _player?.Flags.Add(flag);
                _fateTrackingService.AddFlag(flag);
            }
        }

        // --- Flags entfernen ---
        if (effects.RemoveFlags != null)
        {
            foreach (var flag in effects.RemoveFlags)
            {
                Flags.Remove(flag);
                _player?.Flags.Remove(flag);
                _fateTrackingService.RemoveFlag(flag);
            }
        }

        // --- FateChanged (Schicksals-Wendepunkt) ---
        if (!string.IsNullOrEmpty(effects.FateChanged))
        {
            // Als Flag speichern für spätere Bedingungsprüfungen
            Flags.Add(effects.FateChanged);
            _player?.Flags.Add(effects.FateChanged);

            // FateTrackingService informieren (speichert als "fate_xxx" Flag + feuert Event)
            _fateTrackingService.RecordFateChange(effects.FateChanged);

            // FateChangedOverlay triggern
            FateChangeTriggered?.Invoke(effects.FateChanged);
        }

        EffectsApplied?.Invoke(effects);
    }
}
