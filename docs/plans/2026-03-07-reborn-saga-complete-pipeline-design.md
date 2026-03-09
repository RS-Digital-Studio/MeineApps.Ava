# RebornSaga: Komplette Pipeline — Design-Dokument

**Datum:** 7. März 2026
**Status:** Genehmigt
**Scope:** Style-LoRA Training + Asset-Generierung + App-Integration + Audio + Lokalisierung + Firebase Delivery + Release

---

## Design-Entscheidungen (vom User genehmigt)

| Frage | Entscheidung | Details |
|-------|-------------|---------|
| Kampf-System | **A: Beibehalten + visuell aufwerten** | AI-Sprites für Gegner, Angriffs-Animationen (Translate+zurück), Element-Skill-Effekte (SkiaSharp), Dodge-Ghosting |
| Overworld-Map | **A: Beibehalten + AI-Hintergründe** | Node-Map bleibt, AI-Region-BGs, AI-Node-Icons statt Kreise |
| Dialog-System | **C: Manga-Panel-Stil + AI-Charakter-Animationen** | Visual Novel als Basis, MangaPanelRenderer für Schlüsselszenen, AI-Sprites mit Blink/Mund/Breathing sichtbar |
| Spielgefühl | **D: Mix Power-Fantasy + Story-Driven** | Stärker werden als Gameplay-Hook, Story als emotionaler Anker (Solo Leveling Rezept) |

## Inhaltsverzeichnis

