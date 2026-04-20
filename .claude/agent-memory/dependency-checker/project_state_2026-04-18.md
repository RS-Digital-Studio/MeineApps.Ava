---
name: Dependency-Stand 2026-04-18
description: Geprüfte Paketversionen, Vulnerabilities und Update-Status der Solution MeineApps.Ava.sln
type: project
---

Avalonia 11.3.13 in Directory.Packages.props (CLAUDE.md zeigt noch 11.3.12 — veraltet).
CommunityToolkit.Mvvm 8.4.2 (CLAUDE.md zeigt noch 8.4.0 — veraltet).
SkiaSharp 3.119.2 konsistent, alle Varianten (SkiaSharp.Skottie, NativeAssets.*) gleiche Version.
Keine lokalen Versions-Overrides in .csproj (grep-Ergebnis leer).
Kein Versions-Drift.

**Why:** CLAUDE.md-Pakettabelle war vor dem Update auf 11.3.13/8.4.2 nicht angepasst worden.
**How to apply:** CLAUDE.md-Pakettabelle nach jeder Versionserhöhung in Directory.Packages.props mitpflegen.

Bekannte transitive Vulnerabilities (beide suppresst via NoWarn NU1902/NU1903):
- SixLabors.ImageSharp 1.0.4 — transitiv via PdfSharpCore — Severity High+Moderate (7 CVEs)
  - Betroffen: FinanzRechner, HandwerkerRechner, WorkTimePro, SmartMeasure (Shared+Android+Desktop+Tests)
  - Kein direkter User-Bild-Upload — Risiko akzeptiert
- Tmds.DBus.Protocol 0.21.2 — transitiv via Avalonia.Desktop — Severity High (GHSA-xrw6-gwf8-vvr9)
  - Betroffen: alle *.Desktop-Projekte (Linux-IPC-Bibliothek)
  - Fix: kommt mit Avalonia 12 (Tmds.DBus.Protocol ≥ 0.22.0)
- SmartMeasure.Desktop zusätzlich: Tmds.DBus 0.20.0 (Mapsui-transitiv)

Verfügbare Major-Updates (NICHT updaten ohne Migrator-Agent):
- Avalonia 11.3.13 → 12.0.1 (Breaking Changes: Compiled Bindings mandatory, Theme-Änderungen)
- Avalonia.Labs.Controls 11.3.1 → 12.0.0
- Avalonia.Labs.Lottie 11.3.1 → 12.0.0
- Xaml.Behaviors.Avalonia 11.3.9.5 → 12.0.0
- Avalonia.Headless.XUnit 11.3.13 → 12.0.1
- coverlet.collector 8.0.1 → 10.0.0 (Major, nur Tests)

Verfügbare Patch-Updates (sicher):
- Avalonia.Diagnostics 11.3.13 → 11.3.14 (alle Desktop-Projekte, nur Dev-Tool)
- Material.Icons.Avalonia 3.0.0 → 3.0.2 (Patch, sicher)
- Microsoft.NET.Test.Sdk 18.3.0 → 18.4.0 (Minor, nur Tests)

Android-Workload: android 36.1.30/10.0.100 — aktuell (kein Update verfügbar).
