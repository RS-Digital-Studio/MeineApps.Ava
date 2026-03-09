# RebornSaga: Charakter-Visual-Upgrade — Design-Dokument

> Datum: 2026-03-06
> Status: Genehmigt
> Scope: Komponenten-basiertes Anime-Rendering für alle 10 Charaktere
> Priorität: Charakter-Visuals zuerst, Story-Texte als separater Plan später

---

## Zusammenfassung

Das aktuelle Charakter-Rendering ist zu primitiv für ein Anime-RPG: flache Farben, Oval-Kopf ohne Nase/Ohren, geometrischer Trapez-Körper ohne Arme, keine Haar-Tiefe. Ziel ist ein stilisierter Anime-Look mit Gradienten, Schattierung, Highlights und natürlicheren Proportionen — prozedural mit SkiaSharp, ohne Bitmap-Assets.

**Ansatz:** Komponenten-basierte Renderer (7 spezialisierte Klassen + Layout-System) statt einer monolithischen CharacterParts-Klasse.

**Render-Modi:** Portrait (Brust aufwärts, 90% der Spielzeit) UND Fullbody (Klassenwahl, Status) gleich hochwertig.

---

## Architektur

### Dateistruktur

```
Rendering/Characters/
├── CharacterDefinition.cs       (erweitert: +HasBangs, +BangStyle, +AuraColor)
├── CharacterDefinitions.cs      (10 Definitionen aktualisiert)
├── CharacterLayout.cs           (NEU: Ankerpunkt-Berechnung, RenderMode)
├── EmotionSet.cs                (unverändert: 6 Emotionen)
├── CharacterRenderer.cs         (Fassade: DrawPortrait, DrawFullBody, DrawIcon — unverändert)
├── CharacterParts.cs            (wird zum Orchestrator, ruft Renderer in Reihenfolge auf)
├── Renderers/
│   ├── FaceRenderer.cs          (Kopf, Nase, Ohren, Kinn, Mund, Haut-Shading)
│   ├── EyeRenderer.cs           (Anime-Augen: Iris-Gradient, Pupille, Reflektionen, Brauen)
│   ├── HairRenderer.cs          (DrawBack + DrawFront, 4 Styles, Pony, Wind-Animation)
│   ├── BodyRenderer.cs          (Hals, Schultern, Arme/Hände, Körpersilhouette)
│   ├── ClothingRenderer.cs      (Outfit-Details: Kragen, Gürtel, Falten, Muster)
│   ├── AccessoryRenderer.cs     (Waffen mit Metall-Glanz, Schmuck, Glow)
│   └── CharacterEffects.cs      (Schatten, Hologramm, Aura, leuchtende Augen)
```

### CharacterLayout — Zentrales Positionssystem

Alle Renderer arbeiten mit einem shared `CharacterLayout` struct statt rohen `(cx, cy, scale)` Parametern. Berechnet alle Ankerpunkte einmal pro Frame.

```csharp
public enum RenderMode { Portrait, FullBody, Icon }

public readonly struct CharacterLayout
{
    // Kopf
    public float HeadCenterX, HeadCenterY;
    public float HeadWidth, HeadHeight;

    // Hals
    public float NeckTopY, NeckBottomY;

    // Schultern + Arme
    public float ShoulderLeftX, ShoulderRightX, ShoulderY;

    // Gesichtsfeatures
    public float EyeY, EyeOffsetX, EyeSize;
    public float MouthY;
    public float NoseX, NoseY;
    public float EarLeftX, EarRightX, EarY;

    // Körper
    public float BodyTop, BodyBottom, BodyWidth;

    // Meta
    public float Scale;
    public RenderMode Mode;

    public static CharacterLayout Calculate(float cx, float cy, float scale, RenderMode mode);
}
```

### Render-Reihenfolge

```
1. CharacterEffects.DrawShadow(layout)           — Boden-Schatten
2. HairRenderer.DrawBack(layout, def, time)       — Haare HINTER dem Körper
3. BodyRenderer.Draw(layout, def)                  — Hals + Schultern + Arme + Silhouette
4. ClothingRenderer.Draw(layout, def)              — Details über Body (Kragen, Gürtel, Falten)
5. AccessoryRenderer.Draw(layout, def, time)       — Waffen an der Seite
6. FaceRenderer.Draw(layout, def, emotion)         — Kopf + Nase + Ohren + Mund
7. EyeRenderer.Draw(layout, def, emotion, time)    — Augen + Brauen + Blinzeln
8. HairRenderer.DrawFront(layout, def, time)       — Pony/Strähnen VOR dem Gesicht
9. CharacterEffects.DrawAura(layout, def, time)    — Glow, Hologramm, Status
```

---

## Visuelle Upgrades pro Komponente

### FaceRenderer — Anime-Gesicht statt Oval

**Aktuell:** Einfaches Oval mit Outline.