1. [Bestandsaufnahme](#1-bestandsaufnahme)
2. [Solo Leveling Stil-Definition](#2-solo-leveling-stil-definition)
3. [Style-LoRA Training-Pipeline](#3-style-lora-training-pipeline)
4. [Asset-Generierung (356 Assets)](#4-asset-generierung-356-assets)
5. [App-Integration (Kampf, Map, Dialog)](#5-app-integration)
6. [Audio-Assets](#6-audio-assets)
7. [UI-Lokalisierung](#7-ui-lokalisierung)
8. [Asset Delivery & Release](#8-asset-delivery--release)
9. [Kampf-Verbesserungen (verifiziert)](#9-kampf-verbesserungen)
10. [Dialog-Manga-Panels (verifiziert)](#10-dialog-manga-panels)

---

## Phase 0: Kritischer Bugfix (VOR allem anderen)

### SKMaskFilter Disposal-Bug

**Problem:** `LevelUpOverlay.Cleanup()` und `ChapterUnlockOverlay.Cleanup()` rufen `_glowBlur.Dispose()` auf einem **statischen shared** `SKMaskFilter` auf. Wenn ein anderer Overlay denselben Filter danach nutzt → Crash.

**Fix:** Statische `SKMaskFilter` NICHT in `Cleanup()` disposen — nur bei App-Shutdown. Alternativ: pro-Instanz statt statisch.

**Dateien (5 betroffen):**
- `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/LevelUpOverlay.cs:218`
- `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/ChapterUnlockOverlay.cs:267`
- `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/SystemMessageOverlay.cs:120`
- `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs:1408`
- `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/UI/StatusWindowRenderer.cs:258`

---

## 1. Bestandsaufnahme

### Code-Status: 95% produktionsreif

| Bereich | Status | Details |
|---------|--------|---------|
| 11 Szenen | Kompiliert | Title, Dialogue, Battle, Overworld, Inventory, Shop, Gacha, ClassSelect, Loading, GameOver, CutScene |
| 10 Overlays | Kompiliert | LevelUp, ChapterUnlock, Loot, Achievement, Quest, StatusEffect, Tutorial, Pause, Settings, Notification |
| Rendering | Kompiliert | SpriteCharacterRenderer (Crossfade, Blink, Mouth-Anim), BackgroundCompositor, AnimatedWebPRenderer |
| Services | Kompiliert | 18 Services inkl. AssetDeliveryService (SHA256, Delta, Retry) |
| Story | Kompiliert | 200+ StoryNodes, DialogueScene mit Pose-Support |
| RPG-Systeme | Kompiliert | Combat, Inventory, Equipment, Skills, Gacha, Shop, Quests |

### Was fehlt

| Kategorie | Anzahl | Format | Geschätzte Größe |
|-----------|--------|--------|-------------------|
| Charakter-Sprites (11 Chars) | 147 | WebP 832×1216 | ~20-30 MB |
| Gegner-Sprites | 30 | WebP 832×1216 | ~5-8 MB |
| Szenen-Hintergründe | 14 | WebP 1216×832 | ~3-5 MB |
| Map-Hintergründe | 11 | WebP 1216×832 | ~3-5 MB |
| Map Node-Icons | 8 | WebP 512×512 | ~0.5-1 MB |
| Item-Icons | 58 | WebP 512×512 | ~2-4 MB |
| Audio SFX | 27 | OGG | ~2-3 MB |
| Audio BGM | 9 | OGG | ~15-25 MB |
| **Gesamt** | **~304** | | **~50-80 MB** |

### Was gelöscht werden kann (nach Asset-Verifikation)

- `CharacterParts.cs` — altes prozedurales System
- `FaceRenderer.cs` — altes Gesichts-Compositing
- `BodyRenderer.cs` — alter Body-Builder
- Alle `DrawProcedural*()` Methoden in Renderern

CharacterRenderer bestätigt: Zeile 9 sagt `/// KEIN Fallback auf prozedurales Rendering — ohne Sprites wird nichts gezeichnet.`

---

## 2. Solo Leveling Stil-Definition

### Farbpalette

| Element | Farben | Verwendung |
|---------|--------|------------|
| Primär | Dunkles Blau (#0A1628), Schwarz (#050A14) | Hintergründe, UI-Basis |
| Akzent 1 | Electric Blue (#4A90D9, #6BB5FF) | System-UI, Ränge, HUD |
| Akzent 2 | Gold (#FFD700, #FFA500) | Rare Items, Belohnungen, XP |
| Akzent 3 | Violett (#8B5CF6, #A855F7) | Magie, Boss-Aura, Skills |
| Gefahr | Crimson (#DC2626, #EF4444) | HP-Bars, Damage, Enemies |
| Erfolg | Emerald (#10B981) | Heals, Buffs, Bestätigung |

### Stil-Merkmale (für Prompt-Engineering)

| Bereich | Stil-Beschreibung |
|---------|-------------------|
| **Charaktere** | Anime-Proportionen, scharfe Augen, detaillierte Rüstungen mit metallischem Glanz, dramatische Posen, dunkle Umrisslinien, cel-shading mit weichen Highlights |
| **Waffen** | Überdimensioniert, leuchtende Runen/Gravuren, Partikel-Effekte an Klingen, metallisch-magische Hybride |
| **Gegner** | Monströs, Shadow-Monarch-Ästhetik (dunkle Aura, glühende Augen), Größenvariation, detaillierte Texturen |
| **Hintergründe** | Atmosphärisch, volumetrische Beleuchtung, Dungeon-Atmosphäre (dunkle Höhlen, magische Portale), kontrastreich |
| **Items** | Ikonisch, glühende Konturen nach Seltenheit, metallischer Glanz, magische Partikel |

### Seltenheits-Farben (Items + Waffen)

| Seltenheit | Glow-Farbe | Hintergrund |
|------------|-----------|-------------|
| Common | Grau (#9CA3AF) | Transparent |
| Uncommon | Grün (#10B981) | Leichter Schimmer |
| Rare | Blau (#3B82F6) | Blau-Glow |
| Epic | Violett (#8B5CF6) | Violett-Aura |
| Legendary | Gold (#FFD700) | Gold-Strahlen |

---

## 3. Style-LoRA Training-Pipeline

### Ziel

Ein `sololeveling_style.safetensors` LoRA, das den Solo-Leveling-Kunststil auf ALLE Asset-Typen anwendet (Charaktere, Items, Enemies, Backgrounds). Wird mit Character-LoRAs gestackt.

### Dataset

**Quelle:** `F:\AI\datasets\solo_leveling_arise_assets\` (5068 Dateien, 1732 mit ≥512px)

**Kuratierung (25-30 Bilder):**

| Kategorie | Anzahl | Quelle-Ordner |
|-----------|--------|----------------|
| Charakter-Portraits (HD) | 8 | Characters/ (332 HD-Bilder) |
| Boss/Monster-Art | 5 | Boss - Mobs/ (207 Portraits) |
| Waffen (Detail) | 4 | Weapons/ (112 Bilder) |
| Hintergründe (Szenen) | 5 | Stuff/Backgrounds/ (474 HD) |
| Items/Artefakte | 3 | Artifacts/ (234 Bilder), Gems/ |
| UI-Elemente (Stil-Referenz) | 2-3 | Stuff/Icons/ (312 Icons) |

**Auswahlkriterien:**
- Mindestens 1024×1024 (SDXL-optimiert)
- Klarer Solo-Leveling-Stil (nicht generisch)
- Verschiedene Beleuchtungssituationen
- Keine Duplikate oder zu ähnliche Bilder
- Mix aus Action-Posen und Still-Life

### Automatisches Captioning

**Tool:** WD14 Tagger (kohya_ss Modul, verifiziert vorhanden)

```bash
cd F:\AI\kohya_ss
PYTHONIOENCODING=utf-8 uv run --link-mode=copy --index-strategy unsafe-best-match \
  python finetune/tag_images_by_wd14_tagger.py \
  --onnx --batch_size 4 --caption_extension .txt \
  "F:\AI\datasets\sololeveling_style_training"
```

**Caption-Format:** `sololeveling_style, [WD14-Tags]`
- `keep_tokens=1` → Trigger-Word bleibt immer am Anfang
- Manuelle Nachbearbeitung: Stil-irrelevante Tags entfernen (Character-Namen, Copyright)

### Training-Parameter

| Parameter | Wert | Begründung |
|-----------|------|------------|
| Basis-Modell | Animagine XL 4.0 Opt | Gleich wie alle anderen LoRAs |
| network_dim (Rank) | 32 | Bewährt bei Character-LoRAs |
| network_alpha | 16 | alpha = dim/2 (Standard) |
| learning_rate | 3e-4 | Stil-LoRA braucht höhere LR als Charakter |
| text_encoder_lr | 0 (deaktiviert) | Stil soll visuell lernen, nicht semantisch |
| unet_lr | 3e-4 | Nur UNet trainieren |
| optimizer | AdamW8bit | Memory-effizient, bewährt |
| scheduler | cosine | Sanftes Decay |
| max_train_epochs | 15 | Mehr als Character (10) weil Style komplexer |
| repeats | 10 | 25 Bilder × 10 = 250 Bilder/Epoche |
| total_steps | ~3750 | 250 × 15 = 3750 Steps |
| noise_offset | 0.05 | Bessere Kontraste (wichtig für Dark Style) |
| min_snr_gamma | 5 | Stabileres Training |
| resolution | 1024×1024 | SDXL native |
| mixed_precision | bf16 | RTX 4080 optimiert |
| cache_latents | True | RAM statt VRAM für Latents |
| cache_latents_to_disk | True | Spart RAM |
| xformers | True | Memory-effiziente Attention |
| shuffle_caption | True | Robusteres Lernen |
| keep_tokens | 1 | Trigger-Word immer vorne |
| Geschätzte Dauer | ~66 Min | RTX 4080, ~1.06s/Step |

### Training-Script

Neues Script: `F:\AI\ComfyUI_workflows\train_style_lora.py`

Verwendet gleiche kohya_ss-Integration wie `train_lora_generic.py`, aber mit Style-spezifischen Parametern (kein text_encoder_lr, höhere LR, mehr Epochen).

### Automatische Qualitätsprüfung

**CLIP-Score:** HuggingFace CLIP (verifiziert verfügbar in ComfyUI Python)
- Generiere 8 Test-Bilder mit Style-LoRA (verschiedene Szenen)
- CLIP-Score gegen SL:A Referenzbilder messen
- Ziel: Cosine Similarity ≥ 0.75

**Blur-Detection:** OpenCV Laplacian Variance
- Threshold: ≥ 100 (unter 100 = verwaschen)

**Visueller Check:** 8 Validierungsprompts (analog zu Character-LoRA-Validierung)

### 2-Layer LoRA-System

```
Nur Style-LoRA (Strength 0.8):
  → Items, Enemies, Backgrounds, Map-Assets, UI-Elemente

Style-LoRA (0.6) + Character-LoRA (0.8):
  → Charakter-Sprites (Stacking)
```

**Wichtig:** Style-LoRA Strength beim Stacking reduzieren (0.6 statt 0.8), damit Character-Features nicht überschrieben werden.

---

## 4. Asset-Generierung (356 Assets)

### Gemeinsame Einstellungen (alle Scripts)

| Parameter | Wert |
|-----------|------|
| Modell | Animagine XL 4.0 Opt |
| Sampler | euler_ancestral |
| Steps | 28 |
| CFG | 5.0 (offiziell für Animagine XL 4.0) |
| Hires Fix | 1.5× Latent Upscale + 15 Steps DPM++ 2M Karras @ 0.25-0.35 Denoise |
| BG-Removal | BiRefNet-general (mask_blur=0, mask_offset=0, refine_foreground=True) |
| Quality-Tags | Am ENDE: `masterpiece, high score, great score, absurdres` |
| Style-LoRA | `sololeveling_style` (Strength je nach Typ) |

### 4.1 Charakter-Sprites (147 Bilder)

**Script:** `generate_game_sprites.py` (existiert, Update mit Style-LoRA nötig)

**Änderungen:**
- Style-LoRA laden (Strength 0.6) + Character-LoRA (Strength 0.8)
- Trigger-Word `sololeveling_style` am Prompt-Anfang
- Auflösung bleibt 832×1216 (Portrait)

**Pro Charakter (11 Chars):**

| Pose | Emotion | Verwendung |
|------|---------|------------|
| standing | neutral | Default, Overworld |
| standing | happy | Dialog (positiv) |
| standing | sad | Dialog (traurig) |
| standing | angry | Dialog (wütend) |
| standing | surprised | Dialog (überrascht) |
| battle_ready | neutral | Kampf-Idle |
| attacking | fierce | Kampf-Angriff |
| defending | focused | Kampf-Verteidigung |
| casting | focused | Skill-Einsatz |
| hurt | pained | Treffer |
| victory | happy | Sieg |
| portrait_close | neutral | Dialog-Portrait |
| portrait_close | happy | Dialog-Portrait |

**Spezial-Posen (charakterspezifisch):**
- Protagonist Schwert: `dual_wield` (2 Schwerter)
- Protagonist Magier: `levitating` (schwebend)
- Protagonist Assassine: `stealth` (Schatten)
- Aria: `healing` (Heilmagie)
- System ARIA: `holographic` (holographisch)

### 4.2 Gegner-Sprites (30 Bilder)

**Script:** `generate_enemies.py` (existiert, Update mit Style-LoRA nötig)

**Änderungen:**
- Style-LoRA (Strength 0.8, ohne Character-LoRA)
- Shadow-Monarch-Ästhetik für Bosse
- `no ground, no floor, no shadow` im Prompt

**Aufschlüsselung:**
- 5 reguläre Gegner (je 1 Sprite)
- 5 Bosse × 5 Phasen (idle, attack, special, hurt, enraged) = 25 Sprites

### 4.3 Szenen-Hintergründe (14 Bilder)

**Script:** `generate_backgrounds.py` (existiert, Update nötig)

**Änderungen:**
- Style-LoRA (Strength 0.8)
- Auflösung 1216×832 (Landscape)
- Atmosphärische Beleuchtung, volumetrisches Licht im Prompt

**Szenen:**
1. Wald-Eingang (Tutorial)
2. Dunkler Dungeon (Kapitel 1)
3. Kristallhöhle (Kapitel 2)
4. Schattenportal (Kapitel 3)
5. Ruinenstadt (Kapitel 4)
6. Vulkanfestung (Kapitel 5)
7. Himmelstempel (Kapitel 6)
8. Abgrund (Kapitel 7)
9. Thronraum (Final Boss)
10. Friedliche Stadt (Hub)
11. Marktplatz (Shop)
12. Trainingsarena (Battle Tutorial)
13. Gacha-Tempel (Gacha)
14. Bibliothek (Lore/Bestiary)

### 4.4 Map-Assets (19 Bilder)

**Script:** `generate_map.py` (existiert, Update nötig)

**11 Map-Hintergründe (1216×832):**
- Regionen der Overworld (Wald, Sumpf, Berge, Wüste, Vulkan, Eis, Dungeon, Ruinen, Schatten, Himmel, Abgrund)

**8 Node-Icons (512×512, mit BG-Removal):**
- Battle, Boss, Shop, Rest, Event, Treasure, Elite, Portal

### 4.5 Item-Icons (58 Bilder)

**Script:** `generate_items.py` (existiert als v5, Update auf v6 nötig)

**Änderungen:**
- Style-LoRA (Strength 0.8)
- Seltenheits-Glow als SkiaSharp-Overlay (NICHT in Generierung)
- `(single:1.5)` gegen Duplikate
- `still life, weapon focus, no humans, one [item] centered in frame`

**Kategorien:**

| Kategorie | Anzahl | Beispiele |
|-----------|--------|-----------|
| Waffen | 12 | Schwerter, Stäbe, Dolche, Bögen |
| Rüstungen | 10 | Helme, Brustplatten, Roben, Schilde |
| Accessoires | 8 | Ringe, Amulette, Gürtel |
| Verbrauchsgüter | 12 | Tränke, Schriftrollen, Nahrung |
| Materialien | 8 | Erze, Kristalle, Essenzen |
| Schlüssel-Items | 8 | Quest-Items, Dungeon-Keys |

### Script-Update-Strategie

Alle 5 existierenden Scripts bekommen:
1. Style-LoRA Loader-Node im Workflow
2. Trigger-Word `sololeveling_style` am Prompt-Anfang
3. Angepasste LoRA-Strength je nach Typ
4. Versionsbump (v6 für Items, Update-Marker für andere)

Neues Script: `validate_style_lora.py` — generiert 8 Testbilder pro Kategorie

---

## 5. App-Integration

### 5.1 Download-Loading-Screen (NEUE Szene)

**Datei:** `Scenes/AssetDownloadScene.cs`

**Flow:**
1. App startet → `AssetDeliveryService.CheckForUpdatesAsync()`
2. Wenn Assets fehlen/veraltet → `AssetDownloadScene` anzeigen
3. Progress-Bar + animierter Hintergrund (prozedural, kein Asset nötig)
4. Download: Stream mit SHA256-Verifikation (bereits implementiert)
5. Nach Abschluss → `TitleScene`

**UI-Elemente (SkiaSharp):**
- Animierte Partikel (blau/violett, Solo-Leveling-Stil)
- Progress-Bar mit Glow-Effekt
- Aktueller Download-Fortschritt (MB/MB)
- "Erstmaliger Download — bitte WLAN verwenden" Hinweis
- Abbrechen-Button (App funktioniert ohne Assets NICHT)

### 5.2 Hintergrund-Integration (BackgroundCompositor)

**Status:** Grundstruktur existiert, braucht Wiring zu `SpriteCache.GetBackground()`

**Hybrid-System:**
- Layer 1: AI-generierter Hintergrund (DrawBitmap)
- Layer 2: SkiaSharp-Overlays (Partikel, Wetter, dynamische Beleuchtung)
- Layer 3: UI-HUD (DrawText, Bars, Buttons)

**Szenen-Mapping:**

| Szene | Hintergrund-Key | Overlay |
|-------|----------------|---------|
| TitleScene | "title" | Partikel, Logo-Glow |
| DialogueScene | story-abhängig | Kein |
| BattleScene | gegner-abhängig | Kampf-Effekte |
| OverworldScene | region-abhängig | Wolken, Nebel |
| InventoryScene | "library" | Schwebende Partikel |
| ShopScene | "marketplace" | Licht-Rays |
| GachaScene | "gacha_temple" | Magische Kreise |

### 5.3 Item-Icons in InventoryScene

**Aktuell:** Placeholder-Rechtecke mit Seltenheits-Farbe
**Neu:** AI-generierte Icons + SkiaSharp-Seltenheits-Overlay

```
[AI Item-Icon 512×512] → DrawBitmap (skaliert auf Grid-Slot)
[SkiaSharp Glow-Border] → DrawRoundRect mit MaskFilter.CreateBlur
[Seltenheits-Sterne] → DrawPath (1-5 Sterne unten)
```

### 5.4 Enemy-Sprites in BattleScene

**Aktuell:** Prozeduraler Gegner (Silhouette + Element-Glow + Augen + Hörner) — absichtlich so designed
**Neu:** AI-Sprites (5 Phasen pro Boss)

**Änderungen in BattleScene.cs:**
- `DrawEnemy()`: AI-Sprite statt prozedural
- Boss-Phasen-Wechsel: Crossfade zwischen Sprites (150ms)
- Treffer-Feedback: Flash-Overlay + Shake (bereits implementiert)
- Enraged-Phase: Rot-Tint-Overlay auf Sprite

### 5.5 Map-Assets in OverworldScene

**Aktuell:** Prozeduraler Hintergrund + Node-Dots
**Neu:** AI-Region-Hintergründe + AI-Node-Icons

**Änderungen:**
- Region-Hintergrund wechselt bei Kamera-Pan (Crossfade)
- Node-Icons (Battle/Boss/Shop etc.) statt farbige Kreise
- Pfade bleiben prozedural (Bezier-Kurven) — passen sich dynamisch an

### 5.6 Altes prozedurales System entfernen

**ERST nach vollständiger Asset-Verifikation auf Android.**

Zu löschen:
- `Rendering/Characters/CharacterParts.cs`
- `Rendering/Characters/FaceRenderer.cs`
- `Rendering/Characters/BodyRenderer.cs`
- Alle `DrawProcedural*()` Methoden
- Ungenutzte Farb-Konstanten für prozedurales Rendering

---

## 6. Audio-Assets

### 6.1 SFX (27 Effekte)

**Format:** OGG Vorbis, Mono, 44.1kHz, ~50-200KB pro Datei
**Quellen:** Freesound.org (CC0), Kenney.nl (Public Domain), SFXR (generiert)

| Kategorie | Effekt | Dateiname |
|-----------|--------|-----------|
| **Kampf** | Schwert-Hieb | sfx_sword_hit.ogg |
| | Magie-Cast | sfx_magic_cast.ogg |
| | Magie-Impact | sfx_magic_impact.ogg |
| | Ausweichen | sfx_dodge.ogg |
| | Kritischer Treffer | sfx_critical.ogg |
| | Schild-Block | sfx_shield_block.ogg |
| | Pfeil-Schuss | sfx_arrow.ogg |
| | Gift-Tick | sfx_poison.ogg |
| | Boss-Erscheinen | sfx_boss_appear.ogg |
| | Treffer (Spieler) | sfx_player_hit.ogg |
| **UI** | Button-Click | sfx_click.ogg |
| | Tab-Wechsel | sfx_tab.ogg |
| | Item-Aufheben | sfx_item_pickup.ogg |
| | Item-Ausrüsten | sfx_equip.ogg |
| | Fehler/Warnung | sfx_error.ogg |
| | Bestätigung | sfx_confirm.ogg |
| | Level-Up | sfx_levelup.ogg |
| | Quest-Abschluss | sfx_quest_complete.ogg |
| | Achievement | sfx_achievement.ogg |
| **Gacha** | Beschwörung Start | sfx_gacha_start.ogg |
| | Ergebnis (Common) | sfx_gacha_common.ogg |
| | Ergebnis (Rare+) | sfx_gacha_rare.ogg |
| | Ergebnis (Legendary) | sfx_gacha_legendary.ogg |
| **Overworld** | Node-Auswahl | sfx_node_select.ogg |
| | Portal-Betreten | sfx_portal.ogg |
| **Dialog** | Typewriter-Tick | sfx_typewriter.ogg |
| | Choice-Auswahl | sfx_choice.ogg |

### 6.2 BGM (9 Tracks)

**Format:** OGG Vorbis, Stereo, 44.1kHz, 128kbps, Loop-fähig
**Quellen:** Freesound (CC0 Loops), OpenGameArt.org, Eigene Komposition (LMMS/GarageBand)

| Track | Dateiname | Stimmung | Szene |
|-------|-----------|----------|-------|
| Titelmusik | bgm_title.ogg | Episch, orchestral | TitleScene |
| Hub/Stadt | bgm_town.ogg | Ruhig, warm | Overworld (Stadt) |
| Erkundung | bgm_exploration.ogg | Mysteriös, ambient | Overworld (Dungeon) |
| Normal-Kampf | bgm_battle.ogg | Intensiv, treibend | BattleScene (Normal) |
| Boss-Kampf | bgm_boss.ogg | Dramatisch, episch | BattleScene (Boss) |
| Shop | bgm_shop.ogg | Leicht, fröhlich | ShopScene |
| Dialog (ernst) | bgm_dialogue_serious.ogg | Spannend, leise | DialogueScene |
| Dialog (emotional) | bgm_dialogue_emotional.ogg | Sanft, melancholisch | DialogueScene |
| Sieg/Ergebnis | bgm_victory.ogg | Triumphierend | GameOver (Sieg) |

### 6.3 Audio-Service (neu zu implementieren)

**Interface:** `IAudioService`

```csharp
public interface IAudioService
{
    Task PlayBgmAsync(string trackName, bool loop = true, float fadeInSeconds = 1.0f);
    Task StopBgmAsync(float fadeOutSeconds = 1.0f);
    Task CrossfadeBgmAsync(string newTrack, float duration = 2.0f);
    void PlaySfx(string sfxName, float volume = 1.0f);
    void SetBgmVolume(float volume);  // 0.0 - 1.0
    void SetSfxVolume(float volume);  // 0.0 - 1.0
    bool IsBgmMuted { get; set; }
    bool IsSfxMuted { get; set; }
}
```

**Implementierung:**
- Android: `MediaPlayer` für BGM (Loop, Crossfade), `SoundPool` für SFX (low-latency)
- Desktop: `NAudio` oder `OpenAL` (Avalonia hat kein Audio-API)
- Volumen-Settings persistent in `IPreferencesService`

### 6.4 Audio in Szenen

| Szene | BGM | SFX |
|-------|-----|-----|
| TitleScene | bgm_title | sfx_click (Buttons) |
| DialogueScene | bgm_dialogue_* | sfx_typewriter, sfx_choice |
| BattleScene | bgm_battle/bgm_boss | Alle Kampf-SFX |
| OverworldScene | bgm_exploration/bgm_town | sfx_node_select, sfx_portal |
| InventoryScene | (BGM weiter) | sfx_equip, sfx_item_pickup |
| ShopScene | bgm_shop | sfx_click, sfx_confirm |
| GachaScene | bgm_title | sfx_gacha_* |

---

## 7. UI-Lokalisierung

### Status Quo

- `AppStrings.resx` + 5 Sprach-Varianten (de/en/es/fr/it/pt): 68 Schlüssel vorhanden
- Viele Szenen-Strings noch hardcodiert

### Hardcodierte Strings (zu migrieren)

| Datei | Strings | Beispiele |
|-------|---------|-----------|
| BattleScene.cs | ~15 | "Angriff", "Ausweichen", "Skill", "Item", "SIEG!", "NIEDERLAGE" |
| InventoryScene.cs | ~10 | Kategorie-Namen, "Ausrüsten", "Ablegen" |
| ShopScene.cs | ~8 | "Kaufen", "Verkaufen", Preisformate |
| OverworldScene.cs | ~5 | Region-Namen, Node-Typen |
| GachaScene.cs | ~6 | "Einzelbeschwörung", "10er-Beschwörung", Ergebnis-Texte |
| Overlays (10) | ~20 | Level-Up-Text, Achievement-Titel, Quest-Updates |
| **Gesamt** | ~64 | Verdoppelung der bestehenden Keys |

### Migrations-Strategie

1. Alle hardcodierten Strings identifizieren
2. Neue Keys in `AppStrings.resx` anlegen (~64 neue Keys → ~132 gesamt)
3. `_localization.GetString("Key")` statt Literal
4. String-Caching beibehalten (gecachte Felder in Szenen)
5. `UpdateLocalizedTexts()` bei Sprachwechsel (analog zu allen anderen Apps)

### Spezialfälle

- **Floating Damage Numbers:** Bleiben Zahlen (keine Lokalisierung nötig)
- **Item-Namen:** Aus `ItemData.json` (bereits lokalisierbar per separater JSON-Datei pro Sprache)
- **Story-Text:** In `StoryData.json` (separates Lokalisierungs-System, nicht AppStrings)

---

## 8. Asset Delivery & Release

### Firebase Storage

**Bucket:** `rebornsaga-671b6.firebasestorage.app` (verifiziert, HTTP 200)
**Status:** Bucket existiert, Service Account Key fehlt (Robert erstellt ihn)

### Asset-Manifest

**Datei:** `asset_manifest.json`

```json
{
  "version": 1,
  "totalSize": 52000000,
  "packs": [
    {
      "name": "core_sprites",
      "files": [
        {
          "path": "sprites/aria/standing_neutral.webp",
          "sha256": "abc123...",
          "size": 145000
        }
      ]
    }
  ]
}
```

**Packs:**
- `core_sprites` — Charakter-Sprites (Pflicht, ~25 MB)
- `enemies` — Gegner-Sprites (~7 MB)
- `backgrounds` — Alle Hintergründe (~8 MB)
- `items` — Item-Icons (~3 MB)
- `map` — Map-Assets (~4 MB)
- `audio_sfx` — Sound-Effekte (~3 MB)
- `audio_bgm` — Musik (~20 MB)

### Delivery-Flow

```
App-Start → CheckForUpdatesAsync()
  ├─ Manifest vorhanden + aktuell → TitleScene
  ├─ Manifest veraltet → Delta-Download (nur geänderte Files)
  └─ Kein Manifest → AssetDownloadScene (Komplett-Download)
       ├─ Stream-Download pro Pack
       ├─ SHA256-Verifikation pro Datei
       ├─ Retry bei Fehler (3×, exponential backoff)
       └─ Fertig → TitleScene
```

### Scripts (existierend + neu)

| Script | Status | Zweck |
|--------|--------|-------|
| `copy_sprites_to_deploy.py` | Existiert | ComfyUI-Output → Deploy-Ordner, PNG→WebP |
| `generate_manifest.py` | Existiert | Manifest mit SHA256 generieren |
| `upload_assets.py` | Existiert | Upload zu Firebase Storage |
| `curate_style_training.py` | NEU | SL:A Bilder kuratieren + kopieren |
| `train_style_lora.py` | NEU | Style-LoRA Training |
| `validate_style_lora.py` | NEU | 8 Testbilder + CLIP-Score |

### Release-Checkliste

1. **Style-LoRA trainieren** und validieren
2. **Alle Assets generieren** (356 Bilder + 36 Audio)
3. **Qualitätsprüfung** (CLIP-Score, Blur-Detection, visueller Check)
4. **Assets kopieren** (`copy_sprites_to_deploy.py`)
5. **Manifest generieren** (`generate_manifest.py`)
6. **Firebase Upload** (`upload_assets.py`)
7. **App-Code finalisieren** (Download-Screen, Audio-Service, Lokalisierung)
8. **Altes prozedurales System entfernen** (erst nach Android-Verifikation)
9. **SKMaskFilter-Bug fixen** (Phase 0)
10. **dotnet build** + **AppChecker**
11. **Version erhöhen** in csproj
12. **Android-Build testen** (physisches Gerät)
13. **AAB erstellen** + in Releases kopieren
14. **CLAUDE.md aktualisieren**

---

## Abhängigkeiten und Reihenfolge

```
Phase 0: SKMaskFilter Bugfix
    ↓
Phase 1: Style-LoRA (curate → caption → train → validate)
    ↓
Phase 2: Assets generieren (sprites, enemies, backgrounds, items, map)
    ↓                ↓ (parallel)
Phase 3a: App-Integration     Phase 3b: Audio beschaffen
    ↓                              ↓
Phase 4: Lokalisierung (hardcoded → resx)
    ↓
Phase 5: Firebase Setup + Upload
    ↓
Phase 6: Build + Test + Release
```

---

## Risiken und Mitigationen

| Risiko | Wahrscheinlichkeit | Mitigation |
|--------|-------------------|------------|
| Style-LoRA übertrainiert | Mittel | Validierung nach 10 + 15 Epochen, bestes wählen |
| ComfyUI OOM bei 356 Bildern | Niedrig (bekannt) | Pagefile vergrößern, Batch-Generierung mit Pausen |
| Firebase Kosten bei vielen Downloads | Niedrig | CDN-Caching, Delta-Updates, Spark-Plan reicht für Start |
| Audio-Lizenz-Probleme | Niedrig | Nur CC0/Public Domain verwenden |
| Style-LoRA + Character-LoRA Interferenz | Mittel | Strength-Tuning (0.6 + 0.8), Validierung pro Kombination |
| Android-Performance (viele Bitmaps) | Mittel | LRU-Cache, Lazy Loading, WebP-Kompression |

---

## 9. Kampf-Verbesserungen (verifiziert)

### Aktueller Zustand (verifiziert aus BattleScene.cs)

- **Gegner:** Prozeduraler Sprite (DrawEnemySprite:443-476) — dunkle Silhouette + pulsierender Element-Glow + 2 leuchtende Augen + Hörner bei Bossen
- **Aktionen:** 2×2 Grid unten (Angriff rot, Ausweichen grün, Skill lila, Item gold)
- **Effekte vorhanden:** Screen-Shake (3 Stufen), BloodSplatter-Partikel, Floating Damage Numbers, Enemy-Flash (weiß 0.3s), Combo-Counter (ab 3 Hits)
- **Boss-Phasen:** HP-Reset + schwarzes Overlay + "Phase X" mit Glitch-Text + SystemGlitch-Partikel (2.5s)
- **Ausweichen:** SPD-basierte Chance (10-50%), Floating "Ausgewichen!" bei Erfolg
- **Skill-Menü:** Overlay-Panel (90% Breite, 35% Höhe), max 6 Skills, MP-Kosten rechts, Element-Tag, grau wenn MP nicht reicht
- **Victory:** Schwarzes Overlay, "SIEG!" gold, +EXP/Gold/Items aufgelistet, "Tippen zum Fortfahren"
- **Defeat:** Rötliches Overlay, "Gefallen..." rot → GameOverOverlay (Revive/Laden)

### Geplante Verbesserungen (Entscheidung A)

**Alle Verbesserungen nutzen existierende Effekt-Systeme. Kein neues Framework nötig.**

#### 9.1 AI Enemy-Sprites statt Prozedural

`DrawEnemySprite()` (Zeile 443-476) ersetzen:
- AI-Sprite via `SpriteCache.GetEnemy(enemyId)` laden
- Boss: 5 Phasen-Sprites (idle, attack, special, hurt, enraged) — automatisch gewechselt
- Fallback: Alter prozeduraler Code bleibt als `DrawProceduralEnemy()` bis Assets verifiziert

#### 9.2 Angriffs-Animation (Spieler)

Wenn Spieler "Angriff" wählt (PlayerAttack-Phase, 0.8s):
- Charakter-Sprite: Kurzer Translate nach vorne (+30px X über 0.2s) + zurück (0.2s)
- Am Umkehrpunkt: Slash-Effekt als 3 weiße Linien diagonal über Gegner (0.15s, fade-out)
- Bestehender BloodSplatter + Screen-Shake + Enemy-Flash bleibt

#### 9.3 Dodge-Ghosting

Wenn Ausweichen erfolgreich:
- Charakter-Sprite: Alpha 50% + Duplikat mit 20% Alpha um 10px versetzt (Nachbild)
- Duration: 0.3s, dann zurück zu normal
- Bestehender grüner Floating-Text "Ausgewichen!" bleibt

#### 9.4 Element-Skill-Effekte (NEU)

6 neue Partikel-Configs in ParticleSystem.cs (analog zu bestehenden 5 Presets):

| Element | Effekt | Farbe | Beschreibung |
|---------|--------|-------|-------------|
| Feuer | FireBurst | #FF4500 | 15 Partikel aufsteigend, Gravity -50 (steigen), Fade |
| Eis | IceShatter | #00BFFF | 12 Partikel nach außen, kleine Kristall-Formen (Quadrat rotiert) |
| Blitz | LightningStrike | #FFD700 | 8 sehr schnelle vertikale Linien (200px/s), 0.1s Lifetime |
| Wind | WindSlash | #7CFC00 | 10 horizontale Bögen, Speed 100px/s, bogenförmig |
| Licht | HolyBurst | #FFFFE0 | 20 strahlenförmig nach außen, langsam (15px/s), lang (2s) |
| Dunkel | ShadowVoid | #8B008B | 12 langsam nach außen, pulsierend + ShrinkOut (Void-Effekt) |

Trigger: In `ExecuteSkillAttack()` nach Damage-Berechnung, Position = Gegner-Mitte.

#### 9.5 SplashArt bei Ultimate-Skills (BEREITS IMPLEMENTIERT)

`SplashArtRenderer.cs` existiert und ist vollständig:
- Fullscreen-Overlay mit Glow + Speed-Lines + Skill-Name
- Trigger: Wenn Skill.Tier == "ULT" in ExecuteSkillAttack()
- Bereits implementiert, nur noch in BattleScene verdrahten

---

## 10. Dialog — Manga-Panels für Schlüsselszenen (verifiziert)

### Aktueller Zustand (verifiziert)

- **DialogueScene:** Klassische Visual Novel (Hintergrund + max 2 Portraits + Textbox + Choices)
- **SpriteCharacterRenderer:** AI-Sprites mit Idle-Breathing (Y-Offset Sinuswelle 1.5Hz/1.5px), Blink (3-5s Intervall, 150ms, separates Overlay), Mund-Animation (3 Frames: geschlossen/offen/weit, 150ms Toggle), Crossfade bei Emotionswechsel (150ms)
- **MangaPanelRenderer:** VOLLSTÄNDIG implementiert (148 Zeilen), Dual (2 Panels) + Triple (3 Panels) mit diagonalen Schnitten. NIRGENDS aufgerufen (Dead Code).
- **GlitchEffect:** VOLLSTÄNDIG implementiert (99 Zeilen), horizontale Verschiebung + RGB-Split + Flicker. NIRGENDS aufgerufen (Dead Code). Keine Dependencies — sofort nutzbar.
- **MangaWipeTransition:** VOLLSTÄNDIG (58 Zeilen), diagonale Wipe-Animation für Szenen-Übergang (0.6s)

### Geplante Integration (Entscheidung C + Charakter-Animationen)

#### 10.1 AI-Charakter-Sprites in Dialogen

Die AI-Sprites sind das Herzstück der Dialoge:
- Jeder Charakter hat eigenes unabhängiges Blinzel-Timing (nicht synchron!)
- Mund bewegt sich beim Sprechen (3 Frames, 150ms Wechsel)
- Sanftes Auf-und-Ab-Atmen (Idle-Breathing)
- Smooth Crossfade bei Emotionswechsel (150ms)
- Overlay-Dateien pro Charakter nötig: `blink.webp`, `mouth_open.webp`, `mouth_wide.webp`
- **147 Charakter-Sprites + 33 Overlays (11 Chars × 3) = 180 Character-Assets**

#### 10.2 MangaPanelRenderer in DialogueScene einbauen

Für dramatische Story-Momente (Schicksals-Wendepunkte, Boss-Intros, Climax-Szenen):

**Implementierung:**
1. Neues Property `MangaPanelMode` (Off/Dual/Triple) in DialogueScene
2. In `Render()`: Branch basierend auf Mode
3. Dual-Panel: Z.B. Protagonist links-oben, Antagonist rechts-unten (Konfrontation)
4. Triple-Panel: 3 Charaktere/Perspektiven gleichzeitig
5. Jedes Panel bekommt eigenen Render-Callback mit Charakter + Hintergrund-Ausschnitt

**Story-Integration:**
- Neues JSON-Feld `"mangaPanel": "dual"` oder `"triple"` in StoryNode
- StoryEngine setzt `DialogueScene.MangaPanelMode` beim Node-Wechsel
- Automatisches Reset auf "Off" nach dem Panel-Node

**Einsatz-Stellen (pro Kapitel 1-2 Manga-Momente):**
- Prolog: Letzte Schlacht (Dual — Protagonist vs. Boss)
- K1: Erwachen (Triple — Protagonist verwirrt, ARIA fragmentiert, Wald-Umgebung)
- K5: Verrats-Szene (Dual — Protagonist vs. Verräter)
- K10: Finale (Triple — Team vereint)

#### 10.3 GlitchEffect bei ARIA-Szenen

GlitchEffect direkt in DialogueScene einbauen:

**Implementierung:**
1. `private readonly GlitchEffect _glitchEffect = new();`
2. In `Update()`: `_glitchEffect.Update(deltaTime);`
3. In `Render()` (nach allem anderen): `_glitchEffect.Render(canvas, bounds);`
4. Trigger: Wenn `line.Character == "ARIA"` → `_glitchEffect.Start(0.7f, 0.8f)`. Für Node-Level: neues `VisualEffects`-Feld (List<string>) in StoryNode

**Verwendung:**
- ARIA-System-Fehler (Intensität 1.0, 1.5s)
- Dimension-Risse (Intensität 0.5, 0.5s)
- Erinnerungs-Flashbacks (Intensität 0.3, 0.3s — subtil)
- FateChange-Events (bereits vorhanden als `OnFateChangeTriggered`)

#### 10.4 Kamera-Effekte in Dialogen

Ohne neue Systeme, nur Canvas-Transformationen:
- **Zoom auf Sprecher:** `canvas.Scale(1.05f)` bei emotionalen Momenten (über 0.5s interpoliert)
- **Leichtes Wackeln:** ScreenShake.Light() bei Erschütterungen
- **Langsamer Zoom-Out:** Scale 1.1→1.0 über 2s bei Szenen-Eröffnung

#### 10.5 Zusammenfassung: Was für Charakter-Assets nötig ist

| Asset-Typ | Pro Charakter | 11 Chars | Format |
|-----------|---------------|----------|--------|
| Pose+Emotion Sprites | 13 Standard + Spezial | 147 | WebP 832×1216 |
| Blink-Overlay | 1 | 11 | WebP (nur Augenpartie) |
| Mouth-Open Overlay | 1 | 11 | WebP (nur Mundpartie) |
| Mouth-Wide Overlay | 1 | 11 | WebP (nur Mundpartie) |
| **Gesamt Character** | | **180** | |

Die Overlays müssen pixelgenau auf die Pose "standing_neutral" passen (gleiche Auflösung, transparenter Rest).
