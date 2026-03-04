# HandwerkerImperium - Gilden-System AAA-Überarbeitung

**Datum:** 2026-03-03
**Status:** Genehmigt
**Scope:** Ganzheitliche Überarbeitung: Architektur, Features, UI/UX, Renderer

---

## 1. Service-Architektur

Aufteilung des God-Service (GuildService 1139 Zeilen) in modulare Services:

```
GuildViewModel
  ├── WarSeasonSubVM → GuildWarSeasonService
  ├── ResearchSubVM  → GuildResearchService
  ├── ChatSubVM      → GuildChatService (+AutoPoll)
  └── GuildHallSubVM → GuildHallService
                           ↓
              GuildService (schlank, ~400 Zeilen)
              Membership, CRUD, Rollen, Kick, Promote, Transfer
                           ↓
                    FirebaseService
```

### Neue/Überarbeitete Services

| Service | Zeilen (ca.) | Verantwortung |
|---------|-------------|---------------|
| `GuildService` | ~400 | CRUD, Membership, 3 Rollen, Kick, Promote, Transfer, Wochenziel |
| `GuildResearchService` | ~350 | Extrahiert: 18 Forschungen, Timer, Effekte |
| `GuildWarSeasonService` | ~600 | Saison-System, Ligen, Matching, Phasen, Belohnungen |
| `GuildChatService` | ~200 | Bestehend + Auto-Refresh (20s Polling), Quick-Reactions |
| `GuildHallService` | ~400 | Interaktives Hauptquartier, Gebäude, Upgrades |
| `GuildBossService` | ~350 | Kooperative Bosse, HP-Pool, Damage-Tracking |
| `GuildTipService` | ~150 | Kontextuelle Tipps, First-Time-Flags |
| `GuildAchievementService` | ~250 | Gilden-Achievements, Meilensteine, Cosmetics |

### 3-Rollen-System

| Rolle | Rechte |
|-------|--------|
| **Leader** | Alles + Gilde löschen, Leader übertragen, Offiziere ernennen |
| **Offizier** | Einladen, Kicken (nur Members), Forschung starten, War-Strategie setzen |
| **Member** | Beitragen, Chatten, Kämpfen, Forschen (beitragen) |

---

## 2. Firebase-Datenstruktur

### Bestehende Pfade (überarbeitet)

```
guilds/{guildId}/
  ├── name, icon, color, level
  ├── memberCount
  ├── maxMembers              ← NEU: durch Forschung/Hauptquartier erhöhbar
  ├── weeklyGoal, weeklyProgress, weekStartUtc
  ├── totalWeeksCompleted
  ├── leagueId                ← NEU: "bronze" | "silver" | "gold" | "diamond"
  ├── leaguePoints            ← NEU: Punkte für Auf-/Abstieg
  ├── hallLevel               ← NEU: Hauptquartier-Stufe (1-10)
  ├── createdBy, createdAt
  └── description             ← NEU: Gildenbeschreibung (max 200 Zeichen)

guild_members/{guildId}/{uid}/
  ├── name, role              ← role: "leader" | "officer" | "member"
  ├── contribution, playerLevel
  ├── joinedAt                ← NEU
  ├── lastActiveAt            ← NEU: wird bei jeder Aktion aktualisiert
  ├── weeklyWarScore          ← NEU: Kriegs-Beitrag diese Woche
  └── totalWarScore           ← NEU: Gesamt-Kriegs-Beitrag
```

### Neue Pfade: Saison-System

```
guild_war_seasons/{seasonId}/
  ├── startDate, endDate
  ├── status                  ← "active" | "completed"
  └── week                    ← 1-4

guild_war_seasons/{seasonId}/leagues/{leagueId}/
  └── {guildId}/
      ├── points, wins, losses
      └── rank

guild_wars/{seasonId}_{weekNr}_{warId}/
  ├── guildAId, guildBId
  ├── guildAName, guildBName
  ├── guildALevel, guildBLevel
  ├── scoreA, scoreB
  ├── phase                   ← "attack" | "defense" | "completed"
  ├── phaseEndsAt
  ├── startDate, endDate
  └── status

guild_war_scores/{warId}/{guildId}/{uid}/
  ├── attackScore             ← getrennte Phasen-Scores
  ├── defenseScore
  └── updatedAt

guild_war_log/{warId}/
  └── {pushKey}/
      ├── type                ← "score" | "milestone" | "phase_change"
      ├── guildId, playerName
      ├── points, message
      └── timestamp
```

### Neue Pfade: Gilden-Bosse

