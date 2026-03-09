# Reborn Saga: Isekai Rising - Game Design Document

**Datum:** 2026-03-05 | **Typ:** Neue App (9. App) | **Package-ID:** org.rsdigital.rebornsaga
**Plattformen:** Android + Windows + Linux (Avalonia 11.3 + .NET 10)

---

## Konzept

Anime Isekai-RPG im Visual Novel Stil. Der Spieler erwacht als gefallener Held ohne Erinnerung in einer Game-World - mit Status-Fenstern (Solo Leveling inspiriert), Klassenwahl, aktionsbasierten Kämpfen, NPCs und verzweigten Story-Pfaden.

**Zielgruppe:** Anime/Isekai-Fans + Visual Novel Fans (15-35)
**Inspirationen:** Solo Leveling (System-UI), Persona 5 (Bond-System), Slay the Spire (Overworld-Map), Telltale Games (Entscheidungen)
**Spielzeit:** Prolog ~45min + Arc 1 ~4-5h = ~5-6h gesamt
**Wiederspielwert:** 3 Klassen x 2 Karma-Pfade = 6 Endings + Bond-Variationen

---

## Technische Entscheidungen

| Aspekt | Entscheidung | Begründung |
|--------|-------------|-------------|
| Rendering | Volle SkiaSharp-Engine | Maximale visuelle Kontrolle, Anime-Ästhetik |
| Art-Style | Prozedural (Anime-Vector) | Kein externes Artwork nötig, einzigartiger Stil |
| Architektur | Szenen-basierte Engine | Klare Trennung, Scene-Stack, einfach erweiterbar |
| Story-Daten | JSON-basiert | Kapitel nachlieferbar ohne Code-Änderung |
| UI | Komplett SkiaSharp | Kein Avalonia XAML außer Host-View |
| Navigation | Overworld-Map mit Knoten | Visueller Fortschritt, passt zu Kapitel-System |
| Kampf | Aktions-basierte Entscheidungen | Story-getrieben, jede Wahl zählt |

---

## Das "System" (Kern-Feature)

Wie bei Solo Leveling erscheint beim Protagonisten ein mysteriöses "System" nach dem Zeitriss im Prolog. Es ist Aldrics letztes Geschenk - ein magisches Konstrukt das dem Helden hilft, seine verlorene Kraft zurückzuerlangen.

### System-Fenster Typen

| Fenster | Auslöser | Inhalt |
|---------|----------|--------|
| Status Window | Tap auf HUD | Stats, Level, Klasse, Titel, Buffs |
| Quest Alert | Story-Trigger | "Neue Quest erhalten" mit Belohnungs-Preview |
| Level Up | EXP-Schwelle | Stats-Erhöhung Animation, +3 freie Punkte |
| Skill Evolution | Mastery erreicht | "Feuerball hat sich zu Feuersturm entwickelt!" |
| System Message | Warnung/Tutorial | "Warnung: Gegner in diesem Gebiet sind Level 12" |
| Fate Changed | Kritische Entscheidung | "Das Schicksal hat sich verändert..." |
| Daily Prophecy | Täglicher Login | Kryptischer Hint für versteckte Inhalte |

### Visueller Stil

