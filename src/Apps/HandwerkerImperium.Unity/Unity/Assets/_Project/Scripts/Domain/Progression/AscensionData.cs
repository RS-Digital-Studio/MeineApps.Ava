using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Speichert alle Ascension-bezogenen Daten (Meta-Prestige).
    /// Klasse wird persistiert (JSON) - nicht löschen wegen Save-Kompatibilität.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/AscensionData.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class AscensionData
    {
        /// <summary>Anzahl der durchgeführten Ascensions.</summary>
        [JsonProperty("ascensionLevel")]
        public int AscensionLevel { get; set; }

        /// <summary>Verfügbare Ascension-Punkte zum Ausgeben.</summary>
        [JsonProperty("ascensionPoints")]
        public int AscensionPoints { get; set; }

        /// <summary>Gesamt verdiente Ascension-Punkte (Lifetime).</summary>
        [JsonProperty("totalAscensionPoints")]
        public int TotalAscensionPoints { get; set; }

        /// <summary>Gekaufte Perks mit Stufe: PerkId → Level (1-3). Alte Saves mit Level >3 werden geclampt.</summary>
        [JsonProperty("perks")]
        public Dictionary<string, int> Perks { get; set; } = new Dictionary<string, int>();

        /// <summary>Zeitpunkt der letzten Ascension (UTC).</summary>
        [JsonProperty("lastAscensionDate")]
        public DateTime LastAscensionDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Permanente Erbstücke aus dem Erbstück-Schrein. Jedes gibt +0.5% Globales Einkommen forever.
        /// Wird bei jeder Ascension befüllt mit allen aktiven Erbstücken des aktuellen Runs.
        /// </summary>
        [JsonProperty("permanentHeirlooms")]
        public List<string> PermanentHeirlooms { get; set; } = new List<string>();

        /// <summary>Gibt die Stufe eines Perks zurück (0 = nicht gekauft).</summary>
        public int GetPerkLevel(string perkId)
        {
            return Perks.TryGetValue(perkId, out var level) ? level : 0;
        }
    }
}
