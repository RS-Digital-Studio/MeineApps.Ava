# SocialPostGenerator

Konsolen-Tool zum Generieren von Social-Media-Posts und Promo-Bildern für die 8 öffentlichen
Apps (nicht alle 12 Workspace-Apps — `AppRegistry` enthält bewusst nur die produktiv/beta-
verfügbaren Apps ohne private Projekte wie SmartMeasure oder Server-Tools wie BingXBot).

## Build & Verwendung

```bash
# Gesamte Solution (vom Workspace-Root)
dotnet build tools/SocialPostGenerator

# CLI-Befehle
dotnet run --project tools/SocialPostGenerator post HandwerkerImperium x
dotnet run --project tools/SocialPostGenerator post RechnerPlus reddit
dotnet run --project tools/SocialPostGenerator image HandwerkerImperium
dotnet run --project tools/SocialPostGenerator image portfolio
dotnet run --project tools/SocialPostGenerator all

# Ohne Argumente: Interaktiver Modus
dotnet run --project tools/SocialPostGenerator
```

## Struktur

| Datei | Zweck |
|-------|-------|
| `Program.cs` | CLI + interaktiver Modus, Enums (`Platform`, `PostCategory`) |
| `AppRegistry.cs` | 8 App-Definitionen (`AppInfo`-Record), Tester-Link |
| `PostTemplates.cs` | X (280-Zeichen-Anzeige, 1-2 Hashtags) + Reddit (Titel + Body, keine Hashtags) |
| `ImageGenerator.cs` | SkiaSharp Promo-Bilder (1200×675) |
| `VersionDetector.cs` | Liest `ApplicationDisplayVersion` aus Android-`.csproj` |

## Post-Kategorien (6 Stück)

| Enum | Anzeigename | Bedingung |
|------|-------------|-----------|
| `LaunchUpdate` | Launch / Update | alle Apps |
| `FeatureSpotlight` | Feature Spotlight | alle Apps |
| `FreeNoAds` | Free & No Ads | nur wenn `!app.HasAds` |
| `IndieDevStory` | Indie Dev Story | alle Apps |
| `Comparison` | Comparison / Alternative | alle Apps |
| `CallToAction` | Call to Action (Feedback) | alle Apps |

## Bild-Typen

1. **App-Promo-Card** (1200×675) — Icon links + Name + Version-Badge + Kurzbeschreibung + 3 Features + Preis + Plattform-Badges
2. **Portfolio** (1200×675) — 8 Icons im 2×4-Grid + „8 Apps | 3 Platforms | 1 Developer"

## Ausgabe

- Posts: in Zwischenablage kopiert via `TextCopy`; bei fehlender Zwischenablage (CI/SSH) Fallback-Meldung
- Bilder: `Releases/SocialPosts/{AppName}_promo.png` bzw. `Releases/SocialPosts/portfolio.png`
- Screenshot-Vorschläge: `Releases/{AppName}/phone_1.png` (Hero), `phone_2–4.png` (FeatureSpotlight), `feature_graphic.png` (LaunchUpdate)

## Packages

Eigenständiges Tool (`ManagePackageVersionsCentrally=false`), keine Abhängigkeit auf Solution-Libraries:

| Package | Version | Zweck |
|---------|---------|-------|
| `SkiaSharp` | 3.119.2 | Bild-Generierung |
| `SkiaSharp.NativeAssets.Win32` | 3.119.2 | Native-Assets für Windows |
| `TextCopy` | 6.2.1 | Zwischenablage |

## Design-System

- **Midnight Indigo Palette** (`#0B0E1A` Hintergrund, `#4F46E5` Indigo-Bright, `#818CF8` Glow)
- Akzentfarbe pro App aus `AppInfo.AccentColorHex` (in `AppRegistry`)
- Code-Bracket-Ecken als Deko-Element, subtile Rasterlinien im Hintergrund
- Wasserzeichen „RS-Digital" unten rechts

## AppRegistry erweitern

Neue App in `AppRegistry.GetAll()` als `AppInfo`-Record eintragen:

```csharp
new(
    Name:             "MeineApp",
    PackageId:        "com.meineapps.meineapp",
    AccentColorHex:   "#RRGGBB",
    Type:             "kurze englische Beschreibung (für Templates)",
    Price:            "Free + $X.XX ...",
    HasAds:           true,
    KeyFeatures:      ["feature 1", "feature 2", "feature 3", "feature 4", "feature 5"],
    ShortDescription: "Ein Satz für Reddit/X-Body.",
    Tags:             ["hashtag1", "hashtag2"],
    TesterLink:       TesterGroupLink
)
```

Das Portfolio-Bild zeigt maximal 8 Apps (2×4-Grid, `i < 8` im Loop) — bei mehr als 8 Einträgen
muss `ImageGenerator.GeneratePortfolio` angepasst werden.

---

Haupt-CLAUDE.md → Build-Befehle, App-Portfolio-Übersicht, Social-Media-Post-Strategie:
`F:\Meine_Apps_Ava\CLAUDE.md`
