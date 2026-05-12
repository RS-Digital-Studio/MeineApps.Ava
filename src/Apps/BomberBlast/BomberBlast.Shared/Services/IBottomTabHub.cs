namespace BomberBlast.Services;

/// <summary>
/// Bottom-Tab-Hub (Sprint 3.1 AAA-Audit #4).
///
/// <para>
/// Konsolidiert 15+ sichtbare Navigationspunkte auf 4 Bottom-Tabs:
/// <list type="bullet">
/// <item><b>Home</b>: Story-Progression + Daily-Dashboard (Daily-Challenge, Race, Lucky-Spin als Karten)</item>
/// <item><b>Spielen</b>: Quick / Survival / Dungeon / Boss-Rush / Master als Liste mit Hero-Image</item>
/// <item><b>Shop</b>: Coin-Shop / Gem-Shop / Battle-Pass als horizontale Sub-Tabs</item>
/// <item><b>Profil</b>: Achievements / Collection / League / Statistics / Settings als ListView</item>
/// </list>
/// </para>
///
/// <para>
/// Side-Loops (Daily-Race, Lucky-Spin, Weekly) bleiben erreichbar aber als Karten im Home-Dashboard
/// statt als eigene Tabs. Mental-Load des Spielers sinkt drastisch.
/// </para>
///
/// <para>
/// HINWEIS: Sprint 3.1 ist Foundation-Service. Echte UI-Refactor der 974-LOC MainMenuView
/// (Buttons → Hero-Tiles + Card-Dashboard) ist eigener Game-Design-Sprint und braucht
/// User-Input was wo gruppiert wird. Service-Layer + Tab-Enum stehen bereit.
/// </para>
/// </summary>
public interface IBottomTabHub
{
    /// <summary>Aktueller Tab (Default: Home).</summary>
    BottomTab ActiveTab { get; }

    /// <summary>Wechselt den Tab + persistiert Auswahl. Feuert ActiveTabChanged.</summary>
    void SetActiveTab(BottomTab tab);

    /// <summary>Wird beim Tab-Wechsel gefeuert.</summary>
    event Action<BottomTab>? ActiveTabChanged;
}

/// <summary>Die 4 konsolidierten Bottom-Tabs (Brawl-Stars-Pattern).</summary>
public enum BottomTab
{
    Home = 0,
    Play = 1,
    Shop = 2,
    Profile = 3,
}

/// <summary>Default-Implementation mit Pref-Persistenz.</summary>
public sealed class BottomTabHub : IBottomTabHub
{
    private const string KeyActiveTab = "BottomTab_Active";
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _prefs;
    private BottomTab _activeTab;

    public BottomTab ActiveTab => _activeTab;
    public event Action<BottomTab>? ActiveTabChanged;

    public BottomTabHub(MeineApps.Core.Ava.Services.IPreferencesService prefs)
    {
        _prefs = prefs;
        int stored = _prefs.Get(KeyActiveTab, (int)BottomTab.Home);
        _activeTab = Enum.IsDefined(typeof(BottomTab), stored)
            ? (BottomTab)stored
            : BottomTab.Home;
    }

    public void SetActiveTab(BottomTab tab)
    {
        if (_activeTab == tab) return;
        _activeTab = tab;
        _prefs.Set(KeyActiveTab, (int)tab);
        ActiveTabChanged?.Invoke(tab);
    }
}
