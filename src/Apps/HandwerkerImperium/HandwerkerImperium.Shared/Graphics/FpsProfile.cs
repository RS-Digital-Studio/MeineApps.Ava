using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Zentrale Tabelle aller Render-Intervalle pro Grafik-Qualitaetsstufe.
///
/// Zweck: Bis v2.0.33 hatte jeder Render-Timer seine Intervalle hartcoded
/// (<c>TimeSpan.FromMilliseconds(33) // 30fps</c>). <see cref="GraphicsQuality"/> steuerte
/// nur Wetter+Shimmer an/aus — die tatsaechlichen FPS blieben gleich. Low-End-Spieler
/// hatten keine Moeglichkeit, den Battery-Drain zu reduzieren.
///
/// Mit dieser Klasse liest jede View ihre Intervalle zur Laufzeit aus dem aktuell
/// aktiven Profil. Aenderung in den Settings wirkt beim naechsten <c>StartRenderLoop()</c>.
///
/// Referenz (v2.0.34):
/// <code>
/// | View / Renderer | Low | Medium       | High         |
/// |------------------------|--------------|--------------|--------------|
/// | MiniGame (10 Views) | 24fps (42ms) | 30fps (33ms) | 30fps (33ms) |
/// | Research/Workshop- | 15fps (66ms) | 20fps (50ms) | 24fps (42ms) |
/// | Guild-Research |              |              |              |
/// | Dashboard Basis | 5fps (200ms) | 10fps (100ms)| 10fps (100ms)|
/// | Dashboard bei Effekten | 15fps (66ms) | 24fps (42ms) | 30fps (33ms) |
/// | WorkerAvatar shared | 5fps (200ms) | 8fps (125ms) | 10fps (100ms)|
/// | MainView (Bg + TabBar) | 10fps (100ms)| 15fps (66ms) | 15fps (66ms) |
/// </code>
///
/// Hinweis: Die Werte sind bewusst konservativ — selbst bei High werden die alten
/// Raten teilweise reduziert (Research/Workshop: 30→24fps, WorkerAvatar 20→10fps).
/// Diese Senkungen sind visuell nicht unterscheidbar (24fps = Kino-Standard), sparen
/// aber durchgaengig Battery.
/// </summary>
public static class FpsProfile
{
    /// <summary>
    /// Aktuelle Qualitaetsstufe. Wird bei App-Start auf Plattform-Default gesetzt
    /// (Android=Medium, Desktop=High) und beim GameState-Load mit den Spieler-Settings
    /// ueberschrieben. Aenderungen in den Settings aktualisieren den Wert sofort;
    /// bereits laufende Render-Timer lesen den neuen Wert bei ihrem naechsten Neustart
    /// (z.B. Tab-Wechsel, IsVisible-Toggle).
    /// </summary>
    public static GraphicsQuality Current { get; set; } = GraphicsQuality.Medium;

    /// <summary>
    /// Wird gefeuert, wenn <see cref="Current"/> sich aendert. Views koennen damit
    /// ihren aktiven Render-Timer sofort neu konfigurieren.
    /// </summary>
    public static event Action<GraphicsQuality>? CurrentChanged;

    /// <summary>
    /// Setzt <see cref="Current"/> und feuert <see cref="CurrentChanged"/> bei Wertaenderung.
    /// </summary>
    public static void SetCurrent(GraphicsQuality quality)
    {
        if (Current == quality) return;
        Current = quality;
        CurrentChanged?.Invoke(quality);
    }

    /// <summary>
    /// Liefert den aktiven Render-Intervall fuer MiniGame-Views.
    /// Bei Low auf 24fps reduziert — Gameplay bleibt fluessig, Battery gewonnen.
    /// </summary>
    public static TimeSpan MiniGame() => MiniGame(Current);

    /// <summary>Komfort-Overload: Nutzt <see cref="Current"/>.</summary>
    public static TimeSpan ScrollView() => ScrollView(Current);

    /// <summary>Komfort-Overload: Nutzt <see cref="Current"/>.</summary>
    public static TimeSpan DashboardIdle() => DashboardIdle(Current);

