using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// v2.0.60 (B-C1): D0-Modal-Priorisierung.
///
/// <para>Am D0 (erster App-Start des Spielers) können DailyReward + WhatsNew + FeatureUnlock
/// + Discovery-Overlay alle gleichzeitig triggern. Das ist eine Neuling-Überforderung —
/// der Spieler schließt alles weg ohne zu lesen.</para>
///
/// <para>Lösung: Dieser Gate gewährt am D0 nur EINEN Modal pro Session, in der Reihenfolge
/// WhatsNew > DailyReward > FeatureUnlock > Discovery. Ab D1 (zweite Session) ist der Gate
/// transparent — alle Modals dürfen erscheinen.</para>
/// </summary>
public interface ID0ModalGate
{
    /// <summary>True wenn die App das erste Mal seit Installation gestartet wird (D0-Session).</summary>
    bool IsD0Session { get; }

    /// <summary>True wenn in dieser Session bereits ein D0-Modal angezeigt wurde.</summary>
    bool HasShownD0ModalThisSession { get; }

    /// <summary>
    /// Versucht einen Modal-Slot zu beanspruchen. Returns true wenn der Modal angezeigt werden darf.
    /// Am D0 gibt nur der erste Caller true zurück — alle folgenden Modals der gleichen Session
    /// werden unterdrückt. Ab D1 immer true.
    /// </summary>
    /// <param name="modalType">Modal-Typ für Priorisierung (informational).</param>
    bool TryClaimModalSlot(D0ModalType modalType);

    /// <summary>Markiert die D0-Session als abgeschlossen (wird bei App-Shutdown aufgerufen).</summary>
    void MarkD0Complete();
}

/// <summary>Modal-Typen für D0-Priorisierung (in Wertigkeits-Reihenfolge).</summary>
public enum D0ModalType
{
    WhatsNew = 0,
    DailyReward = 1,
    FeatureUnlock = 2,
    Discovery = 3,
}

public sealed class D0ModalGate : ID0ModalGate
{
    private const string FirstLaunchCompleteKey = "D0Modal_FirstLaunchComplete";
    private readonly IPreferencesService _preferences;
    private bool _hasShownThisSession;

    public D0ModalGate(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    public bool IsD0Session => !_preferences.Get(FirstLaunchCompleteKey, false);
    public bool HasShownD0ModalThisSession => _hasShownThisSession;

    public bool TryClaimModalSlot(D0ModalType modalType)
    {
        // Ab D1 (FirstLaunchComplete=true) immer durchlassen.
        if (!IsD0Session) return true;

        // Am D0 nur ein Modal pro Session.
        if (_hasShownThisSession) return false;

        _hasShownThisSession = true;
        return true;
    }

    public void MarkD0Complete()
    {
        _preferences.Set(FirstLaunchCompleteKey, true);
    }
}
