using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IWorldStoryService"/> (Sprint 6.2 AAA-Audit #16).
///
/// <para>
/// Hardcoded Story-Beats fuer 10 Welten — RESX-Keys folgen dem Pattern
/// <c>WorldStoryIntroN_Welt</c> / <c>WorldStoryOutroN</c>.
/// Jede Welt hat: Intro mit Welt-Theme-Atmosphaere + Outro mit Cliffhanger zur naechsten Welt.
/// </para>
///
/// <para>
/// Beispiel:
/// <list type="bullet">
/// <item>Welt 1 Forest Intro: "Der Wald flüstert. Etwas Boeses ist hier verborgen — finde es."</item>
/// <item>Welt 2 Desert Intro: "Die Wueste flimmert. Ein alter Bombenleger ruht hier seit 100 Jahren. Wecke ihn nicht."</item>
/// <item>Welt 9 → Welt 10 Outro: "Die Schattenwelt oeffnet ihre Tore. Bist du bereit fuer die Dunkelheit?"</item>
/// </list>
/// </para>
/// </summary>
public sealed class WorldStoryService : IWorldStoryService
{
    private const string IntroSeenPrefix = "WorldStory_IntroSeen_";
    private const string OutroSeenPrefix = "WorldStory_OutroSeen_";

    private readonly IPreferencesService _prefs;

    public WorldStoryService(IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public StoryCutscene? GetIntro(int worldId)
    {
        if (worldId is < 1 or > 10) return null;
        return new StoryCutscene
        {
            WorldId = worldId,
            TextKey = $"WorldStoryIntro{worldId}",
            // World 1 hat keinen Stinger (Soft-Onboarding), ab Welt 2 Boss-Reveal-Stinger fuer Atmosphaere
            StingerKey = worldId >= 2 ? "stinger_boss_reveal" : null,
        };
    }

    public StoryCutscene? GetOutro(int worldId)
    {
        // Welt 10 hat keinen Outro (das ist das Ende)
        if (worldId is < 1 or > 9) return null;
        return new StoryCutscene
        {
            WorldId = worldId,
            TextKey = $"WorldStoryOutro{worldId}",
            StingerKey = "stinger_victory",
        };
    }

    public bool HasSeenIntro(int worldId) => _prefs.Get(IntroSeenPrefix + worldId, false);
    public bool HasSeenOutro(int worldId) => _prefs.Get(OutroSeenPrefix + worldId, false);

    public void MarkIntroSeen(int worldId) => _prefs.Set(IntroSeenPrefix + worldId, true);
    public void MarkOutroSeen(int worldId) => _prefs.Set(OutroSeenPrefix + worldId, true);
}
