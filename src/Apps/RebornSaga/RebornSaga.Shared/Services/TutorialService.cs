namespace RebornSaga.Services;

using MeineApps.Core.Ava.Services;
using System.Collections.Generic;

/// <summary>
/// Verwaltet Tutorial-Hinweise. Speichert gesehene Hints in Preferences.
/// Jeder Hint hat eine eindeutige ID und wird nur einmal angezeigt.
/// </summary>
public class TutorialService
{
    private readonly IPreferencesService _preferences;
    private readonly HashSet<string> _seenHints = new();

    public TutorialService(IPreferencesService preferences)
    {
        _preferences = preferences;
        LoadSeenHints();
    }

    /// <summary>Prüft ob ein Hint noch nie gesehen wurde.</summary>
    public bool ShouldShow(string hintId) => !_seenHints.Contains(hintId);

    /// <summary>Markiert einen Hint als gesehen.</summary>
    public void MarkSeen(string hintId)
    {
        if (_seenHints.Add(hintId))
            SaveSeenHints();
    }

    /// <summary>Setzt alle Hints zurück (für Debug/Neustart).</summary>
    public void ResetAll()
    {
        _seenHints.Clear();
        _preferences.Set("tutorial_seen_hints", "");
    }

    private void LoadSeenHints()
    {
        var raw = _preferences.Get("tutorial_seen_hints", "");
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var id in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            _seenHints.Add(id);
    }

    private void SaveSeenHints()
    {
        _preferences.Set("tutorial_seen_hints", string.Join(",", _seenHints));
    }
}

/// <summary>
/// Vordefinierte Tutorial-Hint-IDs.
/// </summary>
public static class TutorialHints
{
    public const string FirstBattle = "first_battle";
    public const string SkillSystem = "skill_system";
    public const string InventoryUse = "inventory_use";
    public const string ClassSelect = "class_select";
    public const string BondSystem = "bond_system";
    public const string KarmaSystem = "karma_system";
    public const string OverworldMap = "overworld_map";
    public const string SaveGame = "save_game";
    public const string GoldShop = "gold_shop";
    public const string CodexHint = "codex_hint";
    public const string AriaSystem = "aria_system";
    public const string DialogChoices = "dialog_choices";
    public const string EquipItems = "equip_items";
    public const string StatusWindow = "status_window";
}
