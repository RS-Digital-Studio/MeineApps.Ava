# ArcaneKingdom — Setup-Anleitung (Unity 6.4)

Schritt-fuer-Schritt-Guide fuer das **erste Oeffnen** des Projekts in Unity 6 (6000.4.x).
Realistischer Zeitaufwand: **30-60 Minuten**, davon ist das meiste Wartezeit beim
Package-Resolve und Asset-Import.

> Ergebnis nach Befolgen dieser Anleitung: lauffaehiges Boot → Login (Stub) → Hub-Scene,
> alle 165+ Domain-Tests gruen, AAB-Build-Pipeline einsatzbereit.

---

## 1. Unity 6 — was du brauchst

| Komponente | Wert |
|------------|------|
| Editor | **Unity 6000.4.8f1** (genau diese Minor-Version, andere 6000.4.x sollten auch laufen) |
| Module | **Android Build Support** (inkl. **Android SDK & NDK Tools** + **OpenJDK**) |
| Optional | **Windows Build Support (IL2CPP)** wenn du auf Desktop testen willst |
| RAM | mindestens 16 GB empfohlen (Unity 6 ist hungrig beim Import) |
| Disk | ca. 8-10 GB fuer `Library/` + Android-Tools |

> Unity Hub: **Installs → Install Editor** und die Version + Module waehlen.
> Falls die genaue Version nicht in der Liste ist: **Archive** anklicken → Browser-Link
> → richtige Version aus dem Unity-Archiv.

---

## 2. Projekt oeffnen

**Unity Hub → Add → Add project from disk → waehle:**

```
F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity\
```

**Beim ersten Oeffnen passiert das** (in dieser Reihenfolge, **5-20 Minuten**):

1. Unity erstellt `Library/` (nicht in Git, lokal generiert)
2. Packages werden aufgeloest aus `Packages/manifest.json`:
   - Unity Registry: Addressables, URP 17, Localization, Newtonsoft.Json, TextMeshPro, …
   - OpenUPM: VContainer 1.16, UniTask 2.5.10, UniRx 7.1
3. Asset-Import — alle JSON/CSV/Scripts werden indiziert
4. Auto-Compile der C#-Scripts (6 Assemblies + 2 Tests-Assemblies)

**Falls Package-Resolve-Fehler:** Window → Package Manager → ⟳ Refresh
**Falls "Importing Assets" haengt:** Library/ loeschen + Unity neu starten

---

## 3. First-Time Setup Wizard

Nach dem ersten Compile **oeffnet sich automatisch** das Setup-Fenster (oder manuell:
**Menue ArcaneKingdom → Setup → First-Time Setup Wizard**).

Drei automatische Schritte mit ✓/○-Status:

| Schritt | Aktion |
|---------|--------|
| 1. BalancingConfig.asset | "Anlegen"-Button → erzeugt `Assets/_Project/ScriptableObjects/Config/BalancingConfig.asset` |
| 2. JSON-Daten importieren | "Import All"-Button → liest alle Resources/Data/*.json und erzeugt ScriptableObject-Assets unter `Assets/_Project/ScriptableObjects/` (30 Karten, 32 Faehigkeiten, 18 Runen, 6 Helden, 9 Welten, 4 Sets) |
| 3. Build-Scenes registrieren | "Registrieren"-Button → traegt alle 6 Scenes in den Build-Settings ein, Boot.unity als Index 0 |

**Schneller Weg:** Button **"Alle automatischen Schritte ausfuehren (1-3)"** macht alle drei in einem Klick.

**Console** sollte zeigen:
```
[Setup] BalancingConfig.asset angelegt unter Assets/_Project/ScriptableObjects/Config/BalancingConfig.asset
[DataImporter] 32 Faehigkeiten importiert.
[DataImporter] 30 Karten importiert.
[DataImporter] 18 Runen importiert.
[DataImporter] 9 Welten importiert.
[DataImporter] 6 Helden importiert.
[DataImporter] BalancingConfig-Asset vorhanden (Werte via Inspector pflegen).
[DataImporter] Alle Daten importiert.
[Setup] Build-Scenes registriert (6 Scenes, Boot.unity als Index 0).
```

---

## 4. Bootstrapper-Scene verdrahten (manueller Schritt)

Diese eine Sache muss von Hand passieren — Scene-YAML-GUIDs sind Maschinen-spezifisch.

Im Wizard steht der "Boot.unity jetzt oeffnen"-Button, oder Doppelklick im Project-Fenster:
`Assets/_Project/Scenes/Boot/Boot.unity`

Dann in der Hierarchy:

1. **[Bootstrapper]** auswaehlen
2. Inspector: **Add Component → ArcaneKingdom.Bootstrap.RootLifetimeScope**
3. Im RootLifetimeScope-Inspector: Slot **Balancing Config** mit dem in Schritt 3 erzeugten Asset belegen (Drag-and-Drop aus dem Project-Fenster)
4. Unter [Bootstrapper] ein neues leeres Child anlegen (Rechtsklick → Create Empty), nennen: **[Audio]**
5. [Audio] auswaehlen → **Add Component → ArcaneKingdom.Game.Services.UnityAudioService**
6. Zurueck zu [Bootstrapper] → Slot **Audio Service** mit [Audio]-GameObject belegen
7. Boot.unity speichern (Strg+S)

---

## 5. Erster Play-Test

Boot.unity im Editor offen → **Play** druecken (Strg+P).

**Erwartung in der Console:**

```
[Boot] ArcaneKingdom gestartet.
[FirebaseAuth] SignInAnonymouslyAsync — STUB. Firebase Unity SDK fehlt.
[Save] Neuer Save initialisiert.
[Login] Lade Hub-Scene...
[SceneLoader] Lade additive: Hub
```

Wenn das so durchlaeuft → **die gesamte Business-Logik funktioniert**. Du siehst noch
keine UI — die Scenes haben nur Cameras + GameObjects als Platzhalter. Das ist der
gewollte MVP-Ausgangspunkt.

---

## 6. Tests laufen lassen (empfohlen)

**Window → General → Test Runner**

In den EditMode-Tests sollten **alle 165+ Tests gruen** durchlaufen (etwa 20-30 Sekunden).

Falls Tests nicht erkannt werden: Project Settings → Player → Other Settings →
Scripting Define Symbols muss `UNITY_INCLUDE_TESTS` enthalten (Default ja).

---

## 7. Optionale Editor-Tools

Im Menue **ArcaneKingdom → Inspectors**:

| Tool | Was es zeigt |
|------|--------------|
| **Card Preview** | Alle 30 Karten in sortierbarer Tabelle mit Element/Rarity-Filter |
| **Balancing Dashboard** | Stat-Verteilungen + Power-Outliers (welche Karten zu stark/schwach) |
| **Localization Check** | Findet fehlende oder unbenutzte Locale-Keys |

---

## 8. Android-Build (AAB)

**Menue ArcaneKingdom → Build → Android Release (AAB)**

Output landet in `Unity/Build/arcanekingdom-<version>.aab`. Beim ersten Mal dauert das
**10-20 Minuten** (IL2CPP-Compile + Gradle-Build).

**Signierung:** Player Settings → Publishing Settings → Custom Keystore aktivieren:
- Pfad: `F:\Meine_Apps_Ava\Releases\meineapps.keystore`
- Passwort: `MeineApps2025`
- Alias: `meineapps`

---

## 9. Render Pipeline (URP) — optional

URP 17.x ist im `manifest.json` mit drin, aber NICHT automatisch als aktive Render Pipeline gesetzt. Das **MUSS** Robert per Hand machen wenn URP-Features (Shader Graph, Visual Effect Graph) genutzt werden sollen:

1. **Project-Fenster → Create → Rendering → URP Asset (mit Universal Renderer)**
2. Place under `Assets/_Project/Settings/` (Folder bei Bedarf anlegen)
3. **Edit → Project Settings → Graphics** → Slot **Scriptable Render Pipeline Asset** mit dem gerade erzeugten Asset belegen
4. **Edit → Project Settings → Quality** → fuer jede Quality-Stufe das URP Asset zuweisen

> Fuers MVP nicht zwingend — Default Built-in Render Pipeline funktioniert auch.

---

## Troubleshooting (Unity 6 spezifisch)

| Problem | Loesung |
|---------|---------|
| "Project requires Unity X.X.X" | Genau die in ProjectVersion.txt angegebene Version installieren — Unity Hub bietet Upgrade an, wenn die Version aktueller ist (ist meist OK fuer Minor-Versionen) |
| Package Resolve Errors | Window → Package Manager → ⟳ Refresh oder `Library/` loeschen und Unity neu starten |
| UniTask / VContainer / UniRx nicht gefunden | OpenUPM-Registry wurde nicht akzeptiert → Edit → Project Settings → Package Manager → "I made my choice" bestaetigen |
| `Missing script` auf [Bootstrapper] | Schritt 4 noch nicht durchgefuehrt — RootLifetimeScope per Add Component anhaengen |
| `Resources.LoadAll<T>("")` liefert leer | Schritt 2 (DataImporter) nicht ausgefuehrt — Wizard nochmal oeffnen |
| Android-Build: JDK-Fehler | Unity Hub → Installs → 6000.4.8f1 → ⋮ → Add modules → JDK + Android SDK & NDK Tools |
| `obsolete API` Warnungen | Unity 6 hat einige BuildTargetGroup-APIs deprecated — BuildScripts.cs nutzt bereits NamedBuildTarget |
| URP Cameras zeigen Rosa/Magenta | URP nicht als Render-Pipeline gesetzt (Schritt 9) ODER Materialien sind noch Built-in — Materialien upgraden: Window → Rendering → Render Pipeline Converter |

---

## Was als naechstes (MVP-Roadmap)

1. **Firebase Unity SDK** installieren (Firebase Console → Projekt anlegen → `google-services.json` runterladen → in `Assets/` legen)
2. **UI-Layouts** bauen — Hub mit Energie-Bar + Navigation, Battle mit Drag&Drop fuer Karten, Deck-Builder
3. **Karten-Artworks** generieren (Mid-Journey/Stable-Diffusion)
4. **Cloud-Functions** deployen (siehe `Server/SERVEROPS.md`)

Bei Problemen: Screenshot der Console-Fehlermeldung schicken.