**Neu:**
- **Kopfform:** Bezier-Path statt `DrawOval` — leicht eckigeres Kinn, breitere Stirn, weichere Wangenpartie
- **Haut-Shading:** Radialer Gradient (hellerer Kern im Gesicht, dunklere Ränder an Wangen/Kinn)
- **Wangen-Highlight:** Subtiler heller Fleck auf beiden Wangen (Anime-typisch)
- **Nase:** Minimalistischer Anime-Stil (kleines L oder Punkt) — gleich für alle Charaktere
- **Ohren:** Seitlich am Kopf, nur sichtbar bei kurzem Haar (HairStyle 0, 3). Bei langem Haar von Haaren verdeckt
- **Kinn-Definition:** Subtile dunklere Linie unterhalb des Mundes
- **Mund:** 6 Emotions-Varianten, verbessert:
  - Happy: Offener Mund mit weißer Zahnreihe
  - Angry: Zusammengebissene Zähne
  - Surprised: Größeres Oval mit rosa Innenseite
  - Sad: Leicht geöffnet
  - Determined: Leichtes Grinsen
  - Neutral: Dezenter Strich mit leichtem Schatten

### EyeRenderer — Das Herzstück des Anime-Looks

**Aktuell:** Oval + farbige Iris + einzelner weißer Highlight-Punkt.

**Neu:**
- **Iris:** Mehrschichtiger radialer Gradient (äußerer Ring dunkler als EyeColor, innerer Ring heller, Farb-Highlights)
- **Pupille:** Ovale Form statt Kreis, leicht nach oben versetzt
- **Reflektionen:** 2 weiße Punkte (groß oben-rechts, klein unten-links) — DAS typische Anime-Kennzeichen
- **Wimpern:** Oberer Lidrand als dickere Linie mit leichtem Aufwärts-Schwung an den Außenseiten
- **Brauen:** Für ALLE 6 Emotionen gezeichnet (nicht nur Angry/Sad):
  - Neutral: Leicht geschwungene Linie
  - Happy: Leicht angehoben
  - Angry: Nach innen-unten gezogen (verstärkt)
  - Sad: Nach innen-oben gezogen
  - Surprised: Stark angehoben
  - Determined: Gerade, tiefe Position
- **Blinzeln:** Weicherer Übergang (oberes Lid senkt sich statt abrupter Linie)
- **Leuchtende Augen:** Pulsierender Glow mit verbessertem MaskFilter (für Nihilus etc.)

### HairRenderer — Volumen und Bewegung

**Aktuell:** Flache Silhouette mit einzelner Farbe, 3 Highlight-Linien bei Style 1.

**Neu:**
- **Basis-Form:** Mehr Kontrollpunkte für natürlichere Bezier-Kurven
- **Volumen:** Haar-Silhouette ÜBER dem Kopf (nicht bündig) — besonders bei Style 1 und 3
- **Strähnchen:** 3-5 individuelle Strähnen als separate Paths über der Basis-Form
- **Highlights:** Helle Linien entlang der Haar-Kurven (LightenColor 30-40%)
- **Schatten:** Dunklere Bereiche an der Unterseite und nahe am Kopf (DarkenColor 20-30%)
- **Pony (Bangs):** Neues Feature — separate Strähnen die über die Stirn fallen
  - `HasBangs` Property auf CharacterDefinition
  - `BangStyle`: 0=gerade Pony, 1=seitlich geschwungen, 2=lose Strähnen
  - Gezeichnet in DrawFront() → überlagert Stirn und teilweise Augen
- **Wind-Animation:** Pro Strähne unterschiedlich stark (vorne weniger, hinten mehr Sway)
- **DrawBack/DrawFront Split:** Haare hinter dem Kopf (lange Haare, Zopf) vs. vor dem Gesicht (Pony, Seitensträhnen)

### BodyRenderer — Von Trapez zu Anime-Oberkörper

**Aktuell:** Einfacher Trapez-Path mit Outline und Akzent-Streifen.

**Neu:**
- **Hals:** Zylindrischer Verbinder, Hautfarbe mit leichter Schattierung
- **Schultern:** Breitere, natürlichere Form mit Bezier-Kurven (runde Schultern statt eckig)
- **Arme:** Einfache Arme an den Seiten, leicht angewinkelt
  - Portrait-Modus: Nur Oberarm sichtbar (bis Bildrand)
  - Fullbody-Modus: Vollständige Arme mit Händen (Faust für Krieger, offen für Magier)
- **Körperformen (BodyType):**
  - 0 (Schlank): Schmale Schultern, definierte Taille
  - 1 (Muskulös): Breite Schultern, V-Form, muskulöser Oberkörper
  - 2 (Robe): Weite Silhouette, die Körperform verbirgt
- **Outfit-Gradient:** Vertikaler Gradient auf dem Outfit (heller oben, dunkler unten)

### ClothingRenderer — Character-definierende Details

**Aktuell:** Nur Schulterpolster (BodyType 1) und ein Akzent-Streifen (Gürtel).

