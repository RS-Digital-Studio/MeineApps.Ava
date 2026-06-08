# P2 — Content-Spec (Tiefe, Breite & Live-Ops-Basis)

> Dritte Phase der 3D-Idle-Neuausrichtung ([3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md), Phase P2).
> Baut auf der spielbaren Hansstadt aus [P1_VERTICAL_SLICE.md](P1_VERTICAL_SLICE.md) auf.
> **Zweck:** aus „ein Loop, eine Stadt" wird **ein Spiel mit Woche-1-Substanz** — alle 4 Städte, die volle
> Stadt-Sanierung, Sammel- & Cosmetic-Schichten und ein täglicher Live-Ops-Rhythmus.

---

## 1. Ziel & Exit-Gate

**Leitfrage:** *Hat ein engagierter Spieler über die erste Woche durchgehend ein lohnendes nächstes Ziel —
neue Stadt, nächstes Wahrzeichen, nächstes Master-Tool, nächster Skin, heutiger Daily?*

**Exit-Gate (alle):**
1. **Alle 4 Städte** durchspielbar (Prestige-Kette Hansstadt→…→Metropole, **max. 3 Prestige**) mit ansteigendem World-Tier-Look.
2. **Alle Distrikt-Wahrzeichen** sanierbar, jeweils mit Hans-Cutscene.
3. **Daily-Loop** (Reward + Tasks) + **Rush-Events** laufen stabil über Tagesgrenzen (UTC).
4. **Master-Tools** und **Cosmetics** geben spürbare Sammel-/Ausdrucks-Anreize.
5. **6 Sprachen** vollständig (DE/EN/ES/FR/IT/PT), Hans-Voice in allen sechs.
6. Content-Konfiguration ist **datengetrieben & validiert** (keine kaputten Kataloge).

---

## 2. Scope

### Drin
- **Franchise-Karte** mit allen **4 Städten** (echte Layouts, 4 ästhetische **World-Tiers**, saisonale Overrides möglich).
- **Volle Stadt-Sanierung:** alle **4–6 Distrikt-Wahrzeichen** je Stadt (oder gestaffelt nach World-Tier) mit 5 Bauphasen + Hans-Cutscene.
- **Master-Tools (12)** als 3D-Collectibles auf dem Hof-Altar, permanente Boni, Freischalt-Bedingungen über alle Akte gestreut.
- **Permanentes Langzeit-Rückgrat:** **Meisterschafts-Track** (kontoweite XP, nie reset, `1.15^N`) + **Imperium-Marken-Perkboard** (Marken aus den 3 Prestiges + Meilensteinen) — die eigentliche Monate-Progression (→ [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)).
- **Cosmetics-Shop:** Avatar-Skins, Werkstatt-Skins, Stadt-Deko-Themes (Hart-/Weichwährung).
- **Daily Reward** (30-Tage-Leiter) + **Daily Tasks** (3 Ziele → Gems) + **Achievements**.
- **Rush-Events** (zeitbegrenzte 2×-Phasen, 1×/Tag gratis) + **1–2 Saisons** (Deko-Override + Event-Währung + Event-Shop).
- **2–3 optionale Mini-Game-Tap-Boosts** (z. B. Säge-Schnitt/Hau-den-Nagel → temporäres 2× an einer Station).
- **Volllokalisierung 6 Sprachen** (Unity Localization String-Tables) + **Hans-Voice-Vollsatz** für alle Story-Beats.

### Raus (→ P3+)
Online-Multiplayer/Gilde, Remote-Config-getriebene Live-Events, Leaderboards, Push, Referral/Cross-Promo-Backend,
Cloud-Save, BattlePass (optional hier startbar, sonst P3). Final-Polish/Store-Assets → P4.

---

## 3. Neue / erweiterte Systeme

| System | P2-Verantwortung |
|--------|------------------|
| `FranchiseMapController` *(neu)* | 4-Städte-Karte (3 Prestige-Übergänge), Stadt-Wechsel bei Prestige, World-Tier-Look/Scaling |
| `TownRestorationService` | erweitert: **alle** Distrikte/Wahrzeichen je Stadt |
| `MasterToolService` *(neu)* | 12 Tools, Eligibility-Check (Station-Level/Auftrags-/Sanierungs-Zahlen/Prestige), Boni, Altar-Display |
| `MeisterschaftService` *(neu)* | kontoweiter XP-Track (`1.15^N`), permanenter Global-Bonus/Level, **nie reset** — Langzeit-Rückgrat |
| `ImperiumMarkenService` *(neu)* | Perkboard: Marken aus 3 Prestiges + Meilensteinen → permanente Boni (Start-Geld/Offline-Cap/Tempo/Radius) |
| `CosmeticService` + `ShopController` *(neu)* | Skins/Deko, Besitz, Anwenden, Shop-UI, Preise |
| `DailyRewardService` + `DailyTaskService` *(neu)* | 30-Tage-Leiter (Skalierung), 3 Tagesziele, Reset auf UTC-Tag |
| `AchievementService` *(neu)* | Katalog + Fortschritt + Gem-Belohnung |
| `RushEventService` *(neu)* | 2×-Phasen, Cooldown, Ad-Start |
| `SeasonalService` *(neu)* | Saison-Erkennung, Deko-Override, Event-Währung + Event-Shop |
| `MiniGameBoostService` *(neu)* | 2–3 optionale Tap-Timing-Boosts |
| `LocalizationService` | Voll-Ausbau 6 Sprachen (String-Tables) + Voice-Zuordnung |

