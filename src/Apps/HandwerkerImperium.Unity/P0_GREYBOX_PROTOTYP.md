# P0 — Greybox-Prototyp-Spec (Go/No-Go Fun-Check)

> Konkreter, **buildbarer** erster Schritt der 3D-Idle-Neuausrichtung ([3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md), Phase P0).
> **Zweck:** beweisen, dass der Kern-Loop **ohne jede Grafik** Spaß macht. Reine Würfel/Capsule-Primitives.
> **Wenn der Greybox-Loop nicht zieht, baut man keine Assets** — dann werden die Loop-Parameter iteriert.
> Arbeitsgrundlage: die gesetzten Defaults aus [GDD §16](3D_IDLE_GAME_PLAN.md).

---

## 1. Ziel & Leitfrage

**Eine** Leitfrage entscheidet über Go/No-Go:
> *Macht „laufen → einsammeln → upgraden → Arbeiter anstellen → freischalten" als nackter Würfel-Loop süchtig genug,
> dass ein Testspieler ungefragt „nur noch eins" denkt — und der erste angestellte Arbeiter sich wie ein Befreiungsschlag anfühlt?*

Kein Polish, keine Story, kein Audio, keine Monetarisierung. Nur der Loop.

---

## 2. Scope

### Drin (das Minimum für den Loop)
- **1 Hof** (flache Plane), **3 Produktionsstationen** (farbige Würfel: Schreiner/Klempner/Elektriker).
- **Avatar** = Capsule mit `CharacterController`, Joystick-Steuerung (New Input System), Follow-Cam (Cinemachine).
- **Produktion → Carry → Abgabe → Cash → Pickup:** Stationen erzeugen Waren-Würfel (Stapel), Avatar trägt sie (Carry-Stack über Kopf), lädt am **Tresen** ab, **Cash-Würfel** spawnen, Avatar sammelt per **Auto-Pickup-Radius**.
- **3 Upgrade-Pads** (Hold-to-Pay, rampende Rate): Stations-Tempo · Sammelradius · Trag-Kapazität.
- **2 Worker-Hire-Pads:** stellt je eine **NPC-Capsule** an, die das Tragen einer Station übernimmt (= Automatisierung).
- **1 Plot-Unlock** (Bauzaun): schaltet die **4. Station** frei (Hold-to-Pay).
- **Offline-Earnings:** Beim Re-Start „Während du weg warst"-Dialog (Betrag aus Automatisierungsgrad × Zeit, gedeckelt).

### Raus (bewusst nicht in P0)
Story/Hans, Audio, Stadt-Distrikte & Restaurierung, Stern-Rating, Prestige/Franchise, Material-/Versorgungs-Schicht,
Master-Tools, Mini-Game-Boosts, Monetarisierung/Ads/IAP, finale Assets, Lokalisierung, Save-Verschlüsselung/HMAC.
(Alle kommen erst ab P1 — siehe GDD §14.)

---

## 3. Minimale Systeme (Game-Layer)

Alle im `HandwerkerImperium.Game`-Assembly, Domain bleibt Unity-frei. DI via VContainer, Async via UniTask.

| System | Verantwortung (P0-Minimum) |
|--------|----------------------------|
| `AvatarController` | CharacterController + Joystick-Move, Carry-Stack-Visual (gestapelte Würfel skalieren mit Menge) |
| `InteractionTriggerSystem` | Annäherungs-Trigger: Station (Aufnehmen), Tresen (Abgeben), Cash (Pickup), Pad (Hold) |
| `StationService` | pro Station: Produktionsintervall → Waren-Stapel (Cap), Verkaufswert je Ware |
| `EconomyService` | Cash-Spawn (GPU-instanzierte Würfel), Auto-Collect-Radius, Geldstand |
| `WorkerAutomationService` | Hire → NavMesh-light-NPC trägt Station→Tresen; ersetzt die Spielerlauferei |
| `UpgradePadService` | Hold-to-Pay mit rampender Ausgaberate, Kostenkurve (geometrisch) |
| `PlotUnlockService` | Bauzaun → Station 4 aktivieren bei Bezahlung |
| `OfflineProgressService` | Rückkehr-Verdienst — **`OfflineProgressFormulas` aus dem Domain-Port wiederverwenden** (Staffel 0.80/0.35/0.15/0.05) |

**Bewusst simpel:** kein Save-HMAC (PlayerPrefs/JSON reicht für P0), keine Addressables (Primitives), keine Lokalisierung.

