---
name: BingXBot Exchange-Clients + Security Review
description: 7 Findings (3 krit): CancellationToken nicht propagiert, ContinueWith statt await, User-Data-Stream ohne Reconnect
type: project
---

Review der Exchange-Clients (REST, Public, WebSocket) und SecureStorageService am 07.04.2026.

**Kritisch (3):**
1. `_httpClient.SendAsync(request)` ohne CancellationToken (Zeile 188) — EmergencyStop blockiert bis zu 90s
2. Alle Trading-Methoden nutzen CancellationToken.None Ueberladung — kein Abbruch moeglich
3. PlaceTpLimitOrderAsync nutzt ContinueWith statt await — AggregateException, kein TaskScheduler

**Verbesserung (3):**
4. User-Data-Stream (WebSocket) hat keinen Auto-Reconnect — Netzwerk-Hiccup = stille Event-Verluste
5. Retry-Delay (Zeile 197) ohne CancellationToken (inkonsistent: andere Stellen nutzen ct)
6. BingXPublicClient.GetKlinesAsync bricht bei erstem Fehler die Pagination ab

**Hinweis (1):**
7. SetPositionSlTpAsync Cancel-Then-Create kann Position kurzzeitig ohne SL lassen (dokumentiert)

**Was gut war:**
- HMAC-SHA256 Signatur korrekt (roher QueryString, URL-Encoding separat, recvWindow=5000)
- FlexibleStringConverter fuer BingX API-Inkonsistenzen
- SecureStorageService: DPAPI/AES-256-CBC+PBKDF2, Legacy-Migration, chmod 600

**Why:** Trading-Bot mit echtem Geld. CancellationToken-Propagierung ist kritisch fuer EmergencyStop.
**How to apply:** Bei naechstem Exchange-Code-Review speziell auf CT-Propagierung und Reconnect achten.
