# Art/Models — 3D-Assets (ComfyUI-Pipeline-Output)

Diese GLB-Modelle stammen aus der lokalen, EU-sauberen Image-to-3D-Pipeline (außerhalb des Git-Repos
unter `D:\AI\`). Sie werden hier von Unity über **glTFast** (`com.unity.cloud.gltfast`) importiert —
volles glTF-PBR → URP-Lit-Mapping (Albedo + Metallic/Roughness automatisch verdrahtet).

> **Nicht in Git versioniert (wie die generierten Scenes):** Die `.glb`-Binaries sind groß (4–6 MB)
> und jederzeit aus der Pipeline reproduzierbar; `.meta` ist projektweit gitignored (committete Art
> hätte instabile GUIDs). Versioniert sind nur die `manifest.json` (glTFast-Dependency) und diese Doku.
> Re-Import: die GLBs aus dem Quellordner unten nach `Assets/_Project/Art/Models/` kopieren, Unity refresht.

## Quelle (Re-Import)

```
Pilot:      D:\AI\3d_output\handwerkerimperium_unity\unity_glb\{worker,workshop,hammer,stool,guild_boss}.glb
Genre-Kern: D:\AI\3d_output\handwerkerimperium_unity\genre_core\unity_glb\{avatar_hans,customer_npc,workshop_smith}.glb
```

> **Hunyuan-VRAM-Gotcha (wichtig):** Den Hunyuan-Runner NIE parallel zu einer laufenden ComfyUI
> (8188/8189) starten — die belegte VRAM zwingt mmgp das Shape-DiT auf die CPU, die Diffusion
> bricht von ~1,8 it/s auf ~0,005 it/s ein (3 min statt 1 s pro Step → Stunden statt ~3,5 min/Asset).
> Vor Stage 2 alle ComfyUI-Prozesse beenden (volle VRAM). Mit freier VRAM: Shape ~62 s + Textur ~105 s.

Produktionskette (validiert): SDXL-Konzept → Hunyuan3D-2.1 (Shape+PBR) → Blender-Decimate (~12k Tris,
PBR eingebettet) → `unity_glb\`. Setup/Runner → `D:\AI\ComfyUI_workflows\STAGE2_3D_SETUP.md`.

## Pilot-Assets + Rolle in der 3D-Idle-Ausrichtung (GDD §5/§6)

| GLB | Import (verifiziert) | Rolle im 3D-Idle-Loop |
|-----|----------------------|------------------------|
| `worker.glb` | 1 Mesh / 11.999 Tris / 1 Mat / 2 Tex | **Avatar** (laufender Spieler) **oder** Stations-NPC-Arbeiter |
| `workshop.glb` | 1 Mesh / 12.000 Tris / 1 Mat / 2 Tex | **Station/Plot** (Werkstatt-Gebäude) |
| `hammer.glb` | 1 Mesh / 11.999 Tris / 1 Mat / 2 Tex | **Master-Tool**-Prop (Sammler-Regal im Hof) |
| `stool.glb` | 1 Mesh / 12.000 Tris / 1 Mat / 2 Tex | **Trag-Prop / Crafting-Produkt** (Ware über dem Kopf) |
| `guild_boss.glb` | 1 Mesh / 12.000 Tris / 1 Mat / 2 Tex | Deko / optionales Distrikt-Highlight |

## Genre-Kern-Batch (generiert, 3D-Idle-Loop)

Hunyuan3D-2.1 aus eigens generierten SDXL-Konzepten (visuell gesichtet, beste Variante gewählt),
je 1 Mesh / 11.999 Tris / URP-Lit / 2 Tex (volles PBR) — in echtem Unity 6000.4.8f1 importiert + per
Blender-Kontaktblatt qualitäts-geprüft (kohärent rundum, game-ready Toon).

| GLB | Konzept | Rolle |
|-----|---------|-------|
| `avatar_hans.glb` | `avatar_hans_v03` | **Spieler-Avatar** (Meister Hans, voller Körper — rigg-fähig für Idle/Walk/Carry) |
| `customer_npc.glb` | `customer_npc_v01` | **Kunde** am Tresen (distinkte Silhouette zum Avatar) |
| `workshop_smith.glb` | `workshop_smith_v04` | **2. Stationstyp** (Schmiede mit Esse/Schornstein) |

## Stations-Batch: alle 10 Gewerke komplett (GDD §6.1)

Die restlichen 8 Stations-Gebäude, gleiche Kette (Konzept-Skript `station_concepts.py`, Sichtung
mit Regen-Fix für painter/architect — "workshop/studio" kippt sonst in Innenräume → Exterieur-Prompt):

| GLB | Gewerk (Stations-Index) | Charakter |
|-----|--------------------------|-----------|
| `workshop_plumber.glb` | Klempner (1) | blaues Haus, Kupferrohre, Wasserfass |
| `workshop_electrician.glb` | Elektriker (2) | dunkles Holz, Laterne, Antennen-Mast |
| `workshop_painter.glb` | Maler (3) | buntes Schindel-Dach, Farbeimer |
| `workshop_roofer.glb` | Dachdecker (4) | rotes Fachwerk, markantes Schichtdach |
| `workshop_contractor.glb` | Bauunternehmer (5) | zweistöckig, Veranda, Fässer |
| `workshop_architect.glb` | Architekt (6) | helles Studio, Erkerfenster |
| `workshop_general_contractor.glb` | Generalunternehmer (7) | Holz-HQ mit Flagge |
| `workshop_innovation_lab.glb` | Innovationslabor (9) | Kupfer-Glas-Kuppel, Steampunk |

(Schreiner (0) = `workshop.glb`, Meisterschmied (8) = `workshop_smith.glb`.) Alle per
Blender-Kontaktblatt qualitäts-geprüft, je 12k Tris + PBR. Quelle: `…\genre_core\unity_glb\`.

## Trag-Waren je Gewerk (komplett)

`ware_concepts.py` → Hunyuan → Decimate auf **1.200 Tris** (Waren werden zigfach gestapelt —
12k wäre Mobile-Budget-Bruch). Schreinerei nutzt `stool.glb` (Pilot-Produkt). Sichtungs-Lehren:
zusammenhängende Einzel-Objekte wählen (getrennte Teile zerfallen bei Single-View-3D); „roof tiles"
kippt hartnäckig in Häuser-Sheets; ein Asset (Tiegel auf beigem Grund) meshte als Bodenplatte →
Alternativ-Vorlage durch die Kette (Amboss v04).

| GLB | Gewerk | Ware |
|-----|--------|------|
| `ware_plumber.glb` | Klempnerei | Kupfer-Winkelrohr |
| `ware_electrician.glb` | Elektriker | Kabeltrommel |
| `ware_painter.glb` | Malerei | Farbeimer mit Kelle |
| `ware_roofer.glb` | Dachdeckerei | Ziegel-Paket |
| `ware_contractor.glb` | Bauunternehmen | Ziegelstein-Cluster |
| `ware_architect.glb` | Architekturbüro | Bauplan-Bündel |
| `ware_general_contractor.glb` | Generalunternehmer | Bauhelm (auf Stapel) |
| `ware_master_smith.glb` | Meisterschmiede | Amboss mit Hammer |
| `ware_innovation_lab.glb` | Innovationslabor | Zahnrad-Kiste |

## Wahrzeichen (Stadt-Wiederaufbau) + Kunden-Vielfalt (komplett)

`landmark_concepts.py` → gleiche Kette, Wahrzeichen auf **8.000 Tris** (Gebäude-Klasse, nur eines
der beiden Modelle gleichzeitig aktiv), Kunden 12k (Charakter-Klasse). Sichtungs-Lehre: Konzepte
mit Rahmen/Pergola VOR dem Objekt kollabieren bei Single-View-3D zu flachen Dioramen →
freistehende Silhouetten wählen (Brunnen-saniert über die Pavillon-Vorlage neu generiert).

| GLB-Paar | Wahrzeichen (Phasen) |
|----------|----------------------|
| `landmark_fountain_ruined/_restored` | Brunnen (3) — bemoostes Becken → Pavillon-Brunnen |
| `landmark_clocktower_ruined/_restored` | Glockenturm (4) — Moos-Häuschen → Pagoden-Uhrturm |
| `landmark_gate_ruined/_restored` | Stadttor (5) — zerfallenes Steintor → Fachwerk-Tor |

`customer_woman.glb` (Frau mit Brotkorb) + `customer_elder.glb` (Senior mit Stock) = Statisten an
der Tresen-Queue. Ids/Phasen = `LandmarkCatalog` (Domain), Szene-Plots = `GameSceneBuilder.MakeLandmark`.

## Offener Generierungs-Backlog (gleiche Kette, GPU)

**Münze/Geld** + **Upgrade-Pad** + **Plot-Bauzaun** sind besser als **Unity-Primitive**
(Zylinder/Quader + Material) — flach/einfach, Image-to-3D ungeeignet (wie City-Tiles, siehe
`pilot_log.md`). **Auto-Rig-Stufe** (AccuRIG/UniRig) für echte Walk-Cycles statt ToonBob.
