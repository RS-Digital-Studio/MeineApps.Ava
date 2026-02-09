# StoreAssetGenerator

Automatischer Google Play Store Asset-Generator fuer alle 8 Apps. Erstellt Icons, Feature Graphics und Screenshots mit SkiaSharp im Midnight-Theme.

## Verwendung

```bash
# Alle 8 Apps generieren
dotnet run --project tools/StoreAssetGenerator

# Nur bestimmte Apps (Filter nach Name)
dotnet run --project tools/StoreAssetGenerator Finanz
dotnet run --project tools/StoreAssetGenerator Rechner
```

## Generierte Assets pro App

| Asset | Groesse | Dateiname |
|-------|---------|-----------|
| App-Icon | 512x512 px | icon_512.png |
| Feature Graphic | 1024x500 px | feature_graphic.png |
| 6 Phone Screenshots | 1080x2340 px | phone_1.png - phone_6.png |
| 4 Tablet Screenshots | 1200x1920 px | tablet_1.png - tablet_4.png |

**Gesamt**: 12 Dateien pro App = 96 Dateien fuer alle 8 Apps

## Ausgabeverzeichnis

```
Releases/
  RechnerPlus/
    icon_512.png
    feature_graphic.png
    phone_1.png ... phone_6.png
    tablet_1.png ... tablet_4.png
  ZeitManager/
    ...
```

## App-spezifische Farben

| App | Akzentfarbe |
|-----|-------------|
| RechnerPlus | Indigo (#3949AB) |
| ZeitManager | Cyan (#22D3EE) |
| FinanzRechner | Gruen (#22C55E) |
| HandwerkerRechner | Orange (#FF6D00) |
| FitnessRechner | Pink (#E91E63) |
| WorkTimePro | Teal (#009688) |
| HandwerkerImperium | Lila (#9C27B0) |
| BomberBlast | Rot (#FF5252) |

## Architektur

- `Program.cs` - Entry Point, App-Registry, CLI-Filter
- `Gfx.cs` - Shared Drawing Helpers (Midnight-Theme Farben, RoundRect, Circle, Text, StatusBar, TabBar, GradientBg etc.)
- `Apps/{AppName}App.cs` - Pro App eine Klasse mit DrawIcon(), DrawFeature(), DrawPhoneX(), DrawTabletX() Methoden

## Abhaengigkeiten

- .NET 10.0
- SkiaSharp 3.119.1
- SkiaSharp.NativeAssets.Win32 3.119.1
