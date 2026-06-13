# HandwerkerImperium.Unity — 3D-Idle-Game-Plan (GDD)

> **Neuausrichtung (8. Juni 2026).** Dieses Dokument definiert die Unity-Version **neu** als
> **3D-Walk-around-Idle-Tycoon** im Stil moderner Genre-Hits (My Perfect Hotel, My Mini Mart,
> Idle Office Tycoon). Es **ersetzt** die bisherige Leitlinie „dasselbe Spiel, nur 3D-Präsentation"
> als verbindliche Spiel-Design-Quelle. Mechanik **darf jetzt bewusst vom Avalonia-Original abweichen.**
> Das Avalonia-HandwerkerImperium bleibt unverändert produktiv; die Unity-Version wird ein
> **eigenständiges, genre-typisches Spiel** mit demselben Thema (Handwerk) und Personal (Meister Hans).

Verbindliche Spiel-Design-Quelle ab sofort: **dieses Dokument.** Die alten Plandocs (DESIGN.md,
ORIGINAL_WERTE.md, PLAN_ABGLEICH_ORIGINAL.md, DOMAIN_3D_PLAN.md) gelten nur noch als **Referenz**
für Thema, Werte-Ideen und den schon portierten Code — nicht mehr als Soll.

---

## 0. Status & Abgrenzung (Doktrin-Wechsel)

| Was | Alt (bis 7.6.26) | Neu (dieser Plan) |
|-----|------------------|-------------------|
| Mechanik | 1:1 zum Avalonia-Original, jede Abweichung = Bug | **Voll neu im 3D-Idle-Stil**, Abweichung ist gewollt |
| Perspektive | 3D-Re-Skin der bestehenden 5-Tab-UI | **3D-Welt mit laufendem Avatar**, kaum klassische Tab-UI |
| Kern-Interaktion | Tippen auf Karten/Buttons | **Avatar läuft, sammelt, baut** physisch in der 3D-Welt |
| Tiefe | Voller Wirtschafts-Sim (Crafting-Tree, Markt, Gilde, Ascension, Eternal Mastery) | **Schlanke Arcade-Idle-Schleife** + leichte Management-Tiefe |
| Status Domain-Port | Pflicht-Fundament (Schicht 1–16, 1:1 verifiziert) | **Teil-Referenz** — einige Formeln werden wiederverwendet, vieles ruht (s. §12) |

**Warum der Wechsel:** Die produktive Avalonia-Version deckt die „tiefe Sim"-Zielgruppe bereits ab.
Die Unity-Version soll einen **anderen, größeren Markt** treffen — den Massenmarkt der 3D-Idle-Arcade-Tycoons,
deren Loop bewiesen retention- und monetarisierungsstark ist. Gleiches Thema, gleiche Marke, **anderes Spielgefühl.**