---

## 4. Content & Daten (datengetrieben, validiert)

Alle Inhalte als **ScriptableObject-Kataloge** (Addressables), per Editor-Validator geprüft:

| Katalog | Inhalt | Werte-Referenz |
|---------|--------|----------------|
| Städte/World-Tiers | 4 Städte, Skalierungs-/Ästhetik-Parameter | neu (Genre-getunt) |
| Meisterschaft/Perkboard | XP-Kurve, Bonus/Level, Marken-Quellen, Perk-Definitionen | → PROGRESSION_BALANCING.md |
| Wahrzeichen | je Stadt 4–6, Kosten + 5 Bauphasen | neu |
| Master-Tools | 12 Defs, Boni (gesamt ~+74 %), Freischalt-Bedingungen | Original-Werte als Referenz |
| Cosmetics | Skins/Deko, Preise, Währung | neu |
| Daily Reward | 30-Tage-Leiter (`GetScaledMoney`) | Original-Logik |
| Daily Tasks | Ziel-Typen + Gem-Reward | neu |
| Achievements | 95+ Defs sinngemäß, an neue Mechanik gemappt | Original-Katalog als Referenz |
| Saisons | 4/Jahr (Spring/Summer/Autumn/Winter), Deko + SP-Währung | Original-Logik |
| Lokalisierung | alle UI-/Story-Keys × 6 Sprachen | Englisch = Basis-Fallback |

**Editor-Tooling:** ein **Content-Validator** (Editor-Menü) prüft fehlende Lok-Keys, kaputte Asset-Referenzen,
Katalog-Lücken (Master-Tool ohne Bedingung, Wahrzeichen ohne Bauphasen) **vor** dem Build.

---

## 5. Art- & Audio-Bedarf (P2 = Content-Batch der GDD §13)

| Asset | Menge P2 | Hinweis |
|-------|----------|---------|
| Stadt-Kits | 4 (je World-Tier, Ruine↔saniert) | TripoSG-Batch, Atlas/Tier |
| Wahrzeichen | 4–6 × 5 Bauphasen (× Stadt, ggf. gestaffelt) | Hero-Assets, Cloud-Fallback |
| Master-Tools | 12 + Altar + Rarity-Mats | Decal-Recolor |
| Cosmetics | Skin-Sets (Avatar/Werkstatt/Deko) | Recolor/Decal |
| Saison-Deko | 1–2 Saisons Override | modular |
| Audio | mehr Loops + Stinger + **Hans-Voice 6 Sprachen** (~900 Files) | ElevenLabs Multilingual |
| Mini-Game-Props | 2–3 Boost-Props | klein |

---

## 6. Tests & QA

- **EditMode:** Daily-Reward-Skalierung, Master-Tool-Eligibility, Saison-Erkennung (UTC-Datum, `RoundtripKind`), Achievement-Fortschritt, Cosmetic-Besitz/Anwenden.
- **Content-Validator (Editor):** 6-Sprachen-Vollständigkeit, Katalog-Integrität, Addressables-Auflösung — als CI-Gate.
- **PlayMode:** Stadt-Wechsel/World-Tier, Wahrzeichen-Sanierung je Distrikt, Daily-Reset über Mitternacht-UTC, Rush-Event-Phase, Mini-Game-Boost wirkt.
- **Lokalisierung:** Stichprobe je Sprache (Umlaute/CJK-Font nicht relevant hier, aber Akzente ES/FR/PT/IT), Voice-Zuordnung korrekt.
- **Perf-Regression:** mehr Assets → erneut 60 FPS Mid / 30+ Low-End prüfen (LOD/Atlas greifen).

---

## 7. Aufwand & Abhängigkeiten

- **Content-/Asset-lastig** — Hauptlast liegt in der KI-Asset-Pipeline (Batch 4 Städte + Wahrzeichen + Voice-6-Sprachen).
- **Abhängigkeit:** P1-Systeme (Restoration/Franchise/StarRating/Save) müssen stehen.
- **Parallelisierbar:** Lokalisierung + Voice-Batch laufen neben dem Code-Content.
- **Risiko:** World-Tier-Scaling über 4 Städte + Übergang ins Endgame (Late-Game-Progression darf nicht abreißen/eskalieren) → Balancing-Kurven früh prüfen.

---

## Verweise
- Spiel-Design: [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) (Stadt §5, Systeme §6, Progression §7, Live-Ops §10, Assets §13)
- Vorgänger: [P1_VERTICAL_SLICE.md](P1_VERTICAL_SLICE.md) · Nachfolger: [P3_SOCIAL_BETA.md](P3_SOCIAL_BETA.md)
- Tech: [CLAUDE.md](CLAUDE.md) · [ARCHITECTURE.md](ARCHITECTURE.md) · Assets/Voice: [ASSETS_AI.md](ASSETS_AI.md)
