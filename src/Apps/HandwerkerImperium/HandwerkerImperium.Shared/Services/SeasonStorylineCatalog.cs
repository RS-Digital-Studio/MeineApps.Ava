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
/// Stand 09.05.2026: Alle 4 Saisons (Spring/Summer/Autumn/Winter) komplett ausgeschrieben.
/// 5 Kapitel je Saison × 4 Saisons = 20 narrative Story-Beats. RESX-Lokalisierung in
/// 6 Sprachen (DE/EN/ES/FR/IT/PT) plus neutral wird beim naechsten Localization-Pass
/// nachgezogen — die Fallback-Texte sind allesamt Schreibtext-Qualitaet, kein Stub.
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

        // ───────────── Summer: „Die Insel-Auftrag" — voll ausgeschrieben (AAA-Audit P1) ─────────────
        yield return new StoryChapter
        {
            Id = "season_summer_ch1", ChapterNumber = 105,
            TitleKey = "SeasonStory_Summer_Ch1_Title",
            TextKey = "SeasonStory_Summer_Ch1_Text",
            TitleFallback = "Sommer auf der Insel",
            TextFallback = "Es ist passiert, Meister! Ein milliardenschwerer Investor hat eine ganze Insel gekauft und will dort sein Traum-Resort bauen. Drei Konkurrenz-Firmen sind im Rennen — aber er will dich. Er hat gehört, was wir im Frühling für die Stadt geleistet haben. Pack die Werkzeuge, das Schiff fährt um sechs!",
            RequiredBattlePassTier = 1, RequiredSeasonTheme = Season.Summer,
            MoneyReward = 500_000m, GoldenScrewReward = 8, XpReward = 300, Mood = "excited"
        };
        yield return new StoryChapter
        {
            Id = "season_summer_ch2", ChapterNumber = 106,
            TitleKey = "SeasonStory_Summer_Ch2_Title",
            TextKey = "SeasonStory_Summer_Ch2_Text",
            TitleFallback = "Der Hafen-Auftrag",
            TextFallback = "Tier 10! Der alte Inselhafen ist verrottet — Holz morsch, Pfähle wackelig, Liegeplätze für die Resort-Yachten gibt's keine. Der Investor will einen Yacht-Hafen für 50 Boote, in 6 Wochen einsatzbereit. Klingt nach viel? Genau — und genau das machen wir. Trommle die Crew zusammen, Meister!",
            RequiredBattlePassTier = 10, RequiredSeasonTheme = Season.Summer,
            MoneyReward = 2_500_000m, GoldenScrewReward = 18, XpReward = 1_000, Mood = "proud"
        };
        yield return new StoryChapter
        {
            Id = "season_summer_ch3", ChapterNumber = 107,
            TitleKey = "SeasonStory_Summer_Ch3_Title",
            TextKey = "SeasonStory_Summer_Ch3_Text",
            TitleFallback = "Sturm-Notdienst",
            TextFallback = "Tier 25 — und die schlechtesten Nachrichten des Jahrzehnts. Ein Jahrhundert-Hurrikan hat die Insel getroffen. Das halbe Resort ist hin, Dächer wie Konfetti, der Hafen unter Wasser. Der Investor steht kurz vorm Aufgeben. Aber wir geben nicht auf, hörst du? Wir haben 14 Tage. Stadt evakuieren, Schäden kartieren, neu bauen. Los, Meister!",
            RequiredBattlePassTier = 25, RequiredSeasonTheme = Season.Summer,
            MoneyReward = 7_500_000m, GoldenScrewReward = 30, XpReward = 2_000, Mood = "concerned"
        };
        yield return new StoryChapter
        {
            Id = "season_summer_ch4", ChapterNumber = 108,
            TitleKey = "SeasonStory_Summer_Ch4_Title",
            TextKey = "SeasonStory_Summer_Ch4_Text",
            TitleFallback = "Das Insel-Spektakel",
            TextFallback = "Tier 40! Die Resort-Eröffnung wird zum Spektakel. 2000 Gäste, internationale Presse, Feuerwerk so groß, dass man es bis zum Festland sieht. Du stehst neben dem Investor auf der Bühne, in der Hand das goldene Inselschloss-Zertifikat. Die Welt schaut zu, Meister. Genieß den Moment — du hast ihn dir verdient.",
            RequiredBattlePassTier = 40, RequiredSeasonTheme = Season.Summer,
            MoneyReward = 35_000_000m, GoldenScrewReward = 60, XpReward = 3_500, Mood = "excited"
        };
        yield return new StoryChapter
        {
            Id = "season_summer_ch5", ChapterNumber = 109,
            TitleKey = "SeasonStory_Summer_Ch5_Title",
            TextKey = "SeasonStory_Summer_Ch5_Text",
            TitleFallback = "Capstone: Insel-Architekt",
            TextFallback = "Tier 50! Der Investor hat das Resort offiziell nach dir benannt. „Meister-Resort“ steht in Leuchtbuchstaben über dem Eingang. Aber das ist nicht alles — er bietet uns einen Mehrjahres-Vertrag für drei weitere Inseln. Was sagst du, Meister? Sehen wir den Herbst zuerst — die Innungen rufen zum Jahreswettbewerb.",
            RequiredBattlePassTier = 50, RequiredSeasonTheme = Season.Summer,
            MoneyReward = 150_000_000m, GoldenScrewReward = 120, XpReward = 8_500, Mood = "proud"
        };

        // ───────────── Autumn: „Wettbewerb der Innungen" — voll ausgeschrieben ─────────────
        yield return new StoryChapter
        {
            Id = "season_autumn_ch1", ChapterNumber = 110,
            TitleKey = "SeasonStory_Autumn_Ch1_Title",
            TextKey = "SeasonStory_Autumn_Ch1_Text",
            TitleFallback = "Der Herbst-Wettbewerb beginnt",
            TextFallback = "Es ist soweit, Meister! Die Innungen rufen zum großen Jahreswettbewerb. 28 Werkstätten aus dem ganzen Land treten gegeneinander an, in fünf Disziplinen: Holz, Stein, Metall, Glas und Innovation. Der Sieger bekommt den Goldenen Hammer und ein Jahr lang die besten Aufträge des Königreichs. Bist du bereit?",
            RequiredBattlePassTier = 1, RequiredSeasonTheme = Season.Autumn,
            MoneyReward = 700_000m, GoldenScrewReward = 10, XpReward = 350, Mood = "excited"
        };
        yield return new StoryChapter
        {
            Id = "season_autumn_ch2", ChapterNumber = 111,
            TitleKey = "SeasonStory_Autumn_Ch2_Title",
            TextKey = "SeasonStory_Autumn_Ch2_Text",
            TitleFallback = "Erste Runde",
            TextFallback = "Tier 10! Die Vorrunde läuft. Drei Aufgaben in 72 Stunden: ein Eichentisch für 12 Personen, eine Brunnen-Restaurierung am Marktplatz, und eine Lichterkette für das Stadtfest. Klingt machbar? Vielleicht. Aber drei Konkurrenten haben die gleichen Aufträge. Wer schneller und schöner liefert, kommt eine Runde weiter. Tempo, Meister!",
            RequiredBattlePassTier = 10, RequiredSeasonTheme = Season.Autumn,
            MoneyReward = 3_500_000m, GoldenScrewReward = 22, XpReward = 1_200, Mood = "proud"
        };
        yield return new StoryChapter
        {
            Id = "season_autumn_ch3", ChapterNumber = 112,
            TitleKey = "SeasonStory_Autumn_Ch3_Title",
            TextKey = "SeasonStory_Autumn_Ch3_Text",
            TitleFallback = "Halbfinale",
            TextFallback = "Tier 25! Wir sind im Halbfinale, Meister — vier Innungen, ein Sieger. Unsere Gegner: Eisenhammer aus Norden, Glasbläser-Gilde Süd, und die Möbeldynastie aus dem Osten. Die Aufgabe: Ein vollständiges Wohnhaus in 14 Tagen. Schlüsselfertig, hochwertig, stilvoll. Die Konkurrenz hat doppelt so viele Worker wie wir. Aber wir haben dich.",
            RequiredBattlePassTier = 25, RequiredSeasonTheme = Season.Autumn,
            MoneyReward = 12_000_000m, GoldenScrewReward = 38, XpReward = 2_400, Mood = "proud"
        };
        yield return new StoryChapter
        {
            Id = "season_autumn_ch4", ChapterNumber = 113,
            TitleKey = "SeasonStory_Autumn_Ch4_Title",
            TextKey = "SeasonStory_Autumn_Ch4_Text",
            TitleFallback = "Das Finale",
            TextFallback = "Tier 40! Wir sind im Finale, Meister! Letzter Gegner: Die Möbeldynastie. Aufgabe: Ein königliches Schmuckstück. Nur 7 Tage. Die Punktrichter sind streng, das Publikum erwartet eine Show. Die Möbeldynastie hat 200 Jahre Tradition — wir haben sechs Monate Erfahrung. Aber Erfahrung ist nicht alles. Kreativität schlägt Tradition, wenn der Funke springt. Lass ihn springen.",
            RequiredBattlePassTier = 40, RequiredSeasonTheme = Season.Autumn,
            MoneyReward = 50_000_000m, GoldenScrewReward = 80, XpReward = 4_500, Mood = "concerned"
        };
        yield return new StoryChapter
        {
            Id = "season_autumn_ch5", ChapterNumber = 114,
            TitleKey = "SeasonStory_Autumn_Ch5_Title",
            TextKey = "SeasonStory_Autumn_Ch5_Text",
            TitleFallback = "Capstone: Wettbewerbssieger",
            TextFallback = "Tier 50! Wir haben gewonnen, Meister! Der Goldene Hammer ist unser. Das gesamte Königreich kennt jetzt unseren Namen. Die Möbeldynastie hat sich verbeugt — alte Tradition vor neuer Energie. Du hast uns dahin gebracht, wo niemand uns vermutet hätte. Heute Abend: Stadtfest, in unserem Namen. Morgen: Der erste Schnee fällt — und der Winter braucht Helden.",
            RequiredBattlePassTier = 50, RequiredSeasonTheme = Season.Autumn,
            MoneyReward = 200_000_000m, GoldenScrewReward = 150, XpReward = 10_000, Mood = "excited"
        };

        // ───────────── Winter: „Der Sturm-Notdienst" — voll ausgeschrieben ─────────────
        yield return new StoryChapter
        {
            Id = "season_winter_ch1", ChapterNumber = 115,
            TitleKey = "SeasonStory_Winter_Ch1_Title",
            TextKey = "SeasonStory_Winter_Ch1_Text",
            TitleFallback = "Der erste Schnee",
            TextFallback = "Der Winter ist da, Meister. Über Nacht 30 Zentimeter Neuschnee, und jetzt klingelt das Telefon im Minutentakt. Eingefrorene Rohre, abgebrochene Dachrinnen, kaputte Heizungen — die Stadt ist im Notstand. Der Bürgermeister hat einen offiziellen Notdienst-Auftrag ausgesprochen. Wir sind die ersten auf der Liste. Anziehen, Meister — heute wird's kalt.",
            RequiredBattlePassTier = 1, RequiredSeasonTheme = Season.Winter,
            MoneyReward = 600_000m, GoldenScrewReward = 9, XpReward = 320, Mood = "concerned"
        };
        yield return new StoryChapter
        {
            Id = "season_winter_ch2", ChapterNumber = 116,
            TitleKey = "SeasonStory_Winter_Ch2_Title",
            TextKey = "SeasonStory_Winter_Ch2_Text",
            TitleFallback = "Frostschäden",
            TextFallback = "Tier 10! Minus 20 Grad seit drei Nächten — und die Frostschäden explodieren. Im Bezirk Eichenwald sind 47 Häuser ohne Wasser. Im Westen platzen die Gussrohre wie Glasflaschen. Wir haben die einzige Crew mit genug Klempner-Worker, um den Notstand zu händeln. Doppelschichten, Meister. Aber die Stadt zahlt — und sie zahlt gut.",
            RequiredBattlePassTier = 10, RequiredSeasonTheme = Season.Winter,
            MoneyReward = 3_000_000m, GoldenScrewReward = 20, XpReward = 1_100, Mood = "concerned"
        };
        yield return new StoryChapter
        {
            Id = "season_winter_ch3", ChapterNumber = 117,
            TitleKey = "SeasonStory_Winter_Ch3_Title",
            TextKey = "SeasonStory_Winter_Ch3_Text",
            TitleFallback = "Der große Sturm",
            TextFallback = "Tier 25! Ein Jahrhundert-Sturm fegt über die Region. Kein Strom, keine Heizung, kein Telefon. Die halbe Stadt sitzt im Dunkeln, viele Familien frieren. Der Bürgermeister ruft den Katastrophenfall aus und übergibt uns die Notfall-Leitung des Wiederaufbaus. Das ist mehr als ein Auftrag, Meister — das ist Verantwortung. Aber wer wenn nicht wir?",
            RequiredBattlePassTier = 25, RequiredSeasonTheme = Season.Winter,
            MoneyReward = 10_000_000m, GoldenScrewReward = 35, XpReward = 2_200, Mood = "concerned"
        };
        yield return new StoryChapter
        {
            Id = "season_winter_ch4", ChapterNumber = 118,
            TitleKey = "SeasonStory_Winter_Ch4_Title",
            TextKey = "SeasonStory_Winter_Ch4_Text",
            TitleFallback = "Wiederaufbau",
            TextFallback = "Tier 40! Der Sturm ist vorbei, der Wiederaufbau läuft. Wir bauen Tag und Nacht. 80 Häuser brauchen neue Dächer. 200 Familien brauchen wieder Heizung. Die Konkurrenz hilft mit — das hat es noch nie gegeben. Heute streiten wir nicht, heute bauen wir. Stuhlbein an Stuhlbein, wie du sagen würdest. Diese Stadt vergisst das nie.",
            RequiredBattlePassTier = 40, RequiredSeasonTheme = Season.Winter,
            MoneyReward = 40_000_000m, GoldenScrewReward = 70, XpReward = 4_000, Mood = "proud"
        };
        yield return new StoryChapter
        {
            Id = "season_winter_ch5", ChapterNumber = 119,
            TitleKey = "SeasonStory_Winter_Ch5_Title",
            TextKey = "SeasonStory_Winter_Ch5_Text",
            TitleFallback = "Capstone: Held des Winters",
            TextFallback = "Tier 50! Die Stadt hat dich heute zum „Helden des Winters“ ernannt. Eine Bronze-Statue auf dem Marktplatz, mit deinem Hammer und deinem Namen. Die Familien, die wir gerettet haben, kommen zur Einweihung. Sie bringen Kerzen, Schokolade, Kuchen. Es ist nicht das Geld, Meister — es ist das hier. Genau das ist es, wofür wir das alles machen. Auf den nächsten Frühling.",
            RequiredBattlePassTier = 50, RequiredSeasonTheme = Season.Winter,
            MoneyReward = 180_000_000m, GoldenScrewReward = 130, XpReward = 9_000, Mood = "excited"
        };
    }
}
