# BomberBlast 3D — Roadmap & Produktion (Solo-Indie, v0.5)

> Schlanker Produktionsplan für **modernes 3D-Bomberman, reiner Single-Player**, gebaut von **einer
> Person + KI-Assistenz**. Komplementär zu [PLAN.md](PLAN.md) (Vision/Phasen — maßgeblich),
> [DESIGN.md](DESIGN.md) (Game-Design), [ARCHITECTURE.md](ARCHITECTURE.md) (Tech). Stand v0.5 (2026-06-08).
>
> **Bewusst NICHT Teil von v0.5 (alte Logik entfernt):** kein Vollzeit-Studio/Budget, **kein Multiplayer/
> Photon/Netcode, kein Esports, kein Online-PvP/Co-op, kein iOS/Steam/Cross-Save, kein 1:1-Parität-Mandat,
> kein Idle/AFK**. Verbindliche Phasen-Übersicht → [PLAN.md §11](PLAN.md).

---

## Inhaltsverzeichnis

1. [Arbeitsweise (Solo + KI)](#1-arbeitsweise-solo--ki)
2. [Laufende Kosten (lean)](#2-laufende-kosten-lean)
3. [Roadmap (~12 Monate)](#3-roadmap-12-monate)
4. [Compliance (DSGVO, COPPA, PEGI, Lootbox)](#4-compliance-dsgvo-coppa-pegi-lootbox)
5. [Anti-Cheat (Single-Player)](#5-anti-cheat-single-player)
6. [LiveOps & Saisons](#6-liveops--saisons)
7. [Hot-Patch & Krisen-Reaktion](#7-hot-patch--krisen-reaktion)
8. [Risiko-Register](#8-risiko-register)
9. [Marketing & Launch (lean)](#9-marketing--launch-lean)
10. [Pre-Launch-Checkliste](#10-pre-launch-checkliste)
11. [Post-Launch-Saisons](#11-post-launch-saisons)

---

## 1. Arbeitsweise (Solo + KI)

Kein Team-Stellenplan — **eine Person trägt alle Hüte**, KI-Assistenz beschleunigt Code/Art/Doku.
Disziplin statt Personal:

- **Fokus-Reihenfolge:** aktiver Bomberman-Kern → Meta-Progression → Content-Breite → Polish. Jede Phase
  hat ein **klares Gate** (lauffähig + getestet), bevor die nächste startet.
- **Scope-Schutz:** Content maximal aus dem Original wiederverwenden (Domain-Code, Balancing). Alles, was
  nicht zum Single-Player-Kern beiträgt, fällt raus.
- **Rhythmus:** 2-Wochen-Iterationen, am Ende jeweils ein spielbarer Stand auf Min-Spec-Device (Galaxy A50).
- **Definition-of-Done je Feature:** Code + Tests (Domain/Service) + 0 Warnungen + Min-Spec-Performance + Doku-Update.

---

## 2. Laufende Kosten (lean)

Nur reale Sachkosten — **keine Personalkosten, keine Photon-/Server-Match-Kosten** (kein Multiplayer).

| Posten | Kosten (Indikation) | Hinweis |
|--------|---------------------|---------|
| Google Play Developer | 25 $ einmalig | Pflicht |
| Firebase (Spark/Blaze) | 0–~10 €/Mo | Cloud-Save + async Grid-Rankings + Remote Config + Push; bei kleiner Nutzerzahl meist Free-Tier |
| Unity | Personal/kostenlos solange unter Umsatz-Schwelle | sonst Unity-Lizenz prüfen |
| KI-Asset-Tools | variabel | lokal (ComfyUI) bevorzugt; Cloud nur wo nötig ([ASSETS_AI.md](ASSETS_AI.md)) |
| Audio/Sonstiges | gering | CC0-Basis + optionale Tools |

> **Gestrichen ggü. alter Planung:** Personal (5+ FTE), Photon-CCU-Kosten, dedizierte Server, Esports-Infra,
> bezahltes UA-Marketing-Budget.

---

## 3. Roadmap (~12 Monate)

> Verbindliche Phasen → [PLAN.md §11](PLAN.md). Hier die Produktions-Sicht, Single-Player-only.

| Phase | Monat | Hauptergebnis |
|-------|-------|---------------|
| **0 Setup** | 1 | Unity-6/URP-Skelett, Asmdefs, CI, Daten-Importer ([SETUP.md](SETUP.md)) |
| **1 Aktiver Core** | 2–4 | Grid, Bomben/Ketten, 12 PowerUps, 12 Gegner, 5 Wardens, 100 Level in 10 Sektoren, Combo, HUD; Fixed-Step-Sim |
| **2 Meta-Progression** | 4–6 | Coins/Gems, 12 Shop-Upgrades, Karten/Deck/Crafting, Helden, 72 Achievements, Cloud-Save, Tutorial |
| **3 Modi & LiveOps** | 6–8 | Master-Mode (Reborn/NG+), Anomaly-Dives, Boss-Rush, Grid-Rankings (async), Daily/Weekly/Events, Lucky-Spin, Battle-Pass |
| **4 3D-Art & Polish** | 8–9 | alle Sektoren/Helden/Gegner/Wardens, VFX-Graph, Shader, adaptive Music, Story-Cutscenes, Cosmetics |
| **5 Closed Beta DACH** | 9–10 | Balancing, Low-End-Performance, Tutorial-Funnel, LiveOps-Tooling |
| **6 Soft-Launch DACH** | 11–12 | Saison 1, Stabilisierung |
| **7 Skalierung** | 13+ | EU → Global, weitere Saisons & Content. **Kein** Multiplayer, **kein** iOS/Steam |

**Realistischer Soft-Launch ~Monat 12.**

---

## 4. Compliance (DSGVO, COPPA, PEGI, Lootbox)

- **DSGVO (EU):** `IAccountDeletionService` (Art. 17, Cascade Local→Firebase), `IDataExportService`
  (Art. 20, JSON+Human-Readable), Consent-Toggles, Privacy Policy verlinkt. Daten minimal halten.
- **COPPA / Kinder:** altersgerecht; falls < 13 adressiert, keine personalisierte Werbung, Age-Gate prüfen.
- **PEGI/USK/ESRB:** stilisierte Bomben-Action ohne Blut → erwartbar **PEGI 7–12**; Rating-Fragebögen
  wahrheitsgemäß (keine Lootboxen, keine Echtgeld-Glücksmechanik).
- **Lootbox-Regulierung (UK/BE/NL etc.):** **keine Lootboxen.** Lucky-Spin mit **transparenten Drop-Rates +
  Pity-Counter** (`GetDropRates()`-Disclosure). Battle-Pass mit klaren Rewards pro Tier (kein Zufall).
- **Werbung:** Rewarded opt-in, kein Banner; 1,99 € Remove-Ads. Consent für Ad-SDK (UMP/Consent-SDK).

---

## 5. Anti-Cheat (Single-Player)

Kein Online-Match → kein server-autoritatives Anti-Cheat. Fokus auf **lokale Integrität**:

- **Zeit-Manipulation:** Hybrid-Timer (`Environment.TickCount64` **+** persistierte `DateTime.UtcNow`,
  OR-verknüpft) für Daily-Bonus/Cooldowns/Comeback.
- **Save-Integrität:** Overflow-Guards (`(long)+amount`-Clamp), `PersistenceHealth`-Corruption-Flag,
  Cloud-Pull-statt-Push bei erkannter Korruption (kein Data-Loss).
- **Grid-Rankings (async):** Firebase-Security-Rules + Server-Timestamp + Write-Rate-Limit; Score-Plausibilität
  serverseitig per RTDB-Rules begrenzen. Profanity-Filter + Report-Button. **Keine** Echtzeit-Match-Validierung nötig.

---

## 6. LiveOps & Saisons

- **Battle-Pass-Saison** 30 Tage (Free + optional Premium), Theme-Rotation (10 Themes deterministisch).
- **Liga-Saison** 14 Tage (Grid-Rankings, Perzentil-Promotion/Relegation, NPC-Backfill).
- **Wochen-Events** (ISO-Wochen-Seed, deterministisch) + **saisonale Events** (Halloween/Christmas/NewYear/Summer).
- **Daily/Weekly-Missions**, Daily-Reward (7-Tage + Comeback), Lucky-Spin, Rotating-Deals.
- **Remote Config** steuert Event-Overrides ohne Client-Update; **Push** (FCM) für Daily/Event-Reminder.
- **Tooling:** schlanke Remote-Config-Schalter + ein einfaches Telemetrie-Dashboard (welche Sektoren/Level
  hängen, Tutorial-Funnel, Reborn-Rate) für datengetriebenes Balancing.

---

## 7. Hot-Patch & Krisen-Reaktion

| Severity | Beispiel | Reaktion |
|----------|----------|----------|
| **S1 kritisch** | Crash beim Start, Save-Loss, IAP defekt | Sofort: Remote-Config-Kill-Switch / Hot-Fix-Build, Kommunikation in Store-Notes |
| **S2 hoch** | Progression-Blocker, Balancing-Exploit | Patch in Tagen; Remote-Config-Workaround wo möglich |
| **S3 mittel** | UI-Bug, kleinere Balancing-Schieflage | nächster regulärer Release |

- **Kill-Switches** für riskante Features via Remote Config (Event abschalten, Angebot zurückziehen).
- **Rollback:** vorherige AAB im Play-Console-Track behalten; Crash-Free-Rate als Trigger (< 99 % → Stopp/Review).

---

## 8. Risiko-Register

| # | Risiko | Wkt. | Impact | Mitigation |
|---|--------|------|--------|------------|
| 1 | **Scope** für Solo zu groß | Hoch | Hoch | Phasen-Gates, Content-Reuse, Single-Player-Fokus (kein MP), Polish nach hinten |
| 2 | **3D-Performance Low-End** | Mittel | Hoch | Hardware-Tier, LOD, VFX-Caps, Object-Pooling, Min-Spec-Test pro Iteration |
| 3 | **Balancing/Schwierigkeit** | Mittel | Mittel | `BalancingConfig`-ScriptableObject, Beta-Telemetrie, Tuning-Loop |
| 4 | **Save-Integrität / Migration** | Mittel | Hoch | Schema-Migrator, Overflow-Guards, Corruption-Schutz, Backup-Pull |
| 5 | **Motivations-/Zeit-Risiko (Solo)** | Mittel | Mittel | kleine spielbare Meilensteine, realistischer 12-Mo-Plan, KI-Assistenz für Fleißarbeit |
| 6 | **Store-Compliance** (Ads/Consent/Rating) | Niedrig | Mittel | UMP-Consent, ehrliche Rating-Angaben, keine Lootboxen |

---

## 9. Marketing & Launch (lean)

- **Soft-Launch DACH** (Android, Closed/Open Testing) → Feedback + Balancing → **EU** → **Global**.
- **ASO:** Keyword-optimierter Store-Eintrag (Bomberman/Arcade/3D), 5 Screenshots (Boss-Action, Anomaly-Dive,
  Hero-Showcase, Sektor-Variety, Cosmetics), kurzer Gameplay-Trailer (3D-Optik + Combos + Warden-Reveal).
- **Organisch statt Paid:** kurze Clips (Combos/Reborn/Boss) für Social; Cross-Promotion aus dem eigenen
  App-Portfolio (House-Ads). **Kein** bezahltes UA-Budget eingeplant.
- **Reviews:** In-App-Review-Prompt nach Erfolgserlebnis (z. B. erster Warden-Sieg).

---

## 10. Pre-Launch-Checkliste

**Technisch:** 60/30 FPS High/Low-End · Crash-Free ≥ 99 % · Cloud-Save-Sync ≥ 99,5 % · AAB < 250 MB
(Play-Asset-Delivery) · Determinismus-Replay-Suite grün (Daily-Race) · 0 Build-Warnungen.

**Content:** 100 Level in 10 Sektoren + Master-Mode · 5 Wardens (+8 Modifier) · 12 Gegner · 14 Bomben ·
12 PowerUps · Anomaly-Dives · Grid-Rankings · Battle-Pass · 72 Achievements · 98 Cosmetics · 6 Sprachen.

**Compliance:** DSGVO-Delete/Export · UMP-Consent · ehrliches Store-Rating · keine Lootboxen · Privacy Policy.

**Operations:** Remote-Config-Schalter · Push-Trigger · Telemetrie-Dashboard · Hot-Fix-Pfad getestet.

---

## 11. Post-Launch-Saisons

| Saison | Monat | Inhalt |
|--------|-------|--------|
| 1 | 12–14 | Launch-Saison, alle 5 Helden + 10 Sektoren, BP-Theme Classic |
| 2 | 14–16 | BP-Theme Cyberpunk; neues saisonales Event; Balancing-Pass aus Beta-Daten |
| 3 | 16–18 | BP-Theme aus Rotation; neue Cosmetics; ggf. neuer Bomben-/Karten-Inhalt |
| 4+ | 18+ | EU/Global-Skalierung, weitere Saisons, optional 6. Held als echte Content-Erweiterung |

> Reine Single-Player-Content-Updates. **Kein** Versus/Co-op/Esports.

---

## Änderungslog (ROADMAP)

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial 18-Mo-Plan, Team, Marketing, Compliance | Robert Schneider + Claude |
| 2026-05-30 | v0.2 | Auf treuen 3D-Remake ausgerichtet | Robert Schneider + Claude |
| 2026-06-08 | **v0.5** | **Komplett-Neubau schlank: Solo-Indie, reiner Single-Player. Team/Budget/Esports/Online-MP/Photon/iOS/Steam/Cross-Save und 1:1-Parität-Mandat **entfernt**. Behalten/getrimmt: Compliance, Single-Player-Anti-Cheat, LiveOps/Saisons, Risiken, Launch, Post-Launch.** | Robert Schneider + Claude |

> **Status:** v0.5 — Solo-Indie-Single-Player-Produktionsplan. Maßgeblich für Scope/Phasen: [PLAN.md §11](PLAN.md).
> **Nächste Schritte:** [SETUP.md](SETUP.md) → Vertical-Slice (Sektor 1 + Granite Warden).
