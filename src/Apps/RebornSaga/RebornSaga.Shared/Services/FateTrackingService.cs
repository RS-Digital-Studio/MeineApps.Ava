namespace RebornSaga.Services;

using RebornSaga.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Trackt Karma, Entscheidungen und bestimmt Story-Verzweigungen.
/// Karma ist unsichtbar für den Spieler, beeinflusst aber NPC-Reaktionen und das Ending.
/// </summary>
public class FateTrackingService
{
    /// <summary>
    /// Unsichtbarer Karma-Wert (-100 bis +100, Start: 0).
    /// Positiv = heldenhaft, Negativ = pragmatisch/dunkel.
    /// </summary>
    public int Karma { get; private set; }

    /// <summary>
    /// Log aller Story-Entscheidungen (für Kodex und Kapitelzusammenfassung).
    /// </summary>
    public List<FateDecision> Decisions { get; } = new();

    /// <summary>
    /// Schicksals-Flags (z.B. "betrayed_aldric", "saved_luna").
    /// Permanente Story-Marker die Verzweigungen und NPC-Reaktionen beeinflussen.
    /// </summary>
    public HashSet<string> FateFlags { get; } = new();

    /// <summary>
    /// Event wenn sich das Karma ändert (alter Wert, neuer Wert).
    /// ARIA kommentiert subtil bei Karma-Änderungen.
    /// </summary>
    public event Action<int, int>? KarmaChanged;

    /// <summary>
    /// Event wenn ein Fate-Flag gesetzt oder entfernt wird (Flag-Name, gesetzt/entfernt).
    /// </summary>
    public event Action<string, bool>? FateFlagChanged;

    /// <summary>
    /// Event wenn ein Schicksals-Wendepunkt eintritt (FateChanged-Key).
    /// </summary>
    public event Action<string>? FateChanged;

    /// <summary>
    /// Ändert das Karma und loggt die Entscheidung.
    /// </summary>
    public void RecordDecision(string chapterId, string nodeId, int choiceIndex,
        int karmaChange, string descriptionKey)
    {
        var oldKarma = Karma;
        Karma = Math.Clamp(Karma + karmaChange, -100, 100);

        Decisions.Add(new FateDecision
        {
            ChapterId = chapterId,
            NodeId = nodeId,
            ChoiceIndex = choiceIndex,
            KarmaChange = karmaChange,
            DescriptionKey = descriptionKey
        });

        if (karmaChange != 0)
            KarmaChanged?.Invoke(oldKarma, Karma);
    }

    /// <summary>
    /// Ändert das Karma ohne Entscheidungs-Log (für Story-Effekte).
    /// </summary>
    public void ModifyKarma(int amount)
    {
        if (amount == 0) return;
        var oldKarma = Karma;
        Karma = Math.Clamp(Karma + amount, -100, 100);
        KarmaChanged?.Invoke(oldKarma, Karma);
    }

    /// <summary>
    /// Registriert einen Schicksals-Wendepunkt (löst FateChangedOverlay aus).
    /// </summary>
    public void RecordFateChange(string fateKey)
    {
        if (string.IsNullOrEmpty(fateKey)) return;

        // Fate-Key als Flag speichern für spätere Bedingungsprüfungen
        FateFlags.Add($"fate_{fateKey}");
        FateChanged?.Invoke(fateKey);
    }

    /// <summary>
    /// Setzt ein Schicksals-Flag (z.B. "betrayed_aldric", "saved_luna").
    /// </summary>
    public void AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        if (FateFlags.Add(flag))
            FateFlagChanged?.Invoke(flag, true);
    }

    /// <summary>
    /// Entfernt ein Schicksals-Flag.
    /// </summary>
    public void RemoveFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        if (FateFlags.Remove(flag))
            FateFlagChanged?.Invoke(flag, false);
    }

    /// <summary>
    /// Prüft ob ein Schicksals-Flag gesetzt ist.
    /// </summary>
    public bool HasFlag(string flag)
    {
        return FateFlags.Contains(flag);
    }

    /// <summary>
    /// Gibt die Karma-Tendenz zurück (für Ending-Berechnung).
    /// </summary>
    public KarmaTendency GetTendency() => Karma switch
    {
        >= 60 => KarmaTendency.Hero,        // Wahrer Held
        >= 20 => KarmaTendency.Good,        // Gutherzig
        > -20 => KarmaTendency.Neutral,     // Pragmatiker
        > -60 => KarmaTendency.Dark,        // Dunkel
        _ => KarmaTendency.Fallen           // Gefallen
    };

    /// <summary>
    /// Gibt Entscheidungen für ein bestimmtes Kapitel zurück.
    /// </summary>
    public List<FateDecision> GetDecisionsForChapter(string chapterId)
    {
        return Decisions.FindAll(d => d.ChapterId == chapterId);
    }

    /// <summary>
    /// Stellt den Zustand aus einem Save wieder her.
    /// </summary>
    public void Restore(int karma, List<FateDecision> decisions, HashSet<string>? fateFlags = null)
    {
        Karma = Math.Clamp(karma, -100, 100);
        Decisions.Clear();
        Decisions.AddRange(decisions);
        FateFlags.Clear();
        if (fateFlags != null)
            foreach (var flag in fateFlags)
                FateFlags.Add(flag);
    }
}

/// <summary>
/// Karma-Tendenz für Ending-Berechnung und NPC-Reaktionen.
/// </summary>
public enum KarmaTendency
{
    Hero,    // >= 60: Wahrer Held-Ending
    Good,    // >= 20: Gutes Ending
    Neutral, // -19 bis 19: Neutrales Ending
    Dark,    // -59 bis -20: Dunkles Ending
    Fallen   // <= -60: Gefallener Held-Ending
}