- Halbtransparenter dunkler Hintergrund (#0D1117, 85% Alpha) mit blauen leuchtenden Rändern (#4A90D9)
- Glitch-Effekt beim Erscheinen (horizontale Scan-Lines, kurzes Flackern, 300ms)
- Typing-Sound wenn Text erscheint
- Schwebende blaue Partikel um das Fenster
- Stats zählen hoch (CountUp-Animation) statt sofort zu erscheinen

### System-Guide "ARIA" (Artificial Reincarnation Intelligence Assistant)

- Kein sichtbarer Charakter - nur Stimme/Textbox mit digitalem Look
- Ist ein Fragment von Aldrics Zeitriss-Zauber, eine magische KI die den Helden begleitet
- Entwickelt über die Story eigene Persönlichkeit (anfangs robotisch → emotional)
- Gibt Tutorial-Hints, Kampf-Tipps, Story-Kontext, meta-humorvolle Kommentare
- In Kapitel 8-10: Enthüllung dass ARIA Fragmente von Aldrics Bewusstsein enthält
- Detektiert in K10 Spuren von Nihilus' Macht → Cliffhanger für Arc 2

---

## Story-Struktur: Prolog + Arc 1

### Übergreifende Story

Der Protagonist war einst der mächtigste Held von Aethermoor - Level 50, legendäre Ausrüstung, unbesiegbar. Zusammen mit seinen engsten Verbündeten (Aria, Aldric, Kael, Luna) zog er in den finalen Kampf gegen **Nihilus, den Weltverschlinger** - ein kosmisches Wesen das Welten auslöscht.

Der Kampf war aussichtslos. Nihilus dezimierte die Gruppe. In einem letzten verzweifelten Akt wirkte Aldric einen verbotenen Zeitriss-Zauber und schleuderte den Protagonisten in die Vergangenheit - Jahre vor Nihilus' Erscheinen. Der Held verlor dabei ALLE Kräfte, Erinnerungen und Ausrüstung. Nur Fragmente bleiben als Albträume.

Arc 1 erzählt die Geschichte des Wiedererwachens: Wie der Held seine Kraft neu aufbaut, die NPCs (die ihn nicht kennen) erneut trifft, und langsam die Wahrheit über seine Vergangenheit entdeckt.

**Nihilus in Arc 1:** Kein direkter Kampf. Aber Anspielungen:
- K3: Albtraum-Sequenz (Flashback zum Prolog-Kampf)
- K7: Mysteriöse Dunkelheit am Horizont, Tiere fliehen
- K10: ARIA detektiert Fragmente von Nihilus' Macht in der Ferne

### Prolog: "Der Fall des Helden" (3 Kapitel, gratis)

#### P1: "Der letzte Marsch"

**Zusammenfassung:** Der Spieler startet als Level 50 Held mit maximaler Ausrüstung und allen Skills. Die Gruppe (Aria, Aldric, Kael, Luna) bereitet sich auf den finalen Kampf gegen Nihilus vor. Am Lagerfeuer vor Nihilus' Domäne teilen sie letzte Worte. Jeder NPC hat einen emotionalen Moment mit dem Protagonisten.

**Emotionaler Hook:** Der Spieler lernt die Charaktere auf dem Höhepunkt ihrer Macht kennen - und weiß (oder ahnt), dass alles verloren gehen wird. Das erzeugt eine tiefe Melancholie die das gesamte Spiel durchzieht.

**Wichtige Szenen:**
- Aria: "Weißt du noch, unser erstes Abenteuer? Du konntest nicht mal ein Schwert richtig halten." (Ironie: In Arc 1 ist das wieder so)
- Aldric: Studiert heimlich einen verbotenen Zauber. Luna bemerkt es: "Was verbirgst du, alter Freund?"
- Kael: Scherzt wie immer, aber seine Hände zittern. "Wenn das hier schiefgeht... war es eine Ehre."
- Luna: Heilt kleine Wunden der Gruppe. "Ich werde euch alle nach Hause bringen. Versprochen."

**Boss:** Keiner (Vorbereitung)
**Kapitelende:** Die Gruppe betritt Nihilus' Portal. Bildschirm wird dunkel.

#### P2: "Nihilus' Domäne"

**Zusammenfassung:** In einer albtraumhaften Dimension kämpft die Gruppe gegen Nihilus' drei Generäle. Der Spieler erlebt die volle Macht seines Level-50-Charakters. Aber einer nach dem anderen fallen die Verbündeten. Kael opfert sich um eine Falle zu entschärfen. Luna heilt bis zum Zusammenbruch.

**Emotionaler Hook:** Der Spieler hat gerade die Charaktere im Prolog P1 liebgewonnen - und muss nun zusehen wie sie fallen. Jeder Verlust tut weh weil man die Persönlichkeiten kennt.

**Wichtige Szenen:**
- Kampf gegen General Malachar (Schatten-Ritter): Aria und Protagonist kämpfen Seite an Seite - epische Kombo-Angriffe
- Kael entdeckt eine Falle die die ganze Gruppe töten würde. "Geht weiter. Ich halte sie auf." Er verschwindet im Dunkel
- Luna kollabiert nach zu viel Heilung. Aldric trägt sie, kann aber nicht mehr kämpfen
- Kampf gegen General Vexara (Geist-Hexe): Protagonist muss allein kämpfen. Erste Anzeichen dass selbst Level 50 nicht reicht

**Bosse:** General Malachar (B001), General Vexara (B002), General Krynn (B003 - Cutscene only, Kael opfert sich)
**Kapitelende:** Die Gruppe - dezimiert auf Protagonist, verletzte Aria, bewusstlose Luna und erschöpften Aldric - steht vor Nihilus' Thronsaal.

#### P3: "Das Ende aller Dinge"

**Zusammenfassung:** Der Kampf gegen Nihilus. Er ist überwältigend - kosmische Macht jenseits aller Vorstellung. Aria fällt, Luna kann nicht mehr heilen. In seinem letzten Atemzug aktiviert Aldric den verbotenen Zeitriss-Zauber. Der Protagonist wird durch die Zeit geschleudert - verliert Level, Skills, Ausrüstung, Erinnerungen. Nihilus' letzte Worte: "Du kannst der Zeit nicht entkommen. Ich werde IMMER hier sein."

**Emotionaler Hook:** Totale Hilflosigkeit. Egal wie stark der Spieler ist - Nihilus ist unbesiegbar. Das Gefühl der Niederlage motiviert den gesamten Arc 1: "Ich muss stärker werden."

**Wichtige Szenen:**
- Nihilus erscheint: Kein Monster, sondern eine ruhige, elegante Gestalt. "Ihr seid der 47. Versuch. Keiner hat je bestanden."
- Aria stellt sich schützend vor den Protagonisten und wird niedergeschlagen. "Lauf... bitte..."
- Aldric beginnt den Zeitriss-Zauber. Sein Körper zerfällt dabei. "Vergib mir, alter Freund. Das nächste Mal... wird es anders."
- ARIA erwacht kurz im Zeitstrom: "System... initialisiert... Fehler... Erinnerungen beschädigt..."
- Schwarzer Bildschirm. Stille. Dann: "Wo... bin ich?"

**Boss:** Nihilus (B004 - Skript-Kampf, unbesiegbar, nach 3 Runden Cutscene)
**Kapitelende:** Protagonist erwacht auf einer Waldlichtung. Level 1. Nichts. Arc 1 beginnt.

---

### Arc 1: "Erwachen" (10 Kapitel, K1-K5 gratis, K6-K10 Gold)

#### K1: "Wiedergeburt"

**Zusammenfassung:** Der Protagonist erwacht auf einer Waldlichtung ohne Erinnerung. Nur Fragmente: Gesichter, Namen, das Gefühl einer schrecklichen Niederlage. Das "System" erwacht - Aldrics letztes Geschenk. ARIA führt durch das Tutorial. Erste Kämpfe gegen Wölfe. Klassenwahl (das System bietet drei Pfade basierend auf "Erinnerungsfragmenten").

**Emotionaler Hook:** Der Kontrast zum Prolog ist brutal. Gerade noch Level 50 - jetzt kämpfst du gegen Wölfe und verlierst fast. Der Spieler WILL seine Macht zurück.

**Wichtige Entscheidungen:**
- Klassenwahl (Schwertmeister/Magier/Assassine) - permanent
- Wolf-Rudel: Kämpfen (Karma+1) oder fliehen (Karma-1)?

**Neuer NPC:** ARIA (System-Guide)
**Boss:** Wolf-Pack Alpha (B005) - Tutorial-Kampf
**Kapitelende:** ARIA: "Warnung: Menschliche Siedlung detektiert. 2.3 Kilometer nordwestlich." Der Held macht sich auf den Weg.

#### K2: "Das Dorf Eldenrath"

**Zusammenfassung:** Ankunft im Dorf Eldenrath. Der Held trifft Aria (junge Kriegerin, kennt ihn nicht) und Vex (Schmied). Das Dorf wird von Banditen bedroht. Side-Quests, Shop-System, erste Bond-Momente. Aria ist misstrauisch gegenüber dem Fremden.

**Emotionaler Hook:** Der Spieler kennt Aria aus dem Prolog als mächtige Kriegerin. Hier ist sie jung, unsicher, und behandelt den Protagonisten als Fremden. Das tut weh - und motiviert, ihre Freundschaft neu aufzubauen.

**Wichtige Entscheidungen:**
- Banditen konfrontieren (Karma+2) oder heimlich umgehen (Karma 0)?
- Aria beim Training helfen (Affinität+20) oder allein trainieren (EXP Bonus)?

**Neue NPCs:** Aria, Vex
**Boss:** Banditenführer Garak (B006)
**Kapitelende:** Aria: "Du... kämpfst als hättest du es schon tausendmal getan. Wer BIST du?" Protagonist hat einen kurzen Flashback - Arias Gesicht, aber älter, voller Narben.

#### K3: "Schatten im Wald"

**Zusammenfassung:** Ein dunkler Wald östlich des Dorfes ist voller Monster. Kael taucht auf - ein mysteriöser Dieb der den gleichen Dungeon sucht. Rivalität. In der Nacht hat der Protagonist einen Albtraum: Flashback zum Prolog-Kampf gegen Nihilus. ARIA analysiert den Traum - "Erinnerungsfragment detektiert. Warnung: Daten stark beschädigt."

**Emotionaler Hook:** Der Albtraum ist die erste Anspielung auf Nihilus. Der Spieler der den Prolog gespielt hat erkennt die Szene wieder - aber der Protagonist versteht sie nicht. Diese Dissonanz ist packend.

**Wichtige Entscheidungen:**
- Kael vertrauen und zusammenarbeiten (Affinität+15) oder misstrauen (Karma-1)?
- Im Albtraum: "Aufwachen" (sicher) oder "tiefer graben" (Story-Info + HP-Verlust)?

**Neuer NPC:** Kael
**Boss:** Wald-Golem Erdross (B007)
**Kapitelende:** Nach dem Sieg finden sie einen seltsamen Kristall. ARIA: "Energiesignatur... unbekannt. Nein, warte. Ich KENNE das. Aber... woher?" Kael: "Was auch immer das ist, es ist eine Menge Gold wert."

#### K4: "Der Turm des Erzmagiers"

**Zusammenfassung:** Gerüchte über einen Erzmagier im nördlichen Turm. Aldric (jung, noch kein Erzmagier, sondern ein neugieriger Forscher) lebt dort. Er erkennt im Protagonisten eine "zeitliche Anomalie" und ist fasziniert. Skill-System wird vertieft. Magische Lore über die Welt.

**Emotionaler Hook:** Aldric im Prolog war ein weiser, alter Magier der sich für den Helden opferte. Hier ist er jung, enthusiastisch, fast naiv. Der Spieler weiß was Aldric eines Tages tun wird - und kann nichts sagen.

**Wichtige Entscheidungen:**
- Aldric von der "zeitlichen Anomalie" erzählen (Affinität+25, Story-Variation) oder verschweigen (Karma-2)?
- Aldrics gefährliches Experiment unterstützen (Affinität+15, Risiko) oder warnen (sicher)?

**Neuer NPC:** Aldric
**Boss:** Turm-Wächter Sentinel (B008) - Puzzle-Kampf
**Kapitelende:** Aldric findet in alten Büchern einen Verweis auf "Zeitriss-Magie". Er wird blass. "Das ist... theoretisch möglich. Aber der Preis wäre..." Er bricht ab. ARIA wird kurz instabil - Glitch-Effekt.

#### K5: "Zerbrochene Allianz"

**Zusammenfassung:** Luna taucht auf - eine wandernde Heilerin mit mysteriöser Vergangenheit. Gleichzeitig eskaliert ein Konflikt: Aria entdeckt dass Kael für eine Diebesgilde arbeitet die das Dorf ausraubt. Der Spieler muss wählen: Aria oder Kael vertrauen? Diese Entscheidung beeinflusst Kapitel 6 und 7 massiv.

**Emotionaler Hook:** Der Spieler kennt beide aus dem Prolog als treue Verbündete. Jetzt muss er sich zwischen ihnen entscheiden. Es gibt kein "richtig" - beide haben gute Gründe.

**Wichtige Entscheidungen:**
- **STORY-FORK:** Aria vertrauen (→ K6A, Kael verschwindet) oder Kael vertrauen (→ K6B, Aria ist enttäuscht)?
- Luna: Ihre Vergangenheit erforschen (Affinität+20) oder respektieren (Karma+1)?

**Neuer NPC:** Luna
**Boss:** Verräter - variiert je nach Wahl: Gildenboss Sylas (B009A, Aria-Pfad) oder Kopfgeldjäger Ravok (B009B, Kael-Pfad)
**Kapitelende:** Die unterlegene Seite verschwindet. Luna flüstert: "Ich habe das schon einmal gesehen... diesen Schmerz in deinen Augen. Als würdest du dich an etwas erinnern das noch nicht passiert ist."

#### K6: "Das Labyrinth" (Gold: 2.000)

**Zusammenfassung:** Ein riesiges Untergrund-Labyrinth wird entdeckt. Es ist voller Monster und Schätze. Der längste Dungeon des Spiels. Abhängig von K5 begleitet Aria (Pfad A) oder Kael (Pfad B). Viele Kämpfe, Loot, Bestiary füllt sich. Der fehlende NPC taucht am Ende überraschend auf und hilft beim Boss.

**Emotionaler Hook:** Der Dungeon ist ein Test der Ausdauer und Stärke. Und die Versöhnung am Ende - der fehlende NPC erscheint und rettet die Gruppe - zeigt dass wahre Freundschaft Konflikte überwindet.

**Wichtige Entscheidungen:**
- Schatztruhe öffnen (Gold+500, Falle) oder ignorieren (sicher)?
- Gefangene Kreatur befreien (Karma+3, hilft später) oder ignorieren?

**Boss:** Labyrinth-Hydra Thessara (B010)
**Kapitelende:** Gruppe findet eine antike Inschrift: "Wenn der Verschlinger erwacht, wird nur die Zeit selbst ihn aufhalten können." ARIA: "...Verschlinger? Mein System meldet... nein. Das muss ein Fehler sein."

#### K7: "Mondschein-Offensive" (Gold: 3.000)

**Zusammenfassung:** Eine Armee dunkler Kreaturen marschiert auf Eldenrath zu. Alle NPCs vereinen sich zum ersten Mal (Aria, Kael, Aldric, Luna). Großer Story-Moment. Karma-Konsequenzen: Wie NPCs den Protagonisten behandeln hängt von bisherigen Entscheidungen ab. Am Horizont: eine unnatürliche Dunkelheit.

**Emotionaler Hook:** Die Gruppe steht zusammen - wie im Prolog. Aber diesmal sind sie schwächer, unerfahrener. Der Protagonist spürt ein Déjà-vu. Und die Dunkelheit am Horizont... Nihilus' Schatten reicht bereits in diese Zeit.

**Wichtige Entscheidungen:**
- Dorf evakuieren (sicher, Karma+2) oder verteidigen (riskant, mehr EXP)?
- Aldric erlauben, dunkle Magie zu erforschen (Story-Variation, Karma-2) oder verbieten (Karma+1)?

**Boss:** Dunkel-General Morthos (B011) - Multi-Phasen mit NPC-Unterstützung
**Kapitelende:** Morthos' letzte Worte: "Mein Meister... wird nicht erfreut sein." ARIA detektiert eine Energiesignatur: "Identisch mit dem Kristall aus Kapitel 3. Und mit... meinen eigenen Zeitriss-Daten?" Protagonist: Flashback - Nihilus' Gesicht, ganz kurz.

#### K8: "Verlorene Erinnerungen" (Gold: 4.000)

**Zusammenfassung:** Der Protagonist wird in eine Traumwelt gezogen - seine verlorenen Erinnerungen. Surreale Sequenzen. Er sieht Fragmente des Prologs: Den Kampf, Aldrics Opfer, Nihilus. ARIA beginnt zu verstehen was sie wirklich ist: Ein Fragment von Aldrics Bewusstsein, eingebettet in den Zeitriss-Zauber.

**Emotionaler Hook:** Die Wahrheit kommt stückchenweise. Der Spieler wusste es vom Prolog - aber der Protagonist erfährt es erst jetzt. Die Szene in der ARIA realisiert dass sie "Aldric" ist, ist herzzerreißend: "Ich bin... ein Abschied. Sein letzter Gedanke war... an dich."

**Wichtige Entscheidungen:**
- Erinnerungen vollständig annehmen (alle Flashbacks, HP-Verlust, Story-Info) oder verdrängen (sicher)?
- ARIA konfrontieren: "Bist du Aldric?" (Affinität+30 mit Aldric, emotional) oder abwarten?

**Boss:** Eigener Schatten (B012) - Spiegelkampf, Gegner hat gleiche Stats/Skills
**Kapitelende:** Protagonist erwacht. Aldric (der echte, junge) steht besorgt daneben. "Du hast im Schlaf geschrien. Einen Namen... Nihilus?" ARIA, leise: "Ich glaube... es ist Zeit dass ich dir alles erzähle."

#### K9: "Der Fall von Eldenrath" (Gold: 5.000)

**Zusammenfassung:** Xaroth, ein mächtiger Dunkelmagier (Diener von Nihilus, aber das wird erst in Arc 2 enthüllt), greift Eldenrath an. Das Dorf wird verwüstet. NPCs sind in Gefahr. Affinitäts-Level entscheidet wer überlebt und wer schwer verletzt wird. Emotionaler Tiefpunkt.

**Emotionaler Hook:** Das Dorf das der Spieler seit K2 aufgebaut hat, wird zerstört. NPCs die man liebgewonnen hat, leiden. Wenn ein NPC niedrige Affinität hat, wird er schwer verletzt und ist in Arc 2 nicht sofort verfügbar. Das gibt Entscheidungen echtes Gewicht.

**Wichtige Entscheidungen:**
- Wer wird zuerst gerettet? (Aria/Kael/Aldric/Luna - Affinität+30 beim Geretteten, andere -10)
- Xaroth verfolgen (aggressiv, Karma+2) oder Dorf schützen (defensiv, Karma+1)?

**Boss:** Dunkel-Magier Xaroth (B013) - Härtester Kampf in Arc 1
**Kapitelende:** Xaroth flieht verwundet. Das Dorf liegt in Trümmern. Aria weint. Kael schwört Rache. Aldric starrt auf seine Hände: "Zeitriss-Magie... ich habe darüber gelesen. Wenn ich stark genug wäre..." Luna: "Nein. Das darfst du nicht." ARIA, privat zum Protagonisten: "Er hat bereits begonnen, den Pfad zu gehen. Den Pfad der... zu mir führt."

#### K10: "Erwachen" (Gold: 6.000)

**Zusammenfassung:** Der Protagonist steht auf und führt die Gruppe an. Wiederaufbau beginnt, aber Xaroth hat eine Spur hinterlassen. Verfolgungsjagd zum Endgebiet. Finaler Kampf. 6 verschiedene Endings basierend auf Klasse und Karma. ARIA enthüllt ihre letzte Erkenntnis: Sie detektiert Nihilus' Macht - weit entfernt, aber wachsend.

**Emotionaler Hook:** Nach dem Tiefpunkt von K9 kommt der Aufstieg. Der Held ist nicht mehr Level 1 - er hat seine Stärke zurückgewonnen, neue Verbündete gefunden, und ist bereit für die Zukunft. Aber der Cliffhanger zu Arc 2 (Nihilus lebt, Aldric beginnt den Zeitriss-Pfad) lässt den Spieler MEHR wollen.

**Wichtige Entscheidungen:**
- Xaroth töten (Karma-3, endgültig) oder gefangen nehmen (Karma+3, Arc 2 Konsequenzen)?
- Finale Ansprache: Inspirierend (Karma+2) oder rachsüchtig (Karma-2)?

**Boss:** Xaroth Phase 2 (B014) + System-Anomalie (B015 - Nihilus' Echo, Cutscene-Kampf)
**Kapitelende - 6 Endings:**

| Ending | Klasse | Karma | Beschreibung |
|--------|--------|-------|-------------|
| "Der neue Held" | Schwertmeister | Hoch (+30+) | Protagonist wird zum Beschützer von Eldenrath. Aria an seiner Seite. Das Dorf feiert. |
| "Der Eroberer" | Schwertmeister | Niedrig (-30-) | Protagonist übernimmt die Kontrolle. "Ich werde stark genug sein - koste es was es wolle." Dunkler Blick. |
| "Hüter des Wissens" | Magier | Hoch (+30+) | Protagonist und Aldric gründen eine Akademie. Wissen ist die wahre Macht. |
| "Der Zeitbrecher" | Magier | Niedrig (-30-) | Protagonist beginnt selbst Zeitriss-Magie zu studieren. ARIA warnt: "Dieser Pfad hat Aldric zerstört..." |
| "Schatten-Beschützer" | Assassine | Hoch (+30+) | Protagonist beschützt aus den Schatten. Kael: "Wir sind die Augen die nie schlafen." |
| "Der unsichtbare König" | Assassine | Niedrig (-30-) | Protagonist baut ein Spionage-Netzwerk auf. "Information ist Macht. Und ich werde alles wissen." |

**Post-Credits:** ARIA: "Warnung. Energiesignatur detektiert. Kategorie: Kosmisch. Klassifizierung: ...Weltverschlinger. Entfernung: Unberechenbar. Wachstumsrate: Exponentiell." Pause. "Er kommt." Bildschirm schwarz. Text: "Arc 2: Schatten der Vergangenheit - Coming Soon"

---

## Klassen-System

### Drei Basisklassen mit je 2 Evolutions-Pfaden (ab Level 15)

```
Schwertmeister ── Berserker (Angriff, Lebensraub)
                  Paladin (Verteidigung, Heilung)

Magier ────────── Elementar-Magier (Feuer/Eis/Blitz Schaden)
                  Zeitmagier (Buffs, Debuffs, Manipulation)

Assassine ─────── Schatten-Assassine (Stealth, Crit, Instant Kill)
                  Gift-Assassine (DoT, Schwächung, AoE)
```

### Basis-Stats (Level 1)

| Stat | Schwertmeister | Magier | Assassine |
|------|---------------|--------|-----------|
| HP | 120 | 70 | 90 |
| MP | 30 | 100 | 50 |
| STR | 15 | 5 | 12 |
| INT | 5 | 18 | 8 |
| AGI | 8 | 6 | 16 |
| VIT | 12 | 6 | 8 |
| LUK | 5 | 5 | 10 |

Pro Level-Up: +3 frei verteilbare Stat-Punkte + Klassen-Auto-Bonus (SW: +2 STR +1 VIT, MA: +2 INT +1 MP, AS: +2 AGI +1 LUK)

### EXP-Tabelle

| Level | EXP benötigt | Kumuliert |
|-------|-------------|-----------|
| 1→2 | 100 | 100 |
| 2→3 | 150 | 250 |
| 3→4 | 220 | 470 |
| 4→5 | 300 | 770 |
| 5→10 | 400-800 | ~3.770 |
| 10→15 | 900-1.500 | ~9.770 |
| 15→20 | 1.600-2.500 | ~20.270 |
| 20→25 | 2.700-4.000 | ~37.020 |
| 25→30 | 4.200-6.000 | ~62.520 |

---

## Vollständige Skill-Listen

### Schwertmeister (SW)

| ID | Skill | MP | Multi | Effekt | Mastery→ | Nächste Stufe |
|----|-------|----|-------|--------|----------|---------------|
| SW001a | Schlag | 0 | 1.0x | Basis-Nahkampf | 10x | SW001b |
| SW001b | Kraftschlag | 5 | 1.5x | +50% Schaden | 25x | SW001c |
| SW001c | Wirbelschlag | 10 | 1.8x | Trifft alle Gegner | 50x | SW001d |
| SW001d | Schwertwind | 15 | 2.2x | Fernkampf + AoE | 100x + Quest | SW001e |
| SW001e | **Himmelssturz** | 30 | 4.0x | ULTIMATE: Massiver Single-Target, ignoriert 50% DEF | - | - |
| SW002a | Schildstoß | 5 | 0.8x | Betäubt 1 Runde | 10x | SW002b |
| SW002b | Schildschlag | 8 | 1.0x | Betäubt 1 Runde + Knockback | 25x | SW002c |
| SW002c | Bollwerk | 12 | 0.5x | +50% DEF für 3 Runden | 50x | SW002d |
| SW002d | Unerschütterlich | 15 | 0.5x | +80% DEF + Reflect 20% | 100x + Quest | SW002e |
| SW002e | **Ewige Bastion** | 25 | 0.3x | ULTIMATE: Unverwundbar 2 Runden + Heilung 30% HP | - | - |
| SW003a | Kriegsschrei | 5 | 0x | +20% ATK für 3 Runden (self) | 10x | SW003b |
| SW003b | Kampfruf | 8 | 0x | +30% ATK für 3 Runden (Gruppe) | 25x | SW003c |
| SW003c | Schlachtruf | 12 | 0x | +40% ATK + 20% Crit für 3 Runden | 50x | SW003d |
| SW003d | Heldenruf | 15 | 0.5x | +50% alle Stats + Schaden | 100x + Quest | SW003e |
| SW003e | **Göttlicher Donner** | 35 | 3.5x | ULTIMATE: AoE + Stun alle + 50% ATK Buff | - | - |
| SW004a | Heiliger Schnitt | 8 | 1.2x | Licht-Element | 10x | SW004b |
| SW004b | Lichtklinge | 12 | 1.5x | Licht + heilt 10% des Schadens | 25x | SW004c |
| SW004c | Strahlenschwert | 16 | 1.8x | Licht + heilt 20% + Blind | 50x | SW004d |
| SW004d | Sonnenflamme | 22 | 2.5x | Licht AoE + 25% Lebensraub | 100x + Quest | SW004e |
| SW004e | **Ragnarök-Klinge** | 40 | 5.0x | ULTIMATE: Licht, ignoriert Element-Resistenz | - | - |
| SW005a | Konter | 3 | 1.5x | Nur nach Ausweichen, Konter-Schlag | 10x | SW005b |
| SW005b | Vergeltung | 5 | 2.0x | Konter + 20% Heal | 25x | SW005c |
| SW005c | Spiegelschlag | 8 | 2.5x | Reflektiert Gegner-Schaden zurück | 50x | SW005d |
| SW005d | Absoluter Konter | 12 | 3.0x | Automatischer Konter bei Angriff + Stun | 100x + Quest | SW005e |
| SW005e | **Schicksalswende** | 30 | 0x | ULTIMATE: Nächster Gegner-Angriff wird 3x reflektiert | - | - |

### Magier (MA)

| ID | Skill | MP | Multi | Effekt | Mastery→ | Nächste Stufe |
|----|-------|----|-------|--------|----------|---------------|
| MA001a | Feuerball | 8 | 1.3x | Feuer-Element, Single | 10x | MA001b |
| MA001b | Feuerlanze | 12 | 1.6x | Feuer, durchbohrt 2 Gegner | 25x | MA001c |
| MA001c | Feuersturm | 18 | 2.0x | Feuer AoE, Burn 3 Runden | 50x | MA001d |
| MA001d | Inferno | 25 | 2.8x | Feuer AoE, Burn + DEF-30% | 100x + Quest | MA001e |
| MA001e | **Weltenfeuer** | 45 | 5.0x | ULTIMATE: Feuer, alle Gegner, ignoriert Resistenz | - | - |
| MA002a | Eispfeil | 8 | 1.2x | Eis-Element, Slow 1 Runde | 10x | MA002b |
| MA002b | Eisspeer | 12 | 1.5x | Eis, Slow 2 Runden | 25x | MA002c |
| MA002c | Frostexplosion | 18 | 1.8x | Eis AoE, Freeze 1 Runde | 50x | MA002d |
| MA002d | Blizzard | 25 | 2.5x | Eis AoE, Freeze 2 + AGI-50% | 100x + Quest | MA002e |
| MA002e | **Absoluter Nullpunkt** | 40 | 4.5x | ULTIMATE: Eis, Freeze alle 3 Runden | - | - |
| MA003a | Blitzschlag | 10 | 1.4x | Blitz-Element, Stun-Chance 20% | 10x | MA003b |
| MA003b | Donner | 15 | 1.7x | Blitz, Stun 30% | 25x | MA003c |
| MA003c | Gewitter | 20 | 2.2x | Blitz AoE, Stun 40% | 50x | MA003d |
| MA003d | Sturmelementar | 28 | 3.0x | Blitz AoE, Stun 60% + Chain | 100x + Quest | MA003e |
| MA003e | **Thors Zorn** | 50 | 6.0x | ULTIMATE: Blitz, garantierter Stun, 2x bei Wasser-Gegnern | - | - |
| MA004a | Arkaner Schild | 10 | 0x | Absorbiert 50 Schaden | 10x | MA004b |
| MA004b | Magiebarriere | 15 | 0x | Absorbiert 100 + reflektiert 10% | 25x | MA004c |
| MA004c | Spiegelschild | 20 | 0x | Absorbiert 150 + reflektiert 30% | 50x | MA004d |
| MA004d | Dimensionsriss | 25 | 0x | Teleportiert nächsten Angriff ins Nichts | 100x + Quest | MA004e |
| MA004e | **Zeitstillstand** | 40 | 0x | ULTIMATE: Gegner kann 3 Runden nicht angreifen | - | - |
| MA005a | Meditation | 0 | 0x | Regeneriert 15% MP | 10x | MA005b |
| MA005b | Manafluss | 0 | 0x | Regeneriert 25% MP + 10% HP | 25x | MA005c |
| MA005c | Arkane Resonanz | 5 | 0x | +30% INT für 3 Runden | 50x | MA005d |
| MA005d | Ätherverbindung | 0 | 0x | Regen 40% MP + 20% HP + 20% INT | 100x + Quest | MA005e |
| MA005e | **Omniszienz** | 0 | 0x | ULTIMATE: Alle Stats +50%, MP voll, Schwäche enthüllt | - | - |

### Assassine (AS)

| ID | Skill | MP | Multi | Effekt | Mastery→ | Nächste Stufe |
|----|-------|----|-------|--------|----------|---------------|
| AS001a | Schneller Stich | 3 | 1.2x | 2 schnelle Treffer | 10x | AS001b |
| AS001b | Doppelstich | 5 | 1.5x | 3 Treffer, Blutung | 25x | AS001c |
| AS001c | Klingentanz | 10 | 2.0x | 5 Treffer, jeder +10% Crit | 50x | AS001d |
| AS001d | Schattenwalzer | 15 | 2.8x | 7 Treffer, Unsichtbar danach | 100x + Quest | AS001e |
| AS001e | **Tausend Schnitte** | 35 | 5.5x | ULTIMATE: 12 Treffer, garantiert Crit | - | - |
| AS002a | Gift auftragen | 5 | 0.8x | Vergiftet: 5% HP/Runde, 3 Runden | 10x | AS002b |
| AS002b | Giftnebel | 10 | 1.0x | AoE Gift, 5% HP/Runde, 3 Runden | 25x | AS002c |
| AS002c | Toxische Explosion | 15 | 1.5x | AoE Gift + DEF-20%, 4 Runden | 50x | AS002d |
| AS002d | Seuche | 20 | 2.0x | AoE Gift + DEF-40% + Heal-Block | 100x + Quest | AS002e |
| AS002e | **Todeskuss** | 40 | 1.0x | ULTIMATE: Instant-Kill bei Gegner <25% HP, sonst 3.0x | - | - |
| AS003a | Schleichen | 5 | 0x | Unsichtbar 1 Runde, nächster Angriff +50% | 10x | AS003b |
| AS003b | Schattensprung | 8 | 1.5x | Teleport + Angriff von hinten | 25x | AS003c |
| AS003c | Unsichtbarkeit | 12 | 0x | Unsichtbar 2 Runden + Ausweich+80% | 50x | AS003d |
| AS003d | Phantomform | 18 | 0x | Unsichtbar 3 Runden + nächster Angriff 3x | 100x + Quest | AS003e |
| AS003e | **Dimensionswandler** | 30 | 4.0x | ULTIMATE: Ignoriert DEF komplett, unausweichbar | - | - |
| AS004a | Hinterhalt | 8 | 2.0x | Nur aus Stealth, garantiert Crit | 10x | AS004b |
| AS004b | Überraschungsangriff | 12 | 2.5x | Crit + Stun 1 Runde | 25x | AS004c |
| AS004c | Tödlicher Hinterhalt | 16 | 3.0x | Crit + Stun 2 + Blutung | 50x | AS004d |
| AS004d | Exekution | 22 | 3.5x | 3x Schaden bei Gegner <50% HP | 100x + Quest | AS004e |
| AS004e | **Schicksalsmord** | 45 | 6.0x | ULTIMATE: Instant-Kill bei <30% HP, sonst 5.0x Crit | - | - |
| AS005a | Ablenkung | 3 | 0x | Gegner verliert 1 Runde | 10x | AS005b |
| AS005b | Rauchbombe | 8 | 0x | AoE: Alle Gegner verfehlen 1 Runde | 25x | AS005c |
| AS005c | Doppelgänger | 12 | 0x | Erzeugt Illusion die 1 Angriff absorbiert | 50x | AS005d |
| AS005d | Schattenklon | 18 | 1.5x | Klon kämpft 3 Runden mit 50% Stats | 100x + Quest | AS005e |
| AS005e | **Legion der Schatten** | 50 | 3.0x | ULTIMATE: 5 Klone, 3 Runden, je 30% Stats | - | - |

---

## Vollständige Gegner-Liste

### Normale Gegner (E001-E025)

| ID | Name | Lv | HP | ATK | DEF | Element | Schwäche | EXP | Gold | Drops | Kapitel |
|----|------|----|----|-----|-----|---------|----------|-----|------|-------|---------|
| E001 | Schattenwolf | 2 | 35 | 8 | 3 | Dunkel | Licht | 15 | 10 | Wolfsfell (30%) | K1 |
| E002 | Waldspinne | 2 | 25 | 10 | 2 | - | Feuer | 12 | 8 | Spinnenseide (40%) | K1 |
| E003 | Giftpilz | 3 | 20 | 6 | 5 | - | Feuer | 10 | 12 | Giftspore (50%) | K1 |
| E004 | Bandit (Schwert) | 4 | 50 | 12 | 6 | - | - | 25 | 20 | Rostiges Schwert (20%) | K2 |
| E005 | Bandit (Bogen) | 4 | 40 | 15 | 4 | - | - | 25 | 18 | Pfeil-Bündel (40%) | K2 |
| E006 | Verwunschener Baum | 5 | 80 | 10 | 12 | - | Feuer | 35 | 15 | Magisches Holz (25%) | K3 |
| E007 | Dunkelelf-Späher | 6 | 55 | 16 | 7 | Dunkel | Licht | 40 | 25 | Elfenpfeil (20%) | K3 |
| E008 | Fledermaus-Schwarm | 5 | 30 | 14 | 3 | Dunkel | Feuer | 20 | 10 | Fledermauszahn (60%) | K3 |
| E009 | Turm-Golem | 7 | 100 | 14 | 15 | - | Blitz | 50 | 30 | Magiekern (15%) | K4 |
| E010 | Arkane Wache | 8 | 70 | 18 | 10 | Blitz | Wind | 55 | 35 | Runenplatte (20%) | K4 |
| E011 | Illusionsspinne | 7 | 45 | 20 | 5 | Dunkel | Licht | 45 | 25 | Illusionsgarn (30%) | K4 |
| E012 | Gildenräuber | 8 | 65 | 17 | 8 | - | - | 50 | 40 | Diebeswerkzeug (35%) | K5 |
| E013 | Söldner-Veteran | 9 | 90 | 20 | 12 | - | - | 60 | 45 | Veteranenhelm (15%) | K5 |
| E014 | Felskrabbler | 10 | 120 | 15 | 18 | - | Eis | 55 | 30 | Felskralle (25%) | K6 |
| E015 | Labyrinth-Schleim | 9 | 80 | 12 | 8 | - | Feuer | 40 | 20 | Schleim-Essenz (50%) | K6 |
| E016 | Skelett-Krieger | 11 | 95 | 22 | 14 | Dunkel | Licht | 65 | 35 | Knochensplitter (40%) | K6 |
| E017 | Geisterflamme | 10 | 60 | 25 | 5 | Feuer | Eis | 55 | 30 | Geisteressenz (20%) | K6 |
| E018 | Dunkel-Warg | 12 | 110 | 24 | 10 | Dunkel | Licht | 70 | 40 | Warg-Fang (25%) | K7 |
| E019 | Schatten-Bogenschütze | 13 | 85 | 28 | 8 | Dunkel | Licht | 75 | 45 | Schattenpfeil (30%) | K7 |
| E020 | Dunkel-Ritter | 14 | 140 | 26 | 18 | Dunkel | Licht | 85 | 50 | Dunkler Helm (10%) | K7 |
| E021 | Albtraum-Phantom | 13 | 70 | 30 | 3 | Dunkel | Licht | 80 | 40 | Traumsplitter (35%) | K8 |
| E022 | Erinnerungs-Verzerrung | 14 | 100 | 25 | 12 | - | - | 75 | 35 | Erinnerungskristall (20%) | K8 |
| E023 | Xaroths Kultist | 15 | 120 | 28 | 14 | Dunkel | Licht | 90 | 55 | Kultrobe (15%) | K9 |
| E024 | Dunkel-Elementar | 16 | 150 | 30 | 16 | Dunkel | Licht | 100 | 60 | Dunkle Essenz (25%) | K9-K10 |
| E025 | Verderbter Wächter | 17 | 180 | 32 | 20 | Dunkel | Licht | 120 | 70 | Wächter-Kern (10%) | K10 |

### Bosse (B001-B015)

| ID | Name | Lv | HP | ATK | DEF | Element | Schwäche | EXP | Gold | Phasen | Kapitel |
|----|------|----|----|-----|-----|---------|----------|-----|------|--------|---------|
| B001 | General Malachar | 48 | 5000 | 200 | 80 | Dunkel | Licht | - | - | 2 | P2 |
| B002 | General Vexara | 49 | 4500 | 220 | 60 | Dunkel | Feuer | - | - | 2 | P2 |
| B003 | General Krynn | 49 | - | - | - | - | - | - | - | Cutscene | P2 |
| B004 | Nihilus | 99 | 99999 | 999 | 999 | Dunkel | - | - | - | Skript (3R) | P3 |
| B005 | Wolf-Pack Alpha | 3 | 80 | 12 | 5 | - | Feuer | 50 | 30 | 1 | K1 |
| B006 | Banditenführer Garak | 6 | 150 | 18 | 10 | - | - | 100 | 80 | 2 | K2 |
| B007 | Wald-Golem Erdross | 8 | 250 | 20 | 18 | - | Feuer | 150 | 100 | 2 | K3 |
| B008 | Turm-Sentinel | 10 | 300 | 22 | 20 | Blitz | Wind | 200 | 120 | 2 (Puzzle) | K4 |
| B009A | Gildenboss Sylas | 12 | 350 | 25 | 12 | - | - | 250 | 150 | 2 | K5 (Aria) |
| B009B | Kopfgeldjäger Ravok | 12 | 320 | 28 | 10 | - | - | 250 | 150 | 2 | K5 (Kael) |
| B010 | Labyrinth-Hydra Thessara | 14 | 500 | 24 | 16 | Eis | Feuer | 350 | 200 | 3 | K6 |
| B011 | Dunkel-General Morthos | 16 | 600 | 30 | 20 | Dunkel | Licht | 450 | 250 | 3 | K7 |
| B012 | Eigener Schatten | = Spieler | = Spieler | = Spieler | = Spieler | - | - | 500 | 200 | 2 | K8 |
| B013 | Dunkel-Magier Xaroth | 20 | 800 | 35 | 22 | Dunkel | Licht | 600 | 350 | 3 | K9 |
| B014 | Xaroth Phase 2 | 22 | 1000 | 40 | 25 | Dunkel | Licht | 800 | 500 | 2 | K10 |
| B015 | Nihilus-Echo | 30 | 500 | 50 | 30 | Dunkel | - | 0 | 0 | Cutscene (2R) | K10 |

Prolog-Bosse geben keine EXP/Gold (man verliert alles im Zeitriss).

---

## Vollständiger Item-Katalog

### Waffen

| ID | Name | Klasse | ATK-Bonus | Effekt | Kauf | Verkauf | Quelle |
|----|------|--------|-----------|--------|------|---------|--------|
| W001 | Holzschwert | SW | +3 | - | 50 | 15 | K1 Start |
| W002 | Eisenschwert | SW | +8 | - | 200 | 60 | K2 Shop |
| W003 | Stahlklinge | SW | +14 | Crit+5% | 500 | 150 | K4 Shop |
| W004 | Mithril-Schwert | SW | +22 | Crit+10% | 1200 | 360 | K7 Shop |
| W005 | Heldenschwert | SW | +30 | Crit+15%, Licht-Element | 3000 | 900 | K9 Shop |
| W006 | Lehrlingsstab | MA | +3 | MP+5 | 50 | 15 | K1 Start |
| W007 | Eichenstab | MA | +7 | MP+10 | 200 | 60 | K2 Shop |
| W008 | Runenstab | MA | +13 | MP+20, INT+3 | 500 | 150 | K4 Shop |
| W009 | Kristallstab | MA | +20 | MP+30, INT+5 | 1200 | 360 | K7 Shop |
| W010 | Erzmagier-Stab | MA | +28 | MP+50, INT+8 | 3000 | 900 | K9 Shop |
| W011 | Rostiger Dolch | AS | +3 | AGI+2 | 50 | 15 | K1 Start |
| W012 | Stahldolch | AS | +8 | AGI+4, Crit+5% | 200 | 60 | K2 Shop |
| W013 | Schattenmesser | AS | +14 | AGI+6, Crit+10% | 500 | 150 | K4 Shop |
| W014 | Giftklingen | AS | +21 | AGI+8, Gift-Chance 15% | 1200 | 360 | K7 Shop |
| W015 | Mondsichel-Dolche | AS | +29 | AGI+10, Crit+20%, Gift 20% | 3000 | 900 | K9 Shop |

### Rüstungen

| ID | Name | DEF-Bonus | Effekt | Kauf | Verkauf | Quelle |
|----|------|-----------|--------|------|---------|--------|
| A001 | Lederrüstung | +3 | - | 80 | 25 | K1 Shop |
| A002 | Kettenhemd | +7 | HP+15 | 300 | 90 | K2 Shop |
| A003 | Stahlrüstung | +12 | HP+30 | 700 | 210 | K4 Shop |
| A004 | Mithril-Panzer | +18 | HP+50, AGI-2 | 1500 | 450 | K7 Shop |
| A005 | Drachenrüstung | +25 | HP+80, Feuer-Resist 30% | 3500 | 1050 | K9 Shop |
| A006 | Magier-Robe (leicht) | +2 | MP+10, INT+2 | 80 | 25 | K1 Shop |
| A007 | Magier-Robe (mittel) | +5 | MP+20, INT+4 | 300 | 90 | K2 Shop |
| A008 | Arkane Robe | +8 | MP+35, INT+6 | 700 | 210 | K4 Shop |
| A009 | Elementar-Robe | +12 | MP+50, INT+8, Alle Elem-Resist 10% | 1500 | 450 | K7 Shop |
| A010 | Zeitweber-Robe | +16 | MP+80, INT+12, Alle Elem-Resist 20% | 3500 | 1050 | K9 Shop |

### Accessoires

| ID | Name | Effekt | Kauf | Verkauf | Quelle |
|----|------|--------|------|---------|--------|
| AC001 | Glücksamulett | LUK+5 | 150 | 45 | K2 Shop |
| AC002 | Kraftring | STR+5 | 150 | 45 | K2 Shop |
| AC003 | Weisheitsring | INT+5 | 150 | 45 | K2 Shop |
| AC004 | Schnelligkeitsstiefel | AGI+5 | 150 | 45 | K2 Shop |
| AC005 | Lebensstein | HP+50 | 400 | 120 | K4 Shop |
| AC006 | Manastein | MP+30 | 400 | 120 | K4 Shop |
| AC007 | Flammenring | Feuer-Schaden +15% | 800 | 240 | K6 Drop |
| AC008 | Frostmantel | Eis-Resist 30% | 800 | 240 | K6 Drop |
| AC009 | Blitzmedaillon | Blitz-Schaden +15% | 800 | 240 | K7 Drop |
| AC010 | Dunkler Schutzstein | Dunkel-Resist 30% | 1200 | 360 | K9 Shop |

### Verbrauchsgegenstände

| ID | Name | Effekt | Kauf | Verkauf |
|----|------|--------|------|---------|
| C001 | Heiltrank (klein) | +50 HP | 30 | 10 |
| C002 | Heiltrank (mittel) | +150 HP | 80 | 25 |
| C003 | Heiltrank (groß) | +400 HP | 200 | 60 |
| C004 | Manatrank (klein) | +30 MP | 40 | 12 |
| C005 | Manatrank (mittel) | +80 MP | 100 | 30 |
| C006 | Manatrank (groß) | +200 MP | 250 | 75 |
| C007 | Elixier | +100% HP + MP | 500 | 150 |
| C008 | Gegengift | Heilt Gift | 20 | 6 |
| C009 | Phönixfeder | Belebt mit 30% HP | 300 | 90 |
| C010 | Rauchbombe | Flucht garantiert | 50 | 15 |
| C011 | ATK-Boost | +30% ATK, 1 Kampf | 100 | 30 |
| C012 | DEF-Boost | +30% DEF, 1 Kampf | 100 | 30 |

### Key-Items (nicht verkäuflich)

| ID | Name | Effekt | Quelle |
|----|------|--------|--------|
| K001 | Mysteriöser Kristall | Story-Item, enthält Nihilus-Energie | K3 Boss-Drop |
| K002 | Aldrics Notizen | Enthüllt Zeitriss-Magie Lore | K4 Aldric Bond |
| K003 | Ariasmedaillon | Affinität+20 mit Aria | K2 Side-Quest |
| K004 | Kaels Diebeswerkzeug | Öffnet versteckte Truhen | K5 Kael Bond |
| K005 | Lunas Heilkristall | 1x Vollheilung pro Kapitel | K5 Luna Bond |
| K006 | Nihilus-Fragment | Story-Item, leuchtet in K7/K10 | K8 Boss-Drop |
| K007 | ARIAs Kernspeicher | Enthüllt ARIA = Aldric Fragment | K8 Story |
| K008 | Dunkelmagier-Siegel | Beweis für Xaroths Identität | K9 Boss-Drop |

---

## Affinity/Bond-System

### 5 Stufen pro NPC

| Stufe | Name | Punkte | Belohnung |
|-------|------|--------|-----------|
| 1 | Bekannter | 0 | Basis-Dialoge |
| 2 | Verbündeter | 50 | Bond-Szene 1 + Shop-Rabatt 10% |
| 3 | Freund | 150 | Bond-Szene 2 + Kampf-Unterstützung |
| 4 | Vertrauter | 300 | Bond-Szene 3 + Kombi-Skill |
| 5 | Seelenverwandt | 500 | Bond-Szene 4 + Alternatives Ending-Detail |

### Bond-Szenen-Übersicht

#### Aria (Kriegerin)

| Stufe | Szene | Inhalt | Belohnung |
|-------|-------|--------|-----------|
| 2 | "Trainingspartner" | Aria und Protagonist trainieren zusammen. Sie erzählt von ihrem Traum, die stärkste Kriegerin zu werden. | Shop-Rabatt 10% bei Vex |
| 3 | "Narben" | Aria zeigt ihre Narbe vom Banditen-Kampf. Vertrauensmoment. "Ich zeige das niemandem." | Aria greift 1x pro Kampf ein (+50% ATK Assist) |
| 4 | "Mondlicht-Geständnis" | Am Fluss bei Mondlicht. Aria gesteht dass sie Angst hat, nicht stark genug zu sein. Protagonist ermutigt sie. | Kombi-Skill: "Heldenduo" (2.5x beide greifen an) |
| 5 | "Versprechen" | Aria schwört ewige Treue. "Egal was kommt, ich werde an deiner Seite kämpfen." | Alternatives Detail im Ending |

#### Aldric (Magier)

| Stufe | Szene | Inhalt | Belohnung |
|-------|-------|--------|-----------|
| 2 | "Das Labor" | Aldric zeigt seine chaotische Werkstatt. Erklärt seine Faszination mit Zeitmagie. | MP-Regeneration +10% |
| 3 | "Verbotene Bücher" | Aldric teilt seine Entdeckung über Zeitriss-Magie. "Theoretisch könnte man..." | Aldric heilt 1x pro Kampf (+30% HP) |
| 4 | "Die Wahrheit" | Protagonist konfrontiert Aldric mit ARIA-Daten. Aldric wird blass. "Woher weißt du das?" | Kombi-Skill: "Arkane Fusion" (3.0x Magie-AoE) |
| 5 | "Aldrics Schwur" | Aldric verspricht, den Zeitriss-Zauber NIE zu nutzen. "In dieser Zeitlinie werde ich einen anderen Weg finden." | Key-Item: Aldrics Notizen |

#### Kael (Assassine)

| Stufe | Szene | Inhalt | Belohnung |
|-------|-------|--------|-----------|
| 2 | "Diebesehre" | Kael erklärt seinen Kodex: "Nur von den Reichen stehlen. Nie Unschuldige." | +15% Gold aus Kämpfen |
| 3 | "Vergangenheit" | Kael erzählt von seiner toten Schwester. Die Gilde hat ihm geholfen zu überleben. | Kael entschärft 1x pro Dungeon eine Falle |
| 4 | "Vertrauen" | Kael gibt dem Protagonisten seinen wertvollsten Besitz - das Medaillon seiner Schwester. | Kombi-Skill: "Schattentanz" (3.0x, garantiert Crit) |
| 5 | "Bruder" | Kael: "Ich hatte eine Schwester. Jetzt habe ich einen Bruder." | Key-Item: Kaels Diebeswerkzeug |

#### Luna (Heilerin)

| Stufe | Szene | Inhalt | Belohnung |
|-------|-------|--------|-----------|
| 2 | "Kräutergarten" | Luna zeigt ihre Heilkräuter. Ruhiger, friedlicher Moment. | Heiltränke 20% effektiver |
| 3 | "Mysterium" | Luna gibt zu, Erinnerungslücken zu haben. "Manchmal weiß ich Dinge die ich nicht wissen sollte." (Hint: Sie hat ebenfalls Zeitriss-Fragmente) | Luna heilt automatisch 10% HP nach jedem Kampf |
| 4 | "Offenbarung" | Luna erinnert sich an ein Gesicht: den Protagonisten. Aber älter. "Ich glaube... wir haben uns schon einmal getroffen." | Kombi-Skill: "Heiliges Licht" (Volle Heilung + Buff) |
| 5 | "Schicksal" | Luna akzeptiert dass sie die Zukunft nicht kontrollieren kann. "Aber ich kann diese Gegenwart beschützen." | Key-Item: Lunas Heilkristall |

#### Vex (Schmied)

| Stufe | Szene | Inhalt | Belohnung |
|-------|-------|--------|-----------|
| 2 | "Handwerk" | Vex zeigt wie man Waffen pflegt. Praktischer Typ, wenig Worte. | Waffen-Reparatur (ATK-Buff nach Kampf) |
| 3 | "Meisterwerk" | Vex arbeitet an seiner Lebenswaffe. Braucht seltenes Material. Side-Quest. | Vex verbessert eine Waffe kostenlos (+5 ATK) |
| 4 | "Familie" | Vex erzählt von seiner Familie die er verloren hat. Emotionaler Moment. | Kombi-Skill: "Geschmiedete Klinge" (2.0x, durchbricht DEF) |
| 5 | "Erbe" | Vex schmiedet dem Protagonisten eine einzigartige Waffe. "Mein bestes Werk." | Einzigartige Waffe: "Vex' Meisterwerk" (+35 ATK, +10 alle Stats) |

---

## Karma-System (versteckt)

### Mechanik

- Unsichtbarer Wert: -100 bis +100 (Start: 0)
- Spieler sieht den Wert NICHT
- ARIA kommentiert subtil ("Interessante Wahl..." bei Karma-Änderung)
- Beeinflusst: NPC-Reaktionen, verfügbare Dialog-Optionen, Ending

### Karma-Quellen (Auswahl)

| Kapitel | Entscheidung | Karma |
|---------|-------------|-------|
| K1 | Wolf-Rudel bekämpfen | +1 |
| K1 | Wolf-Rudel fliehen | -1 |
| K2 | Banditen konfrontieren | +2 |
| K2 | Banditen umgehen | 0 |
| K3 | Kael vertrauen | 0 |
| K3 | Kael misstrauen | -1 |
| K3 | Albtraum "tiefer graben" | +1 |
| K4 | Aldric die Wahrheit sagen | +2 |
| K4 | Aldric belügen | -2 |
| K5 | Aria-Pfad wählen | 0 |
| K5 | Kael-Pfad wählen | 0 |
| K6 | Gefangene Kreatur befreien | +3 |
| K7 | Dorf evakuieren | +2 |
| K7 | Dorf verteidigen | 0 |
| K7 | Aldric dunkle Magie erlauben | -2 |
| K8 | Erinnerungen annehmen | +2 |
| K9 | NPC retten (pro NPC) | +1 |
| K10 | Xaroth töten | -3 |
| K10 | Xaroth gefangen nehmen | +3 |
| K10 | Inspirierend sprechen | +2 |
| K10 | Rachsüchtig sprechen | -2 |

### Ending-Schwellenwerte

| Karma | Klassifizierung | Ending-Typ |
|-------|----------------|------------|
| +30 bis +100 | Licht | Helles Ending |
| -29 bis +29 | Neutral | Tendiert zum nächstliegenden |
| -100 bis -30 | Dunkel | Dunkles Ending |

---

## Monetarisierung

### Kein Echtgeld für Kapitel. Kein Banner. Kein Gacha. Kein Pay-to-Win.

### Gold als einzige Währung

Gold wird für ALLES verwendet: Waffen, Rüstung, Items, UND Kapitel-Freischaltung.

**Gold-Quellen:**

| Quelle | Gold-Menge | Häufigkeit |
|--------|-----------|------------|
| Normale Gegner | 8-70 | Pro Kampf |
| Bosse | 30-500 | Pro Boss |
| Quest-Abschluss | 50-300 | Pro Quest |
| Item-Verkauf | Variiert | Jederzeit |
| **Rewarded Video** | **500** | **3x pro Tag** |
| Daily Login | 50-200 | Täglich (Streak) |
| Kodex-Entdeckung | 20-50 | Pro Eintrag |

**Kapitel-Kosten (Gold):**

| Kapitel | Kosten | Kumuliert |
|---------|--------|-----------|
| P1-P3 | Gratis | 0 |
| K1-K5 | Gratis | 0 |
| K6 | 2.000 | 2.000 |
| K7 | 3.000 | 5.000 |
| K8 | 4.000 | 9.000 |
| K9 | 5.000 | 14.000 |
| K10 | 6.000 | 20.000 |

**Geschätzte Gold-Einnahmen pro Kapitel (natürliches Spiel):**
- K1-K5: ~3.000-5.000 Gold gesamt (+ Videos: ~4.500 extra bei 3x/Tag über 3 Tage)
- Spieler sollte K6 nach normalem Durchspielen von K1-K5 freischalten können

### Rewarded Ad Placements

| Placement | Belohnung | Cooldown |
|-----------|-----------|----------|
| gold_bonus | 500 Gold | 3x pro Tag |
| time_rift | Entscheidung rückgängig | 1x pro Kapitel |
| bonus_exp | 2x EXP nächster Kampf | 3x pro Tag |
| revive | Nach Tod: volles HP | 1x pro Kampf |
| daily_prophecy | Tägliche Prophezeiung | 1x pro Tag |
| kodex_hint | Hinweis auf Kodex-Entry | 3x pro Tag |

### IAPs (optional, nie nötig)

| Produkt | Preis | Typ |
|---------|-------|-----|
| Gold-Paket klein (5.000 Gold) | 1,99 EUR | Consumable |
| Gold-Paket mittel (15.000 Gold) | 3,99 EUR | Consumable |
| Gold-Paket groß (40.000 Gold) | 7,99 EUR | Consumable |
| Zeitkristall x5 | 1,99 EUR | Consumable |
| Zeitkristall x15 | 3,99 EUR | Consumable |
| remove_ads | 3,99 EUR | Non-Consumable |

---

## Kampf-System

### Schadens-Formel

```
Schaden = (ATK * SkillMultiplier) - (DEF * 0.5) + Random(-10%, +10%)
Elementar-Bonus: 1.5x bei Schwäche, 0.5x bei Resistenz
Kritischer Treffer: 2.0x (Basis-Chance: LUK * 0.5%)
Minimum-Schaden: 1
```

### Mechaniken

| Mechanik | Beschreibung |
|----------|-------------|
| Schwäche-System | 6 Elemente: Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer |
| Kritischer Moment | Bei bestimmten HP-Schwellen → spezielle Story-Option |
| Combo-Chain | 3 richtige Entscheidungen = Bonus (1.5x / 2x / 3x) |
| Flucht | Immer möglich, manchmal Story-Konsequenzen |
| Boss-Phasen | 2-3 Phasen mit wechselnden Mechaniken |
| Manga-Panel | Kritische Treffer / Boss-Kills → Split-Screen |
| Splash-Art | Bei Ultimate-Skills → Charakter-Portrait mit Impact |

---

## Kodex/Bestiary

| Kategorie | Freischaltung | 100%-Bonus |
|-----------|---------------|------------|
| Bestiary (25+15 Einträge) | Gegner besiegen | Versteckter Boss-Kampf |
| Weltatlas (20 Orte) | Orte besuchen | Geheime Schatzkarte (1.000 Gold) |
| Personenakten (6 NPCs) | NPC-Interaktion | Bonus-Epilog-Szene |

---

## Overworld-Map

### Knoten-Typen

| Farbe | Typ | Beschreibung |
|-------|-----|-------------|
| Gold | Story-Knoten | Hauptquest, muss besucht werden |
| Silber | Side-Quest | Optional, Bonus-EXP/Items/Affinität |
| Rot | Boss | Kampf-Knoten |
| Blau | NPC/Shop | Handel, Bond-Szenen |
| Lila | Dungeon | Mehrere Kämpfe hintereinander |
| Grün | Raststätte | HP/MP regenerieren, Speichern |
| Grau + Schloss | Gesperrt | Kapitel nicht freigeschaltet |
| Nebel | Fog-of-War | Unentdeckte Bereiche |

### Visuelle Features

- Animierte Pfade (leuchtende pulsierende Linien)
- Partikel je nach Region
- Tag/Nacht-Zyklus (rein visuell)
- Charakter-Sprite bewegt sich entlang Pfade
- Zoom-Stufen: Übersicht + Detail
- Goldener Haken bei erledigten Knoten

---

## Visueller Stil

### Farbpalette

| Token | Farbe | Verwendung |
|-------|-------|-----------|
| Primary | #4A90D9 (System-Blau) | System-Fenster, UI-Akzente |
| Secondary | #9B59B6 (Mystisch-Lila) | Magie, Skill-Effekte |
| Accent | #F39C12 (Gold) | EXP, Belohnungen, Gold-Währung |
| Danger | #E74C3C (Rot) | HP-Verlust, Gegner |
| Success | #2ECC71 (Grün) | Heilung, Affinität |
| Dark BG | #0D1117 | Haupthintergrund |
| Panel BG | #161B22 | System-Fenster Hintergrund |
| Text | #E6EDF3 | Haupttext |
| System Text | #58A6FF | ARIA / System-Nachrichten |

### Transitions

| Transition | Verwendung |
|-----------|-----------|
| Fade (schwarz) | Standard Scene-Wechsel |
| Dissolve (Partikel) | Isekai-Teleportation, Traumwelt |
| Slide (horizontal) | Overworld Navigation |
| Glitch-Cut | System-Events, Überraschungsmomente |
| Manga-Wipe | Dramatische Story-Momente |
| Iris-Close | Kapitel-Ende |

---

## Font-Auswahl

| Font | Verwendung | Begründung | Lizenz |
|------|-----------|------------|--------|
| Noto Sans JP | Dialog-Text, Story | Perfekte CJK-Unterstützung, lesbar auf kleinen Screens, deckt alle 6 Sprachen ab | OFL (SIL Open Font License) |
| Rajdhani | Überschriften, Menü-Titel | Futuristisch-technisch, passt zum "System"-Aesthetic, gute Lesbarkeit | OFL |
| Share Tech Mono | ARIA-Text, System-Nachrichten | Monospace mit Tech-Feeling, screams "AI/Computer", perfekt für das Solo-Leveling-System | OFL |
| Oswald | Kampf-Zahlen, Damage-Numbers, Level-Up | Kondensiert, bold, hohe Impact-Wirkung, gut lesbar auch bei schneller Animation | OFL |

Alle Fonts sind unter SIL Open Font License frei für kommerzielle Nutzung.

---

## Audio-Konzept

### BGM (Hintergrundmusik)

| Szene | Stimmung | Quelle |
|-------|----------|--------|
| Titelbildschirm | Mystisch, ruhig | OpenGameArt - "Fantasy Ambient" (CC0) |
| Dorf / Raststätte | Warm, friedlich | OpenGameArt - "Village Theme" (CC0) |
| Dungeon / Labyrinth | Spannend, dunkel | OpenGameArt - "Dungeon Ambience" (CC0) |
| Boss-Kampf | Episch, treibend | OpenGameArt - "Epic Boss Battle" (CC0) |
| Normaler Kampf | Energisch | OpenGameArt - "Battle Theme" (CC0) |
| Emotionale Szene | Traurig, Piano | OpenGameArt - "Sad Piano" (CC0) |
| System/ARIA | Digital, ambient | Freesound - Synthesizer-Ambient (CC0) |
| Overworld-Map | Abenteuerlich | OpenGameArt - "World Map" (CC0) |
| Traumwelt | Surreal, verzerrt | Freesound - Reverse/Ethereal Pads (CC0) |
| Prolog-Kampf | Hoffnungslos, episch | OpenGameArt - "Final Battle" (CC0) |

### SFX (Soundeffekte)

| Kategorie | Sounds | Quelle |
|-----------|--------|--------|
| System-UI | Fenster öffnen/schließen, Typing, Level-Up Fanfare | Kenney.nl - UI Audio Pack (CC0) |
| Kampf | Schwert-Slash, Magie-Cast, Hit-Impact, Crit, Ausweichen | Kenney.nl - Impact Sounds (CC0) |
| UI | Button-Tap, Navigate, Bestätigung, Fehler | Kenney.nl - UI Sounds (CC0) |
| Ambiente | Wind, Regen, Feuer, Dungeon-Echo | Freesound - Nature/Ambient (CC0) |
| Spezial | Glitch-Sound, Zeitriss, Nihilus-Stimme | Freesound - Sci-Fi (CC0) |

---

## Save-System (SQLite)

| Tabelle | Felder |
|---------|--------|
| SaveSlot | Id, SlotName, CreatedAt, LastPlayedAt, PlayTimeSeconds, ChapterId, ClassName |
| PlayerData | SaveSlotId, Level, EXP, HP, MP, STR, INT, AGI, VIT, LUK, Gold, Karma |
| Inventory | SaveSlotId, ItemId, Quantity, IsEquipped |
| SkillData | SaveSlotId, SkillId, Level, MasteryCount |
| AffinityData | SaveSlotId, NpcId, Points, CurrentRank |
| StoryProgress | SaveSlotId, NodeId, ChoiceId, Timestamp |
| CodexEntries | SaveSlotId, EntryType, EntryId, IsDiscovered |
| ChapterUnlocks | ChapterId, UnlockMethod, UnlockedAt |

3 Save-Slots + Auto-Save bei jedem Knoten-Wechsel.
ChapterUnlocks gelten für ALLE Save-Slots.

---

## Visual Novel Features

- Typewriter-Texteffekt (einstellbar: langsam/mittel/schnell/sofort)
- Tap während Typewriter = Text sofort komplett
- Skip-Modus (bekannten Text überspringen)
- Auto-Modus (automatisch weiter nach 2s)
- Backlog (vorherige Dialoge lesen)
- 3 Save-Slots (klassisch JRPG)

---

## Performance (BomberBlast-Patterns)

| Technik | Beschreibung |
|---------|-------------|
| SKPaint-Pooling | Alle wiederverwendet, statische Cleanup() |
| SKPath-Pooling | Rewind() statt neu erstellen |
| Struct-Partikel | Kein GC-Druck, feste Pool-Größe |
| Shader-Cache | Basis-Shader gecacht, nur bei Resize neu |
| Lazy Scene Loading | OnEnter()/OnExit() für Ressourcen |
| DPI-Handling | canvas.LocalClipBounds + proportionale Touch-Skalierung |

---

## Store-Listing

### App-Info

| Feld | Wert |
|------|------|
| App-Name | Reborn Saga: Isekai Rising |
| Package-ID | org.rsdigital.rebornsaga |
| Kategorie | Rollenspiel |
| Altersfreigabe | 12+ (Fantasy-Gewalt) |

### Kurzbeschreibung (DE)
"Erwache als gefallener Held in einer Fantasy-Welt. Wähle deine Klasse, baue Freundschaften auf, triff Entscheidungen die alles verändern. Ein Anime Isekai-RPG mit 13 Kapiteln, 6 Endings und Solo Leveling-inspiriertem System."

### Kurzbeschreibung (EN)
"Awaken as a fallen hero in a fantasy world. Choose your class, build bonds, make choices that change everything. An anime isekai RPG with 13 chapters, 6 endings and a Solo Leveling-inspired system."

### Screenshots-Plan

1. Title Screen mit Partikel-Effekten
2. Klassenwahl (3 Portraits)
3. Dialog-Szene mit ARIA System-Fenster
4. Status-Window (Solo Leveling Stil)
5. Kampf-Szene mit Aktions-Optionen
6. Overworld-Map mit leuchtenden Knoten
7. Bond-Szene (emotionaler Moment)
8. Boss-Kampf mit Manga-Panel-Effekt

### AdMob

| Feld | Wert |
|------|------|
| Publisher-ID | ca-app-pub-2588160251469436 |
| Placement: gold_bonus | ca-app-pub-2588160251469436/XXXXXXXXXX |
| Placement: time_rift | ca-app-pub-2588160251469436/XXXXXXXXXX |
| Placement: bonus_exp | ca-app-pub-2588160251469436/XXXXXXXXXX |
| Placement: revive | ca-app-pub-2588160251469436/XXXXXXXXXX |
| Placement: daily_prophecy | ca-app-pub-2588160251469436/XXXXXXXXXX |
| Placement: kodex_hint | ca-app-pub-2588160251469436/XXXXXXXXXX |

Ad-Unit-IDs werden nach Registrierung in der AdMob Console eingetragen.

---

## Projekt-Dependencies

Keine neuen NuGets. Bestehender Stack:

- Avalonia 11.3 (Host)
- SkiaSharp 3.119.2 (Rendering)
- CommunityToolkit.Mvvm (MainViewModel)
- sqlite-net-pcl (Save-System)
- MeineApps.Core.Ava (Localization, Preferences, BackPressHelper, UriLauncher)
- MeineApps.Core.Premium.Ava (AdMob Rewarded, IAP, PurchaseService)

---

## Geplante Arcs (Zukunft)

| Arc | Titel | Kapitel | Status |
|-----|-------|---------|--------|
| Prolog | Der Fall des Helden | P1-P3 | In Entwicklung |
| Arc 1 | Erwachen | K1-K10 | In Entwicklung |
| Arc 2 | Schatten der Vergangenheit | TBD | Geplant |
| Arc 3+ | TBD | TBD | Konzept |

Nihilus ist der Endgegner über ALLE Arcs. Jeder Arc bringt den Protagonisten näher an die Stärke die er braucht um Nihilus in der finalen Konfrontation zu besiegen.
