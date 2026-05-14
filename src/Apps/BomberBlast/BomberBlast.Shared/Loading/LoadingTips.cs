using MeineApps.Core.Ava.Localization;

namespace BomberBlast.Loading;

/// <summary>
/// Loading-Tipps-System (Phase 28 — PR4).
///
/// <para>Vorbild: Genshin Impact / Royal Match — während des Loading-Screens werden zufällige
/// Spiel-Tipps angezeigt damit der Spieler nicht "Loading..." stiert.</para>
///
/// <para>Pool: 30+ Tipps (RESX-Keys <c>LoadingTip01</c> bis <c>LoadingTip30</c>) in 6 Sprachen.
/// Random-Picker verhindert direkte Wiederholung. Welt-spezifische Tipps werden
/// kontextbezogen priorisiert wenn der Spieler in einer bestimmten Welt ist.</para>
/// </summary>
public static class LoadingTips
{
    // Pool aller verfügbaren Tipp-Keys (kontextfrei).
    // RESX-Keys werden in den nächsten Phase 28b-Sprint nachgezogen — bis dahin sind sie als
    // Defaults vorhanden und werden via TryLoad gefallt zurück (Designer-Property?? "Default-Tipp").
    private static readonly string[] AllTipKeys =
    [
        "LoadingTip01_BombChain",       // "Tipp: Bombe → Bombe ergibt Kettenreaktion"
        "LoadingTip02_FireRange",       // "Power-Up Fire erweitert die Reichweite"
        "LoadingTip03_KickBomb",        // "Mit Kick-PowerUp kannst du Bomben in eine Richtung schieben"
        "LoadingTip04_Detonator",       // "Detonator-PowerUp erlaubt manuelles Zünden"
        "LoadingTip05_PowerBomb",       // "Power-Bomb verbraucht alle Bomben für maximalen Schaden"
        "LoadingTip06_Pontan",          // "Pontan-Spawn warnt 3 Sekunden vorher — nutze die Zeit!"
        "LoadingTip07_HiddenExit",      // "Der Ausgang ist unter einem Block versteckt"
        "LoadingTip08_DailyReward",     // "Logge dich täglich ein für einen Bonus — Tag 7 ist der Jackpot"
        "LoadingTip09_LuckySpin",       // "Das Glücksrad gibt 1× pro Tag gratis"
        "LoadingTip10_Combo",           // "Kills innerhalb 2s erzeugen Combos — x10+ ist ULTRA"
        "LoadingTip11_ShopUpgrades",    // "Im Shop gibt es 9 permanente Upgrades"
        "LoadingTip12_Achievements",    // "66 Achievements warten — jedes gibt Coins"
        "LoadingTip13_DungeonRoguelike",// "Dungeon-Run ist ein Roguelike — wähle deine Buffs weise"
        "LoadingTip14_Liga",            // "Die Liga resetet alle 14 Tage — sammle Punkte schnell"
        "LoadingTip15_BattlePass",      // "Der Battle Pass läuft 30 Tage — verpass keinen Tier"
        "LoadingTip16_Cards",           // "Karten upgrade ist Bronze → Silber → Gold"
        "LoadingTip17_DeckSlots",       // "Das 5. Deck-Slot wird mit 20 Gems freigeschaltet"
        "LoadingTip18_GemSources",      // "Gems gibt es bei 3-Sterne-Clearings, Boss-Kills und Survival-Meilensteinen"
        "LoadingTip19_Survival",        // "Survival-Meilensteine bei 60s/120s/180s/300s geben Coins+Gems"
        "LoadingTip20_QuickPlay",       // "Quick-Play-Seeds kannst du mit Freunden teilen"
        "LoadingTip21_Master",          // "Nach Level 100 schaltest du Master Mode frei"
        "LoadingTip22_Skull",           // "Skull-Power-Ups sind Flüche — meide sie oder finde Cure"
        "LoadingTip23_Frostbomb",       // "Frost-Bombe friert Gegner für 3 Sekunden"
        "LoadingTip24_Lightning",       // "Lightning-Bombe trifft 3 Gegner gleichzeitig"
        "LoadingTip25_BlackHole",       // "Black-Hole-Bombe zieht Gegner für 3s an"
        "LoadingTip26_Mirror",          // "Mirror-Bombe verdoppelt deine Reichweite"
        "LoadingTip27_DungeonSynergy",  // "Bestimmte Dungeon-Buffs ergeben Synergien — Trockenstein!"
        "LoadingTip28_DailyChallenge",  // "Daily Challenge ist für ALLE Spieler dasselbe Level"
        "LoadingTip29_BossWeakness",    // "Bosse haben Schwächen bei 50% HP (Enrage-Phase)"
        "LoadingTip30_Slowmo",          // "ULTRA-Combo (x10+) löst Slow-Motion aus"
        "LoadingTip31_DungeonLite",     // "Dungeon-Lite ist eine 3-Floor-Variante für Einsteiger"
        "LoadingTip32_RewardedAds",     // "Rewarded Ads geben Continues, Skip-Level, Power-Ups"
        "LoadingTip33_BossRush",        // "Boss-Rush kombiniert alle 5 Bosse — wöchentlicher Reset"
    ];

