# Data-Pipeline (JSON-as-Source-of-Truth)

Alle Spielkonfigurationen — Karten, Welten, Faehigkeiten, Runen — werden hier als JSON
gepflegt und vom **DataImporter** (Editor-Tool, Menue *ArcaneKingdom -> Data -> Import All*)
in `ScriptableObject`-Assets unter `Assets/_Project/ScriptableObjects/...` konvertiert.

Vorteil: JSONs sind text-editierbar (kein Unity-Editor noetig fuer Balancing-Aenderungen),
versionierbar in Git und vermeiden die GUID-Klemmen der `.asset`-YAML-Dateien.

## Dateien

| Datei | Inhalt |
|-------|--------|
| `cards.json` | Alle Karten-Definitionen (Element, Cost, ATK/HP, Faehigkeiten-IDs) |
| `abilities.json` | Faehigkeits-Bibliothek (Magnitude, Dauer, Targeting) |
| `runes.json` | Runen-Definitionen |
| `worlds.json` | 9 Welten + 90 Nodes (Gegner-Decks, Belohnungen) |
| `balancing.json` | Globale Konstanten fuer BalancingConfig-SO |

## Import-Workflow

1. JSON editieren (z.B. `cards.json`)
2. Unity-Editor oeffnen
3. Menue *ArcaneKingdom -> Data -> Import All* (oder *Import Cards* fuer Teilimport)
4. Generierte Assets liegen unter `Assets/_Project/ScriptableObjects/Cards/`, etc.
5. Pruefen, committen.

## Validierung

Der Importer prueft beim Import:
- IDs sind eindeutig
- Faehigkeits-IDs in `abilities.json` existieren
- Gegner-Deck-Karten-IDs in `worlds.json` existieren
- Werte sind in plausiblen Ranges (Cost 1-10, Level 0-15, etc.)

Bei Fehlern: Build wird abgebrochen, Log zeigt fehlerhafte Stelle.

## Lokalisierung

Karten/Faehigkeiten haben **Keys**, nicht direkte Texte:
- Karten-Name: `card.drachenherrscher.name`
- Karten-Flavor: `card.drachenherrscher.flavor`
- Faehigkeit-Name: `ability.doppelschlag.name`
- Faehigkeit-Beschreibung: `ability.doppelschlag.desc`

Texte werden in `Assets/_Project/Resources/Localization/strings.csv` gepflegt und vom
Unity-Localization-Package eingelesen.
