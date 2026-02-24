using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet die Story-Kapitel von Meister Hans (35 Kapitel).
/// Tutorial (1-5), Early Game (6-12), Mid Game (13-17), Prestige (18-21), Endgame (22-25), Endgame-Expansion (26-35).
/// </summary>
public class StoryService : IStoryService
{
    private readonly IGameStateService _gameStateService;
    private readonly List<StoryChapter> _chapters;

    public StoryService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _chapters = CreateChapters();
    }

    /// <summary>
    /// Prüft ob ein neues Story-Kapitel freigeschaltet wurde.
    /// Gibt das Kapitel zurück oder null.
    /// </summary>
    public StoryChapter? CheckForNewChapter()
    {
        var state = _gameStateService.State;

        foreach (var chapter in _chapters)
        {
            // Bereits gesehen? Überspringen
            if (state.ViewedStoryIds.Contains(chapter.Id))
                continue;

            // Bedingung erfüllt?
            if (IsChapterUnlocked(chapter, state))
            {
                state.PendingStoryId = chapter.Id;
                _gameStateService.MarkDirty();
                return chapter;
            }

            // Kapitel sind geordnet - wenn dieses nicht freigeschaltet ist,
            // sind spätere auch nicht freigeschaltet
            break;
        }

        return null;
    }

    /// <summary>
    /// Markiert ein Kapitel als gesehen und gibt Belohnungen.
    /// </summary>
    public void MarkChapterViewed(string chapterId)
    {
        var state = _gameStateService.State;
        if (!state.ViewedStoryIds.Contains(chapterId))
        {
            state.ViewedStoryIds.Add(chapterId);

            // Belohnungen nur beim ERSTEN Mal vergeben (Race-Condition-Schutz)
            var chapter = _chapters.FirstOrDefault(c => c.Id == chapterId);
            if (chapter != null)
            {
                if (chapter.MoneyReward > 0)
                {
                    // Geldbelohnung skaliert mit dem aktuellen Einkommen (~10 Min Einkommen als Minimum)
                    var netIncomePerSecond = _gameStateService.State.NetIncomePerSecond;
                    var scaledReward = Math.Max(chapter.MoneyReward, netIncomePerSecond * 600);
                    _gameStateService.AddMoney(scaledReward);
                }
                if (chapter.GoldenScrewReward > 0)
                    _gameStateService.AddGoldenScrews(chapter.GoldenScrewReward);
                if (chapter.XpReward > 0)
                    _gameStateService.AddXp(chapter.XpReward);
            }
        }

        if (state.PendingStoryId == chapterId)
        {
            state.PendingStoryId = null;
        }

        _gameStateService.MarkDirty();
    }

    /// <summary>
    /// Gibt das aktuell wartende (ungesehene) Kapitel zurück.
    /// </summary>
    public StoryChapter? GetPendingChapter()
    {
        var pendingId = _gameStateService.State.PendingStoryId;
        if (string.IsNullOrEmpty(pendingId)) return null;
        return _chapters.FirstOrDefault(c => c.Id == pendingId);
    }

    /// <summary>
    /// Gibt alle Kapitel zurück (für Story-Log).
    /// </summary>
    public IReadOnlyList<StoryChapter> GetAllChapters() => _chapters;

    /// <summary>
    /// Anzahl gesehener Kapitel / Gesamtanzahl.
    /// </summary>
    public (int viewed, int total) GetProgress()
    {
        var viewed = _gameStateService.State.ViewedStoryIds.Count;
        return (viewed, _chapters.Count);
    }

    private bool IsChapterUnlocked(StoryChapter chapter, GameState state)
    {
        // Alle gesetzten Bedingungen müssen erfüllt sein
        if (chapter.RequiredPlayerLevel > 0 && state.PlayerLevel < chapter.RequiredPlayerLevel)
            return false;
        if (chapter.RequiredWorkshopCount > 0 && state.UnlockedWorkshopTypes.Count < chapter.RequiredWorkshopCount)
            return false;
        if (chapter.RequiredTotalOrders > 0 && state.TotalOrdersCompleted < chapter.RequiredTotalOrders)
            return false;
        if (chapter.RequiredPrestige > 0 && state.Prestige.TotalPrestigeCount < chapter.RequiredPrestige)
            return false;
        if (chapter.RequiredPrestigeTier > 0 && (int)state.Prestige.CurrentTier < chapter.RequiredPrestigeTier)
            return false;
        if (chapter.RequiredAscensionLevel > 0 && state.Ascension.AscensionLevel < chapter.RequiredAscensionLevel)
            return false;
        return true;
    }

    private static List<StoryChapter> CreateChapters() =>
    [
        // ═══════════════════════════════════════════════════════════════
        // TUTORIAL (Kapitel 1-5, Level 1-5)
        // ═══════════════════════════════════════════════════════════════

        new()
        {
            Id = "tutorial_welcome",
            ChapterNumber = 1,
            TitleKey = "Story_Ch01_Title",
            TextKey = "Story_Ch01_Text",
            TitleFallback = "Willkommen, Lehrling!",
            TextFallback = "Ich bin Meister Hans! 40 Jahre Erfahrung, und ich hab' schon alles gesehen. Na ja, fast alles – einen Lehrling der so motiviert aussieht wie du noch nicht! Deine Schreinerei ist klein, aber fein. Rüste sie auf, dann reden wir weiter!",
            RequiredPlayerLevel = 1,
            MoneyReward = 100,
            XpReward = 10,
            Mood = "happy",
            IsTutorial = true
        },
        new()
        {
            Id = "tutorial_orders",
            ChapterNumber = 2,
            TitleKey = "Story_Ch02_Title",
            TextKey = "Story_Ch02_Text",
            TitleFallback = "Dein erster Auftrag!",
            TextFallback = "Telefon klingelt? Das ist ein Kunde! Bei Aufträgen musst du kleine Geschicklichkeitsspiele meistern – Sägen, Rohre legen, Kabel verlegen. Je besser du abschneidest, desto mehr Geld und Erfahrung gibt's. Also: Ran an die Arbeit!",
            RequiredPlayerLevel = 2,
            MoneyReward = 250,
            GoldenScrewReward = 0,
            XpReward = 20,
            Mood = "excited",
            IsTutorial = true
        },
        new()
        {
            Id = "tutorial_workers",
            ChapterNumber = 3,
            TitleKey = "Story_Ch03_Title",
            TextKey = "Story_Ch03_Text",
            TitleFallback = "Arbeiter einstellen!",
            TextFallback = "Allein schaffst du das auf Dauer nicht, Lehrling. Auf dem Arbeitsmarkt warten Fachkräfte – jeder hat eigene Stärken und Schwächen. Stell welche ein und sie verdienen Geld, während du Kaffee trinkst! Na ja, fast...",
            RequiredPlayerLevel = 3,
            MoneyReward = 500,
            GoldenScrewReward = 1,
            XpReward = 30,
            Mood = "proud",
            IsTutorial = true
        },
        new()
        {
            Id = "tutorial_golden_screws",
            ChapterNumber = 4,
            TitleKey = "Story_Ch04_Title",
            TextKey = "Story_Ch04_Text",
            TitleFallback = "Goldschrauben!",
            TextFallback = "Siehst du diese goldenen Schrauben? Die sind selten und wertvoll! Du bekommst sie durch Mini-Games, Herausforderungen und Erfolge. Damit kannst du Premium-Arbeiter anheuern und besondere Boosts kaufen. Hüte sie wie deinen Augapfel!",
            RequiredPlayerLevel = 4,
            MoneyReward = 750,
            GoldenScrewReward = 2,
            XpReward = 40,
            Mood = "happy",
            IsTutorial = true
        },
        new()
        {
            Id = "tutorial_buildings",
            ChapterNumber = 5,
            TitleKey = "Story_Ch05_Title",
            TextKey = "Story_Ch05_Text",
            TitleFallback = "Gebäude & Forschung!",
            TextFallback = "Jetzt wird's ernst! Im Forschungsbaum kannst du mächtige Upgrades freischalten – bessere Werkzeuge, effizienteres Management, stärkeres Marketing. Und Gebäude geben permanente Boni. Eine Kantine zum Beispiel hält deine Arbeiter bei Laune!",
            RequiredPlayerLevel = 5,
            MoneyReward = 1_000,
            GoldenScrewReward = 2,
            XpReward = 50,
            Mood = "excited",
            IsTutorial = true
        },

        // ═══════════════════════════════════════════════════════════════
        // EARLY GAME (Kapitel 6-12, Level 8-60)
        // ═══════════════════════════════════════════════════════════════

        new()
        {
            Id = "early_plumber",
            ChapterNumber = 6,
            TitleKey = "Story_Ch06_Title",
            TextKey = "Story_Ch06_Text",
            TitleFallback = "Die Klempnerei öffnet!",
            TextFallback = "Wasser marsch! Die Klempnerei ist ein Goldesel, glaub mir. Jeder braucht mal einen Klempner – besonders nachts um drei, wenn das Rohr platzt! Die Aufträge sind lukrativ und die Mini-Games machen sogar mir Spaß.",
            RequiredPlayerLevel = 8,
            RequiredWorkshopCount = 1,
            MoneyReward = 5_000,
            GoldenScrewReward = 3,
            XpReward = 75,
            Mood = "excited"
        },
        new()
        {
            Id = "early_first_worker",
            ChapterNumber = 7,
            TitleKey = "Story_Ch07_Title",
            TextKey = "Story_Ch07_Text",
            TitleFallback = "Dein erster richtiger Mitarbeiter!",
            TextFallback = "Das Tier-System bei Arbeitern ist wichtig: F-Tier Arbeiter sind günstig, aber... naja, sie verwechseln schon mal Hammer und Schraubenzieher. Spar auf bessere Tiers – ein A-Tier Arbeiter bringt das Zehnfache rein!",
            RequiredPlayerLevel = 10,
            RequiredTotalOrders = 5,
            MoneyReward = 10_000,
            GoldenScrewReward = 3,
            XpReward = 100,
            Mood = "proud"
        },
        new()
        {
            Id = "early_electrician",
            ChapterNumber = 8,
            TitleKey = "Story_Ch08_Title",
            TextKey = "Story_Ch08_Text",
            TitleFallback = "Unter Strom!",
            TextFallback = "Die Elektrikerei – mein Liebling! Kabel verlegen ist Kunst, nicht Handwerk. Ein falscher Draht und... *puff*! Aber keine Sorge, bei uns ist alles gut isoliert. Die Elektrik bringt richtig gutes Geld!",
            RequiredPlayerLevel = 15,
            RequiredWorkshopCount = 2,
            MoneyReward = 25_000,
            GoldenScrewReward = 5,
            XpReward = 150,
            Mood = "happy"
        },
        new()
        {
            Id = "early_daily",
            ChapterNumber = 9,
            TitleKey = "Story_Ch09_Title",
            TextKey = "Story_Ch09_Text",
            TitleFallback = "Tägliche Herausforderungen!",
            TextFallback = "Schau mal, Geselle – jeden Tag gibt's drei neue Herausforderungen! Schließ Aufträge ab, spiel Mini-Games, verdiene Geld. Die Belohnungen lohnen sich, besonders die Goldschrauben. Ein echter Handwerker arbeitet jeden Tag!",
            RequiredPlayerLevel = 18,
            RequiredTotalOrders = 10,
            MoneyReward = 50_000,
            GoldenScrewReward = 5,
            XpReward = 200,
            Mood = "excited"
        },
        new()
        {
            Id = "early_painter",
            ChapterNumber = 10,
            TitleKey = "Story_Ch10_Title",
            TextKey = "Story_Ch10_Text",
            TitleFallback = "Farbe bekennen!",
            TextFallback = "Die Malerei bringt Farbe in dein Imperium! Ein guter Maler hat Farbgefühl – und Geduld. Beim Streich-Minigame musst du die richtige Reihenfolge treffen. Mein Tipp: Achte auf die blinkenden Felder, Geselle!",
            RequiredPlayerLevel = 22,
            RequiredWorkshopCount = 3,
            MoneyReward = 100_000,
            GoldenScrewReward = 8,
            XpReward = 250,
            Mood = "happy"
        },
        new()
        {
            Id = "early_mastertools",
            ChapterNumber = 11,
            TitleKey = "Story_Ch11_Title",
            TextKey = "Story_Ch11_Text",
            TitleFallback = "Legendäre Werkzeuge!",
            TextFallback = "Du hast Meisterwerkzeuge entdeckt! Diese antiken Artefakte tragen die Kraft vergangener Handwerksmeister in sich. Jedes gibt einen permanenten Einkommensbonus. Sammle alle 12 und du wirst die Meisterkrone erhalten – das mächtigste Werkzeug von allen!",
            RequiredPlayerLevel = 35,
            RequiredTotalOrders = 20,
            MoneyReward = 500_000,
            GoldenScrewReward = 10,
            XpReward = 400,
            Mood = "proud"
        },
        new()
        {
            Id = "early_roofer",
            ChapterNumber = 12,
            TitleKey = "Story_Ch12_Title",
            TextKey = "Story_Ch12_Text",
            TitleFallback = "Hoch hinaus!",
            TextFallback = "Die Dachdeckerei – nichts für Leute mit Höhenangst! Aber die Margen sind fantastisch. Und zwischen uns: Bald wirst du ein Level erreichen, wo etwas Besonderes wartet. 'Prestige' nennen wir das. Aber dazu später mehr...",
            RequiredPlayerLevel = 60,
            RequiredWorkshopCount = 4,
            MoneyReward = 2_000_000,
            GoldenScrewReward = 15,
            XpReward = 600,
            Mood = "excited"
        },

        // ═══════════════════════════════════════════════════════════════
        // MID GAME (Kapitel 13-17, Level 75-250)
        // ═══════════════════════════════════════════════════════════════

        new()
        {
            Id = "mid_contractor",
            ChapterNumber = 13,
            TitleKey = "Story_Ch13_Title",
            TextKey = "Story_Ch13_Text",
            TitleFallback = "Der Bauunternehmer!",
            TextFallback = "Fünf Werkstätten und 50 Aufträge – du bist kein Lehrling mehr, Geselle! Die Bauunternehmung ist der nächste Schritt: Mauern hochziehen, Krane bedienen, ganze Häuser bauen. Das ist die Champions League des Handwerks!",
            RequiredPlayerLevel = 100,
            RequiredWorkshopCount = 5,
            RequiredTotalOrders = 50,
            MoneyReward = 25_000_000,
            GoldenScrewReward = 25,
            XpReward = 1_000,
            Mood = "proud"
        },
        new()
        {
            Id = "mid_empire",
            ChapterNumber = 14,
            TitleKey = "Story_Ch14_Title",
            TextKey = "Story_Ch14_Text",
            TitleFallback = "Das Handwerker-Viertel!",
            TextFallback = "Sechs Werkstätten! Die Leute nennen deinen Bezirk schon 'Handwerker-Viertel'. Deine Arbeiter pfeifen morgens fröhlich auf dem Weg zur Arbeit – na gut, die meisten jedenfalls. Halte ihre Stimmung hoch, dann läuft der Laden!",
            RequiredPlayerLevel = 125,
            RequiredWorkshopCount = 6,
            MoneyReward = 50_000_000,
            GoldenScrewReward = 30,
            XpReward = 1_500,
            Mood = "happy"
        },
        new()
        {
            Id = "mid_reputation",
            ChapterNumber = 15,
            TitleKey = "Story_Ch15_Title",
            TextKey = "Story_Ch15_Text",
            TitleFallback = "Der Ruf der Stadt!",
            TextFallback = "75 Aufträge – dein Ruf eilt dir voraus! Die Reputation bestimmt, wie viel Kunden bereit sind zu zahlen. Gute Arbeit = gute Bewertungen = mehr Geld. Schlechte Arbeit... naja, davon reden wir nicht. Mach einfach weiter so!",
            RequiredPlayerLevel = 150,
            RequiredTotalOrders = 75,
            MoneyReward = 100_000_000,
            GoldenScrewReward = 40,
            XpReward = 2_000,
            Mood = "excited"
        },
        new()
        {
            Id = "mid_prestige_hint",
            ChapterNumber = 16,
            TitleKey = "Story_Ch16_Title",
            TextKey = "Story_Ch16_Text",
            TitleFallback = "Ein Neuanfang?",
            TextFallback = "Geselle, ich muss mit dir über Prestige reden. Es klingt verrückt: Alles aufgeben und von vorne anfangen. Aber glaub mir – mit den permanenten Boni wächst du beim zweiten Mal doppelt so schnell. Ich selbst hab's dreimal gemacht. Überlege es dir gut...",
            RequiredPlayerLevel = 200,
            RequiredWorkshopCount = 6,
            MoneyReward = 250_000_000,
            GoldenScrewReward = 50,
            XpReward = 3_000,
            Mood = "concerned"
        },
        new()
        {
            Id = "mid_mastery",
            ChapterNumber = 17,
            TitleKey = "Story_Ch17_Title",
            TextKey = "Story_Ch17_Text",
            TitleFallback = "Bereit für den Neuanfang!",
            TextFallback = "100 Aufträge und Level 250 – du hast alles gemeistert was dieses Leben zu bieten hat. Die Prestige-Tür steht offen. Geh hindurch und komm stärker zurück. Ich warte hier auf dich, Geselle. Nein, ich warte auf dich... Meister!",
            RequiredPlayerLevel = 250,
            RequiredTotalOrders = 100,
            MoneyReward = 500_000_000,
            GoldenScrewReward = 75,
            XpReward = 5_000,
            Mood = "proud"
        },

        // ═══════════════════════════════════════════════════════════════
        // PRESTIGE-REISE (Kapitel 18-21)
        // ═══════════════════════════════════════════════════════════════

        new()
        {
            Id = "prestige_first",
            ChapterNumber = 18,
            TitleKey = "Story_Ch18_Title",
            TextKey = "Story_Ch18_Text",
            TitleFallback = "Wie Phönix aus der Asche!",
            TextFallback = "Du hast es getan, Meister! Ein Neuanfang – aber diesmal mit Erfahrung und permanenten Boni. Alles fühlt sich vertraut an, aber du bist schneller, stärker, klüger. Das ist das Geheimnis der wahren Meister: Fallen und wieder aufstehen!",
            RequiredPrestige = 1,
            MoneyReward = 1_000_000,
            GoldenScrewReward = 100,
            XpReward = 7_500,
            Mood = "concerned"
        },
        new()
        {
            Id = "prestige_architect",
            ChapterNumber = 19,
            TitleKey = "Story_Ch19_Title",
            TextKey = "Story_Ch19_Text",
            TitleFallback = "Das Architekturbüro!",
            TextFallback = "Ein neuer Workshop-Typ, Meister! Das Architekturbüro – hier werden Träume zu Bauplänen. Grundrisse zeichnen, Häuser designen, Visionen verwirklichen. Das ist Handwerk auf höchstem Niveau. Ich bin... *schnief*... so stolz auf dich!",
            RequiredPrestige = 1,
            RequiredPlayerLevel = 50,
            MoneyReward = 5_000_000,
            GoldenScrewReward = 50,
            XpReward = 5_000,
            Mood = "excited"
        },
        new()
        {
            Id = "prestige_silver",
            ChapterNumber = 20,
            TitleKey = "Story_Ch20_Title",
            TextKey = "Story_Ch20_Text",
            TitleFallback = "Silber-Prestige!",
            TextFallback = "Viermal neu angefangen – und jedes Mal besser! Du hast Silber-Prestige erreicht, Meister. Das schaffen die wenigsten. Dein Imperium wächst jetzt mit einer Geschwindigkeit, die selbst mich beeindruckt. Und ich bin nicht leicht zu beeindrucken!",
            RequiredPrestige = 4,
            MoneyReward = 50_000_000,
            GoldenScrewReward = 200,
            XpReward = 15_000,
            Mood = "proud"
        },
        new()
        {
            Id = "prestige_general",
            ChapterNumber = 21,
            TitleKey = "Story_Ch21_Title",
            TextKey = "Story_Ch21_Text",
            TitleFallback = "Der Generalunternehmer!",
            TextFallback = "Der letzte Workshop-Typ: Generalunternehmer! Du koordinierst jetzt ALLES – von der kleinsten Schraube bis zum größten Kran. Kein Projekt ist dir zu groß. Du bist nicht mehr nur ein Handwerker, du bist eine Institution!",
            RequiredPrestige = 4,
            RequiredPlayerLevel = 75,
            MoneyReward = 100_000_000,
            GoldenScrewReward = 100,
            XpReward = 10_000,
            Mood = "excited"
        },

        // ═══════════════════════════════════════════════════════════════
        // ENDGAME (Kapitel 22-25, Level 500+, Gold Prestige)
        // ═══════════════════════════════════════════════════════════════

        new()
        {
            Id = "endgame_gold",
            ChapterNumber = 22,
            TitleKey = "Story_Ch22_Title",
            TextKey = "Story_Ch22_Text",
            TitleFallback = "Gold-Prestige!",
            TextFallback = "Ich... ich bin sprachlos, Großmeister. 13 Prestige-Durchläufe. GOLD. In 40 Jahren Handwerk habe ich so etwas noch nie gesehen. Dein Name wird in die Gilden-Chronik eingehen. Neben meinem natürlich – ich hab dich schließlich ausgebildet!",
            RequiredPrestige = 13,
            MoneyReward = 500_000_000,
            GoldenScrewReward = 500,
            XpReward = 25_000,
            Mood = "excited"
        },
        new()
        {
            Id = "endgame_legend",
            ChapterNumber = 23,
            TitleKey = "Story_Ch23_Title",
            TextKey = "Story_Ch23_Text",
            TitleFallback = "Die lebende Legende!",
            TextFallback = "Level 500 und sieben Werkstätten auf dem Höhepunkt – du bist eine lebende Legende! Die Handwerkskammer hat angerufen, sie wollen ein Portrait von dir aufhängen. Gleich neben meinem. Na gut, vielleicht sogar ÜBER meinem. Du hast es verdient.",
            RequiredPlayerLevel = 500,
            RequiredWorkshopCount = 7,
            MoneyReward = 1_000_000_000,
            GoldenScrewReward = 300,
            XpReward = 50_000,
            Mood = "proud"
        },
        new()
        {
            Id = "endgame_allmax",
            ChapterNumber = 24,
            TitleKey = "Story_Ch24_Title",
            TextKey = "Story_Ch24_Text",
            TitleFallback = "Alle Werkstätten, alle Meister!",
            TextFallback = "Acht Werkstätten, 500 Aufträge, alles auf Maximum. Dein Imperium erstreckt sich über die ganze Stadt! Die Handwerker der nächsten Generation schauen zu dir auf. Weißt du was? Ich glaube, der Schüler hat den Meister übertroffen. Und das sage ich nicht leichtfertig!",
            RequiredPlayerLevel = 750,
            RequiredWorkshopCount = 8,
            RequiredTotalOrders = 500,
            MoneyReward = 5_000_000_000,
            GoldenScrewReward = 500,
            XpReward = 75_000,
            Mood = "happy"
        },
        new()
        {
            Id = "endgame_grandmaster",
            ChapterNumber = 25,
            TitleKey = "Story_Ch25_Title",
            TextKey = "Story_Ch25_Text",
            TitleFallback = "Großmeister der Handwerker-Gilde!",
            TextFallback = "Level 1000. Großmeister der Handwerker-Gilde. Es gibt keinen höheren Titel. Von einem nervösen Lehrling mit einer kleinen Schreinerei zum mächtigsten Handwerker der Geschichte – DEINE Geschichte! Ich bin alt geworden mit dir, Großmeister. Aber es war jede Minute wert. Danke.",
            RequiredPlayerLevel = 1000,
            MoneyReward = 10_000_000_000,
            GoldenScrewReward = 1_000,
            XpReward = 100_000,
            Mood = "excited"
        },

        // ═══════════════════════════════════════════════════════════════
        // ENDGAME-EXPANSION (Kapitel 26-35, Platin+ Prestige)
        // ═══════════════════════════════════════════════════════════════

        new()
        {
            Id = "endgame_platin",
            ChapterNumber = 26,
            TitleKey = "Story_Ch26_Title",
            TextKey = "Story_Ch26_Text",
            TitleFallback = "Der Platin-Handwerker",
            TextFallback = "Platin! Da kenne ich nur eine Handvoll Leute in meinem ganzen Leben, die das geschafft haben. Deine Werkzeuge glänzen wie nie zuvor, und die bleiben dir jetzt – für immer. Das Handwerk hat dich verändert, Meister. Du bist nicht mehr der gleiche Mensch wie am Anfang.",
            RequiredPrestigeTier = 4, // Platin
            MoneyReward = 50_000_000,
            GoldenScrewReward = 200,
            XpReward = 50_000,
            Mood = "proud"
        },
        new()
        {
            Id = "endgame_diamant_path",
            ChapterNumber = 27,
            TitleKey = "Story_Ch27_Title",
            TextKey = "Story_Ch27_Text",
            TitleFallback = "Auf dem Weg zum Diamant",
            TextFallback = "Zwischen Platin und Diamant liegt ein Abgrund, Meister. Viele haben es versucht, wenige haben es geschafft. Du brauchst drei perfekte Platin-Durchläufe. Drei Mal alles aufgeben, drei Mal stärker zurückkommen. Bist du bereit für den härtesten Teil der Reise?",
            RequiredPrestigeTier = 4,
            RequiredPrestige = 7, // Mindestens 7 Gesamtprestiges
            MoneyReward = 100_000_000,
            GoldenScrewReward = 300,
            XpReward = 75_000,
            Mood = "concerned"
        },
        new()
        {
            Id = "endgame_diamant",
            ChapterNumber = 28,
            TitleKey = "Story_Ch28_Title",
            TextKey = "Story_Ch28_Text",
            TitleFallback = "Diamant-Dynastie",
            TextFallback = "Diamant! Unzerstörbar, unvergänglich – wie dein Imperium. Deine Gebäude und Ausrüstung überstehen jetzt jeden Neuanfang. Das ist nicht mehr Handwerk, das ist Kunst. Die Leute sprechen in Ehrfurcht von dir, Meister.",
            RequiredPrestigeTier = 5, // Diamant
            MoneyReward = 500_000_000,
            GoldenScrewReward = 500,
            XpReward = 100_000,
            Mood = "excited"
        },
        new()
        {
            Id = "endgame_meister_weg",
            ChapterNumber = 29,
            TitleKey = "Story_Ch29_Title",
            TextKey = "Story_Ch29_Text",
            TitleFallback = "Der Pfad des Meisters",
            TextFallback = "Nur die Besten der Besten erreichen den Meister-Titel. Deine Manager bleiben dir treu, dein Wissen überdauert jeden Reset. Ich habe in 40 Jahren noch nie jemanden so weit kommen sehen. Du schreibst Geschichte!",
            RequiredPrestigeTier = 5,
            RequiredPrestige = 12,
            MoneyReward = 1_000_000_000,
            GoldenScrewReward = 750,
            XpReward = 150_000,
            Mood = "proud"
        },
        new()
        {
            Id = "endgame_meister",
            ChapterNumber = 30,
            TitleKey = "Story_Ch30_Title",
            TextKey = "Story_Ch30_Text",
            TitleFallback = "Meister der Meister",
            TextFallback = "Du hast es geschafft. Meister. Das Feuer in deinen Augen brennt heller als jede Schmiede. Deine Vorarbeiter folgen dir durch dick und dünn. Jeder Neuanfang macht dich nur stärker. Es gibt nur noch einen Titel über dir...",
            RequiredPrestigeTier = 6, // Meister
            MoneyReward = 5_000_000_000,
            GoldenScrewReward = 1_000,
            XpReward = 200_000,
            Mood = "excited"
        },
        new()
        {
            Id = "endgame_legende_weg",
            ChapterNumber = 31,
            TitleKey = "Story_Ch31_Title",
            TextKey = "Story_Ch31_Text",
            TitleFallback = "Auf dem Weg zur Legende",
            TextFallback = "Die Legende. Der höchste Titel, den ein Handwerker je erreichen kann. Drei perfekte Meister-Durchläufe stehen zwischen dir und der Unsterblichkeit. Deine besten Arbeiter werden bei dir bleiben – ein letztes Zeichen deiner Größe.",
            RequiredPrestigeTier = 6,
            RequiredPrestige = 18,
            MoneyReward = 10_000_000_000,
            GoldenScrewReward = 1_500,
            XpReward = 300_000,
            Mood = "concerned"
        },
        new()
        {
            Id = "endgame_legende",
            ChapterNumber = 32,
            TitleKey = "Story_Ch32_Title",
            TextKey = "Story_Ch32_Text",
            TitleFallback = "Lebende Legende",
            TextFallback = "LEGENDE! *wischt sich eine Träne ab* In all meinen Jahren... Du bist der erste. Der einzige. Dein Name wird in die Geschichtsbücher eingehen. Generationen von Handwerkern werden von dir sprechen. Ich bin... sprachlos. Und das passiert nicht oft!",
            RequiredPrestigeTier = 7, // Legende
            MoneyReward = 50_000_000_000,
            GoldenScrewReward = 2_000,
            XpReward = 500_000,
            Mood = "excited"
        },
        new()
        {
            Id = "endgame_ascension_ready",
            ChapterNumber = 33,
            TitleKey = "Story_Ch33_Title",
            TextKey = "Story_Ch33_Text",
            TitleFallback = "Jenseits der Legende",
            TextFallback = "Drei Legenden-Durchläufe. Du hast bewiesen, dass du über allem stehst. Aber was kommt danach? Ich habe von einem uralten Ritual gehört... die Ascension. Ein kompletter Neustart auf einer höheren Ebene. Nur für wahre Legenden. Bist du bereit, alles loszulassen?",
            RequiredPrestigeTier = 7,
            RequiredPrestige = 21,
            MoneyReward = 100_000_000_000,
            GoldenScrewReward = 3_000,
            XpReward = 750_000,
            Mood = "concerned"
        },
        new()
        {
            Id = "endgame_first_ascension",
            ChapterNumber = 34,
            TitleKey = "Story_Ch34_Title",
            TextKey = "Story_Ch34_Text",
            TitleFallback = "Der Aufstieg",
            TextFallback = "Du hast die Ascension vollzogen! Alles zurückgelassen, um auf einer höheren Ebene neu zu beginnen. Deine Ascension-Perks sind mächtig – Startkapital, ewige Werkzeuge, schnellere Forschung. Jeder Durchlauf wird jetzt einfacher und befriedigender.",
            RequiredAscensionLevel = 1,
            MoneyReward = 500_000_000_000,
            GoldenScrewReward = 5_000,
            XpReward = 1_000_000,
            Mood = "proud"
        },
        new()
        {
            Id = "endgame_transcendence",
            ChapterNumber = 35,
            TitleKey = "Story_Ch35_Title",
            TextKey = "Story_Ch35_Text",
            TitleFallback = "Transzendenz",
            TextFallback = "Drei Ascensions. Du hast die Grenzen des Möglichen gesprengt. Du bist nicht mehr nur ein Handwerker – du bist eine Kraft der Natur. Dein Imperium ist unsterblich. Meister Hans verneigt sich zum letzten Mal. Diese Geschichte kennt kein Ende, nur neue Anfänge. Lebe wohl... und willkommen zurück, wann immer du willst.",
            RequiredAscensionLevel = 3,
            MoneyReward = 1_000_000_000_000,
            GoldenScrewReward = 10_000,
            XpReward = 2_000_000,
            Mood = "excited"
        }
    ];
}
