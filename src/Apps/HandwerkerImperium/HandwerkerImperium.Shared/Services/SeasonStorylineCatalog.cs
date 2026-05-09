using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services;

/// <summary>
/// Statischer Katalog der Saison-Storylines (v2.1.0).
/// Pro Saison-Theme (Spring/Summer/Autumn/Winter) gibt es einen narrativen Bogen mit
/// 5 Kapiteln, gebunden an die BP-Tier-Trigger 1, 10, 25, 40, 50.
///
/// Kapitel-IDs folgen dem Schema „season_{theme}_ch{1..5}" und werden zur Laufzeit aus
/// dem <see cref="StoryService"/> via <see cref="StoryChapter.RequiredBattlePassTier"/>
/// + <see cref="StoryChapter.RequiredSeasonTheme"/> freigeschaltet.
///
/// Stand 05.05.2026: Spring-Saison komplett mit deutschem Fallback-Text.
/// Summer/Autumn/Winter-Kapitel haben Platzhalter-Texte, die bei Story-Writing-Pass
/// ausgearbeitet werden — Spielmechanik (Tier-Trigger + Belohnung) ist bereits vollstaendig.
/// </summary>
public static class SeasonStorylineCatalog
{
    /// <summary>Alle 4 Saison-Storylines, indiziert nach <see cref="Season"/>.</summary>
    public static readonly Dictionary<Season, SeasonStoryline> Storylines = new()
    {
        [Season.Spring] = new SeasonStoryline
        {
            Theme = Season.Spring,
            ThemeKey = "SeasonStorySpringTheme",
            ChapterIds = ["season_spring_ch1", "season_spring_ch2", "season_spring_ch3", "season_spring_ch4", "season_spring_ch5"],
            TierTriggers = [1, 10, 25, 40, 50]
        },
        [Season.Summer] = new SeasonStoryline
        {
            Theme = Season.Summer,
            ThemeKey = "SeasonStorySummerTheme",
            ChapterIds = ["season_summer_ch1", "season_summer_ch2", "season_summer_ch3", "season_summer_ch4", "season_summer_ch5"],
            TierTriggers = [1, 10, 25, 40, 50]
        },
        [Season.Autumn] = new SeasonStoryline
        {
            Theme = Season.Autumn,
            ThemeKey = "SeasonStoryAutumnTheme",
            ChapterIds = ["season_autumn_ch1", "season_autumn_ch2", "season_autumn_ch3", "season_autumn_ch4", "season_autumn_ch5"],
            TierTriggers = [1, 10, 25, 40, 50]
        },
        [Season.Winter] = new SeasonStoryline
        {
            Theme = Season.Winter,
            ThemeKey = "SeasonStoryWinterTheme",
            ChapterIds = ["season_winter_ch1", "season_winter_ch2", "season_winter_ch3", "season_winter_ch4", "season_winter_ch5"],
            TierTriggers = [1, 10, 25, 40, 50]
        }
    };

