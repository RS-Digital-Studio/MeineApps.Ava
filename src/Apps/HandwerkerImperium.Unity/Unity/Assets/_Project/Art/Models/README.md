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

## Offener Generierungs-Backlog (gleiche Kette, GPU)

Noch offen: die übrigen **~8 Stations-Typen** (eigenes Modell + eigene Ware je Station),
**Stadt-/Wahrzeichen-Props** (Sanierung). **Münze/Geld** + **Upgrade-Pad** + **Plot-Bauzaun** sind
besser als **Unity-Primitive** (Zylinder/Quader + Material) — flach/einfach, Image-to-3D ungeeignet
(wie City-Tiles, siehe `pilot_log.md`).