    // Welt-spezifische Tipp-Pools — werden bevorzugt wenn der Spieler in dieser Welt spielt.
    private static readonly string[][] WorldSpecificTips =
    [
        // Welt 1 — Forest
        ["LoadingTip_W1_Easy", "LoadingTip_W1_BlockOrder"],
        // Welt 2 — Industrial
        ["LoadingTip_W2_Conveyor", "LoadingTip_W2_StealthEnemies"],
        // Welt 3 — Cavern
        ["LoadingTip_W3_Lava", "LoadingTip_W3_Teleporter"],
        // Welt 4 — Sky
        ["LoadingTip_W4_PlatformGap", "LoadingTip_W4_FloatingTiles"],
        // Welt 5 — Inferno
        ["LoadingTip_W5_FireImmune", "LoadingTip_W5_AsBoss"],
    ];

    private static readonly Random _random = new();
    private static int _lastIndex = -1;

    /// <summary>
    /// Liefert einen zufälligen Loading-Tipp (lokalisierter Text).
    /// Verhindert direkte Wiederholung des letzten Tipps.
    /// </summary>
    public static string GetRandomTip(int? worldIndex = null)
    {
        // Welt-spezifischer Tipp mit 30% Chance (wenn world angegeben + Pool vorhanden)
        if (worldIndex.HasValue && worldIndex.Value >= 0 && worldIndex.Value < WorldSpecificTips.Length
            && _random.Next(100) < 30)
        {
            var pool = WorldSpecificTips[worldIndex.Value];
            if (pool.Length > 0)
            {
                var key = pool[_random.Next(pool.Length)];
                return Resolve(key);
            }
        }

        // Standard-Tipp aus globalem Pool — Anti-Repeat
        int idx;
        do
        {
            idx = _random.Next(AllTipKeys.Length);
        } while (idx == _lastIndex && AllTipKeys.Length > 1);
        _lastIndex = idx;

        return Resolve(AllTipKeys[idx]);
    }

    /// <summary>
    /// Anzahl Tipps im Pool (für UI: Pagination, Tipp-Browser).
    /// </summary>
    public static int TotalTipCount => AllTipKeys.Length;

