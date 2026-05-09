# Sound-Lizenzen (BomberBlast)

Alle Sounds sind CC0 / Public Domain. Keine Attribution erforderlich.
Letzte Aktualisierung: 09.05.2026 (Phase 17 — Multi-Sample-Pool + Cinematic-Stinger).

> **Hinweis:** Die Audio-Dateien selbst liegen in `BomberBlast.Android/Assets/Sounds/` und werden
> per `AndroidAsset Include="Assets\Sounds\**"` ins APK gepackt. Dieses Verzeichnis enthält nur
> die Spezial-Bomben-SFX und einen LICENSES-Index für die Codebase-Dokumentation. Zur Laufzeit
> werden alle Files aus `BomberBlast.Android/Assets/Sounds/` geladen.

---

## SFX (Pool-Variants — Multi-Sample-Pool, Phase 16/17)

Pool-Variants ermöglichen Anti-Repeat-Logik (Brawl-Stars-Pattern). Bei jedem `PlayPooled(key)`
wird zufällig eine Variant gewählt, nie zweimal direkt hintereinander die gleiche.

| Basis-Key | Variants | Quelle | Beschreibung |
|-----------|---------|--------|--------------|
| `explosion` | a/b/c/d | Kenney Sci-Fi Sounds (`explosionCrunch_000-003`) | Mid-frequency Crunch-Explosion |
| `place_bomb` | a/b/c/d | Kenney Interface Sounds (`drop_001-004`) | Kurzer Drop-Sound |
| `fuse` | a/b/c | Kenney Interface Sounds (`tick_001/002/004`) | Tick-Sequenz |
| `powerup` | a/b/c | Kenney Digital Audio (`powerUp1-3`) | Aufstrebender Sweep |
| `player_death` | a/b | Kenney Digital Audio (`lowDown`, `lowThreeTone`) | Tieffallend |
| `enemy_death` | a/b/c | Kenney Sci-Fi (`impactMetal_000-002`) | Metal-Impact |
| `menu_select` | a/b | Kenney Interface (`select_001/002`) | Soft Click |
| `menu_confirm` | a/b | Kenney Interface (`confirmation_001/002`) | Bestätigender Tone |
| `level_complete` | a/b | Kenney Digital Audio (`zapThreeToneUp`, `threeTone1`) | Aufsteigend |
| `game_over` | a/b | Kenney Digital Audio (`zapThreeToneDown`, `lowDown`) | Abfallend |

## SFX (Single-Sample, ohne Pool)

| Datei | Quelle | Beschreibung |
|-------|--------|--------------|
| `sfx_exit_appear.ogg` | Kenney Digital Audio (`twoTone1`) | Two-Tone-Reveal |
| `sfx_time_warning.wav` / `.ogg` | Kenney Digital Audio (`threeTone2`) | Drei-Ton-Warnung |

## SFX (Spezial-Bomben — bereits seit v2.0.30 vorhanden)

| Datei | Quelle | Lizenz | Beschreibung |
|-------|--------|--------|--------------|
| bomb_ice.ogg | Kenney Impact Sounds | CC0 | Glas-Impact + hochgepitcht (Eis-Kristall) |
| bomb_fire.ogg | Kenney Sci-Fi Sounds | CC0 | thrusterFire + explosionCrunch layered |
| bomb_lightning.ogg | Kenney Sci-Fi + Sonniss | CC0 | laserLarge + Thunder layered |
| bomb_gravity.ogg | Kenney Sci-Fi Sounds | CC0 | lowFrequency_explosion tief gepitcht + forceField reverse |
| bomb_vortex.ogg | Sonniss + Kenney | CC0 | Air-Sound mit Vibrato + forceField |
| bomb_blackhole.ogg | Kenney Sci-Fi Sounds | CC0 | lowFrequency_explosions reversed + tief gepitcht |

## Cinematic-Stinger (Phase 16/17)

Stinger laufen über den Cinematic-Bus und triggern automatisches Music-Ducking (1.5s × 30%).

| Datei | Quelle | Trigger |
|-------|--------|---------|
| `sfx_stinger_combo_mega.ogg` | Kenney Digital Audio (`phaserUp4`) | x5–x9 Combo (MEGA) |
| `sfx_stinger_combo_ultra.ogg` | Kenney Sci-Fi (`laserLarge_002`) | x10+ Combo (ULTRA) |
| `sfx_stinger_boss_reveal.ogg` | Kenney Sci-Fi (`lowFrequency_explosion_001`) | Boss-Auftritt |
| `sfx_stinger_victory.ogg` | Kenney Digital Audio (`powerUp11`) | Level/Victory-Komplettierung |
| `sfx_stinger_defeat.ogg` | Kenney Digital Audio (`lowRandom`) | Game-Over |

---

## Musik (Welt-Themes + Dungeon)

| Datei | Original | Autor | Quelle | Lizenz |
|-------|----------|-------|--------|--------|
| world_forest.ogg | Forest Ambience | TinyWorlds | opengameart.org | CC0 |
| world_industrial.ogg | Factory | (unbekannt) | opengameart.org | CC0 |
| world_cavern.ogg | Cave Theme | (unbekannt) | opengameart.org | CC0 / OGA-BY 3.0 |
| world_sky.ogg | A Legend Will Rise | (unbekannt) | opengameart.org | CC0 |
| world_inferno.ogg | Determined Pursuit | (unbekannt) | opengameart.org | CC0 |
| dungeon.ogg | Hard Dungeon | (unbekannt) | opengameart.org | CC0 |
| music_menu.ogg | Menu Theme | (Bestand) | opengameart.org | CC0 |
| music_gameplay.ogg | Generic Gameplay | (Bestand) | opengameart.org | CC0 |
| music_boss.ogg | Boss Battle | (Bestand) | opengameart.org | CC0 |
| music_victory.ogg | Victory Loop | (Bestand) | opengameart.org | CC0 |

> **Audit-Hinweis (BOMBERBLAST_AAA_AUDIT_2026-05-09 Kapitel 5):** Die Welt-Tracks stammen aus
> heterogenen Open-Game-Art-Bundles und sind als "Asset-Bundle"-erkennbar. Für AAA-Niveau
> wäre ein hauseigener Komponist + adaptive Layering nötig (~6.000–15.000 € Indie-Composer,
> ~30.000–80.000 € etablierter Mobile-Game-Composer). Phase 17 hat die Audio-Pipeline-
> Foundation gebaut (Bus-System, Pool, Spatial, Ducking), aber die Komponisten-Beauftragung
> bleibt explizites externes Budget-Item — siehe Audit-Roadmap.

---

## Quell-Packs

- **Kenney Sci-Fi Sounds**: https://kenney.nl/assets/sci-fi-sounds (CC0, 70 Files)
- **Kenney Impact Sounds**: https://kenney.nl/assets/impact-sounds (CC0)
- **Kenney Interface Sounds**: https://kenney.nl/assets/interface-sounds (CC0)
- **Kenney UI Audio**: https://kenney.nl/assets/ui-audio (CC0)
- **Kenney Digital Audio**: https://kenney.nl/assets/digital-audio (CC0)
- **100 CC0 SFX #2**: https://opengameart.org/content/100-cc0-sfx-2 (CC0)
- **OpenGameArt CC0 Music**: https://opengameart.org/content/cc0-music-0 (CC0)
- **CC0 Fantasy Music & Sounds**: https://opengameart.org/content/cc0-fantasy-music-sounds (CC0)
- **CC0 Cinematic Music**: https://opengameart.org/content/cc0-cinematic-music (CC0)
