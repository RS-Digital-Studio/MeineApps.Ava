# BomberBlast Visual Upgrade — Dark Fantasy Arcade

## Entscheidungen (validiert 10. März 2026)

- **Stil**: Dark Fantasy Arcade — düster, episch, leuchtende Akzente auf dunklem Grund
- **Modell**: DreamShaper XL Alpha2 (validiert mit 5 Test-Assets, alle "geil")
- **Pipeline**: SDXL txt2img (30 Steps, DPM++ 2M Karras, CFG 7.0) → Lanczos Downscale
- **Hires-Option**: RealESRGAN 4x Pixel-Upscale für Marketing-Assets (validiert, scharf)
- **Stil-Prefix**: `dark fantasy game illustration, dramatic lighting, moody atmosphere, bold vivid glowing accent colors on dark background, high contrast, digital painting, game asset, mystical dark energy, cinematic composition, rich deep shadows`

## Asset-Scope (~164 Assets)

| Phase | Kategorie | Anzahl | Gen-Auflösung | Ziel-Auflösung |
|-------|-----------|--------|---------------|----------------|
| **0** | Splash Screen | 1 | 1536x864 | 1920x1080 |
| **0** | Boss-Portraits | 5 | 1024x1024 | 512x512 |
| **0** | Karten-Illustrationen | 14 | 1024x1360 | 384x512 |
| **0** | Welt-Vorschauen | 10 | 1536x864 | 640x360 |
| **1** | Menü-Hintergründe | 7 | 1536x864 | 960x540 |
| **1** | Enemy-Album | 12 | 1024x1024 | 256x256 |
| **1** | PowerUp-Icons | 12 | 1024x1024 | 256x256 |
| **1** | Liga-Embleme | 5 | 1024x1024 | 256x256 |
| **1** | Achievement-Banner | 5 | 1024x768 | 512x256 |
| **2** | Shop-Upgrade-Icons | 12 | 1024x1024 | 256x256 |
| **2** | Dungeon-Buff-Icons | 16 | 1024x1024 | 128x128 |
| **2** | Dungeon-Raum-Typen | 5 | 1024x768 | 384x256 |
| **2** | Floor-Modifikatoren | 7 | 1024x1024 | 128x128 |
| **2** | Battle Pass Header | 1 | 1536x864 | 960x300 |
| **3** | Spieler-Skin-Portraits | 23 | 1024x1024 | 256x256 |
| **3** | Bomben-Skin-Previews | 14 | 1024x1024 | 256x256 |
| **3** | Explosions-Skin-Previews | 15 | 1024x1024 | 256x256 |
| | **Gesamt** | **164** | | |

3 Varianten pro Asset = 492 Bilder. ~2h Generierungszeit auf RTX 4080.

## Architektur (analog HandwerkerImperium)

- **GameAssetService** (IGameAssetService): LRU-Cache, `Lazy<Task>` Deduplication, `ConcurrentDictionary`
- **PlatformAssetLoader**: Android=`Assets.Open("visuals/{path}")`, Desktop=`AssetLoader.Open(avares://...)`
- **Hybrid-Rendering**: AI-Hintergrund (Layer 1) + Prozedurale Overlays/Effekte (Layer 2)
- **Ordner**: `Assets/visuals/{bosses,cards,worlds,enemies,powerups,menu_bg,league,shop,dungeon,battlepass,achievements,skins}/`
- **Format**: WebP (quality 85), ~2-5 MB gesamt geschätzt
- **csproj**: Shared=`<AvaloniaResource Include="Assets\**" />`, Android=`<AndroidAsset ... Link="..." />`

## Integration (welche Renderer werden modifiziert)

| Renderer | Änderung |
|----------|----------|
| GameRenderer.Bosses | Boss-Portrait als Overlay bei Boss-Intro + HP-Bar |
| HelpIconRenderer | Enemy/Boss/PowerUp/Card Icons durch AI-Art ersetzen |
| LevelSelectVisualization | Welt-Thumbnails durch AI-Vorschauen ersetzen |
| MenuBackgroundRenderer | AI-Hintergründe als Layer unter Partikeln |
| ShopIconRenderer | Shop-Icons durch AI-Art ersetzen |
| AchievementIconRenderer | Kategorie-Banner als Hintergrund |
| DungeonMapRenderer | Raum-Typ-Icons + Buff-Icons durch AI-Art |
| Splash (App.axaml.cs) | AI Splash Screen laden |
| CustomizationService | Skin-Portraits in Profil/Shop |