    /// <summary>
    /// Resolve via LocalizationManager mit Fallback auf Default-Hint wenn RESX-Key fehlt.
    /// Phase 28b zieht alle 30+ Keys in alle 6 RESX-Files nach.
    /// </summary>
    private static string Resolve(string key)
    {
        try
        {
            // LocalizationManager ist die zentrale RESX-Brücke der Codebase.
            var resolved = LocalizationManager.GetString(key);
            if (!string.IsNullOrEmpty(resolved) && resolved != key)
                return resolved;
        }
        catch
        {
            // LocalizationManager nicht initialisiert oder Key fehlt — Fallback weiter
        }

        // Fallback: Default-Hints (Deutsch — wird in Phase 28b durch RESX ersetzt).
        // Jeder Key hat einen EINDEUTIGEN Hint, sonst wirkt der Anti-Repeat-Picker auf
        // Pool-Index-Ebene aber der Spieler sieht visuell denselben Text → Bug.
        return key switch
        {
            "LoadingTip01_BombChain" => "Tipp: Bomben können Kettenreaktionen auslösen",
            "LoadingTip02_FireRange" => "Tipp: Fire-PowerUps erweitern die Explosions-Reichweite",
            "LoadingTip03_KickBomb" => "Tipp: Mit Kick kannst du Bomben in eine Richtung schieben",
            "LoadingTip04_Detonator" => "Tipp: Detonator-PowerUp erlaubt manuelles Zünden",
            "LoadingTip05_PowerBomb" => "Tipp: Power-Bomb verbraucht alle Slots für maximalen Schaden",
            "LoadingTip06_Pontan" => "Tipp: Pontan warnt dich 3 Sekunden vor dem Spawn",
            "LoadingTip07_HiddenExit" => "Tipp: Der Ausgang ist unter einem Block versteckt",
            "LoadingTip08_DailyReward" => "Tipp: Logge dich täglich ein — Tag 7 ist der Jackpot",
            "LoadingTip09_LuckySpin" => "Tipp: Das Glücksrad gibt 1× pro Tag gratis",
            "LoadingTip10_Combo" => "Tipp: Kills innerhalb 2s ergeben Combos — x10+ aktiviert ULTRA",
            "LoadingTip11_ShopUpgrades" => "Tipp: Im Shop gibt es 9 permanente Upgrades",
            "LoadingTip12_Achievements" => "Tipp: 66 Achievements warten — jedes gibt Coins",
            "LoadingTip13_DungeonRoguelike" => "Tipp: Dungeon-Run ist ein Roguelike — wähle Buffs weise",
            "LoadingTip14_Liga" => "Tipp: Die Liga resetet alle 14 Tage — sammle Punkte schnell",
            "LoadingTip15_BattlePass" => "Tipp: Der Battle Pass läuft 30 Tage — verpass keinen Tier",
            "LoadingTip16_Cards" => "Tipp: Karten upgrade ist Bronze → Silber → Gold",
            "LoadingTip17_DeckSlots" => "Tipp: Das 5. Deck-Slot wird mit 20 Gems freigeschaltet",
            "LoadingTip18_GemSources" => "Tipp: Gems gibt es bei 3-Sterne-Clearings und Boss-Kills",
            "LoadingTip19_Survival" => "Tipp: Survival-Meilensteine bei 60s/120s/180s/300s geben Bonus",
            "LoadingTip20_QuickPlay" => "Tipp: Quick-Play-Seeds kannst du mit Freunden teilen",
            "LoadingTip21_Master" => "Tipp: Nach Level 100 schaltest du den Master-Mode frei",
            "LoadingTip22_Skull" => "Tipp: Skull-PowerUps sind Flüche — meide sie oder finde Cure",
            "LoadingTip23_Frostbomb" => "Tipp: Frost-Bombe friert Gegner für 3 Sekunden",
            "LoadingTip24_Lightning" => "Tipp: Lightning-Bombe trifft 3 Gegner gleichzeitig",
            "LoadingTip25_BlackHole" => "Tipp: Black-Hole-Bombe zieht Gegner für 3s an",
            "LoadingTip26_Mirror" => "Tipp: Mirror-Bombe verdoppelt deine Reichweite",
            "LoadingTip27_DungeonSynergy" => "Tipp: Bestimmte Dungeon-Buffs ergeben Synergien",
            "LoadingTip28_DailyChallenge" => "Tipp: Daily Challenge ist für ALLE Spieler dasselbe Level",
            "LoadingTip29_BossWeakness" => "Tipp: Bosse haben Schwächen ab 50% HP (Enrage-Phase)",
            "LoadingTip30_Slowmo" => "Tipp: ULTRA-Combo löst Slow-Motion aus",
            "LoadingTip31_DungeonLite" => "Tipp: Dungeon-Lite ist eine 3-Floor-Variante für Einsteiger",
            "LoadingTip32_RewardedAds" => "Tipp: Rewarded Ads geben Continues, Skip-Level, Power-Ups",
            "LoadingTip33_BossRush" => "Tipp: Boss-Rush kombiniert alle 5 Bosse — wöchentlicher Reset",

            // Welt-spezifische Defaults
            "LoadingTip_W1_Easy" => "Welt 1: Forest — gemächlicher Einstieg",
            "LoadingTip_W1_BlockOrder" => "Tipp: Räume Blöcke systematisch — von außen nach innen",
            "LoadingTip_W2_Conveyor" => "Welt 2: Förderbänder können dich gegen deinen Willen schieben",
            "LoadingTip_W2_StealthEnemies" => "Welt 2: Industrial-Gegner können sich tarnen",
            "LoadingTip_W3_Lava" => "Welt 3: Lava-Risse spawnen heiße Zellen — Vorsicht",
            "LoadingTip_W3_Teleporter" => "Welt 3: Teleporter sind in Höhlen üblich",
            "LoadingTip_W4_PlatformGap" => "Welt 4: Sky — Plattform-Lücken sind tödlich",
            "LoadingTip_W4_FloatingTiles" => "Welt 4: Schwimmende Kacheln verschieben sich",
            "LoadingTip_W5_FireImmune" => "Welt 5: Inferno — Feuer-Immunität ist gold wert",
            "LoadingTip_W5_AsBoss" => "Welt 5: Der finale Boss erscheint im Inferno",

            _ => "Tipp: Probier verschiedene Spezial-Bomben aus!",
        };
    }
}
