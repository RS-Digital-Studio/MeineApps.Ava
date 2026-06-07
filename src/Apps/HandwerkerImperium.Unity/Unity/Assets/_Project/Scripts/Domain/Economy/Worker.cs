#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Repräsentiert einen Arbeiter mit Tier, Stimmung, Müdigkeit, Erfahrung und Persönlichkeit.
    /// Arbeiter erzeugen passives Einkommen, brauchen aber Management (Ruhe, Training, Stimmung).
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Worker.cs). Reine Spiellogik. Persistenz über
    /// Newtonsoft.Json (Unity-Konvention) statt System.Text.Json. UI-/Anzeige-Felder (Emoji, Farb-Hex,
    /// lokalisierte Display-Strings, IncomeContribution) leben in der Unity-Präsentationsschicht.
    /// Unity-sicher (C# 9, netstandard2.1): kein Random.Shared, kein generisches Enum.GetValues, kein HashCode.Combine.
    /// </summary>
    public class Worker
    {
        // Geteilte RNG-Instanz (Unity-netstandard hat kein Random.Shared).
        private static readonly Random Rng = new Random();

        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("tier")]
        public WorkerTier Tier { get; set; } = WorkerTier.E;

        /// <summary>Talent-Bewertung (1-5 Sterne). Bestimmt die maximale Effizienz-Obergrenze.</summary>
        [JsonProperty("talent")]
        public int Talent { get; set; } = 3;

        [JsonProperty("personality")]
        public WorkerPersonality Personality { get; set; } = WorkerPersonality.Steady;

        /// <summary>Bevorzugter Workshop-Typ. +15% Effizienz in diesem Workshop.</summary>
        [JsonProperty("specialization")]
        public WorkshopType? Specialization { get; set; }

        /// <summary>
        /// Sekundäre Material-Affinität (Holz/Metall/Stein/Kunst/Tech). Match mit gecraftetem Material
        /// gibt +20% Crafting-Speed des Workshops. Wird beim Hiring gerollt (gleichverteilt 20%).
        /// </summary>
        [JsonProperty("materialAffinity")]
        public MaterialAffinity MaterialAffinity { get; set; } = MaterialAffinity.None;

        /// <summary>Diesem Workshop zugewiesener Arbeiter. Kann zwischen Workshops transferiert werden.</summary>
        [JsonProperty("assignedWorkshop")]
        public WorkshopType? AssignedWorkshop { get; set; }

        /// <summary>Stimmungs-Level (0-100). Unter 50 = unzufrieden, unter 20 = kündigt innerhalb 24h.</summary>
        [JsonProperty("mood")]
        public decimal Mood { get; set; } = 80m;

        /// <summary>
        /// Müdigkeits-Level (0-100). Bei 100 = erschöpft, muss ruhen.
        /// Steigt ~12.5/h beim Arbeiten, wird beim Ruhen zurückgesetzt.
        /// </summary>
        [JsonProperty("fatigue")]
        public decimal Fatigue { get; set; }

        /// <summary>Erfahrungs-Level (1-10). Erhöht die maximale Effizienz.</summary>
        [JsonProperty("experienceLevel")]
        public int ExperienceLevel { get; set; } = 1;

        /// <summary>Aktuelle XP zum nächsten Erfahrungs-Level.</summary>
        [JsonProperty("experienceXp")]
        public int ExperienceXp { get; set; }

        /// <summary>Akkumulator für fraktionale XP-Gewinne beim Arbeiten (persistiert).</summary>
        [JsonProperty("workingXpAcc")]
        public decimal WorkingXpAccumulator { get; set; }

        /// <summary>Akkumulator für fraktionale XP-Gewinne beim Training (persistiert).</summary>
        [JsonProperty("trainingXpAcc")]
        public decimal TrainingXpAccumulator { get; set; }

        /// <summary>Stundenlohn basierend auf Tier.</summary>
        [JsonProperty("wagePerHour")]
        public decimal WagePerHour { get; set; } = 10m;

        [JsonProperty("isResting")]
        public bool IsResting { get; set; }

        [JsonProperty("isTraining")]
        public bool IsTraining { get; set; }

        [JsonProperty("restStartedAt")]
        public DateTime? RestStartedAt { get; set; }

        [JsonProperty("activeTrainingType")]
        public TrainingType ActiveTrainingType { get; set; } = TrainingType.Efficiency;

        /// <summary>Training-Typ der nach automatischer Ruhe fortgesetzt werden soll (null = kein Auto-Resume).</summary>
        [JsonProperty("resumeTrainingType")]
        public TrainingType? ResumeTrainingType { get; set; }

        /// <summary>Ausdauer-Bonus (0-0.5): Reduziert FatiguePerHour permanent um bis zu 50%.</summary>
        [JsonProperty("enduranceBonus")]
        public decimal EnduranceBonus { get; set; }

        /// <summary>Stimmungs-Bonus (0-0.5): Reduziert MoodDecayPerHour permanent um bis zu 50%.</summary>
        [JsonProperty("moraleBonus")]
        public decimal MoraleBonus { get; set; }

        [JsonProperty("trainingStartedAt")]
        public DateTime? TrainingStartedAt { get; set; }

        /// <summary>
        /// Praktikanten-System. F-Tier-Worker, kostenlos eingestellt; nach 24h aktivem Training
        /// wird er zu E-Tier promoviert (Lohn-pflichtig) ODER verlässt die Werkstatt.
        /// </summary>
        [JsonProperty("isIntern")]
        public bool IsIntern { get; set; }

        /// <summary>
        /// Akkumulierte aktive Tick-Zähler für Praktikanten-Training.
        /// 1 Tick = 1 Sekunde aktive Spielzeit (NICHT Echtzeit). Promotions-Schwelle: 86400 (24h).
        /// </summary>
        [JsonProperty("internProgressTicks")]
        public int InternProgressTicks { get; set; }

        /// <summary>True wenn der Praktikant die Promotion-Schwelle erreicht hat und auf Spieler-Entscheidung wartet.</summary>
        [JsonProperty("internAwaitingPromotion")]
        public bool InternAwaitingPromotion { get; set; }

        /// <summary>Gesamtes von diesem Arbeiter verdientes Geld (Lifetime).</summary>
        [JsonProperty("totalEarned")]
        public decimal TotalEarned { get; set; }

        /// <summary>Anzahl Aufträge, zu denen dieser Arbeiter beigetragen hat.</summary>
        [JsonProperty("ordersCompleted")]
        public int OrdersCompleted { get; set; }

        /// <summary>Geschlecht des Workers (deterministisch aus Id abgeleitet).</summary>
        [JsonProperty("isFemale")]
        public bool IsFemale { get; set; }

        /// <summary>Ausgerüstetes Equipment (null = nichts ausgerüstet).</summary>
        [JsonProperty("equippedItem")]
        public Equipment? EquippedItem { get; set; }

        [JsonProperty("hiredAt")]
        public DateTime HiredAt { get; set; }

        /// <summary>Zeitpunkt, zu dem der Arbeiter kündigt, falls die Stimmung unter 20 bleibt.</summary>
        [JsonProperty("quitDeadline")]
        public DateTime? QuitDeadline { get; set; }

        // Legacy-Property für Save-Kompatibilität
        [JsonProperty("efficiency")]
        public decimal Efficiency { get; set; } = 1.0m;

        [JsonProperty("skillLevel")]
        public int SkillLevel { get; set; } = 1;

        // ═══════════════════════════════════════════════════════════════════
        // BERECHNETE PROPERTIES
        // ═══════════════════════════════════════════════════════════════════

        // ───── EffectiveEfficiency-Cache ─────
        // Im GameLoop wird EffectiveEfficiency 1x pro Sekunde für JEDEN Worker gelesen — die
        // Multi-Faktor-Decimal-Berechnung ist teuer. Mood/Fatigue/Experience ändern sich aber selten.
        // Hash-basierter Cache liefert pro Tick einen Cache-Hit (O(1)) statt 8 Decimal-Multiplikationen.
        [JsonIgnore]
        private decimal _cachedEffectiveEfficiency;
        [JsonIgnore]
        private int _cachedEfficiencyHash;
        [JsonIgnore]
        private bool _hasCachedEfficiency;

        /// <summary>
        /// Effektive Effizienz unter Berücksichtigung aller Faktoren.
        /// Formel: BaseEfficiency * XpBonus * MoodFactor * FatigueFactor * (1+Spec+Equip) * Personality * Talent.
        /// XpBonus: +3% pro ExperienceLevel → Training lohnt sich IMMER, auch am Tier-Maximum.
        /// </summary>
        [JsonIgnore]
        public decimal EffectiveEfficiency
        {
            get
            {
                if (IsResting || IsTraining) return 0m;

                int currentHash = ComputeEfficiencyInputHash();
                if (_hasCachedEfficiency && currentHash == _cachedEfficiencyHash)
                    return _cachedEffectiveEfficiency;

                decimal baseEff = Efficiency;
                decimal xpBonus = 1m + ExperienceLevel * 0.03m;
                decimal moodFactor = GetMoodFactor();
                decimal fatigueFactor = GetFatigueFactor();
                decimal specBonus = GetSpecializationBonus();
                decimal personalityMult = Personality.GetEfficiencyMultiplier();
                decimal equipBonus = EquippedItem?.EfficiencyBonus ?? 0m;
                decimal talentBonus = 1m + (Talent - 1) * 0.05m; // 1*=1.0x, 3*=1.10x, 5*=1.20x

                decimal result = Math.Max(0m, baseEff * xpBonus * moodFactor * fatigueFactor * (1m + specBonus + equipBonus) * personalityMult * talentBonus);

                _cachedEffectiveEfficiency = result;
                _cachedEfficiencyHash = currentHash;
                _hasCachedEfficiency = true;
                return result;
            }
        }

        /// <summary>
        /// Stabiler Hash über alle EffectiveEfficiency-Inputs. Änderung in einem Wert invalidiert
        /// den Cache automatisch beim nächsten Get-Aufruf — kein Setter-Hooking nötig.
        /// (Manuelle Kombination statt HashCode.Combine — Unity-netstandard-sicher.)
        /// </summary>
        private int ComputeEfficiencyInputHash()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Mood.GetHashCode();
                h = h * 31 + Fatigue.GetHashCode();
                h = h * 31 + Efficiency.GetHashCode();
                h = h * 31 + ExperienceLevel;
                h = h * 31 + Talent;
                h = h * 31 + (EquippedItem?.EfficiencyBonus ?? 0m).GetHashCode();
                h = h * 31 + (int)Personality;
                h = h * 31 + (Specialization?.GetHashCode() ?? 0);
                return h;
            }
        }

        /// <summary>Maximale erreichbare Effizienz dieses Arbeiters (Talent- + Erfahrungs-Bonus).</summary>
        [JsonIgnore]
        public decimal MaxEfficiency
        {
            get
            {
                decimal tierMax = Tier.GetMaxEfficiency();
                decimal xpBonus = 1m + ExperienceLevel * 0.03m;
                decimal talentBonus = 1m + (Talent - 1) * 0.05m;
                return tierMax * xpBonus * talentBonus;
            }
        }

        /// <summary>Stimmungs-Verfall pro Stunde (Basis 3%, modifiziert durch Persönlichkeit + MoraleBonus).</summary>
        [JsonIgnore]
        public decimal MoodDecayPerHour => 3m * Personality.GetMoodDecayMultiplier() * (1m - MoraleBonus);

        /// <summary>Müdigkeits-Zunahme pro Arbeitsstunde (Basis 12.5, reduziert durch EnduranceBonus).</summary>
        [JsonIgnore]
        public decimal FatiguePerHour => 12.5m * Personality.GetFatigueMultiplier() * (1m - EnduranceBonus);

        /// <summary>Stunden bis zur vollständigen Erholung (Basis 4h).</summary>
        [JsonIgnore]
        public decimal RestHoursNeeded => 4m;

        /// <summary>Trainings-Kosten pro Stunde (2x Stundenlohn).</summary>
        [JsonIgnore]
        public decimal TrainingCostPerHour => 2m * WagePerHour;

        /// <summary>XP-Gewinn pro Trainingsstunde (Basis 50).</summary>
        [JsonIgnore]
        public int TrainingXpPerHour => 50;

        /// <summary>XP für das nächste Erfahrungs-Level.</summary>
        [JsonIgnore]
        public int XpForNextLevel => ExperienceLevel * 200;

        [JsonIgnore]
        public bool IsTired => Fatigue >= 100m;

        [JsonIgnore]
        public bool IsUnhappy => Mood < 50m;

        [JsonIgnore]
        public bool WillQuit => Mood < 20m;

        [JsonIgnore]
        public bool IsWorking => !IsResting && !IsTraining && AssignedWorkshop != null;

        /// <summary>Anstellungskosten (level-skaliert). Persistiert, damit Marktpreise nach Neustart korrekt bleiben.</summary>
        [JsonProperty("hiringCost")]
        public decimal HiringCost { get; set; }

        /// <summary>Zusätzliche Goldschrauben-Kosten (nur Tier A und höher).</summary>
        [JsonIgnore]
        public int HiringScrewCost => Tier.GetHiringScrewCost();

        // ═══════════════════════════════════════════════════════════════════
        // METHODEN
        // ═══════════════════════════════════════════════════════════════════

        private decimal GetMoodFactor()
        {
            // Mood 100 = 1.1x, Mood 80 = 1.0x, Mood 50 = 0.8x, Mood 0 = 0.5x
            if (Mood >= 80m) return 1.0m + (Mood - 80m) / 200m;
            if (Mood >= 50m) return 0.8m + (Mood - 50m) / 150m;
            return 0.5m + Mood / 100m;
        }

        private decimal GetFatigueFactor()
        {
            // Fatigue 0 = 1.0x, 50 = 0.85x, 100 = 0.5x
            if (Fatigue <= 0m) return 1.0m;
            if (Fatigue >= 100m) return 0.5m;
            return 1.0m - (Fatigue / 200m);
        }

        private decimal GetSpecializationBonus()
        {
            if (Specialization == null || AssignedWorkshop == null) return 0m;
            if (Specialization != AssignedWorkshop) return 0m;
            return 0.15m + Personality.GetSpecializationBonus();
        }

        /// <summary>
        /// Berechnet den individuellen Marktpreis basierend auf Tier, Level, Talent, Persönlichkeit,
        /// Spezialisierung und Basis-Effizienz innerhalb des Tiers. Höherwertiger Worker = höherer Preis.
        /// </summary>
        public decimal CalculateMarketPrice(int playerLevel)
        {
            var baseCost = Tier.GetHiringCost(playerLevel);

            // Talent-Multiplikator: 1*=0.70x, 2*=0.85x, 3*=1.00x, 4*=1.15x, 5*=1.30x
            var talentMult = 0.70m + (Talent - 1) * 0.15m;

            // Persönlichkeits-Multiplikator (wertvollere Traits kosten mehr)
            var personalityMult = Personality switch
            {
                WorkerPersonality.Perfectionist => 1.20m,
                WorkerPersonality.Specialist => 1.15m,
                WorkerPersonality.Ambitious => 1.10m,
                WorkerPersonality.Cheerful => 1.05m,
                WorkerPersonality.Relaxed => 0.90m,
                _ => 1.0m // Steady
            };

            // Spezialisierung macht Worker wertvoller
            var specMult = Specialization.HasValue ? 1.15m : 1.0m;

            // Effizienz-Position innerhalb des Tiers (höhere Basis = teurer)
            var minEff = Tier.GetMinEfficiency();
            var maxEff = Tier.GetMaxEfficiency();
            var effRange = maxEff - minEff;
            var effPosition = effRange > 0 ? Math.Clamp((Efficiency - minEff) / effRange, 0m, 1m) : 0.5m;
            var effMult = 0.85m + effPosition * 0.30m; // 0.85x (Min) bis 1.15x (Max)

            return Math.Round(baseCost * talentMult * personalityMult * specMult * effMult);
        }

        /// <summary>
        /// Erzeugt einen Arbeiter für ein bestimmtes Tier mit zufälligen Attributen.
        /// Wenn <paramref name="assignedWorkshop"/> gesetzt ist, wird der Worker direkt zugewiesen
        /// (wichtig für Prestige/Ascension/Hire-Pfade, damit IsWorking=true und Fatigue korrekt akkumuliert).
        /// </summary>
        public static Worker CreateForTier(WorkerTier tier, WorkshopType? assignedWorkshop = null)
        {
            var random = Rng;
            var personality = (WorkerPersonality)random.Next(0, 6);
            var talent = tier switch
            {
                WorkerTier.F => random.Next(1, 3),        // 1-2
                WorkerTier.E => random.Next(1, 4),        // 1-3
                WorkerTier.D => random.Next(2, 4),        // 2-3
                WorkerTier.C => random.Next(2, 5),        // 2-4
                WorkerTier.B => random.Next(3, 5),        // 3-4
                WorkerTier.A => random.Next(3, 6),        // 3-5
                WorkerTier.S => random.Next(4, 6),        // 4-5
                WorkerTier.SS => random.Next(4, 6),       // 4-5
                WorkerTier.SSS => 5,                      // immer 5
                WorkerTier.Legendary => 5,                // immer 5
                _ => 3
            };

            // Spezialisierungs-Chance an Tier gekoppelt (höhere Tiers haben höhere Chance)
            double specChance = tier switch
            {
                WorkerTier.F or WorkerTier.E => 0.40,
                WorkerTier.D or WorkerTier.C => 0.50,
                WorkerTier.B or WorkerTier.A => 0.65,
                _ => 0.85 // S, SS, SSS, Legendary
            };
            WorkshopType? spec = null;
            if (random.NextDouble() < specChance)
            {
                var types = (WorkshopType[])Enum.GetValues(typeof(WorkshopType));
                spec = types[random.Next(types.Length)];
            }

            // Basis-Effizienz innerhalb der Tier-Spanne
            var minEff = tier.GetMinEfficiency();
            var maxEff = tier.GetMaxEfficiency();
            var efficiency = minEff + (maxEff - minEff) * (decimal)random.NextDouble();

            var id = Guid.NewGuid().ToString();

            bool isFemale = (id.GetHashCode() % 2 == 0);

            // Material-Affinität gleichverteilt rollen (5 Achsen).
            var affinity = (MaterialAffinity)(random.Next(1, 6));

            var worker = new Worker
            {
                Id = id,
                Name = GenerateRandomName(isFemale),
                Tier = tier,
                Talent = talent,
                Personality = personality,
                Specialization = spec,
                MaterialAffinity = affinity,
                Mood = 80m,
                Fatigue = 0m,
                ExperienceLevel = 1,
                ExperienceXp = 0,
                WagePerHour = tier.GetWagePerHour(),
                Efficiency = Math.Round(efficiency, 3),
                SkillLevel = 1,
                HiredAt = DateTime.UtcNow,
                IsFemale = isFemale,
                AssignedWorkshop = assignedWorkshop
            };

            return worker;
        }

        /// <summary>Legacy-Factory (erzeugt einen Tier-E-Worker).</summary>
        public static Worker CreateRandom(WorkshopType? assignedWorkshop = null)
        {
            return CreateForTier(WorkerTier.E, assignedWorkshop);
        }

        /// <summary>Liefert die verfügbaren Tiers basierend auf Spieler-Level und Prestige.</summary>
        public static List<WorkerTier> GetAvailableTiers(int playerLevel, int prestigeLevel, bool hasSTierResearch = false)
        {
            var tiers = new List<WorkerTier>();
            foreach (WorkerTier tier in (WorkerTier[])Enum.GetValues(typeof(WorkerTier)))
            {
                // S, SS, SSS, Legendary brauchen alle S-Tier-Research-Unlock
                if (tier >= WorkerTier.S && !hasSTierResearch) continue;
                if (playerLevel >= tier.GetUnlockLevel())
                    tiers.Add(tier);
            }
            return tiers;
        }

        // Statische Arrays (vermeidet Allokation pro Aufruf)
        // 40 männliche + 40 weibliche Vornamen (symmetrisch, deutsch + international gemischt)
        private static readonly string[] FirstNames = new string[]
        {
            // Männlich - Deutsch (20)
            "Hans", "Klaus", "Peter", "Michael", "Thomas", "Stefan", "Andreas", "Markus", "Frank", "Erik",
            "Finn", "Emil", "Anton", "Felix", "Jonas", "Tobias", "Niklas", "Moritz", "Florian", "Kai",
            // Männlich - International (20)
            "Carlos", "Marco", "Pierre", "James", "Oliver", "Lucas", "Matteo", "Hugo", "Leo", "Noah",
            "Liam", "Oscar", "Rafael", "Diego", "Ivan", "Sven", "Lars", "Axel", "Bjorn", "Rolf",
            // Weiblich - Deutsch (20)
            "Sofia", "Anna", "Maria", "Elena", "Laura", "Lena", "Clara", "Mia", "Emma", "Hannah",
            "Ingrid", "Katja", "Petra", "Monika", "Sabine", "Heidi", "Greta", "Rosa", "Frieda", "Ida",
            // Weiblich - International (20)
            "Lucia", "Camille", "Yuki", "Amara", "Zara", "Ines", "Nadia", "Leila", "Astrid", "Freya",
            "Isabella", "Valentina", "Emilia", "Chiara", "Nora", "Elise", "Carmen", "Alma", "Hana", "Saga"
        };

        private static readonly string[] Surnames = new string[]
        {
            // Deutsch
            "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner", "Becker", "Schulz", "Hoffmann",
            "Koch", "Richter", "Wolf", "Neumann", "Schwarz", "Braun", "Krüger", "Lange", "Werner", "Lehmann",
            "Hartmann", "Zimmermann", "Krause", "Berger", "Fuchs", "Engel", "Vogt", "Roth", "Keller", "Huber",
            // International
            "Martin", "Garcia", "Santos", "Silva", "Rossi", "Dupont", "Brown", "Wilson", "Anderson", "Taylor",
            "Larsson", "Berg", "Hansen", "Moreau", "Ferrari", "Russo", "Lopez", "Torres", "Ferreira", "Costa"
        };

        private static string GenerateRandomName(bool isFemale)
        {
            var random = Rng;
            // Männlich: Index 0-39 (40 Namen), Weiblich: Index 40-79 (40 Namen)
            int firstNameStart = isFemale ? 40 : 0;
            int firstNameCount = 40;
            return $"{FirstNames[firstNameStart + random.Next(firstNameCount)]} {Surnames[random.Next(Surnames.Length)]}";
        }
    }
}
