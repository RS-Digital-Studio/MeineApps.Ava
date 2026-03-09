namespace RebornSaga.Scenes;

using Microsoft.Extensions.DependencyInjection;
using RebornSaga.Engine;
using RebornSaga.Engine.Transitions;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Overlays;
using RebornSaga.Rendering.Effects;
using RebornSaga.Rendering.Map;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// Overworld-Map-Szene. Zeigt die Kapitel-Karte mit Knoten, Pfaden und HUD.
/// Spieler kann Knoten antippen um Story/Kampf/Shop zu betreten.
/// Unterstützt Kamera-Pan via Drag und Zoom-Stufen.
/// </summary>
public class OverworldScene : Scene, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly StoryEngine _storyEngine;
    private readonly SaveGameService _saveGameService;
    private readonly SpriteCache? _spriteCache;
    private Player _player;

    // Map-Daten
    private ChapterMap? _map;
    private readonly Dictionary<string, MapNode> _nodeIndex = new();

    // Kamera
    private float _cameraX;
    private float _cameraY;
    private float _zoom = 1f;
    private float _targetZoom = 1f;

    // Drag-State
    private bool _isDragging;
    private float _dragStartX, _dragStartY;
    private float _cameraStartX, _cameraStartY;
    private bool _dragMoved;

    // Animation
    private float _animTime;

    // Partikel für Ambiente
    private readonly ParticleSystem _particles = new(100);

    // Gecachte Bounds
    private SKRect _lastBounds;

    // Gecachte HUD-Strings (keine Allokation pro Frame)
    private string _cachedChapterName = "";
    private string _cachedHpText = "";
    private string _cachedGoldText = "";
    private string _cachedLevelText = "";
    private int _lastPlayerHp, _lastPlayerMaxHp, _lastPlayerGold, _lastPlayerLevel;

    // Gecachte Display-Namen (keine String-Allokation pro Frame)
    private readonly Dictionary<string, string> _displayNames = new();

    // Gecachtes Menü-Button-Rect
    private SKRect _menuButtonRect;

    /// <summary>
    /// Erstellt die Overworld-Szene für das aktuelle Kapitel des Spielers.
    /// </summary>
    public OverworldScene(StoryEngine storyEngine, SaveGameService saveGameService,
        SpriteCache? spriteCache = null)
    {
        _storyEngine = storyEngine;
        _saveGameService = saveGameService;
        _spriteCache = spriteCache;

        // Spieler-Instanz: Platzhalter bis SetPlayer() aufgerufen wird
        _player = Player.Create(Models.Enums.ClassName.Swordmaster);

        // SpriteCache an statischen NodeRenderer weiterreichen
        NodeRenderer.SetSpriteCache(spriteCache);
    }

    /// <summary>
    /// Setzt den Spieler und lädt die Map-Daten neu.
    /// Wird von außen aufgerufen wenn ein aktiver Spieler existiert.
    /// </summary>
    public void SetPlayer(Player player)
    {
        _player = player;
        LoadMap(_player.CurrentChapterId);
    }

    public override void OnEnter()
    {
        // Standard-Map laden wenn noch keine geladen
        if (_map == null)
            LoadMap(_player.CurrentChapterId);
    }

    public override void OnResume()
    {
        // Nach Rückkehr aus Dialog/Battle: aktuellen Knoten als erledigt markieren
        if (_map != null && !string.IsNullOrEmpty(_player.CurrentNodeId))
        {
            if (_nodeIndex.TryGetValue(_player.CurrentNodeId, out var node) && !node.IsCompleted)
            {
                node.IsCompleted = true;
                UpdateNodeAccessibility();
                // AppChecker:ignore
                AutoSaveAsync();
            }
        }
    }

    public override void Update(float deltaTime)
    {
        _animTime += deltaTime;

        // Zoom-Interpolation
        if (MathF.Abs(_zoom - _targetZoom) > 0.01f)
            _zoom += (_targetZoom - _zoom) * deltaTime * 8f;
        else
            _zoom = _targetZoom;

        // Ambiente-Partikel
        _particles.Update(deltaTime);
        if (_lastBounds.Width > 0)
        {
            // Gelegentlich Leuchtpartikel emittieren
            if (_animTime % 2f < deltaTime)
            {
                _particles.Emit(
                    _lastBounds.Left + (float)Random.Shared.NextDouble() * _lastBounds.Width,
                    _lastBounds.Top + (float)Random.Shared.NextDouble() * _lastBounds.Height,
                    2, ParticleSystem.AmbientFloat);
            }
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        _lastBounds = bounds;

        if (_map == null)
        {
            // Lade-Anzeige
            UIRenderer.DrawText(canvas, "Lade Karte...", bounds.MidX, bounds.MidY, 18f,
                UIRenderer.TextSecondary, SKTextAlign.Center, true);
            return;
        }

        // Map mit Knoten und Pfaden zeichnen
        OverworldRenderer.Draw(canvas, bounds, _map, _animTime, _cameraX, _cameraY, _zoom, _displayNames);

        // Partikel über der Map
        _particles.Render(canvas);

        // HUD über allem
        UpdateCachedHudStrings();
        OverworldRenderer.DrawHud(canvas, bounds, _cachedChapterName,
            _cachedLevelText, _cachedHpText, _cachedGoldText,
            _player.Hp, _player.MaxHp);

        // Menü-Button (oben rechts im HUD-Bereich)
        _menuButtonRect = new SKRect(bounds.Right - 80f, bounds.Top + 8f, bounds.Right - 8f, bounds.Top + 48f);
        UIRenderer.DrawButton(canvas, _menuButtonRect, "Menu", false, false, UIRenderer.Border);
    }

    // --- Input ---

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (_map == null) return;

        switch (action)
        {
            case InputAction.Tap:
                HandleTap(position);
                break;

            case InputAction.DoubleTap:
                // Zoom umschalten
                _targetZoom = _targetZoom > 1.2f ? 1f : 1.5f;
                break;

            case InputAction.Back:
                SceneManager.ShowOverlay<PauseOverlay>();
                break;
        }
    }

    public override void HandlePointerDown(SKPoint position)
    {
        _isDragging = true;
        _dragStartX = position.X;
        _dragStartY = position.Y;
        _cameraStartX = _cameraX;
        _cameraStartY = _cameraY;
        _dragMoved = false;
    }

    public override void HandlePointerMove(SKPoint position)
    {
        if (!_isDragging) return;

        var dx = position.X - _dragStartX;
        var dy = position.Y - _dragStartY;

        // Erst als Drag werten wenn mindestens 8px bewegt
        if (MathF.Abs(dx) > 8f || MathF.Abs(dy) > 8f)
        {
            _dragMoved = true;
            // Kamera-Position begrenzen (Map nicht aus dem Sichtfeld verlieren)
            var maxPanX = _lastBounds.Width > 0 ? _lastBounds.Width * 0.3f : 200f;
            var maxPanY = _lastBounds.Height > 0 ? _lastBounds.Height * 0.3f : 300f;
            _cameraX = Math.Clamp(_cameraStartX + dx, -maxPanX, maxPanX);
            _cameraY = Math.Clamp(_cameraStartY + dy, -maxPanY, maxPanY);
        }
    }

    public override void HandlePointerUp(SKPoint position)
    {
        if (_isDragging && !_dragMoved)
        {
            // Kein Drag → war ein Tap, wird von HandleInput verarbeitet
        }
        _isDragging = false;
    }

    // --- Private ---

    private void HandleTap(SKPoint position)
    {
        if (_map == null || _lastBounds.Width < 1) return;

        // Menü-Button prüfen
        if (UIRenderer.HitTest(_menuButtonRect, position))
        {
            SceneManager.ShowOverlay<PauseOverlay>();
            return;
        }

        // Knoten prüfen
        var mapArea = OverworldRenderer.GetMapArea(_lastBounds);
        var hitNodeId = OverworldRenderer.HitTestNode(position, _map, mapArea, _cameraX, _cameraY, _zoom, _lastBounds);

        if (hitNodeId != null && _nodeIndex.TryGetValue(hitNodeId, out var node))
        {
            if (node.IsAccessible && !node.IsCompleted)
                EnterNode(node);
            // Erledigte Knoten: visuelles Feedback (Partikel an Tap-Position)
            else if (node.IsCompleted)
            {
                _particles.Emit(position.X, position.Y, 4, ParticleSystem.AmbientFloat);
            }
        }
    }

    private void EnterNode(MapNode node)
    {
        // Spieler-Position aktualisieren
        foreach (var n in _map!.Nodes)
            n.IsCurrent = false;
        node.IsCurrent = true;
        _player.CurrentNodeId = node.Id;

        // Je nach Knoten-Typ die entsprechende Szene starten
        switch (node.Type)
        {
            case MapNodeType.Story:
            case MapNodeType.SideQuest:
                // Story-Szene laden (wenn StoryNodeId vorhanden)
                if (!string.IsNullOrEmpty(node.StoryNodeId))
                {
                    _storyEngine.AdvanceToNode(node.StoryNodeId);
                    SceneManager.PushScene<DialogueScene>(new SlideTransition());
                }
                break;

            case MapNodeType.Boss:
            case MapNodeType.Dungeon:
                // Kampf-Szene laden (StoryNode-Anbindung für Gegner-ID)
                if (!string.IsNullOrEmpty(node.StoryNodeId))
                    _storyEngine.AdvanceToNode(node.StoryNodeId);
                SceneManager.PushScene<BattleScene>(new FadeTransition());
                break;

            case MapNodeType.Npc:
                // NPC/Shop-Szene
                SceneManager.PushScene<ShopScene>();
                break;

            case MapNodeType.Rest:
                // Raststätte: HP/MP heilen + Partikel-Feedback
                _player.FullHeal();
                node.IsCompleted = true;
                UpdateNodeAccessibility();
                AutoSaveAsync();
                break;
        }
    }

    /// <summary>
    /// Auto-Save beim Knoten-Wechsel (Slot 0).
    /// </summary>
    private async void AutoSaveAsync()
    {
        try
        {
            var sp = App.Services;
            var playTime = await _saveGameService.GetPlayTimeAsync(0);
            await _saveGameService.SaveGameAsync(0, _player,
                sp.GetRequiredService<SkillService>(),
                sp.GetRequiredService<InventoryService>(),
                sp.GetRequiredService<AffinityService>(),
                sp.GetRequiredService<FateTrackingService>(),
                sp.GetRequiredService<CodexService>(),
                playTime);
        }
        catch
        {
            // Auto-Save Fehler sind nicht kritisch
        }
    }

    private void LoadMap(string chapterId)
    {
        _nodeIndex.Clear();

        try
        {
            var resourceName = $"RebornSaga.Data.Maps.overworld_{chapterId}.json";
            using var stream = typeof(StoryEngine).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            _map = JsonSerializer.Deserialize<ChapterMap>(json, JsonOptions);

            if (_map == null) return;

            // AI-generierten Regions-Hintergrund laden (wenn vorhanden)
            var regionBg = _spriteCache?.GetBackground($"map/regions/{_map.ChapterId}");
            OverworldRenderer.SetRegionBackground(regionBg);

            // Index und Display-Namen aufbauen
            _displayNames.Clear();
            var prefix = $"Map_{_map.ChapterId.ToUpperInvariant()}_";
            foreach (var node in _map.Nodes)
            {
                _nodeIndex[node.Id] = node;
                _displayNames[node.Id] = node.NameKey.StartsWith(prefix)
                    ? node.NameKey[prefix.Length..]
                    : node.NameKey;
            }

            // Initiale Zugänglichkeit setzen
            InitializeNodeState();
        }
        catch (Exception)
        {
            // Map konnte nicht geladen werden - bleibt null
            _map = null;
        }
    }

    private void InitializeNodeState()
    {
        if (_map == null) return;

        // Start-Knoten ist immer erreichbar
        if (_nodeIndex.TryGetValue(_map.StartNodeId, out var startNode))
        {
            startNode.IsAccessible = true;
            startNode.IsRevealed = true;

            // Wenn der Spieler keinen aktuellen Knoten hat, Start setzen
            if (string.IsNullOrEmpty(_player.CurrentNodeId))
            {
                startNode.IsCurrent = true;
                _player.CurrentNodeId = startNode.Id;
            }
        }

        // Aktuellen Spieler-Knoten markieren
        if (_nodeIndex.TryGetValue(_player.CurrentNodeId, out var currentNode))
            currentNode.IsCurrent = true;

        // Alle Knoten die vor dem aktuellen liegen, als erledigt markieren
        // (Vereinfacht: alle Knoten mit Verbindung ZUM aktuellen Knoten sind besucht)
        MarkPredecessorsCompleted(_player.CurrentNodeId);

        UpdateNodeAccessibility();
    }

    /// <summary>
    /// Aktualisiert welche Knoten erreichbar sind basierend auf erledigten Knoten.
    /// Ein Knoten ist erreichbar wenn ein verbundener Knoten erledigt oder aktuell ist.
    /// </summary>
    private void UpdateNodeAccessibility()
    {
        if (_map == null) return;

        // Alle Knoten die von einem erledigten/aktuellen Knoten aus verbunden sind, werden erreichbar
        foreach (var node in _map.Nodes)
        {
            if (node.IsCompleted || node.IsCurrent)
            {
                node.IsAccessible = true;
                node.IsRevealed = true;

                // Verbundene Knoten aufdecken und erreichbar machen
                foreach (var connId in node.Connections)
                {
                    if (_nodeIndex.TryGetValue(connId, out var connected))
                    {
                        connected.IsRevealed = true;
                        connected.IsAccessible = true;

                        // Prüfe Required-Flag
                        if (!string.IsNullOrEmpty(connected.RequiredFlag) && !_player.Flags.Contains(connected.RequiredFlag))
                            connected.IsAccessible = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Markiert alle Vorgänger-Knoten des aktuellen Knotens als erledigt.
    /// Nutzt die Verbindungen rückwärts: wenn ein Knoten den Ziel-Knoten als Connection hat,
    /// ist er ein Vorgänger.
    /// </summary>
    private void MarkPredecessorsCompleted(string currentNodeId)
    {
        if (_map == null || string.IsNullOrEmpty(currentNodeId)) return;
        if (!_nodeIndex.ContainsKey(currentNodeId)) return;

        // BFS rückwärts: Finde alle Knoten die ZUM aktuellen führen
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(currentNodeId);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!visited.Add(nodeId)) continue;

            // Alle Knoten finden die diesen Knoten als Connection haben (Vorgänger)
            foreach (var node in _map.Nodes)
            {
                if (node.Id == nodeId) continue;
                if (node.Connections.Contains(nodeId) && visited.Add(node.Id))
                {
                    node.IsCompleted = true;
                    node.IsAccessible = true;
                    node.IsRevealed = true;
                    queue.Enqueue(node.Id);
                }
            }
        }
    }

    private void UpdateCachedHudStrings()
    {
        if (_player.Hp != _lastPlayerHp || _player.MaxHp != _lastPlayerMaxHp)
        {
            _cachedHpText = $"HP {_player.Hp}/{_player.MaxHp}";
            _lastPlayerHp = _player.Hp;
            _lastPlayerMaxHp = _player.MaxHp;
        }

        if (_player.Level != _lastPlayerLevel)
        {
            _cachedLevelText = $"Lv.{_player.Level}";
            _lastPlayerLevel = _player.Level;
        }

        if (_player.Gold != _lastPlayerGold)
        {
            _cachedGoldText = $"{_player.Gold}G";
            _lastPlayerGold = _player.Gold;
        }

        if (_map != null && string.IsNullOrEmpty(_cachedChapterName))
        {
            _cachedChapterName = _map.ChapterNameKey;
        }
    }

    public override void OnExit()
    {
        _particles.Dispose();
    }

    public void Dispose()
    {
        _particles.Dispose();
    }

    /// <summary>
    /// Gibt alle statischen Renderer-Ressourcen frei. Beim App-Beenden aufrufen.
    /// </summary>
    public static void Cleanup()
    {
        NodeRenderer.Cleanup();
        PathRenderer.Cleanup();
        OverworldRenderer.Cleanup();
    }
}