**Neu:**
- **Kragen:** V-Ausschnitt (Schlank), Stehkragen (Muskulös), Kapuze (Robe) — je nach BodyType
- **Gürtel/Schärpe:** Breiter, mit Schnalle oder Ornament in OutfitAccent
- **Stoff-Falten:** 2-3 subtile DarkenColor-Strokes die Tiefe suggerieren
- **Schulterpolster:** Verbesserter Look für BodyType 1 (runder, mit Highlight)
- **Roben-Saum:** Wellenförmiger unterer Rand statt gerader Linie (BodyType 2)

### AccessoryRenderer — Waffen die nach etwas aussehen

**Aktuell:** Geometrische Dreiecke (Klingen), einfache Rechtecke (Griffe), Kreis (Stab-Kristall).

**Neu:**
- **Schwert:** Klinge mit Metall-Gradient (hell in Mitte, dunkel an Kanten), ornamentierte Parierstange, Leder-Griff
- **Stab:** Holz-Textur (feine Linien-Muster), Kristall mit innerem Glow und angedeuteten Facetten
- **Dolche:** Geschwungene Klinge statt gerades Dreieck, Leder-Griff-Wicklung
- **Alle Waffen:** Subtiler wandernder Glanz-Effekt (weißer Highlight-Punkt entlang der Klinge)

### CharacterEffects — Der letzte Schliff

**Aktuell:** Hologramm-Glow (Kreis) und Augen-Glow (Kreis + MaskFilter).

**Neu:**
- **Boden-Schatten:** Ovaler Schatten unter dem Charakter (nur Fullbody-Modus)
- **Hologramm (SystemAria):** Scan-Lines + periodischer Flicker + blaue Kontur-Linie
- **Leuchtende Augen:** Von EyeRenderer delegiert, pulsierender Glow mit besserer Intensität
- **Aura:** Optionaler farbiger Glow um den gesamten Charakter (`AuraColor` auf CharacterDefinition). Für Boss-Einführungen, Power-Ups, Fate-Moments

---

## CharacterDefinition Erweiterungen

### Neue Properties (3)

```csharp
public bool HasBangs { get; init; }      // Pony ja/nein
public int BangStyle { get; init; }      // 0=gerade, 1=seitlich, 2=strähnenartig
public SKColor? AuraColor { get; init; } // Optionale Aura-Farbe (Bosse, Power-Ups)
```

### Aktualisierte Definitionen

| Charakter | HasBangs | BangStyle | AuraColor |
|-----------|----------|-----------|-----------|
| Protagonist_Sword | true | 2 (Strähnen) | null |
| Protagonist_Mage | true | 1 (seitlich) | null |
| Protagonist_Assassin | false | - | null |
| Aria | true | 1 (seitlich) | null |
| Aldric | false | - | null |
| Kael | true | 2 (Strähnen) | null |
| Luna | true | 0 (gerade) | null |
| Vex | false | - | null |
| SystemAria | true | 1 (seitlich) | null |
| Nihilus | false | - | #8B0000 (dunkelrot) |
| Xaroth | true | 1 (seitlich) | #CC0000 (rot) |

---

## Performance-Strategie

- Alle SKPaint/SKFont/SKMaskFilter **statisch pro Renderer** gepooled (keine per-Frame Allokation)
- SKPath-Objekte per `Rewind()` wiederverwendet
- Gradienten: `SKShader` statisch cachen wo möglich, bei charakter-spezifischen Farben lieber `Paint.Color` + `ColorFilter` statt neuen Shader
- `CharacterLayout.Calculate()` wird einmal pro DrawCharacter()-Aufruf berechnet (struct, kein Heap)
- Jeder Renderer hat eigene statische Paints (kein Cross-Renderer-Sharing)

---

## Implementierungs-Reihenfolge

1. **CharacterLayout** — Ankerpunkt-Berechnung, RenderMode
2. **FaceRenderer + EyeRenderer** — Größter visueller Impact (Gesicht = 80% des Eindrucks)
3. **HairRenderer** — Zweiter großer Impact, definiert Charakter-Identität
4. **BodyRenderer + ClothingRenderer** — Oberkörper von Trapez zu Anime
5. **AccessoryRenderer + CharacterEffects** — Waffen-Upgrade + Schatten/Glow
6. **CharacterParts Refactoring** — Alte Klasse zur dünnen Orchestrierungs-Fassade
7. **CharacterDefinitions Update** — Alle 10 Definitionen mit neuen Properties
8. **CharacterRenderer Integration** — DrawPortrait/DrawFullBody/DrawIcon an neues System anbinden

---

## Nicht im Scope (separat geplant)

- Story-Text-Überarbeitung (eigener Plan)
- Hintergrund-Verbesserungen (eigener Plan)
- Neue Emotionen (über die bestehenden 6 hinaus)
- Equipment-abhängige Visuals (kommt mit Inventar-System)