**Was bleibt unangetastet:** Der gesamte **Tech-Stack & die Unity-Conventions** aus
[CLAUDE.md](CLAUDE.md) (Unity 6000.4.8f1, URP, VContainer, UniTask, Newtonsoft, Addressables,
New Input System, Cinemachine, C# 9 / netstandard2.1, asmdef-Hierarchie, DI-Regeln, Gotchas)
gelten **unverändert weiter**. Geändert wird das **Spiel-Design**, nicht der technische Rahmen.

**Umsetzungs-Stand (12.06.2026):** P0–P4-**Logik komplett** (Domain Unity-frei, 179 NUnit grün) +
**spielbare 3D-Welt** gekoppelt an die Runtime: Avatar-Lauf/Gang (geschwindigkeitsgekoppelt), 10 Gewerke
mit Plot-Unlock + Worker-Tempo-Stufen, Kunden-Schlange läuft physisch, Wahrzeichen-Sanierung, Premium-HUD,
Sound, **Android-APK baut** (IL2CPP/ARM64, glTFast-Patch). **Aktuelle Runde:** Visual-/Spiel-Umbau auf den
Hencoop/Open-Shop-Mix (§1) — Casual-Boden-Tiles, Pipeline-Vegetation + Wind, „Stadt heilt" (§6.8), Werkstatt-
Ausbaustufen (§6.1), Worker-Verwaltungs-Panel statt Boden-Platten (§6.2), Karren-Logistik. **Offen (extern/gated):**
Firebase/Ads/IAP-SDK, 6-Sprachen-Lokalisierung, APK-Größe (P4), Beta/Store/KPI/Cutover.

---

## 1. Vision & Genre-Einordnung

**Pitch:** *Du erbst die heruntergekommene Werkstatt deines Großonkels Meister Hans. Aus einer
einzigen Garage läufst du selbst durch deinen Hof, schnappst dir das verdiente Geld, stellst
Handwerker ein, die für dich arbeiten, baust Werkstatt um Werkstatt aus und erweckst eine ganze
verfallene Stadt wieder zum Leben — bis dein Handwerks-Imperium das ganze Land überzieht.*

**Genre:** 3D Walk-around Idle-Arcade-Tycoon mit leichter Management-Tiefe.

**Plattform:** Android (primär), Desktop (Test). Beta-App-ID `com.meineapps.handwerkerimperium2.beta`
(wie gehabt), Avalonia-Original bleibt produktiv.

**Referenzspiele (Loop-Vorbild):** Der Kern-Loop folgt dem bewiesenen „Arcade-Idle"-Muster:
herumlaufen → Stationen bedienen → Bezahlung physisch einsammeln → Personal anstellen (=Automatisierung)
→ neue Bereiche freischalten → expandieren. Vorbilder: **My Perfect Hotel** (der prägende Hit des
Subgenres), **My Mini Mart**, **Idle Office Tycoon**, **Idle Construction 3D**. Restore-/Aufbau-Vorbilder
für das Stadt-Ziel: **Township**-artige Wiederaufbau-Schleifen.

**Visuelle + spielerische Leitreferenz (verbindlich, Nutzer-Entscheidung 12.06.2026):** Ein **Mix aus
zwei GraphicRiver-Idle-Kits** —
[Hencoop Idle Game](https://graphicriver.net/item/hencoop-idle-gam/47410249) liefert die **WELT** (satte
flache Farben, niedliche aufgeräumte Mini-Welt, helle geschwungene Wege mit Einfassung) und die **sichtbare
Logistik** (Worker ziehen Karren mit Waren); [Open Shop Idle Game](https://graphicriver.net/item/open-shop-idle-game/50285069)
liefert den **GEBÄUDE-/SHOP-Charme** (Werkstätten als freundliche Läden mit klarer Front + sichtbaren
Ausbau-Stufen). Stil-Doktrin: flach + satt + sauber, KEIN Textur-Rauschen/Realismus. Details + How-to →
Memory `hwi-unity-visual-stilreferenz`. **Anti-Langeweile-Pflicht (Nutzer):** der Loop darf nie „nur
hin-und-her-laufen" sein — laufend Entscheidungen, sichtbarer Ausbau, kurzfristige Ziele (§6.1/§6.2/§6.8).

**Design-Säulen:**
1. **Sofort verständlich** — kein Tutorial-Wall. In 10 Sekunden läuft der Spieler, sammelt Münzen, kauft Upgrade.
2. **Spürbarer Fortschritt** — jede Aktion gibt sofort Geld, jeder Knopf ist günstig genug für „nur noch eins".
3. **Befriedigende Automatisierung** — der „Aha"-Moment, wenn der erste angestellte Arbeiter die eigene Lauferei übernimmt.
4. **Sichtbare Welt-Veränderung** — die Stadt heilt sichtbar (Ruine → Schmuckstück), das gibt ein Fernziel.
5. **Idle-Versprechen** — kommt der Spieler zurück, wartet Offline-Verdienst. Kurze Sessions, hohe Frequenz.

---

## 2. Story & Setting — der Mix aus drei Strängen

Die drei gewählten Story-Ideen werden zu **einem** Arc verwoben, jede übernimmt eine klare Funktion:

| Strang | Funktion im Spiel | Umsetzung |
|--------|-------------------|-----------|
| **Meister Hans** (Erbe + Mentor) | Das **Warum** — emotionaler Anker & Tutorial-Stimme | Hans hat dir seine alte Garage vererbt und begleitet dich als Stimme (Funkgerät/Anrufbeantworter/Briefe → die schon geplanten Meister-Hans-Voice-Lines). Kommentiert Meilensteine, gibt Tipps. |
| **Garage → Imperium** (Aufstieg) | Die **Progressions-Fantasie** — vertikale Achse | Start in *einer* Garage. Jede Prestige-Stufe = Umzug in eine größere Stadt (Franchise). Vom Ein-Mann-Betrieb zum landesweiten Imperium. |
| **Stadt-Wiederaufbau** (Mission) | Das **Welt-Ziel** — sichtbares Fernziel & Feedback | Die Stadt „**Hansstadt**" ist verfallen. Große „Restaurierungs-Aufträge" sanieren Distrikt für Distrikt (Marktplatz, Kirche, Rathaus, Hafen…). Die Welt verändert sich sichtbar. |

**Erzählbogen (grob):**
- **Prolog:** Hans' Stimme begrüßt dich in der staubigen Garage. Erste Sägen, erstes Geld, erster Arbeiter.
- **Akt 1 (Hansstadt):** Werkstatt um Werkstatt. Jeder Distrikt, den du sanierst, schaltet eine neue Handwerks-Art frei. Hans erzählt zwischendurch die Geschichte der Stadt und seiner eigenen Anfänge.
- **Akt-Übergänge (Prestige):** Hansstadt erstrahlt in 5 Sternen → Hans schickt dich in die nächste, größere Stadt. „Mach das, was du hier geschafft hast — nur größer."
- **Spätes Spiel:** Mehrere Städte als Franchise-Karte, das Imperium wächst. Optionaler emotionaler Abschluss-Beat um Hans.

**Story-Träger sind günstig & re-use-fähig:** Voice-Lines + kurze 3D-Cutscene-Beats bei Distrikt-Sanierung
und Prestige (keine teuren gerenderten Filme). Die geplanten ~100 Story-Lines/„Chapters" passen 1:1 auf diese Beats.

**Tonalität:** warm, bodenständig, leicht humorvoll (Hans als knorriger, herzlicher Handwerksmeister) —
keine düstere Erzählung, passend zum freundlichen Arcade-Look.

---

## 3. Kern-Loop

### 3.1 Sekunde-zu-Sekunde (der eigentliche Loop)
1. **Stationen produzieren** automatisch Waren → die Ware stapelt sich sichtbar an der Station.
2. **Avatar läuft** zur Station, **nimmt automatisch** einen Trag-Stapel auf (Carry-Stack über dem Kopf, genre-typisch).
3. **Avatar trägt** die Ware zum **Tresen / Abgabepunkt**, lädt automatisch ab.
4. **Kunden/Aufträge** am Tresen werden bedient → **Geld spawnt physisch** als Münzen/Scheine.
5. **Avatar läuft über das Geld** → Auto-Pickup (Sammelradius upgradebar).
6. **Avatar geht auf ein Upgrade-Pad** und **hält** (rampende Ausgabe) → Station schneller / Tresen größer / Sammelradius / Trag-Kapazität.
7. **Avatar geht auf ein gesperrtes Plot** → halten zum Bezahlen → **neue Werkstatt/Station/Distrikt** öffnet sich.
8. → zurück zu 1. Sobald genug Arbeiter angestellt sind, läuft die Kette **ohne den Spieler** weiter.

### 3.2 Minute-zu-Minute (Session-Entscheidungen)
- **Arbeiter anstellen** (pro Station ein NPC, der das Tragen/Bedienen übernimmt) → der Schritt von „aktiv" zu „idle".
- **Engpass auflösen:** Wo stapelt sich Ware (Station zu schnell / Träger zu langsam)? Wo steht der Tresen leer (zu wenig Produktion)? → gezielt upgraden.
- **Material nachfüllen** (leichte Versorgungs-Schicht, s. §6.5) oder per Ad/Arbeiter automatisieren.
- **Restaurierungs-Auftrag** abschließen → Distrikt saniert + Hans-Cutscene + Stern-Fortschritt.

### 3.3 Meta (über Sessions)
- **Stern-Rating** der Stadt (1→5★) steigt durch Werkstätten, sanierte Distrikte, Auftragsvolumen → schaltet neue Distrikte/Handwerks-Arten frei.
- **Offline-Verdienst** beim Wiederkommen (gedeckelt, per Ad verdoppelbar).
- **Prestige = Akt-Finale** bei 5★ → permanenter Multiplikator + Umzug in die nächste Stadt. **Maximal 3×** (4 Städte) — selten & zeremoniell, **nicht** der Haupt-Treadmill.
- **Permanente Langzeit-Vektoren:** **Meisterschafts-Track** (kontoweit, nie reset) + **Master-Tools** + **Imperium-Marken-Perkboard** + **Endgame-Meistergrade** (Soft-Infinite nach dem 3. Prestige) tragen die **Monate**-Progression (→ [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)).

**Loop-Tuning-Ziel:** Erste 60 s = erste Werkstatt läuft. Erste 5 min = erster Arbeiter angestellt
(Automatisierungs-Aha). Erste Session = 2.–3. Werkstatt + erstes saniertes Gebäude. **Erstes Prestige ~Woche 1**
(bewusst kein Tag-1-Reset — Prestige ist selten); danach Akt 2/3 über Wochen, Endgame über Monate.

---

## 4. Kamera & Steuerung

| Aspekt | Lösung |
|--------|--------|
| Kamera | **Cinemachine 3.x** 3rd-Person-Follow, leicht erhöhte Schräg-Aufsicht (~45–55°). Pinch-Zoom raus bis Stadt-Übersicht, Drag-Pan im Zoom. Impulse-Shake bei Sanierung/Prestige. |
| Bewegung (Mobile) | **Floating Virtual Joystick** (linker Daumen) + **Tap-to-move**-Fallback. New Input System (Touch nativ). |
| Bewegung (Desktop-Test) | WASD + Maus-Drag-Pan + Scroll-Zoom. |
| Interaktion | **Auto-Trigger bei Annäherung** — keine Buttons an Stationen/Geld: reinlaufen genügt (Carry/Pickup/Abgabe automatisch). |
| Bezahlen | **Hold-to-Pay-Pads** mit **rampender Ausgaberate** (Genre-Signatur: erst langsam, dann schneller) für Upgrades/Unlocks/Material. |
| Avatar | `CharacterController` + Animator (Idle/Walk/Carry/Hammer-Geste). Carry-Stack-Visual skaliert mit Tragmenge. |
| Barrierefreiheit | Auto-Walk-to-nearest-task-Option (Tippen auf Station → Avatar geht selbst hin) für Gelegenheitsspieler. |

**Performance-Leitplanke:** Ziel **60 FPS Mobile** (Low/Mid/High Quality-Tiers), Avatar + ~6–12 NPCs +
instanzierte Carry-Props gleichzeitig. URP, GPU-Instancing für Münzen/Waren, LODs, ASTC-Texturen.

---

## 5. Die Werkstatt-Stadt (Hub-Aufbau)

Statt 5 UI-Tabs gibt es **eine begehbare Stadt**, die organisch wächst. Aufbau von innen nach außen:

```
            [ Rathaus-Distrikt (Ruine→5★) ]
                      |
 [ Markt ] — [ ZENTRALER HOF / Garage (Start) ] — [ Hafen ]
                      |
            [ Wohn-Distrikt (Restaurierung) ]
```

- **Start:** Garage + Hof mit *einem* Plot (Schreinerei). Drumherum alles vernebelt/gesperrt (Fog-of-War-artig, abgesperrte Bauzäune).
- **Werkstatt-Plots:** Die 10 Handwerks-Arten werden nacheinander als Plots freigeschaltet (Hold-to-Pay an Bauzaun). Jedes Plot = eine **Produktionsstation** + Tresen + Upgrade-Pads + Arbeiter-Slot.
- **Distrikte:** Gruppen von Plots + ein **Wahrzeichen** (Marktplatz, Kirche, Rathaus, Hafen). Wahrzeichen sind die **Restaurierungs-Ziele** (Ruine → saniert), gekoppelt an Stern-Rating.
- **Wege/Deko:** Modularer Stadt-Kit (Pflaster, Laternen, Bäume, Schilder) — wächst mit Sanierungsfortschritt von „grau & kaputt" zu „bunt & belebt" (NPC-Passanten als Idle-Leben).
- **Übersicht:** Rauszoomen zeigt die ganze Stadt + Fortschrittsbalken je Distrikt; reinzoomen = spielbarer Hof.

**Franchise-Karte (Meta-Ebene):** Über der Stadt liegt eine **Landkarte** mit den **4 Städten** (Prestige-Ziele):
**Hansstadt → Kreisstadt → Großstadt → Metropole** (4 Städte = **3 Prestige-Übergänge**, max. 3 Prestige;
Reihenfolge gesetzt, Namen provisorisch — §16). Jede Stadt ist größer und thematisch eigener (rein ästhetische
World-Tiers). Prestige ist ein seltenes **Akt-Finale**; die **Metropole ist die Endstadt** mit Soft-Infinite-Endgame
(Meistergrade) — vollständiges Modell → [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md).

---

## 6. Systeme im Detail (Re-Cast des Originals)

**Leitprinzip:** Fantasie & beste Hooks des Originals behalten, alles **als physische 3D-Welt-Interaktion**
neu gießen, schwere 2D-UI-gebundene Sim-Systeme ausdünnen. Übersicht:

| Original-System | Im 3D-Idle | Verdikt |
|-----------------|-----------|---------|
| 10 Werkstatt-Typen | 10 buildbare **Stationen/Plots** | **Behalten** (Kern) |
| Arbeiter (10 Tiers, Mood/Fatigue/Training) | Hireable **NPC-Stationspersonal**, 3–5 Stufen, „Happiness=Tempo" light | **Stark vereinfacht** |
| Aufträge (6 Typen, Risk/Material) | **Kunden-Queue** am Tresen + **Restaurierungs-Aufträge** | **Stark vereinfacht** |
| 10 Mini-Games (pro Auftrag) | Optionale **„Perfekt-Aktion"-Boosts**, 2–3 Stück | **Recast/optional** |
| Crafting (30 Rezepte) + Markt | Leichte **Material-Versorgungs-Schicht** | **Stark gekürzt** |
| Warehouse (Slots/Stacks) | **Lager-Kapazität** als Upgrade-Pad | **Vereinfacht** |
| Forschung (45 Nodes) | In lineare **Upgrade-Ökonomie + Imperium-Marken-Perkboard** gefaltet | **Gekürzt/gefaltet** |
| Reputation (0–100) | **Stadt-Stern-Rating (1–5★)** als Distrikt-Gate | **Recast** |
| Prestige/Ascension/Eternal Mastery | **Max. 3 Prestige** (Akt-Finale, 4 Städte) + permanenter Meisterschafts-Track + Endgame-Meistergrade | **Neu gedacht (§7)** |
| Master-Tools (12 Artefakte) | **3D-Sammler-Boosts** auf einem Regal im Hof | **Behalten** (guter Fit) |
| Gilde/Multiplayer (Firebase) | **Post-MVP** (Genre ist v. a. Single-Player) | **Verschoben** |
| Events/Lieferant/Live-Ops | Daily-Reward, Daily-Tasks, Rush-Events, Saison-Deko | **Behalten, light** |

### 6.1 Werkstätten / Stationen
Die 10 Handwerks-Arten (Schreiner, Klempner, Elektriker, Maler, Dachdecker, Bauunternehmer, Architekt,
Generalunternehmer, Meisterschmied, Innovationslabor) bleiben als **distinkte Stationen** erhalten — jede
mit eigenem Modell, eigener Ware (Trag-Prop) und eigenem Tresen. Upgrade-Achsen pro Station: **Produktionstempo**,
**Stapel-/Tresen-Kapazität**, **Verkaufswert**. Reihenfolge der Freischaltung folgt der Distrikt-/Stern-Progression.

**Sichtbare Ausbau-Stufen (Open-Shop-Konzept, Nutzer-Entscheidung 12.06.):** Jede Werkstatt hat **3 kaufbare
Ausbau-Stufen**, die das Gebäude *sichtbar* verändern (Anbau-Modul → Markise/Schild → Schornstein/Stockwerk) und
je einen Produktions-/Wert-Bonus geben. Der Spieler SIEHT am Gebäude, wie weit es ist — das ist ein laufendes
Fortschritts-Ziel pro Station (gegen „nur hin-und-her-laufen"). Ausgebaute Werkstätten zählen zusätzlich ins
Stern-Rating (§7). Anbau-Module kommen aus der Asset-Pipeline; Domain: `StationBuildLevel` + geometrische Kosten + Save.

### 6.2 Arbeiter (NPC-Automatisierung) + Verwaltung
Der emotionale Kern bleibt, die Tiefe wird arcade-tauglich:
- **Pro Station 1 anstellbarer Arbeiter** (NPC), der das Tragen/Bedienen übernimmt → Automatisierung.
- **5 Leistungsstufen je Arbeiter** (Basis + 4 Tempo-Stufen, geometrische Kosten, +50 %/Stufe). Umgesetzt im Domain-Kern.
- **Sichtbare Karren-Logistik (Hencoop-Konzept):** Der Worker zieht den **Handkarren** (Asset vorhanden) mit
  sichtbar geladenen Waren Station → Tresen, statt nur zu laufen — die Lieferkette wird beobachtbar.
- **Verwaltung statt Boden-Platten (Nutzer-Entscheidung 12.06.):** Anstellen + Stufen laufen über ein **HUD-
  Verwaltungs-Panel** (Button → Liste aller 10 Gewerke: Status/Stufe/Kosten, direkt kaufen), jederzeit erreichbar.
  Die früheren Hold-to-Pay-Boden-Platten für Hire/Boost/Gratis-Geld **entfallen** (zu unübersichtlich); in der
  Welt sind als HUD-Elemente nur **Geld, Gems, Stern/Stufe** + das Verwaltungs-Panel sichtbar.
- **Kein** komplexes Mood/Fatigue/Training-Modell, **keine** Kündigungen, **kein** Müdigkeits-Mikromanagement
  (bewusste Kürzung ggü. Original, §15.6). **Manager-/Vorarbeiter-Idee** optional Post-MVP.

### 6.3 Aufträge & Kunden-Queue
- **Standard:** Kunden erscheinen am Tresen und wollen Ware → bedienen = Geld. Dauerhafter Grund-Loop, keine Auswahl-UI.
- **Spezial-Aufträge** (gelegentlich, optional): „Eil-Auftrag" mit Timer für Bonus, per Ad verlängerbar. Erbt die Idee von Live-/VIP-Aufträgen, aber **ohne** Risk/Reward- und Material-Offer-Komplexität im Front-Loop.

### 6.4 Restaurierungs-Aufträge & Stadt-Wiederaufbau
Die **Meilenstein-Schicht**, die das Welt-Ziel trägt:
- Jeder Distrikt hat ein **Wahrzeichen in Ruine**. Ein Restaurierungs-Auftrag verlangt ein Bündel Ressourcen/Geld (über Zeit gesammelt) → bei Abschluss **baut sich das Wahrzeichen sichtbar auf** (5 Bauphasen, wie im alten Plan für Mega-Projekte) + Hans-Cutscene + Stern-Gewinn.
- Liefert das befriedigende „Vorher/Nachher" und ein Fernziel pro Stadt.

### 6.5 Material / Versorgung (light)
- Pro Distrikt ein **Versorgungs-Pad** (Holz/Metall/…), das sich langsam leert. Nachfüllen per Hold-to-Pay, per **Ad-Sofortfüllung**, oder automatisiert durch einen Lieferanten-Upgrade.
- Ersetzt den 30-Rezepte-Crafting-Tree + Material-Markt + Worker-Affinität des Originals durch **eine** sichtbare, leicht verständliche Engpass-Mechanik. (Größte Kürzung — bewusst.)

### 6.6 Master-Tools (Sammler-Boosts)
Die 12 Meisterwerkzeuge bleiben als **permanente Einkommens-Boosts**, jetzt als **3D-Collectibles** auf einem
Werkzeug-Regal/Altar im zentralen Hof. Freischalt-Bedingungen (Station-Level, Auftrags-/Sanierungs-Zahlen,
Prestige) bleiben sinngemäß. Guter, günstiger „Sammel"-Anreiz mit sichtbarem Trophäen-Wert.

### 6.7 Optionale „Perfekt-Aktionen" (Mini-Game-Recast)
Statt 10 Pflicht-Mini-Games pro Auftrag (genre-fremd) bleiben **2–3 optionale Mikro-Interaktionen** als
kurze **Tap-Timing-Boosts** — z. B. „Hau-den-Nagel" / „Säge-Schnitt" für einen kurzen 2x-Tempo-Buff an einer
Station. Freiwillig, sekundenkurz, nie blockierend. Die übrigen Mini-Game-Konzepte entfallen im Front-Loop.
**UI-Heimat (Nutzer-Entscheidung 12.06.):** Der Boost wird vom **Verwaltungs-Panel** (§6.2) je Gewerk gestartet
(HUD-Overlay), **nicht** als Boden-Platte in der Welt. Domain-Buff (`StationState.BoostMultiplier`) ist umgesetzt.

### 6.8 Lebendige, reagierende Welt (Innovations-Schicht)
Gegen den „nach 5 Minuten langweilig"-Effekt lebt die Welt sichtbar mit dem Fortschritt (Nutzer-Anspruch
„immer innovativ", Memory `feedback_innovativ_praesentation`):
- **„Die Stadt heilt":** Der Sanierungs-Fortschritt der Wahrzeichen (§6.4) steuert die globale Farbsättigung
  und schaltet bei 1/3, 2/3, 100 % zusätzliche Erblüh-Deko frei (Blumenbeete, Büsche). Umgesetzt: `WorldMoodController`.
- **Atmosphäre:** Wind-Sway auf Vegetation (`CrownSway`), Schornstein-Rauch nur an freigeschalteten Werkstätten,
  sanft treibende Blätter über dem Hof. Alles dezent, performant (keine Schatten-Punktlichter auf Mobile).
- **Geplant weiter:** Lieferwagen-/Karren-Moment beim Offline-Einsammeln; kleine Statisten (Vögel) als Zukunft.

---

## 7. Progression & Prestige

**Prestige ist auf max. 3 gedeckelt** und damit ein seltenes, zeremonielles **Akt-Finale** — **nicht** der
Haupt-Treadmill. Die **Monate**-Progression tragen parallele Vektoren + ein permanentes Rückgrat + ein
Soft-Infinite-Endgame. **Vollständiges Modell, Kurven, Pacing & Mathematik → [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md).**

| Ebene | Mechanik | Reset bei Prestige | Belohnung |
|-------|----------|:------------------:|-----------|
| **Station-Level (+Meilensteine)** | Hold-to-Pay (Tempo/Kapazität/Wert), Meilenstein-Sprünge L10/25/50/100/200 | ✅ | Mehr Einkommen/s, sichtbares Upgrade |
| **Distrikt / Sanierung** | Werkstätten + Restaurierung | ✅ | Wahrzeichen, neue Handwerks-Art, Stern |
| **Stern-Rating (1–5★)** | Aggregat, Hysterese; Schwelle steigt je Stadt | ✅ | Distrikt-Gate; 5★ = Prestige bereit |
| **Prestige = Akt-Finale (max. 3×)** | 5★ → Reset + Umzug (4 Städte: Dorf→Kreisstadt→Großstadt→Metropole) | — | **Permanenter Multiplikator** (~×3/×4/×5) + Imperium-Marken + Cinematic |
| **Meisterschafts-Track** | kontoweite XP aus allem; `1.15^N`, 100+ Level | ❌ **nie** | kleiner permanenter Global-Bonus/Level — **das Langzeit-Rückgrat** |
| **Imperium-Marken-Perkboard** | Marken aus Prestiges + Meilensteinen | ❌ | Start-Geld, Offline-Cap, Tempo, Auto-Collect-Radius … |
| **Master-Tools (12)** | Bedingungen über alle Akte gestreut | ❌ | permanente Boni, Sammlung |
| **Endgame-Meistergrade** | nach P3 in der Metropole; `Renommee = base × 1.5^R` | ❌ | **Soft-Infinite** kleine Permanent-Boni — der „Spiel-für-immer"-Schwanz |

**Pacing-Ziel:** Prestige 1 ~Woche 1 · Prestige 2 ~Woche 3 · Prestige 3 ~Woche 8–10 · danach Endgame über
**Monate**. Akte werden **länger**, nicht kürzer (der Multiplikator beschleunigt den Re-Climb, die größere Stadt
verlängert ihn wieder). Aggregierte Multiplikatoren laufen durch den **Log2-Soft-Cap** (`IncomeFormulas`) → Zahlen bleiben lesbar.

**Warum gedeckelt statt unendlich:** 4 handgebaute Städte + zeremonielles Prestige + permanente Systeme halten
Spieler **gesünder über Monate** als repetitiver Reset-Grind. Das „Unendliche" liefert der Meistergrad-Loop —
**bewusst kein 4. Prestige.** **Prestige-Cinematic** (Gold-Regen → Stadt-Reveal → Multiplier-Text) bleibt der Höhepunkt.
(Original-Stack 7-Tier/Ascension/Eternal/Heirloom/Challenges → §15.5.)

---

## 8. Offline-Earnings

Pflicht im Idle-Genre. Übernimmt die bewährte Offline-Logik (Staffelung, Cap) sinngemäß:
- Bei Rückkehr: **„Während du weg warst"-Dialog** mit verdientem Betrag.
- **Cap** abhängig von Imperium-Marken-Perkboard/Premium; **per Ad verdoppelbar** (zentrale Ad-Platzierung).
- Offline-Rate skaliert mit Automatisierungsgrad (angestellte Arbeiter) → Anstellen lohnt doppelt.
- Wiederverwendbar: `OfflineProgressFormulas` aus dem Domain-Port (Staffel 0.80/0.35/0.15/0.05) als Startwerte.

---

## 9. Monetarisierung

Bewährtes Original-Modell, auf Genre-Normen gemappt (Ads tragen idle-arcade typisch den Großteil):

### 9.1 Rewarded Ads (~8 Platzierungen statt 13)
| Platzierung | Effekt |
|-------------|--------|
| **Free-Cash-Pad** | 2× aktuelles Einkommen / Zeitblock (zentrale Geld-Quelle) |
| **Offline ×2** | Offline-Verdienst verdoppeln (Rückkehr-Dialog) |
| **Sofort-Material** | Versorgungs-Pad instant füllen |
| **Tempo-Boost** | Alle Stationen kurz 2× (Rush) |
| **Glücksrad** | 1×/Tag, Gems/Cash/Boost |
| **Auftrags-/Sanierungs-×2** | Belohnung eines Spezial-/Restaurierungs-Auftrags verdoppeln |
| **Skip-Timer** | Bau-/Forschungs-Wartezeit überspringen |
| **Worker-Boost** | Müden Arbeiter sofort auf Volltempo |

### 9.2 Premium „Imperium-Pass" (4,99 € Lifetime — Preis unverändert)
**Keine Werbung**, **Auto-Collect** (riesiger QoL-Gewinn im Walk-around-Idle), **+Einkommen**, höherer
**Offline-Cap & -Multiplikator**, exklusiver **Avatar-Skin**, früheres Auto-Boost. Bestehende Avalonia-Käufer
mit `IsPremium` bekommen den Pass auch hier (Migration-Bonus).

### 9.3 Gems / Goldschrauben (Hartwährung) + IAP
Quellen: Aufträge, Sanierungen, Daily, Achievements, Glücksrad, Rewarded, IAP. Verwendung: Sofort-Unlocks,
Skins, Boosts. IAP-Pakete (z. B. 50/150/450) + Whale-Bundles (9,99 / 19,99 / 49,99 €, Mega inkl. Premium) — wie Original.

### 9.4 Cosmetics (neu, genre-stark)
**Avatar-Skins**, **Werkstatt-Skins**, **Stadt-Deko-Themes** (z. B. Weihnachts-Hansstadt). Niedrigschwellige,
druckfreie Monetarisierung mit hohem Sichtbarkeitswert in der 3D-Welt.

---

## 10. Live-Ops & Retention

- **Daily Reward** (30-Tage-Leiter, skalierend) + **Daily Tasks** (3 kleine Ziele → Gems).
- **Rush-Events** (zeitbegrenzt 2×-Phasen, 1×/Tag gratis).
- **Saisonale Events** (4/Jahr): Stadt-Deko-Override + Event-Währung + Event-Shop (Saison-Story-Beat von Hans).
- **Push-Notifications** (sinngemäß aus Original): Offline-Cap voll, Sanierung fertig, Rush verfügbar, Daily bereit — Meister-Hans-Persona-Präfix.
- **Achievements** + **What's-New**-Dialog beim Update (Workflow wie Original).
- **Verschoben (Phase 3+):** Leaderboards, optionale „Gilde-lite" / Co-op-Events. Firebase-Pfade bleiben kompatibel, falls reaktiviert.

---

## 11. Audio

- **Meister-Hans-Voice** (ElevenLabs Multilingual-Standard-Voice, 6 Sprachen) — Tutorial, Meilenstein-,
  Sanierungs- und Prestige-Kommentare. Die ~900–1500 geplanten Voice-Files (ASSETS_AI.md) passen direkt auf die neuen Beats.
- **Musik:** entspannte Werkstatt-Loops, hochschaltend mit Stadtgröße/Rush; Sanierungs-/Prestige-Stinger.
- **SFX:** Säge/Hammer/Münzen/Pickup/Upgrade-„Cha-Ching"/Bau-Aufbau. 3D-Positional via Unity AudioMixer.
- Pipeline: Stable Audio 3 (SFX/Musik) + ElevenLabs (Voice), OGG, Addressables — wie ASSETS_AI.md.

---

## 12. Tech-Umsetzung (Unity)

Stack & Conventions **unverändert** aus [CLAUDE.md](CLAUDE.md). Neue/angepasste Game-Layer-Systeme:

| System (Game-Layer) | Aufgabe |
|---------------------|---------|
| `AvatarController` | CharacterController + Joystick/Tap-to-move, Animator-States, Carry-Stack-Visual |
| `InteractionTriggerSystem` | Annäherungs-Trigger an Stationen/Geld/Pads (Auto-Carry/Pickup/Abgabe) |
| `EconomyService` | Einkommen/s, Cash-Spawn (physische Münzen, GPU-instanziert), Auto-Collect-Radius |
| `StationService` | Produktion, Stapel, Tresen, Verkaufswert je Station |
| `WorkerAutomationService` | NPC-Anstellung, Pfad-Tragen (NavMesh light), Stufen, Tempo-Boost |
| `UpgradePadService` | Hold-to-Pay mit rampender Ausgaberate, Kosten-Kurven |
| `PlotUnlockService` | Bauzaun-Plots, Distrikt-Gates, Fog-of-War-Reveal |
| `TownRestorationService` | Wahrzeichen-Bauphasen, Distrikt-Zustände (Ruine→saniert), Stern-Rating |
| `FranchisePrestigeService` | Prestige = neue Stadt, permanenter Multiplikator, Imperium-Marken-Perkboard |
| `OfflineProgressService` | Rückkehr-Verdienst (Formeln aus Domain-Port wiederverwenden) |
| `SaveSystem` | **Neues Schema** (Slices passen sich an); HMAC-Anti-Cheat-Pattern aus Original beibehalten |

> **CLEAN-SLATE (Nutzer-Entscheidung):** Der alte 1:1-Domain-Port wurde **vollständig aus dem Working-Tree
> entfernt** — `Domain/` enthält nur noch den neuen Idle-Core (`Idle/`) + die pure Offline-Staffel (`Offline/`).
> Alle unten als „direkt nutzbar/ruht/erhalten" beschriebenen Alt-Dateien sind **gelöscht** und ausschließlich
> über den git-Tag `hwi-unity-domain-port-pre-cleanslate` reaktivierbar. Wo P1 eine Alt-Formel braucht (z. B.
> Log2-Soft-Cap), wird sie **schlank neu** im Idle-Namespace gebaut statt den Alt-Schwanz zurückzuholen. Die
> folgende Liste gilt damit nur noch als **historische Einordnung**, nicht als Working-Tree-Inventar.

**Wiederverwendung des Domain-Ports (historische Einordnung — Code via git-Tag, nicht im Working-Tree):**
- **War direkt nutzbar:** `IncomeFormulas` (Soft-Cap/Log2, Multiplikatoren), `OfflineProgressFormulas`,
  `AutoProductionFormulas` (Intervalle), Teile von `EquipmentFormulas`/Master-Tool-Boost-Arithmetik,
  Enum-/Katalog-Slices als Themen-Referenz, der **SaveGame-/HMAC-/DateTime-Infra-Stack**.
- **Ruht (vorerst nicht im Spiel):** tiefes Crafting (30 Rezepte), Material-Markt, Forschungs-Tree (45 Nodes),
  Gilde/Co-op/Auktion/Mega-Projekt, Ascension/Eternal-Mastery/Heirloom/Challenges, Mood/Fatigue/Training-Detail.
  Code bleibt im Repo als Referenz/Reaktivierungs-Option, ist aber **nicht** Teil des MVP-Soll.
- **Save-Schema:** nicht mehr „Avalonia-v7 1:1". **Neues, schlankeres Schema** mit eigenen Slices
  (Town, Stations, Workers, Restoration, Franchise, Cosmetics, …). Migrierbarkeit & HMAC-Signatur-Pattern bleiben.

**Assembly-Hierarchie** (Core→Domain→Game→UI→Bootstrap) bleibt. Die neuen Systeme liegen im **Game-Layer**;
Domain bleibt Unity-frei und NUnit-testbar (Wirtschafts-Formeln). Coverage-Ziele unverändert (Domain ≥80%).

---

## 13. Asset-Bedarf (3D)

Baut auf der KI-Pipeline aus [ASSETS_AI.md](ASSETS_AI.md) auf (SDXL→TRELLIS 2/TripoSG→Blender→Substance→
Mixamo/Cascadeur→Unity URP/Addressables). **Neu/zusätzlich** ggü. dem alten 3D-Plan:

| Kategorie | Bedarf | Hinweis |
|-----------|--------|---------|
| **Spieler-Avatar** | 1 gerigteter Held + Carry-Anim + 3–5 Skins | Kritischer Pfad (Rig/Anim) |
| **NPC-Arbeiter** | ~6–10 Varianten (Handwerks-Outfits) + Carry/Walk | Mixamo-Reuse, Recolor |
| **Stationen** | 10 Werkstatt-Stationen + Tresen + Upgrade-Pad-Marker | 1 Basis + Decal-Sets |
| **Waren / Carry-Props** | ~10 Trag-Stapel-Props (je Handwerk) | GPU-Instancing |
| **Geld-Pickups** | Münzen/Scheine/Goldschrauben | Instanziert, billig |
| **Stadt-Kit** | Modulare Wege/Deko/Bauzäune, **Ruine↔saniert** je Modul | World-Tiers wie alt §12 |
| **Wahrzeichen** | ~4–6 Distrikt-Wahrzeichen × 5 Bauphasen | Hero-Assets, ggf. Cloud-Fallback |
| **Franchise-Karte** | Stadt-Icons / Karten-Props | klein |
| **VFX** | Pickup-Funken, Upgrade-Glow, Sanierungs-/Prestige-Burst, Rush-Aura | URP-Partikel/Shader |

Asset-Ökonomie wie gehabt: **1 Basis-Mesh + Decal-Material-Sets** statt vieler Modelle, aggressives Recycling.

---

## 14. Roadmap / Phasen

| Phase | Inhalt | Gate / KPI |
|-------|--------|------------|
| **P0 — Greybox-Prototyp** ([Detail-Spec](P0_GREYBOX_PROTOTYP.md)) | 1 Hof, 3 Stationen, Walk-Collect-Upgrade-Hire-Unlock, Offline. Reine Würfel-Assets. | **Fun-Check:** macht der Loop ohne Grafik Spaß? Go/No-Go. |
| **P1 — Vertical Slice** ([Detail-Spec](P1_VERTICAL_SLICE.md)) | 1 volle Stadt (Hansstadt), ~10 Stationen, Arbeiter-Automatisierung, Stern-Rating, 1 Prestige in Stadt 2, Hans-Intro-Voice, Kern-Ads + Premium. | D1-Retention-Gefühl, Loop-Tuning sitzt. |
| **P2 — Content** ([Detail-Spec](P2_CONTENT.md)) | Franchise-Karte/mehrere Städte, Restaurierungs-Distrikte + Cutscenes, Master-Tools, Cosmetics, Daily/Tasks, Saison. | Content-Tiefe für ~Woche-1-Spieler. |
| **P3 — Social/Live + Beta** ([Detail-Spec](P3_SOCIAL_BETA.md)) | Leaderboards, optional Gilde-lite/Events, Push, What's-New. **Closed Beta** unter Beta-App-ID. | Beta-Retention/Monetarisierungs-KPIs. |
| **P4 — Polish & Cutover-Entscheid** ([Detail-Spec](P4_POLISH_CUTOVER.md)) | Balancing gegen KPIs, Performance-Pass Low-End, Lokalisierung 6 Sprachen, Store-Assets. | Go/No-Go für Cutover ggü. Avalonia. |

**Asset-Pipeline-Pilot** (aus altem Plan) kann **parallel ab P0** starten — kritischer Pfad bleibt
**Avatar-/Worker-Rig & -Animation** (jetzt zusätzlich der spielbare Held). Greybox-Loop braucht **keine** finalen Assets.

**Ziel-KPIs (Genre-Benchmark, in Beta zu treffen):** D1 ≥ 35–40 %, D7 ≥ 12–15 %, Session ~4–6 min,
mehrere Sessions/Tag, Ad-getriebener ARPDAU + Premium-Conversion. (Idle-Arcade lebt von hoher Frequenz × Ads.)

---

## 15. Bewusst (vorerst) weggelassen — Detail

Die Neuausrichtung gewinnt Zugänglichkeit, Tempo und Genre-Fit — und **kostet Tiefe**. Dieser Abschnitt
arbeitet jede zurückgestellte Mechanik sauber auf: **was sie im Original ist**, **warum sie (vorerst) raus
ist**, **was ihre Funktion im 3D-Idle übernimmt** und **unter welcher Bedingung sie zurückkommt**. Grundsatz:
**nichts geht verloren** — Thema, Werte und der bereits portierte Code bleiben über die git-History reaktivierbar.

> **Hinweis (Clean-Slate):** Der portierte Alt-Code wurde auf Nutzer-Entscheidung **aus dem Working-Tree entfernt**
> (git-Tag `hwi-unity-domain-port-pre-cleanslate`). „Erhalten/✅" unten heißt ab jetzt **„über git-Tag reaktivierbar"**,
> nicht „liegt im Repo-Working-Tree".

### 15.1 Tiefes Crafting & Material-Wirtschaft → leichte Versorgungs-Schicht
- **Original:** 30 Rezepte (10 WS × 3 Tiers), Cross-Workshop-Inputs ab Lv100, T4-Manufaktur (villa/skyscraper/imperium_hq), Material-Markt mit deterministischem Tagespreis (Seed + Sinus ±50 %, 5 % Spread, Event-Modulatoren), Worker-Material-Affinität (+20 % Crafting-Speed).
- **Warum raus:** Rezept-Tree + Börsen-Arbitrage + Reservierungs-Logik ist Sim-Tiefe, die den Walk-around-Loop bremst und die „in 10 Sekunden verständlich"-Säule bricht.
- **Ersatz (§6.5):** **ein** sichtbares Versorgungs-Pad pro Distrikt (leert sich → nachfüllen per Hold-to-Pay / Ad-Sofortfüllung / Lieferanten-Upgrade). Eine einzige, greifbare Engpass-Mechanik statt einer Wirtschaftssimulation.
- **Reaktivierung:** Wenn die Beta nach Mid-/Late-Game-Substanz verlangt → Crafting als **optionaler „Manufaktur"-Distrikt** wieder einsetzbar (Aufwand: mittel — UI + 3D-Regale + Markt-Heatmap).
- **Erhalten:** `CraftingFormulas.cs`, `MarketFormulas.cs`, Rezept-Katalog, Affinitäts-Logik (portiert/dokumentiert).

### 15.2 Warehouse-Tiefe → eine Kapazitäts-Achse
- **Original:** 20→200 Slots, Stack-Limit 50, `Available = Crafting − Reserved`, Auto-Sell bei Overflow, Stack-Prüfung vor Job-Start/Collect (kein Material-Burn).
- **Warum raus:** Slot-/Stack-/Reservierungs-Management ergibt nur Sinn **mit** tiefem Crafting — fällt mit ihm weg.
- **Ersatz:** **Lager = eine** Hold-to-Pay-Kapazitäts-Upgrade-Achse (begrenzt, wie weit Stationen ohne Abholung vorproduzieren).
- **Reaktivierung:** gemeinsam mit 15.1.
- **Erhalten:** Warehouse-Service-Logik als Referenz.

### 15.3 Forschungs-Tree → gefaltet ins Imperium-Marken-Perkboard
- **Original:** 45 Nodes in Branches (inkl. Logistics-Branch) + 18 kollaborative Gilden-Forschungen in 6 Kategorien (permanente Boni).
- **Warum raus:** Ein 45-Node-Tech-Tree ist ein **zweites** Meta-System neben Prestige — zu viel UI-Last für das Genre.
- **Ersatz (§7):** die wirkungsstärksten permanenten Boni wandern in die **lineare Upgrade-Ökonomie** (Pads) + das **Imperium-Marken-Perkboard** (Prestige-Perks). Optional ein kleines 3D-„Meister-Werkstatt"-Brett mit ~6–10 Upgrades.
- **Reaktivierung:** Tree als spätes Tiefen-Feature (Effekt-Werte dokumentiert).
- **Erhalten:** ResearchNode-Katalog + Effekt-Werte als Referenz.

### 15.4 Equipment, Workshop-Spezialisierung & Rebirth → gefaltet
- **Original:** Ausrüstungs-Shop + Drops (`CalculateDropChance` 0.05 + diff×0.05 + perfect×0.05) + Slots; Workshop-Spezialisierung (Efficiency/Quality/Economy ab Lv50, Re-Spec 20 GS); Rebirth (0–5 Sterne, +15…+150 % Income).
- **Warum raus:** Drei überlappende Buff-Schichten auf Worker/Workshop sind im schlanken Loop redundant.
- **Ersatz:** Station-Upgrades (Pads) + **Master-Tools** (§6.6) decken die „permanenter Boost"-Fantasie ab; das **Stern-Rating** ersetzt Reputation/Spezialisierung als sichtbares Progressions-Ziel.
- **Reaktivierung:** Equipment als cosmetic-nahe Boost-Schicht später denkbar.
- **Erhalten:** `EquipmentFormulas.cs` (Drop-Chance), Spec-/Rebirth-Werte als Referenz.

### 15.5 Prestige-Tiefe → max. 3 Prestige + permanenter Meisterschafts-Track + Endgame-Meistergrade
- **Original:** 7 Prestige-Tiers (PP = floor(sqrt(money/100k)), Boni +20…+800 %, Diminishing ×1/(1+0.1·tierCount), Cap 20×), Bewahrungs-Stufen, 6 Challenges, Bonus-PP-Quellen, kumulative Meilensteine, **Heirlooms** (Tier-4 überlebt Prestige), **Ascension** (6 Perks × Lv3 = 54 AP, Vollreset nach 3× Legende), **Eternal Mastery** (Late-Game-Income-Bonus, kein Ascension-Reset).
- **Warum geändert:** Drei gestapelte Reset-Ebenen sind Hardcore-Idle-Tiefe; **unendliches** Prestige stumpft ab. Genre- **und** Langzeit-Fit = **wenige, zeremonielle** Prestiges + dauerhafte Systeme.
- **Ersatz (§7 / [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)):** **max. 3 Prestige** (Akt-Finale, 4 Städte) mit starkem permanentem Multiplikator + **Imperium-Marken-Perkboard**. **Heirloom/Ascension/Eternal** werden zu **einem** permanenten **Meisterschafts-Track** (kontoweit, nie reset) + einem **Soft-Infinite-Endgame-Meistergrad-Loop** (nach P3) verdichtet — die Eternal-Mastery-Idee kehrt also **als Track zurück, NICHT als Prestige**. Das ist die **Monate-Spielzeit-Lösung** trotz gedeckeltem Prestige.
- **Reaktivierung:** zusätzliche Challenges/Perk-Tiefe später additiv (ohne die Basis zu brechen).
- **Erhalten:** PP-/Tier-/Ascension-/Eternal-Mastery-Formeln als Referenz (der Income-Soft-Cap wird ohnehin wiederverwendet).

### 15.6 Worker-Sim-Tiefe → Hire + Stufen + Tempo-Boost
- **Original:** 10 Tiers (F…Legendary), EffectiveEfficiency aus 7 Faktoren (XP · Mood · Fatigue · (1+Spec+Equip) · Personality · Talent), Mood-Decay & **Kündigung** bei Mood<20, 3 Trainings-Typen, Auto-Rest, Praktikanten, Aura-Bonus (S-Tier+), Legende-Prestige sichert Top-Worker.
- **Warum raus:** Mood/Fatigue/Training/Kündigung ist Mikromanagement, das dem „anstellen und es läuft"-Idle-Versprechen direkt widerspricht.
- **Ersatz (§6.2):** NPC-Arbeiter **anstellen** (= Automatisierung) + **3–5 Tempo-/Trag-Stufen**; optional **leichtes „Laune = Tempo"** mit Ad-Boost, **keine** Kündigung. Manager-Idee → optionaler „Vorarbeiter".
- **Reaktivierung:** Tiefere Worker-Progression (Tiers/Talente/Affinität) als Mid-Game-Layer.
- **Erhalten:** `WorkerFormulas.cs` (Mood/Fatigue/Level/Efficiency) — der „leichte" Modus nutzt eine **Teilmenge** davon.

### 15.7 Auftrags-Komplexität & Pflicht-Mini-Games → Kunden-Queue + optionale Tap-Boosts
- **Original:** 6 OrderTypes (Quick/Standard/Large/Cooperation/Weekly/MaterialOrder), Risk/Reward (Safe 0.75× / Standard / Risk 2.0× mit Hard-Fail), Material-Offer (atomare Reservierung, „echtes Risiko"), Stammkunden (1.1–1.5×), Live-/VIP-Aufträge (ExpiresAt 45–180 s, 3× Reward), bis zu 3 parallele Aufträge; **10 Mini-Games** (eines je Auftrag, Rating Perfect/Good/Ok/Miss).
- **Warum raus:** Pro-Auftrag-Auswahl + Risk-Layer + **Pflicht**-Mini-Game pro Abschluss bricht den „laufen & einsammeln"-Flow (genre-fremd).
- **Ersatz (§6.3, §6.7):** dauerhafte **Kunden-Queue** am Tresen (bedienen = Geld) + gelegentliche **Eil-Aufträge** (Timer, per Ad verlängerbar); Mini-Games → **2–3 optionale Tap-Timing-Boosts** für temporäres 2× an einer Station.
- **Reaktivierung:** einzelne Mini-Games als optionale „Events"; Order-Typen-Vielfalt als Mid-Game-Variation.
- **Erhalten:** `OrderGenerationFormulas.cs`, die 10 dokumentierten Mini-Game-Konzepte, Rating-Logik.

### 15.8 Gilde / Online-Multiplayer → Phase 3+ (optional)
- **Original:** Gilden-Hub (Übersicht/Kampf/Forschung/Chat/Mitglieder), Gildenkrieg, Boss-Kämpfe (6 Bosse), Co-op-Orders, Worker-Auktionen, **Mega-Projekte** (wochenlange Spenden-Pipeline, permanente Gildenboni), Hall-Gebäude, Firebase-RTDB + HMAC + PlayerId.
- **Warum raus:** Die prägenden Genre-Hits (My Perfect Hotel etc.) sind primär **Single-Player**; Online-Soziales ist teuer (Backend, Anti-Cheat, Live-Ops) und nicht MVP-kritisch.
- **Ersatz:** im MVP **keiner** (bewusst) — der Single-Player-Loop trägt Retention über Offline-Earnings + Daily + das Sanierungs-Fernziel. Die **Mega-Projekt-Idee** ist als Solo-**„Restaurierungs-Auftrag"** (§6.4) bereits ins MVP recycelt.
- **Reaktivierung:** **Phase 3+** als optionaler Online-Layer — **Firebase-Pfade & HMAC-Pattern bleiben kompatibel** (CLAUDE.md §7), Reaktivierung ohne Schema-Bruch geplant.
- **Erhalten:** `GuildBossFormulas.cs`, alle Gilden-DTOs/Enums, Mega-Projekt-Bauphasen-Logik.

### 15.9 Onboarding, BattlePass & sonstige Live-Ops-Tiefe → schlanker
- **Original:** FTUE 10-Step State-Machine (Spotlight-Overlays), BattlePass (30-Tier-Saison, Free/Premium), 8 Event-Typen + Lieferant + Feierabend-Rush, Reputation-Shop, Referral, Cross-Promotion.
- **Warum raus/leichter:** Der Walk-around-Loop **lehrt sich selbst** (laufen → Münzen sehen → Pad kaufen) → schwere FTUE-Maschine unnötig; BattlePass + voller Event-Zoo überfrachten den MVP.
- **Ersatz (§10):** **Hans-Voice-geführtes** „learn-by-doing"-Onboarding (3–4 Beats) + Daily-Reward + Daily-Tasks + Rush-Events + Saison-Deko. Referral/Cross-Promo bleiben (günstig, wirksam).
- **Reaktivierung:** BattlePass als **Phase-2**-Live-Ops-Layer (starkes ARPU-Werkzeug).
- **Erhalten:** BattlePass-/Event-/Referral-Logik portiert bzw. dokumentiert.

### 15.10 Überblick — Status & Reaktivierungs-Trigger

| System | MVP-Status | Funktion übernimmt im MVP | Reaktivierungs-Trigger | Code/Werte |
|--------|-----------|---------------------------|------------------------|-----------|
| Tiefes Crafting + Material-Markt | ❌ raus | Versorgungs-Pad (§6.5) | Beta will Mid-Game-Tiefe | ✅ Formulas + Katalog |
| Warehouse-Tiefe | ⚠️ vereinfacht | Lager-Kapazitäts-Upgrade | mit Crafting zusammen | ✅ |
| Forschungs-Tree (45 + 18) | ❌ gefaltet | Imperium-Marken-Perkboard + Pads (§7) | mehr Meta gewünscht | ✅ Katalog/Effekte |
| Equipment + Spec + Rebirth | ❌ gefaltet | Station-Upgrades + Master-Tools | optionale Boost-Schicht | ✅ EquipmentFormulas |
| Prestige-Tiefe (7T/Ascension/Eternal/Heirloom/Challenges) | ⚠️ max. 3 Prestige | 3× „Neue Stadt" + Meisterschaft + Endgame-Meistergrade (§7) | Challenges/Perk-Tiefe additiv | ✅ Formeln |
| Worker-Sim (Mood/Fatigue/Training/Tiers…) | ⚠️ light | Hire + Stufen + Boost (§6.2) | Mid-Game-Worker-Layer | ✅ WorkerFormulas |
| Auftrags-Komplexität (6 Typen/Risk/Material) | ⚠️ vereinfacht | Kunden-Queue + Eil-Auftrag (§6.3) | optionale Events | ✅ OrderFormulas |
| 10 Pflicht-Mini-Games | ⚠️ optional | 2–3 Tap-Boosts (§6.7) | Mini-Game-Events | ✅ 10 Konzepte |
| Gilde/Multiplayer (Krieg/Boss/Coop/Auktion/Mega) | ❌ Phase 3+ | Solo-Restaurierung recycelt Mega | Online-Phase (Firebase bleibt kompatibel) | ✅ DTOs/Formulas |
| FTUE-Maschine | ❌ raus | Hans „learn-by-doing" (§10) | — | ✅ |
| BattlePass + voller Event-Zoo | ❌ Phase 2 | Daily/Tasks/Rush/Saison (§10) | Live-Ops-Ausbau | ✅ |

**Legende:** ❌ nicht im MVP · ⚠️ stark vereinfacht im MVP · ✅ Code/Werte über git-Tag `hwi-unity-domain-port-pre-cleanslate` reaktivierbar (aus Working-Tree entfernt).

**Netto:** Das MVP behält die **emotionalen Anker** (Hans, Arbeiter, Werkstätten, Master-Tools, Stadt-Aufstieg),
**drei** zeremonielle Prestiges + ein **permanentes Meisterschafts-/Endgame-Rückgrat** für Monate-Spielzeit —
und legt die schweren Sim-Schichten **geordnet** zur Seite, **ohne Code zu verlieren.**
Jede Streichung hat einen dokumentierten Reaktivierungs-Pfad; keine ist eine Sackgasse.

---

## 16. Entscheidungen (Defaults gesetzt)

Die zuvor offenen Punkte sind mit den Defaults **festgeschrieben** (überstimmbar, solange P1 nicht steht):

| # | Entscheidung | Gesetzt |
|---|--------------|---------|
| 1 | **Avatar-Identität** | Spielbares **Erbe/Erbin von Hans**, anpassbar (m/w + Skins). Default-Skin gratis, weitere als Cosmetic. |
| 2 | **Prestige (gedeckelt)** | **Max. 3 Prestige = 4 Städte:** Hansstadt → Kreisstadt → Großstadt → Metropole (Endstadt). Langzeit über permanente Vektoren + Endgame-Meistergrade statt mehr Prestige → [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md). |
| 3 | **„Laune = Tempo" (Arbeiter)** | **Aus** im MVP (maximale Idle-Einfachheit). Nur optionaler Ad-Tempo-Boost. Reaktivierbar als Mid-Game-Option. |
| 4 | **Material-/Versorgungs-Tiefe** | **Stufe 1:** genau **ein** Versorgungs-Pad pro Distrikt (nicht 0, nicht voller Crafting-Tree). |
| 5 | **Multiplayer** | Bleibt im MVP draußen, **als Phase-3-Ziel gesetzt** (nicht dauerhaft gestrichen); Firebase-/HMAC-Kompatibilität bleibt gewahrt. |

> Diese Defaults sind ab jetzt die Arbeitsgrundlage (u. a. für die P0-Spec). Einzelne Punkte jederzeit per Hinweis überstimmen.

---

## Verweise

- **Progression & Balancing (Langzeit-Modell, max. 3 Prestige, Monate):** [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)
- Unity-Tech-Conventions (weiter gültig): [CLAUDE.md](CLAUDE.md)
- Thema/Werte/Code-Referenz (Status: Referenz, nicht Soll): [DESIGN.md](DESIGN.md) · [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) · [PLAN_ABGLEICH_ORIGINAL.md](PLAN_ABGLEICH_ORIGINAL.md)
- Domain-Port-Roadmap (Mechanik-Teil abgelöst, Formel-/Save-Teil weiter nutzbar): [DOMAIN_3D_PLAN.md](DOMAIN_3D_PLAN.md)
- Asset-Pipeline: [ASSETS_AI.md](ASSETS_AI.md) · Architektur/Infra: [ARCHITECTURE.md](ARCHITECTURE.md) · Setup: [SETUP.md](SETUP.md)
- Avalonia-Original (bleibt produktiv): [../HandwerkerImperium/CLAUDE.md](../HandwerkerImperium/CLAUDE.md)
- Unity-Architektur-Vorbild (echter Code): [../ArcaneKingdom/CLAUDE.md](../ArcaneKingdom/CLAUDE.md)
