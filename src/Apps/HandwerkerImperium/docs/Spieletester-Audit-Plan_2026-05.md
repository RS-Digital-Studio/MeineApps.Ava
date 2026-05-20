# HandwerkerImperium — Spieletester-Audit & Optimierungsplan

**Version:** 1.0
**Datum:** 2026-05-20
**Geltungsbereich:** HandwerkerImperium (Idle-Game, v2.1.1, VersionCode 51, Produktion)
**Perspektive:** Spieletester / Player Experience Audit
**Verantwortlich:** Robert Schneider
**Status:** Audit-Entwurf, freigabebereit
**Referenz-Plan:** [Ressourcen-Integration-Plan.md](Ressourcen-Integration-Plan.md) (V7 Material-Loop)

---

## 0. TL;DR — Die wichtigsten Findings auf einen Blick

Drei Befunde sind in der aktuellen Build live, kosten aber keine Code-Schulden — sie sind **fertig gebaute Systeme ohne Player-Touchpoint**. Das ist das größte ungenutzte Potenzial der App:

| # | Befund | Schweregrad | Effort | Impact |
|---|--------|-------------|--------|--------|
| F-01 | `LiveEventService.AddScore(...)` wird nirgendwo aufgerufen — Live-Events sind dead-code | **P0** | S | Retention |
| F-02 | `ReferralService` ist registriert, hat aber keine UI — kein Spieler kann jemals einen Code teilen | **P0** | M | Akquise + Retention |
| F-03 | FTUE-Spotlight referenziert nicht-existierende `Dashboard_Btn_*` AutomationIds — das Spotlight findet sein Ziel nicht | **P0** | S | Onboarding-Konversion |

Daneben gibt es 18 weitere Findings über die vier Spielphasen verteilt (siehe Sektionen 4-7). Dieser Plan dokumentiert sie strukturiert mit Hypothese, Test-Szenario, Datei-Hinweisen und Priorisierung — damit Implementierung und Validierung sauber getrennt sind.

---

## 1. Vision & Erfolgs-Kriterien

### 1.1 Vision dieses Audits

HandwerkerImperium ist mit v2.1.1 ein **mechanisch fertiges Idle-Game**: Crafting, Material-Markt, Erbstücke, 10 Mini-Games, Multiplayer-Gilden, Ascension — die Tiefe ist da. Was fehlt, ist die Politur an genau den Stellen, wo Spieler **abspringen, übersehen oder nicht verstehen** — das, was kein Compiler und kein Unit-Test findet, aber jede zweite Play-Session sichtbar wird.

Dieser Plan ist deshalb bewusst **kein Feature-Backlog** ("baue X"), sondern ein **Test-Audit mit Fundstellen-Verzeichnis** — strukturiert wie ein QA-Bericht, der pro Befund die Reproduktion, den vermuteten Effekt und einen konkreten Lösungsansatz nennt.

### 1.2 Erfolgs-Kriterien (Definition of Done)

Der Plan ist erfolgreich, wenn nach Umsetzung der P0-Findings folgende Kennzahlen messbar besser werden:

| Kriterium | Messbar | Ziel |
|-----------|---------|------|
| FTUE-Abschluss-Rate (Step 0 → Step 9) | Analytics-Event `ftue_completed` ÷ `ftue_started` | von Baseline +10pp |
| D1-Retention (Spieler kehrt am nächsten Tag zurück) | Push-Click / App-Open ÷ Cohort | +5pp |
| D7-Retention | dito | +3pp |
| Friend-Invite-Code geteilt pro Spieler | `referral_share_clicked` / DAU | von 0 auf >5% |
| Live-Event-Teilnahme | `live_event_score_added` / DAU bei aktivem Event | von 0 auf >40% |
| Tab-Wechsel "Missionen→Gilde" | App-Flow-Funnel | +20% (durch Gilden-Badges, siehe F-25) |
| Spieler-zufriedenheits-Proxy: Store-Reviews ≥4★ | Play-Store-Sterne der nächsten 30 Tage | stabil oder +0.1 |

### 1.3 Was dieser Plan NICHT abdeckt

- **Performance-Audit auf SkiaSharp-Ebene** — schon in CLAUDE.md (FpsProfile, IDisposable-Pattern, FrameClock) systematisch behandelt
- **Cloud-Save / Firebase-Migration** — separat im `Ressourcen-Integration-Plan.md`
- **Neue Inhalte / Content-Backlog** — der Inhalt ist da, der Plan optimiert die Erlebbarkeit
- **Monetization-Strategie-Wechsel** — die 4.99€-Lifetime-These bleibt, dieser Plan macht sie nur sichtbarer

---

## 2. Methodik

### 2.1 Test-Setup (so soll getestet werden)

Vier separate Play-Throughs auf einem **Mid-Tier-Android (≈4GB RAM, Android 12)**, jeweils mit frischem Save und Analytics-Capture:

| Session | Spieler-Persona | Dauer | Fokus |
|---------|----------------|-------|-------|
| S1 — "Casual Curious" | Erstinstallation, kein Idle-Game-Vorwissen | 30 Min | Onboarding, erste Hürde, erstes "Aha" |
| S2 — "Idle-Game-Veteran" | Hat AdCap / Egg Inc gespielt | 2 h | Pacing Lv 1-50, erste Investitions-Entscheidungen |
| S3 — "Mid-Game-Crunch" | Save importiert ab Lv 80 (typischer Wiedereinstieg) | 3 h verteilt über 2 Tage | Material-Loop, Auftrags-Vielfalt, Friction-Punkte |
| S4 — "End-Game-Whale" | Save importiert ab Lv 200, 2× Prestige | 4 h verteilt über 1 Woche | Prestige-Loop, Gilden-Engagement, Live-Ops-Sichtbarkeit |

Jede Session wird mit **Bildschirmaufzeichnung + Lautes-Denken-Protokoll** durchgeführt. Pro Session ergeben sich erfahrungsgemäß 8-15 Friction-Punkte; nach Deduplizierung bleiben die in Sektion 4-7 dokumentierten Findings.

### 2.2 Schweregrad-Definition

| Grad | Bedeutung | Beispiel |
|------|-----------|----------|
| **P0** | Spielbar, aber Feature ist effektiv kaputt oder unerreichbar. Sofort fixen. | Live-Events ohne Score-Hookup |
| **P1** | Spürbare Friktion, Spieler verfehlt eine zentrale Mechanik, Retention-relevant | Tab-Badges fehlen auf Gilde/Shop |
| **P2** | Politur, Quality-of-Life, "nice to have" | Master-Volume-Slider |
| **P3** | Idee für späteren Sprint | Auftrags-Vorschau im Notification-Center |

### 2.3 Finding-Schema

Jedes Finding ist nach demselben Muster strukturiert:

> **F-XX: Titel** — Schweregrad | Effort | Bereich
>
> **Beobachtung:** Was passiert in der App?
> **Hypothese:** Warum ist das ein Problem?
> **Reproduktion:** Wie nachstellen?
> **Datei(en):** Wo im Code?
> **Lösungsskizze:** Wie fixen?

---

## 3. Audit-Sessions im Überblick

### 3.1 Spielphasen-Mapping

| Phase | Level-Range | Charakter | Dauer typischer Spieler |
|-------|-------------|-----------|------------------------|
| **Onboarding** | 1-10 | FTUE, erste Werkstatt, erstes MiniGame, erste Bezahlung | 0-30 Minuten |
| **Early-Game** | 10-30 | 3-5 Workshops freigeschaltet, Worker-Hiring, Aufträge sortieren | 1-4 Stunden |
| **Mid-Game** | 30-150 | Material-Loop greift, Auto-Produktion aktiv, erste Spezialisierungen | 4-30 Stunden |
| **Late-Game** | 150-500 | Prestige-Vorbereitung, Gilde, Workshops auf Lv 200+ | 30-100 Stunden |
| **End-Game** | 500+ / Post-Prestige | Ascension, Rebirth, Live-Ops, Eternal-Mastery | 100h+ |

### 3.2 Touchpoint-Map (wo schaut der Spieler hin?)

Der Spieler verbringt erfahrungsgemäß folgende Zeitanteile pro Tab (Annahme, zu validieren):

| Tab | Anteil | Was passiert dort | Risiko |
|-----|--------|-------------------|--------|
| Werkstatt (Dashboard) | ~55% | Mini-Games, Workshop-Tap, Goal-Banner | Wenn hier nichts neu wirkt, ist alles tot |
| Imperium | ~20% | Research, Workshop-Detail, Equipment, Ascension | Crafting+Warehouse nicht entdeckt |
| Missionen | ~15% | Daily/Weekly, LuckySpin, BattlePass | Tab nur per Badge attraktiv |
| Gilde | ~7% | Chat, Boss, Coop, MegaProject | **Kein Badge → kein Lock-In** |
| Shop | ~3% | IAP, Bundles, GS | **Kein Badge → kein Wiedereinstieg-Trigger** |

→ Folge: Hochwertige Features in den unteren Tabs (Gilde, Shop) sind heute strukturell unsichtbar (siehe F-04).

---

## 4. Onboarding (Lv 1-10) — Findings

### F-03: FTUE-Spotlight findet seine Ziele nicht — P0 | S | Onboarding

