# ArcaneKingdom — Spielablauf & Implementierungsstand (Code-verifiziert)

> **Stand:** 2026-05-31 · **Engine:** Unity 6 (6000.4.8f1) · **Plattform:** Android (Pre-MVP)
> **Quelle:** vollständige Lektüre des echten C#-Codes unter `Unity/Assets/_Project/Scripts/{Bootstrap,Core,Domain,Game,UI}`, der JSON-Daten unter `Resources/Data/`, `Resources/Localization/strings.csv` und der `Packages/manifest.json`.
>
> **Was dieses Dokument ist:** Es beschreibt, wie ArcaneKingdom **tatsächlich abläuft** — vom App-Start bis ins Endgame — und verknüpft die **Spielerperspektive** (was der Spieler erlebt) mit der **technischen Umsetzung** (welche Klasse/Methode/Datei, welche Werte). Es ist die **faktische Ergänzung** zum [DESIGN.md](DESIGN.md) (GDD v6.0). Wo das GDD den *Soll*-Zustand beschreibt, hält dieses Dokument den *Ist*-Zustand fest. Jede Zahl stammt aus dem Code/JSON, nicht aus dem GDD.

---

## 0. Die zentrale Wahrheit vorab: zwei Schichten

ArcaneKingdom besteht aus **zwei klar getrennten Realitäten**, und dieser Unterschied zieht sich durch jedes System:

1. **Eine reiche, saubere, getestete Domain-Schicht** — die eigentlichen Spielregeln (Kampf-Engine, Fusion, Prestige, Login, Sternkarten, Arena-Ligen, Dieb-Belohnungen, Quests, Achievements, Saison-Pass, Pack-Rolling) sind als UnityEngine-freie, deterministische C#-Logik **fertig implementiert** und laut CLAUDE.md mit ~165 Test-Cases abgedeckt.
2. **Eine lückenhaft verdrahtete Anbindungs-Schicht** — ein großer Teil dieser Logik ist **nicht an den Spielfluss angeschlossen**: Tutorial, Helden-Passivs im Kampf, Quest-/Achievement-Fortschritt, Shop-Käufe, Material-Drops, die Kampfwirkung der Prestige-Stufe, der gesamte Online-/Social-Layer, Firebase, Photon und IAP existieren als Logik, sind im laufenden Spiel aber **wirkungslos**.

Diese Doppel-Realität ist kein Bug, sondern der natürliche Pre-MVP-Zustand: Erst wurden die Regeln gebaut und getestet, die Verdrahtung folgt. Dieses Dokument markiert **jeden** Baustein eindeutig.

### Status-Legende (durchgängig verwendet)

| Marker | Bedeutung |
|--------|-----------|
| **[LIVE]** | Real implementiert UND im Spielfluss wirksam — der Spieler erlebt es. |
| **[DOMAIN-ONLY]** | Logik vollständig und testbar, aber nicht an UI/Gameplay angebunden → im Spiel **wirkungslos**. |
| **[SKELETT]** | Stub/Platzhalter/Mock — zeigt z.B. nur einen Toast, tut nichts Echtes. |
| **[NUR DATEN]** | JSON + Editor-Validierung vorhanden, keine Runtime-Nutzung. |
| **[FEHLT]** | Nicht vorhanden. |

---

## 1. Der reale spielbare Kern (Executive Summary)

**Was man heute spielen kann:** Konto anlegen → Rasse wählen → Starter-Karten erhalten → im Hub navigieren → auf der Welt-Karte einen Node und eine Schwierigkeit wählen → einen **vollständigen, manuellen, deterministischen Kartenkampf** austragen → Gold/EXP/Sterne kassieren → bei Welt-Bossen eine Story-Cutscene sehen → Karten leveln, Decks bauen, Karten fusionieren, Welten prestige-aufwerten, passives Tagesgold und die tägliche Login-Belohnung abholen.

### 1.1 Die "lebende Schleife"

```
App-Start (Boot.unity)
  → Splash  → [Registration | Login/Auto-SignIn]  → Rassenwahl  → HUB
                                                                    │
        ┌───────────────────────────────────────────────────────────┤
        │                                                            │
   WorldMap ──► DifficultyPicker ──► BATTLE (manuell) ──► BattleReport ──► (Welt-Boss: Memory-Fragment)
        │                                                            │
        └──► Belohnung: Gold + EXP + Sterne  ──────────────────────►┘

   HUB-Gebäude (alle [LIVE] erreichbar & funktional):
     Codex (Karten-Lexikon) · Schmiede (Fusion) · Tempel (Login-Belohnung + Sternkarten)
     · QuestCenter · Shop [SKELETT] · Guild [SKELETT] · MeritRanking · Chat [SKELETT]
   HUB-Nav: WorldMap · Arena [SKELETT] · Runen · Profil · DeckBuilder · Freunde [Anzeige LIVE]
```

### 1.2 Implementierungs-Überblick (System für System)

| System | Stand | Kernbefund |
|--------|-------|-----------|
| Boot / DI / ScreenManager | **[LIVE]** | Fail-Fast, UIRoot-Warten, sauberes Screen-Routing |
| Splash / Onboarding | **[LIVE]** | Splash + Registration + Auto-SignIn + Rassenwahl |
| Tutorial | **[DOMAIN-ONLY]** | 8 Schritte definiert, aber **nie getriggert** |
| Hub-Navigation | **[LIVE]** | Eintritts-Pipeline (Claims/Income/Energie), 17 Nav-Ziele |
| Kampf-Engine | **[LIVE]** | Vollständig, deterministisch, manuell gespielt |
| Helden-Passivs (im Kampf) | **[DOMAIN-ONLY]** | Logik da, aber im Kampf **nicht verdrahtet** → wirkungslos |
| Auto-Battle | **[DOMAIN-ONLY]** | Freischalt-Kurve da, kein Auto-Modus im Battle-Screen |
| Karten-Level / Deckbau / Codex / Fusion | **[LIVE]** | Funktional (mit kleinen UI-Lücken, s.u.) |
| Welt-Karte / Schwierigkeit / Settlement | **[LIVE]** | Gold/EXP/Sterne real vergeben |
| Prestige-Aufwertung + Idle-Income | **[LIVE]** | Vollständig, gehärtet |
| Prestige-Kampfwirkung / Material-Drops | **[DOMAIN-ONLY]** | Logik da, im Kampf **nie aufgerufen** |
| Login-Belohnung (Tempel, 30 Tage) | **[LIVE]** | (Karten-Items aber noch Pending/Müll, s.u.) |
| Sternkarten-Tempel | **[LIVE/Teil]** | Abbuchung real, Belohnungsausgabe (außer Mythic-Fragment) **TODO** |
| Quests / Achievements | **[DOMAIN-ONLY]** | Trigger-Hooks **nirgends aufgerufen** → kein Fortschritt |
| Shop / IAP / DailyShop / Saison-Pass / Events | **[SKELETT]** | UI zeigt Mock, Services ohne Aufrufer |
| Arena / Gilden / Klan-Welt / Chat / Dieb / Merit | **[SKELETT]** | Domain-Logik real, UI = Mock, kein Netzwerk |
| Story / Memory-Fragmente | **[LIVE]** | Fragment-Cutscene bei Welt-Boss-Sieg; Enden (W10) **[FEHLT]** |
| Save-System (lokales JSON v4) | **[LIVE]** | Atomic-Write, Backup, Migrator, hochwertig |
| Firebase / Photon / Cloud | **[FEHLT]** | Kein SDK installiert, nur Namens-Präfix + TODOs |

---

# TEIL I — Der Spielablauf von A bis Z

## 2. App-Start & Boot-Kette  **[LIVE]**

### Spielerperspektive
Der Spieler tippt das App-Icon an. Nach dem Unity-Splash erscheint ohne schwarzen Zwischen-Bildschirm der erste Spiel-Screen.

### Technische Umsetzung
- **Boot-Scene:** `Assets/_Project/Scenes/Boot/Boot.unity` mit einem `[Bootstrapper]`-GameObject, an dem `RootLifetimeScope` (VContainer-`LifetimeScope`, `DontDestroyOnLoad`) hängt.
- **`RootLifetimeScope.Configure()`** (`Bootstrap/RootLifetimeScope.cs:23-51`):
  1. Prüft drei Pflicht-`SerializeField`-Slots — `balancingConfig`, `audioService`, `uiRoot`. Fehlt einer, wird **laut** eine `InvalidOperationException` geworfen (Fail-Fast statt späterem NullRef, `:28-36`).
  2. `RegisterInstance(balancingConfig)`, `RegisterComponent(audioService).AsImplementedInterfaces()`, `RegisterComponent(uiRoot)`.
  3. `GameInstaller.RegisterServices(builder)` — ~50 Singleton-Services.
  4. `UIBootstrap.RegisterAllScreens(builder)` — ScreenManager + ~31 Screens.
  5. `RegisterEntryPoint<BootEntryPoint>()`.
- **`BootEntryPoint.StartAsync()`** (`Bootstrap/BootEntryPoint.cs:40-68`, VContainer `IAsyncStartable`):
  1. Bindet UI-Globals **vor** der ersten View: `CardTileFactory.ArtworkService` + `.LocalizationService` (`:43-44`).
  2. **Wartet aktiv auf `UIRoot.IsReady`**, max. 200 Frames (`:50-58`). Grund (dokumentiert): Wird ein Screen vor fertiger UI-Wurzel gepusht, lösen UXML-`var()`-Theme-Variablen gegen einen leeren StyleSheet-Tree auf → `NullReferenceException` im `StyleVariableResolver`. (Das ist exakt die in der CLAUDE.md beschriebene Inline-`var()`-Crash-Klasse.)
  3. Pusht den Initial-Screen: `ScreenId.Splash` (Fallback `Login`) via `ScreenManager.ReplaceAsync`.

### ScreenManager — die Navigations-Mechanik (`UI/Foundation/ScreenManager.cs`)
Ein zentraler UI-Router über **einem** `VisualElement`-Root (`UIRoot.ScreenContainer`):
- Datenstrukturen: `Stack<IScreen>`, `Dictionary<string,IScreen> _builtCache`, `CancellationTokenSource`, `bool _busy`.
- `PushAsync(id)` legt einen Screen oben drauf (versteckt den vorigen nur, wenn der neue **kein Overlay** ist), ruft `OnEnterAsync`.
- `PopAsync()` entfernt den obersten (ignoriert bei ≤1), macht den vorigen wieder sichtbar.
- `ReplaceAsync(id)` **leert den kompletten Stack** und pusht nur den neuen — die Standardoperation für Boot-Übergänge.
- `GetOrBuild(id)` cached gebaute Screens; Erzeugung via `VContainerScreenFactory.Create(id)` (Screens sind **Transient** registriert, der Cache macht sie effektiv einmalig).
- Sichtbarkeit = `display: Flex/None` + CSS-Klasse `ak-screen--hidden`. **Keine echte Fade-Transition** — nur Display-Toggle.
- Re-Entrancy-Bremse `_busy` (max. 500 ms Warten). Deshalb starten `LoginScreen`/`SplashScreen` ihre Folgeschritte **detached** (`.Forget()`), um keinen Busy-Deadlock auszulösen.

---

## 3. Splash  **[LIVE]** (Laden simuliert)

### Spielerperspektive
Erster Screen: zentriertes Logo, das im 2-Sekunden-Takt pulsiert (1 s an / 1 s aus), ein Ladebalken mit wechselndem Status-Text und ein Versions-Label (`v{Application.version}`). Der Splash bleibt **mindestens 3 Sekunden** sichtbar (Marken-Erlebnis).

Status-Text-Phasen: „Vorbereitung…" (0 %) → „Lade Karten-Daten…" (10→25 %) → „Prüfe Account…" (40 %) → „Verbinde mit Server…" (65→85 %) → „Bereit" (100 %).

### Technische Umsetzung (`UI/Splash/SplashScreen.cs`)
- `MinSplashMillis = 3000`. Phasen-Delays simuliert (`UniTask.Delay` 400/300/400/400/300 ms) — **kein** echtes Asset-Preload, **kein** echter Server-Call.
- **Account-Weiche** (`:84-117`): liest direkt `PlayerPrefs.GetString("last_user_email")` + `"auth_token"`.
  - kein Token → `ReplaceAsync(Registration)` (Fallback `Login`).
  - Token vorhanden → `ReplaceAsync(Login)`.

---

## 4. Onboarding: Registration / Login / Auto-SignIn  **[LIVE]** (lokal)

### 4.1 Erststart → Registrierung (`UI/Registration/RegistrationScreen.cs`)
**Spielerperspektive:** Eingabe von Spielername, E-Mail + Bestätigung, Passwort + Bestätigung (maskiert) sowie drei Pflicht-Checkboxen (AGB, Datenschutz, Verhaltensregeln). Bei Erfolg: Toast „Konto erstellt!", direkt zur Rassenwahl.