    /// <summary>
    /// Liefert die <see cref="StoryChapter"/>-Definitionen fuer alle Saison-Kapitel.
    /// Werden vom <see cref="StoryService"/> beim Konstruktor zur Master-Liste hinzugefuegt.
    /// </summary>
    public static IEnumerable<StoryChapter> GetAllSeasonChapters()
    {
        // ───────────── Spring: „Der Aufschwung der Stadt" ─────────────
        yield return new StoryChapter
        {
            Id = "season_spring_ch1",
            ChapterNumber = 100,
            TitleKey = "SeasonStory_Spring_Ch1_Title",
            TextKey = "SeasonStory_Spring_Ch1_Text",
            TitleFallback = "Frühlingserwachen",
            TextFallback = "Die Stadt erwacht aus dem Winterschlaf, Meister! Überall blühen Baustellen wie Blumen. Der Bürgermeister hat einen Stadt-Erweiterungsplan angekündigt — und du sollst dabei sein!",
            RequiredBattlePassTier = 1,
            RequiredSeasonTheme = Season.Spring,
            MoneyReward = 100_000,
            GoldenScrewReward = 5,
            XpReward = 200,
            Mood = "excited"
        };
        yield return new StoryChapter
        {
            Id = "season_spring_ch2",
            ChapterNumber = 101,
            TitleKey = "SeasonStory_Spring_Ch2_Title",
            TextKey = "SeasonStory_Spring_Ch2_Text",
            TitleFallback = "Erste Auftraege der Stadt",
            TextFallback = "Tier 10! Der Stadtrat hat unsere Werkstaetten ausgewaehlt. Drei neue Wohnviertel brauchen Handwerker — und du leitest die Vergabe! Zeig was du kannst, Meister!",
            RequiredBattlePassTier = 10,
            RequiredSeasonTheme = Season.Spring,
            MoneyReward = 1_000_000,
            GoldenScrewReward = 15,
            XpReward = 750,
            Mood = "proud"
        };
        yield return new StoryChapter
        {
            Id = "season_spring_ch3",
            ChapterNumber = 102,
            TitleKey = "SeasonStory_Spring_Ch3_Title",
            TextKey = "SeasonStory_Spring_Ch3_Text",
            TitleFallback = "Konkurrenz aus der Nachbarstadt",
            TextFallback = "Tier 25! Schlechte Nachrichten — eine Konkurrenz-Firma will unsere Stadt-Auftraege uebernehmen. Aber wir geben nicht klein bei. Mit deiner Crew sind wir unschlagbar!",
            RequiredBattlePassTier = 25,
            RequiredSeasonTheme = Season.Spring,
            MoneyReward = 5_000_000,
            GoldenScrewReward = 25,
            XpReward = 1_500,
            Mood = "concerned"
        };
        yield return new StoryChapter
        {
            Id = "season_spring_ch4",
            ChapterNumber = 103,
            TitleKey = "SeasonStory_Spring_Ch4_Title",
            TextKey = "SeasonStory_Spring_Ch4_Text",
            TitleFallback = "Die grosse Stadt-Eroeffnung",
            TextFallback = "Tier 40! Die neue Stadt-Erweiterung wird eingeweiht — und unsere Werkstaetten sind das Herzstueck! Der Buergermeister will dich persoenlich ehren, Meister.",
            RequiredBattlePassTier = 40,
            RequiredSeasonTheme = Season.Spring,
            MoneyReward = 25_000_000,
            GoldenScrewReward = 50,
            XpReward = 3_000,
            Mood = "excited"
        };
        yield return new StoryChapter
        {
            Id = "season_spring_ch5",
            ChapterNumber = 104,
            TitleKey = "SeasonStory_Spring_Ch5_Title",
            TextKey = "SeasonStory_Spring_Ch5_Text",
            TitleFallback = "Capstone: Stadt-Bauherr des Jahres",
            TextFallback = "Tier 50! Die Stadt hat dich zum Bauherrn des Jahres gekuert! Dein Name steht jetzt auf einer Bronze-Tafel im Rathaus. Welche Saison wird wohl als Naechstes kommen, Meister?",
            RequiredBattlePassTier = 50,
            RequiredSeasonTheme = Season.Spring,
            MoneyReward = 100_000_000,
            GoldenScrewReward = 100,
            XpReward = 7_500,
            Mood = "proud"
        };

        // ───────────── Summer: „Die Insel-Auftrag" (Stub-Texte, story-writing pendant) ─────────────
        foreach (var (id, num, tier, title, text, money, gs, xp) in new[]
        {
            ("season_summer_ch1", 105, 1,  "Sommer auf der Insel",         "Ein Investor will eine Insel-Resort bauen. Pack die Werkzeuge, Meister!",  500_000m,    8, 300),
            ("season_summer_ch2", 106, 10, "Der Hafen-Auftrag",            "Tier 10. Der Inselhafen wartet auf unsere Crew.",                          2_500_000m, 18, 1_000),
            ("season_summer_ch3", 107, 25, "Sturm-Notdienst",              "Tier 25. Sturm zerstoert die halbe Insel — wir reparieren sie!",          7_500_000m, 30, 2_000),
            ("season_summer_ch4", 108, 40, "Das Insel-Spektakel",          "Tier 40. Die Eroeffnung mit Feuerwerk!",                                  35_000_000m, 60, 3_500),
            ("season_summer_ch5", 109, 50, "Capstone: Insel-Architekt",    "Tier 50! Das Resort traegt jetzt deinen Namen.",                          150_000_000m, 120, 8_500),
        })
        {
            yield return new StoryChapter
            {
                Id = id, ChapterNumber = num, TitleFallback = title, TextFallback = text,
                TitleKey = $"SeasonStory_Summer_Ch{num - 104}_Title",
                TextKey = $"SeasonStory_Summer_Ch{num - 104}_Text",
                RequiredBattlePassTier = tier, RequiredSeasonTheme = Season.Summer,
                MoneyReward = money, GoldenScrewReward = gs, XpReward = xp,
                Mood = tier == 50 ? "proud" : (tier == 25 ? "concerned" : "excited")
            };
        }

        // ───────────── Autumn: „Wettbewerb der Innungen" ─────────────
        foreach (var (id, num, tier, title, text, money, gs, xp) in new[]
        {
            ("season_autumn_ch1", 110, 1,  "Der Herbst-Wettbewerb beginnt",   "Die Innungen rufen zum Jahreswettbewerb. Bist du dabei, Meister?", 700_000m,    10, 350),
            ("season_autumn_ch2", 111, 10, "Erste Runde",                      "Tier 10 — die ersten Aufgaben kommen rein.",                       3_500_000m, 22, 1_200),
            ("season_autumn_ch3", 112, 25, "Halbfinale",                        "Tier 25. Nur noch 4 Innungen im Rennen — wir gehoeren dazu!",     12_000_000m, 38, 2_400),
            ("season_autumn_ch4", 113, 40, "Das Finale",                        "Tier 40. Kopf-an-Kopf-Rennen mit der Konkurrenz!",                 50_000_000m, 80, 4_500),
            ("season_autumn_ch5", 114, 50, "Capstone: Wettbewerbssieger",       "Tier 50! Pokal in der Hand — und ein Stadtfest in unserem Namen.", 200_000_000m, 150, 10_000),
        })
        {
            yield return new StoryChapter
            {
                Id = id, ChapterNumber = num, TitleFallback = title, TextFallback = text,
                TitleKey = $"SeasonStory_Autumn_Ch{num - 109}_Title",
                TextKey = $"SeasonStory_Autumn_Ch{num - 109}_Text",
                RequiredBattlePassTier = tier, RequiredSeasonTheme = Season.Autumn,
                MoneyReward = money, GoldenScrewReward = gs, XpReward = xp,
                Mood = tier == 50 ? "excited" : (tier == 40 ? "concerned" : "proud")
            };
        }

        // ───────────── Winter: „Der Sturm-Notdienst" ─────────────
        foreach (var (id, num, tier, title, text, money, gs, xp) in new[]
        {
            ("season_winter_ch1", 115, 1,  "Der erste Schnee",            "Wintereinbruch! Die Stadt braucht Notdienst-Handwerker.",           600_000m,    9, 320),
            ("season_winter_ch2", 116, 10, "Frostschaeden",                "Tier 10. Die Rohre platzen reihenweise.",                          3_000_000m, 20, 1_100),
            ("season_winter_ch3", 117, 25, "Der grosse Sturm",             "Tier 25. Ein Jahrhundert-Sturm — alles steht still.",              10_000_000m, 35, 2_200),
            ("season_winter_ch4", 118, 40, "Wiederaufbau",                  "Tier 40. Stuhlbein an Stuhlbein bauen wir die Stadt wieder auf.",  40_000_000m, 70, 4_000),
            ("season_winter_ch5", 119, 50, "Capstone: Held des Winters",   "Tier 50! Die Stadt ehrt dich als Held des Winters.",                180_000_000m, 130, 9_000),
        })
        {
            yield return new StoryChapter
            {
                Id = id, ChapterNumber = num, TitleFallback = title, TextFallback = text,
                TitleKey = $"SeasonStory_Winter_Ch{num - 114}_Title",
                TextKey = $"SeasonStory_Winter_Ch{num - 114}_Text",
                RequiredBattlePassTier = tier, RequiredSeasonTheme = Season.Winter,
                MoneyReward = money, GoldenScrewReward = gs, XpReward = xp,
                Mood = tier == 50 ? "excited" : (tier <= 25 ? "concerned" : "proud")
            };
        }
    }
}
