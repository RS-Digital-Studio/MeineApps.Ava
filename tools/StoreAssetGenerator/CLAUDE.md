# StoreAssetGenerator

Generiert Google Play Store Assets (Icons, Feature Graphics, Screenshots) und X/Twitter
Profil-Assets (Profilbild, Banner) mit SkiaSharp im Midnight-Theme. Unterstützt aktuell
**8 der 12 Avalonia-Apps** plus den RS-Digital X-Profil-Generator.

## Verwendung

```bash
# Alle 8 Apps generieren
dotnet run --project tools/StoreAssetGenerator

# Einzelne App per Name-Filter
dotnet run --project tools/StoreAssetGenerator Finanz
dotnet run --project tools/StoreAssetGenerator Rechner

# X/Twitter-Profil-Assets für RS-Digital
dotnet run --project tools/StoreAssetGenerator XProfile
dotnet run --project tools/StoreAssetGenerator x
```

## Generierte Assets pro App

| Asset | Größe | Dateiname |
|-------|---------|-----------|
| App-Icon | 512×512 px | icon_512.png |
| Feature Graphic | 1024×500 px | feature_graphic.png |
| Phone Screenshots | 1080×2340 px | phone_1.png … phone_N.png |
| Tablet Screenshots | 1200×1920 px | tablet_1.png … tablet_N.png |

Anzahl Screenshots variiert pro App (definiert in der jeweiligen `{App}App.cs`).

## Ausgabeverzeichnis

```
Releases/
  RechnerPlus/
    icon_512.png
    feature_graphic.png
    phone_1.png … phone_6.png
    tablet_1.png … tablet_4.png
  ZeitManager/
    ...
  RS-Digital/        ← XProfile-Ausgabe
    x_profile_400.png
    x_banner_1500x500.png
```

## Architektur

- `Program.cs` — Entry Point, `AppDef`-Record, `Gfx`-Klasse (Midnight-Theme-Farben + alle
  Drawing-Helpers: `RoundRect`, `Circle`, `Text`, `TextC`, `Progress`, `StatusBar`, `TabBar`,
  `StatItem`, `GradientBg`, `IconBg`, `FeatureGraphicBase`, `GenerateScreenshot`, `SavePng`)
- `{AppName}App.cs` — Pro App eine `static class` mit `Create()`, `DrawIcon()`,
  `DrawFeature()`, `DrawPhoneX()`, `DrawTabletX()` — alle als Lambdas in `AppDef` verpackt
- `XProfileGenerator.cs` — RS-Digital X/Twitter-Assets (Midnight-Indigo-Palette, unabhängig
  von `AppDef`)

## App-Akzentfarben (im Generator)

Diese Werte sind die tatsächlich verwendeten `AccentColor`-Werte im Generator — können von
den App-Farbpaletten abweichen (→ Haupt-CLAUDE.md § 4 für die Produktions-Farbpaletten).

| App | Akzentfarbe im Generator |
|-----|--------------------------|
| RechnerPlus | `Primary` (#6366F1 Indigo) |
| ZeitManager | `Cyan` (#22D3EE) |
| FinanzRechner | `Success` (#22C55E Grün) |
| HandwerkerRechner | `Orange` (#FF6D00) |
| FitnessRechner | `Pink` (#E91E63) |
| WorkTimePro | `Teal` (#009688) |
| HandwerkerImperium | `Primary` (#6366F1 Indigo) |
| BomberBlast | `RedAccent` (#FF5252) |

Die `Gfx`-Klasse definiert das Midnight-Theme (Bg `#0F172A`, Surface `#1E293B`, Card
`#334155`, Primary `#6366F1`, …). Alle App-Klassen importieren `Gfx`-Konstanten via
`using static StoreAssetGenerator.Gfx`.

## Abhängigkeiten

- .NET 10.0
- SkiaSharp 3.119.4-preview.1.1
- SkiaSharp.NativeAssets.Win32

Versionen zentral in `Directory.Packages.props`.

---

## Verweise

- [Haupt-CLAUDE.md](../../CLAUDE.md) — Build-Befehle, App-Portfolio, App-Farbpaletten
- Ausgabe: `Releases/{AppName}/` bzw. `Releases/RS-Digital/`
