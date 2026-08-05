---
name: build-release
description: Build-Konfiguration und Release-Setup des Workspaces - Directory.Build.props/.targets, Android-Signing, Keystore, Full-AOT- und Symbol-Stripping-Begruendungen, D8/DEX-Fix. Verwenden beim Aendern von Build-Settings, beim Erstellen eines Android-Release (AAB) oder wenn ein Android-Build-Fehler auf die Build-Konfiguration zeigt.
---

# Build-Konfiguration & Release

Migriert aus der Root-`CLAUDE.md` (§5), damit die Begruendungen nicht in jeder Session
resident sind. Inhalt unveraendert.

## Directory.Build.props (alle Projekte)

`net10.0` (Default), `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`,
`AvaloniaUseCompiledBindingsByDefault=true`.
`NoWarn` für `NU1902;NU1903` (ImageSharp-CVE, nur transitiv über PdfSharpCore, keine
User-Bild-Verarbeitung). Company/Copyright: RS Digital.

## Directory.Build.targets (nur `*-android`, ausgewertet *nach* den Projektdateien)

| Setting | Wert / Grund |
|---------|--------------|
| Signing | Keystore `Releases\meineapps.keystore`, Alias `meineapps` |
| `AndroidPackageFormat` | `aab` (Play Store erfordert AAB) |
| `AndroidEnableProfiledAot=false` | **Full AOT** — alle Methoden kompiliert, kein JIT-Fallback. Behebt Mono-JIT-Assertion `!ji->async` (z.B. Huawei P30). `UseInterpreter` ist mit AOT inkompatibel (XA0119). |
| Debug-Symbole | Release: `DebugType=embedded` + `AndroidIncludeDebugSymbols=false` → Symbole **nicht** in der AAB (erschwert Reverse-Engineering), aber lokal für Play-Console-Upload erzeugt. Debug: `portable`. |
| D8/DEX | `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` mit `ExcludeAssets=all` (Duplicate-Class-Fix gegen `…Annotation.Android`). |

## Packages (Central Package Management)

Versionen zentral in `Directory.Packages.props` — dort nachsehen statt hier zu spiegeln
(62 `PackageVersion`-Eintraege). Kern-Abhaengigkeiten sind Avalonia 12, Material.Icons.Avalonia,
CommunityToolkit.Mvvm, Xaml.Behaviors.Avalonia, SkiaSharp (+ Skottie), Avalonia.Labs.Lottie,
sqlite-net-pcl; Premium-Android ueber die `Xamarin.*`-Bindings (Ads.Lite, BillingClient,
Play.Review, Games.V2, Firebase.Messaging/.Config); Tests auf der `xunit.v3`-Linie plus
NSubstitute / FluentAssertions / coverlet.collector.

## Release-Befehle

```bash
# Desktop Release
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r win-x64     # bzw. linux-x64
# Android Release (AAB) → bin/Release/net10.0-android/publish/
dotnet publish src/Apps/{App}/{App}.Android -c Release
```

## Keystore

`F:\Meine_Apps_Ava\Releases\meineapps.keystore` · Alias `meineapps` · Passwort `MeineApps2025`
(in `Directory.Build.targets`).

> **Hinweis:** Dieses Passwort liegt im Klartext in einer eingecheckten Datei und damit in der
> Git-History. Perspektivisch in eine gitignorierte Datei oder Umgebungsvariable auslagern.