```
guild_bosses/{guildId}/
  ├── bossId                  ← "stone_golem" | "iron_titan" | ...
  ├── bossHp, currentHp
  ├── bossLevel
  ├── startedAt, expiresAt    ← 48h Zeitlimit
  ├── status                  ← "active" | "defeated" | "expired"
  └── rewards/

guild_boss_damage/{guildId}/{uid}/
  ├── totalDamage
  ├── hits
  └── lastHitAt
```

### Neue Pfade: Hauptquartier

```
guild_hall/{guildId}/
  ├── hallLevel               ← 1-10
  └── buildings/
      └── {buildingId}/
          ├── level
          ├── upgradingUntil
          └── unlockedAt
```

### Neue Pfade: Achievements

```
guild_achievements/{guildId}/
  └── {achievementId}/
      ├── progress
      ├── completed
      └── completedAt
```

### Race-Condition-Fix

Statt read-modify-write für War-Scores: Jeder Spieler schreibt nur SEINEN Score. Gesamt-Score wird client-seitig aus allen Member-Scores aggregiert.

---

## 3. Gildenkrieg - Saison & Ligen-System

### Saison-Zyklus

```
Saison (4 Wochen)
├── Woche 1-4: Je ein War (Mo-So)
│   ├── Angriffsphase   (Mo 00:00 - Do 00:00 UTC) → 3 Tage
│   ├── Verteidigungsphase (Do 00:00 - Sa 00:00 UTC) → 2 Tage
│   └── Auswertung (Sa 00:00 - So 23:59 UTC)
└── Saison-Abrechnung (Sonntag Woche 4, 23:59 UTC)
```

### Ligen

| Liga | Aufstieg | Abstieg | Saison-Belohnung |
|------|----------|---------|------------------|
| Bronze | Top 30% → Silber | - | 10 Goldschrauben |
| Silber | Top 25% → Gold | Bottom 25% → Bronze | 25 GS + Silber-Banner |
| Gold | Top 20% → Diamant | Bottom 20% → Silber | 50 GS + Gold-Banner |
| Diamant | - | Bottom 15% → Gold | 100 GS + Diamant-Banner + Cosmetic |

Neue Gilden starten in Bronze. Punkte: Sieg=+3, Unentschieden=+1, Niederlage=+0.

### Matching

1. Alle Gilden derselben Liga sammeln
2. Nach Level sortieren (±3 Toleranz)
3. Zufällig paaren, übrige: ±5 erweitern
4. Letzte übrige: Bye-Woche (+1 Punkt)

### Phasen-Mechanik

**Angriff (3 Tage):** Punkte durch Aufträge (100P), Mini-Games (75P), Crafting (75P), Upgrades (25P). Plus 3 tägliche Bonus-Missionen (200P/150P/100P).

**Verteidigung (2 Tage):** Punkte halbiert. Forschung "Festungsmauern" gibt +10%. Aufhol-Multiplikator 1.5x für die zurückliegende Gilde.

**Belohnungen pro Krieg:**
- Sieg: 20 GS + 3 Liga-Punkte
- Unentschieden: 10 GS + 1 Liga-Punkt
- Niederlage: 5 GS + 0 Liga-Punkte
- MVP: +5 GS Bonus
- Alle 3 Bonus-Missionen: +3 GS

---

## 4. Gilden-Bosse

Wöchentlich spawnt ein Boss (48h Zeitlimit). Schaden durch normale Spielaktionen.

### Schadens-Quellen

| Aktion | Basis-Schaden |
|--------|--------------|
| Auftrag abschließen | 500 HP |
| Mini-Game perfekt | 400 HP |
| Mini-Game normal | 200 HP |
| Crafting | 150 HP |
| Gildenbeitrag 10.000€ | 100 HP |
| Forschung beitragen | 300 HP |

### Boss-Roster (6, rotierend)

| Boss | HP-Formel | Besonderheit |
|------|-----------|-------------|
| Steingolem | 5.000 × GildeLv | Standard |
| Eisentitan | 7.500 × GildeLv | Crafting 2x |
| Meisterarchitekt | 6.000 × GildeLv | Aufträge 2x |
| Rostdrache | 8.000 × GildeLv | Mini-Games 2x |
| Schattenhändler | 5.500 × GildeLv | Geldspenden 3x |
| Uhrwerk-Koloss | 10.000 × GildeLv | 24h, alle 1.5x |

### Belohnungen

| Rang | Belohnung |
|------|-----------|
| MVP (Platz 1) | 30 GS + Boss-Trophy-Cosmetic |
| Top 3 | 20 GS |
| Teilgenommen | 10 GS |
| Gilde gesamt | +1 Gildenlevel-Fortschritt |

---

## 5. Interaktives Hauptquartier (Gildenhalle)

Isometrische SkiaSharp-Szene die mit Gildenlevel wächst.

### 10 Hallen-Level

