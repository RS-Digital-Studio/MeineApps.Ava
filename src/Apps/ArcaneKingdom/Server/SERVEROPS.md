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

Cloud Functions (8 Endpoints, exportiert in CloudFunctions/src/index.ts)
  ├── Firestore-Trigger
  │     ├── onReportReceived         (AutoMute-Aggregation)
  │     └── onKlanMatchCompleted     (Belohnungs-Verteilung, Idempotenz-Lock)
  ├── Scheduler (Cron)
  │     ├── settleSeasonRewards      (taeglich 00:00 UTC, idempotent pro Spieler)
  │     ├── dailyTerritoryTick       (taeglich 00:00 UTC, Doppellauf-Schutz pro Gilde)
  │     └── updateThiefMultiplier    (alle 4h, DAU-basierter Dieb-Multiplikator)
  └── Callable (vom Client, App-Check erzwungen)
        ├── validateBattleResult     (Anti-Cheat: Owner/Seed-Nonce + server-autoritative Rewards)
        ├── validateIapReceipt       (Google-Play-Verifikation + Idempotenz-Ledger)
        └── createGuild              (Tag-Eindeutigkeit + Gold-Deckung + Kompensation)
```

---

## 2. Kritische Validierungen (Anti-Cheat)

### 2.1 Battle-Result-Validation (`validateBattleResult`)

Client sendet `{ worldId, nodeId, stars, seed, deckCardIds, claimedGold, claimedExp }`.

**Aktueller Stand (implementiert):**
- Vollstaendige Input-Validierung (Typen/Ranges/Pfad-sichere Ids).
- Owner-Pruefung: jede `deckCardId` muss in `players/{uid}/cardInventory` existieren.
- Seed-Nonce gegen Replay: `battleNonces/{uid}/{seed}` per RTDB-Transaction, TTL 7 Tage.
- **Server-autoritative Rewards**: Gold/Exp werden aus der NodeDefinition
  (`remoteConfig/worlds/{worldId}/{nodeId}`) + Sterne-Faktor berechnet — fehlt die
  Definition, greift eine dokumentierte Fallback-Formel (`500·w·n·sterneFaktor` Gold,
  `50·w·n·sterneFaktor` Exp). Die `claimed`-Werte werden NUR als Plausibilitaets-Vergleich
  geloggt, NICHT ausgezahlt (kein Trust-the-Client mehr).

**Offener Folgeschritt (markiert mit `// TODO BattleEngine-Replay`):**
- Der vollstaendige deterministische `BattleEngine`-Replay (Seed + Deck → Ergebnis/Sterne)
  wird separat ergaenzt, sobald die C#-Engine nach TypeScript portiert ist. Die Struktur
  (Owner/Nonce/server-autoritative Rewards) ist bereits vorbereitet.

### 2.2 IAP-Receipt-Validation (`validateIapReceipt`)

**Echte** Google-Play-Developer-API-Verifikation (`androidpublisher` via `googleapis`,
Service-Account aus Secret `GOOGLE_PLAY_SERVICE_ACCOUNT`):
- `purchases.products.get` → `purchaseState === 0` (purchased) wird geprueft.
- **Idempotenz ueber die von Google vergebene `orderId`** (NICHT den purchaseToken):
  RTDB-Transaction auf `ledger/{orderId}` reserviert die Gutschrift; ein zweiter Aufruf
  bricht ab und schreibt KEINE Diamanten (Replay-Schutz).
- Diamanten werden erst NACH erfolgreicher Ledger-Reservierung gutgeschrieben.
- `purchases.products.acknowledge` wird aufgerufen (sonst storniert Google nach 3 Tagen).
- Diamanten-Mengen pro Produkt sind server-autoritativ (nicht vom Client steuerbar).

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

`firebase.json`, `database.rules.json`, `firestore.rules` und `.firebaserc` liegen im
`Server/`-Ordner (eine Ebene ueber `CloudFunctions/`). `firebase`-Befehle daher aus
`Server/` ausfuehren.