**Validierungsregeln (echt, `:127-180`):**
- Spielername 3–20 Zeichen.
- E-Mail gültiges Format (`System.Net.Mail.MailAddress`), Confirm muss übereinstimmen.
- Passwort: **min. 8 Zeichen, mind. 1 Groß-, 1 Klein-, 1 Ziffer**; darf E-Mail/Spielername nicht enthalten; **keine 3-fache Zeichen-Wiederholung** („aaa" verboten); Confirm muss übereinstimmen.
- Alle 3 Checkboxen Pflicht.
- Bei Erfolg: `last_user_email`/`auth_token` in PlayerPrefs, Save, dann `ReplaceAsync(RaceSelection)`.

### 4.2 Wiederkehr → Login (`UI/Login/LoginScreen.cs` + `Game/Login/LoginController.cs`)
**Spielerperspektive:** Reiner Status-/Progress-Screen — **kein Eingabefeld** (UXML hat keine TextFields). Logo pulsiert, Ladebalken: „Verbinde mit Server…" (20 %) → „Lade Spielerdaten…" (55 %) → „Validiere…" (80 %) → „Bereit!" (100 %). Bei Fehler: Retry-Button.

**Technik:** `RunLoginAsync` läuft **detached**. Es werden **keine** E-Mail/Passwort übergeben → der `LoginController` fällt immer auf `SignInAnonymouslyAsync` (anonymer Auto-SignIn) zurück. Stages: `Authenticating → LoadingSave → Validating → Ready` (`LoginStage`-Enum). Bei `Validating` läuft `QuestService.RestoreFromSave` (idempotent). Nach Erfolg 400 ms Pause, dann Hub — oder, falls Erststart-Heuristik greift, Rassenwahl.

**First-Time-Heuristik** (`LoginScreen.cs:109-113`): `save.Story.UnlockedMemoryFragments.Count == 0 && save.CardInventory.Count == 0` → Rassenwahl.

### 4.3 Wichtige Onboarding-Befunde
- **„Firebase"-Auth ist lokal** (`FirebaseAuthService` → PlayerPrefs, Mock-Token `local-token-{Guid}`). Login prüft **kein** Passwort serverseitig.
- **Anonymer Spieler ohne Registrierung wird beim nächsten Start erneut zur Registrierung geschickt** — der Auto-SignIn setzt `auth_token` nie (nur der E-Mail-Pfad und die Registrierung tun das). Folge-Inkonsistenz zwischen Splash-Weiche und Auto-SignIn.

---

## 5. Rassenwahl & Starter-Deck  **[LIVE]**

### Spielerperspektive (`UI/RaceSelection/RaceSelectionScreen.cs`)
Vier wählbare Rassen-Karten mit Helden-Portrait: **Ritter** (Mentor Marschall Aldor), **Elfen** (Mondpriesterin Lira), **Tiergeister** (Grimmfang), **Dämonen** (Lilith). **Götter sind nicht wählbar** (Premium-Rasse, nur per Fusion). Auswahl zeigt Rassen-Name + Passiv-Beschreibung + Mentor. „Bestätigen" speichert die Wahl, vergibt das Starter-Deck, Toast, dann Hub.

### Technik
- `ConfirmAsync` mutiert `s.Story.ChosenRace`.
- **Starter-Deck** (`:41`, idempotent nur wenn `CardInventory.Count == 0`): drei feste Karten als `CardInstance` (Level 0):

| Karten-ID | Element | ATK | HP | turnsToSpecial |
|-----------|---------|-----|----|----|
| `wachsoldat` | Erde | 140 | 680 | 3 |
| `novizen_bogenschuetzin` | Natur | 190 | 520 | 2 |
| `lehrling_magier` | Feuer | 180 | 490 | 2 |

### Zwei dokumentierte Abweichungen
- **Starter-Karten sind nicht rassenspezifisch** — alle drei sind Rasse **Ritter** und werden unabhängig von der gewählten Rasse vergeben.
- **Starter-Karten landen im Inventar, nicht im Deck.** Der `PlayerSave`-Ctor legt ein leeres „Deck 1" an; die drei Karten werden ihm **nicht** zugewiesen. Ein neuer Spieler muss vor dem ersten Kampf manuell ein Deck bauen.

### Fresh-Player-Startwerte (verifiziert)
Level 1, ExpTotal 0 · **0 Gold, 0 Diamant, 0 Energie**, alle Scraps 0 · Energie-Cap 60, Regen 1/6 min · 1 leeres Deck · Default-Server „Poseidon".

---

## 6. Tutorial  **[DOMAIN-ONLY]** — findet nie statt

`TutorialService`, `TutorialOverlay` und `tutorial.json` (8 Schritte) sind vollständig vorhanden und im DI registriert, werden aber **nirgends aufgerufen** (verifiziert: kein einziger `OnEvent(...)`-Aufruf, kein `TutorialOverlay`-Push). Die Onboarding-Tooltips erscheinen für den Spieler faktisch nie.

**Die 8 geplanten Schritte** (`tutorial.json`, nur `welcome` ist nicht überspringbar):

| # | id | Trigger-Event | Highlight |
|---|----|----|----|
| 1 | welcome | first_session_start | — |
| 2 | hub | hub_entered | nav_world_map_button |
| 3 | first_battle | battle_started | hand_first_card |
| 4 | deck_edit | first_battle_won | deck_tab |
| 5 | first_pack | shop_entered | shop_common_pack |
| 6 | collection | material_card_obtained | deck_collection_tab |
| 7 | arena | level_15_reached | nav_arena_button |
| 8 | guild | level_25_reached | menu_guild_button |

---

## 7. Der Hub — zentrale Gebäude-Welt  **[LIVE]**

### Spielerperspektive (`UI/Hub/HubScreen.cs`)
Der Hub ist eine **Gebäude-Stadt** (Vollbild-Hintergrund `hub_main.png`), kein Tab-Layout. Layout von oben nach unten:

1. **Top-Bar:** runder Avatar mit Gold-Ring + Level-Badge, Spielername, Gilden-Tag `[ABCDE]`, „LV n"-Badge, Arena-Badge (dauerhaft ausgeblendet), Energie-Leiste „n/60", Gold- und Diamant-Pills.
2. **Energie-Leiste** (gold umrandet).
3. **Event-Banner** (statisch „Willkommen zurück, Held!" — kein dynamischer Event-Text gebunden).
4. **Gebäude-Grid (8 Gebäude):** Karten-Turm→**Codex**, Zauberschmiede→**Schmiede**, Bibliothek→**QuestCenter**, **Tempel**, Gilden-Hafen→**Guild**, Marktplatz→**Shop**, Wand der Ehre→**MeritRanking**, Postamt→**ChatOverlay**.
5. **Right-Nav:** Landkarte→**WorldMap**, **Arena**, Zauber→**Runen**, **Profil**.
6. **Bottom-Nav:** Menü→**Settings**, Laden→**Shop**, **Hub** (aktiv), Deck→**DeckBuilder**, **Freunde**.

Alle 8 Gebäude + 4 Right-Nav + 4 Bottom-Nav-Ziele sind registriert und erreichbar. Postamt→Chat ist ein **Overlay** (Hub bleibt sichtbar), alle anderen sind Voll-Screens.

### Eintritts-Pipeline (`OnEnterAsync`, automatisch beim Hub-Eintritt)
1. `_save.LoadAsync` → `QuestService.RestoreFromSave`.
2. **PendingClaims einlösen** (`RedeemPendingClaimsAsync`, atomar): Currency, Scrap, Card, Rune, FeatureUnlock, RuneSlotUnlock, Title, AvatarFrame. Pack-Claims bleiben liegen (Öffnen im Shop). Bei ≥1 eingelöst: Toast „n Belohnung(en) eingelöst".
3. **Daily-Income-Tick** (`PrestigeAppService.TickDailyIncomeAsync`) → bei >0 Toast „Passives Einkommen: +X Gold".
4. **Energie-Regeneration** (`HubController.RegenerateEnergyAsync`).
5. Erneuter Load → `RefreshHeader()`.
6. `QuestService.FlushAsync`.

### Technische Details
- **`HubController`** ist explizit als „SKELETT" markiert — nur `RegenerateEnergyAsync` ist echt; `OpenWorldMapAsync`/`OpenShopAsync` sind TODO-Stubs, werden aber **gar nicht genutzt** (Navigation läuft direkt über den `ScreenManager`).
- **`HubCityRenderer`** (programmatischer 2×4-Renderer) ist **Legacy/ungenutzt** und hat eine abweichende Gebäudeliste.
- Zahlen-Formatierung (`FormatNumber`): de-DE Tausenderpunkt, ab 100 Mio „X,X Mio", ab 1 Mrd „X,XX Mrd".

---

## 8. Welt-Karte (WorldMap) & Node-Auswahl  **[LIVE]**

### Spielerperspektive (`UI/WorldMap/WorldMapScreen.cs`)
Oben Zurück-Button + Gesamt-Sterne „x / y" (max **400** = 10 Welten × 10 Nodes × 4 Sterne). Darunter eine horizontale Reihe **Welt-Tabs** (W1–W10, je mit „Lv X+"-Badge, element-getöntem Hintergrund). Welt-Auswahl zeigt Name + Element + empf. Stufe (+ ggf. „Prestige X") und ein **Node-Grid** (10 Kacheln). Gesperrte Nodes sind ausgegraut. Jede Kachel zeigt Node-Marker (normal/miniboss/worldboss), Index und eine 4er-Stern-Reihe (Sprites, **keine** ★-Glyphen wegen Android-Tofu). Klick öffnet ein Detail-Panel: Name, Typ, Energie-Kosten, aktuelle Sterne, Belohnungstabelle (1★–4★ Gold+EXP), „Kampf starten"-Button. Wenn alle Nodes ≥3★: zusätzlich „Aufwerten zu Prestige X"-Button.

### Freischalt-Logik
- Node 1 immer frei; sonst muss der vorherige Node **≥1 Stern** haben.
- **Kein Spieler-Level-Gate:** `recommendedPlayerLevel` ist rein informativ (Tab-Badge). Welten werden **nicht** über das Spieler-Level gesperrt — nur über die Sterne-Kette.

### Technik-Hinweise
- Programmatisch gestylt (inline Maße/Farben), noch nicht voll auf das Arcane-Realm-Theme umgestellt.
- Detail-Panel zeigt Energie über die **Legacy-NodeType**-Kosten (1/2/3), während der Kampf nach **Difficulty** abrechnet — bei Gott-Stufe kann die Anzeige (1) von den realen Kosten (3) abweichen.
- `StartBattleWithDifficulty` legt Node + Difficulty in `ModalContext` (`"battle_node"`/`"battle_difficulty"`) und pusht `ScreenId.Battle`.

---

## 9. Schwierigkeits-Wahl (DifficultyPicker)  **[LIVE]**

### Spielerperspektive (`UI/Modals/DifficultyPickerModal.cs`, Overlay)
Node-Name oben, Bestwert-Anzeige („Bestwert: N/4 Sterne"), vier Buttons mit je Sternzahl, Energie-Kosten und Gold-Belohnung. Buttons mit zu hohen Energie-Kosten sind deaktiviert. Auswahl startet den Kampf; Backdrop/X schließt.

### Die vier Stufen (`Domain/World/NodeDifficulty.cs`)

| Stufe | enum | Sterne | Energie | Gegner-Stat-Mult. | Spezial-Skills | Boss-Phasen |
|-------|------|--------|---------|-------------------|----------------|-------------|
| Classic | 1 | 1 | 1 | ×1.00 | nein | nein |
| Amateur | 2 | 2 | 1 | ×1.25 | nein | nein |
| Profi | 3 | 3 | 2 | ×1.60 | (`ActivatesEnemySpecialSkills`, aber nicht abgefragt) | nein |
| Gott | 4 | 4 | 3 | ×2.20 | — | **ja** (nur Node 5/10) |

Der enum-Wert IST direkt die Sternzahl. **Hinweis:** GDD Kap. 4.4 sagt für Amateur „+50 % HP" — der Code nutzt **×1.25 (+25 %)** auf ATK **und** HP.

---

## 10. Der Kampf — die Krone des Spiels  **[LIVE]**

Die Kampflogik ist dreischichtig getrennt:

| Schicht | Datei | Rolle |
|---------|-------|-------|
| **Domain (reine Regeln)** | `Domain/Battle/BattleEngine.cs` | Vollständige, UnityEngine-freie, deterministische Engine. Hier liegt die echte Mechanik. |
| **Game** | `Game/Battle/BattleBootstrap.cs`, `BattleAI.cs` | Baut die Engine aus `PlayerSave`/`Node`, liefert die KI. (`BattleController` ist ein **[SKELETT]**, wird nicht genutzt.) |
| **UI (realer Pfad)** | `UI/Battle/BattleScreen.cs` | Treibt die Engine durch Drag&Drop, rendert HUD/Felder/Hand, zeigt Events als Toasts. |

> **Wichtig:** Der echte Kampf läuft über `BattleScreen` + `BattleBootstrap` + `BattleEngine`. Der DI-registrierte `BattleController` ist toter Code — er würde jeden Kampf sofort als Sieg werten (`state.Result = PlayerWins;`, Stub).

### 10.1 Mana-System — was der Code WIRKLICH macht

> **Dies ist die zentral richtigzustellende Mechanik.** Das GDD (Kap. 10.1) sagt „Start-Mana 3 … Mana regeneriert +1 pro Runde bis max. 10". **Der Code macht das NICHT.**

- **Mana ist konstant 3 pro Runde, kein Anstieg, kein Cap 10.** Start `PlayerMana = PlayerMaxMana = 3` (`BattleState.cs:108-111`); in `EndTurn` wird Mana auf den Max-Wert **zurückgesetzt**, nicht inkrementiert (`BattleEngine.cs:230/237`).
- **Jede Karte kostet flat 1 Mana** (`ManaPerCard = 1`, `:26`), unabhängig vom COST. Pro Runde also ~3 Karten spielbar.
- **Waldläufer** (Elfen-Passiv): die erste Karte einer Runde kostet 0 Mana.

### 10.2 COST — die zwei echten Rollen (kein Mana-Preis)
COST ist **nicht** der Mana-Preis. Im Code hat COST zwei Funktionen:
1. **Deck-Bau-Budget:** Summe aller Karten-COST ≤ **200** (`DeckValidator.MaxDeckCost = 200`).
2. **Schwere-Karten-Gate im Kampf:** Karten mit **COST > 30** (`HeavyCardCostThreshold = 30`) dürfen nur eingesetzt werden, wenn diese Runde noch nichts gespielt wurde (`cardsPlayedThisTurn == 0`). Die KI respektiert dasselbe Gate.

COST-Wertebereich im Datenmodell 1–60 (`[Range(1,60)]`), in den echten Daten 4–50.

### 10.3 Runden-Ablauf (echte Engine-Reihenfolge)
**Setup** (`BattleEngine.cs:52-70`): Decks übernehmen → Rudelbund-Vorberechnung → **deterministisch mischen** (Fisher-Yates + `DeterministicRng`) → **je 4 Karten** Start-Hand ziehen → Phase `PlayerTurn`.

**`EndTurn()`** wickelt **einen** Halbzug ab (die „Phase"-Seite ist Angreifer):
1. **DoT-Tick** vor der Attacke (Poison/Burning → HP abziehen), Tod via `ResolveDeathAt` (mit Göttlicher-Segen-Rettung + OnDeath-Event).
2. **Attack-Phase** je Feld-Karte: geblockte Karten (Sleep/Frozen/Stunned) überspringen; sonst greift die Karte die **vorderste** Verteidiger-Karte an (oder direkt den Helden, wenn das Feld leer ist); Element-Multiplikator anwenden; Lifesteal (Dämonen); Spezial-Timer −1, bei 0 → `TriggerSpecial` + Reset.
3. **Boss-Phase-2-Trigger** (nur bei Boss-Encounter, nach Spieler-Zug, Gegner-Held < 50 %).
4. `CurrentTurn++`; Status-Dauer auf beiden Feldern ticken; Waldläufer-Reset.
5. **Phasenwechsel:** Mana der nun ziehenden Seite auf Max zurücksetzen, `CardsPlayedThisTurn = 0`, **1 Karte ziehen**.
6. **Sieg-Check**.

**UI-Treiber** (`BattleScreen.OnEndTurnAsync`): `EndTurn()` (Spieler) → 500 ms Pause → KI wählt Karten (`BattleAI.ChooseCardsToPlay`, 280 ms je Karte) → `EndTurn()` (Gegner) → Game-Over-Check.

**Hand/Feld-Limits:** `MaxFieldSlots = 5`, `MaxHandSize = 5`, Start-Hand 4. Leeres Deck → kein Nachziehen (kein Fatigue, kein Reshuffle).

### 10.4 Schadensformel & Element-Matchup
```
dealt = (int)( attacker.CurrentAttack × ElementMatchup.GetMultiplier(attacker.Element, target.Element) )
```
- Bei leerem Verteidiger-Feld: voller Schaden direkt auf den Helden, **ohne** Element-Multiplikator.
- **Keine** `buffPercent`/`defensePercent`-Faktoren in der normalen Attacke (GDD-Formel Kap. 10.2 ist nur teilweise umgesetzt). Buffs/Defense existieren nur als separate Spezial-Skill-Effekte.

**Element-Matchup** (`ElementMatchup.cs`): Neutral **1.00x**, Stark **1.10x**, Schwach **0.90x**.
- Physisches Dreieck: Feuer→Natur→Wasser→Feuer (stark), Rückrichtung schwach.
- Magisches Dreieck: Licht→Dunkel→Erde→Licht (stark), Rückrichtung schwach.
- Cross-Dreieck (z.B. Feuer vs. Licht): neutral.
- **Hinweis:** Ein starkes Matchup löst in der normalen Attacke **keinen** automatischen Status-Effekt aus (anders als GDD Kap. 3.4 suggeriert) — Status kommt nur aus Control-Spezial-Skills.

### 10.5 Spezial-Skills (Rundenwarten)  **[LIVE, aber nur Skill 1]**
Bei Spezial-Timer 0 zündet `TriggerSpecial` — **ausschließlich `def.BaseAbility` (Skill 1)**. `SecondAbility`/`ThirdAbility`/`LastWillAbility` (Letzter Wille) existieren als Felder, werden im Kampf aber **nirgends ausgewertet**.

| Kategorie | Wirkung im Code |
|-----------|-----------------|
| **Damage** | AoE `Max(1, ATK × Magnitude/100)` auf alle Gegner (`TargetsAllEnemies`), sonst Single-Target. |
| **Defense** | Heilt den Caster `Max(1, MaxHealth × Magnitude/100)`, gedeckelt. |
| **Buff** | `TargetsAllAllies`: jedem Ally ATK +`Max(1, ATK × Magnitude/100)`. |
| **Debuff** | `TargetsAllEnemies`: ATK reduzieren (min. 1). |
| **Control** | Status aus Element ableiten, Dauer `DurationTurns>0 ? : 2`, DoT-Magnitude `Max(50, Magnitude)` für Poison/Burning. |
| **Synergy** | Wie Buff (ATK-Bonus auf alle Allies). |

Element→Status: Feuer→Burning, Wasser→Frozen, Natur→Poisoned, Erde→Stunned, Dunkel→Silence, Licht→Slowed.

### 10.6 Status-Effekte — 8 Typen, davon 5 wirksam

| Typ | Wirkung | Stand |
|-----|---------|-------|
| Sleep / Frozen / Stunned | blockt die komplette Aktion | **[LIVE]** |
| Poisoned / Burning | DoT (Schaden/Runde = Magnitude), tickt vor der Attacke | **[LIVE]** |
| Silence | sollte Skills blocken (`BlocksSkills`) | **[DOMAIN-ONLY]** — wird nie abgefragt |
| Slowed | sollte +1 Rundenwarten geben | **[DOMAIN-ONLY]** — keine Logik |
| Rooted | sollte „nicht entfernbar" sein | **[DOMAIN-ONLY]** — keine Logik |

Frozens „+30 % Schaden" (Doc-Kommentar) ist **nicht** implementiert. Kein Stacking gleichen Typs (`ApplyOrRefresh` nimmt nur längere Restdauer).

### 10.7 Helden-Passivs — vorhanden, aber im Kampf TOT  **[DOMAIN-ONLY]**

Die 5 Passivs sind in der Engine korrekt programmiert (gespeist aus `heroes.json`):

| Rasse | Typ | magnitude | Code-Wirkung |
|-------|-----|------|--------------|
| Ritter | KoeniglicheAura | 5 | +5 % HP auf eigene Karten beim Ausspielen |
| Götter | GoettlicherSegen | 1 | 1× pro Kampf eine sterbende Karte auf 1 HP retten |
| Elfen | Waldlaeufer | 0 | erste Karte jeder Runde kostet 0 Mana |
| Tiergeister | Rudelbund | 3 | +3 % ATK je Tiergeist im Deck |
| Dämonen | LebensraubAura | 20 | 20 % des Schadens heilen den Helden |

> **Kritischer Befund:** `State.PlayerHeroPassiv`/`EnemyHeroPassiv` werden **im Produktionspfad nirgends gesetzt** (`BattleBootstrap.Build` weist sie nicht zu — nur der `BattleStateSerializer` für Tests/Replay tut das). Da die Felder `null` sind, durchlaufen alle Passiv-Checks den No-Op-Pfad. **Im gespielten Kampf hat KEIN Helden-Passiv eine Wirkung**, obwohl die Logik fertig und getestet ist.

### 10.8 Synergy & Rivalry  **[LIVE]** (datenseitig kaum bestückt)
- **Synergy:** liegt ein `SynergyCardId`-Partner im eigenen Feld → beide +5 % HP + `SynergyActivated`-Event.
- **Rivalry:** liegt eine `RivalCardId` im Gegnerfeld → nur ein `RivalryClashed`-Event (reiner Flavor, keine Mechanik).
- Datenlage: nur **2** Karten mit `synergyCardIds`, **5** mit `rivalCardIds` — das System ist weitgehend unbestückt.

### 10.9 Boss-Phase 2  **[LIVE]** (eine Phase, nur Gott-Stufe)
Trigger nur bei `IsBossEncounter` (= Gott-Stufe + MiniBoss/WorldBoss), nach Spieler-Zug, Gegner-Held < 50 %, einmalig. Effekte:
1. Jede Gegner-Karte: `CurrentAttack += Max(200, ATK/2)` (also +200 oder +50 %, je größer).
2. Bis zu 3 Verstärkungs-Karten (erste 3 `enemyDeckCardIds`), skaliert mit `EnemyStatMultiplier`.
3. `BossPhaseChange`-Event → Toast „BOSS-PHASE 2".

> Abweichungen zum GDD Kap. 10.3/10.4: **keine** „1.5x-Ultimate", **keine** „AoE alle 3 Runden", **keine** Mini-/World-Boss-Unterscheidung, **keine** Mehr-Phasen (Prestige III=3/IV=4). Es gibt nur diese eine generische Phase-2, ausschließlich auf Gott-Stufe.

### 10.10 Sieg, Max-Runden
- Gegner-Held ≤ 0 → Sieg; eigener Held ≤ 0 → Niederlage; beide → Unentschieden.
- `MaxTurns = 50` → bei Erreichen entscheidet das höhere Helden-HP. **Kein** Sudden-Death/doppelter Schaden (anders als GDD Kap. 10.1).
- Helden-Start-HP: Spieler **1000**, Gegner `1000 × EnemyStatMultiplier`.

### 10.11 Determinismus / Anti-Cheat-Fundament  **[LIVE]**
- PRNG `DeterministicRng` (Mulberry32, `0x6D2B79F5`) — bit-identisch zur TS-Server-Portierung, **nicht** `System.Random`. Genutzt nur fürs Deck-Mischen; der Rest des Kampfs ist deterministisch.
- Seed-Ableitung via FNV-1a über Node-Id + Deck-Instanz-IDs (`ComputeDeterministicSeed`), Seed 0 → 1.
- `BattleStateSerializer` (Newtonsoft, Schema 1) serialisiert verlustfrei. Fundament für den serverseitigen Replay-Cross-Check steht — die Aufzeichnung/Validierung ist aber **[DOMAIN-ONLY]** (`ReplayService` ist registriert, wird im Kampf aber nie aufgerufen, kein Upload).

### 10.12 Battle-UI-Stand
- **[LIVE]:** vertikales HUD (Gegner oben / Spieler unten), HP-Bars, Felder, Hand mit **vollständigem Drag&Drop** (`CardDragManipulator` + Tap-Fallback), Mana-Orbs (gefüllt/leer), Effekt-Bursts (`Effects/effect_<element>_burst`), Floating-Text, Status-Kürzel (ASCII: `Zz/Si/Fr/St/Px/Br/Sl/Rt`), Personality-/Synergy-/Rivalry-/Boss-Toasts, Sieg→BattleReport-Übergang, Memory-Fragment bei World-Boss.
- **Lücken:** Karten-Tiles zeigen `def.Id` statt lokalisiertem Namen; Floating-**Schadenszahlen** beim Angriff fehlen (die Engine gibt keine Damage-Werte an die UI zurück, nur HP-Bar-Refresh); Status als ASCII statt Sprites; Sterne im Report als Text statt Sprite.

### 10.13 Der UI-Mana-Gating-Bug (real, dokumentiert)
Die `BattleScreen`-UI prüft die Spielbarkeit einer Handkarte gegen **`def.Cost`** statt gegen `ManaPerCard`:
- `canPlay = … && s.PlayerMana >= def.Cost` (`BattleScreen.cs:422-424`), gleiches im Drag-Lambda und im Cost-Badge.
- Da nur 3 Mana vorhanden sind, COST aber bis 50 reicht, erscheinen Epic/Legendär/Mythisch-Karten dauerhaft als **nicht spielbar (Opacity 0.5, kein Drag)** — obwohl die Engine sie für 1 Mana akzeptieren würde. Das ist der „kampfbrechende Bug", vor dem die CLAUDE.md warnt, hier in der UI-Schicht reproduziert. (Die Engine selbst ist korrekt.)

---

## 11. Schlachtbericht & Belohnungs-Settlement  **[LIVE]**

### Spielerperspektive (`UI/BattleReport/BattleReportScreen.cs`)
Banner „SIEG" (grün) / „NIEDERLAGE" (rot) / „UNENTSCHIEDEN" (gold), eine Zeile mit Sternen/Gold/EXP (oder Arena-Rang), Gegner-Box, Schlachtzeit, Buttons „Replay", „Nochmal", „Profil", „Schließen".

### Settlement (`BattleScreen.HandleGameOverAsync` → `ApplyRewardsAsync`)
- Bei Sieg: `stars = difficulty.StarsOnVictory()`, `gold = node.GoldReward(difficulty)`, `exp = node.ExpReward(difficulty)`.
- `Currencies.AddGold(gold)`, `Profile.ExpTotal += exp`, Sterne nur überschreiben wenn besser.
- Energie wird **erst nach erfolgreichem Engine-Build** abgezogen (Schutz gegen Energie-Verlust bei Build-Fehler).

### Lücken
- **Nur Gold/EXP/Sterne werden vergeben** — die im GDD Kap. 4.3/4.4 versprochenen Karten-Drops, Scraps und „garantierte Epic/Legendary bei 4★" fehlen im Settlement.
- Replay-Button = Toast-Stub; „Nochmal" = nur `PopAsync`; Banner-Texte hardcodiert (nicht lokalisiert).

---

## 12. Erinnerungs-Fragment (Story-Cutscene)  **[LIVE]**

Nur bei **erstmaligem Welt-Boss-Sieg** (Node 10) wird `ShowMemoryFragmentIfNewAsync` aufgerufen → `MemoryFragmentModal` (schwarz-weisse Cutscene): Titel + Inhalt + ggf. Twist-Banner. Bei Welt 8 (Abysstiefe) ist `IsMajorTwist = true` → eigenes Twist-Banner + `Story.TwistRevealed = true`. Idempotent über `Story.ViewedMemoryFragments`. (Inhaltliche Details → Teil VII.)

---

## 13. Auto-Battle  **[DOMAIN-ONLY]**

Die Freischalt-Kurve ist exakt definiert (`AutoBattleProgression.cs`), aber **nicht verdrahtet** — es gibt keinen Auto-Battle-Button und keine Speed-Umschaltung im Battle-Screen.

| Spieler-Level | Speed |
|--------------|-------|
| < 10 | 0 (deaktiviert) |
| 10–19 | 1× (Unlock) |
| 20–29 | 2× |
| 30–49 | 3× |
| 50+ | 4× (MAX) |

Ebenfalls definiert, aber wirkungslos: `IsAutoBattleAllowedForBoss` (Boss-Erstversuch sperrt Auto-Battle).

---

# TEIL II — Sammlung & Progression

## 14. Karten (CardDefinition)  **[LIVE]**

`CardDefinition` ist ein `ScriptableObject` (statische Stammdaten); veränderlicher Zustand liegt in `CardInstance`. **27 Felder**, u.a.: `id`, `displayNameKey`, `flavorTextKey`, `element`, `rarity`, `race`, `cost [1..60]`, `baseAttack`, `baseHealth`, `turnsToSpecial [1..10]`, `baseAbility`, `secondAbility` (LV 5+), `thirdAbility` (LV 10+), `lastWillAbility` (6★, LV 15), `deckLimit`, `globalCraftLimit`, `onPlay/onVictory/onDeathLineKey`, `rivalCardIds`, `synergyCardIds`, fünf Ökosystem-Flags (`isEvent/Premium/Prestige/StarTemple/SaisonPassCard`), `artworkAddressableKey`, `voiceLineAddressableKey`.

Abgeleitet: `IsExclusive` (= einer der Ökosystem-Flags), `CanBeUsedInFusion => !isPremiumCard` (Fusions-Schutz für gekaufte Karten).

### Echte Verteilung (162 Karten, aus `cards.json` gezählt)
- **Rarity:** Gewöhnlich 40, Ungewöhnlich 32, Selten 35, Epic 36, Legendär 14, Mythisch 5.
- **Race:** Ritter 36, Elfen 37, Tiergeister 42, Dämonen 39, Götter 8.
- **Element:** Licht 34, Natur 33, Dunkel 29, Erde 25, Feuer 25, Wasser 16.
- **DeckLimit:** Unlimited 106, MaxTwo 32, OneOnly 24.
- **Mehrfach-Fähigkeiten:** 59 Karten mit Skill 2+3, **5** mit Letztem Willen (die Mythischen).
- **Persönlichkeits-Lines:** 63 Karten (GDD-Soll „ab 3★ Pflicht" = 90 Karten — nicht erfüllt).
- **Stat-Spannen (Ist):** ATK 110–1200, HP 440–3200, COST 4–50, turnsToSpecial 2–4.

Die 5 Mythischen (`OneOnly`, `globalCraftLimit 5`, mit Letztem Willen): `erzkoenig_aldric` (Ritter), `sternbaum_elarion` (Elfen), `fenrir_urdrachenwolf` (Tiergeister), `urdaemon_malphas_rex` (Dämonen), `aetherius_allschoepfer` (Götter, 6★).

Runtime-Lookup über `CardCatalogService` (lazy aus `Resources/CardCatalog`, per Editor „Sync CardCatalog" gefüllt).

## 15. Karten-Level-System (LV 0–15)  **[LIVE]**

### Spielerperspektive
Eine Karten-Instanz wird von LV 0 bis LV 15 (MAX) hochgelevelt. Jeder Level kostet EXP + Gold + Upgrade-Steine; an LV 5/10/15 zusätzlich 1/2/3 Karten-Kopien. ATK/HP steigen prozentual; Skill 2 (LV 5), Skill 3 (LV 10), Letzter Wille (LV 15, nur 6★) werden freigeschaltet; LV 15 gibt einen goldenen Rahmen. Ablauf über das Card-Detail-Modal: „Aufwerten" → Material prüfen/abziehen → Level +1 → Toast.

### Werte-Tabelle (`CardLevelTable`, code-verifiziert)

| Level | EXP | Gold | Stein-Typ × Anzahl | Kopien | Stat-Bonus |
|-------|----:|-----:|--------------------|:------:|:----------:|
| 1 | 100 | 500 | Common ×2 | – | +5 % |
| 2 | 200 | 1.500 | Common ×4 | – | +10 % |
| 3 | 400 | 4.000 | Common ×8 | – | +15 % |
| 4 | 700 | 10.000 | Common ×16 | – | +20 % |
| **5** | 1.200 | 25.000 | Rare ×4 | **1** | +25 % · **Skill 2** |
| 6 | 2.000 | 50.000 | Rare ×8 | – | +30 % |
| 7 | 3.000 | 90.000 | Rare ×16 | – | +35 % |
| 8 | 4.500 | 150.000 | Rare ×32 | – | +40 % |
| 9 | 6.000 | 250.000 | Rare ×60 | – | +50 % |
| **10** | 9.000 | 500.000 | Epic ×10 | **2** | +55 % · **Skill 3** |
| 11 | 13.000 | 800.000 | Epic ×25 | – | +58 % |
| 12 | 18.000 | 1.200.000 | Epic ×50 | – | +63 % |
| 13 | 25.000 | 2.000.000 | Epic ×100 | – | +68 % |
| 14 | 35.000 | 3.500.000 | Epic ×200 | – | +75 % |
| **15** | 50.000 | 8.000.000 | Legendary ×50 | **3** | **+80 %** · Goldrahmen · (6★) Letzter Wille |

Stein-Typ pro Stufe: LV 1–4 Common, 5–9 Rare, 10–14 Epic, 15 Legendary. `ApplyUpgrade` verbraucht Gold + Steine + Kopien-Instanzen atomar.

> **Lücke:** Der EXP-Pfad (`ExpWithinLevel`) wird im gelesenen Karten-Bereich von **keiner Quelle befüllt** — ohne separate EXP-Vergabe scheitern Upgrades an der EXP-Bedingung. (EXP-Quelle möglicherweise außerhalb des Karten-Bereichs; im Code nicht gefunden.) Außerdem werden DeckBuilder/Codex-Tiles immer mit `currentLevel = 0` gebaut — der echte Instanz-Level wird dem Tile nicht durchgereicht.

## 16. Deckbau  **[LIVE]**

### DeckValidator (`Domain/Cards/DeckValidator.cs`) — Live-Regeln
1. Leeres Deck → `EmptyDeck`.
2. > 10 Karten → `TooManyCards`.
3. Pro-Karte-Limit aus `DeckLimit`: OneOnly→1, MaxTwo→2, Unlimited→3.
4. COST-Summe > 200 → `CostBudgetExceeded`.
5. > 2 Legendäre / > 3 Epics / > 1 Mythische → eigene Fehler.

(Konstanten: `MaxDeckCost = 200`, `Deck.MaxCards = 10`, `Deck.MaxRuneSlots = 5`.)

### DeckBuilder-Screen
Sammlung links, Deck rechts, Helden-Portrait (nach gewählter Rasse), Slot-Selector, Namensfeld, „Vorschlag"- und „Speichern"-Button, Suche + Rarity-Filter. Karte antippen → hinzufügen; „X" → entfernen; Live-Zähler „x/10" + COST + Validierungsstatus; Save mit Validierungs-Gate; Dirty-Warnung beim Verlassen.

### Lücken
- **„Vorschlag"-Button ist nicht angebunden** — der vollständig implementierte `DeckBuilderService` (Greedy-Heuristik: `(ATK+HP)×StatBonus/cost`, +Element/Rassen/Rarity/Speed-Boni) ist **[DOMAIN-ONLY]**.
- Kartennamen zeigen Roh-IDs (Suche matcht nur `def.Id`).
- Rarity-Filter ohne „Mythisch".
- Multi-Deck-Slots (Config erlaubt bis 6) nicht im UI baubar — nur ein „Deck 1".

## 17. Codex (Karten-Lexikon)  **[LIVE]**
Grid aller 162 Karten, besessene voll / nicht-besessene ausgegraut, Completion-Zähler „X / 162 entdeckt", Suche, Element-Filter, „Nur Besessene". Klick → Detail-Modal.

**Lücken:** Element-Filter listet nur 5/6 Elemente — **„Erde" fehlt** komplett (25 Erd-Karten nicht filterbar). Suche nur über ID. Der reichhaltige `CodexService` (auch Helden/Welten/Runen/Abilities, Suche über DisplayNameKey) wird vom Screen **nicht** genutzt.

## 18. Fusion / Crafting  **[LIVE]**

Erreichbar über die **Zauberschmiede**. Inventar als Kacheln, 4 Eingabe-Slots, Vorschau-Panel, „Schmieden". Die Vorschau prüft **zuerst ein festes Rezept**, dann Kategorie-Fusion.

### 18.1 Kategorie-Fusion (Typ A) — `CategoryFusionRules.cs`

| Von → Nach | Karten (gleiche Rasse+Rarity) | Gold | Material |
|------------|:-----:|-----:|----------|
| 1★ → 2★ | 3 | 1.000 | — |
| 2★ → 3★ | 3 | 5.000 | — |
| 3★ → 4★ | 4 | 25.000 | rare_scrap |
| 4★ → 5★ | 4 | 100.000 | epic_scrap |
| 5★ → 6★ | 3 | 5.000.000 | mythic_core |

Ergebnis ist eine **zufällige** höherwertige Karte derselben Rasse, **nicht-exklusiv** (`IsExclusive`-Filter). Atomare Save-Mutation mit Re-Check.

> **Wichtig:** 6★ über Typ A ist faktisch **gesperrt** — `PreviewCategoryFusion` bricht bei Mythisch hart ab („nur über feste Rezepte"). Die 5★→6★-Config ist toter Pfad.

### 18.2 Feste Rezepte (Typ B) — alle 10 (`fusion_recipes.json`)

| Rezept | Ergebnis | Input-Karten | Material | Gold | hidden |
|--------|----------|--------------|----------|-----:|:------:|
| recipe_mondbogen_jaegerin | waldlaeuferin_fenris (3★) | elfenschuetze, blumenfee | — | 2.000 | nein |
| recipe_schattenfuerst_kael | schattenlaeuferin_nyx (3★) | schattenklaue, nachtkreatur | dunkel_rune | 3.000 | ja |
| recipe_gott_des_schildes | solaris_gott_feuer (4★) | libra_waage, scharlachrose, sternenwanderer_caelum | heiliger_stein + epic_scrap | 500.000 | ja |
| recipe_solaris_4star | solaris_gott_feuer (4★) | kriegsmagier_voss, daemonenrufer_kelzar, feuervogel_pyra | sonnenstein + 3× epic_scrap | 500.000 | nein |
| recipe_thalassa_4star | thalassa_gezeiten (4★) | elfenmagierin_lira, wasserdrache_tidal, sturmreiterin_kira | gezeitenkristall + 3× epic_scrap | 500.000 | nein |
| recipe_gaia_4star | gaia_erdmutter (4★) | donnerhirsch_kaelen, felsenbrecher_gorm, steinhueterin_gaia | urkern_erde + 3× epic_scrap | 500.000 | nein |
| recipe_noctis_4star | noctis_schatten (4★) | dunkelmagierin_seela, traumweberin_aria, nebelkraehe_moira | schattenkern + 3× epic_scrap | 500.000 | nein |
| recipe_selene_5star | selene_mondschleier (5★) | solaris_gott_feuer, mondkoenigin_naeris, weissesherz | mondstein + 5× legendary_scrap | 2.000.000 | nein |
| recipe_chronos_5star | chronos_zeit (5★) | thalassa_gezeiten, hoellenhund_gruum, grimmfang_alpha | zeitsplitter + 5× legendary_scrap | 2.000.000 | nein |
| recipe_aetherius_6star | aetherius_allschoepfer (6★) | selene_mondschleier, artemis_jaegerin, hoellenfuerst_malphas | mythischer_kern + 10× legendary_scrap | 5.000.000 | ja |

**Crafting-Kette:** 4★-Götter sind Inputs für 5★, 5★ für 6★. Der `mythischer_kern` wird über Sternpunkte erzeugt (3 Fragmente à 500 SP = 1 Kern, also 1.500 SP).

### 18.3 Götter-Crafting
Götter sind Premium-**Rasse** (nicht Premium-Karte) — ausschließlich über feste Rezepte (kein Drop, kein Event-Pfad). Es gibt keinen separaten Götter-Service; die „verschiedene Rassen"-Philosophie ist nur durch handkuratierte Inputs realisiert, nicht im Code erzwungen.

### 18.4 Sicherheitsmechanismen (`FusionService.CanUseInFusion`)
- **Premium-Sperre** (`!CanBeUsedInFusion`), **Favoriten-Schutz** (`FavoritedCardInstanceIds`), **Deck-Sperre** (Karten im aktiven Deck), Doppel-Input-Abwehr, Premium-Ausblendung im Schmiede-Inventar.
- **[FEHLT] gegenüber GDD 6.4:** Confirmation-Dialog (Schmieden läuft ohne Modal), blockierende Letzte-Kopie-Warnung (nur Text), Rückkauf (24h, doppelter Preis), Besitz-Limits (`GetMaxCopies` definiert, aber nie aufgerufen).

## 19. Sammlungen & Material-Drops

### 19.1 Sammelsets (`collections.json`)  **[LIVE]** (aber Hub-Zugang fehlt)

| Set | Materialien | Belohnung |
|-----|:-----------:|-----------|
| white_heart | 4 | engelsritter (Epic) |
| dark_heart | 6 | schattenfuerst (Legendär) |
| dragon_set | 5 | elder_drache (Legendär) |
| machine_core | 4 | kriegsmaschine (Epic) |

Der `CollectionTradeScreen` nutzt den `CollectionService` echt (Set-Status, Tausch). **Aber:** kein Hub-Button führt zu `collection-trade` (nur im Legacy-`HubCityRenderer`) → aktuell für den Spieler **nicht erreichbar**. Zudem prüft der aktive Pfad `ClaimedCollectionSetIds` nicht → Sets sind mehrfach tauschbar. (Ein zweiter `CollectionExchangeService` existiert als toter, inkonsistenter Parallel-Code.)

### 19.2 Material-Drops (`material_drops.json`)  **[DOMAIN-ONLY]**
18 Drop-Tabellen (nur n5/n10 jeder Welt), höhere Sterne = höhere Chance (bis 80 % bei 4★ in späten Welten). `MaterialDropService.RollAndAwardAsync` ist vollständig — wird aber im Kampf-Settlement **nirgends aufgerufen** → Materialien droppen im Spiel **nie**. (Drop-RNG nutzt `System.Random`, nicht deterministisch.)

## 20. Spieler-Level-Progression  **[teilweise LIVE]**
- EXP-Kurve `PlayerLevelCurve`: `EXP(n) = round(1000 × 1.08^n + 50 × n²)`, SoftCap 150.
- `ProgressionService.AwardExpAsync` wendet EXP an, vergibt Gold/Diamanten der Schwellen (LV 5/10/15/…/100); Packs/Features/Runen-Slots werden bisher nur **geloggt**, nicht als Inventar persistiert.
- **Welt-/Node-Level-Gates werden NICHT durchgesetzt.** Drei untereinander inkonsistente Modelle existieren: `recommendedPlayerLevel` (worlds.json), `AccountUnlocks` (Level→Feature), `LevelUpRewardTable.feature.world_5/7` — keines sperrt im WorldMap-Flow tatsächlich Welten.

---

# TEIL III — Welten, Schwierigkeit, Prestige & Idle

## 21. Die 10 Welten (echte Werte aus `worlds.json`)  **[LIVE]**

Jede Welt hat **genau 10 Nodes** (8 Normal + 1 MiniBoss [Node 5] + 1 WorldBoss [Node 10]) → **100 Nodes gesamt**.

| # | id | Element | Counter | empf. Lvl | Gold/Tag | Boss | Mentor | Prestige-IV-Karte |
|---|----|---------|---------|----:|----:|------|--------|-------------------|
| 1 | elderwald | Natur | Feuer | 1 | 100 | uralter_baumwaechter | Lumis | urwaldgeist_ygg |
| 2 | sandreich | Erde | Dunkel | 8 | 250 | erdtitan_gorath | Marschall Aldor | sandkaiser_darius |
| 3 | vulkanhort | Feuer | Wasser | 18 | 500 | hoellenfuerst_malphas | Lilith | lavaschmied_pyrros |
| 4 | frostgipfel | Wasser | Natur | 30 | 750 | wasserdrache_tidal | Mondpriesterin Lira | eiskoenigin_freja |
| 5 | schattenlande | Dunkel | Licht | 50 | 1.200 | daemonenkoenigin_lilith | Grimmfang | schattenfuerst_mordred |
| 6 | sturmzitadelle | Licht | Erde | 65 | 1.600 | sturmadler_aethon | Königin Sera | blitzgeneral_thorak |
| 7 | titanengrat | Erde | Dunkel | 80 | 2.000 | kristalldrache_diamara | General Dorn | bergtitan_gorak |
| 8 | abysstiefe | Wasser | Natur | 95 | 2.400 | jormungand_weltenschlange | Lumis (Twist!) | tiefseekaiser_leviath |
| 9 | galaxy_wald | Licht | Dunkel | 110 | 2.800 | selene_mondschleier | Aetherius-Geist | kosmischer_druide |
| 10 | drachenfeste | Feuer | Wasser | 130 | 3.000 | aetherius_allschoepfer | Nythragor | urdrachenlord_tiamat |

Querverweise sind 100 % konsistent: alle 110 Gegner-Karten, alle 10 Bosse, alle 10 Prestige-IV-Karten existieren in `cards.json`.

### Belohnungs-Skalierung (Node-Rewards, code-verifiziert)
- **Normal-Node:** 2★ = 2× / 3★ = 4× / 4★ = 10× des 1★-Gold. (EXP: 2★ = 2.5× / 3★ = 5× / 4★ = 10×.)
- **Mini-Boss (Node 5):** 2× der Normal-Belohnung. **Welt-Boss (Node 10):** 4×.
- Beispiel Elderwald Normal 1★/2★/3★/4★: Gold 50/100/200/500, EXP 10/25/50/100.

## 22. Prestige-System (Welt-Aufwertung I–IV)  **[LIVE]**

Sind **alle 10 Nodes einer Welt ≥3 Sterne**, erscheint „Aufwerten zu Prestige X". Das Bestätigungs-Modal zeigt Kosten, neue Multiplikatoren, Boss-Phasen und (bei IV) die exklusive Karte; eine Warnung weist auf den **Sterne-Reset** hin. Bestätigung: Gold abziehen, Sterne nullen, Stufe +1, bei IV exklusive Karte ins Inventar.

| Stufe | Upgrade-Kosten | Gegner-Stats | Gold-Drop | Daily-Income | Boss-Phasen | Exkl. Karte |
|-------|----------------:|:------------:|:---------:|:------------:|:-----------:|:-----------:|
| Normal | 100.000 | ×1.0 | ×1.0 | ×1.0 | 2 | nein |
| I | 500.000 | ×1.3 | ×1.5 | ×2 | 2 | nein |
| II | 2.000.000 | ×1.6 | ×2.0 | ×4 | 2 | nein |
| III | 5.000.000 | ×2.0 | ×3.0 | ×8 | 3 | nein |
| IV (MAX) | — (-1) | ×2.5 | ×4.0 | ×16 | 4 | **ja** |

(Kosten = um auf die nächste Stufe zu kommen. Mult. = Boni der jeweiligen Stufe selbst. Code und `prestige_balancing.json` sind identisch.)

> **[DOMAIN-ONLY]-Lücke:** `ScaleEnemyStats` und `ScaleGoldDrop` werden im Kampf **nie aufgerufen** — die Prestige-Stufe macht Gegner im Kampf **nicht** stärker und erhöht den Gold-Drop **nicht**. Auch die erhöhte Boss-Phasen-Zahl (III/IV) wird nur im Modal angezeigt, nicht im Battle-Setup angewandt. Real wirksam ist von Prestige nur: die Stufe selbst, der Sterne-Reset, die IV-Karte und das skalierte **Idle-Income**.

## 23. Daily-Income / Idle  **[LIVE]**

Beim Hub-Eintritt wird passives Gold für jeden vollen Tag seit dem letzten Tick gutgeschrieben, gedeckelt auf **7 Tage** (Schutz gegen Uhr-Manipulation). Init-Tick bucht 0 (Schutz gegen Riesen-Windfall). `daily = (int)(baseGoldPerDay × DailyRevenueMultiplier)`.

> **Verhaltens-Vorbehalt:** Income wird nur für Welten gebucht, die einen Eintrag in `Prestige.StufenByWorldId` haben — und dieser entsteht **erst durch ein Prestige-Upgrade**. Eine nie aufgewertete Welt (Stufe „Normal") generiert daher **0 Gold/Tag**. Passives Einkommen startet faktisch erst ab dem ersten Prestige-Upgrade einer Welt.

Rechnerisch bei allen 10 Welten auf Prestige IV: Σ baseGoldPerDay = 14.600 × 16 = **233.600 Gold/Tag** (GDD nennt konservativ „150.000+").

---

# TEIL IV — Ökonomie & Engagement

## 24. Währungen  **[LIVE]**
`Currency`-Enum: Gold, Diamond, Energy, GuildPoints, UniversalScraps, MeritPoints, ArenaTickets (TBD). Plus vier separate Karten-Level-Stein-Pools `ScrapType { Common, Rare, Epic, Legendary }`. Mutationen gekapselt (private set, Add/Spend mit Guards). **Merit-Hardcap 199.999.**

## 25. Energie  **[LIVE]**
- **Cap 60** (`EnergyDefaultCap`), **Regen 1 / 360 s (6 min)** → 60 Energie in 6 h.
- **Bonus-Energie** ohne Cap (`EnergyBonus`, kann über 60 gehen, grüne UI-Anzeige). `AddEnergyAdaptive` füllt erst Normal, dann Bonus; `SpendEnergy` verbraucht zuerst Bonus.
- Energiekosten: Normal-Node 1, Mini-Boss 2, Welt-Boss 3, Arena 5, Dieb-Angriff 5.
- Regen ist drift-frei (`LastEnergyRegenAtUtc` wird exakt um verbrauchte Sekunden vorgeschoben).

## 26. Login-Belohnungen (30-Tage-Zyklus, Tempel)  **[LIVE]**

Im **Tempel** (Hub-Gebäude): 30-Tage-Kalender, heutige Belohnung als Klartext, „Abholen"-Button, datumsbasierter Re-Claim-Schutz. **Belohnt Konstanz, nicht Spielzeit** (jeder Tag schaltet den nächsten Zyklustag frei).

**Sternkarten-Verteilung:** jeder Tag ≥1 Sternkarte; Tag 7/14 Silber, Tag 21 Gold, Tag 30 Gold+Platin. Pro lückenlosem Monat: 22 Bronze + 2 Silber + 2 Gold + 1 Platin = **112 Sternpunkte**.

**Meilensteine:** Tag 5 → 1★-Karte · Tag 7 → 2★ + 5.000 Gold + Runen-Fragment · Tag 14 → 3★ + 15.000 Gold · Tag 21 → 3★ + 25.000 Gold + Epic Scrap · **Tag 30 → 4★ + 50.000 Gold + 50 Diamanten + Legendary Scrap**. Diamanten ab Tag 10 (10) bis Tag 30 (50).

> **Lücken:** `rune_fragment` und `exp_potion` werden **nicht gebucht** (nur geloggt — betrifft Tage 7/10/13/17/26). **Karten-Items erzeugen Müll-Instanzen:** sie landen als `PendingClaim` mit `SubType = "card_random_4star"`; beim Hub-Einlösen wird daraus eine `CardInstance` mit `CardDefinitionId = "card_random_4star"` — **keine gültige Karte**. Die echte Rarity-Pool-Auswahl ist „TODO Phase 2".

## 27. Sternkarten & Sternkarten-Tempel  **[LIVE / Teil]**

Im Tempel: Sternkarten-Sammlung (Bronze 1 / Silber 5 / Gold 15 / Platin 50 SP), Sternpunkte-Saldo, Mythic-Fragment-Fortschritt, **6 Eintausch-Optionen**:

| Option | Kosten | Belohnung |
|--------|-------:|-----------|
| Zufällige 2★-Karte | 30 | card_random_2star |
| Wählbare 3★-Karte | 80 | card_chosen_3star |
| Exklusive 3★ | 150 | sternenweber_astria |
| Exklusive 4★ | 350 | sternentiger_raj |
| Legendary Scrap | 100 | legendary_scrap |
| Mythic-Fragment | 500 | mythic_fragment (3 = 1 Kern) |

> **Lücke:** Der Eintausch **bucht die Sternpunkte ab, gibt aber (außer dem Mythic-Fragment) KEINE Belohnung aus** — „TODO Phase 2: Belohnung ins Inventar legen". Der Spieler verliert für die 5 anderen Optionen aktuell nur den Saldo. Der Mythic-Fragment-Pfad funktioniert real.

## 28. Quests (Daily / Weekly)  **[DOMAIN-ONLY]**

20 Quests (10 Daily + 10 Weekly) in `quests.json`, vollständiges Modell (`QuestService`: Progress, Persistenz, Re-Claim-Schutz, Reset-Logik, Belohnungs-Auszahlung für Currency+Scrap). UI mit 5 Tabs (Täglich/Wöchentlich/Erfolge/Events/Login).

> **Kritische Lücke:** Die Trigger-Hooks (`OnBattleWon`, `OnCardPlayed`, `OnDamageDealt`, `OnBossDefeated`, `OnArenaMatchWon`, `OnThiefAttacked`, …) werden **von keinem Gameplay-Code aufgerufen**. Quest-Fortschritt steigt im echten Spiel **nie** → keine Quest wird je abschließbar. Zudem nutzt der Reset-Service als Anker `LastEnergyRegenAtUtc` (nicht die dafür vorgesehenen Felder) und wird im Hub-Flow nicht getickt.

**Beispiel-Quests:** daily_win_battles (3 Siege → 200 Gold + 2 Common Scrap), daily_login (1 → 10 Diamanten), weekly_login_7_days (7 → 200 Diamanten + 10.000 Gold), weekly_deal_500k_damage (→ 5.000 Gold + 3 Rare Scrap).

## 29. Achievements (Erfolge)  **[DOMAIN-ONLY]**
10 Erfolge à 4 Tiers (`achievements.json`), Trophäen-Punkte + Titel (z.B. boss_slayer T4 „Drachen-Töter" bei 500 Bossen / 2000 Punkte). `AchievementService` voll, Trophäen werden als Merit-PendingClaim ausgezahlt.

> **Lücken:** Trigger-Hooks **nirgends aufgerufen** (kein Fortschritt). Der „Erfolge"-Tab liest aus dem `QuestService` (nicht `AchievementService`) und bleibt mangels Achievement-Period-Quests **leer**. Auffälligkeit: `world_conqueror`-Top-Schwelle = 9 (bei 10 Welten — Rest der alten 9-Welten-Planung).

## 30. Die drei konkurrierenden Login-Systeme
1. **30-Tage-Tempel-Zyklus** (`login_rewards.json`) — real, GDD-konform. **[LIVE]**
2. **`DailyRewardService`** (7-Tage-Zyklus) — registriert, aber von keinem Screen genutzt. **[DOMAIN-ONLY]**
3. **QuestCenter-Login-Tab** (7 Tage, hardcodiert, Gold = `day × 1000`) — bucht nichts. **[SKELETT]**

---

# TEIL V — Monetarisierung

> **Querschnitt:** Es existieren eine saubere Service-Schicht (Pack-Rolling mit Pity, DailyShop-Rotation, Saison-Pass-Engine, IAP-Stub) **und** UI-Screens — aber **keiner der Screens ruft seinen Service auf**. Die JSON-Dateien `premium_shop.json` und `events.json` werden zur Laufzeit nie gelesen (nur Editor-Validierung). **Es kann aktuell niemand etwas kaufen.**

## 31. IAP / Diamanten-Pakete  **[SKELETT]**
`UnityIapService.BuyAsync` schreibt nach 200 ms Fake-Delay direkt Diamanten gut (`ServerValidated = false`) — **keine** echte Billing-Anbindung, kein `com.unity.purchasing`-Package, **kein Aufrufer im UI**. Der Shop-Tab „Diamanten" zeigt nur ein Text-Label.

Hartcodierter Katalog: `diamonds_starter` 0,99€/60 · `_small` 4,99€/300+30 · `_medium` 14,99€/980+150 · `_large` 29,99€/1980+400 · `_huge` 49,99€/3280+800 · `_mega` 99,99€/6480+2000 (Whale-Staffelung, mehr Diamanten/€ bei großen Paketen).

## 32. Card-Packs  **[Logik teils real, UI Mock]**
- **Domain real:** `CardPackRoller` (gewichteter Roll, Pity, Legendary-Cap, Garantie). `ShopController.BuyPackAsync` (Diamant-Abzug + Pity-Persistenz im Save) — **aber** erzeugt noch keine Karten-Instanzen (TODO) und hat **keinen UI-Aufrufer**.
- **`packs.json` (4 Packs):** common_pack (10 Karten/50💎), rare_pack (250💎, Pity 60), epic_pack (1000💎, Garantie Legendär, Pity 15), mythic_summon (2000💎, bis 3 Legendäre, Pity 10). Mythische Karten sind über **keinen** Pack ziehbar.
- **UI (`ShopScreen`):** zeigt **6 ganz andere** hardcodierte Packs (Basis/Standard/Premium/Legendär/10er/Element); Kauf = Toast. → Drei widersprüchliche Pack-Kataloge (JSON ≠ UI ≠ GDD).

## 33. Premium-Karten  **[NUR DATEN]**
`premium_shop.json`: 3 permanente (goldwolf_aurelius/kristallhirsch_cervus/infernalwolf_pyrrhus, je 300💎) + 3 rotierende (himmelsritter_orion/schattenprinzessin_nyx/elfenprinz_luminaris, je 800💎). **Keine Runtime-Klasse, kein Screen** — nur Editor-Validierung. Karten existieren mit `isPremiumCard`-Flag (+ Fusions-Schutz).

## 34. DailyShop  **[DOMAIN-ONLY]**
`DailyShopService`: deterministische 6-Slot-Rotation (Seed = Datum), ein Slot halbpreisig, vollständige Kauf-Buchung — aber **kein UI-Screen** (Shop-Tab „Angebote" = Text). Pool: common_pack 50💎, rare_pack 250💎, Common-Scrap 5000 Gold, Rare-Scrap 80💎, Epic-Scrap 150💎, Energie 30/60 (50/90💎), Rune angriff_klein 100💎.

## 35. Saison-Pass „Aetherius" (Saison 1)  **[Engine real, UI Mock]**
- **`saison_pass.json`:** Start 2026-06-01, Ende 2026-07-01, 30 Stufen, `xpPerTier 1167` (Σ 35.010), Premium 500💎. Belohnungen nur an 6 Meilenstein-Tiers (5/10/15/20/25/30). Free Tier 15 = any_rare-Karte, Tier 30 = 100.000 Gold + Legendary Scrap. Premium Tier 15 = `saison_pass_3star`, Tier 30 = `saison_pass_4star` + 100💎 + Titel. Premium-Diamanten gesamt 330 (bei 500 Kaufpreis).
- **`SaisonPassEngine`/`SaisonPassService`** real (XP→Tier, Reward-Range, PendingClaim) — **aber `AwardXpAsync` hat keinen Aufrufer** (Pass bekommt nie XP), `MakeClaim` ignoriert Rune/ExpPotion/Cosmetic.
- **`SaisonPassScreen`:** Mock (eigene Konstanten `XpPerTier=1000`, 14-Tage-Mock-Restdauer, Belohnungen per `tier % 5` — kein Bezug zur JSON; Premium-Kauf = Toast).

## 36. Event-Kalender & Notfall-Kauf  **[NUR DATEN]**
`events.json`: 5 saisonale Events (Yule 15.12.–10.01., Blütenfest, Sonnenwende, Erntemond, Schattenerwachen) mit Punktschwellen und Notfall-Diamantkosten (500/1000/1500). **Kein Runtime-Event-System, kein Punkt-Tracking, kein Notfall-Kauf** — nur Editor-Validierung; Shop-Tab „Event" = Text.

## 37. „Kein Pay-to-Win" im Code
Die Schutz-Mechaniken sind real implementiert: Premium-Karten sind nur 3★/4★ (nicht die 6★-Mythics), Götter nur per Fusion, Diamanten auch ohne Geld erreichbar (Login/Saison/Events), Pack-Pity ist server-resistent im Save persistiert (Anti-Reset-Exploit), Legendary-Cap pro Pack, Fusions-Schutz für gekaufte Karten, Re-Entrancy-Schutz gegen Doppelkauf. Da der Kauf-Pfad aber nicht verdrahtet ist, ist aktuell schlicht kein Kauf — und damit kein P2W — möglich.

---

# TEIL VI — Soziales & Online (GDD-Phase 5)

> **Gesamtbild:** Dieser Bereich ist **fast vollständig [SKELETT]**. Es gibt **kein Netzwerk-Backend** (kein `INetworkService`/`PhotonNetworkService`, kein Photon-Package, Firebase nicht angebunden). Charakteristisch: saubere, getestete **Domain-Logik** (Punkte, Ligen, Tiers, Gebots-Auswertung, Belohnungs-Tabellen) — aber die UI-Screens benutzen sie fast nirgends und zeigen Mock-Daten + „… folgt mit Backend"-Toasts.

## 38. Arena (asynchrones PvP)  **[SKELETT]**
- **Domain real:** `ArenaLeagueTable` (7 Stufen, Schwellen Trainings 0 → Meister 40.000), `ArenaSeasonService` (Sieg +25 / Niederlage −15, Saison-Rewards Bronze→Meister), `ArenaController.CalculateRankChange` (Glicko-2-ähnlich).
- **UI:** `ArenaScreen` berechnet den Rang aus **MeritPoints** (falsche Währung, eigene dritte Liga-Logik), W/N hardcodiert „0/0"; „Quick-Match"/„Leaderboard" = Toasts. `ArenaController.StartMatchAsync` ist Stub (harte Mock-`MatchSummary`).
- Separater `PvpMatchmakingScreen` (Such-Animation, hardcodierter Gegner „[NEXUS] Sturmreiterin LV 88") — **vom Hub nicht erreichbar**.
- Saison-Konflikt: Domain rechnet `+3 Monate`, GDD/Config sagen 30 Tage.

## 39. Gilden  **[SKELETT]**
- **Domain real:** `GuildSnapshot` (Tag 5 Zeichen, Name 3–20), MaxMembers 30/40/50, Level-Beitrags-Tabelle.
- **`GuildController.CreateGuildAsync`** ist die **einzige** funktionierende Methode (Level-Gate 25, 50.000 Gold, GuildId-Persistenz) — aber das UI ruft sie nicht (Gründen/Suchen = Toasts).
- `GuildScreen`: in-Gilde 3 Mock-Mitglieder, Tech-Tree-Tab ohne Daten, Mock-Chat, Mock-Spende. UI-Toast sagt fälschlich „Stufe 20" (Code-Konstante = 25). Gildeninterner Karten-Tausch **[FEHLT]**.

## 40. Klan-Welt / Gebiets-Krieg  **[SKELETT]**
- **Domain real:** `TerritoryService` (Gebots-Auswertung, Tie→Klan-Match, Gebiets-Zuweisung), `Territory` (Daily-Income/Min-Gebot je Rarity: Common 1.000/50.000 … Legendär 20.000/1.500.000), `GuildTreasuryService.ComputeAutoSplit`.
- **`GuildWorldMapScreen`:** 10 hardcodierte Mock-Gebiete; `TerritoryService` ist injiziert, wird aber **nie aufgerufen**; Gebot = Toast. **Vom Hub nicht erreichbar.** Kein ClanMatch-Screen (Best-of-9 nicht spielbar).
- GDD-Abweichungen: keine 3-Tage-Bietphase (nur `+7 Tage`), kein Best-of-9-Format, keine saisonalen Boni, keine Live-Weltkarte.

## 41. Chat  **[SKELETT]**
- **Domain real:** `ChatValidator` (max 200 Zeichen, Welt-Cooldown 30 s), `ChatController.SendAsync` (Länge/Cooldown/Profanity, aber kein Versand), `ChatModerationService` (Mute/Report/Auto-Mute ≥3 Reports/24h, **persistiert** in `ChatSlice`).
- **`ChatOverlay`:** 4 Tabs, Mock-Nachrichten, Senden = Toast. Weder Controller noch Moderation injiziert. Kein Realtime-Empfang.

## 42. Freunde  **[Anzeige LIVE, Aktionen SKELETT]**
- **`FriendsService` real & persistiert** (`FriendsSlice`): SendRequest/Accept/Reject/Remove/Block (Limits 100/100, Block-Kaskaden).
- **`FriendsScreen`** liest den Slice (Anzeige funktioniert), aber Annehmen/Ablehnen/Hinzufügen = Toasts (Service nicht aufgerufen). Kein Friend-Code, kein Status-Sync.

## 43. Dieb-Event (Server-Coop)  **[SKELETT]**
- **Domain real:** `ActiveThief.ContributionShare`, `ThiefService` (Reward-Tiers Pity→TopAttacker, Last-Hit-/Discovery-Bonus), `ThiefController.AttackAsync` (Energie −5, max 10 Angriffe/Spieler).
- **`ThiefScreen`:** spawnt lokal einen Mock-Dieb (Elite LV 58, 120 min), Angriff = Zufallsschaden 1.000–3.000, **umgeht** den Controller (kein Energie-Abzug, kein Limit). **Vom Hub nicht erreichbar.**
- **[FEHLT]:** server-weiter HP-Pool, DAU-Skalierung, Photon-Sync. Zwei widersprüchliche Belohnungs-Tabellen (Service bis 150.000 Gold vs. Controller bis 15.000 + Merit).

## 44. Merit / Ehre-Ranking  **[LIVE — das einzige verdrahtete Social-Feature]**
- **`MeritRankingScreen`** nutzt seinen `MeritService` **echt** (Podium Top-3 + Liste, eigener Spieler einsortiert). `MeritService` real (Vergabe, Cap 199.999, Ranking, Rang-Belohnungen Rang 1 → 100.000 Gold + Titel).
- **Aber:** die 8 Mitbewerber sind Bot-Mocks (kein Server-Leaderboard); `MeritService.Award` wird vom Gameplay nicht aufgerufen (nur indirekt über `ThiefController`/PendingClaims werden Merit-Punkte gestreift); Rang-Belohnungen werden nie ausgezahlt.

---

# TEIL VII — Story & Welt (Narrative)

## 45. Mythologie (Aetherius / Nythragor / 6 Säulen)  **[Daten LIVE, Anzeige indirekt]**

- **Aetherius, der Allschöpfer** sprach am Anfang ein Wort — daraus wurden die 6 Elemente und die Welt **Aethera**. Er schläft in der Mythischen Karte, erwacht erst am Ende.
- **Nythragor, der Kettenbrecher** — der siebte, verbannte Gott des Wandels. Glaubt aufrichtig, dass nur Zerstörung wahre Neuerschöpfung bringt. War einst wie ein Bruder für den Spieler.
- **Die 6 Säulen** halten Aethera im Gleichgewicht; jede ist in einer Welt korrumpiert:

| Säule | Element | Welt | Zustand |
|-------|---------|------|---------|
| Lebensbaum | Natur | elderwald | Verwelkend |
| Flammenherz | Feuer | vulkanhort | Korrodiert |
| Schattenriss | Dunkel | schattenlande | Aufgerissen |
| Sternenfeuer | Licht | sturmzitadelle | Verblassend |
| Urkern | Erde | titanengrat | Zerbrochen |
| Gezeitenkern | Wasser | abysstiefe | Eingefroren |

Die 4 übrigen Welten (Sandreich, Frostgipfel, Galaxy-Wald, Drachenfeste) haben Ersatz-/Splitter-„Säulen" (Sandwacht/Gezeitenkern-Echo/Dimensionsknoten/Finale). Der `mythology`-Block wird zur Laufzeit **nicht** deserialisiert (kein Lexikon-Screen) — die Mythologie erlebt der Spieler nur über Welt-Story- und Fragment-Texte.

## 46. Der Spieler-Charakter — Der Rufer
Der Spieler ist **der Rufer**, der ohne Erinnerung im Elderwald erwacht. Name = `PlayerProfile.DisplayName` (Registrierung). **Kein Geschlechts-Feld** im Datenmodell. Die „Rasse" bestimmt Helden-Passiv + Mentor, **nicht** das Aussehen (keine sichtbare Avatar-Figur). Götter sind nicht wählbar.

## 47. Die 10 Erinnerungs-Fragmente + Twist + Enden  **[LIVE; Enden FEHLT]**

Trigger: erstmaliger **Welt-Boss-Sieg** → schwarz-weisse Cutscene. Die Fragmente decken schrittweise auf, dass der „Held" einst **Nythragors Champion** war.

| # | Welt | Inhalt (Kurzfassung) | Wahrheit |
|---|------|----------------------|----------|
| 1 | elderwald | Ein Name: Nythragor. Angst & Schuld. | Ich diente ihm |
| 2 | sandreich | Hände, Karten, Macht, ein Lächeln. | Ich war machtsüchtig |
| 3 | vulkanhort | „Du gehörst mir." Ich nicke. | Ich ging freiwillig |
| 4 | frostgipfel | Ich stand neben Nythragor. Götter weinen. | Erste Zweifel |
| 5 | schattenlande | Ich zerstörte eine Säule mit Absicht. | Ich war Teil des Problems |
| 6 | sturmzitadelle | Götter flehten. Ich hörte nicht. | Was habe ich getan? |
| 7 | titanengrat | Nythragor gab mir Macht. Ich NAHM sie. | Ich war gierig |
| **8** | **abysstiefe** | **DER TWIST** — Jormungand zeigt: Ich war Nythragors Champion. | Ich WAR der Feind |
| 9 | galaxy_wald | Ich brach den Pakt. Es kostete alles. | Ich tat das Richtige |
| 10 | drachenfeste | Mein wahrer Name. Warum ich mich abwandte: Ich sah das Leid. | Ich entschied mich, besser zu sein |

- Fragment 8 setzt `IsMajorTwist` → Twist-Banner + `Story.TwistRevealed`.
- **Code-Eigenheit:** Als Inhalt wird `world.<id>.memory` angezeigt (nicht `fragment.N.content`), und als Fragment-ID wird die **Welt-ID** persistiert (nicht `fragment_N`) — beide Textsätze existieren in `strings.csv` und divergieren leicht. `world.sandreich.memory` fehlt möglicherweise.
- **Die zwei Enden (Welt 10):** `NythragorEndingChoice { Destroyed, Redeemed }` existiert im Save-Modell, wird aber **nirgends geschrieben/gelesen** — **kein Auswahl-Screen, keine Branching-Logik**. **[FEHLT].**

## 48. Schlüssel-NPCs

| NPC | Rasse | Welten | Rolle |
|-----|-------|--------|-------|
| Lumis | Götter | alle 10 | Lichtgeist, ständiger Begleiter, eigenes Geheimnis |
| Marschall Aldor | Ritter | 1,2,9,10 | Veteran, kannte den letzten Rufer |
| Mondpriesterin Lira | Elfen | 1,4,9,10 | Weise, fürchtet die alte Macht im Spieler |
| Grimmfang | Tiergeister | 1,3,7,10 | Uralter Wolf, wusste immer wer der Spieler ist |
| Dämonenkönigin Lilith | Dämonen | 3,5,9,10 | Bündnis aus Pragmatismus |
| Königin Sera | Ritter | 2,6,10 | Symbol der Hoffnung |
| General Dorn | Ritter | 2,6 | Misstrauisch — hatte von Anfang an recht |
| Nythragor | Götter (gefallen) | 8,9,10 | Antagonist |
| Aetherius (Geist) | — | (Mentor Galaxy-Wald) | Allschöpfer, erwacht am Ende |

NPC-Daten + Texte sechssprachig vollständig. Portraits im Fragment-Modal sind vorgesehen, aber inaktiv (`NpcId` wird nie gesetzt).

---

# TEIL VIII — Technische Infrastruktur

## 49. Architektur (6 Assemblies)  **[LIVE]**
Gerichtete Clean-Architecture-Kette, vom Compiler erzwungen:
```
Bootstrap → UI → Game → Domain → Core
```
- **Core:** rein technisch (Addressables/UniTask), keine Spiel-Assembly.
- **Domain:** Geschäftsregeln, UnityEngine-frei, testbar (nur Core + Newtonsoft + UniTask).
- **Game:** Application-Services/Controller.
- **UI:** Screens (UI-Toolkit).
- **Bootstrap:** Composition Root.
Keine zyklischen Referenzen.

## 50. Save-System (lokales JSON, Schema v4)  **[LIVE, hochwertig]**

`PlayerSave` (`[Serializable]`, Newtonsoft), **23 Felder/Slices**, im Ctor alle nicht-null mit Defaults. Slices nach Generation:

- **v1:** Profile, Currencies, CardInventory, RuneInventory, Decks, ActiveDeckSlot, WorldProgress, LastEnergyRegenAtUtc, LastSavedAtUtc.
- **v2:** Tutorial, Achievements, FriendsSlice, ChatSlice, PendingClaims, PackPityCounters, UnlockedFeatureKeys, SaisonPassXp.
- **v3:** Prestige, Sternkarten, Story (= ChosenRace/Fragmente/Personality-Lines/TwistRevealed/EndingChoice), Events, FavoritedCardInstanceIds, ClaimedCollectionSetIds.
- **v4:** Quests.

> **Schema-Stand:** `SaveMigrator.CurrentSchemaVersion = 4` — das GDD/CLAUDE.md sagen noch „v3". Die GDD-Slices `MemoryFragmentSaveSlice`/`HeroPassivSaveSlice`/`KartenPersoenlichkeitSlice` existieren **nicht einzeln** — alles ist in `StorySaveSlice` konsolidiert.

**Persistenz (`FirebaseSaveService` — trotz Namens rein lokal):**
- 3 Dateien in `Application.persistentDataPath`: `player_save.json` / `.bak.json` / `.tmp.json`.
- **Atomic-Write** (Temp → Backup-Rotation → Live), `SemaphoreSlim`-Gate, In-Memory-Cache mit Invalidierung, atomares `MutateAsync` (Read-Modify-Write unter einem Lock).
- `SaveMigrator` migriert v1→v2→v3→v4 (defensiv, idempotent), warnt bei Downgrade statt still zu degradieren.
- Dokumentierte Exploit-Fixes: C3 (Idle-Income-Windfall), H12 (Quest-Re-Claim), M10 (Mythic-Kern-Verlust).

## 51. DI (VContainer 1.16.9)  **[LIVE]**
Constructor Injection durchgängig. `GameInstaller` registriert ~50 Singletons (Infrastruktur-Services interface-gebunden, alle Domain-/Application-Services), `UIBootstrap`/`UIInstaller` registrieren ScreenManager + ~31 Screens (als Transient, vom ScreenManager gecached) + 4 Modal-Contexts. `FusionService` bewusst nicht registriert (vom Wrapper newable).

## 52. Lokalisierung (eigener `CsvLocalizationService`)  **[LIVE]**
- Quelle: `Resources/Localization/strings.csv` (1.668 Zeilen, Header `Key,DE,EN,ES,FR,IT,PT`).
- **6 Sprachen real befüllt** (auch ES/FR/IT/PT — keine Platzhalter), Fallback-Kette aktuelle Sprache → DE → key. Persistenz `ak.lang`, Systemsprache-Erkennung, Default DE.
- Key-Pattern: `card.<id>.name|flavor|play|victory|death`, `world.<id>.name|story|memory|saeule`, `fragment.<n>.title|content|reveal`, `hero.<rasse>.…`, `npc.<id>.…`, `saeule.<name>[.state]`.
- **Abweichung:** Es wird **nicht** das `com.unity.localization`-Package genutzt (wie CLAUDE.md/Tech-Stack nahelegen), sondern dieser leichtgewichtige Eigen-Service.

## 53. Tech-Stack — installierte Pakete  **(manifest.json verifiziert)**
**Installiert:** UniTask 2.5.10, UniRx 7.1.0, VContainer 1.16.9, Addressables 2.9.1, Localization 1.5.11, mobile.notifications 2.4.3, Newtonsoft-Json 3.2.2, URP 17.0.4, InputSystem 1.19.0, TextMeshPro, Timeline, UGUI, Test-Framework.

**NICHT installiert (obwohl GDD/CLAUDE.md sie nennen):** kein Firebase-Package (Auth/RTDB/Analytics/Crashlytics/Messaging), kein Photon/Photon Fusion, kein `com.unity.purchasing` (Unity IAP).

## 54. Weitere Services
- **Notifications:** `NotificationService` (Templates aus `notifications.json`, Opt-In-Persistenz, In-Memory-Scheduling, Opt-Out-Cancel-Fix) — Verwaltung real, aber die `AndroidNotificationCenter`-OS-Aufrufe sind TODO (Package ist installiert). **[SKELETT+Logik]**
- **Analytics:** `FirebaseAnalyticsService` loggt nur in die Konsole. **[SKELETT]**
- **Cloud-Functions** (TypeScript, 8 Endpoints unter `Server/`): Skelett, clientseitig nicht angebunden (kein Firebase-SDK).

---

# TEIL IX — Datenbestand (Ground Truth aus `Resources/Data/`)

Alle Querverweise sind 100 % konsistent (0 tote Referenzen).

| Datei | Inhalt |
|-------|--------|
| `cards.json` | **162 Karten** (40/32/35/36/14/5 nach Rarity; 9 Event, 6 Premium, 10 Prestige, 2 Sternkarten-Tempel, 0 SaisonPass-Flag, 4 Sammelset-Belohnungen) |
| `abilities.json` | **317 Abilities** (285 echte Karten-Skills `skill_*` + 32 ungenutzte generische; Passive 247 / ActiveOnSpecial 70) — UTF-8-BOM! |
| `worlds.json` | **10 Welten, 100 Nodes** (80 Normal + 10 MiniBoss + 10 WorldBoss) |
| `heroes.json` | 5 Helden (magnitude 5/1/0/3/20) |
| `runes.json` | 18 Runen (Element/Angriff/Verteidigung/Speed/Hero/Kombo/Mana) |
| `fusion_recipes.json` | 10 Rezepte (4 hidden) |
| `login_rewards.json` | 30-Tage-Zyklus |
| `star_temple.json` | 6 Eintausch-Optionen (Bronze 1/Silber 5/Gold 15/Platin 50 SP) |
| `premium_shop.json` | 3 permanent (300💎) + 3 rotierend (800💎) |
| `events.json` | 5 saisonale Events |
| `prestige_balancing.json` | 5 Stufen (Normal–IV) |
| `story_fragments.json` | Mythologie + 10 Fragmente + 8 NPCs + 6 Säulen |
| `collections.json` | 4 Sammelsets |
| `material_drops.json` | 18 Drop-Nodes, 19 Materialien |
| `tutorial.json` | 8 Schritte |
| `notifications.json` | 5 Push-Vorlagen |
| `saison_pass.json` | Saison 1, 30 Stufen, Free 12 + Premium 18 Belohnungen |
| `packs.json` | 4 Packs |
| `achievements.json` | 10 Erfolge à 4 Tiers |
| `quests.json` | 20 Quests (10 Daily + 10 Weekly) |

---

# TEIL X — GDD (DESIGN.md) vs. Code: die wichtigsten Diskrepanzen

| # | GDD-Behauptung | Code-Realität | Quelle |
|---|----------------|---------------|--------|
| 1 | Mana „+1/Runde bis max. 10" | **Konstant 3, kein Anstieg, kein Cap** | `BattleEngine.cs:230/237`, `BattleState.cs:108-111` |
| 2 | COST = Mana-Preis (4–50) | COST nur Deck-Budget (≤200) + Heavy-Gate (>30); **jede Karte 1 Mana** | `BattleEngine.cs:26/88/115`, `DeckValidator.cs:16` |
| 3 | Schaden `baseAttack × Element × (1+buff) × (1−def)` | `CurrentAttack × Element` (keine Buff/Def-Faktoren) | `BattleEngine.cs:183-185` |
| 4 | Sudden-Death (doppelter Schaden) ab Runde 50 | **Kein** Sudden-Death; Entscheidung nach Helden-HP | `BattleEngine.cs:256-257` |
| 5 | Heldenpassiv aktiv im Kampf | **Nicht verdrahtet** → wirkungslos | `BattleBootstrap.Build` setzt Passiv nicht |
| 6 | Boss-Phasen: Mini/World unterschiedlich, 1.5×-Ultimate, Prestige 3/4 Phasen | **Eine** generische Phase-2 (`Max(200,+50%)` + ≤3 Karten), nur Gott-Stufe | `BattleEngine.cs:527-568` |
| 7 | 131 / 158 Karten | **162 Karten** | `cards.json` |
| 8 | Rarity-Verteilung 40/32/24/20/10/5 | **40/32/35/36/14/5** | `cards.json` |
| 9 | 7 Götter | **8 Götter** | `cards.json` |
| 10 | Save-Schema v3 | **v4** (+ QuestSaceSlice, ClaimedCollectionSetIds) | `SaveMigrator.cs:12` |
| 11 | Firebase „Source-of-Truth" / Realtime DB | **Rein lokales JSON, kein SDK** | `manifest.json`, `FirebaseSaveService.cs` |
| 12 | Photon-Multiplayer, `INetworkService`/`PhotonNetworkService` | **Existiert nicht** (kein Package, keine Klasse) | `manifest.json`, `GameInstaller.cs` |
| 13 | IAP Unity IAP + Billing v6 | Lokaler Stub, kein `com.unity.purchasing` | `UnityIapService.cs`, `manifest.json` |
| 14 | Amateur „+50 % HP" | **×1.25 (+25 %)** auf ATK+HP | `NodeDifficulty.cs:44-51` |
| 15 | Idle-Income „150.000+/Tag" | rechnerisch 233.600/Tag (+ greift erst ab Prestige-Upgrade) | `worlds.json`, `PrestigeStufe.cs` |
| 16 | Karten-/Scrap-/Material-Drops als Node-Belohnung | Nur Gold/EXP/Sterne; Material-Drops nie ausgelöst | `BattleScreen.ApplyRewardsAsync` |

Zahlreiche `<summary>`-Kommentare verweisen auf falsche GDD-Kapitel (z.B. „DESIGN.md 17.1/17.2" für IAP/Packs — dort steht Lokalisierung; „16.3" für Saison-Pass — dort stehen Gilden). Die Monetarisierung lebt im GDD real in Kap. 13 + Intro-Zeile 49.

---

# TEIL XI — Was zum spielbaren MVP fehlt (Verdrahtungs-Lücken)

Die teuerste Arbeit ist bereits getan (die Domain-Logik existiert). Was fehlt, ist überwiegend **Anbindung**. Priorisierte Liste:

### A. Kampf-kritisch (blockiert Kern-Loop)
1. **UI-Mana-Gating-Bug fixen** — `BattleScreen` muss gegen `ManaPerCard` (1) statt `def.Cost` prüfen, sonst sind teure Karten unspielbar.
2. **Helden-Passivs verdrahten** — `BattleBootstrap.Build` muss `PlayerHeroPassiv`/`EnemyHeroPassiv` aus `heroes.json` + gewählter Rasse setzen.
3. **Starter-Karten ins Deck legen** — der neue Spieler hat sonst ein leeres Deck und kann nicht kämpfen.
4. **Karten-EXP-Quelle** — ohne EXP-Vergabe scheitern alle Karten-Upgrades.

### B. Progression & Belohnung
5. **Quest-/Achievement-Hooks aufrufen** (`OnBattleWon`, `OnBossDefeated`, `OnCardPlayed` … aus `BattleScreen`/Settlement) — sonst kein Quest-/Erfolgs-Fortschritt.
6. **SeasonResetService im Hub ticken** + korrekte Reset-Anker nutzen.
7. **Login-Karten-Belohnung reparieren** — derzeit Müll-`CardInstance` mit `card_random_Nstar`-ID statt echter Karten-Auswahl.
8. **Sternkarten-Tempel-Belohnung ausgeben** (außer Mythic-Fragment derzeit TODO).
9. **Material-Drops im Settlement auslösen** (`MaterialDropService.RollAndAwardAsync` bei Boss-Sieg).
10. **Prestige-Kampfwirkung** — `ScaleEnemyStats`/`ScaleGoldDrop` im Kampf anwenden.

### C. Engagement & Monetarisierung
11. **Shop-Screens an Services anbinden** (`ShopController`, `DailyShopService`, `SaisonPassService`, `IIapService`) + Karten-Materialisierung aus Rarities.
12. **Saison-Pass-XP-Trigger** (`AwardXpAsync` aus Quests/Arena/Battle).
13. **Tutorial verdrahten** (Trigger-Events feuern, Overlay pushen).
14. **Auto-Battle-Modus** im Battle-Screen.

### D. Online (GDD-Phase 5, größter Block)
15. **Netzwerk-Backend** (Firebase-SDK + Photon) — Voraussetzung für echtes PvP, Gilden-Sync, Realtime-Chat, server-weiten Dieb, Leaderboards, Cloud-Save, IAP-Validierung, Replay-Cross-Check.
16. **Social-UI an Domain-Services anbinden** (Arena/Guild/Chat/Friends/Thief — die Logik ist da, die Screens nutzen sie nicht).

### E. Story & Politur
17. **Die zwei Enden (Welt 10)** — Auswahl-UI + Branching + `EndingChoice` schreiben.
18. **Lokalisierte Karten-Namen** in DeckBuilder/Codex/Battle (statt Roh-IDs); Codex-Element-Filter „Erde" ergänzen.
19. **Collection-Trade Hub-Zugang** + „1× pro Save"-Sperre.

---

# Anhang A — Bekannte Bugs & Code-interne Risiken

| Befund | Quelle |
|--------|--------|
| UI prüft Karten-Spielbarkeit gegen `def.Cost` statt `ManaPerCard` (teure Karten unspielbar) | `BattleScreen.cs:422-440` |
| Helden-Passivs im Produktionspfad nicht gesetzt → wirkungslos | `BattleBootstrap.Build` |
| Login-Karten erzeugen Müll-`CardInstance` (`card_random_4star` als Definition-ID) | `HubScreen.cs:225-232`, `LoginRewardController.cs:159-170` |
| Sternkarten-Tempel bucht ab, gibt (außer Mythic) keine Belohnung | `TempelScreen.RunExchangeAsync:271-275` |
| Quest-/Achievement-Hooks ohne Aufrufer → kein Fortschritt | projektweite Suche |
| Collection-Set mehrfach tauschbar (`ClaimedCollectionSetIds` ungeprüft) | `CollectionService.ExchangeAsync` |
| Toter Parallel-Code `CollectionExchangeService` (Präfix-Bug, nie registriert) | `Domain/Collection/CollectionExchangeService.cs` |
| `DeckBuilderService` (Auto-Vorschlag) vollständig, aber UI-Button = Toast | `DeckBuilderScreen.cs:356-360` |
| Doppeltes Solaris-Rezept (zwei Rezepte → gleiche Karte) | `fusion_recipes.json` |
| Drei widersprüchliche Pack-Kataloge (JSON ≠ UI ≠ GDD) | `packs.json` vs. `ShopScreen.cs` |
| Drei konkurrierende Login-Systeme (nur Tempel real) | Tempel / DailyRewardService / QuestCenter-Tab |
| `BattleController` ist ein Sofort-Sieg-Stub (toter Code, nicht genutzt) | `BattleController.cs:81-84` |
| Guild-UI sagt „Stufe 20", Code-Konstante = 25 | `GuildScreen.cs:86` vs. `BalancingConfig` |
| `world.sandreich.memory` fehlt evtl. in strings.csv (Welt-2-Fragment zeigt Rohkey) | `strings.csv` |
| Material-Drop-RNG nutzt `System.Random` (nicht deterministisch wie Battle) | `MaterialDropService.cs:24` |
| `held_elfen` magnitude 0 + 28 Karten-Skills magnitude 0 (Balancing-Platzhalter?) | `heroes.json`, `abilities.json` |

---

# Anhang B — Methodik & Verlässlichkeit

Dieses Dokument entstand aus einer code-verifizierten Bestandsaufnahme: 13 thematische Code-Bereiche wurden parallel gegen den echten C#-Code und die JSON-Daten geprüft, plus eine unabhängige Gegenprüfung der kritischsten Querschnitts-Fakten (Enums, Karten-/Welt-Anzahl, Mana-Mechanik, Save-Version, Package-Bestand). Alle Zahlen wurden aus den Quelldateien gelesen bzw. gezählt, nicht aus dem GDD übernommen. Wo der Code vom GDD abweicht, ist der Code maßgeblich.

> **Nächste Aktualisierung:** sobald die Verdrahtungs-Lücken aus Teil XI geschlossen sind — dann wandern Systeme von **[DOMAIN-ONLY]**/**[SKELETT]** nach **[LIVE]**.