| Level | Visuell | Neue Gebäude |
|-------|---------|-------------|
| 1 | Holzhütte | Werkstatt (Start) |
| 2 | Steinmauern | Forschungslabor |
| 3 | Größer, Fahne | Handelskontor |
| 4 | Schmiede-Anbau | Schmiede |
| 5 | Turm | Wachturm |
| 6 | Innenhof | Versammlungshalle |
| 7 | Zweites Stockwerk | Schatzkammer |
| 8 | Burgmauer | Festungsmauer |
| 9 | Große Fahne + Banner | Trophäenhalle |
| 10 | Prachtvolle Burg | Meisterthron |

### Gebäude-Effekte

| Gebäude | Effekt pro Level | Max |
|---------|-----------------|-----|
| Werkstatt | +2% Crafting-Speed | 5 |
| Forschungslabor | -5% Forschungszeit | 5 |
| Handelskontor | +3% Einkommen | 5 |
| Schmiede | +2% Auftragsbelohnung | 5 |
| Wachturm | +5% War-Punkte | 5 |
| Versammlungshalle | +2 Max-Mitglieder | 3 |
| Schatzkammer | +5% Wochenziel-Belohnung | 3 |
| Festungsmauer | +5% Verteidigungsbonus | 3 |
| Trophäenhalle | Zeigt Achievements + Trophäen | 1 |
| Meisterthron | +5% auf ALLES | 1 |

Kosten: Goldschrauben + Gildengeld. Timer: 1h/4h/12h je nach Tier.

---

## 6. UI/UX & Onboarding

### Gilden-Hub (Neue Struktur)

```
Gilden-Tab (Hub)
├── Header: Gildenhalle-Szene (interaktiv)
├── Quick-Status: Liga | War | Boss | Forschung (tappbar)
└── Sub-Navigation: Übersicht | Krieg | Forschung | Mitglieder | Chat
```

### "Warum eine Gilde?" Vorteile-Karte

Für Spieler ohne Gilde: Klare Darstellung aller Vorteile (+20% Einkommen, Kriege, Bosse, Forschung, Hauptquartier, Ligen) vor dem Browse-Screen.

### Kontextuelle Tipps (GuildTipService)

8 First-Time-Tipps als goldener Balken (Pergament-Textur, 8s Auto-Dismiss):
- Gilde beigetreten, Forschungsbaum, Gildenkrieg, Boss-Spawn, Hauptquartier, Offizier-Beförderung, Saison-Abrechnung, Chat

### Info-Buttons (❓)

Bottom-Sheets mit Erklärungen für: Wochenziel, Forschung, Liga, Boss, Gebäude.

---

## 7. Gilden-Achievements (30 Stück, 3 Tiers)

### Kategorien

**Gemeinsam stark:** Gildengeld (100K/1M/10M), Forschungen (3/9/18), Mitglieder (5/10/20)
**Kriegshelden:** Kriege gewinnen (3/10/50), Saisons (1/4/12), Liga (Silber/Gold/Diamant)
**Drachentöter:** Bosse besiegen (3/10/50), MVP (1/5/20), Boss <24h (1/3/10)
**Baumeister:** Gebäude Max (1/5/10), Hallen-Level (3/6/10)

Belohnungen: Bronze=5 GS, Silber=15 GS + Banner, Gold=30 GS + Wappen-Teil.

---

## 8. SkiaSharp-Renderer

### Neue Renderer

| Renderer | Render-Loop |
|----------|-------------|
| `GuildHallSceneRenderer` | Ja (20fps, Offscreen-Cache für Terrain) |
| `GuildBossRenderer` | Ja (Atem, Partikel, HP-Animation) |
| `GuildWarDashboardRenderer` | Nein (statisch + Countdown) |
| `GuildLeagueBadgeRenderer` | Ja (Shimmer-Loop) |
| `GuildWarLogRenderer` | Nein (Text-Liste) |
| `GuildAchievementRenderer` | Nein (statisch + Glow) |

### Bestehende Renderer Fixes

- `GuildHallHeaderRenderer`: Fackel-Shader cachen statt pro Frame
- `GuildResearchTreeRenderer`: SKPathEffect.CreateDash cachen, Swap-Remove für Partikel

### Performance-Strategie

| Technik | Anwendung |
|---------|-----------|
| Offscreen-Bitmap-Cache | Terrain, Gebäude, Boss-Hintergrund |
| Shader-Caching | Alle Gradients + Vignetten |
| Struct-Pools | Alle Partikel-Systeme |
| Swap-Remove | Partikel-Listen |
| Dirty-Flag | Nur bei Datenänderung neu rendern |
| Frame-Budget | 20fps animiert, 0fps statisch |
| Dispose-Pattern | Vollständiger Cleanup |