```bash
# Einmalig
firebase login
firebase use arcanekingdom-prod
cd Server/CloudFunctions
npm install

# Secrets setzen (einmalig, NICHT im Repo)
firebase functions:secrets:set GOOGLE_PLAY_SERVICE_ACCOUNT   # JSON-Key des Play-Service-Accounts

# Production-Deploy (aus Server/ — deployt Functions + RTDB-Rules + Firestore-Rules)
cd Server
firebase deploy --only functions,database,firestore

# Nur eine Komponente aktualisieren
firebase deploy --only functions:validateBattleResult
firebase deploy --only database        # nur RTDB-Rules
firebase deploy --only firestore       # nur Firestore-Rules
```

---

## 7. Secrets & App Check

- **Admin-SDK**: laeuft in Cloud Functions automatisch mit dem Default-Service-Account
  (`admin.initializeApp()` ohne expliziten Key). Kein separates Secret noetig.
- **Google Play Developer API**: Service-Account-JSON im Secret
  `GOOGLE_PLAY_SERVICE_ACCOUNT` (kompletter JSON-Key, Rolle "Finanzdaten anzeigen" +
  API-Zugriff in der Play Console). Wird in `validateIapReceipt` via `defineSecret`
  geladen. NICHT im Repo.

Secret setzen: `firebase functions:secrets:set GOOGLE_PLAY_SERVICE_ACCOUNT`.

**App Check (M34):** Alle Callables (`validateBattleResult`, `validateIapReceipt`,
`createGuild`) setzen `enforceAppCheck: true` — Anfragen ohne gueltiges App-Check-Token
werden abgelehnt. Der Unity-Client muss App Check initialisieren (Play Integrity Provider),
sonst schlagen die Calls fehl. Trigger/Scheduler laufen server-seitig und brauchen kein
App Check.

**Input-Sanitization (M34):** Alle Callables validieren Eingaben ueber
`CloudFunctions/src/common/validation.ts` (Typen, Ranges, String-Laengen, pfad-sichere
Ids). Kein ungeprueffter Client-Wert fliesst in einen DB-Pfad.

---

## 8. Security-Rules

Default Deny-All auf beiden Datenbanken; Schreibrechte auf betrugsanfaellige Pfade liegen
ausschliesslich beim Admin-SDK (Cloud Functions). Das Admin-SDK umgeht die Rules vollstaendig.

- **`database.rules.json`** (RTDB): `players/{uid}` nur fuer den eigenen `auth.uid` lesbar;
  Currencies/cardInventory/pendingClaims/prestige/settledSeasons/chatSlice fuer Clients NUR
  lesbar (Schreibrecht ausschliesslich Cloud Functions). `battleNonces` und `ledger` sind
  vollstaendig server-only (kein Client-Zugriff).
- **`firestore.rules`**: guilds/guildTags/territories/klanMatches/seasons/remoteConfig fuer
  eingeloggte Clients lesbar, aber server-only schreibbar. Clients duerfen lediglich
  Chat-Reports (`chats/reports`) und Session-Logs (`sessionLogs`) mit eigener uid anlegen.

## 9. Naechste Schritte

1. Firebase-Projekt anlegen (`arcanekingdom-prod`) + Play-Developer-API-Service-Account.
2. Secret `GOOGLE_PLAY_SERVICE_ACCOUNT` setzen.
3. **C#-`BattleEngine`-Logik nach TypeScript portieren** und in `validateBattleResult` am
   `// TODO BattleEngine-Replay`-Haken einsetzen (Owner/Nonce/Reward-Struktur steht bereits).
4. NodeDefinition-Rewards in Firestore (`remoteConfig/worlds/{worldId}/{nodeId}`) pflegen —
   bis dahin greift die dokumentierte Fallback-Formel.
5. App Check im Unity-Client initialisieren (Play Integrity Provider).
6. Unit-Tests fuer Validation-Functions (Jest).
7. Deploy zu Staging-Environment, mit Test-Account testen.
