# ArcaneKingdom Cloud Functions

Server-seitige Logik fuer ArcaneKingdom — siehe [../SERVEROPS.md](../SERVEROPS.md) fuer
Architektur, Pricing-Erwartung und Deployment-Schritte.

## Lokal entwickeln

```bash
npm install
npm run build
npm run serve     # firebase emulators:start --only functions
```

## Deploy

```bash
firebase login
firebase use arcanekingdom-prod
npm run deploy
```

## Tests

```bash
npm test           # Jest, alle src/**/*.test.ts
```

## Function-Liste

| Name | Trigger | Zweck |
|------|---------|-------|
| `validateBattleResult` | HTTPS Callable | Anti-Cheat fuer Welt-Kaempfe |
| `validateIapReceipt` | HTTPS Callable | Google Play Receipt-Validierung |
| `createGuild` | HTTPS Callable | Tag-Eindeutigkeit + Gold abziehen |
| `settleSeasonRewards` | Cron (00:00 UTC) | Saison-End-Belohnungen verteilen |
| `dailyTerritoryTick` | Cron (00:00 UTC) | Gilden-Treasury aus Gebiets-Boni |
| `updateThiefMultiplier` | Cron (4h) | DAU-basierter Dieb-Multiplikator |
| `onReportReceived` | Firestore Trigger | AutoMute bei 3 Reports/24h |
| `onKlanMatchCompleted` | Firestore Trigger | Belohnungen + Territory-Transfer |

## Naechste Schritte

1. Firebase-Projekt anlegen + Service-Account erstellen
2. `firebase init` ausfuehren und auf dieses Verzeichnis zeigen
3. C#-`BattleEngine`-Logik in TypeScript portieren (`src/battle/engine.ts`)
4. Tests fuer jede Function (Jest + firebase-functions-test)
