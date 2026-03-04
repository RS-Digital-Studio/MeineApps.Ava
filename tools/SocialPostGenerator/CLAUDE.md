# SocialPostGenerator

Konsolen-Tool zum Generieren von Social-Media-Posts und Promo-Bildern für alle 8 Apps.

## Build & Verwendung

```bash
# Bauen
dotnet build tools/SocialPostGenerator

# Interaktiver Modus
dotnet run --project tools/SocialPostGenerator

# CLI-Befehle
dotnet run --project tools/SocialPostGenerator post HandwerkerImperium x
dotnet run --project tools/SocialPostGenerator post RechnerPlus reddit
dotnet run --project tools/SocialPostGenerator image HandwerkerImperium
dotnet run --project tools/SocialPostGenerator image portfolio
dotnet run --project tools/SocialPostGenerator all
```

## Dateien

| Datei | Zweck |
|-------|-------|
| Program.cs | CLI + interaktiver Modus, Enums (Platform, PostCategory) |
| AppRegistry.cs | 8 App-Definitionen (AppInfo Record), Tester-Link |
| PostTemplates.cs | X (280 Zeichen, 1-2 Hashtags) + Reddit (Titel+Body, keine Hashtags) |
| ImageGenerator.cs | SkiaSharp Promo-Bilder (1200x675) |
| VersionDetector.cs | Liest ApplicationDisplayVersion aus Android .csproj |

## Post-Kategorien (6 Stück)

1. LaunchUpdate - Neue Version / Update
2. FeatureSpotlight - Feature hervorheben
3. FreeNoAds - Nur für werbefreie Apps (RechnerPlus, ZeitManager)
4. IndieDevStory - Solo-Dev / Behind-the-Scenes
5. Comparison - Vergleich / Alternative
6. CallToAction - Feedback / Tester gesucht

## Bild-Typen

1. **App-Promo-Card** (1200x675) - Icon + Name + Version + 3 Features + Preis + Plattformen
2. **Portfolio** (1200x675) - Alle 8 Icons + "8 Apps | 3 Platforms | 1 Developer"

## Ausgabe

- Posts: In Zwischenablage kopiert (TextCopy)
- Bilder: `Releases/SocialPosts/`
- Screenshots: Vorschläge aus `Releases/{AppName}/`

## Packages

- SkiaSharp 3.119.2 + SkiaSharp.NativeAssets.Win32
- TextCopy 6.2.1
- ManagePackageVersionsCentrally=false (eigenständiges Tool)

## Design

- Midnight Indigo Palette (konsistent mit RS-Digital Branding)
- Akzentfarbe pro App aus AppRegistry
- Code-Bracket-Ecken als Deko-Element