---

## 4. Tuning-Knöpfe (ein `BalancingConfig`-ScriptableObject)

Alle Spaß-relevanten Zahlen an **einer** Stelle, damit der Loop in Minuten iterierbar ist — **nicht** hardcoden:

- `walkSpeed`, `collectRadius`, `carryCapacity`
- pro Station: `produceInterval`, `stackCap`, `sellValue`
- `upgradeCostBase`, `upgradeCostGrowth` (geometrisch), `upgradeStep` (Effekt je Stufe)
- `workerHireCost`, `workerCarrySpeed`
- `plotUnlockCost`
- `offlineCapSeconds`, `offlineRatePerWorker`

Start-Tuning grob: erste Station amortisiert in ~10–20 s, erstes Upgrade in <30 s erreichbar, erster Worker in ~3–5 min.

---

## 5. Szene & Tech

- **Eine** Unity-Szene `P0_Greybox.unity`, URP-Default-Renderer, ein Directional Light, graue Plane.
- **Kamera:** Cinemachine 3rd-Person-Follow, ~50° Neigung, fixer Zoom (Pinch erst ab P1).
- **Input:** New Input System, On-Screen-Stick (uGUI) + Tastatur-Fallback (WASD) für den Editor.
- **NPC-Pfade:** simpler NavMesh oder lineare Lerp-Pfade Station↔Tresen (P0 braucht keine echte Navigation).
- **Plattform-Check:** ein Android-Greybox-Build auf einem Low-End-Testgerät (FPS messen).

---

## 6. Erfolgskriterien (Go/No-Go)

**Go (alle müssen erfüllt sein):**
1. Ein neuer Testspieler versteht den Loop in **< 60 s ohne Erklärung**.
2. „Nur noch ein Upgrade"-Sog ist in einer **5-Min-Session** spürbar (selbstbeobachtet + 2–3 Testspieler).
3. Der **erste angestellte Arbeiter** erzeugt ein klares Entlastungs-/Aha-Gefühl.
4. **Offline-Rückkehr** fühlt sich belohnend an (Dialog + sichtbarer Sprung).
5. **> 30 FPS** im Greybox-Build auf Low-End-Android.

**No-Go → Konsequenz:** Loop-Parameter (`BalancingConfig`) iterieren, Pad-Layout/Abstände ändern, ggf. Carry-/Collect-Feel
nachschärfen — **erst** wenn der nackte Loop trägt, startet die Asset-Pipeline. (Der Asset-Pilot aus ASSETS_AI.md
kann ressourcen-unabhängig parallel laufen, ist aber **nicht** Go-Voraussetzung.)

---

## 7. Tests

- **EditMode (NUnit):** `OfflineProgressFormulas`-Werte gegen das Avalonia-Original (Staffelung/Cap) — Pflicht, da wiederverwendet.
- **PlayMode-Smoke:** Avatar bewegt sich; Ware wird produziert/getragen/abgegeben; Cash spawnt & wird eingesammelt;
  Upgrade senkt `produceInterval`; Worker automatisiert eine Station; Offline-Rechnung korrekt nach simulierter Pause.
- **Perf:** Frame-Time-Sample im Greybox-Build (Android), Ziel > 30 FPS.

---

## 8. Aufwand & Abgrenzung

- **~1–2 Wochen, 1 Entwickler**, keine Asset-Pipeline nötig (Primitives).
- Liefert die **Architektur-Skelette** der Game-Layer-Systeme, auf denen P1 (Vertical Slice, GDD §14) aufbaut —
  dieselben Services, dann mit echten Stationen/Stadt/Worker-Visuals, Stern-Rating, 1 Prestige und Kern-Monetarisierung.
- **Save:** P0 nutzt simples JSON; das **schlanke Genre-Save-Schema + HMAC** (GDD §12, CLAUDE.md §7) kommt in P1.

---

## Verweise
- Spiel-Design (verbindlich): [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) (Loop §3, Systeme §6, Roadmap §14, Defaults §16)
- Tech-Conventions: [CLAUDE.md](CLAUDE.md) · Tech-Architektur: [ARCHITECTURE.md](ARCHITECTURE.md)
- Wiederverwendbare Formeln: [DOMAIN_3D_PLAN.md](DOMAIN_3D_PLAN.md) (`*Formulas.cs`) · Asset-Pilot (parallel): [ASSETS_AI.md](ASSETS_AI.md)
