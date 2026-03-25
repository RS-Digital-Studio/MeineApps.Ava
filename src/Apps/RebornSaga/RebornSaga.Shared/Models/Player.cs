namespace RebornSaga.Models;

using RebornSaga.Models.Enums;
using System;
using System.Collections.Generic;

/// <summary>
/// Spieler-Daten: Stats, Level, Inventar, Karma. Wird in SQLite gespeichert.
/// </summary>
public class Player
{
    public string Name { get; set; } = "Held";
    public ClassName Class { get; set; } = ClassName.Swordmaster;
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public int Gold { get; set; }
    public int Karma { get; set; }

    // Aktuelle Stats (Basis + Level-Bonus + manuelle Punkte + Equipment)
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Atk { get; set; }
    public int Def { get; set; }
    public int Int { get; set; }
    public int Spd { get; set; }
    public int Luk { get; set; }

    // Freie Stat-Punkte (3 pro Level-Up zum Verteilen)
    public int FreeStatPoints { get; set; }

    // Inventar (Item-IDs, HashSet verhindert Duplikate)
    public HashSet<string> Inventory { get; set; } = new();

    // Ausgerüstete Items (Slot → Item-ID)
    public Dictionary<string, string> Equipment { get; set; } = new();

    // Story-Fortschritt
    public string CurrentChapterId { get; set; } = "p1";
    public string CurrentNodeId { get; set; } = "";
    public Dictionary<string, int> Affinities { get; set; } = new();
    public HashSet<string> CompletedChapters { get; set; } = new();
    public HashSet<string> Flags { get; set; } = new(); // Story-Flags für Bedingungen

    /// <summary>
    /// Berechnet die EXP-Schwelle für das nächste Level.
    /// Formel: 40 * Level^1.35 (abgeflacht ab Level 15 für weniger Grind)
    /// </summary>
    public int ExpToNextLevel => (int)(40 * MathF.Pow(Level, 1.35f));

    /// <summary>
    /// Initialisiert einen neuen Spieler mit den Basis-Stats der gewählten Klasse.
    /// </summary>
    public static Player Create(ClassName className, string name = "Held")
    {
        var cls = PlayerClass.Get(className);
        var player = new Player
        {
            Name = name,
            Class = className,
            Level = 1,
            Exp = 0,
            Gold = 0,
            Karma = 0,
            Atk = cls.BaseAtk,
            Def = cls.BaseDef,
            Int = cls.BaseInt,
            Spd = cls.BaseSpd,
            Luk = cls.BaseLuk,
            FreeStatPoints = 0
        };
        player.MaxHp = cls.BaseHp;
        player.Hp = player.MaxHp;
        player.MaxMp = cls.BaseMp;
        player.Mp = player.MaxMp;
        return player;
    }

    /// <summary>
    /// Erstellt einen Level-50-Spieler für den Prolog.
    /// </summary>
    public static Player CreatePrologHero()
    {
        var cls = PlayerClass.Swordmaster;
        var player = new Player
        {
            Name = "Held von Aethermoor",
            Class = ClassName.Swordmaster,
            Level = 50,
            Exp = 0,
            Gold = 5000,
            Karma = 75
        };

        // Level 50 Stats berechnen
        player.MaxHp = cls.BaseHp + cls.HpPerLevel * 49;
        player.Hp = player.MaxHp;
        player.MaxMp = cls.BaseMp + cls.MpPerLevel * 49;
        player.Mp = player.MaxMp;
        player.Atk = cls.BaseAtk + cls.AtkPerLevel * 49;
        player.Def = cls.BaseDef + cls.DefPerLevel * 49;
        player.Int = cls.BaseInt + cls.IntPerLevel * 49;
        player.Spd = cls.BaseSpd + cls.SpdPerLevel * 49;
        player.Luk = cls.BaseLuk + cls.LukPerLevel * 49;

        return player;
    }

    /// <summary>
    /// Fügt EXP hinzu und gibt die Anzahl Level-Ups zurück.
    /// </summary>
    public int AddExp(int amount)
    {
        Exp += amount;
        int levelUps = 0;

        while (Exp >= ExpToNextLevel)
        {
            Exp -= ExpToNextLevel;
            Level++;
            levelUps++;
            FreeStatPoints += 3;

            // Auto-Bonus der Klasse
            var cls = PlayerClass.Get(Class);
            MaxHp += cls.HpPerLevel;
            Hp = MaxHp; // Voll heilen bei Level-Up
            MaxMp += cls.MpPerLevel;
            Mp = MaxMp;
            Atk += cls.AtkPerLevel;
            Def += cls.DefPerLevel;
            Int += cls.IntPerLevel;
            Spd += cls.SpdPerLevel;
            Luk += cls.LukPerLevel;
        }

        return levelUps;
    }

    /// <summary>
    /// Heilt HP und MP vollständig.
    /// </summary>
    public void FullHeal()
    {
        Hp = MaxHp;
        Mp = MaxMp;
    }
}
