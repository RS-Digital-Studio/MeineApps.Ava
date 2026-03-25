namespace RebornSaga.Services;

using RebornSaga.Models;
using RebornSaga.Models.Enums;
using System;
using System.Collections.Generic;

/// <summary>
/// Kampf-Logik: Schadensberechnung, Element-Kreislauf, Kampf-Ablauf.
/// Zustandslos bis auf den RNG - kann als Singleton im DI registriert werden.
/// </summary>
public class BattleEngine
{
    private readonly Random _rng = new();

    /// <summary>
    /// Berechnet den Schaden eines Angriffs.
    /// </summary>
    /// <param name="atk">Angriffswert des Angreifers.</param>
    /// <param name="multi">Skill-Multiplikator (1.0 = Standardangriff).</param>
    /// <param name="def">Verteidigungswert des Ziels.</param>
    /// <param name="atkElem">Element des Angriffs (null = elementlos).</param>
    /// <param name="defElement">Element des Ziels (null = elementlos).</param>
    /// <param name="luk">Glück des Angreifers (beeinflusst Crit-Chance).</param>
    /// <returns>Berechneter Schaden (mindestens 5) und ob kritisch.</returns>
    public (int damage, bool isCrit) CalculateDamage(int atk, float multi, int def,
        Element? atkElem, Element? defElement, int luk)
    {
        var baseDmg = Math.Max(5f, (atk * multi) - (def * 0.5f));
        var elemMod = GetElementModifier(atkElem, defElement);
        var randMod = 1f + (_rng.NextSingle() * 0.2f - 0.1f); // ±10%
        var isCrit = _rng.NextSingle() < (luk * 0.005f); // 0.5% pro LUK
        var damage = Math.Max(1, (int)(baseDmg * elemMod * randMod * (isCrit ? 2f : 1f)));
        return (damage, isCrit);
    }

    /// <summary>
    /// Prüft ob ein Ausweich-Versuch erfolgreich ist.
    /// </summary>
    public bool TryDodge(int defenderSpd, int attackerSpd)
    {
        // Basis 25% Dodge-Chance (vorher 10%), max 60% (vorher 50%)
        var dodgeChance = Math.Clamp((defenderSpd - attackerSpd) * 0.02f + 0.25f, 0.1f, 0.6f);
        return _rng.NextSingle() < dodgeChance;
    }

    /// <summary>
    /// Berechnet die Drops eines besiegten Gegners.
    /// </summary>
    public List<string> CalculateDrops(Enemy enemy)
    {
        var drops = new List<string>();
        if (enemy.Drops == null) return drops;

        foreach (var drop in enemy.Drops)
        {
            if (_rng.NextSingle() < drop.Chance)
                drops.Add(drop.ItemId);
        }

        return drops;
    }

    /// <summary>
    /// Element-Modifikator: 1.5x bei Schwäche, 0.5x bei Resistenz, 1.0x sonst.
    /// Kreislauf: Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer
    /// </summary>
    private float GetElementModifier(Element? atkElem, Element? defElement)
    {
        if (atkElem == null || defElement == null) return 1f;

        // Verteidiger ist schwach gegen Angreifer-Element → 1.5x
        if (IsWeakTo(defElement.Value, atkElem.Value))
            return 1.5f;

        // Angreifer ist schwach gegen Verteidiger-Element → 0.5x
        if (IsWeakTo(atkElem.Value, defElement.Value))
            return 0.5f;

        return 1f;
    }

    /// <summary>
    /// Prüft ob ein Element schwach gegen ein anderes ist (im Kreislauf).
    /// </summary>
    private static bool IsWeakTo(Element defender, Element attacker) => attacker switch
    {
        Element.Fire => defender == Element.Ice,
        Element.Ice => defender == Element.Lightning,
        Element.Lightning => defender == Element.Wind,
        Element.Wind => defender == Element.Light,
        Element.Light => defender == Element.Dark,
        Element.Dark => defender == Element.Fire,
        _ => false
    };
}