    /// <summary>Komfort-Overload: Nutzt <see cref="Current"/>.</summary>
    public static TimeSpan DashboardActive() => DashboardActive(Current);

    /// <summary>Komfort-Overload: Nutzt <see cref="Current"/>.</summary>
    public static TimeSpan WorkerAvatar() => WorkerAvatar(Current);

    /// <summary>Komfort-Overload: Nutzt <see cref="Current"/>.</summary>
    public static TimeSpan MainView() => MainView(Current);

    /// <summary>
    /// Liefert den aktiven Render-Intervall fuer MiniGame-Views.
    /// Bei Low auf 24fps reduziert — Gameplay bleibt fluessig, Battery gewonnen.
    /// </summary>
    public static TimeSpan MiniGame(GraphicsQuality q) => q switch
    {
        GraphicsQuality.Low => TimeSpan.FromMilliseconds(42),      // 24fps
        GraphicsQuality.Medium => TimeSpan.FromMilliseconds(33),   // 30fps
        _ => TimeSpan.FromMilliseconds(33)                          // 30fps
    };

    /// <summary>
    /// Liefert den Intervall fuer Idle-/Scroll-basierte Views (Research/Workshop/GuildResearch).
    /// Keine Echtzeit-Gameplay, daher konservativer.
    /// </summary>
    public static TimeSpan ScrollView(GraphicsQuality q) => q switch
    {
        GraphicsQuality.Low => TimeSpan.FromMilliseconds(66),      // 15fps
        GraphicsQuality.Medium => TimeSpan.FromMilliseconds(50),   // 20fps
        _ => TimeSpan.FromMilliseconds(42)                          // 24fps
    };

    /// <summary>
    /// Liefert den Basis-Intervall fuer die Dashboard-City-Canvas (Idle-Modus, ohne Effekte).
    /// </summary>
    public static TimeSpan DashboardIdle(GraphicsQuality q) => q switch
    {
        GraphicsQuality.Low => TimeSpan.FromMilliseconds(200),     // 5fps
        GraphicsQuality.Medium => TimeSpan.FromMilliseconds(100),  // 10fps
        _ => TimeSpan.FromMilliseconds(100)                         // 10fps
    };

    /// <summary>
    /// Liefert den erhoehten Intervall fuer Dashboard-City-Canvas bei aktiven Effekten
    /// (Coin-Fly, Confetti, ScreenShake, Wetter mit Partikel-Burst).
    /// </summary>
    public static TimeSpan DashboardActive(GraphicsQuality q) => q switch
    {
        GraphicsQuality.Low => TimeSpan.FromMilliseconds(66),      // 15fps
        GraphicsQuality.Medium => TimeSpan.FromMilliseconds(42),   // 24fps
        _ => TimeSpan.FromMilliseconds(33)                          // 30fps
    };

    /// <summary>
    /// Liefert den Intervall fuer den geteilten WorkerAvatar-Timer
    /// (Atem + Blinzel-Animation, viele Avatare parallel).
    /// </summary>
    public static TimeSpan WorkerAvatar(GraphicsQuality q) => q switch
    {
        GraphicsQuality.Low => TimeSpan.FromMilliseconds(200),     // 5fps
        GraphicsQuality.Medium => TimeSpan.FromMilliseconds(125),  // 8fps
        _ => TimeSpan.FromMilliseconds(100)                         // 10fps
    };

    /// <summary>
    /// Liefert den Intervall fuer den MainView-Timer (Background + TabBar + Ceremony).
    /// </summary>
    public static TimeSpan MainView(GraphicsQuality q) => q switch
    {
        GraphicsQuality.Low => TimeSpan.FromMilliseconds(100),     // 10fps
        GraphicsQuality.Medium => TimeSpan.FromMilliseconds(66),   // 15fps
        _ => TimeSpan.FromMilliseconds(66)                          // 15fps
    };

    /// <summary>
    /// Liefert das aktuelle Profil aus dem <see cref="SettingsData.GraphicsQuality"/>.
    /// Komfort-Overload fuer Views die das GameStateService injiziert haben.
    /// </summary>
    public static GraphicsQuality CurrentQuality(SettingsData? settings)
        => settings?.GraphicsQuality ?? GraphicsQuality.Medium;
}
