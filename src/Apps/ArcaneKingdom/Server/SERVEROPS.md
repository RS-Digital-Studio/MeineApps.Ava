# ArcaneKingdom — Server-Operations + Cloud-Functions-Spec

> Server-Komponente fuer Anti-Cheat-Validierung, Saison-Belohnungs-Verteilung
> und Aggregations-Logik. Implementierung: **Firebase Cloud Functions (Node.js + TypeScript)**.
> Code-Skelette unter `CloudFunctions/`. Deployment via `firebase deploy --only functions`.

Diese Doku gehoert zur ArcaneKingdom-App und beschreibt **was server-seitig laufen muss**,
damit Client-Mutationen sicher gegen Cheating sind.

---

## 1. Architektur

```
Client (Unity) ─────► Firebase Auth (ID-Token)
                  │
                  ├──► Firebase Realtime DB    (Spieler-Save)
                  ├──► Firestore               (Gilden, Klan-Matches, Marktplatz, Reports)
                  ├──► HTTPS Callable Functions (kritische Operationen)
                  └──► Firebase Cloud Messaging (Push)

Cloud Functions
  ├── Triggered (DB-Listener)
  │     ├── onPlayerWriteValidate    (Schema/Cap-Pruefung)
  │     ├── onReportReceived         (AutoMute-Aggregation)
  │     └── onKlanMatchCompleted     (Belohnungs-Verteilung)
  └── Callable (vom Client)
        ├── validateBattleResult     (Anti-Cheat fuer World-Battles)
        ├── validateIapReceipt       (Google Play Receipt)
        ├── settleSeasonRewards      (taeglicher Cron 00:00 UTC)
        └── createGuild              (Tag-Eindeutigkeits-Transaction)
```

---

## 2. Kritische Validierungen (Anti-Cheat)

### 2.1 Battle-Result-Validation (`validateBattleResult`)

Client sendet `{ worldId, nodeId, stars, deckSnapshot, seed, claimedRewards }`.
Server fuehrt **denselben deterministischen `BattleEngine`** mit dem Seed aus
(C#-Logik wird nach TypeScript portiert, Tests laufen in beiden) und vergleicht
das Ergebnis. Belohnungen werden NUR von der Server-Seite ausgezahlt.

**Akzeptanz-Kriterien:**
- Deck enthaelt nur Karten, die dem Spieler gehoeren
- Seed wurde nicht wiederverwendet (Nonce-Liste 7 Tage)
- Sterne-Anzahl plausibel (z.B. nicht 4★ bei Player-Level <  Welt-Empfehlung)
- Reward-Liste matcht WeltDefinition + DeckLevel

### 2.2 IAP-Receipt-Validation (`validateIapReceipt`)

Google Play Developer API ueberprueft das Purchase-Token. Erst nach
Server-Bestaetigung werden Diamanten geschrieben (Idempotenz via Transaction-Id).

### 2.3 Card-Drop-Validation

Jeder Karten-Drop wird mit `serverTimestamp` versehen. Client kann lokal
optimistisch droppen, Server-Snapshot ist die Wahrheit (Conflict-Resolution).

### 2.4 Saison-Belohnungen (`settleSeasonRewards`)

- Triggert via Cloud Scheduler taeglich um 00:00 UTC
- Sucht Saisons, deren `endsAt` in den letzten 24h liegt
- Wendet `SeasonRewardTable.Tier(rangPunkte)` an
- Schreibt `PendingClaim`-Eintraege in alle Spieler-Saves

---

## 3. Aggregations-Logik (Server-only)

| Aggregation | Trigger | Output |
|-------------|---------|--------|
| Dieb-HP-Master | Photon Realtime Webhook | Authoritativer HP-Wert, Top-Attackers-Liste |
| Auto-Mute Chat | onReportReceived (Firestore) | `chatSlice.mutedUntilUtc` auf 24h gesetzt |
| Saison-XP-Cap | settleSeasonRewards | Saison-Pass-Stufen-Cap durchsetzen |
| DAU-basierter Dieb-Multiplikator | Cron alle 4h | `remoteConfig.thiefDauMultiplier` setzen |
| Territory-Gold-Tick | Cron taeglich 00:00 UTC | `guild.treasury` + Gold zuteilen |

---

## 4. Pricing-Erwartung

| Service | Free-Tier reicht bis | Erwartete Kosten ab Production (10k DAU) |
|---------|---------------------|-------------------------------------------|
| Realtime DB | 1 GB Storage, 10 GB/Monat Download | ca. 25 USD/Monat |
| Firestore | 1 GB Storage, 50k Reads/Tag | ca. 40 USD/Monat |
| Cloud Functions | 2M Invocations/Monat | ca. 30 USD/Monat |
| Cloud Messaging | unbegrenzt | gratis |
| Hosting/Storage | 5 GB | ca. 10 USD/Monat (Replays) |

---

## 5. Code-Skelette

Siehe `CloudFunctions/index.ts` fuer die Function-Exports und die einzelnen
Module unter `CloudFunctions/src/`.

---

## 6. Deployment

```bash
# Einmalig
firebase login
firebase use arcanekingdom-prod
cd Server/CloudFunctions
npm install

# Production-Deploy
npm run lint && npm run build
firebase deploy --only functions

# Nur eine Function aktualisieren
firebase deploy --only functions:validateBattleResult
```

---

## 7. Secrets

- Service-Account-Key fuer Admin-SDK: in Cloud Functions Environment-Variable
  `FIREBASE_SERVICE_ACCOUNT` (NICHT im Repo).
- Google Play Developer API Credentials: separates Secret
  `GOOGLE_PLAY_API_KEY`.

Beide werden ueber `firebase functions:secrets:set <NAME>` verwaltet.

---

## 8. Naechste Schritte

1. Firebase-Projekt anlegen + Service-Account erstellen
2. `CloudFunctions/` mit `npm init` + `firebase init functions` initialisieren
3. C#-`BattleEngine`-Logik in TypeScript portieren (Skelett vorhanden)
4. Unit-Tests fuer Validation-Functions (Jest)
5. Deploy zu Staging-Environment, mit Test-Account testen
