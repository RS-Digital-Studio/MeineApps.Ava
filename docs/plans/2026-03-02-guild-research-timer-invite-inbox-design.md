# Design: Gildenforschung mit Timer + Einladungs-Inbox

**Datum:** 2026-03-02
**App:** HandwerkerImperium
**Status:** Freigegeben

---

## Feature 1: Forschungs-Timer

### Aktuell
Geld wird kollaborativ eingezahlt. Forschung wird sofort abgeschlossen wenn Ziel erreicht.

### Neu
Nach vollständiger Bezahlung startet ein Timer. Erst nach Ablauf wird die Forschung aktiv.

### Ablauf
```
Phase 1: FUNDING     → Mitglieder zahlen kollaborativ ein
Phase 2: RESEARCHING → Timer läuft (1h / 4h / 12h je nach Tier)
Phase 3: COMPLETED   → Effekt aktiv
```

### Regeln
- Während Phase 2 kann KEINE weitere Forschung gestartet werden (eine aktive Forschung gleichzeitig)
- Timer läuft offline weiter (serverbasiert via `researchStartedAt` in Firebase)
- Countdown wird im Forschungsbaum angezeigt (pulsierendes Icon + "Noch 3h 24min")
- `guild_mastery_1` ("Schnellforschung", +20% Speed) reduziert alle Timer um 20%

### Firebase-Änderung
```
/guild_research/{guildId}/{researchId}
  + researchStartedAt: "2026-03-02T14:00:00Z"  (neues Feld)
  // completed wird erst gesetzt wenn Timer abgelaufen
```

### Timer-Tiers

| Tier | Kosten | Dauer | Forschungen |
|------|--------|-------|-------------|
| 1 | < 100M | 1h | guild_expand_1, guild_income_1, guild_knowledge_1, guild_logistics_1 |
| 2 | 100M-2B | 4h | guild_expand_2, guild_income_2, guild_income_3, guild_knowledge_2, guild_workforce_1, guild_workforce_2, guild_mastery_1, guild_logistics_2 |
| 3 | > 2B | 12h | guild_expand_3, guild_income_4, guild_knowledge_3, guild_logistics_3, guild_workforce_3, guild_mastery_2 |

### Completion-Check
Beim Öffnen des Forschungsbaums und im GameLoop-Tick: `DateTime.UtcNow >= researchStartedAt + duration`. Wenn ja → `completed = true` setzen + Effekte cachen.

---

## Feature 2: Einladungs-Inbox

### Aktuell
Leader kann im Player-Browser "Einladen" klicken, aber der Spieler sieht nur einen Code den er manuell eingeben muss.

### Neu
Einladungen werden in Firebase gespeichert. Gildenlose Spieler sehen im Gildenfenster eine Inbox mit allen empfangenen Einladungen.

### Firebase-Struktur
```
/player_invites/{uid}/{guildId}
  → guildName, guildIcon, guildColor, guildLevel,
    memberCount, invitedBy, invitedAt
```

### UI für gildenlose Spieler (Browse-ViewState)
```
┌─────────────────────────────────┐
│  Einladungen (2)                │
├─────────────────────────────────┤
│  [Hammer] Meistergilde          │
│  Level 5 • 12/20 Mitglieder    │
│  Eingeladen von: Max            │
│  [Annehmen]      [Ablehnen]    │
├─────────────────────────────────┤
│  [Zahnrad] Baulöwen            │
│  Level 3 • 8/20 Mitglieder     │
│  Eingeladen von: Lisa           │
│  [Annehmen]      [Ablehnen]    │
├─────────────────────────────────┤
│                                 │
│  ── oder Gilde suchen ──       │
│  [Verfügbare Gilden anzeigen]   │
│  [Gilde erstellen]              │
└─────────────────────────────────┘
```

### Logik
- **Annehmen:** `JoinGuildAsync(guildId)` → alle anderen Einladungen löschen → `UnregisterAvailableAsync()`
- **Ablehnen:** Einzelne Einladung aus Firebase löschen
- **Senden:** `InvitePlayerCommand` schreibt direkt in `/player_invites/{targetUid}/{guildId}`
- **Max 10 Einladungen** pro Spieler (älteste wird überschrieben)