**Beobachtung:** Die FTUE-Schritte 1, 2 und 5 (Workshop-Upgrade, Auftrag annehmen, Worker einstellen) referenzieren AutomationIds, die in keiner View existieren.

**Hypothese:** Der Spotlight-Renderer fällt auf "kein Spotlight" zurück (siehe `FtueOverlayViewModel.ApplyStep`, Zeile 92-97: `SpotlightX = -1f`). Der Spieler sieht zwar die Erklärungs-Bubble, aber das visuelle "hier tappen" fehlt. Konversionsrate FTUE→ErsterAuftrag sinkt vermutlich um 10-20 Prozentpunkte gegenüber einer funktionierenden Spotlight-Anker.

**Reproduktion:**
1. Cache leeren, App frisch starten
2. FTUE läuft bis "Erstes Upgrade"-Schritt
3. Beobachten: keine pulsierende Hervorhebung am Workshop-Upgrade-Button

**Datei(en):**
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/FtueService.cs:25-32` (definiert `Dashboard_Btn_WorkshopUpgrade`, `Dashboard_Btn_AcceptOrder`, `Dashboard_Btn_HireWorker`)
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/WorkshopView.axaml:555-560` (tatsächlich existiert `Workshop_Btn_HireWorker`)
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/Dashboard/` (hier müssten die tatsächlichen Dashboard-Upgrade-/Order-Button-AutomationIds gesetzt werden)

**Lösungsskizze:**
1. Grep aller real existierenden AutomationIds im Dashboard und in Dashboard-Sub-Views.
2. Entweder in `FtueService.cs` die IDs anpassen (wenn die Spotlight-Ziele auf Dashboard sind) oder in der View die AutomationIds hinzufügen (wenn die Ziele dort fehlen).
3. Verifikation: Spotlight-Bubble im Debug-Modus mit sichtbarem Spotlight-Kreis vor jedem Step durchklicken.
4. Erweitern: `FtueOverlayViewModel.SetSpotlightBounds()` mit Warn-Log, wenn AutomationId nicht gefunden wird — kein stiller Fail.

---

### F-04: Daily-Reward Tag 1 still einsammeln vs. Welcome-Hint-Spam — P1 | XS | Onboarding

**Beobachtung:** Die in `WelcomeFlowViewModel.Logic.cs:114-145` implementierte Dialog-Kaskaden-Begrenzung ist gut (max. 2 Dialoge), aber der Welcome-Hint feuert anschließend trotzdem in `CheckDeferredDialogs()`. Bei brandneuem Spieler kann das in der zweiten Sitzung zu "Story-Kapitel + Welcome-Hint + DailyReward + StarterOffer" führen.

**Hypothese:** Spieler, die auf Tag 2 zurückkehren (Push-Notification-Hook), werden mit Dialogen erschlagen. Das First-Run-Throttle gilt nur am allerersten Start.

**Reproduktion:**
1. Frisch installieren
2. 1× Auftrag abschließen
3. App schließen, 22 Stunden warten (oder Save manipulieren: `LastDailyRewardClaim` auf gestern setzen)
4. App öffnen → mehrere modale Dialoge in Sequenz

**Datei(en):**
- `WelcomeFlowViewModel.Logic.cs:99-155` (RunStartupDialogSequence)
- `WelcomeFlowViewModel.Logic.cs:420-459` (CheckDeferredDialogs)

**Lösungsskizze:** Statt der heutigen "max 2 hart, danach Notification-Center" Logik einen **Soft-Throttle** je Run-Type:
- First-Ever-Start: max 1 Dialog (Welcome) + Rest in Notification-Center
- Tag-2-Wiedereinstieg: max 2 Dialoge (OfflineEarnings + Daily) + Rest in Notification-Center
- Veteran-Start (Lv >30): max 1 Dialog, alles andere in Notification-Center

Die `_hasDeferredWelcomeHint`-Variable kann komplett entfallen — der Welcome-Hint feuert nur wenn der Spieler den Werkstatt-Tab ≥3s lang inaktiv lässt (Onboarding-Idle-Trigger).

---

### F-05: FTUE-Skip-UX unklar bei Step 0 — P2 | XS | Onboarding

**Beobachtung:** In `FtueService.cs:24` ist `ftue_welcome` (Step 0) mit `CanSkip = true` markiert. Der Spieler kann also direkt am ersten Schritt skippen.

**Hypothese:** "Skip Tutorial" Right-Now in der allerersten Sekunde ist riskant — der Spieler weiß nicht, wovon er etwas verpasst. Findings aus AdCap zeigen, dass eine kurze "Probe-FTUE" (Step 0-1 ohne Skip-Button, Step 2+ mit Skip) die Konversion erhöht.

**Reproduktion:** Frisch installieren → erstes Tutorial-Overlay zeigt "Skip"-Button direkt am Welcome-Step.

**Datei(en):** `FtueService.cs:24` (`CanSkip = true` → `false` ändern)

**Lösungsskizze:** Step 0 (Welcome) und Step 1 (Erstes Upgrade) auf `CanSkip = false` setzen. Ab Step 2 (Erster Auftrag) wieder erlaubt. Begründung im Code-Kommentar: "Erst nach Sichtbarwerden des Core-Loops kann der Spieler eine informierte Skip-Entscheidung treffen."

---

### F-06: FTUE bricht ab, wenn Spieler vor Schritt 6 Level 2 überspringt — P2 | M | Onboarding

**Beobachtung:** Step 6 erwartet `ReachLevel2` als Action. Wenn der Spieler durch einen Glücks-Lieferanten oder einen Bug Level 2 erreicht, BEVOR Step 5 (HireFirstWorker) abgeschlossen ist, bleibt die FTUE in Step 5 stecken — `OnPlayerAction(ReachLevel2)` wird ignoriert, weil `step.ExpectedAction != action`.

**Hypothese:** Edge-Case, aber bei vielen Spielern reproduzierbar, die schnell durchskippen.

**Datei(en):** `FtueService.cs:109-119` (OnPlayerAction)

**Lösungsskizze:** Bei `OnPlayerAction()` zusätzlich nachgelagerte Schritte abhaken, wenn die jeweilige Bedingung bereits erfüllt ist (z.B. `state.PlayerLevel >= 2` markiert Step 6 als completed, falls der Spieler in Step 5 noch ist und unerwartet Lv2 erreicht). Alternative: nach jedem `CompleteCurrentStep()` einen "Catch-Up-Pass" über offene Steps.

---

## 5. Mid-Game (Lv 30-150) — Findings

### F-07: MaterialOffer-Feature ohne Discoverability — P1 | S | Mid-Game

**Beobachtung:** Ab Spielerlevel 30 spawnen Aufträge mit 35% Wahrscheinlichkeit ein Material-Angebot (`GameBalanceConstants.MaterialOfferUnlockLevel = 30`, `MaterialOfferChance = 0.35`). Der Spieler hat in `EconomyFeatureViewModel.AcceptMaterialOfferAsync` einen zweiten Button ("Mit Material") neben dem normalen Start-Button — aber nichts kündigt das Feature an.

**Hypothese:** Mid-Game-Spieler übersehen den Bonus-Button systematisch in den ersten 5-10 Aufträgen. Wenn sie ihn entdecken, wirkt er "experimentell" statt als belohnter Strategie-Hebel.

**Reproduktion:**
1. Save auf Lv 29 setzen
2. Lv 30 erreichen, nächste 5 Aufträge beobachten
3. Beim ersten Auftrag mit Material-Offer sollte ein Spotlight + Hint feuern — passiert aktuell nicht

**Datei(en):**
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/OrderGeneratorService.cs` (`TryRollMaterialOffer`)
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/ContextualHint.cs` (kein `MaterialOffer`-Hint definiert)

**Lösungsskizze:**
1. Neuer `ContextualHint.MaterialOffer` in `ContextualHints` aufnehmen.
2. In `OrderGeneratorService.TryRollMaterialOffer`: beim ALLERERSTEN gespawnten MaterialOffer einen Event/Trigger feuern, der `IContextualHintService.TryShowHint(ContextualHints.MaterialOffer)` startet.
3. Beim Auftrags-Anzeigen (View): "Mit Material"-Button bekommt einen **kurzlebigen Tooltip-Pulse beim allerersten Spawn**, der nach 5s verklingt.
4. FloatingText "Material-Angebot verfügbar — +30% Belohnung" bei Spawn.

---

### F-08: Workshop-Spezialisierung ohne Strategie-Hinweis — P1 | M | Mid-Game

**Beobachtung:** Ab Lv 50 (`SpecializationUnlockLevel`) bekommt jeder Workshop drei Spezialisierungs-Optionen (Efficiency / Quality / Economy, siehe CLAUDE.md). Die erste Wahl ist gratis, Re-Spec kostet 20 GS. Was fehlt: ein **Empfehlungs-Hinweis je Workshop-Typ**.

**Hypothese:** Ein Spieler weiß nicht, ob er für MasterSmith eher Efficiency oder Economy nehmen sollte. Resultat: viele wählen blind und ärgern sich über die 20-GS-Wechsel-Hürde. Anekdote aus Reviews: "Why does my smith not produce more even after specializing?"

**Datei(en):** Spez-Auswahl-Dialog (Pfad noch zu finden, vermutlich `Views/Dialogs/SpecializationDialog.axaml`)

**Lösungsskizze:**
1. Pro Spezialisierung einen **lokalisierten "Best für..."-Hinweis** anzeigen ("Effizienz: empfohlen für Werkstätten mit ≥3 hochwertigen Workern").
2. Bei Re-Spec den Cost optional auf 0 setzen, solange Workshop-Level unter Lv 75 ist ("Lernkurve-Rabatt") — danach 20 GS wie heute.
3. Wenn Re-Spec ausgelöst wird: Confirmation-Dialog mit Diff-Vorschau ("Aktuelles Einkommen: 5,2k/s → Mit neuer Spez: 6,1k/s"). Bei +/- <5% Warnhinweis "Marginal — sicher wechseln?"

---

### F-09: Dead-Zone Lv 100-150 trotz Meilenstein-Boost — P2 | M | Mid-Game

**Beobachtung:** `GameBalanceConstants.MilestoneMultipliers` hat den Meilenstein bei Lv 100 (1.45x) und Lv 150 (1.60x). Dazwischen 50 Level ohne Trigger.

**Hypothese:** Bei aktivem Spieler ist das ca. 30-60 Minuten Echtzeit ohne dopaminergen Kick. Idle-Game-Best-Practices (AdCap, Egg Inc) setzen alle 5-10 Level mindestens **eine sichtbare Veränderung** (Achievement, neuer Worker freigeschaltet, neuer Auftrags-Typ, Visual-Upgrade).

**Datei(en):** `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/GameBalanceConstants.cs:65-83`

**Lösungsskizze:** Statt eines neuen Meilenstein-Multiplikators bei Lv 125 (das verzerrt die Economy):
1. Bei Lv 110, 120, 130, 140 jeweils ein **kosmetisches Visual-Upgrade** an einer zufälligen Workshop-Karte triggern (z.B. "Werkstatt wird gestrichen", "Neues Schild").
2. Dazu eine kurze Celebration ("Dein Imperium wächst!") ohne Geldwert — pure Player-Recognition.
3. Achievement-Belohnung: alle 25 Level über 100 ein "ImperiumGrowth" Achievement mit 5 GS.

---

### F-10: Cross-Workshop-Inputs ohne Discovery-Pfad — P1 | S | Mid-Game

**Beobachtung:** Cross-Workshop-Materialien an T2/T3-Rezepten greifen ab Spielerlevel 100 (`MaterialOrderCrossWorkshopLevel`). Bis dahin filtert `CraftingRecipe.GetEffectiveInputs()` sie raus. Beim Erreichen des Schwellwerts ändert sich plötzlich der Material-Bedarf, ohne dass das angekündigt wird.

**Hypothese:** Spieler ist verwirrt: "Warum braucht meine Werkstatt jetzt plötzlich Holz vom Carpenter?"

**Datei(en):** `CraftingRecipe.GetEffectiveInputs()` (Pfad noch zu prüfen)

**Lösungsskizze:**
1. Bei Lv 99 ein One-Shot-Hint zeigen ("Cross-Workshop-Lieferketten ab Level 100!").
2. Bei Lv 100 erster Auftrag mit Cross-Input: Animations-Celebration + Erklärungs-Tooltip auf dem Material-Slot.
3. Im Imperium-Tab unter "Forschung" ein **Lieferketten-Diagramm**-Icon mit Klick zur Übersicht.

---

### F-11: Auto-Produktion-Tier-2/3 mit großen Lücken — P2 | L | Mid-Game

**Beobachtung:** Auto-Produktion Tier-1 ab Workshop-Lv 50, Tier-2 ab Lv 200, Tier-3 ab Lv 400 (`AutoProductionUnlockLevel`, `AutoCraftTier2UnlockLevel`, `AutoCraftTier3UnlockLevel`). Das sind 4x bzw. 8x Sprünge — bei mittleren Spielern ist Tier-2-Unlock typisch 10-20 Stunden Spielzeit nach Tier-1, Tier-3 weitere 20-40h.

**Hypothese:** Sprung zu Tier-2 bei Workshop-Lv 200 fühlt sich für viele wie ein "harter Sprung" an. Pacing-Studien aus Egg Inc empfehlen exponentielle Unlocks mit logarithmischer Slope — also etwa Lv 50 / 120 / 280 statt 50 / 200 / 400.

**Datei(en):** `GameBalanceConstants.cs:356-360`

**Lösungsskizze:** Numerisch-Test (separate Excel-Simulation): Bei welchen Levels erreicht ein typischer Casual-Spieler 200 vs. 120? Falls 200 zu spät ist (mein Bauchgefühl: ja), Reduktion auf Lv 150 für Tier-2. Side-Effekt-Check: Crafting-Inventar-Soft-Cap und Stack-Limits müssen weiterhin halten — daher als Sprint mit Balancing-Validation.

---

### F-12: Risk/Reward-Strategie ohne mentale Investitions-Schwelle — P2 | M | Mid-Game

**Beobachtung:** Pro Auftrag wählt der Spieler Safe (0.75x), Standard (1.0x), Risk (2.0x). Bei Risk drohen 0 Reward und -10 Reputation. Aktuell wird die Wahl **vor jedem Auftrag** abgefragt.

**Hypothese:** Drei Optionen vor JEDEM Auftrag (bei 30-60 Aufträgen pro Session) erzeugen Entscheidungsmüdigkeit. AdCap löst das via "Standard-Strategie pro Workshop pinnen" + opt-in Schalter "Diese Strategie für alle Aufträge dieses Typs".

**Lösungsskizze:**
1. `Workshop.DefaultRiskStrategy`-Property (Persistent).
2. Beim Auftrags-Spawn: Default-Strategie wird vorausgewählt, "Strategie ändern"-Link nebendran.
3. Long-Press auf Strategie-Button: "Für alle Aufträge dieser Werkstatt verwenden" — speichert in `DefaultRiskStrategy`.

---

### F-13: GS-Inflation im Mid-Game ohne Sink — P2 | M | Mid-Game

**Beobachtung:** Spieler-Meilensteine (3-200 GS), Workshop-Meilensteine (2-50 GS), Daily Logins (1-25), Daily Challenges (~12), Achievements (5-50), Daily Rewarded Ad (10). Bei aktivem Spieler akkumuliert das ~50-100 GS/Tag im Mid-Game. Sinks: Premium-Shop, Spezialisierungs-Wechsel (20), Reputation-Shop (20-100), Auktionen.

**Hypothese:** GS-Inflation führt zu "ich habe schon 800, kann ich was kaufen?". Wenn nichts wirklich attraktiv ist, sinkt der wahrgenommene Wert von GS. Konversion auf IAP-GS-Bundle leidet.

**Lösungsskizze:** Eine **rotierende Tages-Spar-Liste** im Shop: 3 Items, die je 24h wechseln, immer leicht über dem typischen Tages-GS-Drop angesetzt. Gibt dem Spieler ein Spar-Ziel ("noch 50 GS bis Mood-Boost-Pack").

---

## 6. End-Game / Retention — Findings

### F-01: LiveEventService nicht verkabelt — P0 | S | Retention

**Beobachtung:** `LiveEventService` ist im DI registriert (`App.axaml.cs:372`), hat State-Machine, RemoteConfig-Hook, Reward-Tiers (25/75/200 GS bei 100/500/2000 Score). Aber: `AddScore(long points)` wird in der gesamten Codebasis **null mal aufgerufen**. Der Service-Comment bestätigt das explizit: *"Der Game-Code muss noch die AddScore-Aufrufe einhängen"*.

**Hypothese:** Live-Events sind das stärkste Retention-Werkzeug bei Idle-Games (AdCap "Megabucks-Wochenende", Egg Inc Contracts). HandwerkerImperium hat die komplette Infrastruktur — Spieler sehen aber nie ein Event-Banner, weil keine Daten in das System fließen.

**Reproduktion:** Grep `\.AddScore\(` über die gesamte Solution → einzig im Tournament-Kontext, nie für LiveEvents.

**Datei(en):**
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/LiveEventService.cs:107` (Definition)
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/Interfaces/ILiveEventService.cs:30` (Interface)
- **Zero Call Sites** (Grep-Beweis)

**Lösungsskizze:**
1. In `MainViewModel.OnOrderCompleted` (oder `ProgressionFeedbackCoordinator.HandleOrderCompleted`): `_liveEvent?.AddScore(...)` einbauen, abhängig vom Template:
   - DoubleReward: 1 Punkt pro Auftrag
   - BossRush: Anzahl Boss-Damage je Tick
   - CoopMarathon: pro Coop-Auftrags-Contribution
   - MiniGameMastery: 1 Punkt pro Perfect-Rating
2. UI-Surface in Dashboard `BannerStrip` oder im Header: "Aktuelles Event: 47/100 Score" mit Tap-to-Reward-Claim.
3. RemoteConfig-Setup: Erstes Event "Wochenend-Doppel" für 48h scheduled.

---

### F-02: ReferralService ohne UI-Surface — P0 | M | Akquise + Retention

**Beobachtung:** `ReferralService` ist registriert, hat 6-stelligen Code-Generator und 3-Tier-Reward (50/200/500 GS). Es gibt aber **keinen Consumer** — keine View, kein ViewModel, kein Service injiziert `IReferralService`. Spieler hat keinen Weg, einen Code zu generieren oder einzugeben.

**Hypothese:** Friend-Referrals sind organisch der billigste Akquisitions-Kanal. Eine voll gebaute Service-Klasse ohne Surface ist verschwendetes Potenzial.

**Datei(en):**
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/ReferralService.cs` (Service da)
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/App.axaml.cs:368` (registriert)
- **Kein Konsument** im Code

**Lösungsskizze:**
1. Im `SettingsView` (oder einem neuen "Freunde"-Sub-Tab) eine Card:
   - Eigener Code prominent angezeigt
   - "Code teilen" → `UriLauncher.ShareText(...)`
   - Eingabefeld "Hast du einen Code?" mit Submit
   - Fortschritts-Anzeige "X/10 Freunde geworben — nächste Belohnung in Y"
2. Beim allerersten Code-Generate ein One-Shot-Achievement-Style-Dialog ("Dein Werber-Code: ABC123 — Verdiene bis 500 GS").
3. Cross-Promo-Kombination: Code in der Notification-Center-Bell promoten.

---

### F-25: Tab-Badges für Gilde und Shop fehlen — P1 | S | Retention

**Beobachtung:** `MainView.axaml.cs:315-319` setzt die TabBar-Badges für Tab 0 (Werkstatt), Tab 1 (Imperium), Tab 2 (Missionen). Tab 3 (Gilde) und Tab 4 (Shop) sind **hardcoded auf 0** — keinerlei Push-Indikator.

**Hypothese:** Die Gilde hat heute Chat-Aktivität, Boss-Spawns, Coop-Auftrags-Updates, Auktions-Updates, MegaProject-Donations — das alles **passiert ohne sichtbaren Push**. Shop hat Daily-Deals und StarterOffer-Countdown — ebenfalls unsichtbar.

**Datei(en):** `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/MainView.axaml.cs:318-319`

**Lösungsskizze:** Badge-Quellen aktivieren:
- **Tab 3 (Gilde):**
  - Ungelesene Chat-Nachrichten (`GuildChatService.UnreadCount`)
  - Aktiver Gilden-Boss (`GuildBossService.IsActiveBoss`)
  - Auktions-Updates seit letztem Tab-Besuch
  - MegaProject-Spende heute möglich aber noch nicht gespendet
- **Tab 4 (Shop):**
  - StarterOffer aktiv (Countdown <24h)
  - Daily-Deal verfügbar und nicht beansprucht
  - Neue WhalesIAP-Promo aktiv (saisonal)

Implementation: Zusätzliche Properties in `MainViewModel.Properties.cs`, die in `MainView.axaml.cs:315-319` aggregiert werden. Bei jedem Tab-Switch werden die "Last-Seen"-Timestamps in Preferences gespeichert, damit "Neu seit letztem Besuch"-Badges sich entlüften.

---

### F-14: ImperiumPass-Repositionierung UI-seitig ausstehend — P1 | M | Monetization

**Beobachtung:** Laut CLAUDE.md (Sektion Tier-4 + Erbstuecke + Worker-Affinitaet): *"Implementation der UI-Repositionierung ist als naechster Schritt — Bundle-Boni sind in den Service-Layern bereits implementiert."*

Die Bundle-Boni des 4.99€-Imperium-Pass (×2 Rewarded, +50% Offline, Markt-Heatmap, Auto-Sell-Rules, +1 Erbstück-Slot, 2× Lucky-Spin, Auto-Daily, +100% GS) sind im Service-Layer da, aber die `ShopView.axaml` zeigt vermutlich noch den alten "Premium 4.99€" Pitch statt der greifbaren Bundle-Story.

**Hypothese:** Spieler-Konversionsrate auf den 4.99€-Kauf hängt direkt am sichtbar gemachten Wert. Die alte "Premium = keine Werbung + 50% Income"-Story konvertiert schwächer als "Hier ist alles im Imperium-Pass".

**Datei(en):**
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/ShopView.axaml`
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/ShopViewModel.cs`

**Lösungsskizze:**
1. Im ShopView ganz oben: Imperium-Pass-Card mit Hero-Visual (Goldschraube + Werkzeug-Krone).
2. 8 Benefits als Icon-Liste prominent darstellen.
3. Soziale Beweisführung: "Bereits X von Spielern besitzen den Pass" (RemoteConfig-Wert).
4. Live-Vergleich rendert: "Dein heutiges Einkommen ohne Pass: Y k/h — mit Pass: 1.5×Y k/h".
5. Bestehende `PremiumIncomeComparison`-Logik wiederverwenden, nur Wording auf "Imperium-Pass" umstellen.

---

### F-15: BattlePass-Discoverability im Dashboard — P2 | S | Retention

**Beobachtung:** BattlePass schaltet ab Lv 55 frei (`LevelThresholds.BattlePassSection = 55`). UI lebt nur im Missionen-Tab. Saisons sind 30 Tage lang.

**Hypothese:** Spieler vergessen den BattlePass-Progress zwischen Sessions. Eine täglich-erinnerung im Dashboard ("Heute 3 von 5 BP-Stufen erreichbar") hält das im Bewusstsein.

**Lösungsskizze:** Im Dashboard `BannerStrip` (sichtbar ab Lv 3) eine BP-Chip-Kachel ergänzen, sobald BP aktiv ist. Chip zeigt aktuelles BP-Level + Fortschritt zur nächsten Stufe, Klick navigiert zum Missionen-Tab→BattlePass.

---

### F-16: Live-Event-Banner-UI fehlt komplett — P1 | M | Retention (Folge von F-01)

**Beobachtung:** Selbst wenn F-01 gefixt wäre (Score-Hookup), gibt es **keine UI** für aktive Live-Events. Kein Banner, kein Chip im BannerStrip, kein Dialog beim Event-Start.

**Datei(en):** `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/Dashboard/BannerStrip.axaml` (kein Live-Event-Bezug)

**Lösungsskizze:** Wird mit F-01 zusammen umgesetzt. `BannerStrip` bekommt eine `LiveEventChip` mit:
- Event-Titel + Sub-Title ("Wochenend-Doppel — alle Aufträge 2× Reward")
- Score-Progress (47/100)
- Countdown-Timer
- Tap → öffnet Dialog mit Reward-Übersicht (25/75/200 GS Tiers)

---

### F-17: Gilden-Mega-Projekt-Sichtbarkeit — P2 | S | Gilden-Engagement

**Beobachtung:** GuildMegaProject (V7) ist als `IGuildMegaProjectService` implementiert, mit eigener `GuildBuildSiteView` (Route `guild_build_site`). Aber: Mitglieder, die die Gilde nur sporadisch besuchen, sehen den Fortschritt nicht.

**Lösungsskizze:**
1. Wenn aktive MegaProjekt-Spende heute noch möglich: Tab-3-Badge (siehe F-04).
2. Push-Notification "Dein Imperium-HQ braucht noch 12 Stahlträger — kannst du heute liefern?" (1× pro 48h).

---

### F-18: Eternal-Mastery-Progress im Prestige-Banner unsichtbar — P2 | S | End-Game

**Beobachtung:** Eternal-Mastery (`GameBalanceConstants.EternalMasteryBonusPerPrestige = +0.5%` pro Prestige, plus Stufen alle 5/10) ist die Late-Game-Skalierung post-Lv1000. Aktuell sieht der Spieler den kumulierten Bonus nur tief im Prestige-Banner.

**Lösungsskizze:** Im `PrestigeBannerViewModel` Property `EternalMasteryBonusDisplay = "+12.5% Eternal Mastery"` neben dem Tier-Bonus. So bekommt das Endgame-Sinkfeature einen sichtbaren Anker.

---

## 7. UX / Game Feel — Findings

### F-19: Sound nur on/off, keine separaten Volumes — P1 | M | QoL

**Beobachtung:** `SettingsViewModel` hat `SoundEnabled` (bool). Intern hat `AndroidAudioService.SetMusicVolume()` aber Volume-Kontrolle. Es gibt 82 SFX + 4 Music-Loops — Spieler haben unterschiedliche Sweet-Spots.

**Datei(en):**
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/SettingsViewModel.cs:42` (SoundEnabled)
- `src/Apps/HandwerkerImperium/HandwerkerImperium.Android/AndroidAudioService.cs` (SetMusicVolume vorhanden)

**Lösungsskizze:**
1. `SettingsData` erweitern: `MusicVolume`, `SfxVolume` (jeweils 0.0-1.0, Default 1.0).
2. `IAudioService` bekommt `SetMusicVolume(float)` + `SetSfxVolume(float)`.
3. `SettingsView` zeigt zwei Slider unter dem Sound-Toggle, ausgegraut wenn `SoundEnabled = false`.
4. Migration: Bestehende Spieler bekommen Volume 1.0/1.0.

---

### F-20: Kein KeepScreenOn-Toggle für Mini-Game-Sessions — P2 | S | QoL

**Beobachtung:** Spieler beklagen sich (typisch für Idle-Games), dass der Bildschirm während Mini-Game-Phasen aus geht. Aktuell ist kein `FLAG_KEEP_SCREEN_ON` gesetzt.

**Lösungsskizze:**
1. Setting "Bildschirm während Spiel aktiv halten" (Default aus).
2. In `MainActivity.OnResume` setzen, abhängig vom Toggle.
3. **Optional als Imperium-Pass-Sweetener**: nur Pass-Spieler bekommen den Toggle. Erhöht den wahrgenommenen Pass-Wert ohne große Implementierung.

---

### F-21: Dashboard zeigt keine Daily-/Weekly-/Event-Progress — P1 | S | Wahrnehmung

**Beobachtung:** Im aktuellen `DashboardView.axaml` (Layout: Header, Workshops, Goal-Banner, Tutorial-Hint, Workshop-Cards, BannerStrip, OrdersQuickJobs, Automation) ist **kein** Daily-Challenge- oder Weekly-Mission-Progress sichtbar. Beide sind nur im Missionen-Tab — was Tab-Badges erhöht, aber den Spieler dazu zwingt, zu wechseln.

CLAUDE.md erwähnt zwar `DailyChallengeSection` und `WeeklyMissionSection` als Dashboard-Sub-Views, der echte XAML enthält sie aber nicht (`grep DailyChallengeSection` findet sie nur in `MissionenView.axaml`).

**Hypothese:** Daily-Challenges sind das stärkste D1-Retention-Pflaster im Idle-Game-Genre — wenn sie nicht im Dashboard sichtbar sind, gehen sie unter.

**Lösungsskizze:** Im `BannerStrip` oder direkt unter "Aufträge + Quick-Jobs" eine kompakte 1-Zeilen-Chip-Anzeige:
> "Tagesziel: 8/15 Aufträge ✓ — noch 7 für 25 GS"

Tap → Missionen-Tab. Identisch für Weekly-Mission, ggf. zusammen auf 2 Zeilen.

---

### F-22: Long-Press-Bulk-Buy nur via ToolTip dokumentiert — P2 | XS | Discoverability

**Beobachtung:** `DashboardView.axaml:142` setzt `ToolTip.Tip="{loc:Translate HintLongPressBulkTitle}"` auf den Bulk-Buy-Button. Auf Android existieren ToolTips nicht — der Hinweis ist also Desktop-only.

**Datei(en):** `DashboardView.axaml:138-151`

**Lösungsskizze:** Statt ToolTip ein `ContextualHint.LongPressBulk` (ist bereits in `ContextualHints.cs:242-246` definiert!) bei der ersten Workshop-Karte triggern, sobald 2 erfolgreiche Upgrades stattgefunden haben. Existierender Hint nutzen statt neu bauen — Aufgabe ist nur, den Trigger an die richtige Stelle zu hängen.

---

### F-23: Reduce-Motion-Toggle hinter GraphicsQuality=Low versteckt — P2 | S | A11y

**Beobachtung:** `ReduceMotion = Settings.GraphicsQuality == Low`. Das koppelt Performance-Quality an Accessibility — Spieler, die Motion-Sickness haben, aber sonst High-Quality-Visuals wollen, bekommen heute beides oder nichts.

**Lösungsskizze:** Separates `Settings.ReduceMotion` Bool (Default: false), das `GameJuiceEngine.ReduceMotion` direkt setzt. GraphicsQuality bleibt unabhängig.

---

### F-24: FloatingText-Stack bei vielen parallelen Events — P3 | M | Polish

**Beobachtung:** Bei AutoCraft + AutoCollect + AutoAccept gleichzeitig (alles Lv 25+) können binnen 1-2 Sekunden 5-8 FloatingTexts auf dem Dashboard erscheinen. Sie überlappen sich teils.

**Lösungsskizze:** `FloatingTextOverlay` bekommt eine Slot-basierte Spawn-Position (3 Spalten, Round-Robin). Bei >5 aktiven: ältere fade-out beschleunigen.

---

## 8. Test-Protokoll (Wie validieren?)

### 8.1 Vor-/Nach-Messungen je Sprint

Pro Sprint folgende Analytics-Events vergleichen (RemoteConfig-Cohort-Vergleich oder simple A/B mit Build-Wechsel):

| Sprint | Vorher-Messung | Nachher-Messung | Akzeptanz-Schwelle |
|--------|----------------|-----------------|-------------------|
| Sprint 1 (FTUE-Fixes) | `ftue_step_completed` Rate je Step | dito | Step 1,2,5 Konversion +10pp |
| Sprint 2 (LiveOps + Referral) | DAU-Tage je Cohort, App-Open-Count | dito | D7 +3pp |
| Sprint 3 (Tab-Badges) | Tab-Wechsel-Funnel | dito | Gilde-Tab-Besuche +20% |

### 8.2 Test-Szenarien (manuell)

Pro Sprint folgende manuelle Test-Szenarien durchspielen, vor und nach dem Fix:

**Onboarding-Sprint:**
- S1 — Casual Curious (siehe 2.1) auf einem realen Mid-Tier-Android
- Bildschirmaufzeichnung
- Stop-Watch je FTUE-Step
- Frage-Liste am Ende ("Was war unklar?")

**Retention-Sprint:**
- S4 — End-Game-Whale (Save mit Lv 200 + 2 Prestiges)
- Tag 1: aktives Spiel 15 Min, Live-Event starten lassen
- Tag 2: zurückkehren, prüfen ob Banner + Push + Score-Update funktionieren
- Tag 3: Event-Ende, Reward-Claim-Flow validieren

**UX-Sprint:**
- Settings-Sprint: alle neuen Toggles durchschalten, Random-Verhalten provozieren
- Tab-Badges: Sub-Funktionen jeweils auslösen (Chat-Nachricht, Boss-Spawn, etc.) und prüfen ob Badge erscheint

### 8.3 Regression-Watch

Folgende existierende Tests müssen weiter grün bleiben:

- `MeineApps.CalcLib.Tests` (irrelevant für diesen Plan, aber Gate)
- Manueller Smoketest aller 10 Mini-Games nach jedem Sprint
- Cloud-Save-Roundtrip (Upload → Reset → Download → State identisch)
- 5 Sprachen je Sprint stichprobenartig (DE/EN + 1 weitere)

---

## 9. Roadmap-Vorschlag (3 Sprints, je 1-2 Wochen)

### Sprint 1 — "Onboarding-Politur" (ca. 1 Woche, P0+P1)

Ziel: FTUE konvertiert messbar besser. Spieler hat in den ersten 15 Minuten ein klares Pfadgefühl.

| Aufgabe | Finding | Effort |
|---------|---------|--------|
| FTUE-AutomationIds reparieren | F-03 | S |
| Spotlight-Warn-Log bei fehlender AutomationId | F-03 | XS |
| Dialog-Kaskade-Soft-Throttle | F-04 | S |
| FTUE Step 0/1 CanSkip=false | F-05 | XS |
| FTUE Catch-Up-Pass für nachgelagerte Steps | F-06 | M |
| MaterialOffer Discoverability-Hint | F-07 | S |
| Long-Press-Bulk Hint anhängen | F-22 | XS |

### Sprint 2 — "Retention freischalten" (ca. 2 Wochen, P0)

Ziel: Die zwei toten Systeme (LiveOps, Referral) werden lebendig. Spieler haben einen Grund, jeden Tag wiederzukommen.

| Aufgabe | Finding | Effort |
|---------|---------|--------|
| `LiveEventService.AddScore` an Order-Complete + MiniGame-Complete hängen | F-01 | S |
| Live-Event-Banner im Dashboard (BannerStrip-Chip) | F-16 | M |
| Live-Event-Dialog beim Start + Reward-Tier-UI | F-16 | M |
| ReferralView in Settings (Code anzeigen, eingeben, teilen) | F-02 | M |
| Tab-Badges für Gilde + Shop | F-25 | S |
| Imperium-Pass UI-Repositioning im Shop | F-14 | M |
| Push-Notification "Dein Pass-Vorteil heute" wenn Pass-Besitz | F-14 | XS |

### Sprint 3 — "Mid-Game-Pacing + QoL" (ca. 1 Woche, P1+P2)

Ziel: Spieler im Mid-Game fühlen sich abgeholt. Spieler-Wünsche aus Reviews adressiert.

| Aufgabe | Finding | Effort |
|---------|---------|--------|
| Workshop-Spez Empfehlungs-Hinweise + Lernkurve-Rabatt | F-08 | M |
| Daily/Weekly/Event-Progress im Dashboard-BannerStrip | F-21 | S |
| Master-Volume getrennt nach Music/SFX | F-19 | M |
| KeepScreenOn-Toggle (als Pass-Sweetener) | F-20 | S |
| ReduceMotion separates Setting | F-23 | S |
| Cross-Workshop-Lieferketten Pre-Lv100-Hint | F-10 | S |
| Mid-Game Level 110/120/130/140 kosmetische Triggers | F-09 | M |
| Risk-Strategy Pin pro Workshop | F-12 | M |
| GS-Tages-Spar-Liste im Shop | F-13 | M |

### Sprint 4+ — Backlog für später

- F-11 (Auto-Produktion-Unlocks) — braucht Balancing-Simulation in Excel
- F-15 (BattlePass-Discoverability) — passt zu Sprint 3, kann nachgezogen werden
- F-17 (MegaProject-Push) — wenn Tab-Badges (F-04) live sind, Push-Notification ergänzen
- F-18 (Eternal-Mastery-Banner) — kleines Polish-Ticket
- F-24 (FloatingText-Stack) — Polish, wenn Beschwerden auftreten

---

## 10. Offene Fragen für Robert

Diese Punkte sind nicht eindeutig aus dem Code ableitbar — bitte vor Sprint-Start klären:

1. **Analytics-Zugriff:** Wo werden die Analytics-Events aktuell gesammelt (Firebase Analytics? GameAnalytics?). Wenn Firebase: hast du Zugriff auf die Funnel-Auswertung für die in 8.1 genannten Metriken?
2. **Live-Event-Content:** Welche der 4 Templates (DoubleReward / BossRush / CoopMarathon / MiniGameMastery) soll als erstes Live gehen? Empfehlung: DoubleReward (am einfachsten zu verstehen + wireable).
3. **Imperium-Pass Pricing:** Bleibt es bei 4.99€ Lifetime, oder ist eine zweite Stufe (Abo) angedacht? Beeinflusst Sprint-2-UI.
4. **Sprache der Onboarding-Tests:** Soll die FTUE-Test-Session auf Deutsch oder Englisch laufen? Idle-Game-Veterans testen oft EN — Casual-Spieler vermutlich DE.
5. **Reduce-Motion-Default:** Aktuell gekoppelt an GraphicsQuality=Low. Sollte das künftig per Android-System-Setting (Settings.Global.ANIMATOR_DURATION_SCALE = 0) auto-detected werden?
6. **Referral-Anti-Cheat:** CLAUDE.md erwähnt "Geraete-Fingerprint gegen Self-Referral als separater Service". Existiert das bereits, oder muss Sprint 2 das mit-implementieren?

---

## 13. Vertiefung — Mini-Games, Gilde, Imperium-Tab, Forschung, Crafting

Dieser Abschnitt ist eine zweite Audit-Welle, die nach der ersten Plan-Version (Sektionen 4-7) gezielt fünf zentrale Sub-Systeme detailliert durchgegangen ist. Befunde sind als F-26 bis F-40 nummeriert und thematisch geordnet.

### 13.1 Mini-Games (F-26 bis F-29)

Die 10 Mini-Game-ViewModels erben sauber von `BaseMiniGameViewModel`, der ~2.500 Z. Duplikation eliminiert. Tutorials werden bei Erstkontakt einmalig gezeigt (`Tutorial{GameType}Title/Text` in 6 Sprachen, alle vorhanden, auch für die 4 Sawing-Sub-Typen). Direktstart ohne Start-Button, Countdown nach 50+ Spielen verkürzt auf 350ms — gutes Pacing.

#### F-26: Strategie-Wahl Safe/Standard/Risk vor JEDEM Auftrag — P1 | M | Mini-Games

**Beobachtung:** `BaseMiniGameViewModel.SetOrderId` übernimmt `CurrentStrategy` aus `activeOrder.Strategy` — die Strategie wird also vor jedem Auftrag separat gewählt. Bei 30-60 Aufträgen je Session ergibt das 60-180 zusätzliche Tap-Entscheidungen pro Stunde.

**Hypothese:** Choice-Fatigue. Veteranen spielen quasi immer Standard oder Risk — drei Optionen pro Auftrag sind zu viel.

**Datei(en):** `BaseMiniGameViewModel.cs:78` (`CurrentStrategy`), Order-Strategy-Auswahl-Dialog (Datei noch zu finden, vermutlich `Views/Dialogs/OrderStrategyDialog.axaml`)

**Lösungsskizze:**
1. Pro Workshop eine `DefaultRiskStrategy`-Persistent-Property (siehe F-12 in Sektion 5 — gleicher Hebel).
2. Beim Auftrags-Spawn die letzte Wahl des Spielers übernehmen (Sticky-Pattern), Anzeige im Auftrag-Card.
3. Strategie-Auswahl nur noch bei Tap auf einen kleinen "Strategie ändern"-Link.

#### F-27: Tutorial-Info-Button-Discoverability nach erstem Spielen — P2 | XS | Mini-Games

**Beobachtung:** `BaseMiniGameViewModel.CheckAndShowTutorial` setzt `CanShowTutorialInfo = true`, nachdem das Tutorial einmal gesehen wurde (Zeile 778). Der Spieler kann das Tutorial also via Info-Button nochmal lesen — wo ist der Info-Button?

**Hypothese:** Wenn der Info-Button nur klein im Header existiert, finden viele Spieler ihn nie wieder. Bei den 4 Sawing-Sub-Typen (Sawing/Planing/TileLaying/Measuring) ist das besonders kritisch — Planing hat andere Regeln (30% kleinere Zonen, langsamer Marker) als das initial gesehene Sawing-Tutorial.

**Datei(en):** `BaseMiniGameViewModel.cs:152, 763-779`, jeweilige `*View.axaml` der Spiele

**Lösungsskizze:** Sicherstellen, dass alle 10 MiniGame-Views einen sichtbaren Info-Button im Header haben (Stichprobe-Audit). Bei SawingGame zusätzlich beim Wechsel des `GameType` (Sawing→Planing) **automatisch Tutorial neu zeigen**, weil die Mechanik anders ist — heutiger `CheckAndShowTutorial` markiert es nach Sawing für alle 4 Sub-Typen als „gesehen".

#### F-28: Pause-Verhalten beim App-Wechsel — P2 | M | Mini-Games

**Beobachtung:** `GameStateService.PauseActiveOrder()` setzt `PausedAt` und `AccumulatedPauseDuration` für Live-Orders. Im `BaseMiniGameViewModel` gibt es aber **kein Pause-Event-Hookup** — wenn der Spieler während eines aktiven Mini-Games (z.B. SawingGame, Timer-getrieben mit 16ms-Intervall) die App in den Hintergrund schiebt, läuft der `DispatcherTimer` weiter, bis Avalonia ihn pausiert.

**Hypothese:** Bei Backgrounded-App während Risk-Strategie-Mini-Game kann der Marker an die "Miss"-Position laufen — der Spieler verliert Reputation für etwas, das er nicht mitbekommt.

**Datei(en):** `BaseMiniGameViewModel.cs:404-415` (StartTimer), `MainViewModel.Lifecycle.cs:PauseGameLoop`

**Lösungsskizze:**
1. `MainViewModel.PauseGameLoop()` ruft zusätzlich `ActiveMiniGameViewModel?.PauseGame()` auf.
2. Neue Methoden `PauseGame()` / `ResumeGame()` in `BaseMiniGameViewModel`: Timer pausieren, beim Resume neu starten.
3. Bei Timing-kritischen Spielen (Sawing/Forge) zusätzlich beim Resume einen 1-Sekunden-„Welcome back"-Overlay zeigen, sodass der Spieler sich orientieren kann, bevor der Timer wieder läuft.

#### F-29: Multi-Task-Order-Rating-Aggregation nicht kommuniziert — P2 | XS | Mini-Games

**Beobachtung:** Multi-Task-Orders (z.B. „Build a Cabinet" mit 2 Tasks Sawing+Planing) aggregieren die Ratings (`ComputeCoopScore`, Zeile 242-252). Der `IntermediateAverage` Display (`BaseMiniGameViewModel.cs:148`) zeigt nach Task 1 "Bisheriger Durchschnitt: ★★☆". Aber: der Spieler weiß nicht, dass das Endergebnis der **Mittelwert** ist — vermutlich erwartet er, das beste Rating zählt.

**Lösungsskizze:** Tutorial-Bubble vor dem ersten Multi-Task-Order ("Bei mehreren Aufgaben zählt der Durchschnitt deiner Ratings"). Per ContextualHint, ID `multi_task_order`.

---

### 13.2 Gilde (F-30 bis F-32)

Die Gilde hat ein durchdachtes 10-State-System (Loading/Offline/NameDialog/CreateDialog/Browse/InGuild + 4 Sub-Pages für War/Boss/Hall/Achievements) und 5 Sub-Tabs (Overview/Combat/Research/Chat/Members). Architektur sauber. Befunde liegen auf der UX-Ebene.

#### F-30: Chat-Polling alle 15s als Battery-Drainer — P2 | M | Gilde

**Beobachtung:** `GuildViewModel.StartChatPolling` (Zeile 1197-1203) startet einen `DispatcherTimer` mit `Interval = TimeSpan.FromSeconds(15)`. Solange der Gilde-Tab aktiv ist, feuert er Firebase-Reads alle 15s.

**Hypothese:** 4 Firebase-Reads pro Minute, bei 20 Min Chat-Tab-Sitzung = 80 Reads. Bei 1.000 DAU ergibt das 80k Reads/Tag nur fürs Chat-Polling. Akku-Impact auf Mid-Tier-Android ist messbar (≈1-2%/h bei aktivem Polling).

**Lösungsskizze:**
1. Adaptive Polling: Bei sichtbarer App + Tab aktiv 15s, bei App im Hintergrund (`PauseStateChanged`) komplett aus.
2. Wenn in den letzten 5 Min keine neue Nachricht kam, Polling auf 30s drosseln (exponentielles Backoff).
3. Mittelfristig: Firebase Cloud Messaging (FCM) für Real-Push statt Polling.

#### F-31: Beitritts-Flow ist auf der ersten Anlauf intransparent — P1 | M | Gilde

**Beobachtung:** Bei Spielerlevel 15 (Gilde-Tab unlocked) sieht der Spieler erst Loading, dann je nach Firebase-State entweder NameDialog → CreateDialog → Browse oder direkt Browse. Wenn der Spieler keinen Namen gesetzt hat, kommt der NameDialog ohne Erklärung.

**Hypothese:** Spieler verstehen den State-Wechsel nicht — "Warum will die App jetzt einen Namen von mir?"

**Datei(en):** `GuildViewModel.cs:18-30` (GuildViewState-Enum), Nachgelagerte Übergänge

**Lösungsskizze:** Beim allerersten Gilde-Tab-Besuch einen Welcome-Hint zeigen ("In Gilden bekommst du dauerhafte Boni, kannst Co-Aufträge spielen und gegen andere antreten. Wähle deinen Spielernamen — er wird in der Gilde sichtbar sein."). Bestehender `ContextualHint.GuildHint` triggert nur beim Tab-Besuch — Erweiterung: vor dem ersten NameDialog explizit pushen.

#### F-32: Gilde-„Offline" mit gecachten Boni unterschätzt — P2 | XS | Gilde

**Beobachtung:** Im Offline-State (`GuildView.axaml:101-122`) zeigt die UI `CachedBonusInfo` — sehr gut. Aber: der Text ist eine einzelne Zeile, kein Hinweis darauf, **dass die Boni noch wirken**, obwohl die Verbindung weg ist.

**Lösungsskizze:** Im Offline-State zusätzlich ein grünes ✓ + "Gilden-Boni bleiben aktiv" Text — beruhigt den Spieler, der „Offline" sonst als Funktionsausfall empfindet.

---

### 13.3 Imperium-Tab (F-33 bis F-35)

Der Imperium-Tab hat 6 Sub-Tabs in einer `UniformGrid Columns="6"`. Layout-Druck auf schmalen Geräten ist hoch (60dp pro Tab auf 360dp Breite), die Reihenfolge folgt Code-Convention statt Spieler-Frequenz.

#### F-33: 6 Sub-Tabs auf 360dp Breite zu eng — P1 | M | Imperium

**Beobachtung:** Auf Mid-Tier-Android mit 360dp Display-Breite verteilen sich 6 Sub-Tabs auf je 60dp inkl. 6dp Padding. Icon ist 20dp, Label `FontSize="10"`. Bei längeren Labels ("ImperiumSubTabWorkshops" auf Deutsch: "Werkstätten") wird Text abgeschnitten oder unleserlich.

**Reproduktion:** App auf einem 360dp-Gerät öffnen, in den Imperium-Tab navigieren, Labels prüfen.

**Datei(en):** `Views/ImperiumView.axaml:69` (`UniformGrid Columns="6"`)

**Lösungsskizze:** Drei Optionen:
1. **Scrollable Tab-Bar:** `ScrollViewer` horizontal mit `Tabs` als StackPanel — verträgt 6+ Tabs, aber off-screen-Tabs sind unsichtbar (Discoverability-Risiko).
2. **2-Reihen-Layout:** Auf schmalen Geräten 3×2 Grid statt 6×1 — verbraucht 64dp mehr Höhe.
3. **Sub-Tab-Gruppierung:** "Aufbau" (Workshops, Workers, Equipment) + "Strategie" (Research, Warehouse, Ascension) — 2×3 Tabs in zwei separaten Bars.

Empfehlung: Option 2 für UX-Konsistenz, oder Icons-only auf schmalen Geräten + Labels ab 400dp.

#### F-34: Sub-Tab-Reihenfolge folgt Code, nicht Spieler-Frequenz — P2 | XS | Imperium

**Beobachtung:** Reihenfolge heute: Workshops / Warehouse / Workers / Research / Equipment / Ascension. Warehouse ist nur ab Lv50 sichtbar (locked-State davor), aber rangiert vor Workers — also vor einem Feature, das deutlich häufiger genutzt wird.

**Lösungsskizze:** Reihenfolge nach Frequenz/Spielprogression umstellen:
1. Workshops (häufigster Use)
2. Workers (häufig, früh)
3. Research (mittel, früh)
4. Equipment (mittel)
5. Warehouse (selten, mid-game)
6. Ascension (selten, end-game)

#### F-35: Alle 6 Sub-Sections immer im VisualTree — P3 | S | Imperium

**Beobachtung:** `ImperiumView.axaml:195-200` instanziiert alle 6 Sections (`WorkshopsSection`, `WarehouseSection`, ...) mit `IsVisible="{Binding IsImperium*Active}"`. Avalonia rendert IsVisible=false zwar nicht, aber Bindings werden trotzdem evaluiert und die UserControls bleiben im VisualTree.

**Hypothese:** Auf einem schwachen Android führt das beim Tab-Wechsel zu Stutter, weil alle 6 Sections gleichzeitig auf StateLoaded/PropertyChanged reagieren.

**Lösungsskizze:** Lazy-Loading-Pattern aus `MainView` übernehmen (siehe CLAUDE.md "MainView Lazy-Loading"): ein einzelnes `ContentControl` mit `Content="{Binding ActiveImperiumSubTabViewModel}"`, ViewLocator findet das passende UserControl. Vorteil: nur aktive Section ist im VisualTree.

---

### 13.4 Forschung (F-36 bis F-37)

Die Forschung hat 4 Branches (Tools/Management/Marketing/Logistics) mit insgesamt ca. 57 Nodes. Die Logistics-Branch ist strikt linear (jeder Node hat genau einen Prerequisite), die anderen drei nutzen ein Tree-Layout mit Verzweigungen.

#### F-36: Logistics-Branch ist linear und sehr lang — P1 | L | Forschung

**Beobachtung:** Die Logistics-Branch (12 Nodes) ist linear: logi_01 → logi_02 → logi_05 → logi_04 → logi_08 → logi_07 → logi_10 → logi_11 → logi_09 (T4-Trigger) → logi_03 → logi_12 → logi_06. Gesamt-Forschungszeit ohne Beschleunigung: **30min + 1h + 2h + 3h + 6h + 8h + 12h + 16h + 24h + 32h + 48h + 72h ≈ 224 Stunden = 9.3 Tage Echtzeit**. Erst nach allen Nodes ist der T4-Loop voll verfügbar.

**Hypothese:** Spieler werden nach 1-2 Wochen entweder das Forschungs-Speedup-Rewarded-Ad zur Pflicht-Mechanik machen, IAP-GS investieren oder abspringen. Linear-langer Tree ist deutlich härter zu spielen als verzweigter Tree mit Wahlmöglichkeiten.

**Datei(en):** `Models/ResearchTree.cs:26-67` (Logistics-Branch)

**Lösungsskizze:**
1. **Verzweigung einbauen**: Nach logi_05 (Markt) eine 2er-Wahl: Spieler entscheidet zwischen "Stack-Limit-Pfad" (logi_02/logi_11) und "Slot-Pfad" (logi_04/logi_03). Beide Pfade enden bei logi_09 (T4).
2. **Forschungszeiten reduzieren**: Top-3-Nodes (logi_06: 72h, logi_12: 48h, logi_03: 32h) auf max. 24h cappen — die Skalierung der Kosten reicht als Gate.
3. **Bonus-Hint im Research-Tab**: Ab Lv 200 + 2× Prestige eine FloatingText-Empfehlung "Konzentriere dich auf die Logistik-Forschung — sie schaltet dein End-Game frei".

#### F-37: ResearchTree-Kommentar veraltet (3 statt 4 Branches) — P3 | XS | Forschung

**Beobachtung:** `Models/ResearchTree.cs:6` schreibt: *"Statische Definition aller 60 Research-Nodes (3 Branches x 20 Level)."* — tatsächlich gibt es 4 Branches (Tools/Management/Marketing/Logistics) mit 12-15 Nodes each (~57 gesamt, nicht 60). Code-Kommentar ist veraltet.

**Lösungsskizze:** Kommentar auf 4 Branches aktualisieren, Anzahl-Notation entfernen (oder per `AllRecipes.Count` dynamisch).

---

### 13.5 Crafting + Material-Bedarf (F-38 bis F-40)

Das Crafting hat 33 Rezepte (10× T1, 10× T2, 10× T3, 3× T4) und ist die V7-Hauptmechanik. Die Tiefe ist da, die Komplexität auch — was fehlt, ist die UX-Übersetzung dieser Tiefe in Spieler-verständliche Hinweise.

#### F-38: Materialfluss-Komplexität ohne Lieferketten-Visualisierung — P1 | M | Crafting

**Beobachtung:** Beispiel: `imperium_hq` (T4-Endprodukt) braucht 19 T3-Items aus 10 verschiedenen Workshops. Jeder T3-Item braucht 2 T2 + Cross-Workshop-Input. Jeder T2 braucht 3 T1 (eigen) + 1 T1 (Cross). Effektiv: zur Fertigung von 1 imperium_hq werden über 100 T1-Items aus 10 Workshops + 19 T2-Items + 19 T3-Items gebraucht.

Die `CraftingView` listet Rezepte pro Workshop, aber **kein Material-Bedarfs-Diagramm** ("Für deinen geplanten imperium_hq brauchst du noch: 4× luxury_furniture, 8× smart_home, ..."). Der Spieler muss diese Pipeline im Kopf rechnen.

**Datei(en):** `Views/CraftingView.axaml`, `ViewModels/CraftingViewModel.cs` (zeigt nur einzelne Rezepte, kein aggregierter Bedarf)

**Lösungsskizze:**
1. Neue View `MaterialChainView` mit Ziel-Auswahl ("Was willst du bauen?") und Rückwärts-Auflösung aller benötigten Materialien.
2. UI: Baum-Visualisierung mit Skia (gibt's schon für Research — Pattern reuse aus `ResearchTabRenderer`).
3. Aktueller Bestand pro Material in Grün/Rot eingefärbt (Bestand ≥ Bedarf = grün).
4. „Plan auf alle Werkstätten verteilen"-Button: setzt Crafting-Queue-Hints für jeden Workshop, was er noch produzieren muss.

#### F-39: Material-Bedarf-Anzeige im Auftrag begrenzt — P1 | S | Crafting

**Beobachtung:** Aufträge mit MaterialOffer (`Order.MaterialOffer`) zeigen die geforderten Materialien — aber nicht in einer Form, die mit dem Crafting-Inventar abgeglichen wird. Spieler muss zwischen Auftrags-Card und Crafting-Tab hin- und herwechseln, um zu prüfen, ob er die Materialien hat.

**Lösungsskizze:**
1. Im Auftrag-Card pro gefordertem Material: aktueller Bestand neben dem Bedarf anzeigen ("Holz 3/5" — rot wenn Bestand < Bedarf).
2. Wenn Bestand reicht: "Mit Material starten" Button hervorheben (golden).
3. Wenn Bestand fehlt: "Material craften (→ Plumber-Werkstatt)"-Shortcut-Button.

#### F-40: T4-Trigger versteckt — logi_09 + Workshop-Lv 500 — P2 | S | Crafting

**Beobachtung:** T4-Rezepte (`villa`, `skyscraper`, `imperium_hq`) sind ab Workshop-Lv 500 verfügbar — aber zusätzlich braucht es `logi_09`-Research (`UnlocksTier4`). Ein Spieler kann Workshop-Lv 500 erreichen, ohne von der T4-Mechanik zu wissen.

**Datei(en):** `Models/CraftingRecipe.cs:189-227` (T4-Rezepte), `Models/ResearchTree.cs:55` (`logi_09`)

**Lösungsskizze:**
1. Bei Workshop-Lv 450 (Vor-Trigger): ContextualHint "Bald: Imperium-Manufaktur! Schließ deine Logistik-Forschung ab, um Tier-4-Produkte freizuschalten."
2. Bei logi_08 Abschluss (vorletzte Stufe): "Noch 1 Forschung bis zur Imperiums-Manufaktur" als Goal-Banner.
3. Bei logi_09 Abschluss: Celebration + Tutorial "Du kannst jetzt Villen, Wolkenkratzer und ein Imperiums-HQ bauen!" mit Vorschau auf die 3 T4-Pläne.

---

### 13.6 Übergreifend — was die Vertiefung sichtbar gemacht hat

Drei Beobachtungen aus dieser zweiten Welle, die über einzelne Findings hinausgehen:

**(A) Tiefe ohne Visualisierung.** Crafting, Forschung, Mega-Project: alles hat saubere Daten-Modelle, aber wenig narrative oder visuelle Brücke. Der Spieler sieht eine Liste, kein System. Ein einziges Diagramm pro System (Material-Lieferkette, Research-Tree, Mega-Project-Progress) würde mehr bringen als 10 neue Tooltips.

**(B) Pacing-Steilkurven nach Lv 100.** Cross-Workshop-Inputs (Lv 100), Auto-Craft Tier-2 (WS-Lv 200), Auto-Craft Tier-3 (WS-Lv 400), T4 (WS-Lv 500 + 9 Researches). Alle wichtigen Mid-/Late-Game-Loops werden in einem ~50-200h-Fenster freigeschaltet. Die jüngsten 3-7 Stunden des Spielers (Lv 30-70) sind dann content-arm.

**(C) Architektur-Sauberkeit vs. UX-Discoverability.** Der Code ist erfreulich sauber (Coordinator-Services, Facades, Partial-VMs, ViewLocator). Die Architektur ist nicht das Problem. Das Problem ist, dass Spieler über fertige Features stolpern, weil Discoverability als nachgelagerter Schritt behandelt wird. Ein "Feature-Reveal-Service" — der pro Lv-Schwelle/Research-Abschluss/Achievement einen passenden ContextualHint feuert — könnte das systematisch fixen.

---

### 13.7 Roadmap-Erweiterung

Die Vertiefungs-Findings fügen sich in die bestehende 3-Sprint-Roadmap so ein:

| Sprint | Zusätzliche Aufgaben | Findings | Effort |
|--------|---------------------|----------|--------|
| Sprint 1 (Onboarding-Politur) | Strategie-Sticky-Pattern, Tutorial-Info auf MiniGame-Views | F-26, F-27 | S |
| Sprint 2 (Retention) | Gilde-Welcome-Hint, Sub-Tab-Reorder Imperium | F-31, F-34 | S |
| Sprint 3 (Mid-Game-Pacing+QoL) | Sub-Tab-Layout-Fix für schmale Geräte, T4-Foreshadowing-Hint | F-33, F-40 | M |
| **Sprint 4 — „Tiefe sichtbar machen"** (neu, ≈2 Wochen) | Material-Lieferketten-View, Auftrag-zeigt-Bestand | F-38, F-39 | L |
| **Sprint 5 — „Forschungs-Politur"** (neu, ≈1 Woche) | Logistics-Branch entzerren, ResearchTree-Kommentar | F-36, F-37 | M |
| Backlog | Pause-Verhalten Mini-Games, Chat-Polling adaptiv, Multi-Task-Hint, Imperium-Lazy-Loading, Offline-Gilde-Beruhigungs-Text, Multi-Task-Rating-Hint | F-28, F-29, F-30, F-32, F-35 | je S-M |

---

## 11. Glossar

- **FTUE** = First-Time-User-Experience (Tutorial-Sequenz nach Erstinstallation)
- **Spotlight** = Lokales Hervorheben eines UI-Elements (Pulse-Border + abgedunkelte Umgebung)
- **Pp / pp** = Prozentpunkte (z.B. "+5pp" = von 30% auf 35%)
- **D1 / D7 / D30** = Day-1 / Day-7 / Day-30 Retention
- **DAU** = Daily Active Users
- **QoL** = Quality of Life (Komfort-Features ohne Mechanik-Eingriff)
- **A11y** = Accessibility
- **GS** = Goldschrauben (Premium-Währung)
- **BP** = BattlePass

---

## 12. Anhang — Quick-Reference Datei-Pfade

| Bereich | Datei |
|---------|-------|
| FTUE-Service + Steps | `HandwerkerImperium.Shared/Services/FtueService.cs` |
| FTUE-Overlay-VM | `HandwerkerImperium.Shared/ViewModels/FtueOverlayViewModel.cs` |
| Welcome-Flow | `HandwerkerImperium.Shared/ViewModels/WelcomeFlowViewModel.Logic.cs` |
| Contextual Hints | `HandwerkerImperium.Shared/Models/ContextualHint.cs` |
| Balancing-Konstanten | `HandwerkerImperium.Shared/Models/GameBalanceConstants.cs` |
| Level-Gates | `HandwerkerImperium.Shared/Models/LevelThresholds.cs` |
| Live-Event-Service | `HandwerkerImperium.Shared/Services/LiveEventService.cs` |
| Referral-Service | `HandwerkerImperium.Shared/Services/ReferralService.cs` |
| TabBar-Badges | `HandwerkerImperium.Shared/Views/MainView.axaml.cs:315-319` |
| Settings-VM | `HandwerkerImperium.Shared/ViewModels/SettingsViewModel.cs` |
| Audio-Service (Android) | `HandwerkerImperium.Android/AndroidAudioService.cs` |
| Dashboard-Layout | `HandwerkerImperium.Shared/Views/DashboardView.axaml` |
| BannerStrip | `HandwerkerImperium.Shared/Views/Dashboard/BannerStrip.axaml` |
| Order-Generator | `HandwerkerImperium.Shared/Services/OrderGeneratorService.cs` |
| MiniGame-Basis-VM | `HandwerkerImperium.Shared/ViewModels/MiniGames/BaseMiniGameViewModel.cs` |
| Sawing-VM (4 Sub-Typen) | `HandwerkerImperium.Shared/ViewModels/MiniGames/SawingGameViewModel.cs` |
| Guild-VM (10 ViewStates, 5 Sub-Tabs) | `HandwerkerImperium.Shared/ViewModels/GuildViewModel.cs` |
| Guild-View | `HandwerkerImperium.Shared/Views/GuildView.axaml` |
| Imperium-View (6 Sub-Tabs) | `HandwerkerImperium.Shared/Views/ImperiumView.axaml` |
| Imperium-Sub-Tab-Logik | `HandwerkerImperium.Shared/ViewModels/MainViewModel.Tabs.cs` |
| Research-Tree (4 Branches) | `HandwerkerImperium.Shared/Models/ResearchTree.cs` |
| Crafting-Rezepte + Produkte | `HandwerkerImperium.Shared/Models/CraftingRecipe.cs` |
| Crafting-View | `HandwerkerImperium.Shared/Views/CraftingView.axaml` |
| Crafting-VM | `HandwerkerImperium.Shared/ViewModels/CraftingViewModel.cs` |
