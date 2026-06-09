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
D:\AI\3d_output\handwerkerimperium_unity\unity_glb\{worker,workshop,hammer,stool,guild_boss}.glb
```

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

## Offener Generierungs-Backlog (gleiche Kette, GPU)

Für den vollständigen Loop fehlen noch: dedizierter **Avatar** (rigg-fähig, Idle/Walk/Carry/Hammer),
die übrigen **9 Stations-Typen** (eigenes Modell + eigene Ware je Station), **Kunden-NPC**, **Münz-/
Geld-Prop**, **Upgrade-Pad** + **Plot-Bauzaun**, **Stadt-/Wahrzeichen-Props** (Sanierung). City-Tiles
bleiben Blender-Kit (Image-to-3D ungeeignet, siehe `pilot_log.md`).
