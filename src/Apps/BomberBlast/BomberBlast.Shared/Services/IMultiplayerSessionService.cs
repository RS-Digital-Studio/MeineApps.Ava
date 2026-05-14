using BomberBlast.Core.Multiplayer;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Multiplayer-Session-Service (.2 .
///
/// <para>
/// Verwaltet den aktuellen Multiplayer-Modus + Player-Slot-Konfiguration.
/// Engine fragt diesen Service ab, um zu wissen ob Player2 gespawnt werden soll
/// und welche Input-Quellen aktiv sind.
/// </para>
///
/// <para>
///.2 ist Foundation-Layer: Mode-Selection-API + Persistenz. Die echte
/// Engine-Integration (Player2-Spawn, Dual-Input-Routing, Co-Op-Camera, GameOver
/// bei beide-tot, Splitscreen-Render) ist eigener Multi-Wochen-Sprint.
/// </para>
///
/// <para>
/// Verwendung:
/// <code>
/// // User waehlt 2P-Co-Op im Menue:
/// _multiplayerSession.SetMode(MultiplayerMode.LocalCoop);
/// // Engine prueft beim StartLevelAsync:
/// if (_multiplayerSession.IsCoopEnabled) SpawnPlayer2();
/// </code>
/// </para>
/// </summary>
public interface IMultiplayerSessionService
{
    /// <summary>Aktueller Multiplayer-Modus (Default: Single).</summary>
    MultiplayerMode CurrentMode { get; }

    /// <summary>True wenn Player 2 in der naechsten Session gespawnt werden soll.</summary>
    bool IsCoopEnabled { get; }

    /// <summary>True wenn Versus-Modus aktiv ist (Player 1 vs Player 2).</summary>
    bool IsVersusEnabled { get; }

    /// <summary>Wechselt den Modus + persistiert in Preferences. Feuert ModeChanged-Event.</summary>
    void SetMode(MultiplayerMode mode);

    /// <summary>Wird gefeuert wenn der Modus gewechselt wird.</summary>
    event Action<MultiplayerMode>? ModeChanged;
}

/// <summary>
/// Default-Implementation. Persistiert den Modus, Engine-Integration ist deferred.
/// </summary>
public sealed class MultiplayerSessionService : IMultiplayerSessionService
{
    private const string KeyCurrentMode = "Multiplayer_CurrentMode";

    private readonly IPreferencesService _prefs;
    private MultiplayerMode _currentMode;

    public MultiplayerMode CurrentMode => _currentMode;
    public bool IsCoopEnabled => _currentMode == MultiplayerMode.LocalCoop;
    public bool IsVersusEnabled => _currentMode == MultiplayerMode.LocalVersus;

    public event Action<MultiplayerMode>? ModeChanged;

    public MultiplayerSessionService(IPreferencesService prefs)
    {
        _prefs = prefs;
        var modeInt = _prefs.Get(KeyCurrentMode, (int)MultiplayerMode.Single);
        _currentMode = Enum.IsDefined(typeof(MultiplayerMode), modeInt)
            ? (MultiplayerMode)modeInt
            : MultiplayerMode.Single;
    }

    public void SetMode(MultiplayerMode mode)
    {
        if (_currentMode == mode) return;
        _currentMode = mode;
        _prefs.Set(KeyCurrentMode, (int)mode);
        ModeChanged?.Invoke(mode);
    }
}
